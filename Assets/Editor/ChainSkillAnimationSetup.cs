using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ChainSkillAnimationSetup
{
    private const string MenuPath = "Tools/Animation/콤보 스킬 애니메이션 연결";
    private const string RootBoneName = "Bip001";
    private const string HashedRootBonePath = "path_2797366333";

    private const string CorinDataPath =
        "Assets/Characters/Corin/Data/Ultimate_Corin.asset";
    private const string EllenDataPath =
        "Assets/Characters/Ellen_Joe/Data/Ultimate_Ellen_Joe.asset";
    private const string JaneDataPath =
        "Assets/Characters/Jane_Doe/Data/Ultimate_Jane_Doe.asset";

    private const string CorinClipFolder =
        "Assets/ImportedCharacters/Corin/AnimationClip_Selected";
    private const string EllenClipFolder =
        "Assets/ImportedCharacters/Ellen/AnimationClip_Selected";
    private const string JaneClipFolder =
        "Assets/ImportedCharacters/Jane_Doe/AnimationClip_Selected";

    private static readonly string[] ChainClipPaths =
    {
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_End.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_02.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_02_End.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_02_Explode.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_02_Explode_End.anim",
        CorinClipFolder + "/Avatar_Female_Size01_Corin_Ani_SwitchIn_Attack_02_Landed.anim",
        EllenClipFolder + "/Avatar_Female_Size02_EllenOnCampus_Ani_SwitchIn_Attack.anim",
        EllenClipFolder + "/Avatar_Female_Size02_EllenOnCampus_Ani_SwitchIn_Attack_End.anim",
        JaneClipFolder + "/Avatar_Female_Size03_JaneDoe_Ani_SwitchIn_Attack.anim",
        JaneClipFolder + "/Avatar_Female_Size03_JaneDoe_Ani_SwitchIn_Attack_End.anim"
    };

    static ChainSkillAnimationSetup()
    {
        EditorApplication.update -= TryApplyAutomatically;
        EditorApplication.update += TryApplyAutomatically;
    }

    [MenuItem(MenuPath)]
    public static void ApplyFromMenu()
    {
        ApplyDefaults(true);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;
    }

    private static void TryApplyAutomatically()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= TryApplyAutomatically;
        if (!AreAllClipsImported())
            return;

        ApplyDefaults(false);
    }

    private static bool AreAllClipsImported()
    {
        for (int i = 0; i < ChainClipPaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ChainClipPaths[i]) == null)
                return false;
        }

        return true;
    }

    private static void ApplyDefaults(bool overwriteReferences)
    {
        try
        {
            int removedRootCurves = 0;
            for (int i = 0; i < ChainClipPaths.Length; i++)
            {
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(ChainClipPaths[i]);
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        $"콤보 스킬 애니메이션을 찾지 못했습니다: {ChainClipPaths[i]}");
                }

                removedRootCurves += RemoveRootPositionCurves(clip);
            }

            int connectedCharacters = 0;
            connectedCharacters += ConfigureCharacter(
                CorinDataPath,
                ChainClipPaths[0],
                ChainClipPaths[1],
                overwriteReferences);
            connectedCharacters += ConfigureCharacter(
                EllenDataPath,
                ChainClipPaths[7],
                ChainClipPaths[8],
                overwriteReferences);
            connectedCharacters += ConfigureCharacter(
                JaneDataPath,
                ChainClipPaths[9],
                ChainClipPaths[10],
                overwriteReferences);

            if (removedRootCurves == 0 && connectedCharacters == 0)
                return;

            AssetDatabase.SaveAssets();
            Debug.Log(
                "콤보 스킬 애니메이션 연결 완료.\n" +
                $"캐릭터 데이터 연결: {connectedCharacters}개\n" +
                $"루트 위치 곡선 제거: {removedRootCurves}개");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static int ConfigureCharacter(
        string dataPath,
        string attackClipPath,
        string endClipPath,
        bool overwriteReferences)
    {
        UltimateData data = AssetDatabase.LoadAssetAtPath<UltimateData>(dataPath);
        AnimationClip attackClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(attackClipPath);
        AnimationClip endClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(endClipPath);

        if (data == null)
            throw new InvalidOperationException($"궁극기 데이터가 없습니다: {dataPath}");
        if (attackClip == null || endClip == null)
            throw new InvalidOperationException($"콤보 스킬 클립 연결에 실패했습니다: {dataPath}");

        bool changed = false;
        if (overwriteReferences || data.chainSkillAnim == null)
        {
            if (data.chainSkillAnim != attackClip)
            {
                data.chainSkillAnim = attackClip;
                changed = true;
            }
        }

        if (overwriteReferences || data.chainSkillEndAnim == null)
        {
            if (data.chainSkillEndAnim != endClip)
            {
                data.chainSkillEndAnim = endClip;
                changed = true;
            }
        }

        if (!changed)
            return 0;

        EditorUtility.SetDirty(data);
        return 1;
    }

    private static int RemoveRootPositionCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        int removedCount = 0;

        for (int i = 0; i < bindings.Length; i++)
        {
            if (!IsRootPositionBinding(bindings[i]))
                continue;

            AnimationUtility.SetEditorCurve(clip, bindings[i], null);
            removedCount++;
        }

        if (removedCount > 0)
            EditorUtility.SetDirty(clip);

        return removedCount;
    }

    private static bool IsRootPositionBinding(EditorCurveBinding binding)
    {
        bool isRootPath =
            string.Equals(binding.path, RootBoneName, StringComparison.Ordinal)
            || string.Equals(binding.path, HashedRootBonePath, StringComparison.Ordinal)
            || binding.path.EndsWith("/" + RootBoneName, StringComparison.Ordinal);

        if (!isRootPath)
            return false;

        return string.Equals(
                binding.propertyName,
                "m_LocalPosition.x",
                StringComparison.Ordinal)
            || string.Equals(
                binding.propertyName,
                "m_LocalPosition.y",
                StringComparison.Ordinal)
            || string.Equals(
                binding.propertyName,
                "m_LocalPosition.z",
                StringComparison.Ordinal);
    }
}
