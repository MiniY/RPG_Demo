using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapPlacementOwnership { StaticConstraint = 0, SceneBound = 1, GenerationScoped = 2, Persistent = 3 }

public sealed class MapPlacementRequest
{
    private readonly Vector2Int[] footprintOffsets;
    public MapPlacementRequest(string logicalObjectId, string role, IEnumerable<Vector2Int> footprintOffsets,
        bool required = true, bool requiresWalkableCells = true)
    {
        if (string.IsNullOrWhiteSpace(logicalObjectId)) throw new ArgumentException("Logical object ID is required.", nameof(logicalObjectId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
        LogicalObjectId = logicalObjectId; Role = role;
        this.footprintOffsets = footprintOffsets == null ? new[] { Vector2Int.zero } : new List<Vector2Int>(footprintOffsets).ToArray();
        if (this.footprintOffsets.Length == 0) this.footprintOffsets = new[] { Vector2Int.zero };
        Required = required; RequiresWalkableCells = requiresWalkableCells;
    }
    public string LogicalObjectId { get; }
    public string Role { get; }
    public IReadOnlyList<Vector2Int> FootprintOffsets => footprintOffsets;
    public bool Required { get; }
    public bool RequiresWalkableCells { get; }
}

public sealed class MapPlacementReservation
{
    private readonly Vector2Int[] occupiedCells;
    public MapPlacementReservation(string logicalObjectId, string role, Vector2Int anchorCell,
        IEnumerable<Vector2Int> occupiedCells, MapPlacementOwnership ownership)
    {
        if (string.IsNullOrWhiteSpace(logicalObjectId)) throw new ArgumentException("Logical object ID is required.", nameof(logicalObjectId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
        LogicalObjectId = logicalObjectId; Role = role; AnchorCell = anchorCell;
        this.occupiedCells = new List<Vector2Int>(occupiedCells ?? throw new ArgumentNullException(nameof(occupiedCells))).ToArray();
        if (this.occupiedCells.Length == 0) throw new ArgumentException("A reservation must occupy a cell.", nameof(occupiedCells));
        Ownership = ownership;
    }
    public string LogicalObjectId { get; }
    public string Role { get; }
    public Vector2Int AnchorCell { get; }
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
    public MapPlacementOwnership Ownership { get; }
}

public sealed class MapCellOccupancy
{
    private readonly Dictionary<Vector2Int, string> owners = new Dictionary<Vector2Int, string>();
    public int Count => owners.Count;
    public IReadOnlyDictionary<Vector2Int, string> Owners => owners;
    public bool TryReserve(string logicalObjectId, IEnumerable<Vector2Int> cells, out Vector2Int conflict)
    {
        if (string.IsNullOrWhiteSpace(logicalObjectId)) throw new ArgumentException("Logical object ID is required.", nameof(logicalObjectId));
        List<Vector2Int> requested = new List<Vector2Int>(cells ?? throw new ArgumentNullException(nameof(cells)));
        foreach (Vector2Int cell in requested)
            if (owners.TryGetValue(cell, out string owner) && owner != logicalObjectId) { conflict = cell; return false; }
        foreach (Vector2Int cell in requested) owners[cell] = logicalObjectId;
        conflict = default; return true;
    }
    public bool Release(string logicalObjectId)
    {
        List<Vector2Int> released = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, string> pair in owners) if (pair.Value == logicalObjectId) released.Add(pair.Key);
        foreach (Vector2Int cell in released) owners.Remove(cell);
        return released.Count > 0;
    }
    public bool IsOccupied(Vector2Int cell) => owners.ContainsKey(cell);
    public void Clear() => owners.Clear();
    public MapCellOccupancy Copy()
    {
        MapCellOccupancy copy = new MapCellOccupancy();
        foreach (KeyValuePair<Vector2Int, string> pair in owners) copy.owners.Add(pair.Key, pair.Value);
        return copy;
    }
}

public sealed class MapPlacementPlan
{
    private readonly List<MapPlacementReservation> reservations = new List<MapPlacementReservation>();
    private readonly HashSet<string> logicalIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly MapCellOccupancy occupancy = new MapCellOccupancy();
    public MapPlacementPlan(Guid generationId)
    {
        if (generationId == Guid.Empty) throw new ArgumentException("Generation ID cannot be empty.", nameof(generationId));
        GenerationId = generationId;
    }
    public Guid GenerationId { get; }
    public IReadOnlyList<MapPlacementReservation> Reservations => reservations;
    public int ReservationCount => reservations.Count;
    public int OccupiedCellCount => occupancy.Count;
    public bool TryAdd(MapPlacementReservation reservation, out string reason)
    {
        if (reservation == null) throw new ArgumentNullException(nameof(reservation));
        if (!logicalIds.Add(reservation.LogicalObjectId)) { reason = $"Duplicate logical object ID: {reservation.LogicalObjectId}."; return false; }
        if (!occupancy.TryReserve(reservation.LogicalObjectId, reservation.OccupiedCells, out Vector2Int conflict))
        { logicalIds.Remove(reservation.LogicalObjectId); reason = $"Cell {conflict} is already reserved."; return false; }
        reservations.Add(reservation); reason = null; return true;
    }
    public bool IsOccupied(Vector2Int cell) => occupancy.IsOccupied(cell);
    internal MapCellOccupancy CreateOccupancyCopy() => occupancy.Copy();
}

public sealed class MapPlanningException : Exception
{
    public MapPlanningException(string code, string message, bool retryable) : base(message)
    { Code = string.IsNullOrWhiteSpace(code) ? "PlanningFailure" : code; Retryable = retryable; }
    public string Code { get; }
    public bool Retryable { get; }
}

public static class MapStaticPlacementPlanner
{
    public static MapPlacementPlan Build(Guid generationId, MapData map,
        MapSimpleDecorationData decorations, MapGenerationSettings settings)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        MapPlacementPlan plan = new MapPlacementPlan(generationId);
        List<Vector2Int> spawnSafetyCells = new List<Vector2Int>();
        int spawnSafetyRadius = Mathf.Max(0, settings.spawnProtectionRadius);
        for (int x = map.SpawnCell.x - spawnSafetyRadius;
             x <= map.SpawnCell.x + spawnSafetyRadius;
             x++)
        {
            for (int y = map.SpawnCell.y - spawnSafetyRadius;
                 y <= map.SpawnCell.y + spawnSafetyRadius;
                 y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (map.IsInside(cell)) spawnSafetyCells.Add(cell);
            }
        }
        Add(plan, "map.spawn", "SpawnSafetyReservation", map.SpawnCell, spawnSafetyCells);
        if (map.ExitCell != map.SpawnCell)
            Add(plan, "map.exit", "ExitReservation", map.ExitCell, new[] { map.ExitCell });

        if (decorations == null || settings.simpleDecorationPalette == null) return plan;
        Dictionary<string, MapSimpleDecorationVariant> variants = new Dictionary<string, MapSimpleDecorationVariant>(StringComparer.Ordinal);
        foreach (MapSimpleDecorationVariant variant in settings.simpleDecorationPalette.Variants)
            if (variant != null && variant.IsValid()) variants[variant.VariantId] = variant;

        for (int index = 0; index < decorations.Placements.Count; index++)
        {
            MapSimpleDecorationPlacement placement = decorations.Placements[index];
            if (!variants.TryGetValue(placement.VariantId, out MapSimpleDecorationVariant variant) ||
                variant.CollisionOffsets.Count == 0) continue;
            List<Vector2Int> cells = new List<Vector2Int>();
            foreach (Vector2Int offset in variant.CollisionOffsets) cells.Add(placement.AnchorCell + offset);
            Add(plan, $"static-decoration.{index}", "CollisionDecoration", placement.AnchorCell, cells);
        }
        return plan;
    }

    private static void Add(MapPlacementPlan plan, string id, string role, Vector2Int anchor,
        IEnumerable<Vector2Int> cells)
    {
        MapPlacementReservation reservation = new MapPlacementReservation(id, role, anchor, cells,
            MapPlacementOwnership.StaticConstraint);
        if (!plan.TryAdd(reservation, out string reason))
            throw new MapPlanningException("StaticConstraintConflict", reason, false);
    }
}

public static class MapPlacementValidator
{
    public static void Validate(MapData map, MapPlacementPlan plan)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            foreach (Vector2Int cell in reservation.OccupiedCells)
            {
                if (!map.IsInside(cell))
                    throw new MapPlanningException("PlacementOutOfBounds", $"Placement {reservation.LogicalObjectId} uses out-of-bounds cell {cell}.", false);
                if (!map.IsWalkable(cell))
                    throw new MapPlanningException("PlacementNotWalkable", $"Placement {reservation.LogicalObjectId} uses non-walkable cell {cell}.", false);
            }
        }
    }
}
