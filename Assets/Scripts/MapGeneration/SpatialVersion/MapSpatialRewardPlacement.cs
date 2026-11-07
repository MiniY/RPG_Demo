using System;
using UnityEngine;

public static class MapSpatialRewardPlacement
{
    public static bool UsesImplicitPickupCircle(Transform actor)
    {
        if(actor==null)throw new ArgumentNullException(nameof(actor));
        if(actor.GetComponent<Collider2D>()!=null)return false;
        if(actor.GetComponentsInChildren<Collider2D>(true).Length!=0)throw new InvalidOperationException("ImplicitPickupCircleWithChildGeometryUnsupported");
        return true;
    }
    public static void PrepareOriginalPickupCollider(GameObject actor)
    {
        if(actor==null || actor.activeInHierarchy || actor.activeSelf)throw new InvalidOperationException("OriginalPickupPreparationRequiresInactiveLease");
        Collider2D collider=actor.GetComponent<Collider2D>();
        if(collider==null)
        {
            UsesImplicitPickupCircle(actor.transform);
            var circle=actor.AddComponent<CircleCollider2D>();
            if(circle.radius!=.5f || circle.offset!=Vector2.zero)throw new InvalidOperationException("NativeOriginalPickupDefaultChanged");
            collider=circle;
        }
        collider.isTrigger=true;
    }

    public static bool TryFind(MapRuntimeContext context, Guid generation, Transform prefab, Vector2 preferred, Vector2 origin, out Vector2 position)
        => TryFind(context, generation, prefab, preferred, origin, null, out position);

    /// <summary>Uses existing terrain geometry and excludes every live player pickup contact, without a timer.</summary>
    public static bool TryFindForPlayer(MapRuntimeContext context, Guid generation, Transform prefab,
        Transform player, Vector2 direction, out Vector2 position)
    {
        position = player != null ? (Vector2)player.position : Vector2.zero;
        if (player == null || context == null || context.ActiveGenerationId != generation ||
            context.ActiveSpatialGeometry == null || context.ActiveSpatialContext == null) return false;
        bool hasPlayerShape = false;
        Bounds playerBounds = default;
        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated) continue;
            if (!hasPlayerShape) playerBounds = collider.bounds;
            else playerBounds.Encapsulate(collider.bounds);
            hasPlayerShape = true;
        }
        if (!hasPlayerShape) return false;
        Rect pickup = context.ActiveSpatialGeometry.GetOriginalPickupBounds(prefab);
        Vector2 origin = player.position;
        Vector2 facing = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
        float distance = ((Vector2)playerBounds.size).magnitude * .5f + pickup.size.magnitude * .5f + .15f;
        bool OutsidePlayer(Vector2 candidate) =>
            candidate.x + pickup.xMin > playerBounds.max.x + .04f || candidate.x + pickup.xMax < playerBounds.min.x - .04f ||
            candidate.y + pickup.yMin > playerBounds.max.y + .04f || candidate.y + pickup.yMax < playerBounds.min.y - .04f;
        return TryFind(context, generation, prefab, origin + facing * distance, origin, OutsidePlayer, out position);
    }

    private static bool TryFind(MapRuntimeContext context, Guid generation, Transform prefab, Vector2 preferred,
        Vector2 origin, Predicate<Vector2> extraConstraint, out Vector2 position)
    {
        position = origin;
        if (context == null || context.ActiveGenerationId != generation || context.ActiveSpatialContext == null || context.ActiveSpatialGeometry == null) return false;
        var coordinates = context.ActiveSpatialContext.Coordinates; var map = context.ActiveMap;
        int level = map.GetElevation(coordinates.WorldToCell(origin));
        bool Legal(Vector2 candidate) => map.IsInside(coordinates.WorldToCell(candidate)) && map.GetElevation(coordinates.WorldToCell(candidate)) == level &&
            context.ActiveSpatialGeometry.IsOriginalPickupPoseClear(prefab, candidate) && (extraConstraint == null || extraConstraint(candidate));
        if (Legal(preferred)) { position = preferred; return true; }
        if (Legal(origin)) return true;
        for (int ring = 1; ring <= 8; ring++) for (int y = -ring; y <= ring; y++) for (int x = -ring; x <= ring; x++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring) continue;
            Vector2 p = origin + new Vector2(x, y) * .25f;
            if (Legal(p)) { position = p; return true; }
        }
        return false;
    }
}
