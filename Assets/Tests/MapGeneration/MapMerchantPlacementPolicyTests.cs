using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MerchantPlacementTestTarget : MonoBehaviour, IMerchantPlacementTarget
{
    public Transform PlacementTransform => transform;
    public Transform BoundPlayer { get; private set; }

    public bool TryBindCanonicalPlayer(Transform canonicalPlayer, out string reason)
    {
        BoundPlayer = canonicalPlayer;
        reason = canonicalPlayer == null ? "Canonical Player is missing." : string.Empty;
        return canonicalPlayer != null;
    }
}

public sealed class MapMerchantPlacementPolicyTests
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
        MerchantPlacementProfile profile = new MerchantPlacementProfile();

        Assert.That(profile.MinPathSteps, Is.EqualTo(8));
        Assert.That(profile.MaxPathSteps, Is.EqualTo(24));
        Assert.That(profile.AllowedTerrains,
            Is.EqualTo(MerchantTerrainMask.Grass | MerchantTerrainMask.Path));
    }

    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(24, true)]
    [TestCase(25, false)]
    public void SpawnPathDistanceBoundariesAreInclusive(int pathSteps, bool expectedValid)
    {
        MapData map = CreateOpenMap();
        bool valid = Evaluate(map, new Vector2Int(pathSteps, 2), out _, out string failureCode);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (!expectedValid)
            Assert.That(failureCode, Is.EqualTo("SpawnPathDistance"));
    }

    [TestCase(MapTerrainType.Grass, true)]
    [TestCase(MapTerrainType.Path, true)]
    [TestCase(MapTerrainType.Forest, false)]
    [TestCase(MapTerrainType.Sand, false)]
    public void ApprovedTerrainWhitelistIsIndependentAndExplicit(
        MapTerrainType terrain,
        bool expectedValid)
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        map.SetTerrain(candidate, terrain);

        bool valid = Evaluate(map, candidate, out _, out string failureCode);

        Assert.That(valid, Is.EqualTo(expectedValid));
        if (!expectedValid)
            Assert.That(failureCode, Is.EqualTo("AllowedTerrain"));
    }

    [Test]
    public void WhitelistedButNonWalkableTerrainStillFailsWalkability()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        map.SetTerrain(candidate, MapTerrainType.DeepWater);
        MerchantPlacementProfile profile = new MerchantPlacementProfile(
            8,
            24,
            MerchantTerrainMask.DeepWater);

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
        MapData map = CreateOpenMap(12, 5);
        map.ExitCell = new Vector2Int(11, 2);
        for (int y = 0; y < 4; y++)
            map.SetTerrain(new Vector2Int(2, y), MapTerrainType.DeepWater);
        Vector2Int candidate = new Vector2Int(4, 2);

        bool valid = Evaluate(map, candidate, out MerchantPlacementResult result, out string reason);

        Assert.That(valid, Is.True, reason);
        Assert.That(Mathf.Abs(candidate.x - map.SpawnCell.x) +
                    Mathf.Abs(candidate.y - map.SpawnCell.y), Is.EqualTo(4));
        Assert.That(result.ShortestPathSteps, Is.EqualTo(8));
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
            MerchantPlacementProfile profile = new MerchantPlacementProfile(
                0, 40, MerchantTerrainMask.Grass | MerchantTerrainMask.Path);

            Assert.That(Evaluate(
                map, map.SpawnCell + new Vector2Int(2, 0), out _, out string spawnReason,
                plan, profile), Is.False);
            Assert.That(spawnReason, Is.EqualTo("StaticOccupancy"));
            Assert.That(Evaluate(
                map, map.ExitCell, out _, out string exitReason,
                plan, profile), Is.False);
            Assert.That(exitReason, Is.EqualTo("StaticOccupancy"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void PlannedStaticOccupancyRejectsMerchantFootprintConflict()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        Assert.That(plan.TryAdd(new MapPlacementReservation(
            "static.blocker", "CollisionDecoration", candidate,
            new[] { candidate }, MapPlacementOwnership.StaticConstraint), out _), Is.True);

        bool valid = Evaluate(map, candidate, out _, out string failureCode, plan);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("StaticOccupancy"));
    }

    [Test]
    public void CandidateRequiresAReachableUnoccupiedInteractionApproachCell()
    {
        MapData map = CreateOpenMap();
        Vector2Int candidate = new Vector2Int(8, 2);
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        int index = 0;
        foreach (Vector2Int direction in new[]
                 { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
        {
            Vector2Int cell = candidate + direction;
            Assert.That(plan.TryAdd(new MapPlacementReservation(
                $"approach.blocker.{index++}", "TestReservation", cell,
                new[] { cell }, MapPlacementOwnership.StaticConstraint), out _), Is.True);
        }

        bool valid = Evaluate(map, candidate, out _, out string failureCode, plan);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("InteractionApproach"));
    }

    [Test]
    public void PathCandidateThatDisconnectsSpawnFromExitFailsCriticalPath()
    {
        MapData map = new MapData(30, 1, Vector2Int.zero);
        map.SpawnCell = Vector2Int.zero;
        map.ExitCell = new Vector2Int(29, 0);
        for (int x = 0; x < map.Width; x++)
            map.SetTerrain(new Vector2Int(x, 0), MapTerrainType.Path);

        bool valid = Evaluate(map, new Vector2Int(8, 0), out _, out string failureCode);

        Assert.That(valid, Is.False);
        Assert.That(failureCode, Is.EqualTo("CriticalPath"));
    }

    [Test]
    public void SelectedMerchantHasLegalReservedFootprintAndInteractionApproach()
    {
        MapData map = CreateOpenMap();
        MapPlacementPlan plan = new MapPlacementPlan(Guid.NewGuid());
        MerchantPlacementService service = CreateService(new Vector3(500f, -300f, 7f));
        MapGenerationAttempt attempt = Attempt(plan.GenerationId, 12345);

        MerchantPlacementResult result = service.Plan(attempt, map, plan);

        Assert.That(result.ShortestPathSteps, Is.InRange(8, 24));
        Assert.That(map.IsInside(result.PlacementCell), Is.True);
        Assert.That(map.IsWalkable(result.PlacementCell), Is.True);
        Assert.That(service.Profile.AllowsTerrain(
            map.GetCell(result.PlacementCell).terrainType), Is.True);
        Assert.That(Manhattan(result.PlacementCell, result.InteractionApproachCell), Is.EqualTo(1));
        Assert.That(map.IsWalkable(result.InteractionApproachCell), Is.True);
        Assert.That(plan.ReservationCount, Is.EqualTo(2));
        Assert.That(plan.IsOccupied(result.PlacementCell), Is.True);
        Assert.That(plan.IsOccupied(result.InteractionApproachCell), Is.True);
    }

    [Test]
    public void SameSeedRoleAndLogicalIdProduceSamePlacement()
    {
        MapData map = CreateOpenMap();
        MerchantPlacementPolicy policy = new MerchantPlacementPolicy();
        MerchantSemanticPlacementRequest request = Request();

        MerchantPlacementResult first = policy.SelectRequired(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MerchantPlacementResult second = policy.SelectRequired(
            24680, map, new MapPlacementPlan(Guid.NewGuid()), request);

        Assert.That(second.PlacementCell, Is.EqualTo(first.PlacementCell));
        Assert.That(second.InteractionApproachCell, Is.EqualTo(first.InteractionApproachCell));
    }

    [Test]
    public void UnrelatedPopulationOutsideMerchantConstraintsDoesNotChangePlacement()
    {
        MapData map = CreateOpenMap();
        MerchantPlacementPolicy policy = new MerchantPlacementPolicy();
        MerchantSemanticPlacementRequest request = Request();
        MerchantPlacementResult baseline = policy.SelectRequired(
            13579, map, new MapPlacementPlan(Guid.NewGuid()), request);
        MapPlacementPlan withUnrelatedPopulation = new MapPlacementPlan(Guid.NewGuid());
        Vector2Int unrelatedCell = new Vector2Int(29, 4);
        Assert.That(withUnrelatedPopulation.TryAdd(new MapPlacementReservation(
            "plant.unrelated", "Plant", unrelatedCell, new[] { unrelatedCell },
            MapPlacementOwnership.GenerationScoped), out _), Is.True);

        MerchantPlacementResult afterPopulation = policy.SelectRequired(
            13579, map, withUnrelatedPopulation, request);

        Assert.That(afterPopulation.PlacementCell, Is.EqualTo(baseline.PlacementCell));
        Assert.That(afterPopulation.InteractionApproachCell,
            Is.EqualTo(baseline.InteractionApproachCell));
    }

    [Test]
    public void RequiredFailureRejectsPendingWithoutLegacyOrNearestFallback()
    {
        MapData map = CreateOpenMap();
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                map.SetTerrain(new Vector2Int(x, y), MapTerrainType.Forest);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);

        MapGenerationExecutionResult execution = orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), 77),
            attempt => new MapPendingGeneration(attempt.GenerationId, map, null),
            (attempt, pending) =>
            {
                MapPlacementPlan plan = new MapPlacementPlan(attempt.GenerationId);
                new MerchantPlacementPolicy().SelectRequired(
                    attempt.AttemptSeed, pending.Map, plan, Request());
                return plan;
            },
            _ => Assert.Fail("Required Merchant planning failure must not commit/project."));

        Assert.That(execution.Succeeded, Is.False);
        Assert.That(execution.Failure.Code, Is.EqualTo("RequiredMerchantPlacementUnavailable"));
        Assert.That(execution.Failure.Reason, Does.Contain("Role=Merchant"));
        Assert.That(execution.Failure.Reason, Does.Contain("LogicalObjectId=merchant.main"));
        Assert.That(context.ActiveGenerationId, Is.Null);
        Assert.That(context.PendingGenerationId, Is.Null);
        Assert.That(context.ActiveRegistry, Is.Null);
    }

    [Test]
    public void PlanningDoesNotReadAuthoredMerchantWorldPosition()
    {
        MapData map = CreateOpenMap();
        Guid generationId = Guid.NewGuid();
        MapGenerationAttempt attempt = Attempt(generationId, 8080);
        MerchantPlacementService first = CreateService(new Vector3(-999f, 400f, 0f));
        MerchantPlacementService second = CreateService(new Vector3(2f, -700f, 0f));

        MerchantPlacementResult firstResult = first.Plan(
            attempt, map, new MapPlacementPlan(generationId));
        MerchantPlacementResult secondResult = second.Plan(
            attempt, map, new MapPlacementPlan(generationId));

        Assert.That(secondResult.PlacementCell, Is.EqualTo(firstResult.PlacementCell));
        Assert.That(secondResult.InteractionApproachCell,
            Is.EqualTo(firstResult.InteractionApproachCell));
    }

    [Test]
    public void MaterializationUsesCommittedCellBindsCanonicalPlayerAndRegistry()
    {
        MapData map = CreateOpenMap();
        MerchantPlacementService service = CreateService(new Vector3(900f, 900f, 3f));
        MerchantPlacementTestTarget target =
            (MerchantPlacementTestTarget)service.MerchantTarget;
        CanonicalPlayerProvider provider = CreateCanonicalPlayer(out Transform player);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult execution = ExecuteWithMerchant(
            orchestrator, service, map, 4242);
        Tilemap tilemap = CreateTilemap();

        bool materialized = service.TryMaterialize(
            context,
            execution.GenerationId.Value,
            new MapCoordinateBoundary(tilemap),
            provider,
            player,
            out string reason);

        Assert.That(materialized, Is.True, reason);
        Assert.That(target.BoundPlayer, Is.EqualTo(player));
        Vector3 expected = new MapCoordinateBoundary(tilemap).CellToWorld(
            map, service.PlacementCell);
        Assert.That(target.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(target.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));
        Assert.That(target.transform.position.z, Is.EqualTo(3f));
        Assert.That(context.ActiveRegistry.TryGet(
            MerchantPlacementService.MainMerchantLogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(execution.GenerationId.Value));
        Assert.That(entry.Instance, Is.EqualTo(service.MerchantTarget));
        Assert.That(entry.InitialCell, Is.EqualTo(service.PlacementCell));
        Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
    }

    [Test]
    public void MaterializationRejectsStaleGenerationWithoutMovingMerchant()
    {
        MapData map = CreateOpenMap();
        Vector3 authoredPosition = new Vector3(50f, -20f, 6f);
        MerchantPlacementService service = CreateService(authoredPosition);
        CanonicalPlayerProvider provider = CreateCanonicalPlayer(out Transform player);
        MapRuntimeContext context = InitializedContext();
        MapGenerationExecutionResult execution = ExecuteWithMerchant(
            new MapIntegrationOrchestrator(context), service, map, 9);
        Tilemap tilemap = CreateTilemap();

        bool materialized = service.TryMaterialize(
            context,
            Guid.NewGuid(),
            new MapCoordinateBoundary(tilemap),
            provider,
            player,
            out string reason);

        Assert.That(execution.Succeeded, Is.True);
        Assert.That(materialized, Is.False);
        Assert.That(reason, Does.Contain("stale").IgnoreCase);
        Assert.That(service.MerchantTarget.transform.position, Is.EqualTo(authoredPosition));
        Assert.That(service.MaterializationCount, Is.Zero);
    }

    [Test]
    public void RegenerationRetiresOldRegistryAndRegistersMerchantToNewGeneration()
    {
        MapData map = CreateOpenMap();
        MerchantPlacementService service = CreateService(Vector3.zero);
        CanonicalPlayerProvider provider = CreateCanonicalPlayer(out Transform player);
        Tilemap tilemap = CreateTilemap();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult first = ExecuteWithMerchant(orchestrator, service, map, 111);
        Assert.That(service.TryMaterialize(context, first.GenerationId.Value, coordinates,
            provider, player, out string firstReason), Is.True, firstReason);
        MapObjectRegistry firstRegistry = context.ActiveRegistry;

        MapGenerationExecutionResult second = ExecuteWithMerchant(orchestrator, service, map, 222);
        Assert.That(service.TryMaterialize(context, second.GenerationId.Value, coordinates,
            provider, player, out string secondReason), Is.True, secondReason);

        Assert.That(second.GenerationId, Is.Not.EqualTo(first.GenerationId));
        Assert.That(firstRegistry.IsRetired, Is.True);
        Assert.That(context.ActiveRegistry.GenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(context.ActiveRegistry.TryGet(
            MerchantPlacementService.MainMerchantLogicalObjectId, out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.ReadyGenerationId, Is.EqualTo(second.GenerationId.Value));
        Assert.That(service.MaterializationCount, Is.EqualTo(2));
    }

    private static bool Evaluate(
        MapData map,
        Vector2Int candidate,
        out MerchantPlacementResult result,
        out string failureCode,
        MapPlacementPlan plan = null,
        MerchantPlacementProfile profile = null)
    {
        plan = plan ?? new MapPlacementPlan(Guid.NewGuid());
        profile = profile ?? new MerchantPlacementProfile();
        return new MerchantPlacementPolicy().TryEvaluateCandidate(
            123,
            map,
            plan,
            Request(profile),
            candidate,
            out result,
            out failureCode);
    }

    private static MerchantSemanticPlacementRequest Request(
        MerchantPlacementProfile profile = null)
    {
        return new MerchantSemanticPlacementRequest(
            new MapPlacementRequest(
                MerchantPlacementService.MainMerchantLogicalObjectId,
                MerchantPlacementService.MerchantRole,
                new[] { Vector2Int.zero },
                true,
                true),
            profile ?? new MerchantPlacementProfile());
    }

    private static MapData CreateOpenMap(int width = 30, int height = 5)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);
        map.SpawnCell = new Vector2Int(0, 2);
        map.ExitCell = new Vector2Int(width - 1, 2);
        return map;
    }

    private MerchantPlacementService CreateService(Vector3 authoredPosition)
    {
        GameObject runtime = NewObject("MerchantRuntime");
        MerchantPlacementService service = runtime.AddComponent<MerchantPlacementService>();
        GameObject merchant = NewObject("Merchant");
        merchant.transform.position = authoredPosition;
        MerchantPlacementTestTarget target = merchant.AddComponent<MerchantPlacementTestTarget>();
        SetPrivateField(service, "merchantTarget", target);
        return service;
    }

    private CanonicalPlayerProvider CreateCanonicalPlayer(out Transform player)
    {
        GameObject runtime = NewObject("PlayerRuntime");
        CanonicalPlayerProvider provider = runtime.AddComponent<CanonicalPlayerProvider>();
        GameObject playerObject = NewObject("Player");
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>();
        GameObject cameraTarget = NewObject("CameraTarget");
        cameraTarget.transform.SetParent(playerObject.transform);
        player = playerObject.transform;
        return provider;
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

    private static MapGenerationExecutionResult ExecuteWithMerchant(
        MapIntegrationOrchestrator orchestrator,
        MerchantPlacementService service,
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
        typeof(MerchantPlacementService).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
