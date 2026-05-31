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

    [SerializeField] private ThirdPersonCameraController cameraController;

    [SerializeField] private SupportPointManager supportPointManager;

    void Awake()
    {
        if (partyMembers == null || partyMembers.Length <= 0)
        {
            Debug.LogError("partyMembers error");
            enabled = false;
            return;
        }

        for (int i = 0; i < partyMembers.Length; i++)
        {
            if (partyMembers[i] == null)
            {
                Debug.LogError("partyMember is not available");
                enabled = false;
                return;
            }
        }

    }

    void Update()
    {

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

        switchedPlayer.transform.position = currentPlayer.transform.position;
        switchedPlayer.transform.rotation = currentPlayer.transform.rotation;

        if (cameraController == null || switchedPlayer.CameraFollowTarget == null)
        {
            Debug.LogError("Camera target setting fail");
            return null;
        }

        cameraController.SetTarget(switchedPlayer.CameraFollowTarget);

        currentPlayer.gameObject.SetActive(false);
        switchedPlayer.gameObject.SetActive(true);

        currentIndex = targetIndex;

        return switchedPlayer;
    }

    public void Switch_Nxt()
    {
        if (0 >= partyMembers.Length) return;

        int nextIndex = (currentIndex + 1) % partyMembers.Length;

        PlayerController sourcePlayer = partyMembers[currentIndex];
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
