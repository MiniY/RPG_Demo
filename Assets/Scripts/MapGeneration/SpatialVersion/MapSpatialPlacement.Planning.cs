using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class MapSpatialPlacement
{
    private static bool HasPhysicalDeclaration(Transform actor)
    {
        if (actor.GetComponentsInChildren<Collider2D>(true).Length != 0) return true;
        var declaration = actor.GetComponent<MapGroundFootprint>();
        // A previous generation's derived rectangle must not become authored geometry.
        return declaration != null && !declaration.IsReservationDerived;
    }
    private static void DeclareReservationFootprint(MapRuntimeContext context, GameObject actor, string logicalId, Vector2 destination)
    {
        if (HasPhysicalDeclaration(actor.transform)) return;
        var reservation = context.ActivePlacementPlan.Reservations.Single(r => r.LogicalObjectId == logicalId);
        Vector2[] vertices = reservation.Role == "ExitClearanceReservation" ? CellRectangle(reservation.AnchorCell, context.ActiveSpatialContext.Coordinates) : ReservedRectangle(reservation, context.ActiveSpatialContext.Coordinates);
        var declaration = actor.GetComponent<MapGroundFootprint>() ?? actor.AddComponent<MapGroundFootprint>();
        Matrix4x4 pose = Matrix4x4.TRS(destination, actor.transform.rotation, actor.transform.lossyScale);
        declaration.ConfigureReservation(vertices.Select(p => (Vector2)pose.inverse.MultiplyPoint3x4(p)).ToArray());
    }
    // The retained Portal has no collider. Its ground contact is the ExitCell;
    // the larger ExitClearanceReservation remains spacing for interaction.
    private static Vector2[] CellRectangle(Vector2Int cell, MapCoordinateBoundary coordinates)
    {
        Vector2 centre = coordinates.CellToWorld(cell);
        Vector2 dx = ((Vector2)coordinates.CellToWorld(cell + Vector2Int.right) - centre) / 2;
        Vector2 dy = ((Vector2)coordinates.CellToWorld(cell + Vector2Int.up) - centre) / 2;
        return new[] { centre - dx - dy, centre + dx - dy, centre + dx + dy, centre - dx + dy };
    }
    public static void ValidateExitGround(MapCandidateCollisionGeometry geometry, MapData map, MapCoordinateBoundary coordinates)
    {
        if (!geometry.IsWorldGroundPolygonClear(CellRectangle(map.ExitCell, coordinates)))
            throw new MapPlanningException("CriticalRouteBlocked", "VNext Portal ground footprint rejected before entity planning.", false);
    }
    private static Vector2[] ReservedRectangle(MapPlacementReservation reservation, MapCoordinateBoundary coordinates)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        foreach (var cell in reservation.OccupiedCells)
        {
            Vector2 centre = coordinates.CellToWorld(cell);
            Vector2 dx = ((Vector2)coordinates.CellToWorld(cell + Vector2Int.right) - centre) / 2;
            Vector2 dy = ((Vector2)coordinates.CellToWorld(cell + Vector2Int.up) - centre) / 2;
            min = Vector2.Min(min, centre - dx - dy); max = Vector2.Max(max, centre + dx + dy);
        }
        return new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
    }
    public static void ValidatePlannedRoles(MapCandidateCollisionGeometry geometry, MapPlacementPlan plan, IDictionary<string, Transform> sources, MapCoordinateBoundary coordinates)
    {
        // The original owners may legally omit optional bindings (including ecology mushrooms).
        // Their accepted plan, rather than every authored binding, defines the materialized roles.
        foreach (var reservation in plan.Reservations)
        {
            if (!sources.TryGetValue(reservation.LogicalObjectId, out Transform source)) continue;
            Vector2 destination = coordinates.CellToWorld(reservation.AnchorCell);
            bool clear = HasPhysicalDeclaration(source) ? geometry.IsPotentialPoseClear(source, destination) : geometry.IsWorldGroundPolygonClear(ReservedRectangle(reservation, coordinates));
            if (!clear) throw new MapPlanningException("JointSolidOverlap", "VNext ground footprint rejected before commit: " + reservation.LogicalObjectId, false);
        }
    }
}
