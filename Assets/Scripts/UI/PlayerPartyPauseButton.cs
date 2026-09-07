using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PlayerPartyPauseButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private WipeoutDebugController wipeoutController;

    private bool listenerAttached;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        AttachListener();
    }

    private void OnDisable()
    {
        DetachListener();
    }

    public void Configure(Button targetButton, WipeoutDebugController targetController = null)
    {
        DetachListener();
        button = targetButton;
        wipeoutController = targetController;

        if (isActiveAndEnabled)
            AttachListener();
    }

    public void TogglePauseMenu()
    {
        if (wipeoutController == null)
        {
            wipeoutController = FindFirstObjectByType<WipeoutDebugController>(
                FindObjectsInactive.Include);
        }

        if (wipeoutController != null)
        {
            // 기존 컨트롤러가 패널, 커서, 전투 상태를 함께 관리한다.
            // private 진입점은 기존 코드를 변경하지 않고 Unity 메시지로 호출한다.
            wipeoutController.SendMessage(
                "TogglePauseMenu",
                SendMessageOptions.DontRequireReceiver);
            return;
        }

        Debug.LogWarning(
            "[PlayerPartyPauseButton] WipeoutDebugController를 찾지 못해 패널 없이 일시정지만 전환합니다.",
            this);
        HitStop.SetExternalPause(!HitStop.IsExternallyPaused);
    }

    private void AttachListener()
    {
        if (button == null || listenerAttached)
            return;

        button.onClick.AddListener(TogglePauseMenu);
        listenerAttached = true;
    }

    private void DetachListener()
    {
        if (button == null || !listenerAttached)
            return;

        button.onClick.RemoveListener(TogglePauseMenu);
        listenerAttached = false;
    }
}
