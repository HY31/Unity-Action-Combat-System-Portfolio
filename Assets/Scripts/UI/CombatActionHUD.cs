using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 조작 캐릭터의 전투 입력 가능 상태를 우측 하단 버튼 HUD로 표시한다.
/// 입력을 처리하지 않고, PartyManager와 PlayerController의 원본 데이터만 시각화한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatActionHUD : MonoBehaviour
{
    [Serializable]
    public sealed class ButtonView
    {
        public CanvasGroup root;
        public Image background;
        public Image frame;
        public Image icon;
        public Text keyLabel;
        public Sprite normalSprite;
        public Sprite readySprite;
        public Image readySheen;
        public bool dimUntilReady;

        [NonSerialized] public bool wasReady;
        [NonSerialized] public float sheenProgress = -1f;
    }

    [Header("Data")]
    [SerializeField] private PartyManager partyManager;

    [Header("Buttons")]
    [SerializeField] private ButtonView attackButton;
    [SerializeField] private ButtonView dodgeButton;
    [SerializeField] private ButtonView skillButton;
    [SerializeField] private ButtonView supportButton;
    [SerializeField] private ButtonView ultimateButton;
    [SerializeField] private Image[] supportPointPips;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color32(151, 157, 157, 255);
    [SerializeField] private Color unavailableColor = new Color32(71, 76, 76, 255);
    [SerializeField] private Color supportReadyColor = new Color32(255, 207, 19, 255);
    [SerializeField] private Color ultimateReadyColor = new Color32(255, 145, 18, 255);
    [SerializeField] private Color buttonBackgroundColor = new Color32(12, 15, 16, 238);

    [Header("준비 연출")]
    [SerializeField, Min(0.05f)] private float sheenDuration = 0.85f;
    [SerializeField] private Vector2 sheenStart = new Vector2(-104f, 104f);
    [SerializeField] private Vector2 sheenEnd = new Vector2(104f, -104f);
    [SerializeField, Range(0f, 1f)] private float sheenPeakAlpha = 0.78f;
    [SerializeField, Range(1f, 1.25f)] private float readyPulseScale = 1.08f;

    private PlayerController lastActiveCharacter;

    public void Configure(
        ButtonView attack,
        ButtonView dodge,
        ButtonView skill,
        ButtonView support,
        ButtonView ultimate,
        Image[] supportPips)
    {
        attackButton = attack;
        dodgeButton = dodge;
        skillButton = skill;
        supportButton = support;
        ultimateButton = ultimate;
        supportPointPips = supportPips;
        RefreshNow();
    }

    public void Bind(PartyManager manager)
    {
        partyManager = manager;
        RefreshNow();
    }

    private void LateUpdate()
    {
        // 히트 스톱과 타임 스케일 변화 중에도 준비 상태와 광택 애니메이션은 계속 보인다.
        RefreshNow();
    }

    public void RefreshNow()
    {
        PlayerController active = partyManager != null
            ? partyManager.GetCurrentCharacter()
            : null;
        bool hasActiveCharacter = active != null && !active.IsDefeated;

        bool activeCharacterChanged = lastActiveCharacter != active;
        if (activeCharacterChanged)
        {
            StopReadySheen(skillButton);
            StopReadySheen(ultimateButton);
            lastActiveCharacter = active;
        }

        ApplyButton(attackButton, hasActiveCharacter, false, normalColor);
        ApplyButton(dodgeButton, hasActiveCharacter, false, normalColor);

        bool enhancedSkillReady = hasActiveCharacter && active.IsEnhancedBranchReady;
        ApplyButton(
            skillButton,
            hasActiveCharacter,
            enhancedSkillReady,
            normalColor,
            activeCharacterChanged);

        SupportPointManager supportManager = partyManager != null
            ? partyManager.SupportPointManager
            : active != null
                ? active.SupportPointManager
                : null;
        int currentSupport = supportManager != null
            ? supportManager.CurrentSupportPoint
            : 0;
        int maximumSupport = supportManager != null
            ? supportManager.MaxSupportPoint
            : 0;
        bool supportReady = hasActiveCharacter && currentSupport > 0;
        ApplyButton(supportButton, supportReady, false, supportReadyColor);
        ApplySupportPips(currentSupport, maximumSupport);

        bool ultimateReady = hasActiveCharacter && active.CanUseUltimate;
        ApplyButton(
            ultimateButton,
            hasActiveCharacter,
            ultimateReady,
            ultimateReadyColor,
            activeCharacterChanged);

        UpdateReadySheen(skillButton);
        UpdateReadySheen(ultimateButton);
    }

    private void ApplySupportPips(int current, int maximum)
    {
        if (supportPointPips == null)
            return;

        for (int i = 0; i < supportPointPips.Length; i++)
        {
            Image pip = supportPointPips[i];
            if (pip == null)
                continue;

            bool valid = i < maximum;
            pip.gameObject.SetActive(valid);
            if (valid)
                pip.color = i < current ? supportReadyColor : unavailableColor;
        }
    }

    private void ApplyButton(
        ButtonView view,
        bool available,
        bool ready,
        Color availableKeyColor,
        bool synchronizeReadyState = false)
    {
        if (view == null)
            return;

        if (view.root != null)
        {
            view.root.alpha = 1f;
            view.root.interactable = false;
            view.root.blocksRaycasts = false;
        }

        if (view.background != null)
            view.background.color = buttonBackgroundColor;

        Color visualColor = !available || (view.dimUntilReady && !ready)
            ? unavailableColor
            : Color.white;
        if (view.frame != null)
            view.frame.color = visualColor;

        if (view.icon != null)
        {
            Sprite stateSprite = ready && view.readySprite != null
                ? view.readySprite
                : view.normalSprite;
            if (stateSprite != null)
                view.icon.sprite = stateSprite;
            view.icon.color = visualColor;
        }

        if (view.keyLabel != null)
            view.keyLabel.color = available ? availableKeyColor : unavailableColor;

        if (view.readySheen == null)
            return;

        // 캐릭터 교대 직후에는 이미 준비된 상태를 새 충전으로 오인하지 않는다.
        if (synchronizeReadyState)
        {
            StopReadySheen(view);
            view.wasReady = ready;
            return;
        }

        if (ready && !view.wasReady)
            BeginReadySheen(view);
        else if (!ready)
            StopReadySheen(view);

        view.wasReady = ready;
    }

    private void BeginReadySheen(ButtonView view)
    {
        view.sheenProgress = 0f;
        view.readySheen.gameObject.SetActive(true);
        view.readySheen.rectTransform.anchoredPosition = sheenStart;
        SetSheenAlpha(view.readySheen, 0f);
        SetButtonScale(view, 1f);
    }

    private void UpdateReadySheen(ButtonView view)
    {
        if (view == null || view.readySheen == null || view.sheenProgress < 0f)
            return;

        view.sheenProgress += Time.unscaledDeltaTime / Mathf.Max(0.05f, sheenDuration);
        float progress = Mathf.Clamp01(view.sheenProgress);
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        view.readySheen.rectTransform.anchoredPosition = Vector2.Lerp(
            sheenStart,
            sheenEnd,
            eased);
        SetSheenAlpha(
            view.readySheen,
            Mathf.Sin(progress * Mathf.PI) * sheenPeakAlpha);
        SetButtonScale(
            view,
            1f + Mathf.Sin(progress * Mathf.PI) * (readyPulseScale - 1f));

        if (progress >= 1f)
            StopReadySheen(view);
    }

    private static void SetSheenAlpha(Image sheen, float alpha)
    {
        Color color = sheen.color;
        color.a = alpha;
        sheen.color = color;
    }

    private static void SetButtonScale(ButtonView view, float scale)
    {
        if (view.root != null)
            view.root.transform.localScale = Vector3.one * scale;
    }

    private static void StopReadySheen(ButtonView view)
    {
        if (view == null)
            return;

        view.sheenProgress = -1f;
        if (view.readySheen != null)
            view.readySheen.gameObject.SetActive(false);
        SetButtonScale(view, 1f);
    }

}
