using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private enum EnemyAttackPhase
    {
        None,
        Attack
    }

    [SerializeField] private SupportPointManager supportPointManager;

    public Animator animator;
    public EnemyAttackData[] attackPatterns;
    private EnemyAttackData currentAttack;
    public HitBox attackHitBox;

    [SerializeField] GameObject warningSign_Yellow;
    [SerializeField] GameObject warningSign_Red;

    public WarningType CurrentWarningType => currentAttack != null ? currentAttack.warningType : WarningType.None;

    public bool IsInWarningWindow { get; private set; }
    public bool IsInActiveWindow { get; private set; }
    public bool IsInReactionWindow { get; private set; }

    EnemyAttackPhase phase;

    [SerializeField] private KeyCode triggerKey = KeyCode.R;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            StartAttack();
        }

        if (phase == EnemyAttackPhase.Attack)
        {
            UpdateAttack(animator.GetCurrentAnimatorStateInfo(0));
        }
    }

    private void StartAttack()
    {
        if (attackPatterns == null || attackPatterns.Length == 0) return;

        currentAttack = attackPatterns[Random.Range(0, attackPatterns.Length)];
        phase = EnemyAttackPhase.Attack;
        attackHitBox.SetActive(false);
        animator.CrossFade(currentAttack.attackAnim, 0.05f);
    }

    private void UpdateAttack(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        bool canUseParrySupport =
            supportPointManager != null &&
            supportPointManager.HasEnoughSupportPoint(1);

        bool showWarning = t >= currentAttack.warningStart && t < currentAttack.warningEnd;

        bool showYellow = showWarning &&
            currentAttack.warningType == WarningType.Yellow &&
            canUseParrySupport;

        bool showRed = showWarning && (
            currentAttack.warningType == WarningType.Red ||
            (currentAttack.warningType == WarningType.Yellow && !canUseParrySupport)
            );

        bool shouldHit = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        bool canReaction = t >= currentAttack.reactionStart && t < currentAttack.reactionEnd;

        IsInWarningWindow = showWarning;
        IsInActiveWindow = shouldHit;
        IsInReactionWindow = canReaction;

        attackHitBox.SetActive(shouldHit);
        warningSign_Yellow.SetActive(showYellow);
        warningSign_Red.SetActive(showRed);

        if (t >= 1f)
        {
            if (string.IsNullOrEmpty(currentAttack.endAnim))
            {
                IsInWarningWindow = false;
                IsInActiveWindow = false;
                IsInReactionWindow = false;
                attackHitBox.SetActive(false);  
                currentAttack = null;

                phase = EnemyAttackPhase.None;
                return;
            }

            IsInWarningWindow = false;
            IsInActiveWindow = false;
            IsInReactionWindow = false;
            animator.CrossFade(currentAttack.endAnim, 0.05f);
            attackHitBox.SetActive(false);
            currentAttack = null;
            phase = EnemyAttackPhase.None;
        }
    }

    public void InterruptAttack()
    {
        attackHitBox.SetActive(false);
        warningSign_Yellow.SetActive(false);
        warningSign_Red.SetActive(false);
        IsInWarningWindow = false;
        IsInActiveWindow = false;
        IsInReactionWindow = false;

        if (currentAttack != null && !string.IsNullOrEmpty(currentAttack.endAnim))
            animator.CrossFade(currentAttack.endAnim, 0.05f);

        currentAttack = null;
        phase = EnemyAttackPhase.None;
    }
}
