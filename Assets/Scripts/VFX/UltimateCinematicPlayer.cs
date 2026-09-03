using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 궁극기 영상을 전체 화면으로 준비·재생하고, 재생 중 전투 일시정지와 HUD 숨김을 관리한다.
/// 영상 준비 실패나 상태 조기 종료 시 원래 게임 상태를 반드시 복원한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class UltimateCinematicPlayer : MonoBehaviour
{
    private const int OverlaySortingOrder = 32767;
    private const float PrepareTimeout = 5f;
    private const float FadeInDuration = 0.06f;
    private const float FadeOutDuration = 0.14f;

    private static UltimateCinematicPlayer instance;
    private static bool isPlaying;
    private static bool hasStartedPlayback;

    private VideoPlayer videoPlayer;
    private CanvasGroup overlayGroup;
    private RawImage videoImage;
    private RenderTexture runtimeTexture;
    private Coroutine prepareTimeoutRoutine;
    private Coroutine fadeRoutine;
    private bool finishRequested;
    private bool previousExternalPause;
    private readonly System.Collections.Generic.List<Canvas> hiddenCanvases =
        new System.Collections.Generic.List<Canvas>();

    // 준비·재생·페이드 아웃을 포함해 궁극기 오버레이가 전투를 점유하는 전체 구간이다.
    public static bool IsPlaying => isPlaying;
    // Prepare가 끝나 영상 시간이 실제로 흐르기 시작했는지를 상태머신 동기화에 제공한다.
    public static bool HasStartedPlayback => hasStartedPlayback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isPlaying = false;
        hasStartedPlayback = false;
    }

    public static bool TryPlay(VideoClip clip)
    {
        if (clip == null || isPlaying)
            return false;

        if (instance == null)
        {
            GameObject root = new GameObject("Ultimate Cinematic Player (Runtime)");
            instance = root.AddComponent<UltimateCinematicPlayer>();
        }

        return instance.BeginPlayback(clip);
    }

    public static void StopPlayback()
    {
        if (instance != null && isPlaying)
            instance.RequestFinish(false);
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
        BuildRuntimeView();
    }

    private void OnDestroy()
    {
        UnsubscribeVideoEvents();
        RestoreGameplay();
        ReleaseRenderTexture();

        if (instance == this)
            instance = null;
    }

    private bool BeginPlayback(VideoClip clip)
    {
        if (clip == null || isPlaying)
            return false;

        finishRequested = false;
        isPlaying = true;
        hasStartedPlayback = false;
        previousExternalPause = HitStop.IsExternallyPaused;

        HideExistingCanvases();

        CombatAudio.SetCinematicPlaybackActive(true);
        HitStop.SetExternalPause(true);

        CreateRenderTexture(clip);
        SetOverlayVisible(true);
        overlayGroup.alpha = 1f;
        SetVideoAlpha(0f);

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = runtimeTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;

        SubscribeVideoEvents();
        videoPlayer.Prepare();
        prepareTimeoutRoutine = StartCoroutine(WaitForPrepareTimeout());
        return true;
    }

    private void BuildRuntimeView()
    {
        videoPlayer = gameObject.AddComponent<VideoPlayer>();

        GameObject canvasObject = new GameObject(
            "Ultimate Cinematic Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        overlayGroup = canvasObject.GetComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;

        GameObject backgroundObject = CreateFullscreenGraphic<Image>(
            "Black Background",
            canvasObject.transform);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;

        GameObject videoObject = CreateFullscreenGraphic<RawImage>(
            "Ultimate Clip",
            canvasObject.transform);
        videoImage = videoObject.GetComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.raycastTarget = false;

        AspectRatioFitter fitter = videoObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        SetOverlayVisible(false);
    }

    private static GameObject CreateFullscreenGraphic<T>(
        string objectName,
        Transform parent)
        where T : Graphic
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(T));
        child.transform.SetParent(parent, false);

        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child;
    }

    private void CreateRenderTexture(VideoClip clip)
    {
        ReleaseRenderTexture();

        int width = clip.width > 0 ? (int)clip.width : 1920;
        int height = clip.height > 0 ? (int)clip.height : 1080;
        runtimeTexture = new RenderTexture(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32)
        {
            name = "Ultimate Cinematic Texture"
        };
        runtimeTexture.Create();
        videoImage.texture = runtimeTexture;

        AspectRatioFitter fitter = videoImage.GetComponent<AspectRatioFitter>();
        if (fitter != null)
            fitter.aspectRatio = width / (float)Mathf.Max(1, height);
    }

    private void ReleaseRenderTexture()
    {
        if (runtimeTexture == null)
            return;

        if (videoPlayer != null)
            videoPlayer.targetTexture = null;
        if (videoImage != null)
            videoImage.texture = null;

        runtimeTexture.Release();
        Destroy(runtimeTexture);
        runtimeTexture = null;
    }

    private void SubscribeVideoEvents()
    {
        UnsubscribeVideoEvents();
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
    }

    private void UnsubscribeVideoEvents()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.errorReceived -= OnVideoError;
    }

    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        if (finishRequested)
            return;

        if (prepareTimeoutRoutine != null)
        {
            StopCoroutine(prepareTimeoutRoutine);
            prepareTimeoutRoutine = null;
        }

        bool muteVideoAudio = CombatAudio.IsMasterMuted;
        for (ushort track = 0; track < preparedPlayer.audioTrackCount; track++)
        {
            preparedPlayer.EnableAudioTrack(track, true);
            preparedPlayer.SetDirectAudioMute(track, muteVideoAudio);
            preparedPlayer.SetDirectAudioVolume(track, muteVideoAudio ? 0f : 1f);
        }

        preparedPlayer.Play();
        hasStartedPlayback = true;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeVideoIn());
    }

    private void OnVideoFinished(VideoPlayer finishedPlayer)
    {
        RequestFinish(true);
    }

    private void OnVideoError(VideoPlayer failedPlayer, string message)
    {
        Debug.LogError($"궁극기 영상 재생에 실패했습니다: {message}", this);
        RequestFinish(false);
    }

    private IEnumerator WaitForPrepareTimeout()
    {
        float elapsed = 0f;

        while (!videoPlayer.isPrepared && elapsed < PrepareTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        prepareTimeoutRoutine = null;
        if (!videoPlayer.isPrepared)
        {
            Debug.LogError("궁극기 영상 준비 시간이 초과되어 인게임 연출로 전환합니다.", this);
            RequestFinish(false);
        }
    }

    private IEnumerator FadeVideoIn()
    {
        float elapsed = 0f;

        while (elapsed < FadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetVideoAlpha(Mathf.Clamp01(elapsed / FadeInDuration));
            yield return null;
        }

        SetVideoAlpha(1f);
        fadeRoutine = null;
    }

    private void RequestFinish(bool useFadeOut)
    {
        if (!isPlaying || finishRequested)
            return;

        finishRequested = true;

        if (prepareTimeoutRoutine != null)
        {
            StopCoroutine(prepareTimeoutRoutine);
            prepareTimeoutRoutine = null;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (useFadeOut)
            fadeRoutine = StartCoroutine(FadeOutAndComplete());
        else
            CompletePlayback();
    }

    private IEnumerator FadeOutAndComplete()
    {
        float startAlpha = overlayGroup.alpha;
        float elapsed = 0f;

        while (elapsed < FadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                Mathf.Clamp01(elapsed / FadeOutDuration));
            yield return null;
        }

        fadeRoutine = null;
        CompletePlayback();
    }

    private void CompletePlayback()
    {
        UnsubscribeVideoEvents();
        SetOverlayVisible(false);
        ReleaseRenderTexture();
        RestoreGameplay();
        finishRequested = false;
    }

    private void HideExistingCanvases()
    {
        RestoreHiddenCanvases();

        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null ||
                !canvas.isActiveAndEnabled ||
                canvas.transform.IsChildOf(transform))
            {
                continue;
            }

            hiddenCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    private void RestoreHiddenCanvases()
    {
        for (int i = 0; i < hiddenCanvases.Count; i++)
        {
            Canvas canvas = hiddenCanvases[i];
            if (canvas != null)
                canvas.enabled = true;
        }

        hiddenCanvases.Clear();
    }


    private void RestoreGameplay()
    {
        RestoreHiddenCanvases();

        if (!isPlaying)
            return;

        isPlaying = false;
        hasStartedPlayback = false;
        CombatAudio.SetCinematicPlaybackActive(false);
        HitStop.SetExternalPause(previousExternalPause);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayGroup == null)
            return;

        overlayGroup.alpha = visible ? 1f : 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = visible;
        overlayGroup.gameObject.SetActive(visible);
    }

    private void SetVideoAlpha(float alpha)
    {
        if (videoImage == null)
            return;

        Color color = videoImage.color;
        color.a = Mathf.Clamp01(alpha);
        videoImage.color = color;
    }
}
