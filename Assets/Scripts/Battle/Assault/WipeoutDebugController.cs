using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 런타임에서 일시정지 메뉴와 WIPEOUT 연출을 단독으로 시험하는 개발용 조작기다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WipeoutDebugController : MonoBehaviour
{
    private const int OverlaySortingOrder = 32000;

    private ZZZWipeoutLayeredDirector presenter;
    private CanvasGroup pauseGroup;
    private Coroutine activeSequence;
    private bool ownsPause;
    private bool previousExternalPause;
    private bool pauseMenuOpen;
    private bool sequencePlaying;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool ownsCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<WipeoutDebugController>() != null)
            return;

        GameObject root = new GameObject("[Debug] Wipeout Test");
        DontDestroyOnLoad(root);
        root.AddComponent<WipeoutDebugController>();
    }

    private void Awake()
    {
        CreatePresenterCanvas();
        CreatePauseMenu();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            TogglePauseMenu();
            return;
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            ResetTest();
            return;
        }

        if (!keyboard.f8Key.wasPressedThisFrame)
            return;

        bool partyDefeated = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        StartTest(partyDefeated);
    }

    private void TogglePauseMenu()
    {
        if (sequencePlaying || UltimateCinematicPlayer.IsPlaying)
            return;

        AssaultBattleController battle = FindFirstObjectByType<AssaultBattleController>();
        if (battle != null && battle.State == AssaultBattleState.Finished)
            return;

        if (pauseMenuOpen)
            ClosePauseMenu();
        else
            OpenPauseMenu();
    }

    private void StartTest(bool partyDefeated)
    {
        SetPauseMenuVisible(false);
        ResetTest();
        activeSequence = StartCoroutine(PlayTest(partyDefeated));
    }

    private IEnumerator PlayTest(bool partyDefeated)
    {
        sequencePlaying = true;

        previousExternalPause = HitStop.IsExternallyPaused;
        HitStop.SetExternalPause(true);
        ownsPause = true;

        // 캡처 프레임에 디버그 메뉴가 남지 않도록 한 프레임 기다린다.
        yield return null;
        yield return presenter.Play(partyDefeated);

        ReleaseOwnedPause();
        sequencePlaying = false;
        activeSequence = null;
    }

    private void ResetTest()
    {
        SetPauseMenuVisible(false);
        ReleaseMenuCursor();
        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }

        presenter?.ResetPresentation();
        ReleaseOwnedPause();
        sequencePlaying = false;
    }

    private void ReleaseOwnedPause()
    {
        if (!ownsPause)
            return;

        HitStop.SetExternalPause(previousExternalPause);
        ownsPause = false;
    }

    private void OpenPauseMenu()
    {
        previousExternalPause = HitStop.IsExternallyPaused;
        HitStop.SetExternalPause(true);
        ownsPause = true;
        AcquireMenuCursor();
        SetPauseMenuVisible(true);
    }

    private void ClosePauseMenu()
    {
        SetPauseMenuVisible(false);
        ReleaseMenuCursor();
        ReleaseOwnedPause();
    }

    private void AcquireMenuCursor()
    {
        if (!ownsCursor)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsCursor = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReleaseMenuCursor()
    {
        if (!ownsCursor)
            return;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        ownsCursor = false;
    }

    private void SetPauseMenuVisible(bool visible)
    {
        pauseMenuOpen = visible;
        if (pauseGroup == null)
            return;

        pauseGroup.alpha = visible ? 1f : 0f;
        pauseGroup.interactable = visible;
        pauseGroup.blocksRaycasts = visible;

        if (visible)
            pauseGroup.transform.SetAsLastSibling();
    }

    private void QuitGame()
    {
        SetPauseMenuVisible(false);
        ResetTest();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CreatePauseMenu()
    {
        Transform canvasTransform = presenter.transform;
        GameObject root = new GameObject(
            "PauseMenu",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(canvasTransform, false);
        Stretch(root.GetComponent<RectTransform>());

        pauseGroup = root.GetComponent<CanvasGroup>();
        Image dim = root.GetComponent<Image>();
        dim.color = new Color32(5, 8, 10, 224);
        dim.raycastTarget = true;

        GameObject panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(600f, 650f), Vector2.zero);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color32(20, 27, 29, 252);

        GameObject accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(panel.transform, false);
        SetRect(accent.GetComponent<RectTransform>(), new Vector2(600f, 8f), new Vector2(0f, 321f));
        accent.GetComponent<Image>().color = new Color32(255, 196, 28, 255);

        CreatePauseLabel(panel.transform, "ASSAULT OPERATION", 20, FontStyle.Bold,
            new Vector2(0f, 252f), new Vector2(500f, 38f), new Color32(255, 199, 35, 255));
        CreatePauseLabel(panel.transform, "일시정지", 58, FontStyle.Bold,
            new Vector2(0f, 190f), new Vector2(520f, 80f), Color.white);
        CreatePauseLabel(panel.transform, "ESC 키를 다시 누르면 전투로 돌아갑니다", 20, FontStyle.Normal,
            new Vector2(0f, 135f), new Vector2(520f, 36f), new Color32(170, 180, 182, 255));

        CreatePauseButton(panel.transform, "계속하기", new Vector2(0f, 62f),
            new Color32(233, 183, 28, 255), ClosePauseMenu);
        CreatePauseButton(panel.transform, "WIPEOUT 연출 테스트", new Vector2(0f, -22f),
            new Color32(54, 67, 69, 255), () => StartTest(false));
        CreatePauseButton(panel.transform, "MISSION FAILED 테스트", new Vector2(0f, -106f),
            new Color32(54, 67, 69, 255), () => StartTest(true));
        CreatePauseButton(panel.transform, "게임 종료", new Vector2(0f, -214f),
            new Color32(116, 42, 38, 255), QuitGame);

        SetPauseMenuVisible(false);
    }

    private static Button CreatePauseButton(
        Transform parent,
        string label,
        Vector2 position,
        Color background,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(470f, 64f), position);

        Image image = buttonObject.GetComponent<Image>();
        image.color = background;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        CreatePauseLabel(buttonObject.transform, label, 25, FontStyle.Bold,
            Vector2.zero, new Vector2(450f, 58f), Color.white);
        return button;
    }

    private static Text CreatePauseLabel(
        Transform parent,
        string value,
        int fontSize,
        FontStyle fontStyle,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        SetRect(labelObject.GetComponent<RectTransform>(), size, position);

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void CreatePresenterCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Wipeout Test Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        presenter = canvasObject.AddComponent<ZZZWipeoutLayeredDirector>();
    }

    private void OnDestroy()
    {
        ReleaseMenuCursor();
        ReleaseOwnedPause();
    }
}
