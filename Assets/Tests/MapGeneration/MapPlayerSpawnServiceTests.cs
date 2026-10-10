using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MapPlayerSpawnServiceTests
{
    private GameObject gridObject;
    private Tilemap tilemap;
    private GameObject playerObject;
    private GameObject runtimeObject;

    [TearDown]
    public void TearDown()
    {
        if (runtimeObject != null) UnityEngine.Object.DestroyImmediate(runtimeObject);
        if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
        if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void SpawnUsesCommittedSpawnCellAndPublishesPlayerReadyAfterPhysicsSync()
    {
        MapData map = CreateMap();
        MapRuntimeContext context = CommittedContext(map, out Guid generationId);
        CanonicalPlayerProvider provider = CreateProvider();
        PlayerSpawnService service = runtimeObject.AddComponent<PlayerSpawnService>();
        int readyCount = 0;
        Guid readyGeneration = Guid.Empty;
        service.PlayerReady += id => { readyCount++; readyGeneration = id; };
        Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(3f, -2f);
        body.angularVelocity = 7f;

        bool spawned = service.TrySpawn(context, generationId,
            new MapCoordinateBoundary(tilemap), provider, playerObject.transform, true, out string reason);

        Vector3 expected = new MapCoordinateBoundary(tilemap).CellToWorld(map, map.SpawnCell);
        Assert.That(spawned, Is.True, reason);
        Assert.That(service.SpawnCount, Is.EqualTo(1));
        Assert.That(playerObject.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(playerObject.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));
        Assert.That(body.position.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(body.position.y, Is.EqualTo(expected.y).Within(0.01f));
        Assert.That(body.velocity, Is.EqualTo(Vector2.zero));
        Assert.That(body.angularVelocity, Is.Zero);
        Assert.That(readyCount, Is.EqualTo(1));
        Assert.That(readyGeneration, Is.EqualTo(generationId));
        Assert.That(context.Phase, Is.EqualTo(MapLifecyclePhase.SpawningPlayer));
        Assert.That(context.IsReady, Is.False, "PlayerReady must not mean GameplayReady.");
    }

    [Test]
    public void StaleGenerationDoesNotMovePlayerOrPublishPlayerReady()
    {
        MapRuntimeContext context = CommittedContext(CreateMap(), out _);
        CanonicalPlayerProvider provider = CreateProvider();
        PlayerSpawnService service = runtimeObject.AddComponent<PlayerSpawnService>();
        Vector3 originalPosition = playerObject.transform.position;
        int readyCount = 0;
        service.PlayerReady += _ => readyCount++;

        bool spawned = service.TrySpawn(context, Guid.NewGuid(),
            new MapCoordinateBoundary(tilemap), provider, playerObject.transform, true, out string reason);

        Assert.That(spawned, Is.False);
        Assert.That(reason, Does.Contain("stale").IgnoreCase);
        Assert.That(playerObject.transform.position, Is.EqualTo(originalPosition));
        Assert.That(service.SpawnCount, Is.Zero);
        Assert.That(readyCount, Is.Zero);
        Assert.That(context.IsFailed, Is.True);
    }

    [Test]
    public void IllegalSpawnCellFailsWithoutPublishingPlayerReady()
    {
        MapData map = CreateMap();
        map.SetTerrain(map.SpawnCell, MapTerrainType.DeepWater);
        MapRuntimeContext context = CommittedContext(map, out Guid generationId);
        CanonicalPlayerProvider provider = CreateProvider();
        PlayerSpawnService service = runtimeObject.AddComponent<PlayerSpawnService>();
        int readyCount = 0;
        service.PlayerReady += _ => readyCount++;

        bool spawned = service.TrySpawn(context, generationId,
            new MapCoordinateBoundary(tilemap), provider, playerObject.transform, true, out string reason);

        Assert.That(spawned, Is.False);
        Assert.That(reason, Does.Contain("walkable"));
        Assert.That(service.SpawnCount, Is.Zero);
        Assert.That(readyCount, Is.Zero);
        Assert.That(context.IsFailed, Is.True);
    }

    [Test]
    public void SpawnWaitsForProjectionAndCollisionReadiness()
    {
        MapRuntimeContext context = CommittedContext(CreateMap(), out Guid generationId);
        CanonicalPlayerProvider provider = CreateProvider();
        PlayerSpawnService service = runtimeObject.AddComponent<PlayerSpawnService>();
        int readyCount = 0;
        service.PlayerReady += _ => readyCount++;

        bool spawned = service.TrySpawn(context, generationId,
            new MapCoordinateBoundary(tilemap), provider, playerObject.transform, false, out string reason);

        Assert.That(spawned, Is.False);
        Assert.That(reason, Does.Contain("projection"));
        Assert.That(service.SpawnCount, Is.Zero);
        Assert.That(readyCount, Is.Zero);
        Assert.That(context.IsFailed, Is.True);
    }

    [Test]
    public void DuplicateSpawnForCurrentGenerationFailsWithoutMovingPlayerAgain()
    {
        MapRuntimeContext context = CommittedContext(CreateMap(), out Guid generationId);
        CanonicalPlayerProvider provider = CreateProvider();
        PlayerSpawnService service = runtimeObject.AddComponent<PlayerSpawnService>();
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        Assert.That(service.TrySpawn(context, generationId, coordinates, provider,
            playerObject.transform, true, out string firstReason), Is.True, firstReason);
        Vector3 firstSpawnPosition = playerObject.transform.position;
        playerObject.transform.position = firstSpawnPosition + Vector3.right;

        bool spawnedAgain = service.TrySpawn(context, generationId, coordinates, provider,
            playerObject.transform, true, out string duplicateReason);

        Assert.That(spawnedAgain, Is.False);
        Assert.That(duplicateReason, Does.Contain("duplicate"));
        Assert.That(service.SpawnCount, Is.EqualTo(1));
        Assert.That(playerObject.transform.position, Is.EqualTo(firstSpawnPosition + Vector3.right));
        Assert.That(context.IsFailed, Is.True);
    }

    [Test]
    public void StaticPlanningReservesConfiguredSpawnSafetyRegion()
    {
        MapData map = CreateMap();
        MapGenerationSettings settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.spawnProtectionRadius = 2;
        try
        {
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
                Guid.NewGuid(), map, null, settings);

            for (int x = map.SpawnCell.x - 2; x <= map.SpawnCell.x + 2; x++)
            {
                for (int y = map.SpawnCell.y - 2; y <= map.SpawnCell.y + 2; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (map.IsInside(cell)) Assert.That(plan.IsOccupied(cell), Is.True, cell.ToString());
                }
            }
            Assert.That(plan.IsOccupied(map.SpawnCell + new Vector2Int(3, 0)), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CanonicalPlayerProviderRejectsMissingOrAmbiguousPlayer()
    {
        CanonicalPlayerProvider provider = runtimeObject.AddComponent<CanonicalPlayerProvider>();
        Assert.That(provider.TryResolve(null, out _, out string missingReason), Is.False);
        Assert.That(missingReason, Does.Contain("missing"));

        GameObject duplicate = new GameObject("DuplicatePlayer");
        duplicate.tag = "Player";
        try
        {
            Assert.That(provider.TryResolve(playerObject.transform, out _, out string ambiguousReason), Is.False);
            Assert.That(ambiguousReason, Does.Contain("ambiguous"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(duplicate);
        }
    }

    private CanonicalPlayerProvider CreateProvider()
    {
        CanonicalPlayerProvider provider = runtimeObject.AddComponent<CanonicalPlayerProvider>();
        Assert.That(provider.TryResolve(playerObject.transform, out _, out string reason), Is.True, reason);
        return provider;
    }

    private MapRuntimeContext CommittedContext(MapData map, out Guid generationId)
    {
        MapRuntimeContext context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
        SetPhase(context, MapLifecyclePhase.Initialized);
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult result = orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), 99),
            attempt => new MapPendingGeneration(attempt.GenerationId, map, null),
            (attempt, pending) => new MapPlacementPlan(attempt.GenerationId),
            _ => { });
        Assert.That(result.Succeeded, Is.True);
        generationId = result.GenerationId.Value;
        return context;
    }

    private MapData CreateMap()
    {
        MapData map = new MapData(8, 8, new Vector2Int(10, -4));
        map.SpawnCell = new Vector2Int(12, -2);
        map.ExitCell = new Vector2Int(16, 2);
        return map;
    }

    private void SetUpObjects()
    {
        gridObject = new GameObject("Grid");
        gridObject.AddComponent<Grid>();
        GameObject tilemapObject = new GameObject("Tilemap");
        tilemapObject.transform.SetParent(gridObject.transform);
        tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>();
        GameObject cameraTarget = new GameObject("CameraTarget");
        cameraTarget.transform.SetParent(playerObject.transform);
        runtimeObject = new GameObject("Runtime");
    }

    [SetUp]
    public void SetUp()
    {
        SetUpObjects();
    }

    private static void SetPhase(MapRuntimeContext context, MapLifecyclePhase phase)
    {
        typeof(MapRuntimeContext).GetMethod("SetPhase",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(context, new object[] { phase });
    }
}
