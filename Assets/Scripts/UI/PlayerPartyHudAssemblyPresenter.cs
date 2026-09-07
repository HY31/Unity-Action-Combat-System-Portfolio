using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// 조립용 플레이어 HUD 프리팹을 PartyManager의 세 캐릭터 상태와 연결한다.
/// 배치는 프리팹에서 자유롭게 수정할 수 있고, 이 컴포넌트는 값과 상태만 갱신한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class PlayerPartyHudAssemblyPresenter : MonoBehaviour
{
    [Serializable]
    public sealed class SlotView
    {
        public CanvasGroup root;
        public Image portrait;
        public Image healthTrail;
        public Image healthFill;
        public Image energyFill;
        public RectTransform energyThresholdMarker;
        public Text healthText;
        public Image ultimateReadyIndicator;
    }

    private sealed class HealthTrailState
    {
        public float lastHealth;
        public float trailHealth;
        public float releaseAt;
        public int lastAdvancedFrame = -1;
    }

    [Header("Data")]
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private bool findPartyManager = true;
    [SerializeField] private Sprite[] memberPortraits;
    [SerializeField] private Sprite[] memberChainPortraits;

    [Header("Views")]
    [SerializeField] private SlotView activeSlot;
    [SerializeField] private SlotView[] reserveSlots;

    [Header("Combat Resources")]
    [SerializeField] private DecibelHudText decibelHudText;

    [Header("Health Trail")]
    [SerializeField] private Color healthTrailColor = new Color32(224, 38, 32, 255);
    [SerializeField, Min(0f)] private float healthTrailDelay = 0.16f;
    [SerializeField, Min(0.01f)] private float healthTrailFallSpeed = 0.8f;

    [Header("Energy")]
    [SerializeField] private Color energyNormalColor = new Color32(72, 75, 76, 255);
    [SerializeField] private Color energyReadyBlue = new Color32(88, 150, 255, 255);
    [SerializeField] private Color energyReadyPink = new Color32(255, 91, 181, 255);
    [SerializeField] private Color energyReadyPurple = new Color32(151, 91, 255, 255);
    [SerializeField, Min(0.01f)] private float energyReadyColorSpeed = 0.9f;
    [SerializeField] private Color energyMarkerNormalColor = Color.white;
    [SerializeField] private Color energyMarkerReadyColor = new Color32(255, 36, 47, 255);
    [SerializeField, Range(0f, 1f)] private float fallbackReadyThreshold = 0.5f;

    [Header("Defeated")]
    [SerializeField] private Color defeatedPortraitColor = new Color32(78, 78, 78, 255);

    private readonly Dictionary<PlayerController, HealthTrailState> healthTrailStates = new();

    private void Awake()
    {
        ConfigureImages();
    }

    private void OnEnable()
    {
        ConfigureImages();
        ResolvePartyManager();
    }

    private void LateUpdate()
    {
        if (partyManager == null)
            ResolvePartyManager();

        if (partyManager != null)
            Refresh();
    }

    private void OnValidate()
    {
        ConfigureImages();
    }

    public void Configure(
        SlotView configuredActiveSlot,
        SlotView[] configuredReserveSlots,
        Sprite[] configuredPortraits)
    {
        activeSlot = configuredActiveSlot;
        reserveSlots = configuredReserveSlots;
        memberPortraits = configuredPortraits;
        ConfigureImages();
    }

    public void Bind(PartyManager manager)
    {
        partyManager = manager;
        healthTrailStates.Clear();
        RefreshNow();
    }

    public void ConfigureDecibelHud(DecibelHudText view)
    {
        decibelHudText = view;
    }

    public void RefreshNow()
    {
        if (partyManager == null)
            ResolvePartyManager();

        if (partyManager != null)
            Refresh();
    }

    private void ResolvePartyManager()
    {
        if (!findPartyManager || partyManager != null)
            return;

        partyManager = FindFirstObjectByType<PartyManager>(FindObjectsInactive.Include);
    }

    private void Refresh()
    {
        PlayerController active = partyManager.GetCurrentCharacter();
        ApplySlot(activeSlot, active, true);
        if (decibelHudText != null)
            decibelHudText.SetValue(active != null ? active.CurrentDecibel : 0f);

        PlayerController[] orderedReserve =
        {
            GetRelativePartyMember(active, 1),
            GetRelativePartyMember(active, -1)
        };

        if (reserveSlots == null)
            return;

        for (int index = 0; index < reserveSlots.Length; index++)
        {
            PlayerController member = index < orderedReserve.Length
                ? orderedReserve[index]
                : null;
            ApplySlot(reserveSlots[index], member, false);
        }
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

        if (slot.energyThresholdMarker != null)
            slot.energyThresholdMarker.gameObject.SetActive(visible);

        if (slot.healthText != null)
            slot.healthText.gameObject.SetActive(visible && showHealthText);

        if (!visible)
        {
            if (slot.ultimateReadyIndicator != null)
                slot.ultimateReadyIndicator.gameObject.SetActive(false);
            return;
        }

        if (slot.portrait != null)
        {
            Sprite portrait = GetPortrait(member);
            if (portrait != null)
                slot.portrait.sprite = portrait;
            slot.portrait.color = member.IsDefeated ? defeatedPortraitColor : Color.white;
        }

        float health = member.CurrentHpNormalized;
        if (slot.healthFill != null)
            slot.healthFill.fillAmount = health;

        UpdateHealthTrail(slot, member, health);

        if (slot.healthText != null)
        {
            slot.healthText.text =
                $"{Mathf.RoundToInt(member.CurrentHp)} / {Mathf.RoundToInt(member.CurrentMaxHp)}";
        }

        bool enhancedReady = !member.IsDefeated && member.IsEnhancedBranchReady;
        float energy = member.MaxEnergy > 0f
            ? Mathf.Clamp01(member.CurrentEnergy / member.MaxEnergy)
            : 0f;

        if (slot.energyFill != null)
        {
            slot.energyFill.fillAmount = energy;
            slot.energyFill.color = enhancedReady
                ? EvaluateReadyEnergyColor()
                : energyNormalColor;
        }

        if (slot.energyThresholdMarker != null)
        {
            float threshold = ResolveEnergyThreshold(member);
            PositionEnergyThreshold(slot, threshold);

            Image markerImage = slot.energyThresholdMarker.GetComponent<Image>();
            if (markerImage != null)
            {
                markerImage.color = enhancedReady
                    ? energyMarkerReadyColor
                    : energyMarkerNormalColor;
            }
        }

        if (slot.ultimateReadyIndicator != null)
        {
            slot.ultimateReadyIndicator.gameObject.SetActive(
                !member.IsDefeated && member.CanUseUltimate);
        }
    }

    private void UpdateHealthTrail(SlotView slot, PlayerController member, float health)
    {
        if (slot.healthTrail == null)
            return;

        AlignHealthTrail(slot);

        if (!healthTrailStates.TryGetValue(member, out HealthTrailState state))
        {
            state = new HealthTrailState
            {
                lastHealth = health,
                trailHealth = health,
                releaseAt = Time.unscaledTime
            };
            healthTrailStates.Add(member, state);
        }

        if (health < state.lastHealth - 0.0001f)
        {
            state.trailHealth = Mathf.Max(state.trailHealth, state.lastHealth);
            state.releaseAt = Time.unscaledTime + healthTrailDelay;
        }
        else if (health > state.lastHealth + 0.0001f)
        {
            state.trailHealth = health;
            state.releaseAt = Time.unscaledTime;
        }

        if (Time.unscaledTime >= state.releaseAt && state.lastAdvancedFrame != Time.frameCount)
        {
            state.trailHealth = Mathf.MoveTowards(
                state.trailHealth,
                health,
                healthTrailFallSpeed * Time.unscaledDeltaTime);
            state.lastAdvancedFrame = Time.frameCount;
        }

        state.trailHealth = Mathf.Max(health, state.trailHealth);
        state.lastHealth = health;

        slot.healthTrail.color = healthTrailColor;
        slot.healthTrail.fillAmount = state.trailHealth;
    }

    private PlayerController GetRelativePartyMember(PlayerController active, int offset)
    {
        PlayerController[] members = partyManager.partyMembers;
        if (active == null || members == null || members.Length == 0)
            return null;

        int activeIndex = Array.IndexOf(members, active);
        if (activeIndex < 0)
            return null;

        int memberIndex = (activeIndex + offset + members.Length) % members.Length;
        return members[memberIndex];
    }

    private Sprite GetPortrait(PlayerController member)
    {
        if (member == null || partyManager.partyMembers == null || memberPortraits == null)
            return null;

        int memberIndex = Array.IndexOf(partyManager.partyMembers, member);
        return memberIndex >= 0 && memberIndex < memberPortraits.Length
            ? memberPortraits[memberIndex]
            : null;
    }

    public Sprite GetNextChainPortrait()
    {
        ResolvePartyManager();
        return partyManager != null ? GetChainPortrait(partyManager.GetNextCharacter()) : null;
    }

    public Sprite GetPreviousChainPortrait()
    {
        ResolvePartyManager();
        return partyManager != null ? GetChainPortrait(partyManager.GetPreviousCharacter()) : null;
    }

    public void ConfigureChainPortraits(Sprite[] portraits) => memberChainPortraits = portraits;

    private Sprite GetChainPortrait(PlayerController member)
    {
        int index = member != null && partyManager?.partyMembers != null
            ? Array.IndexOf(partyManager.partyMembers, member) : -1;
        if (index >= 0 && memberChainPortraits != null && index < memberChainPortraits.Length &&
            memberChainPortraits[index] != null) return memberChainPortraits[index];
        return GetPortrait(member);
    }

    // Use the rendered energy image, not its old independently sized marker track.
    // Transform through world space so changing a slot's size/scale/parent stays safe.
    public static void PositionEnergyThreshold(SlotView slot, float threshold)
    {
        if (slot?.energyFill == null || slot.energyThresholdMarker == null)
            return;
        var image = slot.energyFill;
        Rect bounds = image.GetPixelAdjustedRect();
        Sprite sprite = image.overrideSprite;
        if (sprite != null)
        {
            Vector2 size = sprite.rect.size;
            if (image.preserveAspect && size.x > 0f && size.y > 0f && bounds.height > 0f)
            {
                float aspect = size.x / size.y;
                if (aspect > bounds.width / bounds.height)
                {
                    float height = bounds.width / aspect;
                    bounds.y += (bounds.height - height) * image.rectTransform.pivot.y;
                    bounds.height = height;
                }
                else
                {
                    float width = bounds.height * aspect;
                    bounds.x += (bounds.width - width) * image.rectTransform.pivot.x;
                    bounds.width = width;
                }
            }
            Vector4 padding = DataUtility.GetPadding(sprite);
            if (size.x > 0f && size.y > 0f)
            {
                bounds = Rect.MinMaxRect(
                    bounds.xMin + bounds.width * padding.x / size.x,
                    bounds.yMin + bounds.height * padding.y / size.y,
                    bounds.xMax - bounds.width * padding.z / size.x,
                    bounds.yMax - bounds.height * padding.w / size.y);
            }
        }
        float normalized = Mathf.Clamp01(threshold);
        if (image.fillOrigin == (int)Image.OriginHorizontal.Right) normalized = 1f - normalized;
        Vector3 worldPoint = image.rectTransform.TransformPoint(
            new Vector3(Mathf.Lerp(bounds.xMin, bounds.xMax, normalized), bounds.center.y, 0f));
        RectTransform marker = slot.energyThresholdMarker;
        Vector3 desired = marker.parent != null ? marker.parent.InverseTransformPoint(worldPoint) : worldPoint;
        Vector3 centerOffset = marker.localRotation * Vector3.Scale(marker.rect.center, marker.localScale);
        Vector3 position = marker.localPosition;
        position.x = desired.x - centerOffset.x;
        position.y = desired.y - centerOffset.y;
        marker.localPosition = position;
    }

    private static void AlignHealthTrail(SlotView slot)
    {
        if (slot.healthFill == null || slot.healthTrail == slot.healthFill) return;
        var source = slot.healthFill.rectTransform;
        var trail = slot.healthTrail.rectTransform;
        if (source.parent != trail.parent) return;
        trail.anchorMin = source.anchorMin;
        trail.anchorMax = source.anchorMax;
        trail.pivot = source.pivot;
        trail.sizeDelta = source.sizeDelta;
        trail.anchoredPosition3D = source.anchoredPosition3D;
        trail.localScale = source.localScale;
        trail.localRotation = source.localRotation;
        slot.healthTrail.sprite = slot.healthFill.sprite;
        slot.healthTrail.preserveAspect = slot.healthFill.preserveAspect;
    }

    private float ResolveEnergyThreshold(PlayerController member)
    {
        if (member == null || member.MaxEnergy <= 0f || member.CharacterData == null)
            return fallbackReadyThreshold;

        if (member.CharacterData.enhancedSkillBranch == null)
            return fallbackReadyThreshold;

        return Mathf.Clamp01(member.EnhancedSkillEnergyRequirement / member.MaxEnergy);
    }

    private Color EvaluateReadyEnergyColor()
    {
        float phase = Mathf.Repeat(Time.unscaledTime * energyReadyColorSpeed, 3f);
        if (phase < 1f)
            return Color.Lerp(energyReadyBlue, energyReadyPink, phase);
        if (phase < 2f)
            return Color.Lerp(energyReadyPink, energyReadyPurple, phase - 1f);
        return Color.Lerp(energyReadyPurple, energyReadyBlue, phase - 2f);
    }

    private void ConfigureImages()
    {
        ConfigureSlotImages(activeSlot);

        if (reserveSlots == null)
            return;

        foreach (SlotView slot in reserveSlots)
            ConfigureSlotImages(slot);
    }

    private static void ConfigureSlotImages(SlotView slot)
    {
        if (slot == null)
            return;

        ConfigureHorizontalFill(slot.healthTrail, Image.OriginHorizontal.Left);
        ConfigureHorizontalFill(slot.healthFill, Image.OriginHorizontal.Left);
        ConfigureHorizontalFill(slot.energyFill, Image.OriginHorizontal.Left);
    }

    private static void ConfigureHorizontalFill(Image image, Image.OriginHorizontal origin)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)origin;
        image.fillClockwise = true;
    }
}
