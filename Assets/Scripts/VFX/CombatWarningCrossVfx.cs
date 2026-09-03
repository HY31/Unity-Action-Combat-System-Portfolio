using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적의 월드 좌표를 화면 좌표로 투영해, 화면 가장자리에서 적에게 수렴하는 경고 십자를 재생한다.
/// 공격 판정과 경고 색 선택은 EnemyController가 소유하고 이 컴포넌트는 표현만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatWarningCrossVfx : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float BeamThickness = 64f;
    private const float InnerBeamThickness = 18f;
    private const float CoreBeamThickness = 3.25f;

    private static Sprite beamHorizontalForward;
    private static Sprite beamHorizontalReverse;
    private static Sprite beamVerticalForward;
    private static Sprite beamVerticalReverse;
    private static Sprite radialGlow;

    [Header("Colors")]
    [SerializeField] private Color yellowColor = new Color(1f, 0.72f, 0.04f, 1f);
    [SerializeField] private Color redColor = new Color(1f, 0.08f, 0.08f, 1f);

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float convergeDuration = 0.085f;
    [SerializeField, Min(0f)] private float flashHoldDuration = 0.015f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.14f;

    private sealed class Beam
    {
        public RectTransform root;
        public Image outerGlow;
        public Image innerGlow;
        public Image core;
    }

    private GameObject canvasObject;
    private CanvasGroup canvasGroup;
    private RectTransform canvasRect;
    private RectTransform visualRoot;
    private CanvasGroup centerGroup;
    private Image centerGlow;
    private Image centerHorizontal;
    private Image centerVertical;
    private Beam leftBeam;
    private Beam rightBeam;
    private Beam bottomBeam;
    private Beam topBeam;

    private Camera targetCamera;
    private Transform target;
    private Vector3 targetOffset;
    private WarningType currentType = WarningType.None;
    private Sequence sequence;

    public void Play(Transform warningTarget, WarningType warningType, Vector3 worldOffset)
    {
        if (warningTarget == null || warningType == WarningType.None)
        {
            Stop();
            return;
        }

        if (currentType == warningType && target == warningTarget)
            return;

        EnsureVisuals();

        target = warningTarget;
        targetOffset = worldOffset;
        currentType = warningType;
        targetCamera = ResolveCamera();

        ApplyColor(warningType == WarningType.Yellow ? yellowColor : redColor);
        UpdateLayout();
        PlayFlash();
    }

    public void Stop()
    {
        currentType = WarningType.None;
        target = null;
        targetOffset = Vector3.zero;

        sequence?.Kill(false);
        sequence = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (visualRoot != null)
            visualRoot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (currentType == WarningType.None || target == null || visualRoot == null)
            return;

        UpdateLayout();
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        sequence?.Kill(false);

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void EnsureVisuals()
    {
        if (canvasObject != null)
            return;

        EnsureSprites();

        canvasObject = new GameObject(
            $"[Runtime] {name} Warning Cross",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        canvasRect = canvasObject.GetComponent<RectTransform>();

        visualRoot = CreateRectTransform("Cross Flash", canvasRect);
        StretchFullScreen(visualRoot);

        leftBeam = CreateBeam("Left Beam", visualRoot, beamHorizontalForward, false);
        rightBeam = CreateBeam("Right Beam", visualRoot, beamHorizontalReverse, false);
        bottomBeam = CreateBeam("Bottom Beam", visualRoot, beamVerticalForward, true);
        topBeam = CreateBeam("Top Beam", visualRoot, beamVerticalReverse, true);

        CreateCenterFlash();
        visualRoot.gameObject.SetActive(false);
    }

    private void CreateCenterFlash()
    {
        RectTransform centerRoot = CreateRectTransform("Center Flash", visualRoot);
        centerRoot.anchorMin = centerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        centerRoot.pivot = new Vector2(0.5f, 0.5f);
        centerRoot.sizeDelta = new Vector2(220f, 220f);

        centerGroup = centerRoot.gameObject.AddComponent<CanvasGroup>();
        centerGroup.interactable = false;
        centerGroup.blocksRaycasts = false;

        centerGlow = CreateImage("Glow", centerRoot, radialGlow);
        StretchFullScreen(centerGlow.rectTransform);

        centerHorizontal = CreateImage("Horizontal Spark", centerRoot, null);
        SetCenteredSize(centerHorizontal.rectTransform, 190f, 4f);

        centerVertical = CreateImage("Vertical Spark", centerRoot, null);
        SetCenteredSize(centerVertical.rectTransform, 4f, 190f);
    }

    private static Beam CreateBeam(
        string objectName,
        RectTransform parent,
        Sprite sprite,
        bool vertical)
    {
        Beam beam = new Beam
        {
            root = CreateRectTransform(objectName, parent)
        };

        beam.outerGlow = CreateBeamLayer("Outer Glow", beam.root, sprite, vertical, BeamThickness);
        beam.innerGlow = CreateBeamLayer("Inner Glow", beam.root, sprite, vertical, InnerBeamThickness);
        beam.core = CreateBeamLayer("Core", beam.root, sprite, vertical, CoreBeamThickness);
        return beam;
    }

    private static Image CreateBeamLayer(
        string objectName,
        RectTransform parent,
        Sprite sprite,
        bool vertical,
        float thickness)
    {
        Image image = CreateImage(objectName, parent, sprite);
        RectTransform rect = image.rectTransform;

        if (vertical)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(thickness, 0f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, thickness);
        }

        return image;
    }

    private void PlayFlash()
    {
        sequence?.Kill(false);
        sequence = null;

        visualRoot.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        centerGroup.alpha = 0f;

        leftBeam.root.localScale = new Vector3(0f, 1f, 1f);
        rightBeam.root.localScale = new Vector3(0f, 1f, 1f);
        bottomBeam.root.localScale = new Vector3(1f, 0f, 1f);
        topBeam.root.localScale = new Vector3(1f, 0f, 1f);
        centerGroup.transform.localScale = Vector3.one * 0.2f;

        float centerStart = convergeDuration * 0.58f;
        float centerDuration = convergeDuration * 0.78f;

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(canvasGroup
                .DOFade(1f, Mathf.Min(0.035f, convergeDuration))
                .SetEase(Ease.OutQuad))
            .Join(leftBeam.root
                .DOScaleX(1f, convergeDuration)
                .SetEase(Ease.OutCubic))
            .Join(rightBeam.root
                .DOScaleX(1f, convergeDuration)
                .SetEase(Ease.OutCubic))
            .Join(bottomBeam.root
                .DOScaleY(1f, convergeDuration)
                .SetEase(Ease.OutCubic))
            .Join(topBeam.root
                .DOScaleY(1f, convergeDuration)
                .SetEase(Ease.OutCubic))
            .Insert(centerStart, centerGroup
                .DOFade(1f, centerDuration * 0.55f)
                .SetEase(Ease.OutQuad))
            .Insert(centerStart, centerGroup.transform
                .DOScale(1.08f, centerDuration)
                .SetEase(Ease.OutBack))
            .AppendInterval(flashHoldDuration)
            .Append(canvasGroup
                .DOFade(0f, fadeDuration)
                .SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);
            });
    }

    private void UpdateLayout()
    {
        if (target == null || canvasRect == null)
            return;

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            targetCamera = ResolveCamera();

        if (targetCamera == null)
        {
            visualRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(target.position + targetOffset);
        if (screenPoint.z <= 0f)
        {
            visualRoot.gameObject.SetActive(false);
            return;
        }

        if (!visualRoot.gameObject.activeSelf && sequence != null && sequence.IsActive())
            visualRoot.gameObject.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            null,
            out Vector2 center);

        Rect bounds = canvasRect.rect;
        center.x = Mathf.Clamp(center.x, bounds.xMin, bounds.xMax);
        center.y = Mathf.Clamp(center.y, bounds.yMin, bounds.yMax);

        LayoutHorizontalBeam(leftBeam.root, bounds.xMin, center.x, center.y, true);
        LayoutHorizontalBeam(rightBeam.root, center.x, bounds.xMax, center.y, false);
        LayoutVerticalBeam(bottomBeam.root, bounds.yMin, center.y, center.x, true);
        LayoutVerticalBeam(topBeam.root, center.y, bounds.yMax, center.x, false);

        ((RectTransform)centerGroup.transform).anchoredPosition = center;
    }

    private static void LayoutHorizontalBeam(
        RectTransform rect,
        float start,
        float end,
        float y,
        bool pivotAtStart)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(pivotAtStart ? 0f : 1f, 0.5f);
        rect.anchoredPosition = new Vector2(pivotAtStart ? start : end, y);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, end - start), BeamThickness);
    }

    private static void LayoutVerticalBeam(
        RectTransform rect,
        float start,
        float end,
        float x,
        bool pivotAtStart)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, pivotAtStart ? 0f : 1f);
        rect.anchoredPosition = new Vector2(x, pivotAtStart ? start : end);
        rect.sizeDelta = new Vector2(BeamThickness, Mathf.Max(0f, end - start));
    }

    private void ApplyColor(Color color)
    {
        ApplyBeamColor(leftBeam, color);
        ApplyBeamColor(rightBeam, color);
        ApplyBeamColor(bottomBeam, color);
        ApplyBeamColor(topBeam, color);

        centerGlow.color = WithAlpha(color, 0.72f);
        Color hotCore = Color.Lerp(color, Color.white, 0.82f);
        centerHorizontal.color = WithAlpha(hotCore, 0.98f);
        centerVertical.color = WithAlpha(hotCore, 0.98f);
    }

    private static void ApplyBeamColor(Beam beam, Color color)
    {
        beam.outerGlow.color = WithAlpha(color, 0.24f);
        beam.innerGlow.color = WithAlpha(color, 0.62f);
        beam.core.color = WithAlpha(Color.Lerp(color, Color.white, 0.78f), 0.98f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Camera ResolveCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                return cameras[i];
        }

        return null;
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetCenteredSize(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void EnsureSprites()
    {
        if (beamHorizontalForward != null)
            return;

        beamHorizontalForward = CreateBeamSprite(false, false);
        beamHorizontalReverse = CreateBeamSprite(false, true);
        beamVerticalForward = CreateBeamSprite(true, false);
        beamVerticalReverse = CreateBeamSprite(true, true);
        radialGlow = CreateRadialGlowSprite();
    }

    private static Sprite CreateBeamSprite(bool vertical, bool brightAtStart)
    {
        const int longSide = 128;
        const int shortSide = 32;
        int width = vertical ? shortSide : longSide;
        int height = vertical ? longSide : shortSide;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = vertical ? "Warning Beam Vertical" : "Warning Beam Horizontal",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float along = vertical
                    ? y / (float)(height - 1)
                    : x / (float)(width - 1);
                float across = vertical
                    ? x / (float)(width - 1)
                    : y / (float)(height - 1);

                float centerWeight = brightAtStart ? 1f - along : along;
                centerWeight = Mathf.SmoothStep(0f, 1f, centerWeight);
                float crossSection = 1f - Mathf.Abs(across * 2f - 1f);
                crossSection = Mathf.Pow(Mathf.Clamp01(crossSection), 2.7f);
                float alpha = crossSection * Mathf.Lerp(0.12f, 1f, centerWeight);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
    }

    private static Sprite CreateRadialGlowSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Warning Center Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Pow(1f - Mathf.Clamp01(distance), 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
    }
}
