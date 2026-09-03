using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세 개의 전투 각도를 정지 화면으로 캡처하고 캐릭터를 WIPEOUT 문구 앞에 합성한다.
/// 원작의 층 분리형 전투 종료 연출을 한 개의 런타임 오버레이에서 재생한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ZZZWipeoutLayeredDirector : MonoBehaviour
{
    private const int ShotCount = 3;
    private const int ForegroundCaptureLayer = 31;
    private const float ForegroundContrast = 1.15f;
    private const float ForegroundPosterizeSteps = 8f;
    private const float ForegroundGrayFloor = 0.16f;
    private const float ForegroundGrayCeiling = 0.94f;
    private const float ForegroundOpacity = 0.85f;
    private static readonly float[] ShotBoundaries = { 0f, 0.31f, 0.62f, 1f };

    private float duration = 5f;
    private float introDuration = 0.12f;
    private float outroDuration = 0.22f;
    private Color yellow = new Color32(255, 222, 0, 255);
    private Color cyan = new Color32(16, 205, 255, 210);
    private Color red = new Color32(255, 35, 82, 210);

    private CanvasGroup root;
    private RawImage backgroundFrame;
    private RawImage foregroundFrame;
    private Image yellowWash;
    private Image cyanSlash;
    private Image redSlash;
    private Image cutFlash;
    private RectTransform titleGroup;
    private Text title;
    private Text titleShadow;
    private Text cyanEcho;
    private Text redEcho;
    private Material backgroundMaterial;
    private Material foregroundMaterial;

    private readonly Texture2D[] backgrounds = new Texture2D[ShotCount];
    private readonly Texture2D[] foregrounds = new Texture2D[ShotCount];
    private readonly List<Canvas> hiddenCanvases = new();
    private readonly List<LayerState> changedLayers = new();

    private readonly struct LayerState
    {
        public readonly GameObject Target;
        public readonly int Layer;

        public LayerState(GameObject target, int layer)
        {
            Target = target;
            Layer = layer;
        }
    }

    public void Configure(
        float totalDuration,
        float introTime,
        float outroTime,
        Color accentYellow,
        Color accentCyan,
        Color accentRed)
    {
        // 세 장면이 의도적인 화면 전환으로 읽히도록 최소 재생 시간을 보장한다.
        duration = Mathf.Max(2.15f, totalDuration);
        introDuration = Mathf.Clamp(introTime, 0.05f, 0.3f);
        outroDuration = Mathf.Clamp(outroTime, 0.08f, 0.45f);
        yellow = accentYellow;
        cyan = accentCyan;
        red = accentRed;
    }

    public IEnumerator Play(bool partyDefeated)
    {
        EnsureView();
        EnsureForegroundMaterial();
        ResetPresentation();

        // 정지 화면에 디버그 OnGUI가 남지 않도록 한 프레임 기다린다.
        yield return null;
        if (!CaptureThreeAngles())
            yield break;

        // 캡처 직후 HUD 캔버스를 전부 되살리면 별도 정렬 캔버스에 있는
        // 데시벨 UI가 WIPEOUT 오버레이 위로 다시 그려진다. 연출이 끝날 때까지
        // 오버레이가 속한 캔버스만 남기고 나머지 전투 HUD를 숨긴다.
        HideExternalCanvasesForPresentation();

        ConfigureTitle(partyDefeated);
        if (backgroundMaterial != null)
        {
            backgroundMaterial.SetFloat("_FailureBlend", partyDefeated ? 1f : 0f);
            backgroundMaterial.SetFloat("_ChromaticShift", 0.004f);
        }

        yellowWash.color = partyDefeated
            ? new Color(0.55f, 0.01f, 0.005f, 0.12f)
            : new Color(1f, 0.67f, 0f, 0.11f);

        SetVisible(true);
        int currentShot = -1;
        float shotStartedAt = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            int shot = ResolveShotIndex(normalized);
            if (shot != currentShot)
            {
                currentShot = shot;
                shotStartedAt = elapsed;
                ApplyShot(shot);
            }

            float shotAge = Mathf.Max(0f, elapsed - shotStartedAt);
            float shotProgress = ResolveShotProgress(normalized, currentShot);
            float intro = SmoothStep01(Mathf.Clamp01(elapsed / introDuration));
            float outro = SmoothStep01(Mathf.Clamp01(
                (elapsed - (duration - outroDuration)) / outroDuration));
            root.alpha = intro * (1f - outro);

            // 각 정지 화면을 안쪽으로 조금 이동시켜 움직이는 만화 패널처럼 보이게 한다.
            float zoom = Mathf.Lerp(1.055f, 1.012f, SmoothStep01(shotProgress));
            backgroundFrame.rectTransform.localScale = Vector3.one * zoom;
            foregroundFrame.rectTransform.localScale = Vector3.one * zoom;

            float impact = Mathf.Clamp01(1f - shotAge / 0.13f);
            titleGroup.localScale = Vector3.one * (1f + impact * 0.085f);
            float titleJitter = Mathf.Sin(elapsed * 42f) * impact * 6f;
            titleGroup.anchoredPosition = new Vector2(titleJitter, 0f);

            SetImageAlpha(cyanSlash, impact * 0.78f);
            SetImageAlpha(redSlash, impact * 0.68f);
            SetImageAlpha(cutFlash, Mathf.Clamp01(1f - shotAge / 0.075f) * 0.58f);

            float echoJitter = Mathf.Sin(elapsed * 36f) * 2.4f;
            cyanEcho.rectTransform.anchoredPosition = new Vector2(-8f + echoJitter, 3f);
            redEcho.rectTransform.anchoredPosition = new Vector2(8f - echoJitter, -3f);

            if (backgroundMaterial != null)
            {
                backgroundMaterial.SetFloat(
                    "_ChromaticShift",
                    Mathf.Lerp(0.0022f, 0.009f, impact));
            }

            yield return null;
        }

        SetVisible(false);
        RestoreCapturedCanvases();
        ReleaseFrames();
    }

    public void ResetPresentation()
    {
        RestoreChangedLayers();
        RestoreCapturedCanvases();
        SetVisible(false);
        ReleaseFrames();
    }

    private bool CaptureThreeAngles()
    {
        Camera sourceCamera = Camera.main != null
            ? Camera.main
            : FindFirstObjectByType<Camera>();
        if (sourceCamera == null)
        {
            Debug.LogWarning("WIPEOUT 캡처를 건너뜁니다: 사용할 카메라를 찾지 못했습니다.", this);
            return false;
        }

        PlayerController actor = ResolveActivePlayer();
        EnemyController enemy = ResolveEnemy(actor);
        Vector3 actorFocus = ResolveVisualCenter(
            actor != null ? actor.transform : null,
            sourceCamera.transform.position + sourceCamera.transform.forward * 5f);
        Vector3 enemyFocus = ResolveVisualCenter(
            enemy != null ? enemy.transform : null,
            actorFocus + sourceCamera.transform.forward * 3f);

        // 캐릭터가 바라보는 방향을 12시로 두고 종료 카메라를 시계 방향 12·4·7시에 고정한다.
        Vector3 twelveOClock = actor != null
            ? actor.transform.forward
            : sourceCamera.transform.forward;
        twelveOClock.y = 0f;
        if (twelveOClock.sqrMagnitude < 0.01f)
            twelveOClock = Vector3.forward;
        twelveOClock.Normalize();

        Vector3[] clockDirections =
        {
            DirectionAtClock(twelveOClock, 12f),
            DirectionAtClock(twelveOClock, 4f),
            DirectionAtClock(twelveOClock, 7f)
        };
        float combatDistance = Vector3.Distance(
            new Vector3(actorFocus.x, 0f, actorFocus.z),
            new Vector3(enemyFocus.x, 0f, enemyFocus.z));
        float cameraDistance = Mathf.Clamp(4.8f + combatDistance * 0.1f, 4.8f, 6.4f);

        Vector3[] positions =
        {
            actorFocus + clockDirections[0] * (cameraDistance * 1.06f) + Vector3.up * 1.35f,
            actorFocus + clockDirections[1] * cameraDistance + Vector3.up * 1.5f,
            actorFocus + clockDirections[2] * (cameraDistance * 1.08f) + Vector3.up * 1.4f
        };
        Vector3[] targets =
        {
            Vector3.Lerp(actorFocus, enemyFocus, 0.14f),
            Vector3.Lerp(actorFocus, enemyFocus, 0.12f),
            Vector3.Lerp(actorFocus, enemyFocus, 0.16f)
        };
        float[] fieldsOfView = { 46f, 45f, 47f };
        float[] rolls = { -1.2f, 1.6f, -1.4f };

        GameObject cameraObject = new GameObject("[Temp] WIPEOUT Capture Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.CopyFrom(sourceCamera);
        captureCamera.enabled = false;

        HideCanvasesForCapture();
        try
        {
            for (int i = 0; i < ShotCount; i++)
            {
                ApplyCameraPose(captureCamera, positions[i], targets[i], fieldsOfView[i], rolls[i]);
                backgrounds[i] = RenderCamera(captureCamera, false);

                MoveCombatantsToForegroundLayer();
                foregrounds[i] = RenderCamera(captureCamera, true);
                RestoreChangedLayers();
            }
        }
        finally
        {
            RestoreChangedLayers();
            RestoreCapturedCanvases();
            Destroy(cameraObject);
        }

        return backgrounds[0] != null;
    }

    private static void ApplyCameraPose(
        Camera camera,
        Vector3 position,
        Vector3 target,
        float fieldOfView,
        float roll)
    {
        Vector3 direction = target - position;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        rotation *= Quaternion.Euler(0f, 0f, roll);
        camera.transform.SetPositionAndRotation(position, rotation);
        camera.fieldOfView = fieldOfView;
    }

    private static Texture2D RenderCamera(Camera camera, bool foregroundOnly)
    {
        int width = Mathf.Max(2, Screen.width);
        int height = Mathf.Max(2, Screen.height);
        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        CameraClearFlags previousClearFlags = camera.clearFlags;
        Color previousBackground = camera.backgroundColor;
        int previousMask = camera.cullingMask;

        try
        {
            camera.targetTexture = target;
            if (foregroundOnly)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << ForegroundCaptureLayer;
            }

            camera.Render();
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = foregroundOnly
                ? "WIPEOUT Foreground"
                : "WIPEOUT Background";
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            texture.Apply(false, false);
            return texture;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.clearFlags = previousClearFlags;
            camera.backgroundColor = previousBackground;
            camera.cullingMask = previousMask;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private void MoveHierarchyToForegroundLayer(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child == null)
                continue;
            changedLayers.Add(new LayerState(child.gameObject, child.gameObject.layer));
            child.gameObject.layer = ForegroundCaptureLayer;
        }
    }

    private void MoveCombatantsToForegroundLayer()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (PlayerController player in players)
            MoveHierarchyToForegroundLayer(player != null ? player.gameObject : null);

        EnemyController[] enemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
            MoveHierarchyToForegroundLayer(enemy != null ? enemy.gameObject : null);
    }

    private void RestoreChangedLayers()
    {
        for (int i = changedLayers.Count - 1; i >= 0; i--)
        {
            LayerState state = changedLayers[i];
            if (state.Target != null)
                state.Target.layer = state.Layer;
        }
        changedLayers.Clear();
    }

    private void HideCanvasesForCapture()
    {
        hiddenCanvases.Clear();
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.enabled)
                continue;
            hiddenCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    private void RestoreCapturedCanvases()
    {
        foreach (Canvas canvas in hiddenCanvases)
        {
            if (canvas != null)
                canvas.enabled = true;
        }
        hiddenCanvases.Clear();
    }

    private void HideExternalCanvasesForPresentation()
    {
        hiddenCanvases.Clear();

        Canvas overlayCanvas = root != null
            ? root.GetComponentInParent<Canvas>()
            : null;
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.enabled || canvas == overlayCanvas)
                continue;

            hiddenCanvases.Add(canvas);
            canvas.enabled = false;
        }
    }

    private void ApplyShot(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, ShotCount - 1);
        backgroundFrame.texture = backgrounds[safeIndex];
        foregroundFrame.texture = foregrounds[safeIndex];
        titleGroup.localRotation = Quaternion.Euler(
            0f,
            0f,
            safeIndex == 1 ? 1.2f : -1.4f);
    }

    private void ConfigureTitle(bool partyDefeated)
    {
        string message = partyDefeated ? "MISSION FAILED" : "WIPEOUT";
        Color mainColor = partyDefeated
            ? new Color32(255, 67, 38, 255)
            : yellow;

        title.fontSize = partyDefeated ? 188 : 250;
        titleShadow.fontSize = title.fontSize;
        cyanEcho.fontSize = title.fontSize;
        redEcho.fontSize = title.fontSize;
        ConfigureText(titleShadow, message, new Color(0.015f, 0.012f, 0.005f, 0.92f));
        ConfigureText(cyanEcho, message, cyan);
        ConfigureText(redEcho, message, red);
        ConfigureText(title, message, mainColor);
    }

    private void EnsureView()
    {
        if (root != null)
            return;

        GameObject rootObject = CreateObject("ZZZWipeoutLayered", transform, typeof(CanvasGroup));
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.transform.SetAsLastSibling();
        root = rootObject.GetComponent<CanvasGroup>();

        backgroundFrame = CreateObject("BackgroundFrame", rootObject.transform, typeof(RawImage))
            .GetComponent<RawImage>();
        Stretch(backgroundFrame.rectTransform);
        backgroundFrame.raycastTarget = false;

        Shader shader = Shader.Find("Hidden/ZZZ/WipeoutCinematic");
        if (shader != null)
        {
            backgroundMaterial = new Material(shader);
            backgroundFrame.material = backgroundMaterial;
        }

        yellowWash = CreateImage("YellowWash", rootObject.transform, Color.clear);
        Stretch(yellowWash.rectTransform);

        cyanSlash = CreateImage("CyanCut", rootObject.transform, cyan);
        SetAnchoredRect(cyanSlash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(2450f, 185f), new Vector2(-170f, 105f));
        cyanSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);

        redSlash = CreateImage("RedCut", rootObject.transform, red);
        SetAnchoredRect(redSlash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(2350f, 95f), new Vector2(160f, -115f));
        redSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        GameObject titleObject = CreateObject("CenteredWipeoutTitle", rootObject.transform);
        titleGroup = titleObject.GetComponent<RectTransform>();
        SetAnchoredRect(titleGroup, new Vector2(0.5f, 0.5f), new Vector2(2200f, 360f), Vector2.zero);

        cyanEcho = CreateTitle("CyanEcho", titleGroup, cyan, 250);
        redEcho = CreateTitle("RedEcho", titleGroup, red, 250);
        titleShadow = CreateTitle("InkShadow", titleGroup, Color.black, 250);
        titleShadow.rectTransform.anchoredPosition = new Vector2(13f, -13f);
        title = CreateTitle("MainTitle", titleGroup, yellow, 250);

        foregroundFrame = CreateObject("FightersForeground", rootObject.transform, typeof(RawImage))
            .GetComponent<RawImage>();
        Stretch(foregroundFrame.rectTransform);
        foregroundFrame.raycastTarget = false;

        EnsureForegroundMaterial();

        cutFlash = CreateImage("CutFlash", rootObject.transform, Color.white);
        Stretch(cutFlash.rectTransform);
        SetVisible(false);
    }
    private void EnsureForegroundMaterial()
    {
        if (foregroundFrame == null || foregroundMaterial != null)
            return;

        Shader shader = Shader.Find("Hidden/ZZZ/WipeoutForeground");
        if (shader == null)
            return;

        foregroundMaterial = new Material(shader);
        foregroundMaterial.SetFloat("_Contrast", ForegroundContrast);
        foregroundMaterial.SetFloat("_PosterizeSteps", ForegroundPosterizeSteps);
        foregroundMaterial.SetFloat("_GrayFloor", ForegroundGrayFloor);
        foregroundMaterial.SetFloat("_GrayCeiling", ForegroundGrayCeiling);
        foregroundMaterial.SetFloat("_Opacity", ForegroundOpacity);
        foregroundFrame.material = foregroundMaterial;
    }


    private void ReleaseFrames()
    {
        if (backgroundFrame != null)
            backgroundFrame.texture = null;
        if (foregroundFrame != null)
            foregroundFrame.texture = null;

        for (int i = 0; i < ShotCount; i++)
        {
            if (backgrounds[i] != null)
                Destroy(backgrounds[i]);
            if (foregrounds[i] != null)
                Destroy(foregrounds[i]);
            backgrounds[i] = null;
            foregrounds[i] = null;
        }
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
            return;
        root.alpha = visible ? 1f : 0f;
        root.interactable = false;
        root.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        ResetPresentation();
        if (backgroundMaterial != null)
            Destroy(backgroundMaterial);
        if (foregroundMaterial != null)
            Destroy(foregroundMaterial);
    }

    private static PlayerController ResolveActivePlayer()
    {
        PartyManager party = FindFirstObjectByType<PartyManager>();
        PlayerController current = party != null ? party.GetCurrentCharacter() : null;
        return current != null ? current : FindFirstObjectByType<PlayerController>();
    }

    private static EnemyController ResolveEnemy(PlayerController actor)
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (enemies.Length == 0)
            return null;
        if (actor == null)
            return enemies[0];

        EnemyController nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyController candidate in enemies)
        {
            if (candidate == null)
                continue;
            float distance = (candidate.transform.position - actor.transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private static Vector3 ResolveVisualCenter(Transform target, Vector3 fallback)
    {
        if (target == null)
            return fallback;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found ? bounds.center : target.position + Vector3.up * 1.2f;
    }

    private static Vector3 DirectionAtClock(Vector3 twelveOClock, float hour)
    {
        float clockwiseDegrees = Mathf.Repeat(hour, 12f) * 30f;
        return Quaternion.AngleAxis(clockwiseDegrees, Vector3.up) * twelveOClock;
    }

    private static int ResolveShotIndex(float normalized)
    {
        if (normalized < ShotBoundaries[1])
            return 0;
        return normalized < ShotBoundaries[2] ? 1 : 2;
    }

    private static float ResolveShotProgress(float normalized, int shot)
    {
        int safeShot = Mathf.Clamp(shot, 0, ShotCount - 1);
        return Mathf.InverseLerp(
            ShotBoundaries[safeShot],
            ShotBoundaries[safeShot + 1],
            normalized);
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
    {
        System.Type[] allComponents = new System.Type[components.Length + 2];
        allComponents[0] = typeof(RectTransform);
        allComponents[1] = typeof(CanvasRenderer);
        for (int i = 0; i < components.Length; i++)
            allComponents[i + 2] = components[i];

        GameObject result = new GameObject(name, allComponents);
        result.transform.SetParent(parent, false);
        return result;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateObject(name, parent, typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateTitle(string name, Transform parent, Color color, int fontSize)
    {
        GameObject textObject = CreateObject(name, parent, typeof(Text), typeof(Outline));
        Text text = textObject.GetComponent<Text>();
        Stretch(text.rectTransform);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.BoldAndItalic;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(5f, -5f);
        return text;
    }

    private static void ConfigureText(Text target, string message, Color color)
    {
        if (target == null)
            return;
        target.text = message;
        target.color = color;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchor,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }
}
