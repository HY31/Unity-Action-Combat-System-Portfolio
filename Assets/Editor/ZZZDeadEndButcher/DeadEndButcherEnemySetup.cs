using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class DeadEndButcherEnemySetup
{
    private const string SessionKey = "ZZZ.DeadEndButcher.EnemyReplacement.v2";
    private const string MenuRoot = "Tools/ZZZ Dead End Butcher/";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ModelPath =
        "Assets/ThirdParty/ZZZ_DeadEndButcher/Models/Monster_NotoriousDeadEndButcher_P1.fbx";
    private const string ControllerPath = "Assets/Animation/DeadEndButcher.controller";
    private const string EnemyDataPath = "Assets/Monster/Data/DeadEndButcher_Data.asset";
    private const string AttackDataRoot = "Assets/ScriptableObject/Enemy";
    private const string MaterialRoot = "Assets/ThirdParty/ZZZ_DeadEndButcher/Materials";
    private const string LegacyModelPath = "Assets/Monster/Monster_Durahan_LOD3";
    private const float BodyCollisionHeight = 100f;

    private static readonly string[] LegacyAssetPaths =
    {
        LegacyModelPath,
        "Assets/Animation/Durahan.controller",
        "Assets/Monster/Data/Durahan_Data.asset",
        AttackDataRoot + "/Durahan_Attack_Data_1.asset",
        AttackDataRoot + "/Durahan_Attack_Data_2.asset",
        AttackDataRoot + "/Durahan_Attack_Data_3.asset",
        AttackDataRoot + "/Durahan_Attack_Data_4.asset"
    };

    static DeadEndButcherEnemySetup()
    {
        EditorApplication.delayCall += AutoReplaceOnce;
    }

    [MenuItem(MenuRoot + "Replace Durahan Enemy Now")]
    private static void ReplaceFromMenu()
    {
        ReplaceEnemyAndSave(deleteLegacyAssets: true);
    }

    [MenuItem(MenuRoot + "Replace Durahan Enemy Now", true)]
    private static bool ValidateReplaceFromMenu()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating
            && activeScene.IsValid()
            && activeScene.path == ScenePath;
    }

    private static void AutoReplaceOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoReplaceOnce;
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != ScenePath)
            return;

        if (IsAlreadyConverted(activeScene))
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        if (ReplaceEnemyAndSave(deleteLegacyAssets: true))
            SessionState.SetBool(SessionKey, true);
    }

    private static bool ReplaceEnemyAndSave(bool deleteLegacyAssets)
    {
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException($"설정을 실행하기 전에 {ScenePath} 씬을 여세요.");

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
                throw new InvalidOperationException($"도살자 모델이 없습니다: {ModelPath}");

            AnimationClip[] clips = LoadModelClips();
            if (clips.Length == 0)
                throw new InvalidOperationException($"{ModelPath}에서 가져온 애니메이션 클립이 없습니다.");

            AnimationClip idleClip = FindIdleClip(clips);
            AnimationClip[] attackClips = FindAttackClips(clips, idleClip);
            AnimatorController animatorController = BuildAnimatorController(clips, idleClip);
            EnemyAttackData[] attackData = BuildAttackData(attackClips, idleClip);
            EnemyData enemyData = BuildEnemyData(attackData);

            GameObject enemyRoot = FindEnemyRoot(scene);
            if (enemyRoot == null)
                throw new InvalidOperationException("현재 씬에 EnemyController 루트가 없습니다.");

            ReplaceVisual(enemyRoot, modelPrefab, animatorController, enemyData);
            RemoveLooseModelPreviews(scene, enemyRoot);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Unity가 {ScenePath} 씬을 저장하지 못해 기존 애셋을 유지했습니다.");

            if (deleteLegacyAssets)
                DeleteLegacyAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"씬의 듀라한 적을 도살자로 교체했습니다. " +
                $"애니메이터 상태: {clips.Length}개, 공격 패턴: {attackData.Length}개. " +
                "기존 듀라한 애셋을 제거하기 전에 씬을 저장했습니다.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool IsAlreadyConverted(Scene scene)
    {
        GameObject enemyRoot = FindEnemyRoot(scene);
        if (enemyRoot == null)
            return false;

        bool hasButcherVisual = enemyRoot.transform
            .Cast<Transform>()
            .Any(child => child.name == "DeadEndButcher_Visual");
        bool hasLegacyAssets = LegacyAssetPaths.Any(
            path => AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path));
        return hasButcherVisual && !hasLegacyAssets && !HasLooseModelPreviews(scene, enemyRoot);
    }

    private static GameObject FindEnemyRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            EnemyController controller = root.GetComponentInChildren<EnemyController>(true);
            if (controller != null)
                return controller.gameObject;
        }

        return null;
    }

    private static bool HasLooseModelPreviews(Scene scene, GameObject enemyRoot)
    {
        return scene.GetRootGameObjects().Any(root =>
            root != enemyRoot
            && string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                ModelPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveLooseModelPreviews(Scene scene, GameObject enemyRoot)
    {
        GameObject[] loosePreviews = scene.GetRootGameObjects()
            .Where(root =>
                root != enemyRoot
                && string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                    ModelPath,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (GameObject loosePreview in loosePreviews)
            UnityEngine.Object.DestroyImmediate(loosePreview);
    }

    private static AnimationClip[] LoadModelClips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .GroupBy(clip => clip.name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(clip => clip.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static AnimationClip FindIdleClip(IEnumerable<AnimationClip> clips)
    {
        AnimationClip[] clipArray = clips.ToArray();
        return clipArray.FirstOrDefault(clip =>
                   clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
                   && clip.name.IndexOf("To", StringComparison.OrdinalIgnoreCase) < 0)
            ?? clipArray.FirstOrDefault(clip =>
                clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? clipArray[0];
    }

    private static AnimationClip[] FindAttackClips(
        IEnumerable<AnimationClip> clips,
        AnimationClip idleClip)
    {
        AnimationClip[] allClips = clips.ToArray();
        AnimationClip[] attacks = allClips
            .Where(clip =>
                clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0
                && clip.name.IndexOf("Hit", StringComparison.OrdinalIgnoreCase) < 0)
            .Take(4)
            .ToArray();

        if (attacks.Length > 0)
            return attacks;

        return allClips
            .Where(clip => clip != idleClip)
            .Take(4)
            .ToArray();
    }

    private static AnimatorController BuildAnimatorController(
        IEnumerable<AnimationClip> clips,
        AnimationClip idleClip)
    {
        EnsureFolder("Assets/Animation");
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
            stateMachine.RemoveState(childState.state);

        AnimatorState idleState = null;
        int index = 0;
        foreach (AnimationClip clip in clips)
        {
            Vector3 position = new Vector3((index % 6) * 230f, (index / 6) * 75f, 0f);
            AnimatorState state = stateMachine.AddState(clip.name, position);
            state.motion = clip;
            state.writeDefaultValues = true;

            if (clip == idleClip)
                idleState = state;

            index++;
        }

        stateMachine.defaultState = idleState ?? stateMachine.states[0].state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static EnemyAttackData[] BuildAttackData(
        IReadOnlyList<AnimationClip> attackClips,
        AnimationClip idleClip)
    {
        EnsureFolder(AttackDataRoot);
        List<EnemyAttackData> attackData = new List<EnemyAttackData>();

        for (int i = 0; i < attackClips.Count; i++)
        {
            string path = $"{AttackDataRoot}/DeadEndButcher_Attack_Data_{i + 1}.asset";
            EnemyAttackData data = AssetDatabase.LoadAssetAtPath<EnemyAttackData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyAttackData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.attackAnim = attackClips[i].name;
            data.endAnim = idleClip.name;
            data.warningType = i % 2 == 0 ? WarningType.Yellow : WarningType.Red;
            data.warningStart = 0.15f;
            data.warningEnd = 0.28f;
            data.startUpEnd = 0.30f;
            data.activeEnd = 0.60f;
            data.reactionStart = 0.15f;
            data.reactionEnd = 0.45f;
            data.forwardMoveSpeed = 4f;
            data.moveStart = 0f;
            data.moveEnd = 0.2f;
            data.damage = 10f;
            EditorUtility.SetDirty(data);
            attackData.Add(data);
        }

        return attackData.ToArray();
    }

    private static EnemyData BuildEnemyData(EnemyAttackData[] attackData)
    {
        EnsureFolder("Assets/Monster/Data");
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, EnemyDataPath);
        }

        data.enemyName = "Dead End Butcher";
        data.level = 70;
        data.maxHp = 300000f;
        data.attack = 10f;
        data.defense = 50f;
        data.impact = 10f;
        data.maxStun = 100f;
        data.stunResistance = 0f;
        data.groggyDuration = 10f;
        data.groggyDamageMultiplier = 1.5f;
        data.anomalyThreshold = 100f;
        data.elementModifiers = Array.Empty<EnemyElementModifier>();
        data.attackPatterns = attackData;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void ReplaceVisual(
        GameObject enemyRoot,
        GameObject modelPrefab,
        RuntimeAnimatorController animatorController,
        EnemyData enemyData)
    {
        Transform oldVisual = FindDirectVisual(enemyRoot.transform, "Durahan");
        Transform butcherVisual = FindDirectVisual(enemyRoot.transform, "DeadEndButcher");
        float targetGroundY = enemyRoot.transform.position.y;

        if (oldVisual != null && TryGetWorldBounds(oldVisual.gameObject, out Bounds oldBounds))
            targetGroundY = oldBounds.min.y;

        if (oldVisual != null)
            UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

        if (butcherVisual == null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(
                modelPrefab,
                enemyRoot.scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Unity가 도살자 FBX를 인스턴스화하지 못했습니다.");

            butcherVisual = instance.transform;
            butcherVisual.name = "DeadEndButcher_Visual";
            butcherVisual.SetParent(enemyRoot.transform, false);
        }

        butcherVisual.localPosition = Vector3.zero;
        butcherVisual.localRotation = Quaternion.identity;
        butcherVisual.localScale = Vector3.one;
        SetLayerRecursively(butcherVisual.gameObject, enemyRoot.layer);
        ApplyMaterials(butcherVisual.gameObject);

        if (TryGetWorldBounds(butcherVisual.gameObject, out Bounds newBounds))
        {
            Vector3 position = butcherVisual.position;
            position.y += targetGroundY - newBounds.min.y;
            butcherVisual.position = position;
        }

        Animator animator = butcherVisual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = butcherVisual.gameObject.AddComponent<Animator>();

        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EnemyController enemyController = enemyRoot.GetComponent<EnemyController>();
        SerializedObject controllerObject = new SerializedObject(enemyController);
        controllerObject.FindProperty("enemyData").objectReferenceValue = enemyData;
        controllerObject.FindProperty("animator").objectReferenceValue = animator;
        controllerObject.FindProperty("currentHp").floatValue = enemyData.maxHp;
        controllerObject.FindProperty("currentStun").floatValue = 0f;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enemyController);

        HurtBox hurtBox = enemyRoot.GetComponent<HurtBox>();
        if (hurtBox != null)
        {
            SerializedObject hurtBoxObject = new SerializedObject(hurtBox);
            hurtBoxObject.FindProperty("ownerRoot").objectReferenceValue = enemyRoot.transform;
            hurtBoxObject.ApplyModifiedPropertiesWithoutUndo();
        }

        HitBox attackHitBox = controllerObject.FindProperty("attackHitBox")
            .objectReferenceValue as HitBox;
        if (attackHitBox != null)
        {
            SerializedObject hitBoxObject = new SerializedObject(attackHitBox);
            hitBoxObject.FindProperty("ownerRoot").objectReferenceValue = enemyRoot.transform;
            hitBoxObject.ApplyModifiedPropertiesWithoutUndo();
        }

        FitHurtBox(enemyRoot, butcherVisual.gameObject);
        ConfigureBodyCollision(enemyRoot);
    }

    private static void ConfigureBodyCollision(GameObject enemyRoot)
    {
        Transform bodyCollisionTransform = enemyRoot.transform.Find("BodyCollision");
        if (bodyCollisionTransform == null)
        {
            GameObject bodyCollisionObject = new GameObject("BodyCollision");
            bodyCollisionTransform = bodyCollisionObject.transform;
            bodyCollisionTransform.SetParent(enemyRoot.transform, false);
        }

        BoxCollider bodyCollider = bodyCollisionTransform.GetComponent<BoxCollider>();
        if (bodyCollider == null)
            bodyCollider = bodyCollisionTransform.gameObject.AddComponent<BoxCollider>();

        bodyCollider.isTrigger = false;

        // 공중 공격 중 캐릭터가 보스 머리 위에 착지하지 않도록 충돌 기둥을 충분히 높게 유지한다.
        Vector3 center = bodyCollider.center;
        Vector3 size = bodyCollider.size;
        center.y = BodyCollisionHeight * 0.5f;
        size.y = BodyCollisionHeight;
        bodyCollider.center = center;
        bodyCollider.size = size;
        EditorUtility.SetDirty(bodyCollider);
    }

    private static Transform FindDirectVisual(Transform root, string nameFragment)
    {
        foreach (Transform child in root)
        {
            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
            if (child.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0
                || sourcePath.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return null;
    }

    private static void FitHurtBox(GameObject enemyRoot, GameObject visual)
    {
        if (!TryGetWorldBounds(visual, out Bounds worldBounds))
            return;

        Transform root = enemyRoot.transform;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = worldBounds.center + Vector3.Scale(
                worldBounds.extents,
                new Vector3(x, y, z));
            Vector3 localCorner = root.InverseTransformPoint(corner);
            min = Vector3.Min(min, localCorner);
            max = Vector3.Max(max, localCorner);
        }

        BoxCollider collider = enemyRoot.GetComponent<BoxCollider>();
        if (collider == null)
            collider = enemyRoot.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.center = (min + max) * 0.5f;
        Vector3 size = max - min;
        collider.size = new Vector3(size.x * 1.05f, size.y, size.z * 1.05f);
        EditorUtility.SetDirty(collider);
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private static void ApplyMaterials(GameObject visual)
    {
        EnsureFolder(MaterialRoot);
        Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase)
        {
            ["Body1"] = CreateOrUpdateMaterial("Body1"),
            ["Body2"] = CreateOrUpdateMaterial("Body2"),
            ["Body3"] = CreateOrUpdateMaterial("Body3"),
            ["Weapon"] = CreateOrUpdateMaterial("Weapon")
        };

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] assigned = renderer.sharedMaterials;
            for (int i = 0; i < assigned.Length; i++)
            {
                string sourceName = assigned[i] != null ? assigned[i].name : renderer.name;
                assigned[i] = PickMaterial(materials, sourceName + " " + renderer.name);
            }
            renderer.sharedMaterials = assigned;
        }
    }

    private static Material CreateOrUpdateMaterial(string part)
    {
        string materialPath = $"{MaterialRoot}/MAT_DeadEndButcher_{part}_URP.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = $"MAT_DeadEndButcher_{part}_URP" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        string texturePart = part == "Weapon" ? "_Weapon" : part.Substring(part.Length - 1);
        string textureName = part == "Weapon"
            ? $"Monster_NotoriousDeadEndButcher{texturePart}_D.png"
            : $"Monster_NotoriousDeadEndButcher{texturePart}_D.png";
        Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"Assets/ThirdParty/ZZZ_DeadEndButcher/Textures/{textureName}");

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", baseMap);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", baseMap);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.35f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material PickMaterial(
        IReadOnlyDictionary<string, Material> materials,
        string sourceName)
    {
        foreach (KeyValuePair<string, Material> pair in materials)
        {
            if (sourceName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                return pair.Value;
        }

        return materials["Body1"];
    }

    private static void DeleteLegacyAssets()
    {
        foreach (string path in LegacyAssetPaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.IsValidFolder(path))
                continue;

            if (!AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Unity가 기존 듀라한 애셋을 삭제하지 못했습니다: {path}");
        }
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separator = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, separator);
        string folderName = folderPath.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
