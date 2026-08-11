using System;
using UnityEngine;

public enum AssaultBattleState
{
    Waiting,
    Fighting,
    Finished
}

public enum AssaultBattleEndReason
{
    TimeExpired,
    BossDefeated
}

[DisallowMultipleComponent]
public sealed class AssaultBattleController : MonoBehaviour
{
    [Header("Encounter")]
    [SerializeField] private EnemyController boss;
    [SerializeField] private bool deactivateBossWhileWaiting = true;

    [Header("Rules")]
    [SerializeField, Min(1f)] private float battleDuration = 180f;
    [SerializeField, Min(1)] private int maximumDamageScore = 60000;

    [Header("Operation Score")]
    [Tooltip("극한 회피 1회 성공 시 획득하는 조작 점수다.")]
    [SerializeField, Min(0)] private int perfectDodgeScore = 50;
    [Tooltip("패링 지원 1회 성공 시 획득하는 조작 점수다.")]
    [SerializeField, Min(0)] private int defensiveAssistScore = 100;
    [Tooltip("체인 스킬 1회 발동 시 획득하는 조작 점수다.")]
    [SerializeField, Min(0)] private int chainSkillScore = 200;
    [Tooltip("반복 조작만으로 총점이 과도하게 증가하지 않도록 제한하는 최대 조작 점수다.")]
    [SerializeField, Min(0)] private int maximumOperationScore = 5000;

    [Header("Runtime")]
    [SerializeField] private AssaultBattleState state = AssaultBattleState.Waiting;
    [SerializeField] private float remainingTime;
    [SerializeField] private float elapsedTime;
    [SerializeField] private float damageDealt;
    [SerializeField] private int damageScore;
    [SerializeField] private int operationScore;
    [SerializeField] private int currentScore;

    private bool bossEventsSubscribed;
    private bool operationEventsSubscribed;

    public AssaultBattleState State => state;
    public EnemyController Boss => boss;
    public float BattleDuration => battleDuration;
    public float RemainingTime => remainingTime;
    public float ElapsedTime => elapsedTime;
    public float DamageDealt => damageDealt;
    public int DamageScore => damageScore;
    public int OperationScore => operationScore;
    public int CurrentScore => currentScore;
    public int MaximumDamageScore => maximumDamageScore;
    public int MaximumOperationScore => maximumOperationScore;
    public int MaximumTotalScore => maximumDamageScore + maximumOperationScore;

    public float DamageProgressNormalized =>
        boss == null || boss.MaxHp <= 0f
        ? 0f
        : Mathf.Clamp01(damageDealt / boss.MaxHp);

    public event Action BattleStarted;
    public event Action<float> RemainingTimeChanged;
    public event Action<int> ScoreChanged;
    public event Action<AssaultBattleEndReason> BattleFinished;

    private void Awake()
    {
        ResolveBoss();
        PrepareWaitingState();
    }

    private void OnDestroy()
    {
        UnsubscribeBossEvents();
        UnsubscribeOperationEvents();
    }

    private void Update()
    {
        if (state != AssaultBattleState.Fighting)
            return;

        elapsedTime = Mathf.Min(battleDuration, elapsedTime + Time.deltaTime);
        remainingTime = Mathf.Max(0f, battleDuration - elapsedTime);
        RemainingTimeChanged?.Invoke(remainingTime);

        if (remainingTime <= 0f)
            FinishBattle(AssaultBattleEndReason.TimeExpired);
    }

    public void Configure(
        EnemyController bossController,
        float duration,
        int maximumScore,
        bool hideBossWhileWaiting)
    {
        boss = bossController;
        battleDuration = Mathf.Max(1f, duration);
        maximumDamageScore = Mathf.Max(1, maximumScore);
        deactivateBossWhileWaiting = hideBossWhileWaiting;
    }

    public void ConfigureOperationScores(
        int perfectDodge,
        int defensiveAssist,
        int chainSkill,
        int maximumScore)
    {
        perfectDodgeScore = Mathf.Max(0, perfectDodge);
        defensiveAssistScore = Mathf.Max(0, defensiveAssist);
        chainSkillScore = Mathf.Max(0, chainSkill);
        maximumOperationScore = Mathf.Max(0, maximumScore);
    }

    public bool BeginBattle()
    {
        if (state != AssaultBattleState.Waiting)
            return false;

        ResolveBoss();
        if (boss == null)
        {
            Debug.LogError("강습전 시작 실패: 보스가 연결되지 않았습니다.", this);
            return false;
        }

        remainingTime = Mathf.Max(1f, battleDuration);
        elapsedTime = 0f;
        damageDealt = 0f;
        damageScore = 0;
        operationScore = 0;
        currentScore = 0;

        if (!boss.gameObject.activeSelf)
            boss.gameObject.SetActive(true);

        boss.enabled = true;
        EnemyCombatAI combatAI = boss.GetComponent<EnemyCombatAI>();
        if (combatAI != null)
            combatAI.enabled = true;

        SubscribeBossEvents();
        SubscribeOperationEvents();
        state = AssaultBattleState.Fighting;

        BattleStarted?.Invoke();
        RemainingTimeChanged?.Invoke(remainingTime);
        ScoreChanged?.Invoke(currentScore);
        return true;
    }

    public bool FinishBattle(AssaultBattleEndReason reason)
    {
        if (state != AssaultBattleState.Fighting)
            return false;

        state = AssaultBattleState.Finished;

        if (reason == AssaultBattleEndReason.TimeExpired)
            remainingTime = 0f;

        RemainingTimeChanged?.Invoke(remainingTime);
        UnsubscribeBossEvents();
        UnsubscribeOperationEvents();
        StopBossCombat();
        BattleFinished?.Invoke(reason);
        return true;
    }

    private void ResolveBoss()
    {
        if (boss != null)
            return;

        boss = FindFirstObjectByType<EnemyController>(
            FindObjectsInactive.Include);
    }

    private void PrepareWaitingState()
    {
        state = AssaultBattleState.Waiting;
        remainingTime = Mathf.Max(1f, battleDuration);
        elapsedTime = 0f;
        damageDealt = 0f;
        damageScore = 0;
        operationScore = 0;
        currentScore = 0;
        UnsubscribeBossEvents();
        UnsubscribeOperationEvents();

        if (boss == null)
            return;

        EnemyCombatAI combatAI = boss.GetComponent<EnemyCombatAI>();
        if (combatAI != null)
            combatAI.enabled = false;

        boss.InterruptAttack();

        if (deactivateBossWhileWaiting && boss.gameObject.activeSelf)
            boss.gameObject.SetActive(false);
    }

    private void StopBossCombat()
    {
        if (boss == null)
            return;

        boss.InterruptAttack();

        EnemyCombatAI combatAI = boss.GetComponent<EnemyCombatAI>();
        if (combatAI != null)
            combatAI.enabled = false;
    }

    private void SubscribeBossEvents()
    {
        if (boss == null || bossEventsSubscribed)
            return;

        boss.DamageTaken += OnBossDamageTaken;
        boss.Defeated += OnBossDefeated;
        bossEventsSubscribed = true;
    }

    private void UnsubscribeBossEvents()
    {
        if (boss == null || !bossEventsSubscribed)
            return;

        boss.DamageTaken -= OnBossDamageTaken;
        boss.Defeated -= OnBossDefeated;
        bossEventsSubscribed = false;
    }

    private void OnBossDamageTaken(EnemyController damagedBoss, float damage)
    {
        if (state != AssaultBattleState.Fighting ||
            damagedBoss != boss ||
            damage <= 0f)
        {
            return;
        }

        float maximumDamage = Mathf.Max(0f, boss.MaxHp);
        damageDealt = Mathf.Clamp(damageDealt + damage, 0f, maximumDamage);

        damageScore = maximumDamage <= 0f
            ? 0
            : Mathf.RoundToInt(
                damageDealt / maximumDamage * maximumDamageScore);
        damageScore = Mathf.Clamp(damageScore, 0, maximumDamageScore);
        RefreshTotalScore();
    }

    public bool RegisterOperation(CombatOperationType operationType)
    {
        if (state != AssaultBattleState.Fighting ||
            operationScore >= maximumOperationScore)
        {
            return false;
        }

        int awardedScore = operationType switch
        {
            CombatOperationType.PerfectDodge => perfectDodgeScore,
            CombatOperationType.DefensiveAssist => defensiveAssistScore,
            CombatOperationType.ChainSkill => chainSkillScore,
            _ => 0
        };

        if (awardedScore <= 0)
            return false;

        int nextOperationScore = Mathf.Clamp(
            operationScore + awardedScore,
            0,
            maximumOperationScore);
        if (nextOperationScore == operationScore)
            return false;

        operationScore = nextOperationScore;
        RefreshTotalScore();
        return true;
    }

    private void SubscribeOperationEvents()
    {
        if (operationEventsSubscribed)
            return;

        CombatOperationEvents.Performed += OnCombatOperationPerformed;
        operationEventsSubscribed = true;
    }

    private void UnsubscribeOperationEvents()
    {
        if (!operationEventsSubscribed)
            return;

        CombatOperationEvents.Performed -= OnCombatOperationPerformed;
        operationEventsSubscribed = false;
    }

    private void OnCombatOperationPerformed(
        CombatOperationType operationType,
        PlayerController performer)
    {
        RegisterOperation(operationType);
    }

    private void RefreshTotalScore()
    {
        currentScore = Mathf.Max(0, damageScore + operationScore);
        ScoreChanged?.Invoke(currentScore);
    }

    private void OnBossDefeated(EnemyController defeatedBoss)
    {
        if (defeatedBoss == boss)
            FinishBattle(AssaultBattleEndReason.BossDefeated);
    }
}
