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
    public event System.Action<PlayerController> ActiveCharacterChanged;

    [SerializeField] private ThirdPersonCameraController cameraController;

    [SerializeField] private SupportPointManager supportPointManager;

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
        return partyMembers[currentIndex];
    }

    public PlayerController GetNextCharacter()
    {
        int nextIndex = (currentIndex + 1) % partyMembers.Length;

        return partyMembers[nextIndex];
    }

    public PlayerController GetPreviousCharacter()
    {
        int prevIndex = (currentIndex - 1 + partyMembers.Length) % partyMembers.Length;

        return partyMembers[prevIndex];
    }

    public PlayerController SwitchTo(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= partyMembers.Length || targetIndex == currentIndex) return null;

        PlayerController currentPlayer = partyMembers[currentIndex];
        PlayerController switchedPlayer = partyMembers[targetIndex];

        // 비활성 캐릭터를 같은 전투 위치에 배치해 교체가 공간 이동처럼 보이지 않게 한다.
        switchedPlayer.transform.position = currentPlayer.transform.position;
        switchedPlayer.transform.rotation = currentPlayer.transform.rotation;

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

    public void Switch_Nxt()
    {
        if (0 >= partyMembers.Length) return;

        int nextIndex = (currentIndex + 1) % partyMembers.Length;

        PlayerController sourcePlayer = partyMembers[currentIndex];
        // 교체 전에 판정한 적을 보존해야 활성 캐릭터가 바뀐 뒤에도 같은 공격을 끊을 수 있다.
        EnemyController enemy = FindReactionEnemy(partyMembers[currentIndex]);

        SupportType currentSupportType = ResolveSupportType(sourcePlayer, enemy);

        PlayerController nextPlayer = SwitchTo(nextIndex);

        if (nextPlayer == null) return;

        switch (currentSupportType)
        {
            case SupportType.Normal:
                nextPlayer.ChangeState(nextPlayer.LocomotionState);
                break;
            case SupportType.ParrySupport:
                if (enemy != null && supportPointManager != null && supportPointManager.TryUseSupportPoint(1))
                {
                    enemy.InterruptAttack();
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

        int prevIndex = (currentIndex - 1 + partyMembers.Length) % partyMembers.Length;

        PlayerController sourcePlayer = partyMembers[currentIndex];
        // 다음/이전 교체 모두 현재 캐릭터 기준으로 같은 지원 판정 규칙을 사용한다.
        EnemyController enemy = FindReactionEnemy(partyMembers[currentIndex]);

        SupportType currentSupportType = ResolveSupportType(sourcePlayer, enemy);

        PlayerController previousPlayer = SwitchTo(prevIndex);

        if (previousPlayer == null) return;

        switch (currentSupportType)
        {
            case SupportType.Normal:
                previousPlayer.ChangeState(previousPlayer.LocomotionState);
                break;
            case SupportType.ParrySupport:
                if (enemy != null && supportPointManager != null && supportPointManager.TryUseSupportPoint(1))
                {
                    enemy.InterruptAttack();
                    Debug.Log("패링!");
                    previousPlayer.ChangeState(previousPlayer.ParryState);
                }
                break;
            case SupportType.PerfectDodgeSupport:
                previousPlayer.DodgeState.SetDodgeType(DodgeType.Perfect);
                Debug.Log("극한 회피!");
                previousPlayer.ChangeState(previousPlayer.DodgeState);
                break;
        }
    }

    public EnemyController FindReactionEnemy(PlayerController sourcePlayer)
    {
        Transform target = sourcePlayer.FindAttackTarget(10f, 360f);
        if (target == null) return null;

        EnemyController enemy = target.GetComponent<EnemyController>();
        if (enemy == null) return null;

        if (!enemy.IsInReactionWindow) return null;

        return enemy;
    }

    public SupportType ResolveSupportType(PlayerController sourcePlayer, EnemyController enemy)
    {
        if(enemy == null) return SupportType.Normal;

        // 노란색 공격은 포인트가 있을 때만 패링하며, 부족하면 빨간색 공격과 같은 극한 회피 지원이 된다.
        if(enemy.CurrentWarningType == WarningType.Yellow
            && supportPointManager != null
            && supportPointManager.HasEnoughSupportPoint(1))
            return SupportType.ParrySupport;

        if (enemy.CurrentWarningType == WarningType.Red
            || enemy.CurrentWarningType == WarningType.Yellow
            && supportPointManager != null
            && !supportPointManager.HasEnoughSupportPoint(1))
            return SupportType.PerfectDodgeSupport;

        return SupportType.Normal;
    }
}
