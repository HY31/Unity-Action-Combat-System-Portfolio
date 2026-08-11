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
                AddError($"{label} 값이 없습니다.");
        }

        void ValidateHitBoxShape(HitBoxShape shape, string label)
        {
            if (!shape.HasValidSize)
                AddError($"{label}.hitBoxShape 크기는 모든 축이 0보다 커야 합니다.");
        }

        void ValidateAttackData(AttackData attack, string label)
        {
            if (attack == null)
            {
                AddError($"{label} 데이터가 없습니다.");
                return;
            }

            AddErrorIfEmpty(attack.attackAnim, $"{label}.attackAnim");
            AddErrorIfEmpty(attack.endAnim, $"{label}.endAnim");
            ValidateHitBoxShape(attack.hitBoxShape, label);
        }

        void ValidateSkillData(SkillData skill, string label)
        {
            if (skill == null)
            {
                AddError($"{label} 데이터가 없습니다.");
                return;
            }

            AddErrorIfEmpty(skill.skillAnim, $"{label}.skillAnim");
            AddErrorIfEmpty(skill.endAnim, $"{label}.endAnim");

            ValidateHitBoxShape(skill.hitBoxShape, label);
        }

        void ValidateUltimateData(UltimateData ult, string label)
        {
            if (ult == null)
            {
                AddError($"{label} 데이터가 없습니다.");
                return;
            }

            AddErrorIfEmpty(ult.ultStartAnim, $"{label}.ultStartAnim");
            AddErrorIfEmpty(ult.ultHitAnim, $"{label}.ultHitAnim");
            AddErrorIfEmpty(ult.ultEndAnim, $"{label}.ultEndAnim");
            ValidateHitBoxShape(ult.hitBoxShape, label);
        }

        if (preset.basePrefab == null)
            AddError("기본 프리팹이 없습니다.");

        if (preset.modelPrefab == null)
            AddError("모델 프리팹이 없습니다.");

        if (preset.characterData == null)
            AddError("캐릭터 데이터가 없습니다.");

        if (preset.animatorController == null)
            AddError("애니메이터 컨트롤러가 없습니다.");

        if (preset.basePrefab == null)
            return result;

        PlayerController player = preset.basePrefab.GetComponent<PlayerController>();
        Animator animator = preset.basePrefab.GetComponent<Animator>();
        CharacterController controller = preset.basePrefab.GetComponent<CharacterController>();

        if (player == null)
            AddError("기본 프리팹에 PlayerController가 없습니다.");

        if (animator == null)
            AddError("기본 프리팹에 Animator가 없습니다.");

        if (controller == null)
            AddError("기본 프리팹에 CharacterController가 없습니다.");

        if (player == null)
            return result;

        if (player.CameraFollowTarget == null)
            AddError("PlayerController에 CameraFollowTarget이 없습니다.");

        if (player.AttackHitBox == null)
            AddError("PlayerController에 공용 AttackHitBox가 없습니다.");

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
                AddError($"CharacterData.normalCombo 개수가 맞지 않습니다. 예상값: {preset.normalComboCount}, 실제값: {data.normalCombo.Length}.");
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
                    AddError("CharacterData.normalSkillBranch가 없습니다.");
                else
                    ValidateSkillData(data.normalSkillBranch, "CharacterData.normalSkillBranch");
            }

            if (preset.createEnhancedSkillBranch)
            {
                if (data.enhancedSkillBranch == null)
                    AddError("CharacterData.enhancedSkillBranch가 없습니다.");
                else
                    ValidateSkillData(data.enhancedSkillBranch, "CharacterData.enhancedSkillBranch");
            }

            if (preset.createUltimateData)
            {
                if (data.ultimateData == null)
                    AddError("CharacterData.ultimateData가 없습니다.");
                else
                    ValidateUltimateData(data.ultimateData, "CharacterData.ultimateData");
            }
        }

        if (!result.HasError)
            AddInfo($"'{preset.characterName}' 프리셋 검증을 통과했습니다.");

        return result;
    }

    public static void CreatePrefab(CharacterBuildPreset preset)
    {
        Debug.Log($"프리팹 생성: {preset.characterName}");

        if (preset.basePrefab == null
            || preset.modelPrefab == null
            || preset.characterData == null
            || preset.animatorController == null
            || string.IsNullOrWhiteSpace(preset.characterName))
        {
            Debug.LogError("프리셋이 올바르지 않습니다.");
            return;
        }

        // 기본 프리팹 복제
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
            Debug.LogError("기본 프리팹의 구성이 올바르지 않습니다.");
            DestroyImmediate(instance);
            return;
        }

        // 모델 교체
        foreach (Transform child in modelRoot)
            DestroyImmediate(child.gameObject);

        GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(preset.modelPrefab);
        newModel.transform.SetParent(modelRoot, false);

        // 모델의 트랜스폼 초기화
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
            Debug.LogError("출력 폴더 경로는 'Assets'로 시작해야 합니다.");
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
            Debug.LogError("캐릭터 이름이 없습니다.");
            return;
        }

        if (preset.characterData != null)
        {
            Debug.LogWarning("캐릭터 데이터가 이미 할당되어 있습니다. 새 데이터 묶음을 만들려면 기존 할당을 먼저 지우세요.");
            return;
        }

        if (preset.normalComboCount < 1)
        {
            Debug.LogError("일반 콤보 개수는 1 이상이어야 합니다.");
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

        Debug.Log($"'{preset.characterName}'의 데이터 묶음을 생성했습니다.");
    }

    public static void AutoConfigure(CharacterBuildPreset preset)
    {
        if(preset == null)
        {
            Debug.LogError("프리셋이 없습니다.");
            return;
        }

        if(string.IsNullOrWhiteSpace(preset.animationFolderPath))
        {
            Debug.LogError("애니메이션 폴더 경로가 없습니다.");
            return;
        }

        if(!preset.animationFolderPath.StartsWith("Assets"))
        {
            Debug.LogError("애니메이션 폴더 경로는 'Assets'로 시작해야 합니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(preset.animationFolderPath))
        {
            Debug.LogError($"애니메이션 폴더가 존재하지 않습니다: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"자동 구성: {preset.characterName}");
        Debug.Log($"애니메이션 폴더: {preset.animationFolderPath}");

        string[] clipGuids = AssetDatabase.FindAssets(
            "t:AnimationClip",
            new[] {preset.animationFolderPath});

        if(clipGuids.Length == 0)
        {
            Debug.LogError($"폴더에서 애니메이션 클립을 찾지 못했습니다: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"발견한 클립 수: {clipGuids.Length}");

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
            Debug.LogError($"애니메이션 클립을 불러오지 못했습니다: {preset.animationFolderPath}");
            return;
        }

        Debug.Log($"불러온 클립 수: {clips.Count}");

        AnimationClip idleClip = FindFirstClip(
            FindClipContains(clips, "Idle_Loop"),
            FindClipContainsExcludeAll(clips, "Idle", "_Start", "_End"));
        AnimationClip walkStartClip = FindClipContains(clips, "Walk_Start");
        AnimationClip walkLoopClip = FindFirstClip(
            FindClipContains(clips, "Walk_Loop"),
            FindClipContainsExcludeAll(clips, "Walk", "_Start", "_End"));
        AnimationClip walkEndClip = FindClipContains(clips, "Walk_End");
        AnimationClip runLoopClip = FindFirstClip(
            FindClipContains(clips, "Run_Loop"),
            FindClipContainsExcludeAll(clips, "Run", "_Start", "_End"));
        AnimationClip runEndClip = FindClipContains(clips, "Run_End");
        AnimationClip hitLightClip = FindClipContains(clips, "Hit_L");
        AnimationClip hitHeavyClip = FindClipContains(clips, "Hit_H");
        AnimationClip dodgeFrontClip = FindClipContains(clips, "Evade_Front");
        AnimationClip dodgeCounterStartClip = FindClipContainsExclude(clips, "Attack_Counter", "_End");
        AnimationClip dodgeCounterEndClip = FindClipContainsAll(clips, "Attack_Counter", "_End");
        AnimationClip parryLightClip = FindClipContains(clips, "ParryAid_L");
        AnimationClip parryHeavyClip = FindClipContains(clips, "ParryAid_H");
        AnimationClip parryStartClip = FindClipContains(clips, "ParryAid_Start");

        Debug.Log($"대기 = {(idleClip != null ? idleClip.name : "없음")}");
        Debug.Log($"걷기 시작 = {(walkStartClip != null ? walkStartClip.name : "없음")}");
        Debug.Log($"걷기 반복 = {(walkLoopClip != null ? walkLoopClip.name : "없음")}");
        Debug.Log($"걷기 종료 = {(walkEndClip != null ? walkEndClip.name : "없음")}");
        Debug.Log($"달리기 반복 = {(runLoopClip != null ? runLoopClip.name : "없음")}");
        Debug.Log($"달리기 종료 = {(runEndClip != null ? runEndClip.name : "없음")}");
        Debug.Log($"약한 피격 = {(hitLightClip != null ? hitLightClip.name : "없음")}");
        Debug.Log($"강한 피격 = {(hitHeavyClip != null ? hitHeavyClip.name : "없음")}");
        Debug.Log($"전방 회피 = {(dodgeFrontClip != null ? dodgeFrontClip.name : "없음")}");
        Debug.Log($"반격 시작 = {(dodgeCounterStartClip != null ? dodgeCounterStartClip.name : "없음")}");
        Debug.Log($"반격 종료 = {(dodgeCounterEndClip != null ? dodgeCounterEndClip.name : "없음")}");
        Debug.Log($"약한 패링 = {(parryLightClip != null ? parryLightClip.name : "없음")}");
        Debug.Log($"강한 패링 = {(parryHeavyClip != null ? parryHeavyClip.name : "없음")}");
        Debug.Log($"패링 시작 = {(parryStartClip != null ? parryStartClip.name : "없음")}");


        bool verboseClipListLog = false;

        if (verboseClipListLog)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                Debug.Log($"클립[{i}] = {clips[i].name}");
            }
        }

        if (preset.characterData == null)
        {
            Debug.LogError("캐릭터 데이터가 없습니다.");
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
                string comboFallbackToken = $"Attack_Normal_{i + 1:00}";

                AnimationClip comboClip = FindFirstClip(
                    FindClipContainsExclude(clips, comboToken, "_End"),
                    FindClipContainsExclude(clips, comboFallbackToken, "_End"));
                AnimationClip comboEndClip = FindFirstClip(
                    FindClipContainsAll(clips, comboToken, "_End"),
                    FindClipContainsAll(clips, comboFallbackToken, "_End"));

                attack.attackAnim = comboClip != null ? comboClip.name : string.Empty;
                attack.endAnim = comboEndClip != null ? comboEndClip.name : string.Empty;

                EditorUtility.SetDirty(attack);

                Debug.Log($"일반 콤보[{i}] = {attack.attackAnim} / {attack.endAnim}");
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

            Debug.Log($"일반 스킬 = {data.normalSkillBranch.skillAnim} / {data.normalSkillBranch.endAnim}");
        }


        // 강화 스킬 자동 배정
        if (data.enhancedSkillBranch != null)
        {
            AnimationClip enhancedSkillClip = FindFirstClip(
                FindClipContainsExclude(clips, preset.enhancedSkillToken, "_End"),
                FindClipContainsExclude(clips, "Attack_Special", "_End"));
            AnimationClip enhancedSkillEndClip = FindFirstClip(
                FindClipContainsAll(clips, preset.enhancedSkillToken, "_End"),
                FindClipContainsAll(clips, "Attack_Special", "_End"));

            data.enhancedSkillBranch.skillAnim = enhancedSkillClip != null ? enhancedSkillClip.name : string.Empty;
            data.enhancedSkillBranch.endAnim = enhancedSkillEndClip != null ? enhancedSkillEndClip.name : string.Empty;

            EditorUtility.SetDirty(data.enhancedSkillBranch);

            Debug.Log($"강화 스킬 = {data.enhancedSkillBranch.skillAnim} / {data.enhancedSkillBranch.endAnim}");
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

            Debug.Log($"궁극기 = {data.ultimateData.ultStartAnim} / {data.ultimateData.ultHitAnim} / {data.ultimateData.ultEndAnim}");
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Debug.Log("캐릭터 데이터 자동 구성을 완료했습니다.");
        Debug.Log($"일반 콤보 개수 = {(data.normalCombo != null ? data.normalCombo.Length : 0)}");
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

    private static AnimationClip FindFirstClip(params AnimationClip[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
                return candidates[i];
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
            Debug.LogError("데이터 출력 폴더 경로는 'Assets'로 시작해야 합니다.");
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
