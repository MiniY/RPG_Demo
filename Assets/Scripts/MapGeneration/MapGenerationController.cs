using System;
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
        MapGenerationRequest request = new MapGenerationRequest(Guid.NewGuid(), settings.seed);
        MapGenerationExecutionResult result = orchestrator.Execute(
            request,
            GeneratePending,
            (attempt, pending) => MapStaticPlacementPlanner.Build(
                attempt.GenerationId, pending.Map, pending.Decorations, settings),
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
}
