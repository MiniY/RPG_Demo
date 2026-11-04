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
    [Serializable] private sealed class MainFontProbe
    {
        public string project, persistent, slots, legacy, company, product, unity, fontGuid, fontSha256, exception;
        public int characters, glyphs, atlasTextures, missingOldCharacters;
        public bool noStorageOverride, staticAtlas, materialAndAtlasPresent;
    }
    public static void ProbeMainFontAndStorage()
    {
        string output = Environment.GetEnvironmentVariable("RPG_MAIN_FONT_OUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("ExplicitProbeOutputRequired");
        var row = new MainFontProbe(); int exit = 0;
        try
        {
            row.project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!string.Equals(row.project, @"D:\UnityProjects\RPG_Demo", StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlaying)
                throw new InvalidOperationException("MainIdleEditorRequired");
            row.persistent = Application.persistentDataPath; row.slots = TowerRunRuntimeEnvironment.SlotRoot;
            row.legacy = TowerRunRuntimeEnvironment.LegacySavePath; row.company = Application.companyName;
            row.product = Application.productName; row.unity = Application.unityVersion;
            row.noStorageOverride = Path.GetFullPath(row.slots) == Path.GetFullPath(Path.Combine(row.persistent, "tower-run-slots"));
            const string fontPath = "Assets/Font/MyTMP.asset";
            row.fontGuid = AssetDatabase.AssetPathToGUID(fontPath);
            row.fontSha256 = MapHostCompatibility.Sha(File.ReadAllBytes(fontPath));
            var font = AssetDatabase.LoadMainAssetAtPath(fontPath);
            if (font == null || font.GetType().FullName != "TMPro.TMP_FontAsset") throw new InvalidOperationException("BusinessTMPFontMissing");
            var so = new SerializedObject(font); var characters = so.FindProperty("m_CharacterTable");
            row.characters = characters.arraySize; row.glyphs = so.FindProperty("m_GlyphTable").arraySize;
            row.atlasTextures = so.FindProperty("m_AtlasTextures").arraySize;
            row.staticAtlas = so.FindProperty("m_AtlasPopulationMode").intValue == 0;
            row.materialAndAtlasPresent = row.atlasTextures > 0 && so.FindProperty("m_AtlasTextures").GetArrayElementAtIndex(0).objectReferenceValue != null &&
                so.FindProperty("material").objectReferenceValue != null;
            var codes = new HashSet<int>();
            for (int i=0;i<characters.arraySize;i++) codes.Add(characters.GetArrayElementAtIndex(i).FindPropertyRelative("m_Unicode").intValue);
            string expected = Environment.GetEnvironmentVariable("RPG_MAIN_FONT_OLD_CODES");
            foreach (string code in File.ReadAllText(expected).Split(',')) if (!codes.Contains(int.Parse(code))) row.missingOldCharacters++;
            if (!row.noStorageOverride || row.company != "DefaultCompany" || row.product != "RPG_Demo" || row.fontGuid != "18feaf95d90fd2545a36fb4b573dbe0a" ||
                row.fontSha256 != "d369760a46cfd85e4af65dac61fec167bc7548dadbcb3ece7363b09b12ef7503" ||
                row.characters != 388 || row.missingOldCharacters != 0 || !row.staticAtlas || !row.materialAndAtlasPresent)
                throw new InvalidOperationException("MainFontOrStorageProbeFailed");
            var current=AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>("Assets/MapGeneration/MapVNext/I1/Recipe_MyTMPV1.asset");
            current.ResolveTypes(); current.VerifyRetainedSourceFiles(true);
        }
        catch (Exception error) {row.exception=error.ToString();exit=2;}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonUtility.ToJson(row,true)); EditorApplication.Exit(exit);
    }
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
