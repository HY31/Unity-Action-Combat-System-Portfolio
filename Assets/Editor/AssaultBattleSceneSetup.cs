using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssaultBattleSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootName = "AssaultBattle";
    private const string TriggerName = "AssaultBattleStartTrigger";

    [MenuItem("Tools/Assault Battle/Setup Sample Scene")]
    public static void ConfigureSampleScene()
    {
        Scene scene = ResolveSampleScene();
        if (!scene.IsValid())
            return;

        GameObject enemyObject = GameObject.Find("Enemy");
        EnemyController boss = enemyObject != null
            ? enemyObject.GetComponent<EnemyController>()
            : null;

        if (boss == null)
        {
            Debug.LogError("강습전 설치 실패: SampleScene의 Enemy를 찾을 수 없습니다.");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        AssaultBattleController battleController =
            GetOrAddComponent<AssaultBattleController>(root);
        battleController.Configure(boss, 180f, 60000, true);
        battleController.ConfigureOperationScores(50, 100, 200, 5000);

        // 전투 관리자 루트에 남아 있는 화면 없는 HUD는 중복 이벤트 구독을 막기 위해 제거한다.
        AssaultBattleHUD headlessHud = root.GetComponent<AssaultBattleHUD>();
        if (headlessHud != null)
            Object.DestroyImmediate(headlessHud);

        AssaultBattleHUD battleHud = FindCanvasBattleHud();
        if (battleHud != null)
            battleHud.Configure(battleController);
        else
            Debug.LogWarning("강습전 HUD를 찾지 못했습니다. ZZZ HUD 데모 캔버스 연결을 확인하세요.");

        Transform triggerTransform = root.transform.Find(TriggerName);
        bool createdTrigger = triggerTransform == null;

        if (createdTrigger)
        {
            GameObject triggerObject = new GameObject(TriggerName);
            triggerTransform = triggerObject.transform;
            triggerTransform.SetParent(root.transform, false);
            triggerTransform.localPosition = new Vector3(0f, 1.5f, -7f);
        }

        GameObject trigger = triggerTransform.gameObject;
        BoxCollider triggerCollider = GetOrAddComponent<BoxCollider>(trigger);
        Rigidbody triggerRigidbody = GetOrAddComponent<Rigidbody>(trigger);
        AssaultBattleStartTrigger startTrigger =
            GetOrAddComponent<AssaultBattleStartTrigger>(trigger);

        triggerCollider.isTrigger = true;
        if (createdTrigger)
        {
            triggerCollider.center = Vector3.zero;
            triggerCollider.size = new Vector3(20f, 3f, 1.5f);
        }

        triggerRigidbody.useGravity = false;
        triggerRigidbody.isKinematic = true;
        startTrigger.Configure(battleController);

        EditorUtility.SetDirty(battleController);
        if (battleHud != null)
            EditorUtility.SetDirty(battleHud);
        EditorUtility.SetDirty(triggerCollider);
        EditorUtility.SetDirty(triggerRigidbody);
        EditorUtility.SetDirty(startTrigger);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("강습전 코어 설치 완료: 트리거, 보스 활성화, 3분 타이머, 피해 점수를 연결했습니다.");
    }

    private static Scene ResolveSampleScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
            return activeScene;

        if (activeScene.IsValid() && activeScene.isDirty)
        {
            Debug.LogError(
                "강습전 설치 중단: 현재 씬에 저장하지 않은 변경이 있습니다. " +
                "저장한 뒤 다시 실행하세요.");
            return default;
        }

        return EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
    }

    private static AssaultBattleHUD FindCanvasBattleHud()
    {
        AssaultBattleHUD[] huds = Object.FindObjectsByType<AssaultBattleHUD>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AssaultBattleHUD hud in huds)
        {
            if (hud != null && hud.GetComponentInParent<Canvas>() != null)
                return hud;
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
