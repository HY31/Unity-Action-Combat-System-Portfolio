#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CombatAttackAuthoringWindow : EditorWindow
{
    private enum TrackKind
    {
        Warning,
        Hit,
        Reaction,
        Tracking,
        Movement
    }

    private static readonly Color WarningColor = new Color(1f, 0.78f, 0.08f);
    private static readonly Color HitColor = new Color(1f, 0.22f, 0.16f);
    private static readonly Color ReactionColor = new Color(0.25f, 0.75f, 1f);
    private static readonly Color TrackingColor = new Color(0.72f, 0.35f, 1f);
    private static readonly Color MovementColor = new Color(0.25f, 0.9f, 0.45f);

    private EnemyAttackData attackData;
    private Animator previewAnimator;
    private AnimationClip clip;
    private bool editFollowUp;
    private bool previewPlaying;
    private bool loopPreview = true;
    private float normalizedTime;
    private double lastEditorTime;
    private Vector2 scroll;

    private PreviewRenderUtility embeddedPreview;
    private GameObject embeddedPreviewObject;
    private int embeddedPreviewSourceId;
    private Vector3 embeddedPreviewFocus;
    private float embeddedPreviewDistance = 3f;
    private Vector2 embeddedPreviewOrbit = new Vector2(145f, 12f);
    private float embeddedPreviewZoom = 1f;
    private bool draggingEmbeddedPreview;
    private bool scrubbingPlayhead;
    private BoxCollider splitCaptureCollider;
    private UnityEngine.Object splitCaptureOriginalSelection;
    private bool splitCaptureOriginalGameObjectActive;
    private bool splitCaptureOriginalEnabled;
    private Vector3 splitCaptureOriginalCenter;
    private Vector3 splitCaptureOriginalSize;
    private bool splitCaptureActive;

    private TrackKind draggingTrack;
    private int draggingWindow = -1;
    private int draggingEdge;
    private bool draggingMovement;
    private bool creatingWindow;
    private TrackKind creatingTrack;
    private float creatingWindowStart;
    private EnemyAttackWindow creatingWindowPreview;
    private readonly List<Vector3[]> sweepTrailSegments = new List<Vector3[]>();
    private int sweepTrailHash;
    [MenuItem("Tools/Combat/Attack Authoring")]
    public static void Open()
    {
        GetWindow<CombatAttackAuthoringWindow>("Attack Authoring");
    }

    [MenuItem("Assets/Open in Combat Attack Authoring", true)]
    private static bool ValidateOpenSelected()
    {
        return Selection.activeObject is EnemyAttackData;
    }

    [MenuItem("Assets/Open in Combat Attack Authoring")]
    private static void OpenSelected()
    {
        CombatAttackAuthoringWindow window = GetWindow<CombatAttackAuthoringWindow>("Attack Authoring");
        window.SetAttackData(Selection.activeObject as EnemyAttackData);
    }

    [MenuItem("Tools/Combat/Validate All Enemy Attacks")]
    public static void ValidateAllAttackAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyAttackData");
        int issueCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyAttackData data = AssetDatabase.LoadAssetAtPath<EnemyAttackData>(path);
            if (data == null)
                continue;

            List<string> issues = ValidateAttackAsset(data);
            if (issues.Count == 0)
                continue;

            issueCount += issues.Count;
            Debug.LogWarning($"[Combat Authoring] {path}\n- {string.Join("\n- ", issues)}", data);
        }

        Debug.Log(
            issueCount == 0
                ? $"[Combat Authoring] EnemyAttackData {guids.Length}개 검증 완료: 문제 없음"
                : $"[Combat Authoring] EnemyAttackData {guids.Length}개에서 문제 {issueCount}개 발견");
    }

    private static List<string> ValidateAttackAsset(EnemyAttackData data)
    {
        List<string> issues = new List<string>();
        ValidateClipName("Main", data.attackAnim, issues);
        if (!string.IsNullOrWhiteSpace(data.followUpAnim))
            ValidateClipName("Follow-up", data.followUpAnim, issues);

        if (data.useTimingWindows)
        {
            ValidateWindows("Warning", data.warningWindows, issues);
            ValidateWindows("Hit", data.activeWindows, issues);
            ValidateWindows("Reaction", data.reactionWindows, issues);
        }
        else
        {
            ValidateWindows("Warning", new[] { new EnemyAttackWindow { start = data.warningStart, end = data.warningEnd } }, issues);
            ValidateWindows("Hit", new[] { new EnemyAttackWindow { start = data.startUpEnd, end = data.activeEnd } }, issues);
            ValidateWindows("Reaction", new[] { new EnemyAttackWindow { start = data.reactionStart, end = data.reactionEnd } }, issues);
        }

        if (!string.IsNullOrWhiteSpace(data.followUpAnim))
        {
            ValidateWindows("Follow-up Warning", data.followUpWarningWindows, issues);
            ValidateWindows("Follow-up Hit", data.followUpActiveWindows, issues);
            ValidateWindows("Follow-up Reaction", data.followUpReactionWindows, issues);
        }

        ValidateSweepSettings(
            "Main",
            data.hitDetectionMode,
            data.weaponSweepBoneName,
            data.weaponSweepLocalStart,
            data.weaponSweepLocalEnd,
            data.weaponSweepPathSamples,
            issues);

        if (data.overrideFollowUpHitDetection)
        {
            ValidateSweepSettings(
                "Follow-up",
                data.followUpHitDetectionMode,
                data.followUpWeaponSweepBoneName,
                data.followUpWeaponSweepLocalStart,
                data.followUpWeaponSweepLocalEnd,
                data.followUpWeaponSweepPathSamples,
                issues);
        }

        return issues;
    }

    private static void ValidateClipName(string label, string clipName, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            issues.Add($"{label} AnimationClip 이름이 비어 있습니다.");
            return;
        }

        string[] matches = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");
        bool found = false;
        foreach (string guid in matches)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip candidate && candidate.name == clipName)
                {
                    found = true;
                    break;
                }
            }

            if (found)
                break;
        }

        if (!found)
            issues.Add($"{label} AnimationClip '{clipName}'을 찾지 못했습니다.");
    }

    private static void ValidateSweepSettings(
        string label,
        EnemyHitDetectionMode mode,
        string boneName,
        Vector3 localStart,
        Vector3 localEnd,
        int pathSamples,
        List<string> issues)
    {
        if (mode != EnemyHitDetectionMode.WeaponSweep)
            return;

        if (string.IsNullOrWhiteSpace(boneName))
            issues.Add($"{label} Weapon Sweep bone 이름이 비어 있습니다.");
        if ((localEnd - localStart).sqrMagnitude < 0.0001f)
            issues.Add($"{label} Weapon Sweep 시작점과 끝점이 같습니다.");
        if (pathSamples < 2)
            issues.Add($"{label} Weapon Sweep 경로 샘플은 2 이상이어야 합니다.");
    }

    private void OnEnable()
    {
        minSize = new Vector2(720f, 620f);
        EditorApplication.update += EditorUpdate;
        SceneView.duringSceneGui += DuringSceneGUI;
        lastEditorTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= DuringSceneGUI;
        StopPreview();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is EnemyAttackData selected)
            SetAttackData(selected);
    }

    private void OnGUI()
    {
        DrawHeader();

        if (attackData == null)
        {
            EditorGUILayout.HelpBox(
                "EnemyAttackData 에셋을 선택하거나 위 필드에 지정하세요.",
                MessageType.Info);
            return;
        }

        ResolveClipIfNeeded();
        DrawEmbeddedPreview();
        DrawPreviewControls();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawTimeline();
        DrawHitDetectionSettings();
        DrawValidation();
        EditorGUILayout.EndScrollView();
        HandleKeyboardShortcuts();
    }

    private void DrawHeader()
    {
        EditorGUI.BeginChangeCheck();
        EnemyAttackData selected = (EnemyAttackData)EditorGUILayout.ObjectField(
            "Attack Data",
            attackData,
            typeof(EnemyAttackData),
            false);
        if (EditorGUI.EndChangeCheck())
            SetAttackData(selected);

        EditorGUI.BeginChangeCheck();
        Animator selectedAnimator = (Animator)EditorGUILayout.ObjectField(
            new GUIContent("Preview Animator", "씬의 도살자 Animator를 지정하면 프레임과 판정을 함께 볼 수 있습니다."),
            previewAnimator,
            typeof(Animator),
            true);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
            previewAnimator = selectedAnimator;
            ResolveClip();
            SamplePreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            bool canFollowUp = !string.IsNullOrEmpty(attackData != null ? attackData.followUpAnim : null);
            EditorGUI.BeginDisabledGroup(!canFollowUp);
            bool nextFollowUp = GUILayout.Toggle(editFollowUp, "Edit Follow-up", "Button");
            EditorGUI.EndDisabledGroup();

            if (nextFollowUp != editFollowUp)
            {
                editFollowUp = nextFollowUp;
                normalizedTime = 0f;
                ResolveClip();
                SamplePreview();
            }

            if (GUILayout.Button("Find Scene Enemy", GUILayout.Width(140f)))
                FindSceneEnemyAnimator();

            if (GUILayout.Button("Prepare Split Capture", GUILayout.Width(170f)))
                PrepareSplitCapture();
        }
    }

    private void PrepareSplitCapture()
    {
        EndSceneColliderPreview();
        FindSceneEnemyAnimator();
        ResolveClip();
        normalizedTime = 0f;
        previewPlaying = false;

        if (previewAnimator == null)
        {
            ShowNotification(new GUIContent("씬의 Enemy Animator를 찾지 못했습니다."));
            return;
        }

        if (!BeginSceneColliderPreview())
        {
            ShowNotification(new GUIContent("Enemy/AttackHitBox의 BoxCollider를 찾지 못했습니다."));
            return;
        }

        SamplePreview();
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            sceneView = GetWindow<SceneView>("Synced Hitbox Preview");

        sceneView.drawGizmos = true;
        sceneView.FrameSelected(false, true);
        sceneView.Repaint();
        Focus();
        ShowNotification(new GUIContent("실제 BoxCollider와 프레임 동기화 완료"));
    }

    private void DrawPreviewControls()
    {
        using (new EditorGUI.DisabledScope(clip == null || previewAnimator == null))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(previewPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    previewPlaying = !previewPlaying;

                if (GUILayout.Button("< Frame", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    StepFrame(-1);
                if (GUILayout.Button("Frame >", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    StepFrame(1);

                loopPreview = GUILayout.Toggle(loopPreview, "Loop", EditorStyles.toolbarButton, GUILayout.Width(45f));
                GUILayout.FlexibleSpace();

                int frame = clip != null ? Mathf.RoundToInt(normalizedTime * clip.length * clip.frameRate) : 0;
                int totalFrames = clip != null ? Mathf.RoundToInt(clip.length * clip.frameRate) : 0;
                GUILayout.Label($"Frame {frame}/{totalFrames}   {CurrentSeconds:F3}s   {normalizedTime:F3}");
            }
        }

        EditorGUI.BeginChangeCheck();
        normalizedTime = EditorGUILayout.Slider("Playhead", normalizedTime, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            previewPlaying = false;
            SamplePreview();
        }
    }

    private void DrawEmbeddedPreview()
    {
        EditorGUILayout.Space(4f);
        float height = Mathf.Clamp(position.height * 0.36f, 220f, 380f);
        Rect previewRect = GUILayoutUtility.GetRect(
            1f,
            height,
            GUILayout.ExpandWidth(true));

        EditorGUI.DrawRect(previewRect, new Color(0.055f, 0.06f, 0.07f));
        if (previewAnimator == null || clip == null)
        {
            string message = previewAnimator == null
                ? "Preview Animator를 지정하거나 Find Scene Enemy를 누르세요."
                : "공격 데이터에 연결된 AnimationClip을 찾지 못했습니다.";
            GUI.Label(previewRect, message, CenteredPreviewMessageStyle());
            return;
        }

        EnsureEmbeddedPreview();
        HandleEmbeddedPreviewInput(previewRect);

        if (Event.current.type == EventType.Repaint)
            RenderEmbeddedPreview(previewRect);

        DrawEmbeddedPreviewOverlay(previewRect);
    }

    private void EnsureEmbeddedPreview()
    {
        int sourceId = previewAnimator != null ? previewAnimator.GetInstanceID() : 0;
        if (embeddedPreview != null &&
            embeddedPreviewObject != null &&
            embeddedPreviewSourceId == sourceId)
        {
            return;
        }

        ReleaseEmbeddedPreview();
        if (previewAnimator == null)
            return;

        embeddedPreview = new PreviewRenderUtility();
        embeddedPreview.cameraFieldOfView = 30f;
        embeddedPreview.camera.nearClipPlane = 0.01f;
        embeddedPreview.camera.farClipPlane = 1000f;
        embeddedPreview.camera.clearFlags = CameraClearFlags.SolidColor;
        embeddedPreview.camera.backgroundColor = new Color(0.055f, 0.06f, 0.07f, 1f);
        embeddedPreview.ambientColor = new Color(0.42f, 0.42f, 0.42f);
        embeddedPreview.lights[0].intensity = 1.25f;
        embeddedPreview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
        embeddedPreview.lights[1].intensity = 1.1f;
        embeddedPreview.lights[1].transform.rotation = Quaternion.Euler(340f, 215f, 0f);

        embeddedPreviewObject = Instantiate(previewAnimator.gameObject);
        embeddedPreviewObject.name = $"[Preview] {previewAnimator.gameObject.name}";
        embeddedPreviewObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        SetPreviewHideFlags(embeddedPreviewObject);
        DisablePreviewBehaviours(embeddedPreviewObject);
        embeddedPreview.AddSingleGO(embeddedPreviewObject);
        embeddedPreviewSourceId = sourceId;

        SampleEmbeddedPreview();
        Bounds bounds = CalculatePreviewBounds(embeddedPreviewObject);
        embeddedPreviewFocus = bounds.center;
        float largestExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float halfFieldOfView = embeddedPreview.cameraFieldOfView * Mathf.Deg2Rad * 0.5f;
        embeddedPreviewDistance = Mathf.Max(
            0.5f,
            largestExtent / Mathf.Max(0.1f, Mathf.Tan(halfFieldOfView)) * 1.2f);
    }

    private void RenderEmbeddedPreview(Rect previewRect)
    {
        if (embeddedPreview == null || embeddedPreviewObject == null)
            return;

        SampleEmbeddedPreview();
        Quaternion orbit = Quaternion.Euler(
            embeddedPreviewOrbit.y,
            embeddedPreviewOrbit.x,
            0f);
        float distance = embeddedPreviewDistance * embeddedPreviewZoom;
        Vector3 cameraPosition = embeddedPreviewFocus + orbit * (Vector3.back * distance);
        embeddedPreview.camera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(embeddedPreviewFocus - cameraPosition, Vector3.up));

        embeddedPreview.BeginPreview(previewRect, GUIStyle.none);
        embeddedPreview.camera.Render();
        Texture texture = embeddedPreview.EndPreview();
        GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
    }

    private void DrawEmbeddedPreviewOverlay(Rect previewRect)
    {
        Rect clipLabel = new Rect(previewRect.x + 10f, previewRect.y + 8f, previewRect.width - 20f, 22f);
        GUI.Label(
            clipLabel,
            $"{clip.name}   |   {CurrentSeconds:F3}s / {clip.length:F3}s   |   Frame {CurrentFrame}/{TotalFrames}",
            PreviewOverlayStyle(TextAnchor.MiddleLeft));

        string state = ResolvePreviewState(out Color stateColor);
        Rect stateRect = new Rect(previewRect.xMax - 120f, previewRect.y + 34f, 110f, 22f);
        EditorGUI.DrawRect(stateRect, new Color(stateColor.r, stateColor.g, stateColor.b, 0.88f));
        GUI.Label(stateRect, state, PreviewOverlayStyle(TextAnchor.MiddleCenter));

        Rect helpRect = new Rect(previewRect.x + 10f, previewRect.yMax - 34f, previewRect.width - 20f, 20f);
        GUI.Label(helpRect, "좌클릭 드래그: 회전  ·  휠: 확대/축소  ·  Scene View: 히트박스 동기 표시", PreviewOverlayStyle(TextAnchor.MiddleLeft));

        Rect progressRect = new Rect(previewRect.x, previewRect.yMax - 7f, previewRect.width, 7f);
        EditorGUI.DrawRect(progressRect, new Color(0f, 0f, 0f, 0.82f));
        EditorGUI.DrawRect(
            new Rect(progressRect.x, progressRect.y, progressRect.width * normalizedTime, progressRect.height),
            IsHitActive(normalizedTime) ? HitColor : new Color(0.86f, 0.86f, 0.86f));
        EditorGUI.DrawRect(
            new Rect(progressRect.x + progressRect.width * normalizedTime - 1f, progressRect.y - 2f, 2f, progressRect.height + 4f),
            Color.white);
    }

    private void HandleEmbeddedPreviewInput(Rect previewRect)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && previewRect.Contains(evt.mousePosition))
        {
            if (evt.button == 0)
            {
                draggingEmbeddedPreview = true;
                previewPlaying = false;
                evt.Use();
            }
            else if (evt.button == 1)
            {
                ResetEmbeddedPreviewCamera();
                evt.Use();
            }
        }

        if (draggingEmbeddedPreview && evt.type == EventType.MouseDrag)
        {
            embeddedPreviewOrbit.x += evt.delta.x * 0.55f;
            embeddedPreviewOrbit.y = Mathf.Clamp(
                embeddedPreviewOrbit.y - evt.delta.y * 0.45f,
                -20f,
                65f);
            Repaint();
            evt.Use();
        }

        if (draggingEmbeddedPreview && evt.rawType == EventType.MouseUp)
        {
            draggingEmbeddedPreview = false;
            evt.Use();
        }

        if (evt.type == EventType.ScrollWheel && previewRect.Contains(evt.mousePosition))
        {
            embeddedPreviewZoom = Mathf.Clamp(
                embeddedPreviewZoom * (1f + evt.delta.y * 0.06f),
                0.35f,
                2.5f);
            Repaint();
            evt.Use();
        }
    }

    private void ResetEmbeddedPreviewCamera()
    {
        embeddedPreviewOrbit = new Vector2(145f, 12f);
        embeddedPreviewZoom = 1f;
        Repaint();
    }

    private string ResolvePreviewState(out Color color)
    {
        if (IsTimeInWindows(GetWindows(TrackKind.Hit), normalizedTime))
        {
            color = HitColor;
            return "HIT ACTIVE";
        }

        if (IsTimeInWindows(GetWindows(TrackKind.Warning), normalizedTime))
        {
            color = WarningColor;
            return "WARNING";
        }

        if (IsTimeInWindows(GetWindows(TrackKind.Reaction), normalizedTime))
        {
            color = ReactionColor;
            return "PARRY / DODGE";
        }

        color = new Color(0.35f, 0.37f, 0.4f);
        return "RECOVERY";
    }

    private void HandleKeyboardShortcuts()
    {
        Event evt = Event.current;
        if (evt.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            return;

        if (evt.keyCode == KeyCode.Space)
        {
            previewPlaying = !previewPlaying && clip != null && previewAnimator != null;
            evt.Use();
            Repaint();
        }
        else if (evt.keyCode == KeyCode.LeftArrow)
        {
            StepFrame(-1);
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.RightArrow)
        {
            StepFrame(1);
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.Home)
        {
            SetPlayhead(0f);
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.End)
        {
            SetPlayhead(1f);
            evt.Use();
        }
    }

    private void DrawTimeline()
    {
        EditorGUILayout.Space(6f);
        Rect ruler = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        DrawRuler(ruler);

        DrawWindowTrack("Warning", TrackKind.Warning, WarningColor, GetWindows(TrackKind.Warning));
        DrawWindowTrack("Hit Active", TrackKind.Hit, HitColor, GetWindows(TrackKind.Hit));
        DrawWindowTrack("Parry / Dodge", TrackKind.Reaction, ReactionColor, GetWindows(TrackKind.Reaction));
        DrawWindowTrack("Target Tracking", TrackKind.Tracking, TrackingColor, GetWindows(TrackKind.Tracking));
        DrawMovementTrack();
        DrawRecoveryTrack();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "빈 트랙을 드래그하면 구간 추가 · 구간 내부를 드래그하면 이동 · 양 끝을 드래그하면 크기 조절 · 우클릭하면 삭제",
            MessageType.None);
    }

    private void DrawRuler(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            float x = Mathf.Lerp(rect.x, rect.xMax, t);
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawLine(new Vector3(x, rect.yMax - 8f), new Vector3(x, rect.yMax));
            GUI.Label(new Rect(x - 18f, rect.y + 2f, 36f, 18f), t.ToString("0.0"), EditorStyles.miniLabel);
        }

        HandlePlayheadScrubbing(rect);
        DrawPlayhead(rect);
    }

    private void DrawWindowTrack(string label, TrackKind kind, Color color, EnemyAttackWindow[] windows)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(105f));
            Rect rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

            if (windows != null)
            {
                for (int i = 0; i < windows.Length; i++)
                {
                    EnemyAttackWindow window = Sanitize(windows[i]);
                    Rect block = TimeRect(rect, window.start, window.end);
                    EditorGUI.DrawRect(block, color);
                    GUI.Label(block, $"{window.start:F3} - {window.end:F3}", CenteredMiniStyle());
                    HandleWindowInput(rect, block, kind, i, window);
                }
            }

            HandleEmptyTrackInput(rect, kind);
            if (creatingWindow && creatingTrack == kind)
            {
                Rect preview = TimeRect(rect, creatingWindowPreview.start, creatingWindowPreview.end);
                EditorGUI.DrawRect(preview, new Color(color.r, color.g, color.b, 0.45f));
            }
            DrawPlayhead(rect);
        }
    }

    private void DrawMovementTrack()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Movement", GUILayout.Width(105f));
            Rect rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

            if (!editFollowUp && attackData.useForwardMovement)
            {
                EnemyAttackWindow movement = Sanitize(new EnemyAttackWindow
                {
                    start = attackData.moveStart,
                    end = attackData.moveEnd
                });
                Rect block = TimeRect(rect, movement.start, movement.end);
                EditorGUI.DrawRect(block, MovementColor);
                GUI.Label(block, $"{movement.start:F3} - {movement.end:F3}", CenteredMiniStyle());
                HandleMovementInput(rect, block, movement);
            }
            else
            {
                GUI.Label(rect, editFollowUp ? "Follow-up movement는 현재 지원하지 않음" : "Movement disabled", CenteredMiniStyle());
            }

            DrawPlayhead(rect);
        }
    }

    private void DrawRecoveryTrack()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Recovery", GUILayout.Width(105f));
            Rect rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

            float start = 0f;
            EnemyAttackWindow[] hits = GetWindows(TrackKind.Hit);
            if (hits != null)
            {
                foreach (EnemyAttackWindow hit in hits)
                    start = Mathf.Max(start, Sanitize(hit).end);
            }

            Rect block = TimeRect(rect, start, 1f);
            EditorGUI.DrawRect(block, new Color(0.45f, 0.45f, 0.48f));
            string suffix = !editFollowUp && attackData.additionalRecoveryDelay > 0f
                ? $" + {attackData.additionalRecoveryDelay:F2}s delay"
                : string.Empty;
            GUI.Label(block, $"{start:F3} - 1.000{suffix}", CenteredMiniStyle());
            DrawPlayhead(rect);
        }
    }

    private void HandleWindowInput(
        Rect track,
        Rect block,
        TrackKind kind,
        int index,
        EnemyAttackWindow window)
    {
        Event evt = Event.current;
        Rect left = new Rect(block.x - 4f, block.y, 8f, block.height);
        Rect right = new Rect(block.xMax - 4f, block.y, 8f, block.height);

        if (evt.type == EventType.MouseDown && evt.button == 1 && block.Contains(evt.mousePosition))
        {
            RemoveWindow(kind, index);
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0 && block.Contains(evt.mousePosition))
        {
            draggingTrack = kind;
            draggingWindow = index;
            draggingEdge = left.Contains(evt.mousePosition) ? -1 : right.Contains(evt.mousePosition) ? 1 : 0;
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            evt.Use();
        }

        if (draggingWindow == index && draggingTrack == kind && evt.type == EventType.MouseDrag)
        {
            float delta = evt.delta.x / Mathf.Max(1f, track.width);
            if (draggingEdge < 0)
                window.start = Mathf.Clamp(window.start + delta, 0f, window.end - MinimumWindowSize);
            else if (draggingEdge > 0)
                window.end = Mathf.Clamp(window.end + delta, window.start + MinimumWindowSize, 1f);
            else
            {
                float length = window.end - window.start;
                window.start = Mathf.Clamp(window.start + delta, 0f, 1f - length);
                window.end = window.start + length;
            }

            SetWindow(kind, index, window);
            if (draggingEdge != 0)
                SetPlayhead(draggingEdge < 0 ? window.start : window.end);
            evt.Use();
        }

        if (draggingWindow == index && draggingTrack == kind && evt.rawType == EventType.MouseUp)
        {
            draggingWindow = -1;
            draggingEdge = 0;
            GUIUtility.hotControl = 0;
            SortWindows(kind);
            evt.Use();
        }
    }

    private void HandleEmptyTrackInput(Rect track, TrackKind kind)
    {
        Event evt = Event.current;
        if (draggingWindow >= 0 || draggingMovement || evt.button != 0)
            return;

        if (evt.type == EventType.MouseDown && track.Contains(evt.mousePosition))
        {
            creatingWindow = true;
            creatingTrack = kind;
            creatingWindowStart = MouseTime(track, evt.mousePosition.x);
            creatingWindowPreview = new EnemyAttackWindow
            {
                start = creatingWindowStart,
                end = Mathf.Min(1f, creatingWindowStart + MinimumDefaultWindowSize)
            };
            evt.Use();
            return;
        }

        if (creatingWindow && creatingTrack == kind && evt.type == EventType.MouseDrag)
        {
            float current = MouseTime(track, evt.mousePosition.x);
            float start = Mathf.Min(creatingWindowStart, current);
            float end = Mathf.Max(creatingWindowStart, current);
            if (end - start < MinimumWindowSize)
                end = Mathf.Min(1f, start + MinimumDefaultWindowSize);

            creatingWindowPreview = Sanitize(new EnemyAttackWindow { start = start, end = end });
            SetPlayhead(current);
            Repaint();
            evt.Use();
            return;
        }

        if (creatingWindow && creatingTrack == kind && evt.rawType == EventType.MouseUp)
        {
            AddWindow(kind, creatingWindowPreview);
            creatingWindow = false;
            evt.Use();
        }
    }

    private void HandleMovementInput(Rect track, Rect block, EnemyAttackWindow movement)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && block.Contains(evt.mousePosition))
        {
            draggingMovement = true;
            draggingEdge = evt.mousePosition.x < block.x + 7f ? -1 : evt.mousePosition.x > block.xMax - 7f ? 1 : 0;
            evt.Use();
        }

        if (draggingMovement && evt.type == EventType.MouseDrag)
        {
            float delta = evt.delta.x / Mathf.Max(1f, track.width);
            if (draggingEdge < 0)
                movement.start = Mathf.Clamp(movement.start + delta, 0f, movement.end - MinimumWindowSize);
            else if (draggingEdge > 0)
                movement.end = Mathf.Clamp(movement.end + delta, movement.start + MinimumWindowSize, 1f);
            else
            {
                float length = movement.end - movement.start;
                movement.start = Mathf.Clamp(movement.start + delta, 0f, 1f - length);
                movement.end = movement.start + length;
            }

            Undo.RecordObject(attackData, "Edit Attack Movement Window");
            attackData.moveStart = movement.start;
            attackData.moveEnd = movement.end;
            EditorUtility.SetDirty(attackData);
            if (draggingEdge != 0)
                SetPlayhead(draggingEdge < 0 ? movement.start : movement.end);
            evt.Use();
        }

        if (draggingMovement && evt.rawType == EventType.MouseUp)
        {
            draggingMovement = false;
            draggingEdge = 0;
            evt.Use();
        }
    }

    private void DrawHitDetectionSettings()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Hit Detection", EditorStyles.boldLabel);

        SerializedObject serialized = new SerializedObject(attackData);
        string prefix = editFollowUp && attackData.overrideFollowUpHitDetection ? "followUp" : string.Empty;

        if (editFollowUp)
        {
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("overrideFollowUpHitDetection"));
            serialized.ApplyModifiedProperties();
            prefix = attackData.overrideFollowUpHitDetection ? "followUp" : string.Empty;
        }

        serialized.Update();
        SerializedProperty mode = serialized.FindProperty(prefix + (prefix.Length > 0 ? "HitDetectionMode" : "hitDetectionMode"));
        SerializedProperty bone = serialized.FindProperty(prefix + (prefix.Length > 0 ? "WeaponSweepBoneName" : "weaponSweepBoneName"));
        SerializedProperty radius = serialized.FindProperty(prefix + (prefix.Length > 0 ? "WeaponSweepRadius" : "weaponSweepRadius"));
        SerializedProperty localStart = serialized.FindProperty(prefix + (prefix.Length > 0 ? "WeaponSweepLocalStart" : "weaponSweepLocalStart"));
        SerializedProperty localEnd = serialized.FindProperty(prefix + (prefix.Length > 0 ? "WeaponSweepLocalEnd" : "weaponSweepLocalEnd"));
        SerializedProperty pathSamples = serialized.FindProperty(prefix + (prefix.Length > 0 ? "WeaponSweepPathSamples" : "weaponSweepPathSamples"));

        EditorGUILayout.PropertyField(mode);
        if ((EnemyHitDetectionMode)mode.enumValueIndex == EnemyHitDetectionMode.WeaponSweep)
        {
            EditorGUILayout.PropertyField(bone);
            EditorGUILayout.PropertyField(radius);
            EditorGUILayout.PropertyField(localStart);
            EditorGUILayout.PropertyField(localEnd);
            EditorGUILayout.PropertyField(pathSamples);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Find Weapon Bone") && previewAnimator != null)
                {
                    Transform suggested = FindSuggestedWeaponBone(previewAnimator);
                    if (suggested != null)
                        bone.stringValue = suggested.name;
                }
                if (GUILayout.Button("Use Selected Transform") && Selection.activeTransform != null)
                    bone.stringValue = Selection.activeTransform.name;
                if (GUILayout.Button("Ping Bone") && previewAnimator != null)
                {
                    Transform found = FindBone(previewAnimator, bone.stringValue);
                    if (found != null)
                        EditorGUIUtility.PingObject(found);
                }
            }

            if (GUILayout.Button("Auto Fit From Weapon Renderer") && previewAnimator != null)
            {
                Transform targetBone = FindBone(previewAnimator, bone.stringValue);
                Renderer weaponRenderer = FindSuggestedWeaponRenderer(previewAnimator);
                if (targetBone != null && weaponRenderer != null)
                    FitSweepToRenderer(targetBone, weaponRenderer, localStart, localEnd, radius);
            }
        }

        serialized.ApplyModifiedProperties();
    }

    private void DrawValidation()
    {
        EditorGUILayout.Space(10f);
        List<string> issues = ValidateCurrentData();

        if (issues.Count == 0)
            EditorGUILayout.HelpBox("Timing data valid", MessageType.Info);
        else
            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Asset"))
            {
                EditorUtility.SetDirty(attackData);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Sort & Clamp Windows"))
                SanitizeAllWindows();
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (attackData == null || previewAnimator == null || clip == null)
            return;

        bool activeAtPlayhead = IsHitActive(normalizedTime);
        EnemyHitDetectionMode mode = ResolveHitDetectionMode();
        DrawScenePreviewStatus(activeAtPlayhead, mode);

        if (!activeAtPlayhead)
        {
            sceneView.Repaint();
            return;
        }

        Handles.color = HitColor;
        if (mode == EnemyHitDetectionMode.WeaponSweep)
        {
            Transform bone = FindBone(previewAnimator, ResolveSweepBoneName());
            if (bone != null)
            {
                Vector3 start = bone.TransformPoint(ResolveSweepStart());
                Vector3 end = bone.TransformPoint(ResolveSweepEnd());
                Handles.DrawAAPolyLine(7f, start, end);
                Handles.SphereHandleCap(0, start, Quaternion.identity, ResolveSweepRadius() * 2f, EventType.Repaint);
                Handles.SphereHandleCap(0, end, Quaternion.identity, ResolveSweepRadius() * 2f, EventType.Repaint);
                Handles.Label(
                    end + Vector3.up * ResolveSweepRadius(),
                    "WEAPON SWEEP ACTIVE",
                    SceneHitboxLabelStyle());
            }
        }

        sceneView.Repaint();
    }

    private bool BeginSceneColliderPreview()
    {
        EnemyController controller = previewAnimator != null
            ? previewAnimator.GetComponentInParent<EnemyController>()
            : null;
        BoxCollider collider = controller != null && controller.attackHitBox != null
            ? controller.attackHitBox.GetComponent<BoxCollider>()
            : null;
        if (collider == null)
            return false;

        splitCaptureCollider = collider;
        splitCaptureOriginalSelection = Selection.activeObject;
        splitCaptureOriginalGameObjectActive = collider.gameObject.activeSelf;
        splitCaptureOriginalEnabled = collider.enabled;
        splitCaptureOriginalCenter = collider.center;
        splitCaptureOriginalSize = collider.size;
        splitCaptureActive = true;
        collider.gameObject.SetActive(true);
        UpdateSceneColliderPreview();
        return true;
    }

    private void UpdateSceneColliderPreview()
    {
        if (!splitCaptureActive || splitCaptureCollider == null)
            return;

        if (!splitCaptureCollider.gameObject.activeSelf)
            splitCaptureCollider.gameObject.SetActive(true);

        ResolveSceneBodyBox(out Transform root, out Vector3 center, out Vector3 size);
        if (root != splitCaptureCollider.transform)
            return;

        splitCaptureCollider.center = center;
        splitCaptureCollider.size = size;
        bool shouldEnable =
            ResolveHitDetectionMode() == EnemyHitDetectionMode.BodyBox &&
            IsHitActive(normalizedTime);
        splitCaptureCollider.enabled = shouldEnable;

        UnityEngine.Object desiredSelection = shouldEnable
            ? splitCaptureCollider
            : previewAnimator != null
                ? previewAnimator.gameObject
                : splitCaptureCollider.gameObject;
        if (Selection.activeObject != desiredSelection)
            Selection.activeObject = desiredSelection;
    }

    private void EndSceneColliderPreview()
    {
        BoxCollider colliderToRestore = splitCaptureCollider;
        if (splitCaptureCollider != null)
        {
            splitCaptureCollider.enabled = splitCaptureOriginalEnabled;
            splitCaptureCollider.center = splitCaptureOriginalCenter;
            splitCaptureCollider.size = splitCaptureOriginalSize;
            splitCaptureCollider.gameObject.SetActive(splitCaptureOriginalGameObjectActive);
        }

        if (Selection.activeObject == colliderToRestore ||
            (previewAnimator != null && Selection.activeObject == previewAnimator.gameObject))
        {
            Selection.activeObject = splitCaptureOriginalSelection;
        }

        splitCaptureCollider = null;
        splitCaptureOriginalSelection = null;
        splitCaptureActive = false;
        SceneView.RepaintAll();
    }

    private void ResolveSceneBodyBox(out Transform root, out Vector3 center, out Vector3 size)
    {
        EnemyController controller = previewAnimator.GetComponentInParent<EnemyController>();
        BoxCollider sourceCollider = controller != null && controller.attackHitBox != null
            ? controller.attackHitBox.GetComponent<BoxCollider>()
            : null;
        root = sourceCollider != null
            ? sourceCollider.transform
            : previewAnimator.transform;

        if (editFollowUp && attackData.useFollowUpHitBoxShape)
        {
            center = attackData.followUpHitBoxCenter;
            size = attackData.followUpHitBoxSize;
        }
        else if (attackData.useCustomHitBoxShape)
        {
            center = attackData.hitBoxCenter;
            size = attackData.hitBoxSize;
        }
        else if (sourceCollider != null)
        {
            center = sourceCollider.center;
            size = sourceCollider.size;
        }
        else
        {
            center = attackData.hitBoxCenter;
            size = attackData.hitBoxSize;
        }

        size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(size.x)),
            Mathf.Max(0.01f, Mathf.Abs(size.y)),
            Mathf.Max(0.01f, Mathf.Abs(size.z)));
    }

    private void DrawScenePreviewStatus(bool active, EnemyHitDetectionMode mode)
    {
        Handles.BeginGUI();
        Rect panel = new Rect(12f, 12f, 310f, 58f);
        Color panelColor = active
            ? new Color(0.08f, 0.55f, 0.22f, 0.94f)
            : new Color(0.08f, 0.09f, 0.1f, 0.9f);
        EditorGUI.DrawRect(panel, panelColor);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            normal = { textColor = Color.white }
        };
        GUIStyle detailStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        GUI.Label(
            new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 24f),
            active ? "BOXCOLLIDER ENABLED" : "BOXCOLLIDER DISABLED",
            titleStyle);
        GUI.Label(
            new Rect(panel.x + 10f, panel.y + 30f, panel.width - 20f, 20f),
            $"{mode}  |  Frame {CurrentFrame}/{TotalFrames}  |  {normalizedTime:F3}",
            detailStyle);
        Handles.EndGUI();
    }

    private static GUIStyle SceneHitboxLabelStyle()
    {
        return new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(1f, 0.12f, 0.06f) }
        };
    }

    private void RebuildSweepTrailIfNeeded(Transform bone)
    {
        EnemyAttackWindow[] windows = GetWindows(TrackKind.Hit);
        int nextHash = ComputeSweepTrailHash(windows, bone);
        if (nextHash == sweepTrailHash)
            return;

        sweepTrailHash = nextHash;
        sweepTrailSegments.Clear();

        if (EditorApplication.isPlaying || windows == null || windows.Length == 0)
            return;

        AnimationMode.StartAnimationMode();
        foreach (EnemyAttackWindow rawWindow in windows)
        {
            EnemyAttackWindow window = Sanitize(rawWindow);
            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt((window.end - window.start) * clip.length * clip.frameRate) + 1,
                2,
                MaximumTrailSamplesPerWindow);
            Vector3[] startPoints = new Vector3[sampleCount];
            Vector3[] centerPoints = new Vector3[sampleCount];
            Vector3[] endPoints = new Vector3[sampleCount];

            AnimationMode.BeginSampling();
            try
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    float time = Mathf.Lerp(window.start, window.end, i / (sampleCount - 1f));
                    AnimationMode.SampleAnimationClip(previewAnimator.gameObject, clip, time * clip.length);
                    Vector3 start = bone.TransformPoint(ResolveSweepStart());
                    Vector3 end = bone.TransformPoint(ResolveSweepEnd());
                    startPoints[i] = start;
                    centerPoints[i] = Vector3.Lerp(start, end, 0.5f);
                    endPoints[i] = end;
                }
            }
            finally
            {
                AnimationMode.EndSampling();
            }

            sweepTrailSegments.Add(startPoints);
            sweepTrailSegments.Add(centerPoints);
            sweepTrailSegments.Add(endPoints);
        }

        SamplePreview();
    }

    private int ComputeSweepTrailHash(EnemyAttackWindow[] windows, Transform bone)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (attackData != null ? attackData.GetInstanceID() : 0);
            hash = hash * 31 + (previewAnimator != null ? previewAnimator.GetInstanceID() : 0);
            hash = hash * 31 + (clip != null ? clip.GetInstanceID() : 0);
            hash = hash * 31 + (bone != null ? bone.GetInstanceID() : 0);
            hash = hash * 31 + ResolveSweepStart().GetHashCode();
            hash = hash * 31 + ResolveSweepEnd().GetHashCode();
            hash = hash * 31 + ResolveSweepRadius().GetHashCode();
            if (windows != null)
            {
                foreach (EnemyAttackWindow window in windows)
                {
                    hash = hash * 31 + window.start.GetHashCode();
                    hash = hash * 31 + window.end.GetHashCode();
                }
            }
            return hash;
        }
    }

    private void InvalidateSweepTrail()
    {
        sweepTrailHash = 0;
        sweepTrailSegments.Clear();
    }

    private void EditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - lastEditorTime);
        lastEditorTime = now;

        if (!previewPlaying || clip == null)
            return;

        normalizedTime += deltaTime / Mathf.Max(0.001f, clip.length);
        if (normalizedTime >= 1f)
        {
            if (loopPreview)
                normalizedTime %= 1f;
            else
            {
                normalizedTime = 1f;
                previewPlaying = false;
            }
        }

        SamplePreview();
        Repaint();
    }

    private void SetAttackData(EnemyAttackData value)
    {
        StopPreview();
        InvalidateSweepTrail();
        attackData = value;
        editFollowUp = false;
        normalizedTime = 0f;
        FindSceneEnemyAnimator();
        ResolveClip();
        SamplePreview();
        Repaint();
    }

    private void FindSceneEnemyAnimator()
    {
        if (attackData == null)
            return;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null && enemy.EnemyData != null && enemy.EnemyData.attackPatterns != null && Array.IndexOf(enemy.EnemyData.attackPatterns, attackData) >= 0)
            {
                previewAnimator = enemy.animator != null
                    ? enemy.animator
                    : enemy.GetComponentInChildren<Animator>(true);
                return;
            }
        }
    }

    private void ResolveClipIfNeeded()
    {
        string expectedName = editFollowUp ? attackData.followUpAnim : attackData.attackAnim;
        if (clip == null || clip.name != expectedName)
            ResolveClip();
    }

    private void ResolveClip()
    {
        clip = null;
        ReleaseEmbeddedPreview();
        InvalidateSweepTrail();
        if (attackData == null)
            return;

        string clipName = editFollowUp ? attackData.followUpAnim : attackData.attackAnim;
        if (string.IsNullOrEmpty(clipName))
            return;

        if (previewAnimator != null && previewAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip controllerClip in previewAnimator.runtimeAnimatorController.animationClips)
            {
                if (controllerClip != null && controllerClip.name == clipName)
                {
                    clip = controllerClip;
                    return;
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets($"{clipName} t:AnimationClip");
        foreach (string guid in guids)
        {
            AnimationClip candidate = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (candidate != null && candidate.name == clipName)
            {
                clip = candidate;
                break;
            }
        }
    }

    private void SamplePreview()
    {
        if (clip == null || EditorApplication.isPlaying)
            return;

        if (previewAnimator != null)
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(previewAnimator.gameObject, clip, CurrentSeconds);
            AnimationMode.EndSampling();
        }

        SampleEmbeddedPreview();
        UpdateSceneColliderPreview();
        SceneView.RepaintAll();
        Repaint();
    }

    private void StopPreview()
    {
        previewPlaying = false;
        draggingEmbeddedPreview = false;
        scrubbingPlayhead = false;
        EndSceneColliderPreview();
        ReleaseEmbeddedPreview();
        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
    }

    private void StepFrame(int direction)
    {
        if (clip == null)
            return;

        float totalFrames = Mathf.Max(1f, clip.length * clip.frameRate);
        normalizedTime = Mathf.Clamp01((Mathf.Round(normalizedTime * totalFrames) + direction) / totalFrames);
        previewPlaying = false;
        SamplePreview();
    }

    private EnemyAttackWindow[] GetWindows(TrackKind kind)
    {
        if (editFollowUp)
        {
            switch (kind)
            {
                case TrackKind.Warning: return attackData.followUpWarningWindows;
                case TrackKind.Hit: return attackData.followUpActiveWindows;
                case TrackKind.Reaction: return attackData.followUpReactionWindows;
                case TrackKind.Tracking: return attackData.followUpTargetTrackingWindows;
                default: return Array.Empty<EnemyAttackWindow>();
            }
        }

        if (!attackData.useTimingWindows)
        {
            switch (kind)
            {
                case TrackKind.Warning:
                    return new[] { new EnemyAttackWindow { start = attackData.warningStart, end = attackData.warningEnd } };
                case TrackKind.Hit:
                    return new[] { new EnemyAttackWindow { start = attackData.startUpEnd, end = attackData.activeEnd } };
                case TrackKind.Reaction:
                    return new[] { new EnemyAttackWindow { start = attackData.reactionStart, end = attackData.reactionEnd } };
            }
        }

        switch (kind)
        {
            case TrackKind.Warning: return attackData.warningWindows;
            case TrackKind.Hit: return attackData.activeWindows;
            case TrackKind.Reaction: return attackData.reactionWindows;
            case TrackKind.Tracking: return attackData.targetTrackingWindows;
            default: return Array.Empty<EnemyAttackWindow>();
        }
    }

    private void SetWindows(TrackKind kind, EnemyAttackWindow[] windows)
    {
        Undo.RecordObject(attackData, "Edit Attack Timing");

        if (editFollowUp)
        {
            switch (kind)
            {
                case TrackKind.Warning: attackData.followUpWarningWindows = windows; break;
                case TrackKind.Hit: attackData.followUpActiveWindows = windows; break;
                case TrackKind.Reaction: attackData.followUpReactionWindows = windows; break;
                case TrackKind.Tracking: attackData.followUpTargetTrackingWindows = windows; break;
            }
        }
        else
        {
            EnsureMainTimingWindows();
            switch (kind)
            {
                case TrackKind.Warning: attackData.warningWindows = windows; break;
                case TrackKind.Hit: attackData.activeWindows = windows; break;
                case TrackKind.Reaction: attackData.reactionWindows = windows; break;
                case TrackKind.Tracking: attackData.targetTrackingWindows = windows; break;
            }
        }

        EditorUtility.SetDirty(attackData);
        InvalidateSweepTrail();
    }

    private void EnsureMainTimingWindows()
    {
        if (attackData.useTimingWindows)
            return;

        attackData.useTimingWindows = true;
        attackData.warningWindows = new[] { new EnemyAttackWindow { start = attackData.warningStart, end = attackData.warningEnd } };
        attackData.activeWindows = new[] { new EnemyAttackWindow { start = attackData.startUpEnd, end = attackData.activeEnd } };
        attackData.reactionWindows = new[] { new EnemyAttackWindow { start = attackData.reactionStart, end = attackData.reactionEnd } };
    }

    private void SetWindow(TrackKind kind, int index, EnemyAttackWindow value)
    {
        EnemyAttackWindow[] windows = GetWindows(kind);
        if (windows == null || index < 0 || index >= windows.Length)
            return;

        windows = (EnemyAttackWindow[])windows.Clone();
        windows[index] = Sanitize(value);
        SetWindows(kind, windows);
    }

    private void AddWindow(TrackKind kind, EnemyAttackWindow value)
    {
        EnemyAttackWindow[] source = GetWindows(kind) ?? Array.Empty<EnemyAttackWindow>();
        EnemyAttackWindow[] result = new EnemyAttackWindow[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[result.Length - 1] = Sanitize(value);
        Array.Sort(result, (a, b) => a.start.CompareTo(b.start));
        SetWindows(kind, result);
    }

    private void SortWindows(TrackKind kind)
    {
        EnemyAttackWindow[] windows = GetWindows(kind);
        if (windows == null || windows.Length < 2)
            return;

        windows = (EnemyAttackWindow[])windows.Clone();
        Array.Sort(windows, (a, b) => a.start.CompareTo(b.start));
        SetWindows(kind, windows);
    }

    private void RemoveWindow(TrackKind kind, int index)
    {
        EnemyAttackWindow[] source = GetWindows(kind);
        if (source == null || index < 0 || index >= source.Length)
            return;

        List<EnemyAttackWindow> result = new List<EnemyAttackWindow>(source);
        result.RemoveAt(index);
        SetWindows(kind, result.ToArray());
    }

    private void SanitizeAllWindows()
    {
        foreach (TrackKind kind in new[] { TrackKind.Warning, TrackKind.Hit, TrackKind.Reaction, TrackKind.Tracking })
        {
            EnemyAttackWindow[] windows = GetWindows(kind);
            if (windows == null)
                continue;

            windows = (EnemyAttackWindow[])windows.Clone();
            for (int i = 0; i < windows.Length; i++)
                windows[i] = Sanitize(windows[i]);
            Array.Sort(windows, (a, b) => a.start.CompareTo(b.start));
            SetWindows(kind, windows);
        }
    }

    private List<string> ValidateCurrentData()
    {
        List<string> issues = new List<string>();
        if (clip == null)
            issues.Add("AnimationClip을 찾지 못했습니다.");

        ValidateWindows("Warning", GetWindows(TrackKind.Warning), issues);
        ValidateWindows("Hit", GetWindows(TrackKind.Hit), issues);
        ValidateWindows("Reaction", GetWindows(TrackKind.Reaction), issues);

        if (ResolveHitDetectionMode() == EnemyHitDetectionMode.WeaponSweep)
        {
            if (string.IsNullOrWhiteSpace(ResolveSweepBoneName()))
                issues.Add("Weapon Sweep bone 이름이 비어 있습니다.");
            else if (previewAnimator != null && FindBone(previewAnimator, ResolveSweepBoneName()) == null)
                issues.Add($"Preview Animator에서 '{ResolveSweepBoneName()}' 본을 찾지 못했습니다.");

            if ((ResolveSweepEnd() - ResolveSweepStart()).sqrMagnitude < 0.0001f)
                issues.Add("Weapon Sweep 시작점과 끝점이 같습니다.");
        }

        return issues;
    }

    private static void ValidateWindows(string label, EnemyAttackWindow[] windows, List<string> issues)
    {
        if (windows == null)
            return;

        float previousEnd = -1f;
        foreach (EnemyAttackWindow raw in windows)
        {
            EnemyAttackWindow window = Sanitize(raw);
            if (raw.end <= raw.start)
                issues.Add($"{label}: 끝 시간이 시작보다 빠릅니다.");
            if (window.start < previousEnd)
                issues.Add($"{label}: 구간이 서로 겹칩니다.");
            previousEnd = Mathf.Max(previousEnd, window.end);
        }
    }

    private bool IsHitActive(float time)
    {
        EnemyAttackWindow[] windows = GetWindows(TrackKind.Hit);
        if (windows == null)
            return false;
        foreach (EnemyAttackWindow window in windows)
        {
            if (time >= window.start && time < window.end)
                return true;
        }
        return false;
    }

    private EnemyHitDetectionMode ResolveHitDetectionMode()
    {
        return editFollowUp && attackData.overrideFollowUpHitDetection
            ? attackData.followUpHitDetectionMode
            : attackData.hitDetectionMode;
    }

    private string ResolveSweepBoneName()
    {
        return editFollowUp && attackData.overrideFollowUpHitDetection
            ? attackData.followUpWeaponSweepBoneName
            : attackData.weaponSweepBoneName;
    }

    private float ResolveSweepRadius()
    {
        return Mathf.Max(0.01f, editFollowUp && attackData.overrideFollowUpHitDetection
            ? attackData.followUpWeaponSweepRadius
            : attackData.weaponSweepRadius);
    }

    private Vector3 ResolveSweepStart()
    {
        return editFollowUp && attackData.overrideFollowUpHitDetection
            ? attackData.followUpWeaponSweepLocalStart
            : attackData.weaponSweepLocalStart;
    }

    private Vector3 ResolveSweepEnd()
    {
        return editFollowUp && attackData.overrideFollowUpHitDetection
            ? attackData.followUpWeaponSweepLocalEnd
            : attackData.weaponSweepLocalEnd;
    }

    private void SetSweepEndpoints(Vector3 localStart, Vector3 localEnd)
    {
        Undo.RecordObject(attackData, "Edit Weapon Sweep Endpoints");
        if (editFollowUp && attackData.overrideFollowUpHitDetection)
        {
            attackData.followUpWeaponSweepLocalStart = localStart;
            attackData.followUpWeaponSweepLocalEnd = localEnd;
        }
        else
        {
            attackData.weaponSweepLocalStart = localStart;
            attackData.weaponSweepLocalEnd = localEnd;
        }

        EditorUtility.SetDirty(attackData);
        InvalidateSweepTrail();
    }

    private static Transform FindSuggestedWeaponBone(Animator animator)
    {
        if (animator == null)
            return null;

        Transform[] children = animator.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name.Equals("Weapon_1", StringComparison.OrdinalIgnoreCase))
                return child;
        }

        string[] keywords = { "weapon", "blade", "sword", "knife", "prop" };
        foreach (string keyword in keywords)
        {
            foreach (Transform child in children)
            {
                if (child.GetComponent<Renderer>() == null
                    && child.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            }
        }

        return null;
    }

    private static Renderer FindSuggestedWeaponRenderer(Animator animator)
    {
        if (animator == null)
            return null;

        string[] keywords = { "weapon", "blade", "sword", "knife" };
        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
        foreach (string keyword in keywords)
        {
            foreach (Renderer candidate in renderers)
            {
                if (candidate.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return candidate;
            }
        }

        return null;
    }

    private static void FitSweepToRenderer(
        Transform bone,
        Renderer weaponRenderer,
        SerializedProperty localStart,
        SerializedProperty localEnd,
        SerializedProperty radius)
    {
        Bounds bounds = weaponRenderer.bounds;
        Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 local = bone.InverseTransformPoint(corner);
                    localMin = Vector3.Min(localMin, local);
                    localMax = Vector3.Max(localMax, local);
                }
            }
        }

        Vector3 size = localMax - localMin;
        Vector3 center = (localMin + localMax) * 0.5f;
        int longestAxis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 start = center;
        Vector3 end = center;
        start[longestAxis] = localMin[longestAxis];
        end[longestAxis] = localMax[longestAxis];

        float crossA = size[(longestAxis + 1) % 3];
        float crossB = size[(longestAxis + 2) % 3];
        localStart.vector3Value = start;
        localEnd.vector3Value = end;
        radius.floatValue = Mathf.Max(0.05f, Mathf.Max(crossA, crossB) * 0.5f);
    }

    private static Transform FindBone(Animator animator, string boneName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(boneName))
            return null;

        foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == boneName)
                return child;
        }
        return null;
    }

    private void SampleEmbeddedPreview()
    {
        if (embeddedPreviewObject == null || clip == null || EditorApplication.isPlaying)
            return;

        clip.SampleAnimation(embeddedPreviewObject, CurrentSeconds);
    }

    private void ReleaseEmbeddedPreview()
    {
        if (embeddedPreview != null)
            embeddedPreview.Cleanup();

        embeddedPreview = null;
        embeddedPreviewObject = null;
        embeddedPreviewSourceId = 0;
    }

    private static void SetPreviewHideFlags(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.HideAndDontSave;
    }

    private static void DisablePreviewBehaviours(GameObject root)
    {
        foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            behaviour.enabled = false;

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            renderer.updateWhenOffscreen = true;
    }

    private static Bounds CalculatePreviewBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = new Bounds(Vector3.up, Vector3.one * 2f);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer)
            {
                continue;
            }

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

        return bounds;
    }

    private void HandlePlayheadScrubbing(Rect ruler)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && ruler.Contains(evt.mousePosition))
        {
            scrubbingPlayhead = true;
            SetPlayhead(MouseTime(ruler, evt.mousePosition.x));
            evt.Use();
        }

        if (scrubbingPlayhead && evt.type == EventType.MouseDrag)
        {
            SetPlayhead(MouseTime(ruler, evt.mousePosition.x));
            evt.Use();
        }

        if (scrubbingPlayhead && evt.rawType == EventType.MouseUp)
        {
            scrubbingPlayhead = false;
            evt.Use();
        }
    }

    private void SetPlayhead(float time)
    {
        normalizedTime = Mathf.Clamp01(time);
        previewPlaying = false;
        SamplePreview();
    }

    private static bool IsTimeInWindows(EnemyAttackWindow[] windows, float time)
    {
        if (windows == null)
            return false;

        foreach (EnemyAttackWindow raw in windows)
        {
            EnemyAttackWindow window = Sanitize(raw);
            if (time >= window.start && time < window.end)
                return true;
        }

        return false;
    }

    private static GUIStyle PreviewOverlayStyle(TextAnchor alignment)
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = alignment,
            clipping = TextClipping.Clip,
            padding = new RectOffset(5, 5, 0, 0)
        };
        style.normal.textColor = Color.white;
        return style;
    }

    private static GUIStyle CenteredPreviewMessageStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            wordWrap = true
        };
        return style;
    }
    private static EnemyAttackWindow Sanitize(EnemyAttackWindow window)
    {
        float low = Mathf.Clamp01(Mathf.Min(window.start, window.end));
        float high = Mathf.Clamp01(Mathf.Max(window.start, window.end));
        window.start = Mathf.Min(low, 1f - MinimumWindowSize);
        window.end = Mathf.Max(window.start + MinimumWindowSize, high);
        return window;
    }

    private static Rect TimeRect(Rect track, float start, float end)
    {
        float x = Mathf.Lerp(track.x, track.xMax, Mathf.Clamp01(start));
        float xMax = Mathf.Lerp(track.x, track.xMax, Mathf.Clamp01(end));
        return new Rect(x, track.y + 3f, Mathf.Max(2f, xMax - x), track.height - 6f);
    }

    private static float MouseTime(Rect track, float mouseX)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(track.x, track.xMax, mouseX));
    }

    private void DrawPlayhead(Rect rect)
    {
        float x = Mathf.Lerp(rect.x, rect.xMax, normalizedTime);
        EditorGUI.DrawRect(new Rect(x - 1f, rect.y, 2f, rect.height), Color.white);
    }

    private static GUIStyle CenteredMiniStyle()
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            clipping = TextClipping.Clip
        };
        return style;
    }

    private float CurrentSeconds => clip != null ? normalizedTime * clip.length : 0f;
    private int CurrentFrame => clip != null ? Mathf.RoundToInt(CurrentSeconds * clip.frameRate) : 0;
    private int TotalFrames => clip != null ? Mathf.RoundToInt(clip.length * clip.frameRate) : 0;
    private const float MinimumWindowSize = 0.002f;
    private const float MinimumDefaultWindowSize = 0.02f;
    private const int MaximumTrailSamplesPerWindow = 64;
}
#endif
