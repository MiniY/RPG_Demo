using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TinySwordsEnemyPackBuild
{
    [Serializable] sealed class Result
    {
        public string status, scene, executable, unity, graphicsDevice, buildSettingsBefore, buildSettingsAfter;
        public int errors, warnings;
        public ulong bytes;
        public string[] packedAssets, runtimeScripts;
    }
    static string Arg(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length-1; i++) if (args[i] == key) return args[i+1];
        throw new InvalidOperationException("Missing " + key);
    }
    static string Digest(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    public static void Build()
    {
        var result = new Result { status = "FAIL", unity = Application.unityVersion,
            scene = TinySwordsEnemyPackAuthoring.PreviewScene, graphicsDevice = SystemInfo.graphicsDeviceName };
        string reportPath = Arg("-enemyBuildReport");
        try
        {
            // This adapter only builds the minimal recovered closure, never the primary game.
            if (!File.Exists("enemy-recovery-project.json") || Directory.Exists("Assets/Scripts"))
                throw new InvalidOperationException("Build requires the isolated recovery project without game startup scripts");
            result.runtimeScripts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Editor/")).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (result.runtimeScripts.Length != 1 || !result.runtimeScripts[0].EndsWith("TinySwordsEnemyPackPreview.cs"))
                throw new InvalidOperationException("Unexpected runtime code in preview closure");
            const string settings = "ProjectSettings/EditorBuildSettings.asset";
            result.buildSettingsBefore = Digest(settings);
            PlayerSettings.companyName = "CodexEnemyPreview";
            PlayerSettings.productName = "TinySwordsEnemyPackPreview";
            string root = Path.GetFullPath(Arg("-enemyBuildRoot"));
            Directory.CreateDirectory(root);
            result.executable = Path.Combine(root, "TinySwordsEnemyPackPreview.exe");
            if (File.Exists(result.executable)) throw new InvalidOperationException("Build output already exists");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { result.scene }, locationPathName = result.executable,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.DetailedBuildReport
            });
            result.buildSettingsAfter = Digest(settings);
            result.errors = (int)report.summary.totalErrors;
            result.warnings = (int)report.summary.totalWarnings;
            result.bytes = report.summary.totalSize;
            result.packedAssets = report.packedAssets.SelectMany(p => p.contents).Select(p => p.sourceAssetPath)
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (report.summary.result != BuildResult.Succeeded || result.errors != 0 || result.warnings != 0 || result.buildSettingsBefore != result.buildSettingsAfter)
                throw new InvalidOperationException("Build/scene-list validation failed: " + report.summary.result);
            result.status = "PASS";
        }
        catch (Exception error) { Debug.LogError(error); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
        File.WriteAllText(reportPath, JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.status == "PASS" ? 0 : 2);
    }
}
