#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

public sealed class EcologyPlanningDataContractTests
{
    [Test]
    public void ConstructiveCapacityReturnsAnActualSpacedCompatibleGroupInOneComponent()
    {
        Type type = typeof(MapData).Assembly.GetType("EcologyConstructiveCapacity");
        Assert.That(type, Is.Not.Null, "Approved capacity is an actual compatible placement construction, not a count of legal points.");
        MethodInfo build = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        Assert.That(build, Is.Not.Null);
        var legal = new bool[9]; var edges = new byte[9]; var ranks = new ulong[9];
        for (int n = 0; n < 9; n++) { legal[n] = true; ranks[n] = (ulong)n; edges[n] = (byte)((n < 8 ? 1 : 0) | (n > 0 ? 4 : 0)); }
        var graph = new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 9, 1, "explicit-fixture", "fixture-foot", legal, edges);
        Func<int, int, bool> separated = (a, b) => !((a == 0 && b == 3) || (a == 3 && b == 0));
        object result = build.Invoke(null, new object[] { graph, new[] { 8, 7, 6, 5, 4, 3, 2, 1, 0 }, new[] { 4 }, ranks, 3, separated });
        var selected = (IReadOnlyList<int>)result.GetType().GetProperty("PlacementNodes").GetValue(result);
        Assert.That(selected, Is.EqualTo(new[] { 0, 5, 8 }), "Three legal points can still conflict; blocked reservations and actual pair clearance both apply.");
        object reordered = build.Invoke(null, new object[] { graph, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new[] { 4 }, ranks, 3, separated });
        Assert.That(reordered.GetType().GetProperty("PlacementNodes").GetValue(reordered), Is.EqualTo(selected));
        Assert.That(selected, Is.Not.InstanceOf<int[]>());
        edges[4] = 4; edges[5] = 1;
        var split = new EcologyNavigationGraph(graph.GenerationId, Vector2Int.zero, 9, 1, "explicit-fixture", "fixture-foot", legal, edges);
        object component = build.Invoke(null, new object[] { split, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, Array.Empty<int>(), ranks, 3, new Func<int, int, bool>((a, b) => true) });
        var componentNodes = (IReadOnlyList<int>)component.GetType().GetProperty("ComponentNodes").GetValue(component);
        Assert.That(componentNodes, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }), "Disconnected legal islands cannot be summed as one home capacity.");
    }

    [UnityTest]
    public IEnumerator OrdinaryBudgetKeepsGroupsAndAllocatesByRemainingDemand()
    {
        yield return null;
        int[] assigned = Allocate(2, 13, new[] { 11, 22, 33 },
            new[] { 4, 6, 10 }, new[] { 4, 6, 10 }, new ulong[] { 3, 2, 1 });
        Assert.That(assigned, Is.EqualTo(new[] { 3, 4, 6 }),
            "Approved design section 5.1 worked example, not an implementation-derived oracle.");
    }

    private static int[] Allocate(int minimum, int budget, int[] ids, int[] capacities,
        int[] desired, ulong[] scores)
    {
        Type type = typeof(MapData).Assembly.GetType("EcologyPopulationBudget");
        Assert.That(type, Is.Not.Null, "The approved pure population-allocation seam must exist.");
        MethodInfo method = type.GetMethod("TryAllocate", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        object[] arguments = { minimum, budget, ids, capacities, desired, scores, null, null };
        Assert.That((bool)method.Invoke(null, arguments), Is.True, arguments[7] as string);
        return (int[])arguments[6];
    }

    [TestCase(14, 3, 4, 7)]
    [TestCase(30, 4, 6, 10)]
    [TestCase(6, 2, 2, 2)]
    public void WorkedBudgetBoundariesDoNotLoseSlotsOrOverfillDemand(int budget, int a, int b, int c)
    {
        Assert.That(Allocate(2, budget, new[] { 11, 22, 33 }, new[] { 4, 6, 10 },
            new[] { 4, 6, 10 }, new ulong[] { 3, 2, 1 }), Is.EqualTo(new[] { a, b, c }));
    }

    [Test]
    public void EqualRemaindersUseStableScoreThenRegionIdAndIgnoreListOrder()
    {
        Assert.That(Allocate(2, 7, new[] { 30, 10, 20 }, new[] { 3, 3, 3 },
            new[] { 3, 3, 3 }, new ulong[] { 5, 5, 8 }), Is.EqualTo(new[] { 2, 3, 2 }));
        Assert.That(Allocate(2, 7, new[] { 20, 30, 10 }, new[] { 3, 3, 3 },
            new[] { 3, 3, 3 }, new ulong[] { 8, 5, 5 }), Is.EqualTo(new[] { 2, 2, 3 }));
    }

    [Test]
    public void ZeroRemainingDemandLeavesUnusedBudgetAndCannotAliasInputs()
    {
        int[] desired = { 2, 2 };
        int[] actual = Allocate(2, 50, new[] { 1, 2 }, new[] { 5, 5 }, desired, new ulong[] { 0, 0 });
        Assert.That(actual, Is.EqualTo(new[] { 2, 2 }));
        actual[0] = 999;
        Assert.That(desired, Is.EqualTo(new[] { 2, 2 }));
    }

    [TestCase(2, 9, 1, 3, "InsufficientConstructiveCapacity")]
    [TestCase(2, 9, 3, 1, "InvalidRecipe")]
    [TestCase(2, 1, 3, 3, "InsufficientGroupBudget")]
    public void InvalidGroupCannotBeReportedAsAOneMonsterSuccess(int minimum, int budget,
        int capacity, int desired, string expectedFailure)
    {
        Type type = typeof(MapData).Assembly.GetType("EcologyPopulationBudget");
        Assert.That(type, Is.Not.Null);
        object[] args = { minimum, budget, new[] { 1 }, new[] { capacity }, new[] { desired },
            new ulong[] { 0 }, null, null };
        Assert.That((bool)type.GetMethod("TryAllocate").Invoke(null, args), Is.False);
        Assert.That(args[6], Is.Null);
        Assert.That(args[7], Is.EqualTo(expectedFailure));
    }

    [Test]
    public void DensityProductOverflowIsAnExplicitFailureBeforeApplyingCaps()
    {
        Type type = typeof(MapData).Assembly.GetType("EcologyPopulationBudget");
        MethodInfo method = type.GetMethod("TryComputeDemand", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "The approved checked integer-density boundary is missing.");
        object[] args = { long.MaxValue, 2, 1, 2, 5, 5, 0, null };
        Assert.That((bool)method.Invoke(null, args), Is.False);
        Assert.That(args[7], Is.EqualTo("NumericRangeExceeded"));
    }

    [Test]
    public void FrozenLayoutCannotAliasBuilderInputsOrExposeMutableCollections()
    {
        Type builderType = typeof(MapData).Assembly.GetType("MapEcologyLayout+Builder");
        Assert.That(builderType, Is.Not.Null, "The approved immutable layout boundary is missing.");
        Guid generation = Guid.NewGuid();
        object builder = Activator.CreateInstance(builderType, generation, new Vector2Int(-4, 7),
            8, 3, "recipe-v1-content", "candidate-geometry-content", "feet-v1", 0);
        int[] ordinaryNodes = { 8, 9, 10, 11 };
        Assert.That((bool)builderType.GetMethod("TryAddRegion").Invoke(builder,
            new object[] { 1, "MerchantSafety", "", new[] { 0 }, new[] { 1 }, 0, 0 }), Is.True);
        Assert.That((bool)builderType.GetMethod("TryAddRegion").Invoke(builder,
            new object[] { 2, "RestSafety", "", new[] { 23 }, new[] { 22 }, 0, 0 }), Is.True);
        Assert.That((bool)builderType.GetMethod("TryAddRegion").Invoke(builder,
            new object[] { 3, "Ordinary", "torch.blue", ordinaryNodes, new int[0], 4, 2 }), Is.True);
        Assert.That((bool)builderType.GetMethod("TryAddResident").Invoke(builder,
            new object[] { "region.3.resident.001", 3, "torch.blue", 8 }), Is.True);
        Assert.That((bool)builderType.GetMethod("TryAddResident").Invoke(builder,
            new object[] { "region.3.resident.002", 3, "torch.blue", 9 }), Is.True);
        object layout = builderType.GetMethod("Freeze").Invoke(builder, null);
        ordinaryNodes[0] = 999;
        Type layoutType = layout.GetType();
        var canonical = (IReadOnlyList<int>)layoutType.GetProperty("RegionByNode").GetValue(layout);
        Assert.That(canonical[8], Is.EqualTo(3));
        Assert.That(canonical[0], Is.EqualTo(1));
        Assert.That(canonical[1], Is.EqualTo(1));
        Assert.That(canonical[22], Is.EqualTo(2));
        Assert.That(canonical[4], Is.EqualTo(0), "Neutral has neither a home nor safety protection.");
        Assert.That(canonical is int[], Is.False);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)canonical)[8] = 999);
        Assert.That(layoutType.GetProperty("GenerationId").GetValue(layout), Is.EqualTo(generation));
        Assert.That((bool)builderType.GetMethod("TryAddResident").Invoke(builder,
            new object[] { "late", 3, "torch.blue", 10 }), Is.False);
        Assert.That(builderType.GetMethod("Freeze").Invoke(builder, null), Is.SameAs(layout));
        Assert.That(layoutType.GetProperty("Signature").GetValue(layout) as string, Has.Length.EqualTo(64));
    }

    [Test]
    public void EcologyPlanFreezeRejectsWritesAndSeversPreviouslyEscapedReservationAliases()
    {
        Guid generation = Guid.NewGuid();
        var builder = new MapEcologyLayout.Builder(generation, new Vector2Int(-4, 7), 8, 3,
            "recipe-v1", "geometry-v1", "feet-v1", 0);
        Assert.That(builder.TryAddRegion(1, "MerchantSafety", "", new[] { 0 }, new[] { 1 }, 0, 0));
        Assert.That(builder.TryAddRegion(2, "RestSafety", "", new[] { 23 }, new[] { 22 }, 0, 0));
        Assert.That(builder.TryAddRegion(3, "Ordinary", "torch.blue", new[] { 8, 9, 10, 11 }, new int[0], 4, 2));
        Assert.That(builder.TryAddResident("r.1", 3, "torch.blue", 8));
        Assert.That(builder.TryAddResident("r.2", 3, "torch.blue", 9));
        MapEcologyLayout layout = builder.Freeze();
        var plan = new MapPlacementPlan(generation);
        var first = new MapPlacementReservation("r.1", "Monster", layout.CellAt(8),
            new[] { layout.CellAt(8) }, MapPlacementOwnership.GenerationScoped);
        Assert.That(plan.TryAdd(first, out _));
        Assert.That(plan.TryAdd(new MapPlacementReservation("r.2", "Monster", layout.CellAt(9),
            new[] { layout.CellAt(9) }, MapPlacementOwnership.GenerationScoped), out _));
        var oldList = (IList<MapPlacementReservation>)plan.Reservations;
        var oldCells = (IList<Vector2Int>)first.OccupiedCells;
        MethodInfo attach = typeof(MapPlacementPlan).GetMethod("TryAttachEcology");
        Assert.That(attach, Is.Not.Null, "Accepted ecology must freeze the existing plan, not publish another owner.");
        object[] args = { layout, null };
        Assert.That((bool)attach.Invoke(plan, args), Is.True, args[1] as string);
        oldList.Clear(); oldCells[0] = new Vector2Int(999, 999);
        Assert.That(plan.ReservationCount, Is.EqualTo(2));
        Assert.That(plan.IsOccupied(layout.CellAt(8)), Is.True);
        Assert.That(plan.Reservations[0].OccupiedCells[0], Is.EqualTo(layout.CellAt(8)));
        Assert.Throws<NotSupportedException>(() => ((IList<MapPlacementReservation>)plan.Reservations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Vector2Int>)plan.Reservations[0].OccupiedCells)[0] = Vector2Int.zero);
        Assert.That(plan.TryAdd(new MapPlacementReservation("late", "Monster", layout.CellAt(10),
            new[] { layout.CellAt(10) }, MapPlacementOwnership.GenerationScoped), out _), Is.False);
        Assert.That(typeof(MapPlacementPlan).GetProperty("Ecology").GetValue(plan), Is.SameAs(layout));
        var stale = new MapPlacementPlan(Guid.NewGuid());
        Assert.That((bool)attach.Invoke(stale, new object[] { layout, null }), Is.False);
        var legacy = new MapPlacementPlan(Guid.NewGuid());
        Assert.That(legacy.TryAdd(new MapPlacementReservation("legacy", "Monster", Vector2Int.zero,
            new[] { Vector2Int.zero }, MapPlacementOwnership.GenerationScoped), out _), Is.True);
    }

    [Test]
    public void UnitEdgeMultiSourceGrowthKeepsConnectedParentsAndStableTieAcrossSeedOrder()
    {
        Type graphType = typeof(MapData).Assembly.GetType("EcologyNavigationGraph");
        Type partitioner = typeof(MapData).Assembly.GetType("EcologyRegionPartitioner");
        Assert.That(graphType, Is.Not.Null, "The approved frozen legal-edge graph boundary is missing.");
        Assert.That(partitioner, Is.Not.Null);
        Guid generation = Guid.NewGuid();
        bool[] legal = { true, true, true, true, true, true, true };
        byte[] edges = { 1, 5, 5, 5, 5, 5, 4 }; // Right=1, Up=2, Left=4, Down=8.
        object graph = Activator.CreateInstance(graphType, generation, new Vector2Int(-7, 3), 7, 1,
            "explicit-fixture-projection-not-a-production-clearance-proof", "fixture-foot-v1", legal, edges);
        legal[3] = false; edges[0] = 0;
        Array graphs = Array.CreateInstance(graphType, 2); graphs.SetValue(graph, 0); graphs.SetValue(graph, 1);
        MethodInfo build = partitioner.GetMethod("Build");
        object first = build.Invoke(null, new object[] { graphs, new[] { 0, 6 }, new[] { 10, 20 }, new ulong[] { 2, 1 } });
        object second = build.Invoke(null, new object[] { graphs, new[] { 6, 0 }, new[] { 20, 10 }, new ulong[] { 1, 2 } });
        Type resultType = first.GetType();
        var owners = (IReadOnlyList<int>)resultType.GetProperty("RegionByNode").GetValue(first);
        var distances = (IReadOnlyList<int>)resultType.GetProperty("Distances").GetValue(first);
        var parents = (IReadOnlyList<int>)resultType.GetProperty("Parents").GetValue(first);
        Assert.That(owners, Is.EqualTo(new[] { 10, 10, 10, 20, 20, 20, 20 }));
        Assert.That(distances, Is.EqualTo(new[] { 0, 1, 2, 3, 2, 1, 0 }));
        Assert.That(parents, Is.EqualTo(new[] { -1, 0, 1, 4, 5, 6, -1 }));
        Assert.That(resultType.GetProperty("RegionByNode").GetValue(second), Is.EqualTo(owners));
        Assert.Throws<NotSupportedException>(() => ((IList<int>)owners)[0] = 999);
    }

    [Test]
    public void ReturnFieldPropagatesOnReverseEdgesAndEveryNextStepIsForwardLegal()
    {
        var graph = new EcologyNavigationGraph(Guid.NewGuid(), new Vector2Int(-5, 10), 5, 1,
            "explicit-directed-fixture", "fixture-foot-v1", new[] { true, true, true, true, true },
            new byte[] { 1, 1, 0, 1, 4 }); // 0->1->2; 3<->4 is disconnected.
        MethodInfo build = typeof(EcologyNavigationGraph).GetMethod("BuildReturnField");
        Assert.That(build, Is.Not.Null, "The approved reverse-edge home field boundary is missing.");
        object result = build.Invoke(graph, new object[] { new[] { 2 } });
        Type type = result.GetType();
        var distances = (IReadOnlyList<int>)type.GetProperty("Distances").GetValue(result);
        var next = (IReadOnlyList<int>)type.GetProperty("Next").GetValue(result);
        Assert.That(distances, Is.EqualTo(new[] { 2, 1, 0, -1, -1 }));
        Assert.That(next, Is.EqualTo(new[] { 1, 2, -1, -1, -1 }));
        Assert.That(graph.CanMove(0, next[0]) && graph.CanMove(1, next[1]), Is.True);
        Assert.That(graph.CanMove(2, 1), Is.False, "The test must distinguish forward and reverse edges.");
    }

    [UnityTest]
    public IEnumerator FailedMotionConfigurationReleasesOnlyItsOwnRetiredScope()
    {
        var actor = new GameObject("Owned partial motion setup");
        var first = new GameObject("Owned expected scope").AddComponent<EcologyDamageBarrier>();
        var other = new GameObject("Owned wrong scope").AddComponent<EcologyDamageBarrier>();
        try
        {
            actor.AddComponent<Rigidbody2D>();
            var combat = actor.AddComponent<EcologyCombatBinding>();
            var motion = actor.AddComponent<EcologyMotionBody>();
            combat.Bind(first, 1, 1, () => true);
            Assert.Throws<ArgumentException>(() => motion.Configure(null, ~0, .01f));
            Assert.Throws<InvalidOperationException>(() => motion.ReleaseRetiredScopeForLegacy(first), "An open scope cannot be released.");
            first.enabled = false; other.enabled = false;
            Assert.Throws<InvalidOperationException>(() => motion.ReleaseRetiredScopeForLegacy(other), "A different retired owner cannot claim the partial setup.");
            motion.ReleaseRetiredScopeForLegacy(first);
            Assert.That(motion.OwnsMotionRoute, Is.False);
            combat.ReleaseRetiredScopeForLegacy(first);
        }
        finally
        {
            UnityEngine.Object.Destroy(actor); UnityEngine.Object.Destroy(first.gameObject); UnityEngine.Object.Destroy(other.gameObject);
        }
        yield return null;
    }

    [Test]
    public void DifferentGenerationOrProjectionGraphsCannotShareOnePartition()
    {
        Guid generation = Guid.NewGuid();
        var first = new EcologyNavigationGraph(generation, Vector2Int.zero, 2, 1,
            "projection-a", "small", new[] { true, true }, new byte[] { 1, 4 });
        var stale = new EcologyNavigationGraph(Guid.NewGuid(), Vector2Int.zero, 2, 1,
            "projection-a", "large", new[] { true, true }, new byte[] { 1, 4 });
        var otherMap = new EcologyNavigationGraph(generation, Vector2Int.zero, 2, 1,
            "projection-b", "large", new[] { true, true }, new byte[] { 1, 4 });
        Assert.Throws<ArgumentException>(() => EcologyRegionPartitioner.Build(new[] { first, stale },
            new[] { 0, 1 }, new[] { 1, 2 }, new ulong[] { 1, 2 }));
        Assert.Throws<ArgumentException>(() => EcologyRegionPartitioner.Build(new[] { first, otherMap },
            new[] { 0, 1 }, new[] { 1, 2 }, new ulong[] { 1, 2 }));
    }
}
#endif
