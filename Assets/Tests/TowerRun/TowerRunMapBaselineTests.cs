using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TowerRunMapBaselineTests
{
    [Test]
    public void SignatureChangesWhenTerrainPlacementOrDecorationChanges()
    {
        MapData map = CreateMap();
        MapPlacementPlan plan = CreatePlan(new Vector2Int(1, 0));
        MapSimpleDecorationData decorations = CreateDecorations("variant-a");
        string baseline = TowerRunMapBaseline.Compute(map, plan, decorations);

        MapData changedTerrain = CreateMap();
        changedTerrain.SetTerrain(Vector2Int.zero, MapTerrainType.Water);
        MapPlacementPlan changedPlan = CreatePlan(new Vector2Int(0, 1));
        MapSimpleDecorationData changedDecorations = CreateDecorations("variant-b");

        Assert.That(baseline, Is.Not.Empty);
        Assert.That(
            TowerRunMapBaseline.Compute(changedTerrain, plan, decorations),
            Is.Not.EqualTo(baseline));
        Assert.That(
            TowerRunMapBaseline.Compute(map, changedPlan, decorations),
            Is.Not.EqualTo(baseline));
        Assert.That(
            TowerRunMapBaseline.Compute(map, plan, changedDecorations),
            Is.Not.EqualTo(baseline));
    }

    [Test]
    public void SignatureIsCanonicalAcrossEquivalentReservationOrder()
    {
        MapData map = CreateMap();
        MapPlacementPlan first = new MapPlacementPlan(Guid.NewGuid());
        MapPlacementPlan second = new MapPlacementPlan(Guid.NewGuid());
        MapPlacementReservation monster = Reservation("monster", "Monster", new Vector2Int(1, 0));
        MapPlacementReservation merchant = Reservation("merchant", "Merchant", new Vector2Int(0, 1));

        Assert.That(first.TryAdd(monster, out _), Is.True);
        Assert.That(first.TryAdd(merchant, out _), Is.True);
        Assert.That(second.TryAdd(merchant, out _), Is.True);
        Assert.That(second.TryAdd(monster, out _), Is.True);

        MapSimpleDecorationData decorations = CreateDecorations("variant-a");
        Assert.That(
            TowerRunMapBaseline.Compute(map, first, decorations),
            Is.EqualTo(TowerRunMapBaseline.Compute(map, second, decorations)));
    }

    [Test]
    public void MissingStaticOutputCannotProduceTrustedSignature()
    {
        MapData map = CreateMap();
        MapPlacementPlan plan = CreatePlan(new Vector2Int(1, 0));
        MapSimpleDecorationData decorations = CreateDecorations("variant-a");

        Assert.That(TowerRunMapBaseline.Compute(null, plan, decorations), Is.Empty);
        Assert.That(TowerRunMapBaseline.Compute(map, null, decorations), Is.Empty);
        Assert.That(TowerRunMapBaseline.Compute(map, plan, null), Is.Empty);
    }

    private static MapData CreateMap()
    {
        MapData map = new MapData(2, 2, Vector2Int.zero)
        {
            SpawnCell = Vector2Int.zero,
            ExitCell = Vector2Int.one
        };
        map.SetTerrain(Vector2Int.zero, MapTerrainType.Path);
        return map;
    }

    private static MapPlacementPlan CreatePlan(Vector2Int monsterCell)
    {
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        Assert.That(
            plan.TryAdd(Reservation("monster", "Monster", monsterCell), out _),
            Is.True);
        return plan;
    }

    private static MapPlacementReservation Reservation(
        string logicalId,
        string role,
        Vector2Int cell)
    {
        return new MapPlacementReservation(
            logicalId,
            role,
            cell,
            new[] { cell },
            MapPlacementOwnership.GenerationScoped);
    }

    private static MapSimpleDecorationData CreateDecorations(string variantId)
    {
        MapSimpleDecorationData result = new MapSimpleDecorationData();
        FieldInfo placementsField = typeof(MapSimpleDecorationData).GetField(
            "placements",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(placementsField, Is.Not.Null);
        IList placements = (IList)placementsField.GetValue(result);
        placements.Add(new MapSimpleDecorationPlacement(
            Vector2Int.one,
            MapSimpleDecorationType.Bush,
            variantId));
        return result;
    }
}
