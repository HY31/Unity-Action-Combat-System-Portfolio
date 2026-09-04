using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 극한 회피·패링·궁극기의 화면 색조, 플래시, 레터박스를 런타임 오버레이로 재생한다.
/// 전투 로직은 건드리지 않고 연출의 생성과 복원만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatPresentationEffects : MonoBehaviour
{
    private static CombatPresentationEffects instance;

    private CanvasGroup flashGroup;
    private Image flashImage;
    private CanvasGroup perfectDodgeToneGroup;
    private Image perfectDodgeToneImage;
    private CanvasGroup chainPromptToneGroup;
    private Image chainPromptToneImage;
    private Tween flashTween;
    private Tween perfectDodgeToneTween;
    private Tween chainPromptToneTween;

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
        // 짧은 정지에 가까운 감속 뒤 천천히 정상 속도로 복귀시켜 극한 회피 성공을 확실히 보여준다.
        HitStop.DoSlowMotion(0.06f, 0.18f, 0.6f);

        CombatPresentationEffects effects = Resolve();
        effects?.PlayPerfectDodgeTone();

        Flash(new Color(0.15f, 0.9f, 1f), 0.12f, 0.24f);

        if (player != null)
            CombatHitVfx.PlayPerfectDodge(player.transform);

        ThirdPersonCameraController.Active?.PunchFieldOfView(5.5f, 0.32f);
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
        Flash(ResolveElementColor(element), 0.08f, 0.24f);
        ThirdPersonCameraController.Active?.PunchFieldOfView(-8f, 0.42f);
    }

    public static void EndUltimate()
    {
        ThirdPersonCameraController.Active?.PunchFieldOfView(3f, 0.25f);
    }

    public static void BeginChainPrompt()
    {
        // 선택 UI가 열린 동안 월드는 거의 정지시키되 unscaled time을 쓰는 UI 입력과 타이머는 유지한다.
        HitStop.BeginSustainedSlowMotion(0.01f);
        Resolve()?.SetChainPromptToneVisible(true);
        Flash(new Color(0.93f, 0.98f, 1f), 0.14f, 0.26f);
        ThirdPersonCameraController.Active?.BeginChainPromptZoom();
    }

    public static void EndChainPrompt()
    {
        HitStop.EndSustainedSlowMotion();
        Resolve()?.SetChainPromptToneVisible(false);
        ThirdPersonCameraController.Active?.EndChainPromptZoom();
    }

    public static void Flash(Color color, float peakAlpha, float duration)
    {
        CombatPresentationEffects effects = Resolve();
        if (effects == null)
            return;

        effects.PlayFlash(color, peakAlpha, duration);
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
        chainPromptToneTween?.Kill();

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
            new Color(0.72f, 0.82f, 0.9f, 1f));
        StretchFullScreen(perfectDodgeToneImage.rectTransform);
        perfectDodgeToneGroup =
            perfectDodgeToneImage.gameObject.AddComponent<CanvasGroup>();
        perfectDodgeToneGroup.alpha = 0f;

        chainPromptToneImage = CreateImage(
            "Chain Prompt Tone",
            transform,
            new Color(0.72f, 0.82f, 0.9f, 1f));
        StretchFullScreen(chainPromptToneImage.rectTransform);
        chainPromptToneGroup =
            chainPromptToneImage.gameObject.AddComponent<CanvasGroup>();
        chainPromptToneGroup.alpha = 0f;

        flashImage = CreateImage("Impact Flash", transform, Color.white);
        StretchFullScreen(flashImage.rectTransform);
        flashGroup = flashImage.gameObject.AddComponent<CanvasGroup>();
        flashGroup.alpha = 0f;

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
        perfectDodgeToneImage.color = new Color(0.72f, 0.82f, 0.9f, 1f);
        perfectDodgeToneGroup.alpha = 0f;

        // 은회색과 하늘빛 색조를 짧게 보여 준 뒤 불릿 타임 복귀와 함께 원래 색으로 돌아온다.
        perfectDodgeToneTween = DOTween.Sequence()
            .Append(perfectDodgeToneGroup
                .DOFade(0.16f, 0.035f)
                .SetEase(Ease.OutQuad))
            .AppendInterval(0.22f)
            .Append(perfectDodgeToneGroup
                .DOFade(0f, 0.5f)
                .SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }

    private void SetChainPromptToneVisible(bool visible)
    {
        if (chainPromptToneGroup == null || chainPromptToneImage == null)
            return;

        chainPromptToneTween?.Kill(false);
        chainPromptToneImage.color = new Color(0.72f, 0.82f, 0.9f, 1f);

        // 은색 바탕 위로 하늘색을 얹고 흰색 플래시를 별도로 겹쳐 붉은 경고색과 구분한다.
        chainPromptToneTween = chainPromptToneGroup
            .DOFade(visible ? 0.16f : 0f, visible ? 0.08f : 0.16f)
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
