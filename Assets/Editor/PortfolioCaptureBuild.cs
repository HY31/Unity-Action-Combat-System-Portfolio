using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 포트폴리오 촬영용 1920x1080 Windows 플레이어를 생성한다.
/// 빌드에만 촬영 설정을 적용하고 프로젝트의 기존 PlayerSettings는 즉시 복원한다.
/// </summary>
[InitializeOnLoad]
internal static class PortfolioCaptureBuild
{
    private const string RequestPath = "Library/PortfolioCaptureBuild.request";
    private const string OutputDirectory = "Builds/PortfolioCapture";
    private const string ExecutablePath =
        OutputDirectory + "/RealTimeAction_Portfolio.exe";
    private const string StatusPath = OutputDirectory + "/build-status.txt";
    private const string MenuPath = "Tools/Portfolio/Build Capture Player (1080p)";

    static PortfolioCaptureBuild()
    {
        EditorApplication.delayCall += TryRunRequestedBuild;
        // 외부 자동화가 요청 파일을 만든 시점이 도메인 재로드 이후여도 현재 에디터가 즉시 감지한다.
        EditorApplication.update += TryRunRequestedBuild;
    }

    [MenuItem(MenuPath)]
    private static void BuildFromMenu()
    {
        BuildCapturePlayer();
    }

    private static void TryRunRequestedBuild()
    {
        if (!File.Exists(RequestPath))
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        File.Delete(RequestPath);
        BuildCapturePlayer();
    }

    public static void BuildCapturePlayer()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        Directory.CreateDirectory(OutputDirectory);
        if (scenes.Length == 0)
        {
            WriteStatus("FAILED\nNo enabled scenes were found in EditorBuildSettings.");
            Debug.LogError("포트폴리오 촬영 빌드 실패: 활성화된 빌드 씬이 없습니다.");
            return;
        }

        int previousWidth = PlayerSettings.defaultScreenWidth;
        int previousHeight = PlayerSettings.defaultScreenHeight;
        bool previousNativeResolution = PlayerSettings.defaultIsNativeResolution;
        bool previousResizableWindow = PlayerSettings.resizableWindow;
        bool previousRunInBackground = PlayerSettings.runInBackground;
        FullScreenMode previousFullscreenMode = PlayerSettings.fullScreenMode;

        try
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            DateTime startedAt = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            double elapsedSeconds = (DateTime.Now - startedAt).TotalSeconds;

            string status =
                $"Result: {summary.result}\n" +
                $"Output: {Path.GetFullPath(ExecutablePath)}\n" +
                $"SizeMB: {summary.totalSize / 1048576d:F1}\n" +
                $"DurationSeconds: {elapsedSeconds:F1}\n" +
                $"Errors: {summary.totalErrors}\n" +
                $"Warnings: {summary.totalWarnings}\n" +
                $"CompletedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            WriteStatus(status);

            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"포트폴리오 촬영 빌드 완료: {ExecutablePath}");
            else
                Debug.LogError($"포트폴리오 촬영 빌드 실패: {summary.result}");
        }
        catch (Exception exception)
        {
            WriteStatus($"FAILED\n{exception}");
            Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.defaultScreenWidth = previousWidth;
            PlayerSettings.defaultScreenHeight = previousHeight;
            PlayerSettings.defaultIsNativeResolution = previousNativeResolution;
            PlayerSettings.resizableWindow = previousResizableWindow;
            PlayerSettings.runInBackground = previousRunInBackground;
            PlayerSettings.fullScreenMode = previousFullscreenMode;
            AssetDatabase.SaveAssets();
        }
    }

    private static void WriteStatus(string content)
    {
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(StatusPath, content);
    }
}
