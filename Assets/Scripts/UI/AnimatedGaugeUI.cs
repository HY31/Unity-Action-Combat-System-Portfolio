using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI Filled Image의 현재 게이지와 지연 게이지를 부드럽게 갱신하는 공용 UI 컴포넌트다.
/// 가로형 체력바와 Radial360 속성 게이지 모두 같은 정규화 값으로 제어한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimatedGaugeUI : MonoBehaviour
{
    [Header("Images")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayedFillImage;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float fillSpeed = 8f;
    [SerializeField, Min(0f)] private float delayedFillSpeed = 2.5f;
    [SerializeField, Min(0f)] private float delayedFillWait = 0.18f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Editor Preview")]
    [SerializeField, Range(0f, 1f)] private float previewValue = 1f;

    private float targetValue = 1f;
    private float displayedValue = 1f;
    private float delayedValue = 1f;
    private float delayedTimer;

    public float Value => targetValue;
    public Image FillImage => fillImage;
    public Image DelayedFillImage => delayedFillImage;

    private void Awake()
    {
        ConfigureFilledImage(fillImage);
        ConfigureFilledImage(delayedFillImage);
        SnapTo(previewValue);
    }

    private void OnEnable()
    {
        ApplyImages();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        displayedValue = Mathf.MoveTowards(displayedValue, targetValue, fillSpeed * deltaTime);

        // 회복처럼 값이 증가할 때는 지연 게이지를 즉시 따라가게 하고,
        // 피해처럼 감소할 때만 잠시 기다렸다가 느린 속도로 추적시킨다.
        if (targetValue >= delayedValue)
        {
            delayedValue = displayedValue;
            delayedTimer = 0f;
        }
        else if (delayedTimer < delayedFillWait)
        {
            delayedTimer += deltaTime;
        }
        else
        {
            delayedValue = Mathf.MoveTowards(
                delayedValue,
                targetValue,
                delayedFillSpeed * deltaTime);
        }

        ApplyImages();
    }

    private void OnValidate()
    {
        previewValue = Mathf.Clamp01(previewValue);
        ConfigureFilledImage(fillImage);
        ConfigureFilledImage(delayedFillImage);

        if (!Application.isPlaying)
        {
            targetValue = previewValue;
            displayedValue = previewValue;
            delayedValue = previewValue;
            ApplyImages();
        }
    }

    public void SetNormalizedValue(float normalizedValue, bool instant = false)
    {
        // 외부 시스템은 목표값만 전달하고 실제 보간 상태는 이 컴포넌트가 소유한다.
        targetValue = Mathf.Clamp01(normalizedValue);
        previewValue = targetValue;

        if (instant)
            SnapTo(targetValue);
        else if (targetValue < delayedValue)
            delayedTimer = 0f;
    }

    public void SetValue(float current, float maximum, bool instant = false)
    {
        float normalized = maximum > 0f ? current / maximum : 0f;
        SetNormalizedValue(normalized, instant);
    }

    public void SnapTo(float normalizedValue)
    {
        targetValue = Mathf.Clamp01(normalizedValue);
        displayedValue = targetValue;
        delayedValue = targetValue;
        delayedTimer = 0f;
        previewValue = targetValue;
        ApplyImages();
    }

    public void Configure(Image fill, Image delayedFill = null)
    {
        fillImage = fill;
        delayedFillImage = delayedFill;
        ConfigureFilledImage(fillImage);
        ConfigureFilledImage(delayedFillImage);
        ApplyImages();
    }

    private static void ConfigureFilledImage(Image image)
    {
        if (image == null)
            return;

        // 프리팹 생성기나 인스펙터 설정과 무관하게 fillAmount를 사용할 수 있는 타입으로 맞춘다.
        image.type = Image.Type.Filled;
        image.fillAmount = Mathf.Clamp01(image.fillAmount);
    }

    private void ApplyImages()
    {
        if (fillImage != null)
            fillImage.fillAmount = displayedValue;

        if (delayedFillImage != null)
            delayedFillImage.fillAmount = delayedValue;
    }
}
