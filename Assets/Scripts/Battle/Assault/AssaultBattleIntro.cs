using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class AssaultBattleIntro : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private AssaultBattleController battleController;
    [SerializeField] private PartyManager partyManager;

    [Header("Video")]
    [Tooltip("강습전 진입 시 전체 화면으로 재생할 오프닝 영상이다. 비어 있으면 영상을 건너뛴다.")]
    [SerializeField] private VideoClip openingClip;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private CanvasGroup videoGroup;
    [SerializeField] private RawImage videoImage;

    [Header("Skip / Safety")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private Key skipKey = Key.Space;
    [Tooltip("영상 준비가 이 시간을 넘기면 탐색 조작으로 자동 복귀한다.")]
    [SerializeField, Min(1f)] private float prepareTimeout = 8f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;

    private RenderTexture runtimeTexture;
    private Coroutine prepareTimeoutRoutine;
    private Coroutine finishRoutine;
    private bool introCompleted;
    private bool finishRequested;

    public bool IsCompleted => introCompleted;
    public VideoClip OpeningClip => openingClip;
    public event Action IntroFinished;

    private void Awake()
    {
        ResolveReferences();

        // 영상이 준비되는 동안 시작 트리거와 캐릭터 입력을 함께 잠가 재생 순서가 뒤섞이지 않게 한다.
        battleController?.SetBattleEntryEnabled(false);
        partyManager?.SetPartyControlEnabled(false);
    }

    private void Start()
    {
        ResolveReferences();

        if (openingClip == null)
        {
            CompleteIntroImmediately();
            return;
        }

        PrepareOpeningVideo();
    }

    private void Update()
    {
        if (!allowSkip || introCompleted || finishRequested)
            return;

        if (Keyboard.current != null &&
            Keyboard.current[skipKey].wasPressedThisFrame)
        {
            RequestFinish(true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeVideoEvents();
        ReleaseRenderTexture();
    }

    public void Configure(
        AssaultBattleController controller,
        PartyManager party)
    {
        battleController = controller;
        partyManager = party;
    }

    private void ResolveReferences()
    {
        if (battleController == null)
            battleController = GetComponent<AssaultBattleController>();
        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();

        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
    }

    private void PrepareOpeningVideo()
    {
        EnsureVideoView();
        CreateRenderTexture();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = openingClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = runtimeTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        SubscribeVideoEvents();
        // 준비 중에는 검은 배경을 유지하고 첫 프레임이 준비되는 즉시 같은 화면에 영상을 출력한다.
        SetVideoVisible(true, 1f);
        videoPlayer.Prepare();
        prepareTimeoutRoutine = StartCoroutine(WaitForPrepareTimeout());
    }

    private void EnsureVideoView()
    {
        if (videoGroup != null && videoImage != null)
            return;

        GameObject canvasObject = new GameObject(
            "Assault Opening Video",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

        videoGroup = canvasObject.GetComponent<CanvasGroup>();
        videoGroup.interactable = false;
        videoGroup.blocksRaycasts = true;

        GameObject backgroundObject = CreateFullscreenGraphic<Image>(
            "Black Background",
            canvasObject.transform);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;

        GameObject videoObject = CreateFullscreenGraphic<RawImage>(
            "Opening Clip",
            canvasObject.transform);
        videoImage = videoObject.GetComponent<RawImage>();
        videoImage.color = Color.white;
        videoImage.raycastTarget = false;
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

    private void CreateRenderTexture()
    {
        int width = openingClip.width > 0 ? (int)openingClip.width : 1920;
        int height = openingClip.height > 0 ? (int)openingClip.height : 1080;

        runtimeTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "Assault Opening Video Texture"
        };
        runtimeTexture.Create();
        videoImage.texture = runtimeTexture;
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

        preparedPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer finishedPlayer)
    {
        RequestFinish(true);
    }

    private void OnVideoError(VideoPlayer failedPlayer, string message)
    {
        Debug.LogError($"강습전 오프닝 영상 재생 실패: {message}", this);
        RequestFinish(false);
    }

    private IEnumerator WaitForPrepareTimeout()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(1f, prepareTimeout);

        while (!videoPlayer.isPrepared && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        prepareTimeoutRoutine = null;
        if (!videoPlayer.isPrepared)
        {
            Debug.LogError("강습전 오프닝 영상 준비 시간이 초과되어 영상을 건너뜁니다.", this);
            RequestFinish(false);
        }
    }

    private void RequestFinish(bool useFadeOut)
    {
        if (introCompleted || finishRequested)
            return;

        finishRequested = true;
        if (prepareTimeoutRoutine != null)
        {
            StopCoroutine(prepareTimeoutRoutine);
            prepareTimeoutRoutine = null;
        }

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (useFadeOut && videoGroup != null && fadeOutDuration > 0f)
            finishRoutine = StartCoroutine(FadeOutAndComplete());
        else
            CompleteIntroImmediately();
    }

    private IEnumerator FadeOutAndComplete()
    {
        float startAlpha = videoGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeOutDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            videoGroup.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        finishRoutine = null;
        CompleteIntroImmediately();
    }

    private void CompleteIntroImmediately()
    {
        if (introCompleted)
            return;

        introCompleted = true;
        finishRequested = true;
        UnsubscribeVideoEvents();
        SetVideoVisible(false, 0f);
        ReleaseRenderTexture();

        // 오프닝 종료는 전투 시작이 아니라 맵 탐색 허용이다. 실제 보스 생성과 타이머는 기존 트리거가 담당한다.
        battleController?.SetBattleEntryEnabled(true);
        partyManager?.SetPartyControlEnabled(true);
        IntroFinished?.Invoke();
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

    private void SetVideoVisible(bool visible, float alpha)
    {
        if (videoGroup == null)
            return;

        videoGroup.alpha = alpha;
        videoGroup.interactable = false;
        videoGroup.blocksRaycasts = visible;
        videoGroup.gameObject.SetActive(visible);
    }
}
