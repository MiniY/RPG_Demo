using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MapDestructiblePlacementTestTarget : MonoBehaviour,
    IDestructiblePlacementTarget
{
    private Action defeated;

    public UnityEngine.Object PlacementInstance => this;
    public GameObject PlacementGameObject => gameObject;

    public GameObject Spawn(Vector3 position, Quaternion rotation)
    {
        GameObject instance = UnityEngine.Object.Instantiate(gameObject, position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Return(GameObject instance) => instance.SetActive(false);

    public void SubscribeDefeated(Action callback) => defeated += callback;
    public void UnsubscribeDefeated(Action callback) => defeated -= callback;
    public void TriggerDefeated() => defeated?.Invoke();
}

public sealed class MapDestructiblePlacementPolicyTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject target in objectsToDestroy)
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        objectsToDestroy.Clear();
    }

    [Test]
    public void CurrentProfileUsesExplicitAcceptanceDefaults()
    {
        DestructiblePlacementProfile profile = new DestructiblePlacementProfile();

        Assert.That(profile.RequiredMinimum, Is.EqualTo(4));
        Assert.That(profile.TargetCount, Is.EqualTo(8));
        Assert.That(profile.Maximum, Is.EqualTo(8));
        Assert.That(profile.MinPathSteps, Is.EqualTo(8));
        Assert.That(profile.MaxPathSteps, Is.EqualTo(int.MaxValue));
        Assert.That(profile.MinimumSpacingSteps, Is.EqualTo(3));
        Assert.That(profile.ExitSafetyRadius, Is.EqualTo(2));
        Assert.That(profile.AllowedTerrains, Is.EqualTo(
            DestructibleTerrainMask.Grass | DestructibleTerrainMask.Forest));
    }

    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(20, true)]
    public void SpawnPathDistanceUsesInclusiveFourDirectionBfs(int pathSteps, bool expectedValid)
    {
        DestructiblePlacementProfile profile = new DestructiblePlacementProfile(
            8, 20, 1, 0,
            DestructiblePlacementProfile.CurrentDefaultAllowedTerrains,
            1, 1, 1);

        bool valid = Evaluate(
            CreateOpenMap(30, 5),
            new Vector2Int(pathSteps, 2),
            out DestructiblePlacementResult result,
            out string reason,
            profile: profile);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (expectedValid)
            Assert.That(result.ShortestPathSteps, Is.EqualTo(pathSteps));
        else
            Assert.That(reason, Is.EqualTo("SpawnPathDistance"));
    }

    [TestCase(MapTerrainType.Grass, true)]
    [TestCase(MapTerrainType.Forest, true)]
    [TestCase(MapTerrainType.Path, false)]
    [TestCase(MapTerrainType.Sand, false)]
    public void TerrainWhitelistIsIndependentFromWalkability(
        MapTerrainType terrain,
        bool expectedValid)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        map.SetTerrain(candidate, terrain);

        bool valid = Evaluate(map, candidate, out _, out string reason);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (!expectedValid)
            Assert.That(reason, Is.EqualTo("AllowedTerrain"));
    }

    [Test]
    public void AllowedButNonWalkableTerrainIsRejected()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        map.SetTerrain(candidate, MapTerrainType.DeepWater);
        DestructiblePlacementProfile profile = new DestructiblePlacementProfile(
            0, int.MaxValue, 1, 0,
            DestructibleTerrainMask.DeepWater,
            1, 1, 1);

        Assert.That(Evaluate(
            map, candidate, out _, out string reason, profile: profile), Is.False);
        Assert.That(reason, Is.EqualTo("Walkability"));
    }

    [Test]
    public void SpawnExitOccupancyFootprintSpacingAndCriticalPathAreProtected()
    {
        MapData map = CreateOpenMap(35, 5);
        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.spawnProtectionRadius = 2;
        try
        {
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
                Guid.NewGuid(), map, null, settings);
            Assert.That(Evaluate(map, map.SpawnCell, out _, out string spawnReason, plan), Is.False);
            Assert.That(spawnReason, Is.EqualTo("Occupancy"));
            Assert.That(Evaluate(
                map,
                map.ExitCell + Vector2Int.left,
                out _,
                out string exitReason,
                plan), Is.False);
            Assert.That(exitReason, Is.EqualTo("ExitSafety"));

            Vector2Int candidate = new Vector2Int(10, 2);
            Assert.That(plan.TryAdd(new MapPlacementReservation(
                "merchant.test",
                MerchantPlacementService.MerchantRole,
                candidate + Vector2Int.right,
                new[] { candidate + Vector2Int.right },
                MapPlacementOwnership.SceneBound), out _), Is.True);
            Assert.That(Evaluate(
                map,
                candidate,
                out _,
                out string occupancyReason,
                plan,
                footprint: new[] { Vector2Int.zero, Vector2Int.right }), Is.False);
            Assert.That(occupancyReason, Is.EqualTo("Occupancy"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        MapPlacementPlan spacedPlan = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int existing = new Vector2Int(12, 2);
        Assert.That(spacedPlan.TryAdd(new MapPlacementReservation(
            "destructible.mushroom-small.001",
            DestructiblePlacementService.DestructibleRole,
            existing,
            new[] { existing },
            MapPlacementOwnership.GenerationScoped), out _), Is.True);
        DestructiblePlacementProfile spacedProfile = new DestructiblePlacementProfile(
            0, int.MaxValue, 3, 0,
            DestructiblePlacementProfile.CurrentDefaultAllowedTerrains,
            1, 1, 1);
        Assert.That(Evaluate(
            map, existing + Vector2Int.right * 2, out _, out string spacingReason,
            spacedPlan, spacedProfile, "destructible.mushroom-small.002"), Is.False);
        Assert.That(spacingReason, Is.EqualTo("DestructibleSpacing"));
        Assert.That(Evaluate(
            map, existing + Vector2Int.right * 3, out _, out string boundaryReason,
            spacedPlan, spacedProfile, "destructible.mushroom-small.002"), Is.True,
            boundaryReason);

        MapData corridor = CreateOpenMap(20, 3);
        corridor.SpawnCell = new Vector2Int(0, 1);
        corridor.ExitCell = new Vector2Int(19, 1);
        for (int x = 0; x < corridor.Width; x++)
        {
            corridor.SetTerrain(new Vector2Int(x, 0), MapTerrainType.DeepWater);
            corridor.SetTerrain(new Vector2Int(x, 2), MapTerrainType.DeepWater);
        }
        Assert.That(Evaluate(
            corridor,
            new Vector2Int(10, 1),
            out _,
            out string pathReason), Is.False);
        Assert.That(pathReason, Is.EqualTo("CriticalPath"));
    }

    [Test]
    public void SameSeedTypeAndLogicalIdAreStableAndUnrelatedRoleDoesNotConsumeRandomness()
    {
        MapData map = CreateOpenMap();
        DestructiblePlacementPolicy policy = new DestructiblePlacementPolicy();
        DestructibleSemanticPlacementRequest request = Request(
            "destructible.mushroom-small.001");
        DestructiblePlacementResult baseline = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        DestructiblePlacementResult repeated = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MapPlacementPlan withUnrelatedRole = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int unrelatedCell = baseline.PlacementCell == new Vector2Int(34, 4)
            ? new Vector2Int(33, 4)
            : new Vector2Int(34, 4);
        Assert.That(withUnrelatedRole.TryAdd(new MapPlacementReservation(
            "unrelated.population",
            "UnrelatedRole",
            unrelatedCell,
            new[] { unrelatedCell },
            MapPlacementOwnership.SceneBound), out _), Is.True);
        DestructiblePlacementResult afterUnrelatedRole = policy.SelectOptional(
            24680, map, withUnrelatedRole, request);

        Assert.That(repeated.PlacementCell, Is.EqualTo(baseline.PlacementCell));
        Assert.That(afterUnrelatedRole.PlacementCell, Is.EqualTo(baseline.PlacementCell));
    }

    [Test]
    public void PopulationMayUnderfillTargetButCannotCommitBelowRequiredMinimum()
    {
        MapData map = CreateOpenMap(14, 5);
        DestructiblePlacementService service = CreateService(CreateDamageablePrefab());
        SetPrivateField(service, "profile", new DestructiblePlacementProfile(
            8, int.MaxValue, 6, 0,
            DestructiblePlacementProfile.CurrentDefaultAllowedTerrains,
            2, 8, 8));
        MapRuntimeContext context = InitializedContext();

        MapGenerationExecutionResult underfilled = ExecuteWithDestructibles(
            new MapIntegrationOrchestrator(context), service, map, 77);

        Assert.That(underfilled.Succeeded, Is.True);
        Assert.That(service.LastPopulationPlan.ActualCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(service.LastPopulationPlan.ActualCount, Is.LessThan(8));
        Assert.That(service.LastPopulationPlan.UnderfillCount,
            Is.EqualTo(8 - service.LastPopulationPlan.ActualCount));

        MapData blocked = CreateOpenMap();
        for (int x = 0; x < blocked.Width; x++)
        for (int y = 0; y < blocked.Height; y++)
            blocked.SetTerrain(new Vector2Int(x, y), MapTerrainType.Path);
        MapRuntimeContext failingContext = InitializedContext();
        MapGenerationExecutionResult failure = ExecuteWithDestructibles(
            new MapIntegrationOrchestrator(failingContext),
            CreateService(CreateDamageablePrefab()),
            blocked,
            88);

        Assert.That(failure.Succeeded, Is.False);
        Assert.That(failure.Failure.Code, Is.EqualTo("RetryBudgetExhausted"));
        Assert.That(failingContext.ActiveGenerationId, Is.Null);
        Assert.That(failingContext.ActiveRegistry, Is.Null);
    }

    [Test]
    public void MaterializationUsesCommittedCellsAndLifecycleReleaseIsGenerationAwareAndIdempotent()
    {
        GameObject prefab = CreateDamageablePrefab();
        DestructiblePlacementService service = CreateService(prefab);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult execution = ExecuteWithDestructibles(
            orchestrator, service, CreateOpenMap(), 111);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());

        Assert.That(service.TryMaterialize(
            context, execution.GenerationId.Value, coordinates, out string reason), Is.True, reason);
        Assert.That(service.ActiveCount, Is.EqualTo(service.LastPopulationPlan.ActualCount));
        Assert.That(service.TryGetActiveInstance(
            service.LastPopulationPlan.Placements[0].LogicalObjectId,
            out UnityEngine.Object instanceObject), Is.True);
        MapDestructiblePlacementTestTarget instance =
            (MapDestructiblePlacementTestTarget)instanceObject;
        Assert.That(context.ActiveRegistry.TryGet(
            service.LastPopulationPlan.Placements[0].LogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.Instance, Is.SameAs(instance));
        Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.GenerationScoped));
        Vector3 expected = coordinates.CellToWorld(
            context.ActiveMap, service.LastPopulationPlan.Placements[0].PlacementCell);
        Assert.That(instance.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(instance.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));

        string logicalId = entry.LogicalObjectId;
        Assert.That(service.TryRelease(
            execution.GenerationId.Value, logicalId, instance, out string releaseReason),
            Is.True, releaseReason);
        Assert.That(context.ActiveRegistry.TryGet(logicalId, out _), Is.False);
        Assert.That(context.ActiveRegistry.IsOccupied(entry.InitialCell), Is.False);
        Assert.That(service.TryRelease(
            execution.GenerationId.Value, logicalId, instance, out _), Is.False);
        Assert.That(service.TryGetActiveInstance(
            service.LastPopulationPlan.Placements[1].LogicalObjectId,
            out UnityEngine.Object secondInstance), Is.True);
        Assert.That(service.TryRelease(
            Guid.NewGuid(),
            service.LastPopulationPlan.Placements[1].LogicalObjectId,
            secondInstance,
            out _), Is.False);
        Assert.That(service.ActiveCount,
            Is.EqualTo(service.LastPopulationPlan.ActualCount - 1));
    }

    [Test]
    public void DefeatedObjectDoesNotRespawnLocallyButNewGenerationCreatesNewOwnership()
    {
        GameObject prefab = CreateDamageablePrefab();
        DestructiblePlacementService service = CreateService(prefab);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());
        MapGenerationExecutionResult first = ExecuteWithDestructibles(
            orchestrator, service, CreateOpenMap(), 123);
        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason), Is.True, firstReason);
        MapObjectRegistry firstRegistry = context.ActiveRegistry;
        string logicalId = service.LastPopulationPlan.Placements[0].LogicalObjectId;
        Assert.That(service.TryGetActiveInstance(
            logicalId,
            out UnityEngine.Object firstInstanceObject), Is.True);
        MapDestructiblePlacementTestTarget firstInstance =
            (MapDestructiblePlacementTestTarget)firstInstanceObject;

        firstInstance.TriggerDefeated();

        Assert.That(service.ActiveCount, Is.EqualTo(service.LastPopulationPlan.ActualCount - 1));
        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out _), Is.False);
        Assert.That(context.ActiveRegistry.TryGet(logicalId, out _), Is.False);

        MapGenerationExecutionResult second = ExecuteWithDestructibles(
            orchestrator, service, CreateOpenMap(), 123);
        Assert.That(service.TryMaterialize(
            context, second.GenerationId.Value, coordinates, out string secondReason), Is.True,
            secondReason);

        Assert.That(firstRegistry.IsRetired, Is.True);
        Assert.That(second.GenerationId, Is.Not.EqualTo(first.GenerationId));
        Assert.That(context.ActiveRegistry.TryGet(logicalId, out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.ActiveCount, Is.EqualTo(service.LastPopulationPlan.ActualCount));
    }

    private static bool Evaluate(
        MapData map,
        Vector2Int candidate,
        out DestructiblePlacementResult result,
        out string failureCode,
        MapPlacementPlan plan = null,
        DestructiblePlacementProfile profile = null,
        string logicalObjectId = "destructible.mushroom-small.001",
        IReadOnlyList<Vector2Int> footprint = null)
    {
        plan = plan ?? new MapPlacementPlan(Guid.NewGuid());
        return new DestructiblePlacementPolicy().TryEvaluateCandidate(
            map,
            plan,
            Request(logicalObjectId, profile, footprint),
            candidate,
            out result,
            out failureCode);
    }

    private static DestructibleSemanticPlacementRequest Request(
        string logicalObjectId,
        DestructiblePlacementProfile profile = null,
        IReadOnlyList<Vector2Int> footprint = null)
    {
        return new DestructibleSemanticPlacementRequest(
            new MapPlacementRequest(
                logicalObjectId,
                DestructiblePlacementService.DestructibleRole,
                footprint ?? new[] { Vector2Int.zero },
                false,
                true),
            DestructiblePlacementService.CurrentTypeId,
            profile ?? new DestructiblePlacementProfile());
    }

    private static MapData CreateOpenMap(int width = 40, int height = 5)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);
        map.SpawnCell = new Vector2Int(0, 2);
        map.ExitCell = new Vector2Int(width - 1, 2);
        return map;
    }

    private DestructiblePlacementService CreateService(GameObject prefab)
    {
        GameObject runtime = NewObject("DestructibleRuntime");
        DestructiblePlacementService service =
            runtime.AddComponent<DestructiblePlacementService>();
        SetPrivateField(service, "destructiblePrefab", prefab);
        return service;
    }

    private GameObject CreateDamageablePrefab()
    {
        GameObject prefab = NewObject("Mushroom_Small_Destructible_Prefab");
        prefab.SetActive(false);
        prefab.AddComponent<MapDestructiblePlacementTestTarget>();
        return prefab;
    }

    private Tilemap CreateTilemap()
    {
        GameObject grid = NewObject("Grid");
        grid.AddComponent<Grid>();
        GameObject tilemapObject = NewObject("Ground");
        tilemapObject.transform.SetParent(grid.transform);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        return tilemap;
    }

    private GameObject NewObject(string name)
    {
        GameObject target = new GameObject(name);
        objectsToDestroy.Add(target);
        return target;
    }

    private static MapGenerationExecutionResult ExecuteWithDestructibles(
        MapIntegrationOrchestrator orchestrator,
        DestructiblePlacementService service,
        MapData map,
        int seed)
    {
        return orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), seed),
            attempt => new MapPendingGeneration(attempt.GenerationId, map, null),
            (attempt, pending) =>
            {
                MapPlacementPlan plan = new MapPlacementPlan(attempt.GenerationId);
                service.Plan(attempt, pending.Map, plan);
                return plan;
            },
            _ => { });
    }

    private static MapRuntimeContext InitializedContext()
    {
        MapRuntimeContext context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
        typeof(MapRuntimeContext).GetMethod(
                "SetPhase",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(context, new object[] { MapLifecyclePhase.Initialized });
        return context;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
