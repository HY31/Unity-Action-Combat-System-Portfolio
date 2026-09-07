using System;
using UnityEngine;

/// <summary>
/// 씬의 전투 데이터 원본과 각 HUD 뷰를 연결하고, 캐릭터 교체나 보스 생성 때 바인딩을 갱신한다.
/// 씬 종속 참조가 비어 있으면 비활성 오브젝트까지 검색해 런타임에 복구한다.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour
{
    [Header("Data Sources")]
    [SerializeField] private PartyManager partyManager;

    [Header("HUD Views")]
    [SerializeField] private PartyStatusUI partyStatusUI;
    [SerializeField] private PlayerPartyHudAssemblyPresenter assemblyPartyHUD;
    [SerializeField] private ChainSkillPromptUI chainSkillPromptUI;
    [SerializeField] private EnemyWorldStatusUI enemyStatusUI;
    [SerializeField] private AssaultBattleHUD assaultBattleHUD;
    [SerializeField] private CombatActionHUD combatActionHUD;

    [Header("Fallback")]
    [SerializeField] private bool autoFindReferences = true;

    private PartyManager boundPartyManager;
    private PlayerPartyHudAssemblyPresenter boundAssemblyPartyHUD;
    private PlayerController[] boundMembers = Array.Empty<PlayerController>();
    private SupportPointManager boundSupportPointManager;
    private ChainSkillPromptUI boundChainSkillPromptUI;
    private EnemyController boundEnemy;
    private AssaultBattleController boundAssaultBattleController;
    private static UIManager activeInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneManager()
    {
        if (UnityEngine.Object.FindFirstObjectByType<UIManager>() != null)
            return;

        PartyStatusUI partyHud =
            UnityEngine.Object.FindFirstObjectByType<PartyStatusUI>();
        PlayerPartyHudAssemblyPresenter assemblyHud =
            UnityEngine.Object.FindFirstObjectByType<PlayerPartyHudAssemblyPresenter>();
        if (partyHud == null && assemblyHud == null)
            return;

        Component hud = assemblyHud != null ? (Component)assemblyHud : partyHud;
        Canvas canvas = hud.GetComponentInParent<Canvas>();
        GameObject host = canvas != null ? canvas.gameObject : hud.gameObject;

        // 화면 프리팹에 관리자가 빠져 있어도 런타임 데이터 연결만 자동으로 복구한다.
        host.AddComponent<UIManager>();
    }

    private void OnEnable()
    {
        if (activeInstance != null && activeInstance != this)
        {
            enabled = false;
            return;
        }

        activeInstance = this;
        TryBind();
    }

    private void Start()
    {
        TryBind();
        RefreshAll();
    }

    private void LateUpdate()
    {
        if (!ResolveReferences())
            return;

        if (boundPartyManager != partyManager ||
            boundAssemblyPartyHUD != assemblyPartyHUD ||
            boundSupportPointManager != partyManager.SupportPointManager ||
            boundChainSkillPromptUI != chainSkillPromptUI ||
            boundEnemy != ResolveBattleEnemy() ||
            boundAssaultBattleController != FindFirstObjectByType<AssaultBattleController>() ||
            !AreSameMembers(boundMembers, partyManager.partyMembers))
        {
            BindDataSources();
        }

        // 이벤트를 거치지 않고 값이 바뀌는 외부 시스템까지 반영하도록 마지막에 화면을 동기화한다.
        RefreshAll();
    }

    private void OnDisable()
    {
        UnbindDataSources();
        if (activeInstance == this)
            activeInstance = null;
    }

    public void Bind(PartyManager manager, PartyStatusUI partyHud)
    {
        partyManager = manager;
        partyStatusUI = partyHud;
        BindDataSources();
    }

    public void RefreshAll()
    {
        if (partyStatusUI != null && partyStatusUI.isActiveAndEnabled)
            partyStatusUI.RefreshNow();
        if (assemblyPartyHUD != null && assemblyPartyHUD.isActiveAndEnabled)
            assemblyPartyHUD.RefreshNow();
        combatActionHUD?.RefreshNow();
    }

    public void UseAssemblyPartyHud(PlayerPartyHudAssemblyPresenter hud)
    {
        assemblyPartyHUD = hud;
        partyStatusUI = null;
        if (Application.isPlaying && isActiveAndEnabled) TryBind();
    }

    private void TryBind()
    {
        if (ResolveReferences())
            BindDataSources();
    }

    private bool ResolveReferences()
    {
        if (autoFindReferences)
        {
            if (partyManager == null)
                partyManager = FindFirstObjectByType<PartyManager>();

            if (assemblyPartyHUD == null)
                assemblyPartyHUD = FindFirstObjectByType<PlayerPartyHudAssemblyPresenter>(
                    FindObjectsInactive.Include);

            if (partyStatusUI == null && assemblyPartyHUD == null)
                partyStatusUI = FindFirstObjectByType<PartyStatusUI>(
                    FindObjectsInactive.Include);

            if (chainSkillPromptUI == null)
                chainSkillPromptUI = FindFirstObjectByType<ChainSkillPromptUI>(
                    FindObjectsInactive.Include);

            if (enemyStatusUI == null)
                enemyStatusUI = FindFirstObjectByType<EnemyWorldStatusUI>(
                    FindObjectsInactive.Include);

            if (assaultBattleHUD == null)
                assaultBattleHUD = FindFirstObjectByType<AssaultBattleHUD>(
                    FindObjectsInactive.Include);

            if (combatActionHUD == null)
                combatActionHUD = FindFirstObjectByType<CombatActionHUD>(
                    FindObjectsInactive.Include);

            // 콤보 UI는 평소 CanvasGroup으로 숨기며 GameObject 자체는 활성 상태여야
            // EnemyController의 콤보 요청 이벤트를 OnEnable에서 구독할 수 있다.
            if (chainSkillPromptUI != null && !chainSkillPromptUI.gameObject.activeSelf)
                chainSkillPromptUI.gameObject.SetActive(true);
        }

        return partyManager != null && (partyStatusUI != null || assemblyPartyHUD != null);
    }

    private void BindDataSources()
    {
        UnbindDataSources();

        if (partyManager == null || (partyStatusUI == null && assemblyPartyHUD == null))
            return;

        boundPartyManager = partyManager;
        boundAssemblyPartyHUD = assemblyPartyHUD;
        boundPartyManager.ActiveCharacterChanged += HandleActiveCharacterChanged;
        boundSupportPointManager = partyManager.SupportPointManager;
        if (boundSupportPointManager != null)
            boundSupportPointManager.SupportPointChanged += HandleSupportPointChanged;


        boundChainSkillPromptUI = chainSkillPromptUI;
        if (boundChainSkillPromptUI != null)
            boundChainSkillPromptUI.SelectionConfirmed += HandleChainSkillSelected;

        PlayerController[] members = partyManager.partyMembers;
        if (members != null && members.Length > 0)
        {
            boundMembers = new PlayerController[members.Length];
            Array.Copy(members, boundMembers, members.Length);

            foreach (PlayerController member in boundMembers)
            {
                if (member == null)
                    continue;

                member.EnergyChanged += HandleEnergyChanged;
                member.HealthChanged += HandleHealthChanged;
                member.DecibelChanged += HandleDecibelChanged;
            }
        }

        partyStatusUI?.Bind(partyManager);
        assemblyPartyHUD?.Bind(partyManager);
        combatActionHUD?.Bind(partyManager);
        BindBattleViews();
        foreach (PlayerController member in boundMembers)
        {
            if (member != null)
                partyStatusUI?.SetMemberHealth(member, member.CurrentHp, member.CurrentMaxHp);
        }

        RefreshAll();
    }

    private void UnbindDataSources()
    {
        if (boundPartyManager != null)
            boundPartyManager.ActiveCharacterChanged -= HandleActiveCharacterChanged;


        if (boundSupportPointManager != null)
            boundSupportPointManager.SupportPointChanged -= HandleSupportPointChanged;

        if (boundChainSkillPromptUI != null)
            boundChainSkillPromptUI.SelectionConfirmed -= HandleChainSkillSelected;
        foreach (PlayerController member in boundMembers)
        {
            if (member == null)
                continue;

            member.EnergyChanged -= HandleEnergyChanged;
            member.HealthChanged -= HandleHealthChanged;
            member.DecibelChanged -= HandleDecibelChanged;
        }

        boundPartyManager = null;
        boundAssemblyPartyHUD = null;
        boundSupportPointManager = null;
        boundChainSkillPromptUI = null;
        boundEnemy = null;
        boundAssaultBattleController = null;
        boundMembers = Array.Empty<PlayerController>();
    }

    private void HandleActiveCharacterChanged(PlayerController activeCharacter)
    {
        RefreshAll();
    }

    private void HandleEnergyChanged(PlayerController member)
    {
        RefreshAll();
    }
    private void HandleHealthChanged(PlayerController member)
    {
        if (member != null)
            partyStatusUI?.SetMemberHealth(member, member.CurrentHp, member.CurrentMaxHp);
    }

    private void HandleDecibelChanged(PlayerController member)
    {
        RefreshAll();
    }

    private void HandleSupportPointChanged(SupportPointManager manager)
    {
        RefreshAll();
    }

    private void HandleChainSkillSelected(int side, EnemyController enemy)
    {
        bool executed =
            boundPartyManager != null &&
            boundPartyManager.TryExecuteChainSkill(side, enemy);

        if (!executed)
            enemy?.CancelChainSkillSequence();
    }

    private void BindBattleViews()
    {
        boundAssaultBattleController = FindFirstObjectByType<AssaultBattleController>();
        boundEnemy = ResolveBattleEnemy();

        if (enemyStatusUI != null)
            enemyStatusUI.Bind(boundEnemy);

        if (assaultBattleHUD != null && boundAssaultBattleController != null)
            assaultBattleHUD.Configure(boundAssaultBattleController);
    }

    private static EnemyController ResolveBattleEnemy()
    {
        AssaultBattleController battle = FindFirstObjectByType<AssaultBattleController>();
        if (battle != null && battle.Boss != null)
            return battle.Boss;

        return FindFirstObjectByType<EnemyController>(FindObjectsInactive.Include);
    }

    private static bool AreSameMembers(PlayerController[] left, PlayerController[] right)
    {
        int leftLength = left?.Length ?? 0;
        int rightLength = right?.Length ?? 0;

        if (leftLength != rightLength)
            return false;

        for (int i = 0; i < leftLength; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }
}
