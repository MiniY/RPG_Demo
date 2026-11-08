using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MapVNext;
using NUnit.Framework;
using UnityEngine;

public sealed class MapGroundConnectivityTests
{
    [Serializable] private sealed class Header
    {
        public int width, height;
        public Vector2Int origin, spawn, exit;
    }

    private static MapData ReadCapturedBase(int seed)
    {
        string root = Environment.GetEnvironmentVariable("RPG_MAP_BASE_FIXTURES");
        Assert.That(root, Is.Not.Null.And.Not.Empty, "Use the pinned recorded input fixture directory.");
        string prefix = Path.Combine(root, "recorded-base-" + seed);
        Header header = JsonUtility.FromJson<Header>(File.ReadAllText(prefix + ".json"));
        MapData map = new MapData(header.width, header.height, header.origin)
        { GenerationSeed = seed, SpawnCell = header.spawn, ExitCell = header.exit };
        foreach (string line in File.ReadLines(prefix + ".csv").Skip(1))
        {
            int[] fields = line.Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            Assert.That(fields[3], Is.Zero, "Captured formal input must be unelevated.");
            map.SetTerrain(new Vector2Int(fields[0], fields[1]), (MapTerrainType)fields[2]);
        }
        return map;
    }

    [TestCase(2068261711, true)]
    [TestCase(1015988834, true)]
    [TestCase(475325548, true)]
    [TestCase(-1632281016, true)]
    [TestCase(-2070448770, false)]
    [TestCase(435220872, false)]
    [TestCase(621643280, false)]
    [TestCase(-617875732, false)]
    [TestCase(-1725208175, false)]
    [TestCase(2142473481, false)]
    [TestCase(-745174857, false)]
    public void RecordedInputsKeepLegacyRejectionAndOnlyConnectedBasesGainNewLayout(int seed, bool connected)
    {
        MapData basis = ReadCapturedBase(seed);
        string before = MapVNextRandomLayout.SemanticHash(basis);
        Assert.Throws<MapPlanningException>(() => MapVNextRandomLayout.GenerateOnTerrain(basis, seed, 5));
        if (connected)
        {
            MapData result = MapVNextRandomLayout.GenerateOnTerrainPreservingGround(basis, seed, 5);
            Assert.That(MapVNextRandomLayout.Reachable(result), Is.EqualTo(Enumerable.Range(0, result.Width * result.Height)
                .Count(index => result.IsWalkable(result.Origin + new Vector2Int(index % result.Width, index / result.Width)))));
            for (int y = 0; y < basis.Height; y++) for (int x = 0; x < basis.Width; x++)
            {
                Vector2Int cell = basis.Origin + new Vector2Int(x, y);
                Assert.That(result.GetCell(cell).terrainType, Is.EqualTo(basis.GetCell(cell).terrainType));
            }
            Assert.That(result.SpawnCell, Is.EqualTo(basis.SpawnCell));
            Assert.That(result.ExitCell, Is.EqualTo(basis.ExitCell));
        }
        else Assert.Throws<MapPlanningException>(() => MapVNextRandomLayout.GenerateOnTerrainPreservingGround(basis, seed, 5));
        Assert.That(MapVNextRandomLayout.SemanticHash(basis), Is.EqualTo(before), "Neither revision mutates the captured basis.");
    }

    [TestCase(0)]
    [TestCase(33)]
    public void InvalidLayoutBudgetIsTheOnlyRejectedInputCondition(int attempts)
    {
        MapData basis = new MapData(64, 64, Vector2Int.zero);
        Assert.That(basis.HasElevation, Is.False);
        Assert.Throws<ArgumentException>(() => MapVNextRandomLayout.GenerateOnTerrainPreservingGround(basis, 42, 5, attempts));
    }

    [Test] public void OldAndNewAssetsSelectExplicitMatchingRevision()
    {
        const string root = "Assets/MapGeneration/MapVNext/I1/";
        MapVNextFormalTerrain oldTerrain = UnityEditor.AssetDatabase.LoadAssetAtPath<MapVNextFormalTerrain>(root + "Terrain.asset");
        MapVNextFormalTerrain newTerrain = UnityEditor.AssetDatabase.LoadAssetAtPath<MapVNextFormalTerrain>(root + "Terrain_GroundConnectivityV1.asset");
        Assert.That(oldTerrain.LayoutRevision, Is.EqualTo(MapVNextFormalRevision.Legacy));
        Assert.That(newTerrain.LayoutRevision, Is.EqualTo(MapVNextFormalRevision.PreserveGroundConnectivity));
        Assert.That(oldTerrain.ImplementationId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ImplementationId));
        Assert.That(newTerrain.ImplementationId, Is.EqualTo(EcologyRecipeDefinition.VNextI1GroundConnectivityImplementationId));
        EcologyRecipeDefinition previous = UnityEditor.AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(root + "Recipe_MyTMPV1.asset");
        EcologyRecipeDefinition current = UnityEditor.AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(root + "Recipe_GroundConnectivityV1.asset");
        Assert.That(previous.ConfigurationDigest, Is.EqualTo(EcologyRecipeDefinition.VNextI1LegacyConfigurationDigest));
        Assert.That(current.ConfigurationDigest, Is.EqualTo(EcologyRecipeDefinition.VNextI1GroundConnectivityConfigurationDigest));
        Assert.That(previous.TerrainGenerator, Is.SameAs(oldTerrain));
        Assert.That(current.TerrainGenerator, Is.SameAs(newTerrain));
        Assert.DoesNotThrow(() => previous.ResolveTypes());
        Assert.DoesNotThrow(() => current.ResolveTypes());
        Assert.That(previous.ContentVersion, Is.EqualTo(MapHostBuildStamp.PreviousCurrentContentVersion));
        Assert.That(current.ContentVersion, Is.EqualTo(MapHostBuildStamp.NewContentVersion));
    }

    [Test] public void CurrentHostHasExactlyFourFiniteContentSlotsAndRejectsSchemaOrStampMixing()
    {
        string original = Resources.Load<TextAsset>("MapVNextI1Host").text;
        foreach (string identity in new[] { MapHostBuildStamp.NewContentVersion, MapHostBuildStamp.PreviousCurrentContentVersion, MapHostBuildStamp.RetainedContentVersion })
            Assert.DoesNotThrow(() => MapHostCompatibility.ValidateDocument(original, EcologyRecipeDefinition.VNextI1ProfileId, identity));
        Assert.DoesNotThrow(() => MapHostCompatibility.ValidateDocument(original, EcologyRecipeDefinition.MainV1ProfileId, MapHostBuildStamp.OldContentVersion));
        MapHostCompatibility.Host host = JsonUtility.FromJson<MapHostCompatibility.Host>(original);
        host.schema = 2;
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDocument(JsonUtility.ToJson(host), EcologyRecipeDefinition.VNextI1ProfileId, MapHostBuildStamp.NewContentVersion));
        host = JsonUtility.FromJson<MapHostCompatibility.Host>(original);
        host.previousCurrentContentVersion = host.newContentVersion;
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDocument(JsonUtility.ToJson(host), EcologyRecipeDefinition.VNextI1ProfileId, MapHostBuildStamp.NewContentVersion));
        host = JsonUtility.FromJson<MapHostCompatibility.Host>(original);
        host.implementationDigest = new string('0', 64);
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDocument(JsonUtility.ToJson(host), EcologyRecipeDefinition.VNextI1ProfileId, MapHostBuildStamp.NewContentVersion));
        Assert.Throws<InvalidOperationException>(() => MapHostCompatibility.ValidateDocument(original, EcologyRecipeDefinition.MainV1ProfileId, MapHostBuildStamp.NewContentVersion));
    }
}
