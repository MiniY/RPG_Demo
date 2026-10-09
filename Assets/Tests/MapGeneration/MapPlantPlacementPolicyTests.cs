using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PlantPlacementTestTarget : MonoBehaviour,
    IPlantPlacementTarget,
    IMapDependentReinitializable
{
    public Transform PlacementTransform => transform;
    public GameObject ReinitializationTarget => gameObject;
    public bool PlacementAvailable { get; private set; } = true;
    public int ReinitializationCount { get; private set; }
    public void SetPlacementAvailable(bool available) => PlacementAvailable = available;
    public void ReinitializeForMap() => ReinitializationCount++;
}

public sealed class MapPlantPlacementPolicyTests
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
    public void CurrentProfileUsesExplicitConservativeDefaults()
    {
        PlantPlacementProfile profile = new PlantPlacementProfile();

        Assert.That(profile.Required, Is.False);
        Assert.That(profile.RequiredMinimum, Is.Zero);
        Assert.That(profile.TargetCount, Is.EqualTo(17));
        Assert.That(profile.Maximum, Is.EqualTo(17));
        Assert.That(profile.MinPathSteps, Is.Zero);
        Assert.That(profile.MaxPathSteps, Is.EqualTo(int.MaxValue));
        Assert.That(profile.MinimumSpacingSteps, Is.EqualTo(1));
        Assert.That(profile.AllowedTerrains, Is.EqualTo(
            PlantTerrainMask.Grass |
            PlantTerrainMask.Path |
            PlantTerrainMask.Forest |
            PlantTerrainMask.Sand));
    }

    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(12, true)]
    [TestCase(13, false)]
    public void ConfiguredSpawnPathDistanceBoundariesAreInclusive(int pathSteps, bool expectedValid)
    {
        PlantPlacementProfile profile = new PlantPlacementProfile(
            4,
            12,
            1,
            PlantPlacementProfile.CurrentDefaultAllowedTerrains);

        bool valid = Evaluate(
            CreateOpenMap(),
            new Vector2Int(pathSteps, 2),
            out _,
            out string failureCode,
            profile: profile);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (!expectedValid)
            Assert.That(failureCode, Is.EqualTo("SpawnPathDistance"));
    }

    [TestCase(MapTerrainType.Grass)]
    [TestCase(MapTerrainType.Path)]
    [TestCase(MapTerrainType.Forest)]
    [TestCase(MapTerrainType.Sand)]
    public void CurrentWalkableTerrainsAreAllowed(MapTerrainType terrain)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        map.SetTerrain(candidate, terrain);

        Assert.That(Evaluate(map, candidate, out _, out string reason), Is.True, reason);
    }

    [TestCase(MapTerrainType.DeepWater)]
    [TestCase(MapTerrainType.ShallowWater)]
    [TestCase(MapTerrainType.Mountain)]
    public void TerrainOutsideWhitelistIsRejected(MapTerrainType terrain)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        map.SetTerrain(candidate, terrain);

        Assert.That(Evaluate(map, candidate, out _, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("AllowedTerrain"));
    }

    [Test]
    public void AllowedButNonWalkableTerrainStillFailsWalkability()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        map.SetTerrain(candidate, MapTerrainType.DeepWater);
        PlantPlacementProfile profile = new PlantPlacementProfile(
            0,
            int.MaxValue,
            1,
            PlantTerrainMask.DeepWater);

        Assert.That(Evaluate(map, candidate, out _, out string reason, profile: profile), Is.False);
        Assert.That(reason, Is.EqualTo("Walkability"));
    }

    [Test]
    public void ReachabilityUsesFourDirectionWalkableBfs()
    {
        MapData map = CreateOpenMap(18, 5);
        for (int y = 0; y < 4; y++)
            map.SetTerrain(new Vector2Int(2, y), MapTerrainType.DeepWater);
        Vector2Int candidate = new Vector2Int(6, 2);

        Assert.That(Evaluate(
            map,
            candidate,
            out PlantPlacementResult result,
            out string reason), Is.True, reason);
        Assert.That(result.ShortestPathSteps, Is.EqualTo(10));
    }

    [Test]
    public void SpawnExitOccupancyFootprintAndPlantSpacingAreEnforced()
    {
        MapData map = CreateOpenMap();
        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.spawnProtectionRadius = 2;
        try
        {
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(Guid.NewGuid(), map, null, settings);
            Assert.That(Evaluate(map, map.SpawnCell, out _, out string spawnReason, plan), Is.False);
            Assert.That(spawnReason, Is.EqualTo("Occupancy"));
            Assert.That(Evaluate(map, map.ExitCell, out _, out string exitReason, plan), Is.False);
            Assert.That(exitReason, Is.EqualTo("Occupancy"));

            Vector2Int candidate = new Vector2Int(8, 2);
            Assert.That(plan.TryAdd(new MapPlacementReservation(
                "static.test",
                "CollisionDecoration",
                candidate + Vector2Int.right,
                new[] { candidate + Vector2Int.right },
                MapPlacementOwnership.StaticConstraint), out _), Is.True);
            PlantSemanticPlacementRequest footprintRequest = Request(
                "plant.mushroom-big.1",
                footprint: new[] { Vector2Int.zero, Vector2Int.right });
            Assert.That(new PlantPlacementPolicy().TryEvaluateCandidate(
                map, plan, footprintRequest, candidate, out _, out string occupancyReason), Is.False);
            Assert.That(occupancyReason, Is.EqualTo("Occupancy"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        PlantPlacementProfile spacedProfile = new PlantPlacementProfile(
            0,
            int.MaxValue,
            3,
            PlantPlacementProfile.CurrentDefaultAllowedTerrains);
        MapPlacementPlan spacedPlan = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int existing = new Vector2Int(8, 2);
        Assert.That(spacedPlan.TryAdd(new MapPlacementReservation(
            "plant.mushroom-big.1",
            PlantPlacementService.PlantRole,
            existing,
            new[] { existing },
            MapPlacementOwnership.SceneBound), out _), Is.True);
        Assert.That(Evaluate(
            map, existing + new Vector2Int(2, 0), out _, out string spacingReason,
            spacedPlan, spacedProfile, "plant.mushroom-big.2"), Is.False);
        Assert.That(spacingReason, Is.EqualTo("PlantSpacing"));
        Assert.That(Evaluate(
            map, existing + new Vector2Int(3, 0), out _, out string boundaryReason,
            spacedPlan, spacedProfile, "plant.mushroom-big.2"), Is.True, boundaryReason);
    }

    [Test]
    public void StaticPlantCannotBreakSpawnToExitCriticalPath()
    {
        MapData map = CreateOpenMap(7, 3);
        map.SpawnCell = new Vector2Int(0, 1);
        map.ExitCell = new Vector2Int(6, 1);
        for (int x = 0; x < map.Width; x++)
        {
            map.SetTerrain(new Vector2Int(x, 0), MapTerrainType.DeepWater);
            map.SetTerrain(new Vector2Int(x, 2), MapTerrainType.DeepWater);
        }

        Assert.That(Evaluate(
            map,
            new Vector2Int(3, 1),
            out _,
            out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("CriticalPath"));
    }

    [Test]
    public void SameSeedRoleAndLogicalIdProduceSamePlacement()
    {
        MapData map = CreateOpenMap();
        PlantPlacementPolicy policy = new PlantPlacementPolicy();
        PlantSemanticPlacementRequest request = Request("plant.mushroom-big.1");

        PlantPlacementResult first = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        PlantPlacementResult second = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);

        Assert.That(second.PlacementCell, Is.EqualTo(first.PlacementCell));
        Assert.That(second.ShortestPathSteps, Is.EqualTo(first.ShortestPathSteps));
    }

    [Test]
    public void UnrelatedRoleDoesNotPerturbPlantDeterminism()
    {
        MapData map = CreateOpenMap();
        PlantPlacementPolicy policy = new PlantPlacementPolicy();
        PlantSemanticPlacementRequest request = Request("plant.mushroom-big.1");
        PlantPlacementResult baseline = policy.SelectOptional(
            13579, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MapPlacementPlan withUnrelatedPopulation = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int unrelatedCell = baseline.PlacementCell == new Vector2Int(34, 4)
            ? new Vector2Int(33, 4)
            : new Vector2Int(34, 4);
        Assert.That(withUnrelatedPopulation.TryAdd(new MapPlacementReservation(
            "animal.unrelated",
            AnimalPlacementService.AnimalRole,
            unrelatedCell,
            new[] { unrelatedCell },
            MapPlacementOwnership.SceneBound), out _), Is.True);

        PlantPlacementResult afterPopulation = policy.SelectOptional(
            13579, map, withUnrelatedPopulation, request);

        Assert.That(afterPopulation.PlacementCell, Is.EqualTo(baseline.PlacementCell));
    }

    [Test]
    public void OptionalPopulationCommitsWithoutLegacyOrNearestCellFallbackWhenNoCellIsValid()
    {
        MapData map = CreateOpenMap();
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            map.SetTerrain(new Vector2Int(x, y), MapTerrainType.DeepWater);
        PlantPlacementService service = CreateService();
        MapRuntimeContext context = InitializedContext();

        MapGenerationExecutionResult execution = ExecuteWithPlants(
            new MapIntegrationOrchestrator(context), service, map, 77);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(service.PlannedCount, Is.Zero);
        Assert.That(context.ActiveRegistry.Count, Is.Zero);
    }

    [Test]
    public void ServicePlansSeventeenStableSceneBoundPlantsWithoutReadingAuthoredPositions()
    {
        MapData map = CreateOpenMap();
        PlantPlacementService first = CreateService(new Vector3(900f, -700f, 3f));
        PlantPlacementService second = CreateService(new Vector3(-500f, 400f, -2f));
        Guid generationId = Guid.NewGuid();
        MapGenerationAttempt attempt = Attempt(generationId, 12345);
        MapPlacementPlan firstPlan = new MapPlacementPlan(generationId);
        MapPlacementPlan secondPlan = new MapPlacementPlan(generationId);

        IReadOnlyList<PlantPlacementResult> firstResults = first.Plan(attempt, map, firstPlan);
        IReadOnlyList<PlantPlacementResult> secondResults = second.Plan(attempt, map, secondPlan);

        Assert.That(firstResults, Has.Count.EqualTo(17));
        Assert.That(secondResults, Has.Count.EqualTo(17));
        Assert.That(secondResults[0].PlacementCell, Is.EqualTo(firstResults[0].PlacementCell));
        Assert.That(firstPlan.Reservations, Has.Count.EqualTo(17));
        foreach (MapPlacementReservation reservation in firstPlan.Reservations)
        {
            Assert.That(reservation.Role, Is.EqualTo(PlantPlacementService.PlantRole));
            Assert.That(reservation.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        }
    }

    [Test]
    public void MaterializationUsesExactCommittedCellsAndGenerationAwareRegistry()
    {
        MapData map = CreateOpenMap();
        PlantPlacementService service = CreateService(new Vector3(500f, 500f, 7f));
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithPlants(
            new MapIntegrationOrchestrator(context), service, map, 4242);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());

        Assert.That(service.TryMaterialize(
            context, execution.GenerationId.Value, coordinates, out string reason), Is.True, reason);
        Assert.That(service.MaterializedCount, Is.EqualTo(17));
        foreach (PlantPlacementBinding binding in service.Bindings)
        {
            Assert.That(service.TryGetMaterializedCell(binding.LogicalObjectId, out Vector2Int cell), Is.True);
            Vector3 expected = coordinates.CellToWorld(map, cell);
            Assert.That(binding.PlantTransform.position.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(binding.PlantTransform.position.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(binding.PlantTransform.position.z, Is.EqualTo(7f));
            Assert.That(context.ActiveRegistry.TryGet(
                binding.LogicalObjectId, out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(execution.GenerationId.Value));
            Assert.That(entry.Instance, Is.EqualTo(
                binding.PlantTransform.GetComponent<PlantPlacementTestTarget>()));
            Assert.That(entry.InitialCell, Is.EqualTo(cell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        }
    }

    [Test]
    public void StaleMaterializationIsRejectedWithoutMovingPlants()
    {
        Vector3 authoredPosition = new Vector3(50f, -20f, 6f);
        PlantPlacementService service = CreateService(authoredPosition);
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithPlants(
            new MapIntegrationOrchestrator(context), service, CreateOpenMap(), 9);

        bool materialized = service.TryMaterialize(
            context, Guid.NewGuid(), new MapCoordinateBoundary(CreateTilemap()), out string reason);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(materialized, Is.False);
        Assert.That(reason, Does.Contain("stale").IgnoreCase);
        foreach (PlantPlacementBinding binding in service.Bindings)
            Assert.That(binding.PlantTransform.position, Is.EqualTo(authoredPosition));
        Assert.That(service.MaterializationCount, Is.Zero);
    }

    [Test]
    public void RegenerationRetiresOldRegistryAndRebindsSameSceneInstances()
    {
        MapData map = CreateOpenMap();
        PlantPlacementService service = CreateService();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithPlants(orchestrator, service, map, 111);
        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason), Is.True, firstReason);
        MapObjectRegistry firstRegistry = context.ActiveRegistry;
        PlantPlacementTestTarget firstInstance =
            service.Bindings[0].PlantTransform.GetComponent<PlantPlacementTestTarget>();

        MapGenerationExecutionResult second = ExecuteWithPlants(orchestrator, service, map, 222);
        Assert.That(service.TryMaterialize(
            context, second.GenerationId.Value, coordinates, out string secondReason), Is.True, secondReason);

        Assert.That(firstRegistry.IsRetired, Is.True);
        Assert.That(context.ActiveRegistry.TryGet(
            service.Bindings[0].LogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.Instance, Is.SameAs(firstInstance));
        Assert.That(entry.GenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.ReadyGenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.MaterializationCount, Is.EqualTo(2));
    }

    [Test]
    public void OptionalOmissionHidesWithoutDisablingSceneInstances()
    {
        PlantPlacementService service = CreateService();
        MapRuntimeContext context = InitializedContext();
        MapData blockedMap = CreateOpenMap();
        for (int x = 0; x < blockedMap.Width; x++)
        for (int y = 0; y < blockedMap.Height; y++)
            blockedMap.SetTerrain(new Vector2Int(x, y), MapTerrainType.DeepWater);
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithPlants(orchestrator, service, blockedMap, 1);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());

        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason), Is.True, firstReason);
        foreach (PlantPlacementBinding binding in service.Bindings)
        {
            PlantPlacementTestTarget target =
                binding.PlantTransform.GetComponent<PlantPlacementTestTarget>();
            Assert.That(binding.PlantTransform.gameObject.activeSelf, Is.True);
            Assert.That(target.PlacementAvailable, Is.False);
        }

        MapGenerationExecutionResult second = ExecuteWithPlants(
            orchestrator, service, CreateOpenMap(), 2);
        Assert.That(service.TryMaterialize(
            context, second.GenerationId.Value, coordinates, out string secondReason), Is.True, secondReason);
        foreach (PlantPlacementBinding binding in service.Bindings)
        {
            PlantPlacementTestTarget target =
                binding.PlantTransform.GetComponent<PlantPlacementTestTarget>();
            Assert.That(binding.PlantTransform.gameObject.activeSelf, Is.True);
            Assert.That(target.PlacementAvailable, Is.True);
        }
    }

    private static bool Evaluate(
        MapData map,
        Vector2Int candidate,
        out PlantPlacementResult result,
        out string failureCode,
        MapPlacementPlan plan = null,
        PlantPlacementProfile profile = null,
        string logicalObjectId = "plant.mushroom-big.1")
    {
        plan = plan ?? new MapPlacementPlan(Guid.NewGuid());
        return new PlantPlacementPolicy().TryEvaluateCandidate(
            map,
            plan,
            Request(logicalObjectId, profile),
            candidate,
            out result,
            out failureCode);
    }

    private static PlantSemanticPlacementRequest Request(
        string logicalObjectId,
        PlantPlacementProfile profile = null,
        IReadOnlyList<Vector2Int> footprint = null)
    {
        return new PlantSemanticPlacementRequest(
            new MapPlacementRequest(
                logicalObjectId,
                PlantPlacementService.PlantRole,
                footprint ?? new[] { Vector2Int.zero },
                false,
                true),
            profile ?? new PlantPlacementProfile());
    }

    private static MapData CreateOpenMap(int width = 35, int height = 5)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);
        map.SpawnCell = new Vector2Int(0, 2);
        map.ExitCell = new Vector2Int(width - 1, 2);
        return map;
    }

    private PlantPlacementService CreateService(Vector3? authoredPosition = null)
    {
        GameObject runtime = NewObject("PlantRuntime");
        PlantPlacementService service = runtime.AddComponent<PlantPlacementService>();
        string[] logicalIds = PlantPlacementService.CurrentLogicalObjectIds;
        PlantPlacementBinding[] bindings = new PlantPlacementBinding[logicalIds.Length];
        for (int index = 0; index < bindings.Length; index++)
        {
            GameObject plantObject = NewObject($"Plant ({index})");
            plantObject.AddComponent<BoxCollider2D>();
            plantObject.AddComponent<PlantPlacementTestTarget>();
            plantObject.transform.position = authoredPosition ?? Vector3.zero;
            bindings[index] = new PlantPlacementBinding(logicalIds[index], plantObject.transform);
        }
        SetPrivateField(service, "bindings", bindings);
        return service;
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

    private static MapGenerationExecutionResult ExecuteWithPlants(
        MapIntegrationOrchestrator orchestrator,
        PlantPlacementService service,
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

    private static MapGenerationAttempt Attempt(Guid generationId, int seed)
    {
        return new MapGenerationRequest(Guid.NewGuid(), seed)
            .CreateAttempt(0, generationId);
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
