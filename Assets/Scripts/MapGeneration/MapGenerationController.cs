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
    [SerializeField] private bool movePlayerToSpawn = true;
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
            Debug.LogError($"Map generation failed [{result.Failure.Code}]: {result.Failure.Reason}", this);
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

        // Stage 3 will replace this compatibility spawn behavior with PlayerSpawnService.
        if (movePlayerToSpawn) MovePlayerToSpawn(lastGeneratedMap);
        MapGenerated?.Invoke(lastGeneratedMap);

        if (logGenerationSummary)
            Debug.Log($"Map generation committed: GenerationId={orchestrator.Context.ActiveGenerationId}; " +
                $"RequestedSeed={settings.seed}; Size={settings.mapWidth}x{settings.mapHeight}; " +
                $"Spawn={lastGeneratedMap.SpawnCell}; Exit={lastGeneratedMap.ExitCell}; " +
                $"Decorations={lastGeneratedSimpleDecorations?.Count ?? 0}.", this);
    }

    private void MovePlayerToSpawn(MapData mapData)
    {
        if (player == null || tilemapRenderer == null) return;
        Vector3 position = tilemapRenderer.Coordinates.CellToWorld(mapData, mapData.SpawnCell);
        position.z = player.position.z;
        player.position = position;
    }

    private void OnDrawGizmosSelected()
    {
        if (lastGeneratedMap == null || tilemapRenderer == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.SpawnCell), 0.35f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(tilemapRenderer.Coordinates.CellToWorld(lastGeneratedMap, lastGeneratedMap.ExitCell), 0.35f);
    }
}
