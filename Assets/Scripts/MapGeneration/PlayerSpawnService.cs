using System;
using UnityEngine;

/// <summary>Owns the one generation-aware initial Player relocation in RandomGenerated mode.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawnService : MonoBehaviour
{
    public event Action<Guid> PlayerReady;

    public int SpawnCount { get; private set; }
    public Guid? ReadyGenerationId { get; private set; }
    public Vector3 LastTeleportDelta { get; private set; }

    public bool TrySpawn(
        MapRuntimeContext context,
        Guid generationId,
        MapCoordinateBoundary coordinates,
        CanonicalPlayerProvider playerProvider,
        Transform configuredPlayer,
        bool projectionAndCollisionReady,
        out string reason)
    {
        reason = string.Empty;
        if (context == null)
            return Fail(null, generationId, MapFailureCategory.Configuration, "MissingRuntimeContext",
                "Player spawn requires the active runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration, "WrongRuntimeMode",
                "Player spawn is only owned by the RandomGenerated production path.", out reason);
        if (context.ActiveGenerationId != generationId || context.ActiveMap == null)
            return Fail(context, generationId, MapFailureCategory.StaleResult, "StalePlayerSpawn",
                "Player spawn rejected a stale Generation ID.", out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle, "DuplicatePlayerSpawn",
                "Player spawn rejected a duplicate operation for the Current Generation.", out reason);
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle, "IllegalPlayerSpawnPhase",
                $"Player spawn requires Materializing; current phase is {context.Phase}.", out reason);

        MapLifecycleTransitions.Advance(context, MapLifecyclePhase.SpawningPlayer);
        if (!projectionAndCollisionReady)
            return Fail(context, generationId, MapFailureCategory.Generation, "ProjectionNotReady",
                "Player spawn requires the committed map projection and collision to be ready.", out reason);
        if (coordinates == null || playerProvider == null)
            return Fail(context, generationId, MapFailureCategory.Configuration, "MissingSpawnDependency",
                "Player spawn requires the shared coordinate boundary and Canonical Player provider.", out reason);

        MapData map = context.ActiveMap;
        Vector2Int spawnCell = map.SpawnCell;
        if (!map.IsInside(spawnCell) || !map.IsWalkable(spawnCell))
            return Fail(context, generationId, MapFailureCategory.Planning, "InvalidSpawnCell",
                "MapData.SpawnCell must be inside the committed map and walkable.", out reason);
        if (!playerProvider.TryResolve(configuredPlayer, out CanonicalPlayerBinding player, out string bindingReason))
            return Fail(context, generationId, MapFailureCategory.Configuration, "CanonicalPlayerUnavailable",
                bindingReason, out reason);

        Vector3 destination;
        try
        {
            destination = coordinates.CellToWorld(map, spawnCell);
        }
        catch (Exception exception)
        {
            return Fail(context, generationId, MapFailureCategory.Coordinate, "SpawnCoordinateConversionFailed",
                exception.Message, out reason);
        }

        destination.z = player.Player.position.z;
        Vector3 previousPosition = player.Player.position;
        ResetSpatialTransitionState(player);
        player.Body.position = new Vector2(destination.x, destination.y);
        player.Player.position = destination;
        Physics2D.SyncTransforms();

        if (Vector2.Distance(player.Body.position, destination) > 0.01f ||
            Vector2.Distance(player.Player.position, destination) > 0.01f)
            return Fail(context, generationId, MapFailureCategory.Generation, "PlayerPhysicsSyncFailed",
                "Player Transform and Rigidbody2D did not synchronize to the committed SpawnCell.", out reason);

        SpawnCount++;
        ReadyGenerationId = generationId;
        LastTeleportDelta = destination - previousPosition;
        PlayerReady?.Invoke(generationId);
        return true;
    }

    private static void ResetSpatialTransitionState(CanonicalPlayerBinding player)
    {
        player.Body.velocity = Vector2.zero;
        player.Body.angularVelocity = 0f;
        IMapTransitionResettable[] resettable =
            player.Player.GetComponents<IMapTransitionResettable>();
        foreach (IMapTransitionResettable component in resettable)
            component.ResetMapTransitionState();
    }

    private static bool Fail(
        MapRuntimeContext context,
        Guid generationId,
        MapFailureCategory category,
        string code,
        string message,
        out string reason)
    {
        reason = message;
        if (context != null)
        {
            MapFailureDiagnostic failure = new MapFailureDiagnostic(context.Mode, generationId,
                context.Phase, category, code, message);
            context.RecordFailure(failure);
        }
        return false;
    }
}
