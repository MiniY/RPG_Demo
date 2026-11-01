using System;
using UnityEngine;

/// <summary>Actor-local generation lease. No global mutable routing; stale leases fail closed.</summary>
public sealed class MapSpatialActor : MonoBehaviour
{
    private MapRuntimeContext context;
    private Guid generation;
    private bool legacyRoute;
    public void Bind(MapRuntimeContext owner, Guid expected)
    {
        if (owner == null || owner.ActiveGenerationId != expected || owner.ActiveSpatialContext == null)
            throw new InvalidOperationException("InvalidSpatialActorLease");
        context = owner; generation = expected; legacyRoute = false;
    }
    public void BindLegacy(MapRuntimeContext owner, Guid expected)
    {
        if (owner == null || owner.ActiveGenerationId != expected || owner.ActiveSpatialContext != null)
            throw new InvalidOperationException("InvalidLegacyActorLease");
        context = owner; generation = expected; legacyRoute = true;
    }
    public bool CanMove(Vector2 from, Vector2 to)
    {
        if (context == null || context.ActiveGenerationId != generation || context.IsFailed) return false;
        if (legacyRoute) return context.ActiveSpatialContext == null;
        if (context.ActiveSpatialContext == null) return false;
        return MapSpatialPlacement.CanSweep(context, generation, transform, from, to, out _);
    }
}

public static partial class MapSpatialPlacement
{
    public static bool CanPlace(MapRuntimeContext context, Guid generation, Transform actor, Vector2 destination, out string reason)
        => CanSweep(context, generation, actor, destination, destination, out reason);
    public static bool CanSweep(MapRuntimeContext context, Guid generation, Transform actor, Vector2 from, Vector2 to, out string reason)
    {
        reason = string.Empty;
        if (context == null || context.ActiveGenerationId != generation || context.IsFailed)
        { reason = "StaleSpatialGeneration"; return false; }
        if (context.ActiveSpatialContext == null) return true;
        var geometry = context.ActiveSpatialGeometry;
        if (geometry == null || geometry.GenerationId != generation || actor == null)
        { reason = "SpatialGeometryNotCommitted"; return false; }
        // Capture potential configured shapes, including disabled solids and triggers.
        // A sprite-only entity still has a grounded visual footprint.
        if (!geometry.IsPotentialPoseClear(actor, to)) { reason = "ForbiddenSpatialFootprint"; return false; }
        foreach (Collider2D collider in actor.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.isTrigger || !collider.enabled || collider.attachedRigidbody == null) continue;
            if (!geometry.IsTranslationClear(collider, from, to, 0f)) { reason = "ForbiddenSpatialSweep"; return false; }
        }
        if (from != to && !geometry.IsPlannedTranslationClear(actor, from, to))
        { reason = "ForbiddenPotentialSweep"; return false; }
        return true;
    }
    public static bool Prepare(MapRuntimeContext context, Guid generation, GameObject actor, Vector2 destination, out string reason, string logicalId = null)
    {
        if (context != null && context.ActiveSpatialContext != null && actor != null && logicalId != null) DeclareReservationFootprint(context, actor, logicalId, destination);
        if (!CanPlace(context, generation, actor == null ? null : actor.transform, destination, out reason)) return false;
        if (context.ActiveSpatialContext != null)
        {
            var lease = actor.GetComponent<MapSpatialActor>() ?? actor.AddComponent<MapSpatialActor>();
            lease.Bind(context, generation);
        }
        else if (actor != null && actor.TryGetComponent<MapSpatialActor>(out var previousLease))
            previousLease.BindLegacy(context, generation);
        return true;
    }
}
