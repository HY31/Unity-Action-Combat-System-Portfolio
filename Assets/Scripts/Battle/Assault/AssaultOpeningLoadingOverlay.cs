using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프닝 영상이 준비되는 동안 즉시 표시되는 경량 로딩 연출이다.
/// 실제 영상 디코더를 추가로 사용하지 않고 UGUI 요소만 움직인다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssaultOpeningLoadingOverlay : MonoBehaviour
{
    private const string FontPath =
        "Fonts/BarlowCondensed/BarlowCondensed-Black";

    private static readonly Color Yellow = new Color32(255, 220, 0, 255);
    private static readonly Color Dark = new Color32(8, 10, 10, 255);
    private static readonly Color Muted = new Color32(118, 126, 122, 255);

    private CanvasGroup canvasGroup;
    private Image progress;
    private RectTransform sweep;
    private Text status;
    private float shownAt;
    private bool ready;

    public float VisibleDuration => Mathf.Max(0f, Time.unscaledTime - shownAt);

    public static AssaultOpeningLoadingOverlay Create(Transform parent)
    {
        if (parent == null)
            return null;

        AssaultOpeningLoadingOverlay existing =
            parent.GetComponentInChildren<AssaultOpeningLoadingOverlay>(true);
        if (existing != null)
            return existing;

        GameObject root = new GameObject(
            "Opening Loading Overlay",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(AssaultOpeningLoadingOverlay));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());

        AssaultOpeningLoadingOverlay overlay =
            root.GetComponent<AssaultOpeningLoadingOverlay>();
        overlay.Build();
        overlay.HideImmediate();
        return overlay;
    }

    public void Show()
    {
        if (canvasGroup == null)
            Build();

        shownAt = Time.unscaledTime;
        ready = false;
        progress.fillAmount = 0.08f;
        status.text = "COMBAT DATA LOADING.";
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
    }

    public void MarkReady()
    {
        ready = true;
        if (progress != null)
            progress.fillAmount = 1f;
        if (status != null)
            status.text = "VISUAL FEED READY";
    }

    public IEnumerator FadeOut(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;

        while (canvasGroup != null && elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                safeDuration > 0f ? Mathf.Clamp01(elapsed / safeDuration) : 1f);
            yield return null;
        }

        HideImmediate();
    }

    public void HideImmediate()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (ready || canvasGroup == null || canvasGroup.alpha <= 0f)
            return;

        float elapsed = VisibleDuration;
        progress.fillAmount = Mathf.Lerp(
            0.08f,
            0.94f,
            1f - Mathf.Exp(-elapsed * 1.15f));

        float sweepT = Mathf.Repeat(elapsed * 0.72f, 1f);
        sweep.anchoredPosition = new Vector2(
            Mathf.Lerp(-344f, 344f, sweepT),
            0f);

        int dots = 1 + Mathf.FloorToInt(elapsed * 2.4f) % 3;
        status.text = "COMBAT DATA LOADING" + new string('.', dots);
    }

    private void Build()
    {
        if (canvasGroup != null && progress != null && status != null)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;

        Image backdrop = CreateImage("Loading Backdrop", transform, Dark);
        Stretch(backdrop.rectTransform);

        Image topRail = CreateImage("Top Yellow Rail", transform, Yellow);
        SetRect(
            topRail.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -34f),
            new Vector2(0f, 8f));

        Image leftAccent = CreateImage("Left Accent", transform, Yellow);
        SetRect(
            leftAccent.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(58f, 0f),
            new Vector2(24f, 470f));

        Image cornerPanel = CreateImage(
            "Corner Yellow Panel",
            transform,
            new Color32(255, 220, 0, 230));
        SetRect(
            cornerPanel.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-84f, -118f),
            new Vector2(260f, 74f));
        cornerPanel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);

        for (int i = 0; i < 16; i++)
        {
            Image stripe = CreateImage(
                $"Bottom Stripe {i + 1:00}",
                transform,
                i % 3 == 0 ? Color.white : Yellow);
            SetRect(
                stripe.rectTransform,
                Vector2.zero,
                Vector2.zero,
                new Vector2(70f + i * 92f, 68f),
                new Vector2(66f, 16f));
            stripe.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        Text operation = CreateText(
            "Operation Label",
            transform,
            "HOLLOW OPERATION // VISUAL LINK",
            29,
            TextAnchor.MiddleLeft,
            Yellow);
        SetRect(
            operation.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(286f, -88f),
            new Vector2(520f, 48f));

        status = CreateText(
            "Loading Status",
            transform,
            "COMBAT DATA LOADING...",
            68,
            TextAnchor.MiddleCenter,
            Color.white);
        SetRect(
            status.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 44f),
            new Vector2(1250f, 112f));
        AddOutline(status, new Vector2(3f, -3f));

        Text subLabel = CreateText(
            "Loading Sub Label",
            transform,
            "INITIALIZING COMBAT VISUAL FEED",
            24,
            TextAnchor.MiddleCenter,
            Muted);
        SetRect(
            subLabel.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -35f),
            new Vector2(800f, 42f));

        Image progressFrame = CreateImage(
            "Loading Progress Frame",
            transform,
            new Color32(40, 45, 43, 255));
        SetRect(
            progressFrame.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -104f),
            new Vector2(720f, 18f));

        progress = CreateImage("Loading Progress", progressFrame.transform, Yellow);
        StretchInset(progress.rectTransform, 3f);
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = (int)Image.OriginHorizontal.Left;
        progress.fillAmount = 0.08f;

        Image sweepImage = CreateImage(
            "Loading Sweep",
            progressFrame.transform,
            new Color32(255, 250, 187, 230));
        sweep = sweepImage.rectTransform;
        SetRect(
            sweep,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-344f, 0f),
            new Vector2(48f, 24f));

        Text footer = CreateText(
            "Loading Footer",
            transform,
            "TARGET: DEAD END BUTCHER   /   CONNECTION: LOCAL",
            22,
            TextAnchor.MiddleRight,
            Muted);
        SetRect(
            footer.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-340f, 72f),
            new Vector2(620f, 40f));
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject child = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        child.transform.SetParent(parent, false);

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        TextAnchor alignment,
        Color color)
    {
        GameObject child = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        child.transform.SetParent(parent, false);

        Text text = child.GetComponent<Text>();
        text.text = content;
        text.font = Resources.Load<Font>(FontPath) ??
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.BoldAndItalic;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void AddOutline(Text text, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchInset(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }
}
