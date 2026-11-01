using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Finite reviewed host group. This does not rewrite or claim original source integrity.</summary>
public static class MapHostCompatibility
{
    [Serializable] public sealed class FileEntry { public string path, sha256; }
    [Serializable] public sealed class Difference { public string path, originalSha256, currentSha256; }
    [Serializable] public sealed class Host
    {
        public int schema;
        public string buildId, implementationDigest, oldContentVersion, newContentVersion, closureSha256, reviewEvidenceSha256, stampSha256;
        public FileEntry[] files; public Difference[] approvedDifferences; public string[] startupTargets;
    }
    public static string LastStrictSourceResult { get; private set; }
    public static int LastStrictDifferenceCount { get; private set; }
    public static string LastCompatibilityResult { get; private set; }
    public static string Sha(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    public static string GroupDigest(FileEntry[] files) => Sha(Encoding.UTF8.GetBytes(string.Join("\n", files.OrderBy(p => p.path, StringComparer.Ordinal).Select(p => p.path + "\t" + p.sha256)) + "\n"));
    public static string DifferenceDigest(IEnumerable<Difference> rows) => Sha(Encoding.UTF8.GetBytes(string.Join("\n", rows.OrderBy(p => p.path, StringComparer.Ordinal).Select(p => p.path + "\t" + p.originalSha256 + "\t" + p.currentSha256)) + "\n"));
    private static bool IsSha(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    private static bool IsPath(string value) => !string.IsNullOrEmpty(value) && value.StartsWith("Assets/", StringComparison.Ordinal) && !value.Contains("\\") && !value.Split('/').Any(p => p == "." || p == ".." || p.Length == 0);
    private static bool SourceInput(string path) => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".asmdef", StringComparison.Ordinal) || path.EndsWith(".rsp", StringComparison.Ordinal);
    private static void ValidateDifferences(IEnumerable<Difference> rows)
    {
        if (rows == null) throw new InvalidOperationException("InvalidApprovedDifferenceSet");
        var differences = rows.ToArray();
        if (differences.Any(p => p == null || !IsPath(p.path) || !IsSha(p.originalSha256) || !IsSha(p.currentSha256) || p.originalSha256 == p.currentSha256) || differences.Select(p => p.path).Distinct(StringComparer.Ordinal).Count() != differences.Length)
            throw new InvalidOperationException("InvalidApprovedDifferenceSet");
    }
    public static void ValidateDifferenceSet(IEnumerable<Difference> expectedRows, IEnumerable<Difference> actualRows)
    {
        ValidateDifferences(expectedRows); ValidateDifferences(actualRows);
        var expected = expectedRows.OrderBy(p => p.path, StringComparer.Ordinal).ToArray();
        var actual = actualRows.OrderBy(p => p.path, StringComparer.Ordinal).ToArray();
        if (expected.Length != actual.Length || expected.Where((p, i) => p.path != actual[i].path || p.originalSha256 != actual[i].originalSha256 || p.currentSha256 != actual[i].currentSha256).Any())
            throw new InvalidOperationException("UnapprovedOriginalManifestDifference");
    }
    public static void ValidateSourceGroup(Host host, string root)
    {
        root = Path.GetFullPath(root);
        foreach (FileEntry entry in host.files)
        {
            string path = Path.GetFullPath(Path.Combine(root, entry.path));
            if (!IsPath(entry.path) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || Sha(File.ReadAllBytes(path)) != entry.sha256)
                throw new InvalidOperationException("ReviewedHostDependencyMismatch: " + entry.path);
        }
        string stamp = Path.Combine(root, "Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs");
        if (!File.Exists(stamp) || Sha(File.ReadAllBytes(stamp)) != host.stampSha256) throw new InvalidOperationException("HostBuildStampMismatch");
        var authored = Directory.GetFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories)
            .Select(p => p.Substring(root.Length + 1).Replace('\\', '/'))
            .Where(p => SourceInput(p) && p != "Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var declared = host.files.Where(p => SourceInput(p.path)).Select(p => p.path).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (!authored.SequenceEqual(declared)) throw new InvalidOperationException("HostSourceInventoryMismatch");
    }
    public static Host ValidateDocument(string json, string profile, string contentVersion)
    {
        Host host = JsonUtility.FromJson<Host>(json);
        if (host != null && host.approvedDifferences != null) ValidateDifferences(host.approvedDifferences);
        if (host == null || host.schema != 2 || host.files == null || host.approvedDifferences == null ||
            host.files.Length == 0 || host.files.Any(p => p == null || !IsPath(p.path) || !IsSha(p.sha256)) ||
            host.files.Select(p => p.path).Distinct(StringComparer.Ordinal).Count() != host.files.Length ||
            !MapHostBuildStamp.IsReviewedCompatibleHost ||
            host.startupTargets == null || !host.startupTargets.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(new[] { "DEFAULT", "I1_PRIVATE" }) ||
            host.reviewEvidenceSha256 != MapHostBuildStamp.ReviewEvidenceSha256 || host.closureSha256 != MapHostBuildStamp.SourceClosureSha256 ||
            DifferenceDigest(host.approvedDifferences) != MapHostBuildStamp.ApprovedDifferencesDigest ||
            host.buildId != MapHostBuildStamp.BuildId || host.implementationDigest != MapHostBuildStamp.ImplementationDigest ||
            GroupDigest(host.files) != MapHostBuildStamp.ImplementationDigest ||
            host.oldContentVersion != MapHostBuildStamp.OldContentVersion || host.newContentVersion != MapHostBuildStamp.NewContentVersion ||
            !IsSha(host.closureSha256) || !IsSha(host.reviewEvidenceSha256) || !IsSha(host.stampSha256))
            throw new InvalidOperationException("UnreviewedHostImplementationGroup");
        ValidateDifferences(host.approvedDifferences);
        if (host.approvedDifferences.Any(p => !host.files.Any(f => f.path == p.path && f.sha256 == p.currentSha256)))
            throw new InvalidOperationException("ApprovedDifferenceDependencyMissing");
        bool old = profile == EcologyRecipeDefinition.MainV1ProfileId && contentVersion == host.oldContentVersion;
        bool next = profile == EcologyRecipeDefinition.VNextI1ProfileId && contentVersion == host.newContentVersion;
        if (!old && !next) throw new InvalidOperationException("UnknownOrMixedHostContentVersion");
        return host;
    }
    public static void Verify(string profile, string contentVersion, IReadOnlyList<Difference> strictDifferences)
    {
#if UNITY_EDITOR
        LastStrictDifferenceCount = strictDifferences.Count;
        LastStrictSourceResult = strictDifferences.Count == 0 ? "ORIGINAL_BYTES_MATCH" : "ORIGINAL_BYTES_DIFFER_" + strictDifferences.Count;
#else
        LastStrictDifferenceCount = -1; LastStrictSourceResult = "NOT_EXECUTED_IN_PLAYER";
#endif
        var source = Resources.Load<TextAsset>("MapVNextI1Host");
        if (source == null)
        {
            if (strictDifferences.Count != 0 || profile == EcologyRecipeDefinition.VNextI1ProfileId)
                throw new InvalidOperationException("ReviewedHostGroupMissing");
            LastCompatibilityResult = "NOT_REQUIRED_ORIGINAL_HOST"; return;
        }
        Host host = ValidateDocument(source.text, profile, contentVersion);
#if UNITY_EDITOR
        ValidateDifferenceSet(host.approvedDifferences, strictDifferences);
        ValidateSourceGroup(host, Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        LastCompatibilityResult = "REVIEWED_GROUP_MATCH_EDITOR_SOURCE_CHECKED";
#else
        // Player has compiled code, not the Editor source tree. Verify the embedded group against the compiled stamp.
        LastCompatibilityResult = "REVIEWED_GROUP_MATCH_PLAYER_EMBEDDED_IDENTITY_ONLY";
#endif
    }
}
