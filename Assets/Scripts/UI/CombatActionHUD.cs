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
        public Text symbol;
        public Text keyLabel;
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
    [SerializeField, Min(0f)] private float rainbowSpeed = 0.45f;

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
        // 히트 스톱과 타임 스케일 변화 중에도 준비 색상 애니메이션은 계속 보인다.
        RefreshNow();
    }

    public void RefreshNow()
    {
        PlayerController active = partyManager != null
            ? partyManager.GetCurrentCharacter()
            : null;
        bool hasActiveCharacter = active != null && !active.IsDefeated;

        ApplyButton(attackButton, hasActiveCharacter ? normalColor : unavailableColor);
        ApplyButton(dodgeButton, hasActiveCharacter ? normalColor : unavailableColor);

        bool enhancedSkillReady = hasActiveCharacter && active.IsEnhancedBranchReady;
        Color skillColor = enhancedSkillReady
            ? EvaluateRainbowColor()
            : unavailableColor;
        ApplyButton(skillButton, skillColor);

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
        ApplyButton(
            supportButton,
            supportReady ? supportReadyColor : unavailableColor);
        ApplySupportPips(currentSupport, maximumSupport);

        bool ultimateReady = hasActiveCharacter && active.CanUseUltimate;
        ApplyButton(
            ultimateButton,
            ultimateReady ? ultimateReadyColor : unavailableColor);
    }

    private Color EvaluateRainbowColor()
    {
        float hue = Mathf.Repeat(Time.unscaledTime * rainbowSpeed, 1f);
        Color color = Color.HSVToRGB(hue, 0.72f, 1f);
        color.a = 1f;
        return color;
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

    private void ApplyButton(ButtonView view, Color stateColor)
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
        if (view.frame != null)
            view.frame.color = stateColor;
        if (view.symbol != null)
            view.symbol.color = stateColor;
        if (view.keyLabel != null)
            view.keyLabel.color = stateColor;
    }
}
