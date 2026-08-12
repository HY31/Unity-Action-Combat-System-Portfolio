using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 적의 그로기 이벤트를 받아 제한 시간 동안 이전/다음 파티원의 체인 스킬 선택지를 표시한다.
/// 선택 결과만 이벤트로 전달하며 실제 교체와 스킬 실행은 전투 시스템에 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChainSkillPromptUI : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image leftPortrait;
    [SerializeField] private Image rightPortrait;
    [SerializeField] private Image timeFill;
    [SerializeField] private Text timerText;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float defaultDuration = 2.5f;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private KeyCode leftKey = KeyCode.Q;
    [SerializeField] private KeyCode rightKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onSelected;
    [SerializeField] private UnityEvent onCancelled;

    private float duration;
    private float remaining;
    private bool isOpen;
    private Tween visibilityTween;
    private EnemyController requestedEnemy;
    private int inputReadyFrame;
    private static int openPromptCount;

    public bool IsOpen => isOpen;
    public static bool IsAnyOpen => openPromptCount > 0;
    public event System.Action<int, EnemyController> SelectionConfirmed;

    private void Awake()
    {
        if (hideOnAwake)
            HideImmediate();
    }

    private void OnEnable()
    {
        // 그로기 진입 이벤트의 구독 수명은 UI 오브젝트의 활성 수명과 맞춘다.
        EnemyController.ChainSkillRequested += HandleChainSkillRequested;
    }

    private void OnDisable()
    {
        EnemyController.ChainSkillRequested -= HandleChainSkillRequested;
        HideImmediate();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        // 히트스톱이나 연출로 timeScale이 멈춰도 선택 제한 시간은 실제 시간 기준으로 흐른다.
        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        RefreshTime();

        // UI를 연 강공격의 클릭이 같은 프레임에 선택 입력으로 다시 소비되는 것을 막는다.
        if (Time.frameCount < inputReadyFrame)
            return;

        bool leftMousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool rightMousePressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        if (leftMousePressed || Input.GetKeyDown(leftKey))
            Select(0);
        else if (rightMousePressed || Input.GetKeyDown(rightKey))
            Select(1);
        else if (Input.GetKeyDown(cancelKey) || remaining <= 0f)
            Cancel();
    }

    public void Show(float seconds, Sprite left = null, Sprite right = null)
    {
        duration = Mathf.Max(0.1f, seconds);
        remaining = duration;
        inputReadyFrame = Time.frameCount + 1;
        if (!isOpen)
            openPromptCount++;

        isOpen = true;

        if (leftPortrait != null && left != null)
            leftPortrait.sprite = left;
        if (rightPortrait != null && right != null)
            rightPortrait.sprite = right;

        SetCanvasVisible(true);
        RefreshTime();
        PlayShowAnimation();
        CombatPresentationEffects.BeginChainPrompt();
    }

    public void Select(int side)
    {
        if (!isOpen)
            return;

        int selectedSide = Mathf.Clamp(side, 0, 1);
        EnemyController enemy = requestedEnemy;

        // UI를 먼저 입력 잠금 상태에서 해제한 뒤 전투 조정자에게 선택 결과를 전달한다.
        HideAnimated();
        onSelected?.Invoke(selectedSide);
        SelectionConfirmed?.Invoke(selectedSide, enemy);
    }

    public void Cancel()
    {
        if (!isOpen)
            return;

        EnemyController enemy = requestedEnemy;
        enemy?.CancelChainSkillSequence();
        onCancelled?.Invoke();
        HideAnimated();
    }

    public void Configure(
        CanvasGroup group,
        Image left,
        Image right,
        Image progress,
        Text timer)
    {
        canvasGroup = group;
        leftPortrait = left;
        rightPortrait = right;
        timeFill = progress;
        timerText = timer;
    }

    private void HandleChainSkillRequested(EnemyController enemy, PlayerController attacker)
    {
        requestedEnemy = enemy;

        // 파티 순서를 소유한 UI에서 이전/다음 캐릭터 초상화만 가져온다.
        PartyStatusUI partyHud = FindFirstObjectByType<PartyStatusUI>();
        Sprite left = partyHud != null ? partyHud.GetPreviousPortrait() : null;
        Sprite right = partyHud != null ? partyHud.GetNextPortrait() : null;
        Show(defaultDuration, left, right);
    }

    private void RefreshTime()
    {
        float normalized = duration > 0f ? remaining / duration : 0f;
        if (timeFill != null)
            timeFill.fillAmount = normalized;

        if (timerText != null)
        {
            int totalCentiseconds = Mathf.CeilToInt(remaining * 100f);
            int seconds = totalCentiseconds / 100;
            int centiseconds = totalCentiseconds % 100;
            timerText.text = $"00:{seconds:00}:{centiseconds:00}";
        }
    }

    private void PlayShowAnimation()
    {
        if (canvasGroup == null)
            return;

        visibilityTween?.Kill(false);
        canvasGroup.alpha = 0f;
        canvasGroup.transform.localScale = Vector3.one * 0.92f;

        visibilityTween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutCubic))
            .Join(canvasGroup.transform
                .DOScale(Vector3.one, 0.2f)
                .SetEase(Ease.OutBack))
            .SetUpdate(true);
    }

    private void HideAnimated()
    {
        if (isOpen)
            openPromptCount = Mathf.Max(0, openPromptCount - 1);

        isOpen = false;
        remaining = 0f;
        requestedEnemy = null;
        CombatPresentationEffects.EndChainPrompt();

        if (canvasGroup == null)
            return;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        visibilityTween?.Kill(false);

        visibilityTween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InCubic))
            .Join(canvasGroup.transform
                .DOScale(Vector3.one * 0.96f, 0.12f)
                .SetEase(Ease.InCubic))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvasGroup.transform.localScale = Vector3.one;
                SetCanvasVisible(false);
            });
    }
    private void HideImmediate()
    {
        bool wasOpen = isOpen;
        if (wasOpen)
            openPromptCount = Mathf.Max(0, openPromptCount - 1);

        isOpen = false;
        remaining = 0f;
        requestedEnemy = null;
        visibilityTween?.Kill(false);
        visibilityTween = null;

        if (canvasGroup != null)
            canvasGroup.transform.localScale = Vector3.one;

        SetCanvasVisible(false);

        if (wasOpen)
            CombatPresentationEffects.EndChainPrompt();
    }

    private void SetCanvasVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
