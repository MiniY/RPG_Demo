using System;
using System.Collections.Generic;
using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MapCameraBindingServiceTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private Tilemap tilemap;
    private GameObject playerObject;
    private GameObject runtimeObject;
    private CanonicalPlayerProvider playerProvider;
    private PlayerSpawnService playerSpawnService;
    private MapDependentReinitializationService reinitializationService;
    private PlayerFollowCameraProvider cameraProvider;
    private CameraBindingService cameraBindingService;
    private CinemachineVirtualCamera virtualCamera;
    private PolygonCollider2D boundsCollider;

    [SetUp]
    public void SetUp()
    {
        GameObject grid = Track(new GameObject("Grid"));
        grid.AddComponent<Grid>();
        GameObject tilemapObject = Track(new GameObject("Tilemap"));
        tilemapObject.transform.SetParent(grid.transform);
        tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();

        playerObject = Track(new GameObject("Player"));
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>();
        GameObject cameraTarget = Track(new GameObject("CameraTarget"));
        cameraTarget.transform.SetParent(playerObject.transform);

        runtimeObject = Track(new GameObject("Runtime"));
        playerProvider = runtimeObject.AddComponent<CanonicalPlayerProvider>();
        playerSpawnService = runtimeObject.AddComponent<PlayerSpawnService>();
        reinitializationService = runtimeObject.AddComponent<MapDependentReinitializationService>();
        cameraProvider = runtimeObject.AddComponent<PlayerFollowCameraProvider>();
        cameraBindingService = runtimeObject.AddComponent<CameraBindingService>();

        GameObject cameraObject = Track(new GameObject("VCam_Player"));
        virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
        CinemachineConfiner2D confiner = cameraObject.AddComponent<CinemachineConfiner2D>();
        GameObject boundsObject = Track(new GameObject("CurrentMapBounds"));
        boundsCollider = boundsObject.AddComponent<PolygonCollider2D>();
        boundsCollider.isTrigger = true;
        confiner.m_BoundingShape2D = boundsCollider;
        SetField(cameraProvider, "playerFollowCamera", virtualCamera);
        SetField(cameraProvider, "playerFollowConfiner", confiner);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null) UnityEngine.Object.DestroyImmediate(objects[index]);
        }
        objects.Clear();
    }

    [Test]
    public void BindingRepairsFollowAppliesCurrentBoundsRefreshesTrackingAndPublishesCameraReady()
    {
        MapData map = CreateMap();
        MapRuntimeContext context = CommittedContext(map, out Guid generationId);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        SpawnAndReinitialize(context, generationId, coordinates);
        virtualCamera.Follow = runtimeObject.transform;
        int readyCount = 0;
        Guid readyGeneration = Guid.Empty;
        cameraBindingService.CameraReady += id => { readyCount++; readyGeneration = id; };

        bool bound = cameraBindingService.TryBind(
            context, generationId, coordinates, playerProvider, playerObject.transform,
            playerSpawnService, reinitializationService, cameraProvider, out string reason);

        Bounds expectedBounds = coordinates.CellBoundsToWorld(map);
        Transform cameraTarget = playerObject.transform.Find("CameraTarget");
        Assert.That(bound, Is.True, reason);
        Assert.That(virtualCamera.Follow, Is.EqualTo(cameraTarget));
        Assert.That(cameraBindingService.ReadyFollowTarget, Is.EqualTo(cameraTarget));
        Assert.That(cameraBindingService.ReadyGenerationId, Is.EqualTo(generationId));
        AssertBounds(cameraBindingService.AppliedWorldBounds, expectedBounds);
        AssertBounds(boundsCollider.bounds, expectedBounds);
        Assert.That(cameraBindingService.BindingCount, Is.EqualTo(1));
        Assert.That(cameraBindingService.TrackingRefreshCount, Is.EqualTo(1));
        Assert.That(readyCount, Is.EqualTo(1));
        Assert.That(readyGeneration, Is.EqualTo(generationId));
        Assert.That(context.Phase, Is.EqualTo(MapLifecyclePhase.BindingCamera));
        Assert.That(context.IsReady, Is.False, "CameraReady must not publish GameplayReady.");
    }

    [Test]
    public void BindingRejectsStalePlayerReadyWithoutPublishingCameraReady()
    {
        MapRuntimeContext context = CommittedContext(CreateMap(), out Guid generationId);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        SpawnAndReinitialize(context, generationId, coordinates);
        GameObject staleOwner = Track(new GameObject("StaleSpawnOwner"));
        PlayerSpawnService staleSpawnService = staleOwner.AddComponent<PlayerSpawnService>();
        int readyCount = 0;
        cameraBindingService.CameraReady += _ => readyCount++;

        bool bound = cameraBindingService.TryBind(
            context, generationId, coordinates, playerProvider, playerObject.transform,
            staleSpawnService, reinitializationService, cameraProvider, out string reason);

        Assert.That(bound, Is.False);
        Assert.That(reason, Does.Contain("PlayerReady"));
        Assert.That(readyCount, Is.Zero);
        Assert.That(context.IsFailed, Is.True);
    }

    [Test]
    public void BindingRejectsMissingRoleAndInvalidCurrentBounds()
    {
        MapRuntimeContext missingRoleContext = CommittedContext(CreateMap(), out Guid missingRoleGeneration);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        SpawnAndReinitialize(missingRoleContext, missingRoleGeneration, coordinates);

        bool missingRoleBound = cameraBindingService.TryBind(
            missingRoleContext, missingRoleGeneration, coordinates, playerProvider, playerObject.transform,
            playerSpawnService, reinitializationService, null, out string missingRoleReason);

        Assert.That(missingRoleBound, Is.False);
        Assert.That(missingRoleReason, Does.Contain("provider"));
        Assert.That(missingRoleContext.IsFailed, Is.True);

        MapRuntimeContext badBoundsContext = CommittedContext(CreateMap(), out Guid badBoundsGeneration);
        PlayerSpawnService nextSpawnService = Track(new GameObject("NextSpawnOwner")).AddComponent<PlayerSpawnService>();
        MapDependentReinitializationService nextReinitialization =
            Track(new GameObject("NextReinitializationOwner")).AddComponent<MapDependentReinitializationService>();
        Assert.That(nextSpawnService.TrySpawn(
            badBoundsContext, badBoundsGeneration, coordinates, playerProvider,
            playerObject.transform, true, out string spawnReason), Is.True, spawnReason);
        Assert.That(nextReinitialization.TryReinitialize(
            badBoundsContext, badBoundsGeneration, Array.Empty<GameObject>(), out string reinitializeReason),
            Is.True, reinitializeReason);
        tilemap.transform.parent.localScale = new Vector3(0f, 1f, 1f);

        bool badBoundsBound = cameraBindingService.TryBind(
            badBoundsContext, badBoundsGeneration, coordinates, playerProvider, playerObject.transform,
            nextSpawnService, nextReinitialization, cameraProvider, out string badBoundsReason);

        Assert.That(badBoundsBound, Is.False);
        Assert.That(badBoundsReason, Does.Contain("Bounds"));
        Assert.That(badBoundsContext.IsFailed, Is.True);
    }

    [Test]
    public void ReinitializationRestartsExplicitTargetAfterPlayerReady()
    {
        MapRuntimeContext context = CommittedContext(CreateMap(), out Guid generationId);
        MapCoordinateBoundary coordinates = new MapCoordinateBoundary(tilemap);
        Assert.That(playerSpawnService.TrySpawn(
            context, generationId, coordinates, playerProvider,
            playerObject.transform, true, out string spawnReason), Is.True, spawnReason);
        GameObject target = Track(new GameObject("MapDependentTarget"));
        MapReinitializationProbe probe = target.AddComponent<MapReinitializationProbe>();
        int enablesBefore = probe.EnableCount;

        bool reinitialized = reinitializationService.TryReinitialize(
            context, generationId, new[] { target }, out string reason);

        Assert.That(reinitialized, Is.True, reason);
        Assert.That(probe.EnableCount, Is.EqualTo(enablesBefore + 1));
        Assert.That(reinitializationService.ReadyGenerationId, Is.EqualTo(generationId));
        Assert.That(reinitializationService.LastReinitializedObjectCount, Is.EqualTo(1));
        Assert.That(context.Phase, Is.EqualTo(MapLifecyclePhase.Reinitializing));
    }

    private void SpawnAndReinitialize(
        MapRuntimeContext context,
        Guid generationId,
        MapCoordinateBoundary coordinates)
    {
        Assert.That(playerSpawnService.TrySpawn(
            context, generationId, coordinates, playerProvider,
            playerObject.transform, true, out string spawnReason), Is.True, spawnReason);
        Assert.That(reinitializationService.TryReinitialize(
            context, generationId, Array.Empty<GameObject>(), out string reinitializeReason),
            Is.True, reinitializeReason);
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

    private static MapData CreateMap()
    {
        MapData map = new MapData(8, 8, new Vector2Int(10, -4));
        map.SpawnCell = new Vector2Int(12, -2);
        map.ExitCell = new Vector2Int(16, 2);
        return map;
    }

    private GameObject Track(GameObject gameObject)
    {
        objects.Add(gameObject);
        return gameObject;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void AssertBounds(Bounds actual, Bounds expected)
    {
        Assert.That(actual.min.x, Is.EqualTo(expected.min.x).Within(0.01f));
        Assert.That(actual.min.y, Is.EqualTo(expected.min.y).Within(0.01f));
        Assert.That(actual.max.x, Is.EqualTo(expected.max.x).Within(0.01f));
        Assert.That(actual.max.y, Is.EqualTo(expected.max.y).Within(0.01f));
    }

    private static void SetPhase(MapRuntimeContext context, MapLifecyclePhase phase)
    {
        typeof(MapRuntimeContext).GetMethod("SetPhase",
            BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(context, new object[] { phase });
    }
}

public sealed class MapReinitializationProbe : MonoBehaviour, IMapDependentReinitializable
{
    public int EnableCount { get; private set; }

    public void ReinitializeForMap()
    {
        EnableCount++;
    }
}
