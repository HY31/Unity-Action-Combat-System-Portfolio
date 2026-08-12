using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatPresentationEffects : MonoBehaviour
{
    private static CombatPresentationEffects instance;

    private CanvasGroup flashGroup;
    private Image flashImage;
    private CanvasGroup perfectDodgeToneGroup;
    private Image perfectDodgeToneImage;
    private CanvasGroup letterboxGroup;

    private Tween flashTween;
    private Tween perfectDodgeToneTween;
    private Tween letterboxTween;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void PlayHit(CombatElement element, float intensity)
    {
        // 일반/다단 공격은 로컬 히트 VFX와 카메라만 사용한다.
        // 강한 마무리 공격만 아주 약한 화면 플래시를 허용해 연속 점멸을 방지한다.
        if (intensity < 1.25f)
            return;

        float normalizedIntensity = Mathf.InverseLerp(1.25f, 1.5f, intensity);
        float alpha = Mathf.Lerp(0.012f, 0.025f, normalizedIntensity);
        Flash(ResolveElementColor(element), alpha, 0.12f);
    }
    public static void PlayPerfectDodge(PlayerController player)
    {
        // 플레이어는 unscaled action time으로 움직이고 주변 전투만 약 0.6초간 느려진다.
        HitStop.DoSlowMotion(0.16f, 0.12f, 0.48f);

        CombatPresentationEffects effects = Resolve();
        effects?.PlayPerfectDodgeTone();

        Flash(new Color(0.15f, 0.9f, 1f), 0.075f, 0.2f);

        if (player != null)
            CombatHitVfx.PlayPerfectDodge(player.transform);

        ThirdPersonCameraController.Active?.PunchFieldOfView(4f, 0.25f);
    }

    public static void PlayParry()
    {
        HitStop.DoSlowMotion(0.12f, 0.045f, 0.18f);
        Flash(new Color(1f, 0.73f, 0.04f), 0.1f, 0.2f);
        ThirdPersonCameraController.Active?.Shake(0.1f, 0.09f, 20);
        ThirdPersonCameraController.Active?.PunchParryImpact();

    }

    public static void PlayGroggy(CombatElement element)
    {
        HitStop.DoSlowMotion(0.18f, 0.075f, 0.28f);
        Flash(ResolveElementColor(element), 0.1f, 0.24f);
        ThirdPersonCameraController.Active?.PunchFieldOfView(-4f, 0.32f);
    }

    public static void PlayAnomalyBurst(CombatElement element, bool isWeakness)
    {
        float slowMotionDuration = isWeakness ? 0.12f : 0.07f;
        float flashAlpha = isWeakness ? 0.09f : 0.055f;

        HitStop.DoSlowMotion(slowMotionDuration, 0.06f, 0.2f);
        Flash(ResolveElementColor(element), flashAlpha, 0.22f);
        ThirdPersonCameraController.Active?.PunchFieldOfView(
            isWeakness ? -3.5f : -2f,
            0.26f);
    }

    public static void BeginUltimate(CombatElement element)
    {
        HitStop.DoSlowMotion(0.18f, 0.06f, 0.28f);
        ShowLetterbox(true, 0.14f);
        Flash(ResolveElementColor(element), 0.08f, 0.24f);
        ThirdPersonCameraController.Active?.PunchFieldOfView(-8f, 0.42f);
    }

    public static void EndUltimate()
    {
        ShowLetterbox(false, 0.18f);
        ThirdPersonCameraController.Active?.PunchFieldOfView(3f, 0.25f);
    }

    public static void BeginChainPrompt()
    {
        HitStop.DoSlowMotion(0.1f, 0.08f, 0.32f);
        ShowLetterbox(true, 0.12f);
        Flash(new Color(1f, 0.26f, 0.05f), 0.05f, 0.22f);
    }

    public static void EndChainPrompt()
    {
        ShowLetterbox(false, 0.14f);
    }

    public static void Flash(Color color, float peakAlpha, float duration)
    {
        CombatPresentationEffects effects = Resolve();
        if (effects == null)
            return;

        effects.PlayFlash(color, peakAlpha, duration);
    }

    public static void ShowLetterbox(bool visible, float duration)
    {
        CombatPresentationEffects effects = Resolve();
        if (effects == null)
            return;

        effects.SetLetterboxVisible(visible, duration);
    }

    private static CombatPresentationEffects Resolve()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<CombatPresentationEffects>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Combat Presentation FX (Runtime)");
        instance = root.AddComponent<CombatPresentationEffects>();
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
        BuildOverlay();
    }

    private void OnDestroy()
    {
        flashTween?.Kill();
        perfectDodgeToneTween?.Kill();
        letterboxTween?.Kill();

        if (instance == this)
            instance = null;
    }

    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        perfectDodgeToneImage = CreateImage(
            "Perfect Dodge Tone",
            transform,
            new Color(0.32f, 0.36f, 0.4f, 1f));
        StretchFullScreen(perfectDodgeToneImage.rectTransform);
        perfectDodgeToneGroup =
            perfectDodgeToneImage.gameObject.AddComponent<CanvasGroup>();
        perfectDodgeToneGroup.alpha = 0f;

        flashImage = CreateImage("Impact Flash", transform, Color.white);
        StretchFullScreen(flashImage.rectTransform);
        flashGroup = flashImage.gameObject.AddComponent<CanvasGroup>();
        flashGroup.alpha = 0f;

        GameObject letterboxRoot = new GameObject(
            "Cinematic Letterbox",
            typeof(RectTransform),
            typeof(CanvasGroup));
        letterboxRoot.transform.SetParent(transform, false);
        RectTransform letterboxRect = (RectTransform)letterboxRoot.transform;
        StretchFullScreen(letterboxRect);
        letterboxGroup = letterboxRoot.GetComponent<CanvasGroup>();
        letterboxGroup.alpha = 0f;
        letterboxGroup.interactable = false;
        letterboxGroup.blocksRaycasts = false;

        Image topBar = CreateImage("Top Bar", letterboxRoot.transform, Color.black);
        ConfigureBar(topBar.rectTransform, true);

        Image bottomBar = CreateImage("Bottom Bar", letterboxRoot.transform, Color.black);
        ConfigureBar(bottomBar.rectTransform, false);
    }

    private void PlayFlash(Color color, float peakAlpha, float duration)
    {
        if (flashGroup == null || flashImage == null)
            return;

        peakAlpha = Mathf.Clamp01(peakAlpha);
        duration = Mathf.Max(0.08f, duration);

        flashTween?.Kill(false);
        flashImage.color = new Color(color.r, color.g, color.b, 1f);
        flashGroup.alpha = 0f;

        float attackDuration = Mathf.Min(0.06f, duration * 0.4f);
        flashTween = DOTween.Sequence()
            .Append(flashGroup.DOFade(peakAlpha, attackDuration).SetEase(Ease.OutQuad))
            .Append(flashGroup.DOFade(0f, duration - attackDuration).SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }

    private void PlayPerfectDodgeTone()
    {
        if (perfectDodgeToneGroup == null || perfectDodgeToneImage == null)
            return;

        perfectDodgeToneTween?.Kill(false);
        perfectDodgeToneImage.color = new Color(0.32f, 0.36f, 0.4f, 1f);
        perfectDodgeToneGroup.alpha = 0f;

        // 회색빛을 짧게 유지해 파란 캐릭터 강조가 화면에서 분리되어 보이게 한다.
        perfectDodgeToneTween = DOTween.Sequence()
            .Append(perfectDodgeToneGroup
                .DOFade(0.24f, 0.045f)
                .SetEase(Ease.OutQuad))
            .AppendInterval(0.14f)
            .Append(perfectDodgeToneGroup
                .DOFade(0f, 0.42f)
                .SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }

    private void SetLetterboxVisible(bool visible, float duration)
    {
        if (letterboxGroup == null)
            return;

        letterboxTween?.Kill(false);
        duration = Mathf.Max(0.01f, duration);
        letterboxTween = letterboxGroup
            .DOFade(visible ? 1f : 0f, duration)
            .SetEase(visible ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true);
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        child.transform.SetParent(parent, false);

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureBar(RectTransform rect, bool top)
    {
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.sizeDelta = new Vector2(0f, 86f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static Color ResolveElementColor(CombatElement element)
    {
        switch (element)
        {
            case CombatElement.Fire:
                return new Color(1f, 0.2f, 0.04f);
            case CombatElement.Ice:
                return new Color(0.15f, 0.82f, 1f);
            case CombatElement.Physical:
                return new Color(1f, 0.78f, 0.12f);
            case CombatElement.Electric:
                return new Color(0.5f, 0.3f, 1f);
            case CombatElement.Wind:
                return new Color(0.2f, 1f, 0.55f);
            case CombatElement.Ether:
                return new Color(0.95f, 0.2f, 1f);
            default:
                return Color.white;
        }
    }
}
