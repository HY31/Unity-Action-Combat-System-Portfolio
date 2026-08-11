using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// PartyManager의 현재 멤버 순서를 기준으로 활성 캐릭터와 대기 캐릭터의 상태를 표시한다.
/// 초상화·체력·에너지와 강화 스킬 준비 임계점을 한 슬롯 단위로 갱신한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PartyStatusUI : MonoBehaviour
{
    [Serializable]
    public sealed class SlotView
    {
        public CanvasGroup root;
        public Image portrait;
        public Image healthFill;
        public Image energyFill;
        public RectTransform energyThresholdMarker;
        public Text healthText;
    }

    [Serializable]
    public sealed class ResourceView
    {
        public Image decibelFill;
        public Text decibelText;
        public Image[] supportPointPips;
    }


    [Header("Data")]
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private Sprite[] memberPortraits;

    [Header("Views")]
    [SerializeField] private SlotView activeSlot;
    [SerializeField] private SlotView[] reserveSlots;
    [SerializeField] private ResourceView combatResources;

    [Header("Energy State")]
    [SerializeField] private Color energyNormalColor = new Color32(84, 88, 86, 255);
    [SerializeField] private Color energyReadyColor = new Color32(105, 125, 235, 255);
    [SerializeField] private Color energyMarkerNormalColor = new Color32(92, 96, 94, 255);
    [SerializeField] private Color energyMarkerReadyColor = new Color32(225, 25, 48, 255);
    [SerializeField, Range(0f, 1f)] private float fallbackReadyThreshold = 0.5f;

    [Header("Combat Resources")]
    [SerializeField] private Color decibelNormalColor = new Color32(65, 190, 255, 255);
    [SerializeField] private Color decibelReadyColor = new Color32(255, 191, 22, 255);
    [SerializeField] private Color supportPointEmptyColor = new Color32(49, 54, 54, 210);

    private float[] healthNormalized;
    private float[] healthCurrent;
    private float[] healthMaximum;

    private readonly Dictionary<PlayerController, bool> energyReadyStates = new();
    private PlayerController lastActiveMember;
    private bool activeMemberInitialized;

    public void Bind(PartyManager manager, Sprite[] portraits = null)
    {
        partyManager = manager;
        if (portraits != null)
            memberPortraits = portraits;

        EnsureHealthCache(true);
        ConfigureEnergyImages();
        RefreshNow();
    }

    public void Configure(SlotView active, SlotView[] reserves, Sprite[] portraits)
    {
        Configure(active, reserves, portraits, null);
    }

    public void Configure(SlotView active, SlotView[] reserves, Sprite[] portraits, ResourceView resources)
    {
        activeSlot = active;
        reserveSlots = reserves;
        memberPortraits = portraits;
        combatResources = resources;
        ConfigureEnergyImages();
        RefreshNow();
    }

    public void SetMemberHealth(PlayerController member, float current, float maximum)
    {
        // 현재 HP가 PlayerController에 들어가기 전까지 파티 UI가 멤버별 표시값을 임시 보관한다.
        int index = IndexOf(member);
        if (index < 0)
            return;

        EnsureHealthCache();
        healthCurrent[index] = Mathf.Max(0f, current);
        healthMaximum[index] = Mathf.Max(0f, maximum);
        healthNormalized[index] = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        RefreshNow();
    }

    public Sprite GetPortrait(PlayerController member)
    {
        int index = IndexOf(member);
        return index >= 0 && memberPortraits != null && index < memberPortraits.Length
            ? memberPortraits[index]
            : null;
    }

    public Sprite GetNextPortrait()
    {
        return partyManager != null ? GetPortrait(partyManager.GetNextCharacter()) : null;
    }

    public Sprite GetPreviousPortrait()
    {
        return partyManager != null ? GetPortrait(partyManager.GetPreviousCharacter()) : null;
    }

    private void Refresh()
    {
        if (partyManager == null || partyManager.partyMembers == null || partyManager.partyMembers.Length == 0)
            return;

        // PartyManager의 현재 인덱스를 기준으로 활성 슬롯과 다음/이전 대기 슬롯의 순서를 다시 만든다.
        PlayerController active = partyManager.GetCurrentCharacter();
        bool activeChanged = activeMemberInitialized && active != lastActiveMember;
        ApplySlot(activeSlot, active, true);
        ApplyCombatResources(active);

        if (activeChanged)
            PlayActiveSlotTransition();

        lastActiveMember = active;
        activeMemberInitialized = true;

        if (reserveSlots == null)
            return;

        PlayerController[] orderedReserve =
        {
            partyManager.GetNextCharacter(),
            partyManager.GetPreviousCharacter()
        };

        for (int i = 0; i < reserveSlots.Length; i++)
        {
            PlayerController member = i < orderedReserve.Length ? orderedReserve[i] : null;
            ApplySlot(reserveSlots[i], member, false);
        }
    }

    public void RefreshNow()
    {
        EnsureHealthCache();
        Refresh();
    }

    private void ApplySlot(SlotView slot, PlayerController member, bool showHealthText)
    {
        if (slot == null)
            return;

        bool visible = member != null;
        if (slot.root != null)
        {
            slot.root.alpha = visible ? 1f : 0f;
            slot.root.interactable = false;
            slot.root.blocksRaycasts = false;
        }

        if (!visible)
            return;

        int memberIndex = IndexOf(member);
        float hpNormalized = GetHealthNormalized(memberIndex);
        float currentHp = GetCurrentHealth(memberIndex, member);
        float maxHp = GetMaximumHealth(memberIndex, member);

        if (slot.portrait != null)
        {
            Sprite portrait = GetPortrait(member);
            if (portrait != null)
                slot.portrait.sprite = portrait;
        }

        if (slot.healthFill != null)
            slot.healthFill.fillAmount = hpNormalized;

        if (slot.healthText != null)
        {
            slot.healthText.gameObject.SetActive(showHealthText);
            slot.healthText.text = $"{Mathf.RoundToInt(currentHp)} / {Mathf.RoundToInt(maxHp)}";
        }

        float energyNormalized = member.MaxEnergy > 0f
            ? Mathf.Clamp01(member.CurrentEnergy / member.MaxEnergy)
            : 0f;
        float threshold = ResolveEnergyThreshold(member);
        bool enhancedReady = energyNormalized >= threshold;
        bool hadReadyState = energyReadyStates.TryGetValue(member, out bool wasReady);
        energyReadyStates[member] = enhancedReady;

        if (hadReadyState && !wasReady && enhancedReady)
            PlayEnergyReadyPulse(slot);

        // 강화 스킬 진입 에너지를 게이지 마커 위치와 준비 색상에 함께 반영한다.
        if (slot.energyFill != null)
        {
            slot.energyFill.fillAmount = energyNormalized;
            slot.energyFill.color = enhancedReady ? energyReadyColor : energyNormalColor;
        }

        if (slot.energyThresholdMarker != null)
        {
            Vector2 anchor = new Vector2(threshold, 0.5f);
            slot.energyThresholdMarker.anchorMin = anchor;
            slot.energyThresholdMarker.anchorMax = anchor;
            slot.energyThresholdMarker.anchoredPosition = Vector2.zero;

            Image markerImage = slot.energyThresholdMarker.GetComponent<Image>();
            if (markerImage != null)
                markerImage.color = enhancedReady ? energyMarkerReadyColor : energyMarkerNormalColor;
        }
    }
    private void ApplyCombatResources(PlayerController member)
    {
        if (combatResources == null || member == null)
            return;

        float decibelMaximum = Mathf.Max(0f, member.MaxDecibel);
        float decibelNormalized = decibelMaximum > 0f
            ? Mathf.Clamp01(member.CurrentDecibel / decibelMaximum)
            : 0f;
        bool ultimateReady = member.CanUseUltimate;

        if (combatResources.decibelFill != null)
        {
            combatResources.decibelFill.type = Image.Type.Filled;
            combatResources.decibelFill.fillMethod = Image.FillMethod.Horizontal;
            combatResources.decibelFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            combatResources.decibelFill.fillAmount = decibelNormalized;
            combatResources.decibelFill.color = ultimateReady ? decibelReadyColor : decibelNormalColor;
        }

        if (combatResources.decibelText != null)
        {
            combatResources.decibelText.text =
                $"{Mathf.RoundToInt(member.CurrentDecibel)} / {Mathf.RoundToInt(decibelMaximum)}";
            combatResources.decibelText.color = ultimateReady ? decibelReadyColor : Color.white;
        }

        SupportPointManager support = partyManager != null
            ? partyManager.SupportPointManager
            : member.SupportPointManager;
        int currentSupport = support != null ? support.CurrentSupportPoint : 0;
        int maximumSupport = support != null ? support.MaxSupportPoint : 0;

        if (combatResources.supportPointPips == null)
            return;

        for (int i = 0; i < combatResources.supportPointPips.Length; i++)
        {
            Image pip = combatResources.supportPointPips[i];
            if (pip == null)
                continue;

            bool isValidSlot = i < maximumSupport;
            pip.gameObject.SetActive(isValidSlot);
            if (isValidSlot)
                pip.color = i < currentSupport ? decibelReadyColor : supportPointEmptyColor;
        }
    }

    private void PlayActiveSlotTransition()
    {
        if (activeSlot?.root == null)
            return;

        CanvasGroup group = activeSlot.root;
        RectTransform rect = group.transform as RectTransform;

        group.DOKill(false);
        group.alpha = 0.45f;
        group.DOFade(1f, 0.18f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        if (rect == null)
            return;

        rect.DOKill(false);
        rect.localScale = Vector3.one * 0.96f;
        rect.DOScale(Vector3.one, 0.2f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    private static void PlayEnergyReadyPulse(SlotView slot)
    {
        if (slot == null)
            return;

        if (slot.energyFill != null)
        {
            RectTransform fillRect = slot.energyFill.rectTransform;
            fillRect.DOKill(false);
            fillRect.localScale = Vector3.one;
            fillRect.DOPunchScale(
                    new Vector3(0.04f, 0.22f, 0f),
                    0.32f,
                    9,
                    0.55f)
                .SetUpdate(true);
        }

        if (slot.energyThresholdMarker != null)
        {
            RectTransform marker = slot.energyThresholdMarker;
            marker.DOKill(false);
            marker.localScale = Vector3.one;
            marker.DOPunchScale(Vector3.one * 0.35f, 0.3f, 8, 0.5f)
                .SetUpdate(true);
        }
    }
    private float ResolveEnergyThreshold(PlayerController member)
    {
        if (member == null || member.MaxEnergy <= 0f || member.CharacterData == null)
            return fallbackReadyThreshold;

        SkillData enhanced = member.CharacterData.enhancedSkillBranch;
        if (enhanced == null)
            return fallbackReadyThreshold;

        return Mathf.Clamp01(enhanced.requiredEntryEnergy / member.MaxEnergy);
    }

    private void ConfigureEnergyImages()
    {
        ConfigureEnergyImage(activeSlot);

        if (reserveSlots == null)
            return;

        foreach (SlotView slot in reserveSlots)
            ConfigureEnergyImage(slot);
    }

    private static void ConfigureEnergyImage(SlotView slot)
    {
        if (slot?.energyFill == null)
            return;

        slot.energyFill.type = Image.Type.Filled;
        slot.energyFill.fillMethod = Image.FillMethod.Horizontal;
        slot.energyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        slot.energyFill.fillClockwise = true;
    }

    private int IndexOf(PlayerController member)
    {
        if (partyManager == null || partyManager.partyMembers == null || member == null)
            return -1;

        return Array.IndexOf(partyManager.partyMembers, member);
    }

    private void EnsureHealthCache(bool force = false)
    {
        int count = partyManager != null && partyManager.partyMembers != null
            ? partyManager.partyMembers.Length
            : 0;

        if (!force && healthNormalized != null && healthNormalized.Length == count)
            return;

        // 파티 구성이 바뀌면 멤버 수와 같은 크기로 표시용 체력 캐시를 다시 초기화한다.
        healthNormalized = new float[count];
        healthCurrent = new float[count];
        healthMaximum = new float[count];

        for (int i = 0; i < count; i++)
        {
            PlayerController member = partyManager.partyMembers[i];
            float maximum = member != null ? member.CurrentMaxHp : 0f;
            healthNormalized[i] = 1f;
            healthCurrent[i] = maximum;
            healthMaximum[i] = maximum;
        }
    }

    private float GetHealthNormalized(int index)
    {
        return healthNormalized != null && index >= 0 && index < healthNormalized.Length
            ? healthNormalized[index]
            : 1f;
    }

    private float GetCurrentHealth(int index, PlayerController member)
    {
        return healthCurrent != null && index >= 0 && index < healthCurrent.Length
            ? healthCurrent[index]
            : member.CurrentMaxHp;
    }

    private float GetMaximumHealth(int index, PlayerController member)
    {
        return healthMaximum != null && index >= 0 && index < healthMaximum.Length
            ? healthMaximum[index]
            : member.CurrentMaxHp;
    }
}
