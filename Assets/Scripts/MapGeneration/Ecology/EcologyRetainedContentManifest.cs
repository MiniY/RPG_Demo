using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Baked, finite asset dependencies. Source verification runs in Editor/build; Players use the verified direct references.</summary>
public sealed class EcologyRetainedContentManifest
{
    [Serializable] private sealed class Entry { public string path, sha256; }
    [Serializable] private sealed class TypeEntry { public string typeId, prefabGuid, prefabFileId; public int terrains, weight; public bool boss; }
    [Serializable] private sealed class Document { public int schema; public string profile, configurationDigest; public Entry[] files; public TypeEntry[] catalog; }
    private readonly Document document;
    public EcologyRetainedContentManifest(TextAsset source, string profile, string configuration, EcologyEcotypeDefinition[] types)
    {
        if (source == null) throw new InvalidOperationException("RetainedContentManifestMissing");
        string text = source.text;
        document = JsonUtility.FromJson<Document>(text);
        if (document == null || document.schema != 1 || document.profile != profile || document.configurationDigest != configuration ||
            document.catalog == null || document.files == null || document.files.Length == 0 || document.catalog.Length != types.Length)
            throw new InvalidOperationException("RetainedContentManifestMismatch");
        var expected = document.catalog.OrderBy(t => t.typeId, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < types.Length; i++)
        {
            var actual = types[i]; var row = expected[i];
            if (row.typeId != actual.TypeId || row.terrains != (int)actual.Terrains || row.weight != actual.Weight || row.boss != actual.IsBoss)
                throw new InvalidOperationException("RetainedCatalogMismatch: " + actual.TypeId);
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(actual.Prefab)) != row.prefabGuid)
                throw new InvalidOperationException("RetainedPrefabMappingMismatch: " + actual.TypeId);
#endif
        }
        using (SHA256 sha = SHA256.Create())
            Identity = "content-r1:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    public string Identity { get; }
#if UNITY_EDITOR
    private static readonly Dictionary<string, (long bytes, long ticks, string digest)> sourceCache =
        new Dictionary<string, (long, long, string)>(StringComparer.OrdinalIgnoreCase);
#endif
    public void VerifySourceFiles(bool force)
    {
#if UNITY_EDITOR
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (Entry entry in document.files)
        {
            if (entry == null || string.IsNullOrEmpty(entry.path) || string.IsNullOrEmpty(entry.sha256))
                throw new InvalidOperationException("RetainedDependencyRecordInvalid");
            string path = Path.GetFullPath(Path.Combine(root, entry.path));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("RetainedDependencyPathInvalid");
            var info = new FileInfo(path);
            if (!info.Exists) throw new InvalidOperationException("RetainedDependencyMissing: " + entry.path);
            if (force || !sourceCache.TryGetValue(path, out var cached) || cached.bytes != info.Length || cached.ticks != info.LastWriteTimeUtc.Ticks)
            {
                string digest;
                using (var stream = File.OpenRead(path)) using (SHA256 sha = SHA256.Create())
                    digest = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                cached = (info.Length, info.LastWriteTimeUtc.Ticks, digest); sourceCache[path] = cached;
            }
            if (!string.Equals(cached.digest, entry.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("RetainedContentMismatch: " + entry.path);
        }
#endif
    }
}
