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
    {
        position = origin;
        if (context == null || context.ActiveGenerationId != generation || context.ActiveSpatialContext == null || context.ActiveSpatialGeometry == null) return false;
        var coordinates = context.ActiveSpatialContext.Coordinates; var map = context.ActiveMap;
        int level = map.GetElevation(coordinates.WorldToCell(origin));
        bool Legal(Vector2 candidate) => map.IsInside(coordinates.WorldToCell(candidate)) && map.GetElevation(coordinates.WorldToCell(candidate)) == level &&
            context.ActiveSpatialGeometry.IsOriginalPickupPoseClear(prefab, candidate);
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
