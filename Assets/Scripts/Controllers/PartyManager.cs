using UnityEngine;

public enum SupportType
{
    Normal,
    ParrySupport,
    PerfectDodgeSupport
}

public class PartyManager : MonoBehaviour
{
    public PlayerController[] partyMembers;
    private int currentIndex = 0;
    private bool partyDefeatedRaised;
    public event System.Action<PlayerController> ActiveCharacterChanged;
    public event System.Action PartyDefeated;

    [SerializeField] private ThirdPersonCameraController cameraController;

    [SerializeField] private SupportPointManager supportPointManager;
    public SupportPointManager SupportPointManager => supportPointManager;
    [SerializeField, Min(0.5f)] private float parrySpawnDistance = 4.5f;
    [SerializeField, Min(0.5f)] private float chainSkillSpawnDistance = 4.5f;

    void Awake()
    {
        if (partyMembers == null || partyMembers.Length <= 0)
        {
            Debug.LogError("파티 멤버 배열이 올바르지 않습니다.");
            enabled = false;
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            if (partyMembers[i] == null)
            {
                Debug.LogError("사용할 수 없는 파티 멤버가 있습니다.");
                enabled = false;
                return;
            }

            partyMembers[i].Defeated -= OnMemberDefeated;
            partyMembers[i].Defeated += OnMemberDefeated;
        }

    }

    private void OnDestroy()
    {
        if (partyMembers == null)
            return;

        for (int i = 0; i < partyMembers.Length; i++)
        {
            if (partyMembers[i] != null)
                partyMembers[i].Defeated -= OnMemberDefeated;
        }
    }

    private void Start()
    {
        InitializeParty();
    }

    private void InitializeParty()
    {
        if (cameraController == null)
        {
            Debug.LogError("카메라 컨트롤러가 없습니다.");
            return;
        }

        Transform yawPivot = cameraController.YawPivot;

        // 모든 멤버에 공용 전투 참조를 먼저 주입한 뒤 현재 캐릭터 하나만 활성화한다.
        for (int i = 0; i < partyMembers.Length; i++)
        {
            PlayerController member = partyMembers[i];
            if (member == null) continue;

            member.SetRuntimeReferences(this, supportPointManager, yawPivot);
            member.gameObject.SetActive(i == currentIndex);
        }

        PlayerController currentPlayer = GetCurrentCharacter();

        if (currentPlayer == null || currentPlayer.CameraFollowTarget == null)
        {
            Debug.LogError("현재 플레이어의 카메라 대상이 없습니다.");
            return;
        }

        cameraController.SetTarget(currentPlayer.CameraFollowTarget);
        ActiveCharacterChanged?.Invoke(currentPlayer);
    }

    public PlayerController GetCurrentCharacter()
    {
        if (partyMembers == null ||
            currentIndex < 0 ||
            currentIndex >= partyMembers.Length)
        {
            return null;
        }

        return partyMembers[currentIndex];
    }

    public PlayerController GetNextCharacter()
    {
        int nextIndex = FindAvailableMemberIndex(1);
        return nextIndex >= 0 ? partyMembers[nextIndex] : null;
    }

    public PlayerController GetPreviousCharacter()
    {
        int previousIndex = FindAvailableMemberIndex(-1);
        return previousIndex >= 0 ? partyMembers[previousIndex] : null;
    }

    public PlayerController SwitchTo(
        int targetIndex,
        Vector3? switchPosition = null,
        Quaternion? switchRotation = null)
    {
        if (targetIndex < 0 || targetIndex >= partyMembers.Length || targetIndex == currentIndex) return null;

        PlayerController currentPlayer = partyMembers[currentIndex];
        PlayerController switchedPlayer = partyMembers[targetIndex];
        if (currentPlayer == null || switchedPlayer == null || switchedPlayer.IsDefeated)
            return null;

        // 일반 교체는 현재 위치를 사용하고, 패링 교체는 미리 계산한 보스 정면 위치를 사용한다.
        switchedPlayer.transform.SetPositionAndRotation(
            switchPosition ?? currentPlayer.transform.position,
            switchRotation ?? currentPlayer.transform.rotation);

        if (cameraController == null || switchedPlayer.CameraFollowTarget == null)
        {
            Debug.LogError("카메라 대상 설정에 실패했습니다.");
            return null;
        }

        cameraController.SetTarget(switchedPlayer.CameraFollowTarget);

        currentPlayer.gameObject.SetActive(false);
        switchedPlayer.gameObject.SetActive(true);

        currentIndex = targetIndex;
        ActiveCharacterChanged?.Invoke(switchedPlayer);

        return switchedPlayer;
    }

    public bool TryExecuteChainSkill(int side, EnemyController enemy)
    {
        if (enemy == null || partyMembers == null || partyMembers.Length < 2)
            return false;

        int offset = side <= 0 ? -1 : 1;
        int targetIndex = FindAvailableMemberIndex(offset);
        if (targetIndex < 0 || targetIndex == currentIndex)
            return false;

        PlayerController sourcePlayer = partyMembers[currentIndex];

        Vector3 enemyToPlayer = sourcePlayer.transform.position - enemy.transform.position;
        enemyToPlayer.y = 0f;
        if (enemyToPlayer.sqrMagnitude < 0.0001f)
            enemyToPlayer = -enemy.transform.forward;

        Vector3 spawnDirection = enemyToPlayer.normalized;
        Vector3 spawnPosition = enemy.transform.position + spawnDirection * chainSkillSpawnDistance;
        spawnPosition.y = sourcePlayer.transform.position.y;

        Vector3 faceEnemy = enemy.transform.position - spawnPosition;
        faceEnemy.y = 0f;
        Quaternion spawnRotation = faceEnemy.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(faceEnemy.normalized, Vector3.up)
            : sourcePlayer.transform.rotation;

        PlayerController chainPlayer = SwitchTo(targetIndex, spawnPosition, spawnRotation);
        if (chainPlayer == null || chainPlayer.UltimateState == null)
            return false;

        if (!enemy.TryStartChainSkill())
            return false;

        chainPlayer.UltimateState.PrepareChainSkill(enemy.transform);
        chainPlayer.ChangeState(chainPlayer.UltimateState);
        CombatOperationEvents.Report(CombatOperationType.ChainSkill, chainPlayer);
        return true;
    }

    public void Switch_Nxt()
    {
        if (0 >= partyMembers.Length) return;

        int nextIndex = FindAvailableMemberIndex(1);
        if (nextIndex < 0 || nextIndex == currentIndex)
            return;

        PlayerController sourcePlayer = partyMembers[currentIndex];
        // 교체 전에 판정한 적을 보존해야 활성 캐릭터가 바뀐 뒤에도 같은 공격을 끊을 수 있다.
        EnemyController enemy = FindReactionEnemy(partyMembers[currentIndex]);

        SupportType currentSupportType = ResolveSupportType(sourcePlayer, enemy);

        ResolveSwitchPose(
            sourcePlayer,
            enemy,
            currentSupportType,
            out Vector3 switchPosition,
            out Quaternion switchRotation);

        PlayerController nextPlayer = SwitchTo(nextIndex, switchPosition, switchRotation);

        if (nextPlayer == null) return;

        switch (currentSupportType)
        {
            case SupportType.Normal:
                nextPlayer.ChangeState(nextPlayer.LocomotionState);
                break;
            case SupportType.ParrySupport:
                if (enemy != null && supportPointManager != null && supportPointManager.TryUseSupportPoint(1))
                {
                    nextPlayer.ParryState.SetParryTarget(enemy);
                    nextPlayer.ChangeState(nextPlayer.ParryState);
                }
                break;
            case SupportType.PerfectDodgeSupport:
                nextPlayer.DodgeState.SetDodgeType(DodgeType.Perfect);
                nextPlayer.ChangeState(nextPlayer.DodgeState);
                break;
        }
    }

    public void Switch_Pre()
    {
        if (0 >= partyMembers.Length) return;

        int prevIndex = FindAvailableMemberIndex(-1);
        if (prevIndex < 0 || prevIndex == currentIndex)
            return;

        PlayerController sourcePlayer = partyMembers[currentIndex];
        // 다음/이전 교체 모두 현재 캐릭터 기준으로 같은 지원 판정 규칙을 사용한다.
        EnemyController enemy = FindReactionEnemy(partyMembers[currentIndex]);

        SupportType currentSupportType = ResolveSupportType(sourcePlayer, enemy);

        ResolveSwitchPose(
            sourcePlayer,
            enemy,
            currentSupportType,
            out Vector3 switchPosition,
            out Quaternion switchRotation);

        PlayerController previousPlayer = SwitchTo(prevIndex, switchPosition, switchRotation);

        if (previousPlayer == null) return;

        switch (currentSupportType)
        {
            case SupportType.Normal:
                previousPlayer.ChangeState(previousPlayer.LocomotionState);
                break;
            case SupportType.ParrySupport:
                if (enemy != null && supportPointManager != null && supportPointManager.TryUseSupportPoint(1))
                {
                    previousPlayer.ParryState.SetParryTarget(enemy);
                    previousPlayer.ChangeState(previousPlayer.ParryState);
                }
                break;
            case SupportType.PerfectDodgeSupport:
                previousPlayer.DodgeState.SetDodgeType(DodgeType.Perfect);
                previousPlayer.ChangeState(previousPlayer.DodgeState);
                break;
        }
    }

    public EnemyController FindReactionEnemy(PlayerController sourcePlayer)
    {
        return FindNearestReactionEnemy(sourcePlayer, false);
    }

    public EnemyController FindPerfectDodgeEnemy(PlayerController sourcePlayer)
    {
        return FindNearestReactionEnemy(sourcePlayer, true);
    }

    private EnemyController FindNearestReactionEnemy(
        PlayerController sourcePlayer,
        bool includePreHitReactionWindow)
    {
        if (sourcePlayer == null)
            return null;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        EnemyController nearestWarningEnemy = null;
        float nearestSqrDistance = float.MaxValue;

        // 지원 교체는 보이는 워닝을 사용하고, 직접 회피는 워닝 이후 피격 직전 구간까지 허용한다.
        foreach (EnemyController enemy in enemies)
        {
            bool isReactionCandidate = enemy != null && (
                includePreHitReactionWindow
                    ? enemy.CanTriggerPerfectDodge
                    : enemy.IsAnyWarningVisible);

            if (!isReactionCandidate)
                continue;

            float sqrDistance = (enemy.transform.position - sourcePlayer.transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearestWarningEnemy = enemy;
        }

        return nearestWarningEnemy;
    }

    public SupportType ResolveSupportType(PlayerController sourcePlayer, EnemyController enemy)
    {
        if(enemy == null) return SupportType.Normal;

        WarningType visibleWarningType = enemy.VisibleWarningType;

        // 실제로 보이는 워닝 색을 기준으로 지원 타입을 결정한다.
        if(visibleWarningType == WarningType.Yellow
            && supportPointManager != null
            && supportPointManager.HasEnoughSupportPoint(1))
            return SupportType.ParrySupport;

        if (visibleWarningType == WarningType.Red
            || visibleWarningType == WarningType.Yellow
            && supportPointManager != null
            && !supportPointManager.HasEnoughSupportPoint(1))
            return SupportType.PerfectDodgeSupport;

        return SupportType.Normal;
    }

    public void SetPartyControlEnabled(bool controlEnabled)
    {
        if (partyMembers == null)
            return;

        for (int i = 0; i < partyMembers.Length; i++)
        {
            PlayerController member = partyMembers[i];
            if (member != null)
                member.SetCombatControlEnabled(controlEnabled);
        }
    }

    private int FindAvailableMemberIndex(int direction)
    {
        if (partyMembers == null || partyMembers.Length == 0)
            return -1;

        int stepDirection = direction < 0 ? -1 : 1;
        for (int step = 1; step <= partyMembers.Length; step++)
        {
            int index = (currentIndex + stepDirection * step + partyMembers.Length) %
                partyMembers.Length;
            PlayerController member = partyMembers[index];

            if (member != null && !member.IsDefeated)
                return index;
        }

        return -1;
    }

    private void OnMemberDefeated(PlayerController defeatedMember)
    {
        if (partyDefeatedRaised)
            return;

        int nextIndex = FindAvailableMemberIndex(1);
        if (nextIndex < 0)
        {
            partyDefeatedRaised = true;
            PartyDefeated?.Invoke();
            return;
        }

        if (defeatedMember != GetCurrentCharacter())
            return;

        Vector3 switchPosition = defeatedMember.transform.position;
        Quaternion switchRotation = defeatedMember.transform.rotation;
        PlayerController nextPlayer = SwitchTo(
            nextIndex,
            switchPosition,
            switchRotation);

        if (nextPlayer != null)
            nextPlayer.ChangeState(nextPlayer.LocomotionState);
    }

    private void ResolveSwitchPose(
        PlayerController sourcePlayer,
        EnemyController enemy,
        SupportType supportType,
        out Vector3 switchPosition,
        out Quaternion switchRotation)
    {
        switchPosition = sourcePlayer.transform.position;
        switchRotation = sourcePlayer.transform.rotation;

        if (supportType != SupportType.ParrySupport || enemy == null)
            return;

        Vector3 enemyToPlayer = sourcePlayer.transform.position - enemy.transform.position;
        enemyToPlayer.y = 0f;

        if (enemyToPlayer.sqrMagnitude < 0.0001f)
        {
            enemyToPlayer = enemy.transform.forward;
            enemyToPlayer.y = 0f;
        }

        Vector3 spawnDirection = enemyToPlayer.normalized;
        switchPosition = enemy.transform.position + spawnDirection * parrySpawnDistance;
        switchPosition.y = sourcePlayer.transform.position.y;

        Vector3 faceEnemyDirection = enemy.transform.position - switchPosition;
        faceEnemyDirection.y = 0f;

        if (faceEnemyDirection.sqrMagnitude > 0.0001f)
            switchRotation = Quaternion.LookRotation(faceEnemyDirection.normalized, Vector3.up);
    }
}
