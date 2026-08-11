using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class UIManager : MonoBehaviour
{
    [Header("Data Sources")]
    [SerializeField] private PartyManager partyManager;

    [Header("HUD Views")]
    [SerializeField] private PartyStatusUI partyStatusUI;
    [SerializeField] private ChainSkillPromptUI chainSkillPromptUI;

    [Header("Fallback")]
    [SerializeField] private bool autoFindReferences = true;

    private PartyManager boundPartyManager;
    private PlayerController[] boundMembers = Array.Empty<PlayerController>();
    private SupportPointManager boundSupportPointManager;
    private ChainSkillPromptUI boundChainSkillPromptUI;
    private static UIManager activeInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneManager()
    {
        if (UnityEngine.Object.FindFirstObjectByType<UIManager>() != null)
            return;

        PartyStatusUI partyHud =
            UnityEngine.Object.FindFirstObjectByType<PartyStatusUI>();
        if (partyHud == null)
            return;

        Canvas canvas = partyHud.GetComponentInParent<Canvas>();
        GameObject host = canvas != null ? canvas.gameObject : partyHud.gameObject;

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
            boundSupportPointManager != partyManager.SupportPointManager ||
            boundChainSkillPromptUI != chainSkillPromptUI ||
            !AreSameMembers(boundMembers, partyManager.partyMembers))
        {
            BindDataSources();
        }
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
        partyStatusUI?.RefreshNow();
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

            if (partyStatusUI == null)
                partyStatusUI = FindFirstObjectByType<PartyStatusUI>();

            if (chainSkillPromptUI == null)
                chainSkillPromptUI = FindFirstObjectByType<ChainSkillPromptUI>();
        }

        return partyManager != null && partyStatusUI != null;
    }

    private void BindDataSources()
    {
        UnbindDataSources();

        if (partyManager == null || partyStatusUI == null)
            return;

        boundPartyManager = partyManager;
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

        partyStatusUI.Bind(partyManager);
        foreach (PlayerController member in boundMembers)
        {
            if (member != null)
                partyStatusUI.SetMemberHealth(member, member.CurrentHp, member.CurrentMaxHp);
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
        boundSupportPointManager = null;
        boundChainSkillPromptUI = null;
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
        boundPartyManager?.TryExecuteChainSkill(side, enemy);
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
