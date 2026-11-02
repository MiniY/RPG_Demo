using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in importer regression probe. It never saves, renames or edits an asset.</summary>
public static class AssetNameWarningAudit
{
    private const string Target = "Assets/Resources/Ecology/RemediationV3/Recipe.asset";
    private static readonly string[] Paths =
    {
        "Assets/Resources/Ecology/UnitsStableCandidateV2/TerrainStrategy.asset",
        "Assets/Resources/Ecology/SurfaceV4/Recipe.asset",
        "Assets/Resources/Ecology/SurfaceV4MainV1/Recipe.asset",
        Target
    };

    [Serializable] private sealed class ImportRow
    {
        public string path, guid, type, serializedName, loadedName;
        public int round;
        public bool nameMatches;
    }

    [Serializable] private sealed class Check
    {
        public string name;
        public bool passed;
    }

    [Serializable] private sealed class Report
    {
        public string phase, unity, mainProfile, mainContent, r3Profile, r3Configuration, exception;
        public int playerTagEntries;
        public List<ImportRow> imports = new List<ImportRow>();
        public List<Check> checks = new List<Check>();
    }

    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("RPG_DEMO_ASSET_NAME_AUDIT_OUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("An explicit audit output path is required.");

        var report = new Report
        {
            phase = Environment.GetEnvironmentVariable("RPG_DEMO_ASSET_NAME_AUDIT_PHASE"),
            unity = Application.unityVersion
        };
        int exitCode;
        try
        {
            for (int round = 1; round <= 2; round++)
            {
                foreach (string path in Paths)
                {
                    Debug.Log($"[AssetNameWarningAudit] BEGIN round={round} path={path}");
                    AssetDatabase.ImportAsset(path,
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    string serializedName = Regex.Match(File.ReadAllText(path),
                        @"(?m)^  m_Name: (.*)$").Groups[1].Value.TrimEnd('\r');
                    report.imports.Add(new ImportRow
                    {
                        path = path,
                        round = round,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        type = asset == null ? null : asset.GetType().FullName,
                        serializedName = serializedName,
                        loadedName = asset == null ? null : asset.name,
                        nameMatches = serializedName == Path.GetFileNameWithoutExtension(path)
                    });
                    Debug.Log($"[AssetNameWarningAudit] END round={round} path={path}");
                }
            }

            Add(report, "AllEightImportsLoad", report.imports.Count == 8 &&
                report.imports.All(row => !string.IsNullOrEmpty(row.type)));
            Add(report, "RepeatedR3NameMatches", report.imports.Where(row => row.path == Target)
                .All(row => row.nameMatches));

            var r3 = Resources.Load<EcologyRecipeDefinition>(
                EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.RemediationProfileId));
            report.r3Profile = r3.DeclaredProfileId;
            report.r3Configuration = r3.ConfigurationDigest;
            Add(report, "R3ResourcePathAndIdentity", report.r3Profile == EcologyRecipeDefinition.RemediationProfileId &&
                report.r3Configuration == "e5c61979fd4ac982d3613e97ef60fce4e8cd7fe721388bd5bdc8ca93a861ad5f");
            bool archiveRejected = false;
            try { r3.ResolveTypes(); }
            catch (InvalidOperationException error)
            {
                archiveRejected = error.Message == EcologyRecipeDefinition.ArchiveOnlyMessage(report.r3Profile);
            }
            Add(report, "R3ArchivePolicyUnchanged", archiveRejected);

            var main = Resources.Load<EcologyRecipeDefinition>(
                EcologyRecipeDefinition.ResourcePathFor(EcologyRecipeDefinition.MainV1ProfileId));
            Add(report, "MainCatalogStillTwelve", main.ResolveTypes().Length == 12);
            main.VerifyRetainedSourceFiles(true);
            report.mainProfile = main.DeclaredProfileId;
            report.mainContent = main.ContentVersion;
            Add(report, "MainProfileAndFrozenContent", report.mainProfile == EcologyRecipeDefinition.MainV1ProfileId &&
                report.mainContent == "content-r1:c55fb3b4b0362e8bb59e84f4624fbb22c2f9ef96f3dcd5f61481a92dab9c9802");
            Add(report, "TerrainImplementationUnchanged", main.TerrainGenerator.ImplementationId == "terrain-float32-retained-v1");

            report.playerTagEntries = UnityEditorInternal.InternalEditorUtility.tags.Count(tag => tag == "Player");
            var probe = new GameObject("AssetNameWarningAudit_TagProbe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                probe.tag = "Player";
                Add(report, "PlayerTagAssignAndCompare", probe.CompareTag("Player"));
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            exitCode = report.checks.All(check => check.passed) ? 0 : 2;
        }
        catch (Exception error)
        {
            report.exception = error.ToString();
            exitCode = 3;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log($"[AssetNameWarningAudit] COMPLETE phase={report.phase} exit={exitCode} output={output}");
        EditorApplication.Exit(exitCode);
    }

    private static void Add(Report report, string name, bool passed)
    {
        report.checks.Add(new Check { name = name, passed = passed });
    }
}
