using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기존 전투 코드를 침범하지 않고 공개된 전투 상태의 변화를 감지해 프로토타입 SFX를 재생한다.
/// 사운드 이벤트가 전투 데이터에 정식 편입되면 이 감시기는 명시적 이벤트 호출로 교체할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatAudioRuntimeWatcher : MonoBehaviour
{
    private readonly Dictionary<EnemyController, EnemySnapshot> enemySnapshots = new();
    private readonly Dictionary<PlayerController, PlayerSnapshot> playerSnapshots = new();

    private EnemyController[] trackedEnemies = System.Array.Empty<EnemyController>();
    private PlayerController[] trackedPlayers = System.Array.Empty<PlayerController>();
    private PlayerController lastActivePlayer;
    private float nextObjectRefreshTime;

    private struct EnemySnapshot
    {
        public float hp;
        public WarningType warningType;
    }

    private struct PlayerSnapshot
    {
        public IPlayerState state;
        public bool energyReady;
        public bool parryImpactPlayed;
        public float parryImpactTime;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<CombatAudioRuntimeWatcher>() == null)
        {
            GameObject root = new GameObject("Combat Audio Watcher (Runtime)");
            DontDestroyOnLoad(root);
            root.AddComponent<CombatAudioRuntimeWatcher>();
        }

        // 전투 이벤트를 기다리지 않고 씬 진입 즉시 BGM과 오디오 소스를 준비한다.
        CombatAudio.EnsureInitialized();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextObjectRefreshTime)
        {
            RefreshTrackedObjects();
            nextObjectRefreshTime = Time.unscaledTime + 0.75f;
        }

        TrackEnemies();
        TrackPlayers();
    }

    private void RefreshTrackedObjects()
    {
        trackedEnemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        PartyManager party = FindFirstObjectByType<PartyManager>();
        trackedPlayers = party != null && party.partyMembers != null
            ? party.partyMembers
            : FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void TrackEnemies()
    {
        foreach (EnemyController enemy in trackedEnemies)
        {
            if (enemy == null)
                continue;

            WarningType visibleWarning = enemy.VisibleWarningType;

            if (!enemySnapshots.TryGetValue(enemy, out EnemySnapshot snapshot))
            {
                enemySnapshots[enemy] = new EnemySnapshot
                {
                    hp = enemy.CurrentHp,
                    warningType = visibleWarning
                };
                continue;
            }

            if (visibleWarning != WarningType.None && visibleWarning != snapshot.warningType)
                CombatAudio.PlayWarning(visibleWarning);

            if (enemy.CurrentHp < snapshot.hp - 0.001f)
            {
                float maxHp = Mathf.Max(1f, enemy.MaxHp);
                float damageRatio = (snapshot.hp - enemy.CurrentHp) / maxHp;
                CombatAudio.PlayHit(damageRatio >= 0.08f ? 1.3f : 1f);
            }

            snapshot.hp = enemy.CurrentHp;
            snapshot.warningType = visibleWarning;
            enemySnapshots[enemy] = snapshot;
        }
    }

    private void TrackPlayers()
    {
        PlayerController activePlayer = null;

        foreach (PlayerController player in trackedPlayers)
        {
            if (player == null)
                continue;

            if (player.gameObject.activeInHierarchy)
                activePlayer = player;

            if (!playerSnapshots.TryGetValue(player, out PlayerSnapshot snapshot))
            {
                playerSnapshots[player] = new PlayerSnapshot
                {
                    state = player.CurrentState,
                    energyReady = player.IsEnhancedBranchReady
                };
                continue;
            }

            if (!snapshot.energyReady && player.IsEnhancedBranchReady)
                CombatAudio.PlayEnergyReady();

            if (player.CurrentState != snapshot.state)
            {
                if (player.CurrentState == player.HitState)
                    CombatAudio.PlayHit(player.LastHitWasHeavy ? 1.3f : 1f);

                if (player.CurrentState == player.ParryState)
                {
                    snapshot.parryImpactTime = Time.time + player.CharacterData.parryWindUpDuration;
                    snapshot.parryImpactPlayed = false;
                }
            }

            if (player.CurrentState == player.ParryState &&
                !snapshot.parryImpactPlayed &&
                Time.time >= snapshot.parryImpactTime)
            {
                CombatAudio.PlayParry();
                snapshot.parryImpactPlayed = true;
            }

            snapshot.state = player.CurrentState;
            snapshot.energyReady = player.IsEnhancedBranchReady;
            playerSnapshots[player] = snapshot;
        }

        if (lastActivePlayer != null && activePlayer != null && activePlayer != lastActivePlayer)
            CombatAudio.PlaySwitch();

        if (activePlayer != null)
            lastActivePlayer = activePlayer;
    }
}
