using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public class CharacterBuildValidationResult
{
    public List<string> errors = new List<string>();
    public List<string> infos = new List<string>();
     
    public bool HasError => errors.Count > 0;
}

public class CharacterBuildPipeline : MonoBehaviour
{
    public static CharacterBuildValidationResult ValidatePreset(CharacterBuildPreset preset, bool logToConsole = true)
    {
        CharacterBuildValidationResult result = new CharacterBuildValidationResult();

        void AddError(string message)
        {
            result.errors.Add(message);
            if (logToConsole)
                Debug.LogError(message);
        }

        void AddInfo(string message)
        {
            result.infos.Add(message);
            if (logToConsole)
                Debug.Log(message);
        }

        void AddErrorIfEmpty(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                AddError($"{label} is missing.");
        }

        void ValidateAttackData(AttackData attack, string label)
        {
            if (attack == null)
            {
                AddError($"{label} is missing.");
                return;
            }

            AddErrorIfEmpty(attack.attackAnim, $"{label}.attackAnim");
            AddErrorIfEmpty(attack.endAnim, $"{label}.endAnim");
        }

        void ValidateSkillData(SkillData skill, string label)
        {
            if (skill == null)
            {
                AddError($"{label} is missing.");
                return;
            }

            AddErrorIfEmpty(skill.skillAnim, $"{label}.skillAnim");
            AddErrorIfEmpty(skill.endAnim, $"{label}.endAnim");

            if (skill.hitBoxSlotIndex < 0)
                AddError($"{label}.hitBoxSlotIndex is invalid.");
        }

        void ValidateUltimateData(UltimateData ult, string label)
        {
            if (ult == null)
            {
                AddError($"{label} is missing.");
                return;
            }

            AddErrorIfEmpty(ult.ultStartAnim, $"{label}.ultStartAnim");
            AddErrorIfEmpty(ult.ultHitAnim, $"{label}.ultHitAnim");
            AddErrorIfEmpty(ult.ultEndAnim, $"{label}.ultEndAnim");
        }

        if (preset.basePrefab == null)
            AddError("Base Prefab is missing.");

        if (preset.modelPrefab == null)
            AddError("Model Prefab is missing.");

        if (preset.characterData == null)
            AddError("CharacterData is missing.");

        if (preset.animatorController == null)
            AddError("Animator Controller is missing.");

        if (preset.basePrefab == null)
            return result;

        PlayerController player = preset.basePrefab.GetComponent<PlayerController>();
        Animator animator = preset.basePrefab.GetComponent<Animator>();
        CharacterController controller = preset.basePrefab.GetComponent<CharacterController>();

        if (player == null)
            AddError("Base Prefab is missing PlayerController.");

        if (animator == null)
            AddError("Base Prefab is missing Animator.");

        if (controller == null)
            AddError("Base Prefab is missing CharacterController.");

        if (player == null)
            return result;

        if (player.CameraFollowTarget == null)
            AddError("PlayerController is missing CameraFollowTarget.");

        if (player.UltHitBox == null)
            AddError("PlayerController is missing UltHitBox.");

        for (int i = 0; i < player.SkillHitBoxSlotCount; i++)
        {
            if (player.GetSkillHitBox(i) == null)
                AddError($"Skill HitBox Slot {i} is missing.");
        }

        CharacterData data = preset.characterData;

        if (data != null)
        {
            AddErrorIfEmpty(data.idleAnim, "CharacterData.idleAnim");
            AddErrorIfEmpty(data.walkStartAnim, "CharacterData.walkStartAnim");
            AddErrorIfEmpty(data.walkLoopAnim, "CharacterData.walkLoopAnim");
            AddErrorIfEmpty(data.walkEndAnim, "CharacterData.walkEndAnim");
            AddErrorIfEmpty(data.runLoopAnim, "CharacterData.runLoopAnim");
            AddErrorIfEmpty(data.runEndAnim, "CharacterData.runEndAnim");

            AddErrorIfEmpty(data.hitLightFrontAnim, "CharacterData.hitLightFrontAnim");
            AddErrorIfEmpty(data.hitHeavyFrontAnim, "CharacterData.hitHeavyFrontAnim");
            AddErrorIfEmpty(data.dodgeFrontAnim, "CharacterData.dodgeFrontAnim");
            AddErrorIfEmpty(data.dodgeCounterStartAnim, "CharacterData.dodgeCounterStartAnim");
            AddErrorIfEmpty(data.dodgeCounterEndAnim, "CharacterData.dodgeCounterEndAnim");

            AddErrorIfEmpty(data.parrySupportStartAnim, "CharacterData.parrySupportStartAnim");
            AddErrorIfEmpty(data.parrySupportLightAnim, "CharacterData.parrySupportLightAnim");
            AddErrorIfEmpty(data.parrySupportHeavyAnim, "CharacterData.parrySupportHeavyAnim");

            if (data.normalCombo != null && data.normalCombo.Length != preset.normalComboCount)
                AddError($"CharacterData.normalCombo count mismatch. Expected {preset.normalComboCount}, got {data.normalCombo.Length}.");
            else
            {
                for (int i = 0; i < data.normalCombo.Length; i++)
                {
                    ValidateAttackData(data.normalCombo[i], $"CharacterData.normalCombo[{i}]");
                }
            }

            if (preset.createNormalSkillBranch)
            {
                if (data.normalSkillBranch == null)
                    AddError("CharacterData.normalSkillBranch is missing.");
                else
                    ValidateSkillData(data.normalSkillBranch, "CharacterData.normalSkillBranch");
            }

            if (preset.createEnhancedSkillBranch)
            {
                if (data.enhancedSkillBranch == null)
                    AddError("CharacterData.enhancedSkillBranch is missing.");
                else
                    ValidateSkillData(data.enhancedSkillBranch, "CharacterData.enhancedSkillBranch");
            }

            if (preset.createUltimateData)
            {
                if (data.ultimateData == null)
                    AddError("CharacterData.ultimateData is missing.");
                else
                    ValidateUltimateData(data.ultimateData, "CharacterData.ultimateData");
            }
        }

        if (!result.HasError)
            AddInfo($"'{preset.characterName}' preset validation passed.");

        return result;
    }

    public static void CreatePrefab(CharacterBuildPreset preset)
    {
        Debug.Log($"Create Prefab: {preset.characterName}");

        if (preset.basePrefab == null
            || preset.modelPrefab == null
            || preset.characterData == null
            || preset.animatorController == null
            || string.IsNullOrWhiteSpace(preset.characterName))
        {
            Debug.LogError("Preset is invalid.");
            return;
        }

        // basePrefab 복제
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(preset.basePrefab);
        instance.name = preset.characterName;

        PlayerController player = instance.GetComponent<PlayerController>();
        Animator animator = instance.GetComponent<Animator>();
        Transform modelRoot = instance.transform.Find("ModelRoot");

        if (player == null
            || animator == null
            || modelRoot == null
            )
        {
            Debug.LogError("basePrefab's data is invalid.");
            DestroyImmediate(instance);
            return;
        }

        // model 교체
        foreach (Transform child in modelRoot)
            DestroyImmediate(child.gameObject);

        GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(preset.modelPrefab);
        newModel.transform.SetParent(modelRoot, false);

        // 모델의 transform 초기화
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;
        newModel.transform.localScale = Vector3.one;

        // 연결
        SerializedObject playerSO = new SerializedObject(player);
        SerializedProperty characterDataProp = playerSO.FindProperty("characterData");
        characterDataProp.objectReferenceValue = preset.characterData;
        playerSO.ApplyModifiedPropertiesWithoutUndo();

        animator.runtimeAnimatorController = preset.animatorController;

        // 저장 폴더 경로 계산 및 생성
        string folderPath = ResolveOutputFolder(preset);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            DestroyImmediate(instance);
            return;
        }

        EnsureFolderExists(folderPath);


        // 최종 프리팹 저장 경로 생성 후 저장
        string prefabPath = folderPath + "/" + preset.characterName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

        // 임시 인스턴스 삭제
        DestroyImmediate(instance);

        // 애셋 데이터베이스 저장 및 새로고침
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ResolveOutputFolder(CharacterBuildPreset preset)
    {
        string folderPath = string.IsNullOrWhiteSpace(preset.outputFolderPath)
            ? "Assets/Prefabs/Players"
            : preset.outputFolderPath.Trim();

        if (!folderPath.StartsWith("Assets"))
        {
            Debug.LogError("Output folder path must start with 'Assets'.");
            return null;
        }

        return folderPath;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0]; // 시작점은 항상 "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    public static void CreateDataPack(CharacterBuildPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.characterName))
        {
            Debug.LogError("Character Name is missing.");
            return;
        }

        if (preset.characterData != null)
        {
            Debug.LogWarning("CharacterData is already assigned. Clear it fi" +
                "rst if you want to generate a new pack.");
            return;
        }

        if (preset.normalComboCount < 1)
        {
            Debug.LogError("Normal Combo Count must be at least 1.");
            return;
        }

        string folderPath = ResolveDataOutputFolder(preset);
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        EnsureFolderExists(folderPath);

        CharacterData characterData = CreateAsset<CharacterData>(folderPath, $"Character_Data_{preset.characterName}");
        CharacterStatData statData = CreateAsset<CharacterStatData>(folderPath, $"Character_Stat_{preset.characterName}");

        characterData.characterName = preset.characterName;
        characterData.statData = statData;

        AttackData[] combo = new AttackData[preset.normalComboCount];
        for (int i = 0; i < combo.Length; i++)
        {
            combo[i] = CreateAsset<AttackData>(folderPath, $"Attack_{preset.characterName}_Normal_{i + 1:00}");
            combo[i].nextComboIndex = i < combo.Length - 1 ? i + 1 : -1;
        }
        characterData.normalCombo = combo;

        if (preset.createNormalSkillBranch)
        {
            characterData.normalSkillBranch = CreateAsset<SkillData>(folderPath, $"Skill_{preset.characterName}_Normal");
        }

        if (preset.createEnhancedSkillBranch)
        {
            characterData.enhancedSkillBranch = CreateAsset<SkillData>(folderPath, $"Skill_{preset.characterName}_Enhanced");
        }

        if (preset.createUltimateData)
        {
            characterData.ultimateData = CreateAsset<UltimateData>(folderPath, $"Ultimate_{preset.characterName}");
        }

        preset.characterData = characterData;

        // 에셋 바뀌었으니 저장 대상으로 표시
        EditorUtility.SetDirty(characterData);
        EditorUtility.SetDirty(statData);
        EditorUtility.SetDirty(preset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Data Pack created for '{preset.characterName}'.");
    }

    public static void AutoConfigure(CharacterBuildPreset preset)
    {
        if(preset == null)
        {
            Debug.LogError("Preset is null.");
            return;
        }

        if(string.IsNullOrWhiteSpace(preset.animationFolderPath))
        {
            Debug.LogError("Animation folder path is missing.");
            return;
        }

        if(!preset.animationFolderPath.StartsWith("Assets"))
        {
            Debug.LogError("Animation folder path must start with 'Assets'.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(preset.animationFolderPath))
        {
            Debug.LogError($"Animation folder does not exist: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"Auto Configure: {preset.characterName}");
        Debug.Log($"Animation folder: {preset.animationFolderPath}");

        string[] clipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] {preset.animationFolderPath});

        if(clipGuids.Length == 0)
        {
            Debug.LogError($"No AnimationClip found in folder: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"Found clips: {clipGuids.Length}");

        List<AnimationClip> clips = new List<AnimationClip>();

        for(int i = 0; i < clipGuids.Length; i++)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (clip == null)
                continue;

            clips.Add(clip);
        }

        if(clips.Count == 0)
        {
            Debug.LogError($"AnimationClip load failed: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"Loaded clips: {clips.Count}");

        AnimationClip idleClip = FindClipContains(clips, "Idle_Loop");
        AnimationClip walkStartClip = FindClipContains(clips, "Walk_Start");
        AnimationClip walkLoopClip = FindClipContains(clips, "Walk_Loop");
        AnimationClip walkEndClip = FindClipContains(clips, "Walk_End");
        AnimationClip runLoopClip = FindClipContains(clips, "Run_Loop");
        AnimationClip runEndClip = FindClipContains(clips, "Run_End");
        AnimationClip hitLightClip = FindClipContains(clips, "Hit_L");
        AnimationClip hitHeavyClip = FindClipContains(clips, "Hit_H");
        AnimationClip dodgeFrontClip = FindClipContains(clips, "Evade_Front");
        AnimationClip dodgeCounterStartClip = FindClipContainsExclude(clips, "Attack_Counter", "_End");
        AnimationClip dodgeCounterEndClip = FindClipContainsAll(clips, "Attack_Counter", "_End");
        AnimationClip parryLightClip = FindClipContains(clips, "ParryAid_L");
        AnimationClip parryHeavyClip = FindClipContains(clips, "ParryAid_H");
        AnimationClip parryStartClip = FindClipContains(clips, "ParryAid_Start");

        Debug.Log($"Idle = {(idleClip != null ? idleClip.name : "null")}");
        Debug.Log($"WalkStart = {(walkStartClip != null ? walkStartClip.name : "null")}");
        Debug.Log($"WalkLoop = {(walkLoopClip != null ? walkLoopClip.name : "null")}");
        Debug.Log($"WalkEnd = {(walkEndClip != null ? walkEndClip.name : "null")}");
        Debug.Log($"RunLoop = {(runLoopClip != null ? runLoopClip.name : "null")}");
        Debug.Log($"RunEnd = {(runEndClip != null ? runEndClip.name : "null")}");
        Debug.Log($"HitLight = {(hitLightClip != null ? hitLightClip.name : "null")}");
        Debug.Log($"HitHeavy = {(hitHeavyClip != null ? hitHeavyClip.name : "null")}");
        Debug.Log($"DodgeFront = {(dodgeFrontClip != null ? dodgeFrontClip.name : "null")}");
        Debug.Log($"CounterStart = {(dodgeCounterStartClip != null ? dodgeCounterStartClip.name : "null")}");
        Debug.Log($"CounterEnd = {(dodgeCounterEndClip != null ? dodgeCounterEndClip.name : "null")}");
        Debug.Log($"ParryLight = {(parryLightClip != null ? parryLightClip.name : "null")}");
        Debug.Log($"ParryHeavy = {(parryHeavyClip != null ? parryHeavyClip.name : "null")}");
        Debug.Log($"ParryStart = {(parryStartClip != null ? parryStartClip.name : "null")}");


        const bool verboseClipListLog = false;

        if (verboseClipListLog)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                Debug.Log($"Clip[{i}] = {clips[i].name}");
            }
        }

        if (preset.characterData == null)
        {
            Debug.LogError("CharacterData is missing.");
            return;
        }

        CharacterData data = preset.characterData;

        data.idleAnim = idleClip != null ? idleClip.name : string.Empty;
        data.walkStartAnim = walkStartClip != null ? walkStartClip.name : string.Empty;
        data.walkLoopAnim = walkLoopClip != null ? walkLoopClip.name : string.Empty;
        data.walkEndAnim = walkEndClip != null ? walkEndClip.name : string.Empty;
        data.runLoopAnim = runLoopClip != null ? runLoopClip.name : string.Empty;
        data.runEndAnim = runEndClip != null ? runEndClip.name : string.Empty;
        data.hitLightFrontAnim = hitLightClip != null ? hitLightClip.name : string.Empty;
        data.hitHeavyFrontAnim = hitHeavyClip != null ? hitHeavyClip.name : string.Empty;
        data.dodgeFrontAnim = dodgeFrontClip != null ? dodgeFrontClip.name : string.Empty;
        data.dodgeCounterStartAnim = dodgeCounterStartClip != null ? dodgeCounterStartClip.name : string.Empty;
        data.dodgeCounterEndAnim = dodgeCounterEndClip != null ? dodgeCounterEndClip.name : string.Empty;
        data.parrySupportLightAnim = parryLightClip != null ? parryLightClip.name : string.Empty;
        data.parrySupportHeavyAnim = parryHeavyClip != null ? parryHeavyClip.name : string.Empty;
        data.parrySupportStartAnim = parryStartClip != null ? parryStartClip.name : string.Empty;

        // 기본 공격 자동 배정
        if (data.normalCombo != null)
        {
            for (int i = 0; i < data.normalCombo.Length; i++)
            {
                AttackData attack = data.normalCombo[i];

                if (attack == null)
                    continue;

                string comboToken = $"Attack_Normal_{i + 1:00}_01";

                AnimationClip comboClip = FindClipContainsExclude(clips, comboToken, "_End");
                AnimationClip comboEndClip = FindClipContainsAll(clips, comboToken, "_End");

                attack.attackAnim = comboClip != null ? comboClip.name : string.Empty;
                attack.endAnim = comboEndClip != null ? comboEndClip.name : string.Empty;

                EditorUtility.SetDirty(attack);

                Debug.Log($"NormalCombo[{i}] = {attack.attackAnim} / {attack.endAnim}");
            }
        }

        // 노말 스킬 자동 배정
        if (data.normalSkillBranch != null)
        {
            AnimationClip normalSkillClip = FindClipContainsExclude(clips, preset.normalSkillToken, "_End");
            AnimationClip normalSkillEndClip = FindClipContainsAll(clips, preset.normalSkillToken, "_End");

            data.normalSkillBranch.skillAnim = normalSkillClip != null ? normalSkillClip.name : string.Empty;
            data.normalSkillBranch.endAnim = normalSkillEndClip != null ? normalSkillEndClip.name : string.Empty;

            EditorUtility.SetDirty(data.normalSkillBranch);

            Debug.Log($"NormalSkill = {data.normalSkillBranch.skillAnim} / {data.normalSkillBranch.endAnim}");
        }

        if (data.normalSkillBranch != null)
        {
            data.normalSkillBranch.hitBoxSlotIndex = 1;
            EditorUtility.SetDirty(data.normalSkillBranch);

            Debug.Log($"NormalSkill HitBox Slot = {data.normalSkillBranch.hitBoxSlotIndex}");
        }

        // 강화 스킬 자동 배정
        if (data.enhancedSkillBranch != null)
        {
            AnimationClip enhancedSkillClip = FindClipContainsExclude(clips, preset.enhancedSkillToken, "_End");
            AnimationClip enhancedSkillEndClip = FindClipContainsAll(clips, preset.enhancedSkillToken, "_End");

            data.enhancedSkillBranch.skillAnim = enhancedSkillClip != null ? enhancedSkillClip.name : string.Empty;
            data.enhancedSkillBranch.endAnim = enhancedSkillEndClip != null ? enhancedSkillEndClip.name : string.Empty;

            EditorUtility.SetDirty(data.enhancedSkillBranch);

            Debug.Log($"EnhancedSkill = {data.enhancedSkillBranch.skillAnim} / {data.enhancedSkillBranch.endAnim}");
        }

        if (data.enhancedSkillBranch != null)
        {
            data.enhancedSkillBranch.hitBoxSlotIndex = 2;
            EditorUtility.SetDirty(data.enhancedSkillBranch);

            Debug.Log($"EnhancedSkill HitBox Slot = {data.enhancedSkillBranch.hitBoxSlotIndex}");
        }

        // 궁극기 자동 배정
        if (data.ultimateData != null)
        {
            AnimationClip ultStartClip = FindClipContains(clips, "SwitchIn_Attack_Ex_Start");
            AnimationClip ultHitClip = FindClipContainsExcludeAll(clips, "SwitchIn_Attack_Ex", "_Start", "_End");
            AnimationClip ultEndClip = FindClipContainsAll(clips, "SwitchIn_Attack_Ex", "_End");

            data.ultimateData.ultStartAnim = ultStartClip != null ? ultStartClip.name : string.Empty;
            data.ultimateData.ultHitAnim = ultHitClip != null ? ultHitClip.name : string.Empty;
            data.ultimateData.ultEndAnim = ultEndClip != null ? ultEndClip.name : string.Empty;

            EditorUtility.SetDirty(data.ultimateData);

            Debug.Log($"Ultimate = {data.ultimateData.ultStartAnim} / {data.ultimateData.ultHitAnim} / {data.ultimateData.ultEndAnim}");
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Debug.Log("CharacterData auto-configured.");
        Debug.Log($"Normal Combo Count = {(data.normalCombo != null ? data.normalCombo.Length : 0)}");
    }

    private static AnimationClip FindClipContains(List<AnimationClip> clips, string token)
    {
        if (clips == null || clips.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(token))
            return null;

        for(int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];

            if (clip == null)
                continue;

            if (clip.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return clip;
        }

        return null;
    }

    private static AnimationClip FindClipContainsExclude(List<AnimationClip> clips, string includeToken, string excludeToken)
    {
        if (clips == null || clips.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(includeToken))
            return null;

        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];

            if (clip == null)
                continue;

            if (clip.name.IndexOf(includeToken, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!string.IsNullOrWhiteSpace(excludeToken) &&
                clip.name.IndexOf(excludeToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            return clip;
        }

        return null;
    }

    private static AnimationClip FindClipContainsAll(List<AnimationClip> clips, params string[] tokens)
    {
        if (clips == null || clips.Count == 0)
            return null;

        if (tokens == null || tokens.Length == 0)
            return null;

        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];

            if (clip == null)
                continue;

            bool matched = true;

            for (int j = 0; j < tokens.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(tokens[j]))
                    continue;

                if (clip.name.IndexOf(tokens[j], System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return clip;
        }

        return null;
    }

    private static AnimationClip FindClipContainsExcludeAll(List<AnimationClip> clips, string includeToken, params string[] excludeTokens)
    {
        if (clips == null || clips.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(includeToken))
            return null;

        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];

            if (clip == null)
                continue;

            if (clip.name.IndexOf(includeToken, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            bool excluded = false;

            for (int j = 0; j < excludeTokens.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(excludeTokens[j]))
                    continue;

                if (clip.name.IndexOf(excludeTokens[j], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    excluded = true;
                    break;
                }
            }

            if (!excluded)
                return clip;
        }

        return null;
    }

    private static string ResolveDataOutputFolder(CharacterBuildPreset preset)
    {
        string folderPath = string.IsNullOrWhiteSpace(preset.dataOutputFolderPath)
            ? $"Assets/Characters/{preset.characterName}/Data"
            : preset.dataOutputFolderPath.Trim();

        if (!folderPath.StartsWith("Assets"))
        {
            Debug.LogError("Data output folder path must start with 'Assets'.");
            return null;
        }

        return folderPath;
    }

    private static T CreateAsset<T>(string folderPath, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{fileName}.asset");
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }
}
