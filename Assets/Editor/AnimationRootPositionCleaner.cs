using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimationRootPositionCleaner
{
    private const string MenuPath = "Tools/Animation/Remove Corin and Butcher Root Position";
    private const string RootBoneName = "Bip001";
    // 일부 추출 클립은 Bip001 경로를 CRC32 자리표시자로 저장한다.
    private const string HashedRootBonePath = "path_2797366333";
    private const string CorinPrefabPath = "Assets/Prefabs/Players/Corin.prefab";
    private const string CorinClipFolder =
        "Assets/ImportedCharacters/Corin/AnimationClip_Selected";
    private const string ButcherModelPath =
        "Assets/ThirdParty/ZZZ_DeadEndButcher/Models/Monster_NotoriousDeadEndButcher_P1.fbx";
    private const string ButcherClipFolder =
        "Assets/Animation/DeadEndButcher_InPlace";
    private const string ButcherControllerPath =
        "Assets/Animation/DeadEndButcher.controller";

    [MenuItem(MenuPath)]
    public static void CleanNow()
    {
        try
        {
            CleanupResult result = CleanAll();
            Debug.Log(result.ToLogMessage());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCleanNow()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;
    }

    private static CleanupResult CleanAll()
    {
        CleanupResult result = new CleanupResult();
        RepairCorinHashedBindings(result);
        CleanCorinClips(result);
        Dictionary<AnimationClip, AnimationClip> butcherClipMap =
            BuildButcherInPlaceClips(result);
        ReplaceControllerMotions(butcherClipMap, result);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateResult(result);
        return result;
    }

    private static void RepairCorinHashedBindings(CleanupResult result)
    {
        string[] clipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] { CorinClipFolder });
        AnimationClip[] clips = clipGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
            .Where(clip => clip != null)
            .ToArray();

        Dictionary<uint, string> knownPaths = new Dictionary<uint, string>();
        foreach (AnimationClip clip in clips)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AddKnownPath(knownPaths, binding.path);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AddKnownPath(knownPaths, binding.path);
        }

        GameObject corinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorinPrefabPath);
        if (corinPrefab != null)
        {
            foreach (Animator animator in corinPrefab.GetComponentsInChildren<Animator>(true))
            {
                foreach (Transform transform in animator.GetComponentsInChildren<Transform>(true))
                {
                    AddKnownPath(
                        knownPaths,
                        AnimationUtility.CalculateTransformPath(transform, animator.transform));
                }
            }
        }

        foreach (AnimationClip clip in clips)
        {
            if (clip.name.IndexOf("SwitchIn_Attack_Ex", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            result.CorinUltimateClipCount++;
            result.RepairedCorinBindings += RepairHashedBindings(clip, knownPaths);
            result.UnresolvedCorinBindings += CountUnresolvedHashedBindings(clip);
        }
    }

    private static int RepairHashedBindings(
        AnimationClip clip,
        IReadOnlyDictionary<uint, string> knownPaths)
    {
        List<EditorCurveBinding> addedBindings = new List<EditorCurveBinding>();
        List<AnimationCurve> addedCurves = new List<AnimationCurve>();
        List<EditorCurveBinding> removedBindings = new List<EditorCurveBinding>();

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (!TryResolveHashedPath(binding.path, knownPaths, out string resolvedPath))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                continue;

            EditorCurveBinding resolvedBinding = binding;
            resolvedBinding.path = resolvedPath;
            addedBindings.Add(resolvedBinding);
            addedCurves.Add(curve);
            removedBindings.Add(binding);
        }

        if (addedBindings.Count > 0)
        {
            AnimationUtility.SetEditorCurves(
                clip,
                addedBindings.ToArray(),
                addedCurves.ToArray());
            AnimationUtility.SetEditorCurves(
                clip,
                removedBindings.ToArray(),
                new AnimationCurve[removedBindings.Count]);
        }

        int objectBindingCount = 0;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (!TryResolveHashedPath(binding.path, knownPaths, out string resolvedPath))
                continue;

            ObjectReferenceKeyframe[] keyframes =
                AnimationUtility.GetObjectReferenceCurve(clip, binding);
            EditorCurveBinding resolvedBinding = binding;
            resolvedBinding.path = resolvedPath;
            AnimationUtility.SetObjectReferenceCurve(clip, resolvedBinding, keyframes);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            objectBindingCount++;
        }

        int repairedCount = addedBindings.Count + objectBindingCount;
        if (repairedCount > 0)
            EditorUtility.SetDirty(clip);

        return repairedCount;
    }

    private static int CountUnresolvedHashedBindings(AnimationClip clip)
    {
        int floatBindings = AnimationUtility.GetCurveBindings(clip)
            .Count(binding => TryParseHashedPath(binding.path, out _));
        int objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Count(binding => TryParseHashedPath(binding.path, out _));
        return floatBindings + objectBindings;
    }

    private static void AddKnownPath(IDictionary<uint, string> knownPaths, string path)
    {
        if (string.IsNullOrEmpty(path) || TryParseHashedPath(path, out _))
            return;

        uint hash = ComputeCrc32(path);
        if (!knownPaths.ContainsKey(hash))
            knownPaths.Add(hash, path);
    }

    private static bool TryResolveHashedPath(
        string path,
        IReadOnlyDictionary<uint, string> knownPaths,
        out string resolvedPath)
    {
        resolvedPath = null;
        return TryParseHashedPath(path, out uint hash)
            && knownPaths.TryGetValue(hash, out resolvedPath);
    }

    private static bool TryParseHashedPath(string path, out uint hash)
    {
        const string prefix = "path_";
        hash = 0;
        return path != null
            && path.StartsWith(prefix, StringComparison.Ordinal)
            && uint.TryParse(path.Substring(prefix.Length), out hash);
    }

    private static uint ComputeCrc32(string value)
    {
        uint crc = uint.MaxValue;
        foreach (byte character in Encoding.UTF8.GetBytes(value))
        {
            crc ^= character;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }

        return ~crc;
    }
    private static void CleanCorinClips(CleanupResult result)
    {
        string[] clipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] { CorinClipFolder });

        foreach (string guid in clipGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                continue;

            result.CorinClipCount++;
            result.RemovedCorinCurves += RemoveRootPositionCurves(clip);
        }
    }

    private static Dictionary<AnimationClip, AnimationClip> BuildButcherInPlaceClips(
        CleanupResult result)
    {
        EnsureFolder(ButcherClipFolder);

        AnimationClip[] sourceClips = AssetDatabase.LoadAllAssetsAtPath(ButcherModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .GroupBy(clip => clip.name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToArray();

        if (sourceClips.Length == 0)
            throw new InvalidOperationException($"도살자 FBX에서 애니메이션을 찾지 못했습니다: {ButcherModelPath}");

        Dictionary<AnimationClip, AnimationClip> clipMap =
            new Dictionary<AnimationClip, AnimationClip>();

        foreach (AnimationClip sourceClip in sourceClips)
        {
            string fileName = MakeSafeFileName(sourceClip.name) + ".anim";
            string targetPath = ButcherClipFolder + "/" + fileName;
            AnimationClip targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);

            if (targetClip == null)
            {
                targetClip = new AnimationClip();
                EditorUtility.CopySerialized(sourceClip, targetClip);
                targetClip.name = sourceClip.name;
                targetClip.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(targetClip, targetPath);
                result.CreatedButcherClips++;
            }

            result.ButcherClipCount++;
            result.RemovedButcherCurves += RemoveRootPositionCurves(targetClip);
            clipMap.Add(sourceClip, targetClip);
        }

        return clipMap;
    }

    private static int RemoveRootPositionCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        int removedCount = 0;

        foreach (EditorCurveBinding binding in bindings)
        {
            if (!IsRootPositionBinding(binding))
                continue;

            AnimationUtility.SetEditorCurve(clip, binding, null);
            removedCount++;
        }

        if (removedCount > 0)
            EditorUtility.SetDirty(clip);

        return removedCount;
    }

    private static bool IsRootPositionBinding(EditorCurveBinding binding)
    {
        bool isRootPath = string.Equals(
                binding.path,
                RootBoneName,
                StringComparison.Ordinal)
            || string.Equals(
                binding.path,
                HashedRootBonePath,
                StringComparison.Ordinal)
            || binding.path.EndsWith("/" + RootBoneName, StringComparison.Ordinal);

        if (!isRootPath)
            return false;

        return string.Equals(binding.propertyName, "m_LocalPosition.x", StringComparison.Ordinal)
            || string.Equals(binding.propertyName, "m_LocalPosition.y", StringComparison.Ordinal)
            || string.Equals(binding.propertyName, "m_LocalPosition.z", StringComparison.Ordinal);
    }

    private static void ReplaceControllerMotions(
        IReadOnlyDictionary<AnimationClip, AnimationClip> clipMap,
        CleanupResult result)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ButcherControllerPath);
        if (controller == null)
            throw new InvalidOperationException($"도살자 Animator Controller가 없습니다: {ButcherControllerPath}");

        Dictionary<string, AnimationClip> replacementsByName = clipMap.Values
            .ToDictionary(clip => clip.name, StringComparer.Ordinal);

        foreach (AnimatorControllerLayer layer in controller.layers)
            ReplaceStateMachineMotions(layer.stateMachine, replacementsByName, result);

        EditorUtility.SetDirty(controller);
    }

    private static void ReplaceStateMachineMotions(
        AnimatorStateMachine stateMachine,
        IReadOnlyDictionary<string, AnimationClip> replacementsByName,
        CleanupResult result)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            Motion replacement = ReplaceMotion(
                childState.state.motion,
                replacementsByName,
                result);
            if (replacement != childState.state.motion)
            {
                childState.state.motion = replacement;
                EditorUtility.SetDirty(childState.state);
            }
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            ReplaceStateMachineMotions(childMachine.stateMachine, replacementsByName, result);
    }

    private static Motion ReplaceMotion(
        Motion motion,
        IReadOnlyDictionary<string, AnimationClip> replacementsByName,
        CleanupResult result)
    {
        if (motion is AnimationClip clip)
        {
            string clipPath = AssetDatabase.GetAssetPath(clip);
            if (string.Equals(clipPath, ButcherModelPath, StringComparison.OrdinalIgnoreCase)
                && replacementsByName.TryGetValue(clip.name, out AnimationClip replacement))
            {
                result.ReplacedControllerMotions++;
                return replacement;
            }

            return motion;
        }

        if (motion is not BlendTree blendTree)
            return motion;

        ChildMotion[] children = blendTree.children;
        bool changed = false;
        for (int i = 0; i < children.Length; i++)
        {
            Motion replacement = ReplaceMotion(
                children[i].motion,
                replacementsByName,
                result);
            if (replacement == children[i].motion)
                continue;

            children[i].motion = replacement;
            changed = true;
        }

        if (changed)
        {
            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);
        }

        return blendTree;
    }

    private static void ValidateResult(CleanupResult result)
    {
        List<string> failures = new List<string>();

        string[] corinClipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] { CorinClipFolder });
        foreach (string guid in corinClipGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && CountRootPositionBindings(clip) > 0)
                failures.Add(path);
        }

        string[] butcherClipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] { ButcherClipFolder });
        foreach (string guid in butcherClipGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null && CountRootPositionBindings(clip) > 0)
                failures.Add(path);
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ButcherControllerPath);
        if (controller != null)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
                FindRawButcherMotions(layer.stateMachine, failures);
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "루트 위치 제거 검증에 실패했습니다:\n" + string.Join("\n", failures.Distinct()));
        }

        result.ValidationPassed = true;
    }

    private static int CountRootPositionBindings(AnimationClip clip)
    {
        return AnimationUtility.GetCurveBindings(clip).Count(IsRootPositionBinding);
    }

    private static void FindRawButcherMotions(
        AnimatorStateMachine stateMachine,
        ICollection<string> failures)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
            FindRawButcherMotion(childState.state.motion, childState.state.name, failures);

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            FindRawButcherMotions(childMachine.stateMachine, failures);
    }

    private static void FindRawButcherMotion(
        Motion motion,
        string stateName,
        ICollection<string> failures)
    {
        if (motion is AnimationClip clip)
        {
            if (string.Equals(
                AssetDatabase.GetAssetPath(clip),
                ButcherModelPath,
                StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Animator state '{stateName}' -> raw FBX clip '{clip.name}'");
            }

            return;
        }

        if (motion is not BlendTree blendTree)
            return;

        foreach (ChildMotion child in blendTree.children)
            FindRawButcherMotion(child.motion, stateName, failures);
    }

    private static string MakeSafeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        return new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    private sealed class CleanupResult
    {
        internal int CorinClipCount;
        internal int CorinUltimateClipCount;
        internal int RepairedCorinBindings;
        internal int UnresolvedCorinBindings;
        internal int ButcherClipCount;
        internal int CreatedButcherClips;
        internal int RemovedCorinCurves;
        internal int RemovedButcherCurves;
        internal int ReplacedControllerMotions;
        internal bool ValidationPassed;

        internal string ToLogMessage()
        {
            return
                "코린/도살자 애니메이션 루트 위치 제거 완료.\n" +
                $"코린: {CorinClipCount}개 클립, {RemovedCorinCurves}개 위치 곡선 제거\n" +
                $"코린 궁극기: {CorinUltimateClipCount}개 클립, {RepairedCorinBindings}개 본 바인딩 복구, " +
                $"미해결 {UnresolvedCorinBindings}개\n" +
                $"도살자: {ButcherClipCount}개 클립, {CreatedButcherClips}개 편집용 클립 생성, " +
                $"{RemovedButcherCurves}개 위치 곡선 제거\n" +
                $"Animator 교체: {ReplacedControllerMotions}개\n" +
                $"검증: {(ValidationPassed ? "통과" : "실패")}";
        }
    }
}
