using UnityEditor;

/// <summary>
/// 타임스탬프가 불규칙한 강습전 오프닝 원본을 Standalone용 H.264로 한 번 트랜스코딩한다.
/// 원본 MP4는 유지하고 Unity의 임포트 산출물만 정상화해 첫 재생 준비 부하를 줄인다.
/// </summary>
[InitializeOnLoad]
internal static class AssaultOpeningVideoImporter
{
    private const string VideoPath = "Assets/Video/AssaultOpening.mp4";
    private const string AppliedMarker = "AssaultOpening_H264Transcoded_v1";
    private const string MenuPath = "Tools/Assault Battle/Reimport Opening Video";

    static AssaultOpeningVideoImporter()
    {
        EditorApplication.delayCall += ApplyOnce;
    }

    [MenuItem(MenuPath)]
    private static void ReimportFromMenu()
    {
        Apply(true);
    }

    private static void ApplyOnce()
    {
        Apply(false);
    }

    private static void Apply(bool force)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyOnce;
            return;
        }

        VideoClipImporter importer =
            AssetImporter.GetAtPath(VideoPath) as VideoClipImporter;
        if (importer == null || (!force && importer.userData == AppliedMarker))
            return;

        VideoImporterTargetSettings settings = importer.defaultTargetSettings;
        settings.enableTranscoding = true;
        settings.codec = VideoCodec.H264;
        importer.SetTargetSettings("Standalone", settings);
        importer.userData = AppliedMarker;
        importer.SaveAndReimport();
    }
}
