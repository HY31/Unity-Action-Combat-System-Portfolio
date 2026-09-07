using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 포트폴리오 촬영용 1920x1080 Windows 플레이어를 생성한다.
/// 빌드에만 촬영 설정을 적용하고 프로젝트의 기존 PlayerSettings는 즉시 복원한다.
/// </summary>
[InitializeOnLoad]
internal static class PortfolioCaptureBuild
{
    private const string RequestPath = "Library/PortfolioCaptureBuild.request";
    private const string FinalRequestPath = "Library/PortfolioFinalBuild.request";
    private const string FinalStatusPath = "Library/PortfolioFinalBuild.status.txt";
    private const string OutputDirectory = "Builds/PortfolioCapture";
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
        bool final = File.Exists(FinalRequestPath);
        if (!final && !File.Exists(RequestPath))
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        File.Delete(final ? FinalRequestPath : RequestPath);
        if (final) BuildFinalPlayer();
        else BuildCapturePlayer();
    }

    public static void BuildCapturePlayer()
    {
        BuildPlayer(OutputDirectory, false);
    }

    [MenuItem("Tools/Portfolio/Build Final Player (1080p)")]
    public static void BuildFinalPlayer()
    {
        BuildPlayer("Builds/Final_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), true);
    }

    private static void BuildPlayer(string outputDirectory, bool final)
    {
        string executablePath = outputDirectory + "/RealTimeAction_Portfolio.exe";
        void WriteBuildStatus(string content)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputDirectory + "/build-status.txt", content);
            if (final) File.WriteAllText(FinalStatusPath, content);
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            .Select(scene => scene.path)
            .ToArray();

        Directory.CreateDirectory(outputDirectory);
        if (scenes.Length == 0)
        {
            WriteBuildStatus("FAILED\nNo enabled scenes were found in EditorBuildSettings.");
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
            WriteBuildStatus("BUILDING\nOutput: " + Path.GetFullPath(executablePath) +
                "\nStartedAt: " + DateTime.Now.ToString("O"));
            if (final)
            {
                // Save only loaded build scenes, not unrelated open scenes.
                string backupDirectory = Path.GetFullPath("../.codex-temp/final-build-scene-backups/" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || !scene.isDirty || !scenes.Contains(scene.path)) continue;
                    Directory.CreateDirectory(backupDirectory);
                    File.Copy(scene.path, Path.Combine(backupDirectory, Path.GetFileName(scene.path)));
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new IOException("Could not save build scene: " + scene.path);
                }
                AssetDatabase.SaveAssets();
            }
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            DateTime startedAt = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            double elapsedSeconds = (DateTime.Now - startedAt).TotalSeconds;

            string status =
                $"Result: {summary.result}\n" +
                $"Output: {Path.GetFullPath(executablePath)}\n" +
                $"SizeMB: {summary.totalSize / 1048576d:F1}\n" +
                $"DurationSeconds: {elapsedSeconds:F1}\n" +
                $"Errors: {summary.totalErrors}\n" +
                $"Warnings: {summary.totalWarnings}\n" +
                $"CompletedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            string messages = string.Join("\n", report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Warning || message.type == LogType.Error ||
                    message.type == LogType.Exception).Select(message => message.type + ": " + message.content));
            File.WriteAllText(outputDirectory + "/build-messages.txt", messages);
            WriteBuildStatus(status);

            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"포트폴리오 빌드 완료: {executablePath}");
            else
                Debug.LogError($"포트폴리오 촬영 빌드 실패: {summary.result}");
        }
        catch (Exception exception)
        {
            WriteBuildStatus($"FAILED\n{exception}");
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

}
