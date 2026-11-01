using System;
using System.IO;
using NUnit.Framework;

public sealed class MapVNextI1CompatibilityTests
{
    private static string A => new string('a', 64);
    private static string B => new string('b', 64);
    private static MapHostCompatibility.Difference Difference(string path = "Assets/Reviewed.cs", string current = null)
        => new MapHostCompatibility.Difference { path = path, originalSha256 = A, currentSha256 = current ?? B };
    [Test] public void ExactApprovedDifferencesPassAndUnknownHashesOrPathsFail()
    {
        var expected = new[] { Difference() };
        Assert.DoesNotThrow(() => MapHostCompatibility.ValidateDifferenceSet(expected, new[] { Difference() }));
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDifferenceSet(expected, Array.Empty<MapHostCompatibility.Difference>()));
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDifferenceSet(expected, new[] { Difference(current: new string('c', 64)) }));
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDifferenceSet(expected, new[] { Difference("Assets/Other.cs") }));
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDifferenceSet(expected, new[] { Difference(), Difference() }));
    }
    [Test] public void MissingTamperedAndAdditionalAssemblyInputsAreRejected()
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT"), "HostNegative", Guid.NewGuid().ToString("N"));
        string stamp = Path.Combine(root, "Assets/Scripts/MapGeneration/SpatialVersion/MapHostBuildStamp.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(stamp)); File.WriteAllText(stamp, "compiled stamp fixture");
        string dependency = Path.Combine(root, "Assets/Reviewed.cs"); File.WriteAllText(dependency, "reviewed code");
        var host = new MapHostCompatibility.Host {
            stampSha256 = MapHostCompatibility.Sha(File.ReadAllBytes(stamp)),
            files = new[] { new MapHostCompatibility.FileEntry { path = "Assets/Reviewed.cs", sha256 = MapHostCompatibility.Sha(File.ReadAllBytes(dependency)) } }
        };
        Assert.DoesNotThrow(() => MapHostCompatibility.ValidateSourceGroup(host, root));
        File.WriteAllText(dependency, "unknown bytes");
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateSourceGroup(host, root));
        File.WriteAllText(dependency, "reviewed code");
        string extra = Path.Combine(root, "Assets/Unreviewed.asmdef"); File.WriteAllText(extra, "{}");
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateSourceGroup(host, root));
        File.Delete(extra); File.Delete(dependency);
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateSourceGroup(host, root));
    }
}
