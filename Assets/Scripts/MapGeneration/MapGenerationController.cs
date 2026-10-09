using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class MapGenerationController : MonoBehaviour
{
    [SerializeField] private MapGenerationSettings settings;
    [SerializeField] private MapTilemapRenderer tilemapRenderer;
    [FormerlySerializedAs("decorationRenderer")]
    [SerializeField] private MapSimpleDecorationRenderer simpleDecorationRenderer;
    [SerializeField] private Transform player;
    [SerializeField] private bool generateOnPlay;
    [SerializeField] private bool logGenerationSummary = true;

    private MapData lastGeneratedMap;
    private MapSimpleDecorationData lastGeneratedSimpleDecorations;
    private MapIntegrationOrchestrator orchestrator;

    public event Action<MapData> MapGenerated;
    public event Action MapCleared;

    public MapGenerationSettings Settings => settings;
    public MapTilemapRenderer TilemapRenderer => tilemapRenderer;
    public MapSimpleDecorationRenderer SimpleDecorationRenderer => simpleDecorationRenderer;
    public Transform Player => player;
    public MapData LastGeneratedMap => lastGeneratedMap;
    public MapSimpleDecorationData LastGeneratedSimpleDecorations => lastGeneratedSimpleDecorations;
    public MapIntegrationOrchestrator Orchestrator
    {
        get { EnsureOrchestrator(); return orchestrator; }
    }

    public void GenerateMap()
    {
        MapRuntimeBootstrap bootstrap = GetComponentInParent<MapRuntimeBootstrap>();
        if (bootstrap != null && !bootstrap.CanRunRandomGeneration)
        {
            Debug.LogWarning($"Random generation is unavailable in {bootstrap.Mode} mode or after bootstrap validation failure.", this);
            return;
        }
        if (settings == null)
        {
            Debug.LogError("MapGenerationController is missing MapGenerationSettings.", this);
            return;
        }
        if (tilemapRenderer == null)
        {
            Debug.LogError("MapGenerationController is missing MapTilemapRenderer.", this);
            return;
        }

        EnsureOrchestrator();
        MerchantPlacementService merchantPlacementService = null;
        MonsterPlacementService monsterPlacementService = null;
        AnimalPlacementService animalPlacementService = null;
        PlantPlacementService plantPlacementService = null;
        if (bootstrap != null)
        {
            MerchantPlacementService[] merchantPlacementServices =
                GetComponentsInParent<MerchantPlacementService>(true);
            if (merchantPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "MerchantPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Required Merchant placement service; " +
                    $"found {merchantPlacementServices.Length}.");
                return;
            }
            merchantPlacementService = merchantPlacementServices[0];

            MonsterPlacementService[] monsterPlacementServices =
                GetComponentsInParent<MonsterPlacementService>(true);
            if (monsterPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "MonsterPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Required Monster placement service; " +
                    $"found {monsterPlacementServices.Length}.");
                return;
            }
            monsterPlacementService = monsterPlacementServices[0];

            AnimalPlacementService[] animalPlacementServices =
                GetComponentsInParent<AnimalPlacementService>(true);
            if (animalPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "AnimalPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Animal semantic placement service; " +
                    $"found {animalPlacementServices.Length}.");
                return;
            }
            animalPlacementService = animalPlacementServices[0];

            PlantPlacementService[] plantPlacementServices =
                GetComponentsInParent<PlantPlacementService>(true);
            if (plantPlacementServices.Length != 1)
            {
                FailPreGenerationConfiguration(
                    "PlantPlacementOwnerCount",
                    $"RandomGenerated requires exactly one Plant semantic placement service; " +
                    $"found {plantPlacementServices.Length}.");
                return;
            }
            plantPlacementService = plantPlacementServices[0];
        }
        MapGenerationRequest request = new MapGenerationRequest(Guid.NewGuid(), settings.seed);
        MapGenerationExecutionResult result = orchestrator.Execute(
            request,
            GeneratePending,
            (attempt, pending) => BuildPlacementPlan(
                attempt,
                pending,
                merchantPlacementService,
                monsterPlacementService,
                animalPlacementService,
                plantPlacementService),
            ProjectCommitted);

        if (!result.Succeeded)
        {
            Debug.LogError($"Map generation failed [{result.Failure.Code}]: {result.Failure.Reason}", this);
            return;
        }

        PlayerSpawnService[] spawnServices = GetComponentsInParent<PlayerSpawnService>(true);
        CanonicalPlayerProvider[] playerProviders = GetComponentsInParent<CanonicalPlayerProvider>(true);
        if (spawnServices.Length != 1)
        {
            FailPlayerSpawnConfiguration(result.GenerationId.Value, "PlayerSpawnOwnerCount",
                $"RandomGenerated requires exactly one PlayerSpawnService; found {spawnServices.Length}.");
            return;
        }
        if (playerProviders.Length != 1)
        {
            FailPlayerSpawnConfiguration(result.GenerationId.Value, "CanonicalPlayerProviderCount",
                $"RandomGenerated requires exactly one CanonicalPlayerProvider; found {playerProviders.Length}.");
            return;
        }

        PlayerSpawnService spawnService = spawnServices[0];
        CanonicalPlayerProvider playerProvider = playerProviders[0];
        if (merchantPlacementService != null &&
            !merchantPlacementService.TryMaterialize(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                out string merchantFailure))
        {
            Debug.LogError($"Required Merchant materialization failed: {merchantFailure}", this);
            return;
        }
        if (monsterPlacementService != null &&
            !monsterPlacementService.TryMaterialize(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                out string monsterFailure))
        {
            Debug.LogError($"Required Monster materialization failed: {monsterFailure}", this);
            return;
        }
        if (animalPlacementService != null &&
            !animalPlacementService.TryMaterialize(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                out string animalFailure))
        {
            Debug.LogError($"Animal materialization failed: {animalFailure}", this);
            return;
        }
        if (plantPlacementService != null &&
            !plantPlacementService.TryMaterialize(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                out string plantFailure))
        {
            Debug.LogError($"Plant materialization failed: {plantFailure}", this);
            return;
        }
        bool projectionReady = tilemapRenderer.TerrainCollisionTilemap != null &&
                               tilemapRenderer.TerrainCollisionTilemap.GetUsedTilesCount() > 0;
        string spawnFailure = null;
        bool spawned = spawnService.TrySpawn(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                projectionReady,
                out spawnFailure);
        if (!spawned)
        {
            Debug.LogError($"Player spawn failed: {spawnFailure}", this);
            return;
        }

        // The standalone MapGeneration test scene intentionally stops at the Stage 3 seam.
        if (bootstrap == null)
            return;

        MapDependentReinitializationService[] reinitializationServices =
            GetComponentsInParent<MapDependentReinitializationService>(true);
        MapGeneratedObjectPlacementAdapter[] placementAdapters =
            GetComponentsInParent<MapGeneratedObjectPlacementAdapter>(true);
        if (reinitializationServices.Length != 1 || placementAdapters.Length != 1)
        {
            FailStageConfiguration(result.GenerationId.Value, MapLifecyclePhase.Reinitializing,
                "ReinitializationOwnerCount",
                $"RandomGenerated requires exactly one reinitialization service and placement adapter; " +
                $"found {reinitializationServices.Length} and {placementAdapters.Length}.");
            return;
        }

        MapDependentReinitializationService reinitializationService = reinitializationServices[0];
        IReadOnlyList<GameObject> reinitializationTargets = BuildReinitializationTargets(
            placementAdapters[0],
            monsterPlacementService,
            animalPlacementService,
            plantPlacementService);
        if (!reinitializationService.TryReinitialize(
                orchestrator.Context,
                result.GenerationId.Value,
                reinitializationTargets,
                out string reinitializationFailure))
        {
            Debug.LogError($"Map-dependent reinitialization failed: {reinitializationFailure}", this);
            return;
        }

        CameraBindingService[] cameraBindingServices =
            GetComponentsInParent<CameraBindingService>(true);
        PlayerFollowCameraProvider[] cameraProviders =
            GetComponentsInParent<PlayerFollowCameraProvider>(true);
        if (cameraBindingServices.Length != 1 || cameraProviders.Length != 1)
        {
            FailStageConfiguration(result.GenerationId.Value, MapLifecyclePhase.BindingCamera,
                "CameraIntegrationOwnerCount",
                $"RandomGenerated requires exactly one CameraBindingService and Player-follow Camera provider; " +
                $"found {cameraBindingServices.Length} and {cameraProviders.Length}.");
            return;
        }

        if (!cameraBindingServices[0].TryBind(
                orchestrator.Context,
                result.GenerationId.Value,
                tilemapRenderer.Coordinates,
                playerProvider,
                player,
                spawnService,
                reinitializationService,
                cameraProviders[0],
                out string cameraFailure))
        {
            Debug.LogError($"Camera binding failed: {cameraFailure}", this);
        }
    }

    public void ClearMap()
    {
        if (tilemapRenderer != null) tilemapRenderer.Clear();
        if (simpleDecorationRenderer != null) simpleDecorationRenderer.Clear();
        lastGeneratedMap = null;
        lastGeneratedSimpleDecorations = null;
        MapCleared?.Invoke();
    }

    // Keep the previous projection intact until a new pending generation commits.
    public void RegenerateMap() => GenerateMap();

    public void RandomizeSeed()
    {
        if (settings == null)
        {
            Debug.LogError("MapGenerationController is missing MapGenerationSettings.", this);
            return;
        }
        settings.seed = Guid.NewGuid().GetHashCode();
    }

    private void Start()
    {
        if (generateOnPlay) GenerateMap();
    }

    private void EnsureOrchestrator()
    {
        if (orchestrator != null) return;
        MapRuntimeBootstrap bootstrap = GetComponentInParent<MapRuntimeBootstrap>();
        MapRuntimeContext context;
        if (bootstrap != null)
        {
            context = bootstrap.Context;
        }
        else
        {
            context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
            context.SetPhase(MapLifecyclePhase.Initialized);
        }
        orchestrator = new MapIntegrationOrchestrator(context);
    }

    private MapPendingGeneration GeneratePending(MapGenerationAttempt attempt)
    {
        MapGenerationSettings attemptSettings = Instantiate(settings);
        attemptSettings.seed = attempt.AttemptSeed;
        try
        {
            MapData map = RandomMapGenerator.Generate(attemptSettings);
            MapSimpleDecorationData decorations = MapSimpleDecorationGenerator.Generate(map, attemptSettings);
            return new MapPendingGeneration(attempt.GenerationId, map, decorations);
        }
        finally
        {
            if (Application.isPlaying) Destroy(attemptSettings);
            else DestroyImmediate(attemptSettings);
        }
    }

    private MapPlacementPlan BuildPlacementPlan(
        MapGenerationAttempt attempt,
        MapPendingGeneration pending,
        MerchantPlacementService merchantPlacementService,
        MonsterPlacementService monsterPlacementService,
        AnimalPlacementService animalPlacementService,
        PlantPlacementService plantPlacementService)
    {
        MapPlacementPlan plan = MapStaticPlacementPlanner.Build(
            attempt.GenerationId,
            pending.Map,
            pending.Decorations,
            settings);
        merchantPlacementService?.Plan(attempt, pending.Map, plan);
        monsterPlacementService?.Plan(attempt, pending.Map, plan);
        animalPlacementService?.Plan(attempt, pending.Map, plan);
        plantPlacementService?.Plan(attempt, pending.Map, plan);
        return plan;
    }

    private static IReadOnlyList<GameObject> BuildReinitializationTargets(
        MapGeneratedObjectPlacementAdapter placementAdapter,
        MonsterPlacementService monsterPlacementService,
        AnimalPlacementService animalPlacementService,
        PlantPlacementService plantPlacementService)
    {
        List<GameObject> targets = new List<GameObject>();
        foreach (GameObject target in placementAdapter.RestartAfterPlacement)
            targets.Add(target);
        if (monsterPlacementService != null)
            targets.Add(monsterPlacementService.ReinitializationTarget);
        if (animalPlacementService != null)
        {
            foreach (GameObject target in animalPlacementService.ReinitializationTargets)
                targets.Add(target);
        }
        if (plantPlacementService != null)
        {
            foreach (GameObject target in plantPlacementService.ReinitializationTargets)
                targets.Add(target);
        }
        return targets.AsReadOnly();
    }

    private void ProjectCommitted(MapPendingGeneration pending)
    {
        lastGeneratedMap = pending.Map;
        lastGeneratedSimpleDecorations = pending.Decorations;
        tilemapRenderer.Render(lastGeneratedMap, settings);
        if (simpleDecorationRenderer != null)
            simpleDecorationRenderer.Render(lastGeneratedSimpleDecorations, lastGeneratedMap, settings);

        MapGenerated?.Invoke(lastGeneratedMap);

        if (logGenerationSummary)
            Debug.Log($"Map generation committed: GenerationId={orchestrator.Context.ActiveGenerationId}; " +
                $"RequestedSeed={settings.seed}; Size={settings.mapWidth}x{settings.mapHeight}; " +
                $"Spawn={lastGeneratedMap.SpawnCell}; Exit={lastGeneratedMap.ExitCell}; " +
                $"Decorations={lastGeneratedSimpleDecorations?.Count ?? 0}.", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (lastGeneratedMap == null || tilemapRenderer == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.SpawnCell), 0.35f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.ExitCell), 0.35f);
    }

    private void FailPlayerSpawnConfiguration(Guid generationId, string code, string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        if (context.Phase == MapLifecyclePhase.Materializing)
            MapLifecycleTransitions.Advance(context, MapLifecyclePhase.SpawningPlayer);
        context.RecordFailure(new MapFailureDiagnostic(context.Mode, generationId, context.Phase,
            MapFailureCategory.Configuration, code, reason));
        Debug.LogError($"Player spawn failed [{code}]: {reason}", this);
    }

    private void FailPreGenerationConfiguration(string code, string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        context.RecordFailure(new MapFailureDiagnostic(
            context.Mode,
            null,
            context.Phase,
            MapFailureCategory.Configuration,
            code,
            reason));
        Debug.LogError($"Map integration failed [{code}]: {reason}", this);
    }

    private void FailStageConfiguration(
        Guid generationId,
        MapLifecyclePhase failurePhase,
        string code,
        string reason)
    {
        MapRuntimeContext context = orchestrator.Context;
        if (failurePhase == MapLifecyclePhase.Reinitializing &&
            context.Phase == MapLifecyclePhase.SpawningPlayer)
        {
            MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Reinitializing);
        }
        else if (failurePhase == MapLifecyclePhase.BindingCamera)
        {
            if (context.Phase == MapLifecyclePhase.SpawningPlayer)
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Reinitializing);
            if (context.Phase == MapLifecyclePhase.Reinitializing)
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.BindingCamera);
        }

        context.RecordFailure(new MapFailureDiagnostic(context.Mode, generationId, context.Phase,
            MapFailureCategory.Configuration, code, reason));
        Debug.LogError($"Map integration failed [{code}]: {reason}", this);
    }
}
