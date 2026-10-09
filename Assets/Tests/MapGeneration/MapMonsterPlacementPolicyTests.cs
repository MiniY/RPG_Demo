using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MonsterPlacementTestTarget : MonoBehaviour, IMonsterPlacementTarget
{
    public Transform PlacementTransform => transform;
    public GameObject ReinitializationTarget => gameObject;
}

public sealed class MapMonsterPlacementPolicyTests
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
    public void CurrentProfileUsesApprovedExplicitDefaults()
    {
        MonsterPlacementProfile profile = new MonsterPlacementProfile();

        Assert.That(profile.Required, Is.True);
        Assert.That(profile.RequiredMinimum, Is.EqualTo(1));
        Assert.That(profile.TargetCount, Is.EqualTo(1));
        Assert.That(profile.Maximum, Is.EqualTo(1));
        Assert.That(profile.MinPathSteps, Is.EqualTo(10));
        Assert.That(profile.MaxPathSteps, Is.EqualTo(28));
        Assert.That(profile.AllowedTerrains, Is.EqualTo(
            MonsterTerrainMask.Grass |
            MonsterTerrainMask.Path |
            MonsterTerrainMask.Forest |
            MonsterTerrainMask.Sand));
    }

    [TestCase(9, false)]
    [TestCase(10, true)]
    [TestCase(28, true)]
    [TestCase(29, false)]
    public void SpawnPathDistanceBoundariesAreInclusive(int pathSteps, bool expectedValid)
    {
        MapData map = CreateOpenMap();

        bool valid = Evaluate(
            map,
            new Vector2Int(pathSteps, 2),
            out _,
            out string failureCode);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (!expectedValid)
            Assert.That(failureCode, Is.EqualTo("SpawnPathDistance"));
    }

    [TestCase(MapTerrainType.Grass)]
    [TestCase(MapTerrainType.Path)]
    [TestCase(MapTerrainType.Forest)]
    [TestCase(MapTerrainType.Sand)]
    public void ApprovedWalkableTerrainsAreAllowed(MapTerrainType terrain)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        map.SetTerrain(candidate, terrain);

        bool valid = Evaluate(map, candidate, out _, out string failureCode);

        Assert.That(valid, Is.True, failureCode);
    }

    [TestCase(MapTerrainType.DeepWater)]
    [TestCase(MapTerrainType.ShallowWater)]
    [TestCase(MapTerrainType.Mountain)]
    public void TerrainOutsideWhitelistIsRejected(MapTerrainType terrain)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        map.SetTerrain(candidate, terrain);

        bool valid = Evaluate(map, candidate, out _, out string failureCode);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("AllowedTerrain"));
    }

    [Test]
    public void AllowedButNonWalkableTerrainStillFailsWalkability()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        map.SetTerrain(candidate, MapTerrainType.DeepWater);
        MonsterPlacementProfile profile = new MonsterPlacementProfile(
            10,
            28,
            MonsterTerrainMask.DeepWater);

        bool valid = Evaluate(
            map,
            candidate,
            out _,
            out string failureCode,
            profile: profile);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("Walkability"));
    }

    [Test]
    public void ReachabilityAndDistanceUseFourDirectionBfsRatherThanWorldDistance()
    {
        MapData map = CreateOpenMap(16, 5);
        for (int y = 0; y < 4; y++)
            map.SetTerrain(new Vector2Int(2, y), MapTerrainType.DeepWater);
        Vector2Int candidate = new Vector2Int(6, 2);

        bool valid = Evaluate(
            map,
            candidate,
            out MonsterPlacementResult result,
            out string failureCode);

        Assert.That(valid, Is.True, failureCode);
        Assert.That(Manhattan(map.SpawnCell, candidate), Is.EqualTo(6));
        Assert.That(result.ShortestPathSteps, Is.EqualTo(10));
    }

    [Test]
    public void SpawnAndExitSafetyReservationsRejectOtherwiseLegalCells()
    {
        MapData map = CreateOpenMap();
        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.spawnProtectionRadius = 2;
        try
        {
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
                Guid.NewGuid(), map, null, settings);
            MonsterPlacementProfile profile = new MonsterPlacementProfile(
                0, 40, MonsterPlacementProfile.CurrentDefaultAllowedTerrains);

            Assert.That(Evaluate(
                map,
                map.SpawnCell + new Vector2Int(2, 0),
                out _,
                out string spawnReason,
                plan,
                profile), Is.False);
            Assert.That(spawnReason, Is.EqualTo("Occupancy"));
            Assert.That(Evaluate(
                map,
                map.ExitCell,
                out _,
                out string exitReason,
                plan,
                profile), Is.False);
            Assert.That(exitReason, Is.EqualTo("Occupancy"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void OccupancyConflictRejectsAnyCellInMonsterFootprint()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(10, 2);
        Vector2Int occupiedFootprintCell = candidate + Vector2Int.right;
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        Assert.That(plan.TryAdd(new MapPlacementReservation(
            "merchant.main",
            MerchantPlacementService.MerchantRole,
            occupiedFootprintCell,
            new[] { occupiedFootprintCell },
            MapPlacementOwnership.SceneBound), out _), Is.True);
        MonsterSemanticPlacementRequest request = new MonsterSemanticPlacementRequest(
            new MapPlacementRequest(
                MonsterPlacementService.MainMonsterLogicalObjectId,
                MonsterPlacementService.MonsterRole,
                new[] { Vector2Int.zero, Vector2Int.right },
                true,
                true),
            new MonsterPlacementProfile());

        bool valid = new MonsterPlacementPolicy().TryEvaluateCandidate(
            map,
            plan,
            request,
            candidate,
            out _,
            out string failureCode);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("Occupancy"));
    }

    [Test]
    public void ServicePlansExactlyOneSceneBoundMonsterReservation()
    {
        MapData map = CreateOpenMap();
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        MonsterPlacementService service = CreateService(new Vector3(500f, -300f, 7f));

        MonsterPlacementResult result = service.Plan(
            Attempt(plan.GenerationId, 12345),
            map,
            plan);

        Assert.That(result.ShortestPathSteps, Is.InRange(10, 28));
        Assert.That(result.FootprintCells, Is.EqualTo(new[] { result.PlacementCell }));
        Assert.That(plan.ReservationCount, Is.EqualTo(1));
        MapPlacementReservation reservation = plan.Reservations[0];
        Assert.That(reservation.Role, Is.EqualTo(MonsterPlacementService.MonsterRole));
        Assert.That(reservation.LogicalObjectId,
            Is.EqualTo(MonsterPlacementService.MainMonsterLogicalObjectId));
        Assert.That(reservation.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        Assert.That(plan.IsOccupied(result.PlacementCell), Is.True);
    }

    [Test]
    public void SameSeedRoleAndLogicalIdProduceSamePlacement()
    {
        MapData map = CreateOpenMap();
        MonsterPlacementPolicy policy = new MonsterPlacementPolicy();
        MonsterSemanticPlacementRequest request = Request();

        MonsterPlacementResult first = policy.SelectRequired(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MonsterPlacementResult second = policy.SelectRequired(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);

        Assert.That(second.PlacementCell, Is.EqualTo(first.PlacementCell));
        Assert.That(second.ShortestPathSteps, Is.EqualTo(first.ShortestPathSteps));
    }

    [Test]
    public void UnrelatedRoleDoesNotConsumeOrPerturbMonsterDeterminism()
    {
        MapData map = CreateOpenMap();
        MonsterPlacementPolicy policy = new MonsterPlacementPolicy();
        MonsterSemanticPlacementRequest request = Request();
        MonsterPlacementResult baseline = policy.SelectRequired(
            13579, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MapPlacementPlan withUnrelatedPopulation = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int unrelatedCell = new Vector2Int(34, 4);
        Assert.That(withUnrelatedPopulation.TryAdd(new MapPlacementReservation(
            "plant.unrelated",
            "Plant",
            unrelatedCell,
            new[] { unrelatedCell },
            MapPlacementOwnership.GenerationScoped), out _), Is.True);

        MonsterPlacementResult afterPopulation = policy.SelectRequired(
            13579, map, withUnrelatedPopulation, request);

        Assert.That(afterPopulation.PlacementCell, Is.EqualTo(baseline.PlacementCell));
    }

    [Test]
    public void RequiredFailureRejectsPendingWithoutCommitNearestCellOrLegacyFallback()
    {
        MapData map = CreateOpenMap();
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            map.SetTerrain(new Vector2Int(x, y), MapTerrainType.DeepWater);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        bool projected = false;

        MapGenerationExecutionResult execution = orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), 77),
            attempt => new MapPendingGeneration(attempt.GenerationId, map, null),
            (attempt, pending) =>
            {
                MapPlacementPlan plan = new MapPlacementPlan(attempt.GenerationId);
                new MonsterPlacementPolicy().SelectRequired(
                    attempt.AttemptSeed,
                    pending.Map,
                    plan,
                    Request());
                return plan;
            },
            _ => projected = true);

        Assert.That(execution.Succeeded, Is.False);
        Assert.That(execution.Failure.Code,
            Is.EqualTo("RequiredMonsterPlacementUnavailable"));
        Assert.That(execution.Failure.Reason, Does.Contain("RequiredMinimum=1"));
        Assert.That(projected, Is.False);
        Assert.That(context.ActiveGenerationId, Is.Null);
        Assert.That(context.PendingGenerationId, Is.Null);
        Assert.That(context.ActiveRegistry, Is.Null);
    }

    [Test]
    public void PlanningDoesNotReadAuthoredMonsterWorldPosition()
    {
        MapData map = CreateOpenMap();
        Guid generationId = Guid.NewGuid();
        MapGenerationAttempt attempt = Attempt(generationId, 8080);
        MonsterPlacementService first = CreateService(new Vector3(-999f, 400f, 0f));
        MonsterPlacementService second = CreateService(new Vector3(2f, -700f, 0f));

        MonsterPlacementResult firstResult = first.Plan(
            attempt, map, new MapPlacementPlan(generationId));
        MonsterPlacementResult secondResult = second.Plan(
            attempt, map, new MapPlacementPlan(generationId));

        Assert.That(secondResult.PlacementCell, Is.EqualTo(firstResult.PlacementCell));
    }

    [Test]
    public void MaterializationUsesExactCommittedCellAndGenerationAwareRegistry()
    {
        MapData map = CreateOpenMap();
        MonsterPlacementService service = CreateService(new Vector3(900f, 900f, 3f));
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithMonster(
            new MapIntegrationOrchestrator(context), service, map, 4242);
        Tilemap tilemap = CreateTilemap();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);

        bool materialized = service.TryMaterialize(
            context,
            execution.GenerationId.Value,
            coordinates,
            out string reason);

        Assert.That(materialized, Is.True, reason);
        Vector3 expected = coordinates.CellToWorld(map, service.PlacementCell);
        Assert.That(service.MonsterTarget.transform.position.x,
            Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(service.MonsterTarget.transform.position.y,
            Is.EqualTo(expected.y).Within(0.01f));
        Assert.That(service.MonsterTarget.transform.position.z, Is.EqualTo(3f));
        Assert.That(context.ActiveRegistry.TryGet(
            MonsterPlacementService.MainMonsterLogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(execution.GenerationId.Value));
        Assert.That(entry.Instance, Is.EqualTo(service.MonsterTarget));
        Assert.That(entry.InitialCell, Is.EqualTo(service.PlacementCell));
        Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
    }

    [Test]
    public void MaterializationRejectsStaleGenerationWithoutMovingMonster()
    {
        MapData map = CreateOpenMap();
        Vector3 authoredPosition = new Vector3(50f, -20f, 6f);
        MonsterPlacementService service = CreateService(authoredPosition);
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithMonster(
            new MapIntegrationOrchestrator(context), service, map, 9);
        Tilemap tilemap = CreateTilemap();

        bool materialized = service.TryMaterialize(
            context,
            Guid.NewGuid(),
            new MapCoordinateBoundary(tilemap),
            out string reason);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(materialized, Is.False);
        Assert.That(reason, Does.Contain("stale").IgnoreCase);
        Assert.That(service.MonsterTarget.transform.position, Is.EqualTo(authoredPosition));
        Assert.That(service.MaterializationCount, Is.Zero);
    }

    [Test]
    public void RegenerationRetiresOldRegistryAndRebindsSameMonsterInstance()
    {
        MapData map = CreateOpenMap();
        MonsterPlacementService service = CreateService(Vector3.zero);
        Tilemap tilemap = CreateTilemap();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithMonster(orchestrator, service, map, 111);
        Assert.That(service.TryMaterialize(
            context, first.GenerationId.Value, coordinates, out string firstReason),
            Is.True, firstReason);
        MapObjectRegistry firstRegistry = context.ActiveRegistry;
        UnityEngine.Object sceneInstance = service.MonsterTarget;

        MapGenerationExecutionResult second = ExecuteWithMonster(orchestrator, service, map, 222);
        Assert.That(service.TryMaterialize(
            context, second.GenerationId.Value, coordinates, out string secondReason),
            Is.True, secondReason);

        Assert.That(second.GenerationId, Is.Not.EqualTo(first.GenerationId));
        Assert.That(firstRegistry.IsRetired, Is.True);
        Assert.That(context.ActiveRegistry.GenerationId,
            Is.EqualTo(second.GenerationId.Value));
        Assert.That(context.ActiveRegistry.TryGet(
            MonsterPlacementService.MainMonsterLogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.Instance, Is.SameAs(sceneInstance));
        Assert.That(entry.GenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.ReadyGenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.MaterializationCount, Is.EqualTo(2));
    }

    [Test]
    public void ExistingMonsterAiOwnsGameplayAndReinitializesHomePatrolAndTransientState()
    {
        MapData map = CreateOpenMap();
        GameObject runtimeObject = NewObject("MonsterRuntime");
        MonsterPlacementService service = runtimeObject.AddComponent<MonsterPlacementService>();
        MapDependentReinitializationService reinitialization =
            runtimeObject.AddComponent<MapDependentReinitializationService>();
        GameObject monsterObject = NewObject("Torch_Blue");
        monsterObject.AddComponent<BoxCollider2D>();
        Type monsterAiType = FindRuntimeType("MonsterAIController");
        MonoBehaviour monsterAi = (MonoBehaviour)monsterObject.AddComponent(monsterAiType);
        SetPrivateField(service, "monsterTarget", monsterAi);
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithMonster(
            new MapIntegrationOrchestrator(context), service, map, 5150);
        Tilemap tilemap = CreateTilemap();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        Assert.That(service.TryMaterialize(
            context, execution.GenerationId.Value, coordinates, out string materializeReason),
            Is.True, materializeReason);

        GameObject transientTarget = NewObject("TransientTarget");
        SetPrivateField(monsterAi, "currentTarget", transientTarget.transform);
        Component movement = monsterObject.GetComponent("MonsterMovementController");
        movement.GetType().GetMethod("SetDestination")
            .Invoke(movement, new object[] { new Vector2(999f, 999f) });
        PlayerSpawnService playerSpawn = runtimeObject.AddComponent<PlayerSpawnService>();
        CanonicalPlayerProvider playerProvider = runtimeObject.AddComponent<CanonicalPlayerProvider>();
        Transform player = CreateCanonicalPlayerObject();
        Assert.That(playerSpawn.TrySpawn(
            context,
            execution.GenerationId.Value,
            coordinates,
            playerProvider,
            player,
            true,
            out string spawnReason), Is.True, spawnReason);

        Assert.That(reinitialization.TryReinitialize(
            context,
            execution.GenerationId.Value,
            new[] { service.ReinitializationTarget },
            out string reinitializationReason), Is.True, reinitializationReason);

        Vector2 homePosition = (Vector2)monsterAi.GetType().GetProperty(
                "HomePosition",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(monsterAi);
        Assert.That(homePosition.x,
            Is.EqualTo(monsterAi.transform.position.x).Within(0.01f));
        Assert.That(homePosition.y,
            Is.EqualTo(monsterAi.transform.position.y).Within(0.01f));
        Assert.That(monsterAi.GetType().GetProperty("CurrentTarget").GetValue(monsterAi), Is.Null);
        Assert.That(monsterAi.GetType().GetProperty("CurrentStateType").GetValue(monsterAi).ToString(),
            Is.EqualTo("Patrol"));
        Assert.That(monsterObject.GetComponent("MonsterPerceptionController"), Is.Not.Null);
        Assert.That(monsterObject.GetComponent("MonsterMovementController"), Is.Not.Null);
        Assert.That(monsterObject.GetComponent("MonsterAttackController"), Is.Not.Null);
        Assert.That(monsterObject.GetComponent("BaseMonster"), Is.Not.Null);
    }

    private static bool Evaluate(
        MapData map,
        Vector2Int candidate,
        out MonsterPlacementResult result,
        out string failureCode,
        MapPlacementPlan plan = null,
        MonsterPlacementProfile profile = null)
    {
        plan = plan ?? new MapPlacementPlan(Guid.NewGuid());
        return new MonsterPlacementPolicy().TryEvaluateCandidate(
            map,
            plan,
            Request(profile),
            candidate,
            out result,
            out failureCode);
    }

    private static MonsterSemanticPlacementRequest Request(
        MonsterPlacementProfile profile = null)
    {
        return new MonsterSemanticPlacementRequest(
            new MapPlacementRequest(
                MonsterPlacementService.MainMonsterLogicalObjectId,
                MonsterPlacementService.MonsterRole,
                new[] { Vector2Int.zero },
                true,
                true),
            profile ?? new MonsterPlacementProfile());
    }

    private static MapData CreateOpenMap(int width = 35, int height = 5)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);
        map.SpawnCell = new Vector2Int(0, 2);
        map.ExitCell = new Vector2Int(width - 1, 2);
        return map;
    }

    private MonsterPlacementService CreateService(Vector3 authoredPosition)
    {
        GameObject runtime = NewObject("MonsterRuntime");
        MonsterPlacementService service = runtime.AddComponent<MonsterPlacementService>();
        GameObject monster = NewObject("Torch_Blue");
        monster.transform.position = authoredPosition;
        MonsterPlacementTestTarget target = monster.AddComponent<MonsterPlacementTestTarget>();
        SetPrivateField(service, "monsterTarget", target);
        return service;
    }

    private Transform CreateCanonicalPlayerObject()
    {
        GameObject playerObject = NewObject("Player");
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>();
        GameObject cameraTarget = NewObject("CameraTarget");
        cameraTarget.transform.SetParent(playerObject.transform);
        return playerObject.transform;
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

    private static MapGenerationExecutionResult ExecuteWithMonster(
        MapIntegrationOrchestrator orchestrator,
        MonsterPlacementService service,
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

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
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
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }
        Assert.Fail($"Runtime type {typeName} was not loaded.");
        return null;
    }
}
