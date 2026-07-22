using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

    public bool IsOpen => isOpen;

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
    }

    private void Update()
    {
        if (!isOpen)
            return;

        // 히트스톱이나 연출로 timeScale이 멈춰도 선택 제한 시간은 실제 시간 기준으로 흐른다.
        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        RefreshTime();

        if (Input.GetKeyDown(leftKey))
            Select(0);
        else if (Input.GetKeyDown(rightKey))
            Select(1);
        else if (Input.GetKeyDown(cancelKey) || remaining <= 0f)
            Cancel();
    }

    public void Show(float seconds, Sprite left = null, Sprite right = null)
    {
        duration = Mathf.Max(0.1f, seconds);
        remaining = duration;
        isOpen = true;

        if (leftPortrait != null && left != null)
            leftPortrait.sprite = left;
        if (rightPortrait != null && right != null)
            rightPortrait.sprite = right;

        SetCanvasVisible(true);
        RefreshTime();
    }

    public void Select(int side)
    {
        if (!isOpen)
            return;

        // 이 UI는 선택 결과만 알리고 실제 교체·스킬 실행은 전투 조정자가 결정한다.
        onSelected?.Invoke(Mathf.Clamp(side, 0, 1));
        HideImmediate();
    }

    public void Cancel()
    {
        if (!isOpen)
            return;

        onCancelled?.Invoke();
        HideImmediate();
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

    private void HideImmediate()
    {
        isOpen = false;
        remaining = 0f;
        SetCanvasVisible(false);
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
