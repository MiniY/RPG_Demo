using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class AnimalPlacementTestTarget : MonoBehaviour,
    IAnimalPlacementTarget,
    IMapDependentReinitializable
{
    public Transform PlacementTransform => transform;
    public GameObject ReinitializationTarget => gameObject;
    public bool PlacementAvailable { get; private set; } = true;
    public int ReinitializationCount { get; private set; }
    public void SetPlacementAvailable(bool available) => PlacementAvailable = available;
    public void ReinitializeForMap() => ReinitializationCount++;
}

public sealed class MapAnimalPlacementPolicyTests
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
        AnimalPlacementProfile profile = new AnimalPlacementProfile();

        Assert.That(profile.Required, Is.False);
        Assert.That(profile.RequiredMinimum, Is.Zero);
        Assert.That(profile.TargetCount, Is.EqualTo(4));
        Assert.That(profile.Maximum, Is.EqualTo(4));
        Assert.That(profile.MinPathSteps, Is.Zero);
        Assert.That(profile.MaxPathSteps, Is.EqualTo(int.MaxValue));
        Assert.That(profile.MinimumSpacingSteps, Is.EqualTo(1));
        Assert.That(profile.AllowedTerrains, Is.EqualTo(
            AnimalTerrainMask.Grass |
            AnimalTerrainMask.Path |
            AnimalTerrainMask.Forest |
            AnimalTerrainMask.Sand));
    }

    [TestCase(3, false)]
    [TestCase(4, true)]
    [TestCase(12, true)]
    [TestCase(13, false)]
    public void ConfiguredSpawnPathDistanceBoundariesAreInclusive(int pathSteps, bool expectedValid)
    {
        AnimalPlacementProfile profile = new AnimalPlacementProfile(
            4,
            12,
            1,
            AnimalPlacementProfile.CurrentDefaultAllowedTerrains);

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
        AnimalPlacementProfile profile = new AnimalPlacementProfile(
            0,
            int.MaxValue,
            1,
            AnimalTerrainMask.DeepWater);

        Assert.That(Evaluate(
            map,
            candidate,
            out _,
            out string reason,
            profile: profile), Is.False);
        Assert.That(reason, Is.EqualTo("Walkability"));
    }

    [Test]
    public void ReachabilityAndDistanceUseFourDirectionWalkableBfs()
    {
        MapData map = CreateOpenMap(18, 5);
        for (int y = 0; y < 4; y++)
            map.SetTerrain(new Vector2Int(2, y), MapTerrainType.DeepWater);
        Vector2Int candidate = new Vector2Int(6, 2);

        Assert.That(Evaluate(
            map,
            candidate,
            out AnimalPlacementResult result,
            out string reason), Is.True, reason);
        Assert.That(result.ShortestPathSteps, Is.EqualTo(10));
    }

    [Test]
    public void SpawnAndExitSafetyReservationsAreHonored()
    {
        MapData map = CreateOpenMap();
        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.spawnProtectionRadius = 2;
        try
        {
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
                Guid.NewGuid(), map, null, settings);

            Assert.That(Evaluate(
                map,
                map.SpawnCell + new Vector2Int(2, 0),
                out _,
                out string spawnReason,
                plan), Is.False);
            Assert.That(spawnReason, Is.EqualTo("Occupancy"));
            Assert.That(Evaluate(
                map,
                map.ExitCell,
                out _,
                out string exitReason,
                plan), Is.False);
            Assert.That(exitReason, Is.EqualTo("Occupancy"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void FootprintOccupancyAndAnimalSpacingAreEnforced()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        MapPlacementPlan occupiedPlan = new MapPlacementPlan(Guid.NewGuid());
        Assert.That(occupiedPlan.TryAdd(new MapPlacementReservation(
            "static.test",
            "CollisionDecoration",
            candidate + Vector2Int.right,
            new[] { candidate + Vector2Int.right },
            MapPlacementOwnership.StaticConstraint), out _), Is.True);
        AnimalSemanticPlacementRequest footprintRequest = Request(
            "animal.happy-sheep.1",
            footprint: new[] { Vector2Int.zero, Vector2Int.right });

        Assert.That(new AnimalPlacementPolicy().TryEvaluateCandidate(
            map,
            occupiedPlan,
            footprintRequest,
            candidate,
            out _,
            out string occupancyReason), Is.False);
        Assert.That(occupancyReason, Is.EqualTo("Occupancy"));

        AnimalPlacementProfile spacedProfile = new AnimalPlacementProfile(
            0,
            int.MaxValue,
            3,
            AnimalPlacementProfile.CurrentDefaultAllowedTerrains);
        MapPlacementPlan spacedPlan = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int existing = new Vector2Int(8, 2);
        Assert.That(spacedPlan.TryAdd(new MapPlacementReservation(
            "animal.happy-sheep.1",
            AnimalPlacementService.AnimalRole,
            existing,
            new[] { existing },
            MapPlacementOwnership.SceneBound), out _), Is.True);
        Assert.That(Evaluate(
            map,
            existing + new Vector2Int(2, 0),
            out _,
            out string spacingReason,
            spacedPlan,
            spacedProfile,
            "animal.happy-sheep.2"), Is.False);
        Assert.That(spacingReason, Is.EqualTo("AnimalSpacing"));
        Assert.That(Evaluate(
            map,
            existing + new Vector2Int(3, 0),
            out _,
            out string boundaryReason,
            spacedPlan,
            spacedProfile,
            "animal.happy-sheep.2"), Is.True, boundaryReason);
    }

    [Test]
    public void SameSeedRoleAndLogicalIdProduceSamePlacement()
    {
        MapData map = CreateOpenMap();
        AnimalPlacementPolicy policy = new AnimalPlacementPolicy();
        AnimalSemanticPlacementRequest request = Request("animal.happy-sheep.1");

        AnimalPlacementResult first = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        AnimalPlacementResult second = policy.SelectOptional(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);

        Assert.That(second.PlacementCell, Is.EqualTo(first.PlacementCell));
        Assert.That(second.ShortestPathSteps, Is.EqualTo(first.ShortestPathSteps));
    }

    [Test]
    public void UnrelatedRoleDoesNotConsumeOrPerturbAnimalDeterminism()
    {
        MapData map = CreateOpenMap();
        AnimalPlacementPolicy policy = new AnimalPlacementPolicy();
        AnimalSemanticPlacementRequest request = Request("animal.happy-sheep.1");
        AnimalPlacementResult baseline = policy.SelectOptional(
            13579, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MapPlacementPlan withUnrelatedPopulation = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int unrelatedCell = baseline.PlacementCell == new Vector2Int(34, 4)
            ? new Vector2Int(33, 4)
            : new Vector2Int(34, 4);
        Assert.That(withUnrelatedPopulation.TryAdd(new MapPlacementReservation(
            "plant.unrelated",
            "Plant",
            unrelatedCell,
            new[] { unrelatedCell },
            MapPlacementOwnership.GenerationScoped), out _), Is.True);

        AnimalPlacementResult afterPopulation = policy.SelectOptional(
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
        AnimalPlacementService service = CreateService();
        MapRuntimeContext context = InitializedContext();

        MapGenerationExecutionResult execution = ExecuteWithAnimals(
            new MapIntegrationOrchestrator(context), service, map, 77);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(service.PlannedCount, Is.Zero);
        Assert.That(context.ActiveRegistry.Count, Is.Zero);
    }

    [Test]
    public void ServicePlansFourStableSceneBoundAnimalsWithoutReadingAuthoredPositions()
    {
        MapData map = CreateOpenMap();
        AnimalPlacementService first = CreateService(new Vector3(900f, -700f, 3f));
        AnimalPlacementService second = CreateService(new Vector3(-500f, 400f, -2f));
        Guid generationId = Guid.NewGuid();
        MapGenerationAttempt attempt = Attempt(generationId, 12345);
        MapPlacementPlan firstPlan = new MapPlacementPlan(generationId);
        MapPlacementPlan secondPlan = new MapPlacementPlan(generationId);

        IReadOnlyList<AnimalPlacementResult> firstResults = first.Plan(attempt, map, firstPlan);
        IReadOnlyList<AnimalPlacementResult> secondResults = second.Plan(attempt, map, secondPlan);

        Assert.That(firstResults, Has.Count.EqualTo(4));
        Assert.That(secondResults, Has.Count.EqualTo(4));
        Assert.That(secondResults[0].PlacementCell, Is.EqualTo(firstResults[0].PlacementCell));
        Assert.That(firstPlan.Reservations, Has.Count.EqualTo(4));
        foreach (MapPlacementReservation reservation in firstPlan.Reservations)
        {
            Assert.That(reservation.Role, Is.EqualTo(AnimalPlacementService.AnimalRole));
            Assert.That(reservation.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        }
    }

    [Test]
    public void MaterializationUsesExactCommittedCellsAndGenerationAwareRegistry()
    {
        MapData map = CreateOpenMap();
        AnimalPlacementService service = CreateService(new Vector3(500f, 500f, 7f));
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithAnimals(
            new MapIntegrationOrchestrator(context), service, map, 4242);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());

        Assert.That(service.TryMaterialize(
            context,
            execution.GenerationId.Value,
            coordinates,
            out string reason), Is.True, reason);
        Assert.That(service.MaterializedCount, Is.EqualTo(4));
        foreach (AnimalPlacementBinding binding in service.Bindings)
        {
            Assert.That(service.TryGetMaterializedCell(
                binding.LogicalObjectId,
                out Vector2Int cell), Is.True);
            Vector3 expected = coordinates.CellToWorld(map, cell);
            Assert.That(binding.AnimalTransform.position.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(binding.AnimalTransform.position.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(binding.AnimalTransform.position.z, Is.EqualTo(7f));
            Assert.That(context.ActiveRegistry.TryGet(
                binding.LogicalObjectId,
                out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(execution.GenerationId.Value));
            Assert.That(entry.Instance, Is.EqualTo(
                binding.AnimalTransform.GetComponent<AnimalPlacementTestTarget>()));
            Assert.That(entry.InitialCell, Is.EqualTo(cell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        }
    }

    [Test]
    public void StaleMaterializationIsRejectedWithoutMovingAnimals()
    {
        Vector3 authoredPosition = new Vector3(50f, -20f, 6f);
        AnimalPlacementService service = CreateService(authoredPosition);
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithAnimals(
            new MapIntegrationOrchestrator(context), service, CreateOpenMap(), 9);

        bool materialized = service.TryMaterialize(
            context,
            Guid.NewGuid(),
            new MapCoordinateBoundary(CreateTilemap()),
            out string reason);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(materialized, Is.False);
        Assert.That(reason, Does.Contain("stale").IgnoreCase);
        foreach (AnimalPlacementBinding binding in service.Bindings)
            Assert.That(binding.AnimalTransform.position, Is.EqualTo(authoredPosition));
        Assert.That(service.MaterializationCount, Is.Zero);
    }

    [Test]
    public void RegenerationRetiresOldRegistryAndRebindsSameSceneInstances()
    {
        MapData map = CreateOpenMap();
        AnimalPlacementService service = CreateService();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithAnimals(orchestrator, service, map, 111);
        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason), Is.True, firstReason);
        MapObjectRegistry firstRegistry = context.ActiveRegistry;
        AnimalPlacementTestTarget firstInstance =
            service.Bindings[0].AnimalTransform.GetComponent<AnimalPlacementTestTarget>();

        MapGenerationExecutionResult second = ExecuteWithAnimals(orchestrator, service, map, 222);
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
    public void AnimalReinitializationClearsOnlyTransientMapState()
    {
        GameObject animalObject = NewObject("HappySheep");
        animalObject.SetActive(false);
        animalObject.AddComponent<BoxCollider2D>();
        Type animalType = FindRuntimeType("Animal");
        Type hurtType = FindRuntimeType("AnimalHurtController");
        MonoBehaviour animal = (MonoBehaviour)animalObject.AddComponent(animalType);
        Component hurt = animalObject.AddComponent(hurtType);
        animalType.BaseType.GetField(
                "maxHealth",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(animal, 100f);
        animalObject.SetActive(true);
        animalType.BaseType.GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(animal, null);
        animalType.GetMethod("TakeDamage", new[] { typeof(float) })
            .Invoke(animal, new object[] { 15f });
        float healthBefore = (float)animalType.GetProperty("CurrentHealth").GetValue(animal);
        bool defeatedBefore = (bool)animalType.GetProperty("IsDefeated").GetValue(animal);
        bool activeBefore = animalObject.activeSelf;
        Assert.That(healthBefore, Is.EqualTo(85f));
        Assert.That(defeatedBefore, Is.False);

        ((IMapDependentReinitializable)animal).ReinitializeForMap();

        Assert.That((float)animalType.GetProperty("CurrentHealth").GetValue(animal),
            Is.EqualTo(healthBefore));
        Assert.That((bool)animalType.GetProperty("IsDefeated").GetValue(animal),
            Is.EqualTo(defeatedBefore));
        Assert.That(animalObject.activeSelf, Is.EqualTo(activeBefore));
        Assert.That(hurt, Is.Not.Null);
    }

    [Test]
    public void OptionalOmissionHidesWithoutDisablingOrResettingSceneInstance()
    {
        AnimalPlacementService service = CreateService();
        MapRuntimeContext context = InitializedContext();
        MapData blockedMap = CreateOpenMap();
        for (int x = 0; x < blockedMap.Width; x++)
        for (int y = 0; y < blockedMap.Height; y++)
            blockedMap.SetTerrain(new Vector2Int(x, y), MapTerrainType.DeepWater);
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithAnimals(orchestrator, service, blockedMap, 1);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(CreateTilemap());

        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason), Is.True, firstReason);
        foreach (AnimalPlacementBinding binding in service.Bindings)
        {
            AnimalPlacementTestTarget target =
                binding.AnimalTransform.GetComponent<AnimalPlacementTestTarget>();
            Assert.That(binding.AnimalTransform.gameObject.activeSelf, Is.True);
            Assert.That(target.PlacementAvailable, Is.False);
        }

        MapGenerationExecutionResult second = ExecuteWithAnimals(
            orchestrator, service, CreateOpenMap(), 2);
        Assert.That(service.TryMaterialize(
            context, second.GenerationId.Value, coordinates, out string secondReason), Is.True, secondReason);
        foreach (AnimalPlacementBinding binding in service.Bindings)
        {
            AnimalPlacementTestTarget target =
                binding.AnimalTransform.GetComponent<AnimalPlacementTestTarget>();
            Assert.That(binding.AnimalTransform.gameObject.activeSelf, Is.True);
            Assert.That(target.PlacementAvailable, Is.True);
        }
    }

    private static bool Evaluate(
        MapData map,
        Vector2Int candidate,
        out AnimalPlacementResult result,
        out string failureCode,
        MapPlacementPlan plan = null,
        AnimalPlacementProfile profile = null,
        string logicalObjectId = "animal.happy-sheep.1")
    {
        plan = plan ?? new MapPlacementPlan(Guid.NewGuid());
        return new AnimalPlacementPolicy().TryEvaluateCandidate(
            map,
            plan,
            Request(logicalObjectId, profile),
            candidate,
            out result,
            out failureCode);
    }

    private static AnimalSemanticPlacementRequest Request(
        string logicalObjectId,
        AnimalPlacementProfile profile = null,
        IReadOnlyList<Vector2Int> footprint = null)
    {
        return new AnimalSemanticPlacementRequest(
            new MapPlacementRequest(
                logicalObjectId,
                AnimalPlacementService.AnimalRole,
                footprint ?? new[] { Vector2Int.zero },
                false,
                true),
            profile ?? new AnimalPlacementProfile());
    }

    private static MapData CreateOpenMap(int width = 35, int height = 5)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);
        map.SpawnCell = new Vector2Int(0, 2);
        map.ExitCell = new Vector2Int(width - 1, 2);
        return map;
    }

    private AnimalPlacementService CreateService(Vector3? authoredPosition = null)
    {
        GameObject runtime = NewObject("AnimalRuntime");
        AnimalPlacementService service = runtime.AddComponent<AnimalPlacementService>();
        AnimalPlacementBinding[] bindings = new AnimalPlacementBinding[4];
        for (int index = 0; index < bindings.Length; index++)
        {
            GameObject animalObject = NewObject($"HappySheep ({index})");
            animalObject.AddComponent<BoxCollider2D>();
            animalObject.AddComponent<AnimalPlacementTestTarget>();
            animalObject.transform.position = authoredPosition ?? Vector3.zero;
            bindings[index] = new AnimalPlacementBinding(
                AnimalPlacementService.HappySheepLogicalIdPrefix + (index + 1),
                animalObject.transform);
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

    private static MapGenerationExecutionResult ExecuteWithAnimals(
        MapIntegrationOrchestrator orchestrator,
        AnimalPlacementService service,
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

    private static Type FindRuntimeType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }
        Assert.Fail($"Runtime type {typeName} was not loaded.");
        return null;
    }
}
