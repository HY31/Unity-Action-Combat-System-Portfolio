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

    [Header("Fallback")]
    [SerializeField] private bool autoFindReferences = true;

    private PartyManager boundPartyManager;
    private PlayerController[] boundMembers = Array.Empty<PlayerController>();

    private void OnEnable()
    {
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
            !AreSameMembers(boundMembers, partyManager.partyMembers))
        {
            BindDataSources();
        }
    }

    private void OnDisable()
    {
        UnbindDataSources();
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

        PlayerController[] members = partyManager.partyMembers;
        if (members != null && members.Length > 0)
        {
            boundMembers = new PlayerController[members.Length];
            Array.Copy(members, boundMembers, members.Length);

            foreach (PlayerController member in boundMembers)
            {
                if (member != null)
                    member.EnergyChanged += HandleEnergyChanged;
            }
        }

        partyStatusUI.Bind(partyManager);
        RefreshAll();
    }

    private void UnbindDataSources()
    {
        if (boundPartyManager != null)
            boundPartyManager.ActiveCharacterChanged -= HandleActiveCharacterChanged;

        foreach (PlayerController member in boundMembers)
        {
            if (member != null)
                member.EnergyChanged -= HandleEnergyChanged;
        }

        boundPartyManager = null;
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
