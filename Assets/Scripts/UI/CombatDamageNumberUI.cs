using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatDamageNumberUI : MonoBehaviour
{
    private const int SortingOrder = 20000;

    private static CombatDamageNumberUI instance;

    private RectTransform canvasRect;
    private Font font;
    private int spawnSequence;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void Play(
        Vector3 worldPosition,
        float damage,
        CombatElement element,
        bool emphasized)
    {
        CombatDamageNumberUI presenter = Resolve();
        presenter?.Spawn(worldPosition, damage, element, emphasized);
    }

    private static CombatDamageNumberUI Resolve()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<CombatDamageNumberUI>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Combat Damage Numbers (Runtime)");
        instance = root.AddComponent<CombatDamageNumberUI>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>().enabled = false;
        canvasRect = canvas.transform as RectTransform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Spawn(
        Vector3 worldPosition,
        float damage,
        CombatElement element,
        bool emphasized)
    {
        Camera worldCamera = Camera.main;
        if (worldCamera == null || canvasRect == null)
            return;

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out Vector2 anchoredPosition))
        {
            return;
        }

        // Spread consecutive hits without consuming UnityEngine.Random state.
        int lane = spawnSequence++ % 5;
        float horizontalOffset = (lane - 2) * 24f;
        float verticalOffset = (lane % 2) * 12f;
        anchoredPosition += new Vector2(horizontalOffset, verticalOffset);

        GameObject numberObject = new GameObject(
            "Damage Number",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline),
            typeof(CanvasGroup));
        numberObject.transform.SetParent(canvasRect, false);

        RectTransform rect = numberObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(emphasized ? 320f : 260f, 90f);
        rect.localScale = Vector3.one * 0.72f;

        Text text = numberObject.GetComponent<Text>();
        text.font = font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = emphasized ? 52 : 40;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = Mathf.Max(1, Mathf.RoundToInt(damage)) + (emphasized ? "!!" : "!");
        text.color = ResolveElementColor(element);

        Outline outline = numberObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.035f, 0.025f, 0.02f, 0.98f);
        outline.effectDistance = emphasized
            ? new Vector2(4f, -4f)
            : new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        CanvasGroup group = numberObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        float riseDistance = emphasized ? 112f : 82f;
        float lifetime = emphasized ? 0.72f : 0.58f;
        float enterDuration = emphasized ? 0.1f : 0.08f;

        DOTween.Sequence()
            .Join(group.DOFade(1f, enterDuration).SetEase(Ease.OutQuad))
            .Join(rect.DOScale(
                emphasized ? 1.15f : 1f,
                enterDuration).SetEase(Ease.OutBack))
            .Join(rect.DOAnchorPosY(
                anchoredPosition.y + riseDistance,
                lifetime).SetEase(Ease.OutCubic))
            .Append(group.DOFade(0f, 0.16f).SetEase(Ease.InQuad))
            .SetUpdate(true)
            .OnComplete(() => Destroy(numberObject));
    }

    private static Color ResolveElementColor(CombatElement element)
    {
        return element switch
        {
            CombatElement.Fire => new Color(1f, 0.26f, 0.05f),
            CombatElement.Ice => new Color(0.68f, 0.96f, 1f),
            CombatElement.Electric => new Color(0.72f, 0.48f, 1f),
            CombatElement.Wind => new Color(0.35f, 1f, 0.68f),
            CombatElement.Ether => new Color(1f, 0.34f, 0.9f),
            _ => new Color(1f, 0.69f, 0.08f)
        };
    }
}
