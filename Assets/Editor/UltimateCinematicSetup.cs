using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

public static class UltimateCinematicSetup
{
    private readonly struct Assignment
    {
        public readonly string DataPath;
        public readonly string ClipPath;

        public Assignment(string dataPath, string clipPath)
        {
            DataPath = dataPath;
            ClipPath = clipPath;
        }
    }

    private static readonly Assignment[] Assignments =
    {
        new Assignment(
            "Assets/Characters/Ellen_Joe/Data/Ultimate_Ellen_Joe.asset",
            "Assets/Video/Ultimate/Ellen_Ultimate.mp4"),
        new Assignment(
            "Assets/Characters/Jane_Doe/Data/Ultimate_Jane_Doe.asset",
            "Assets/Video/Ultimate/Jane_Ultimate.mp4"),
        new Assignment(
            "Assets/Characters/Corin/Data/Ultimate_Corin.asset",
            "Assets/Video/Ultimate/Corin_Ultimate.mp4")
    };

    [MenuItem("Tools/Combat/Apply Ultimate Cinematics")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        int appliedCount = 0;
        foreach (Assignment assignment in Assignments)
        {
            UltimateData data = AssetDatabase.LoadAssetAtPath<UltimateData>(assignment.DataPath);
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(assignment.ClipPath);

            if (data == null || clip == null)
            {
                Debug.LogError(
                    $"Ultimate cinematic setup failed. Data={assignment.DataPath}, Clip={assignment.ClipPath}");
                continue;
            }

            if (data.cinematicClip != clip)
            {
                data.cinematicClip = clip;
                EditorUtility.SetDirty(data);
            }

            appliedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Ultimate cinematic setup complete: {appliedCount}/{Assignments.Length}");
    }
}
