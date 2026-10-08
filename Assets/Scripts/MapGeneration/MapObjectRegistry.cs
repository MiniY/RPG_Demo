using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapObjectRegistryEntry
{
    internal MapObjectRegistryEntry(Guid generationId, string logicalObjectId, string role,
        UnityEngine.Object instance, Vector2Int initialCell, MapPlacementOwnership ownership,
        IReadOnlyList<Vector2Int> occupiedCells)
    { GenerationId = generationId; LogicalObjectId = logicalObjectId; Role = role; Instance = instance;
      InitialCell = initialCell; Ownership = ownership; OccupiedCells = occupiedCells; }
    public Guid GenerationId { get; }
    public string LogicalObjectId { get; }
    public string Role { get; }
    public UnityEngine.Object Instance { get; }
    public Vector2Int InitialCell { get; }
    public MapPlacementOwnership Ownership { get; }
    public IReadOnlyList<Vector2Int> OccupiedCells { get; }
}

public sealed class MapObjectRegistry
{
    private readonly Dictionary<string, MapObjectRegistryEntry> entries = new Dictionary<string, MapObjectRegistryEntry>(StringComparer.Ordinal);
    private readonly MapCellOccupancy occupancy;
    private bool retired;
    public MapObjectRegistry(Guid generationId) : this(generationId, new MapCellOccupancy()) { }
    internal MapObjectRegistry(Guid generationId, MapCellOccupancy occupancy)
    {
        if (generationId == Guid.Empty) throw new ArgumentException("Generation ID cannot be empty.", nameof(generationId));
        GenerationId = generationId; this.occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
    }
    public Guid GenerationId { get; }
    public int Count => entries.Count;
    public bool IsRetired => retired;
    public IReadOnlyCollection<MapObjectRegistryEntry> Entries => entries.Values;
    public bool TryRegister(Guid generationId, string logicalObjectId, string role, UnityEngine.Object instance,
        Vector2Int initialCell, MapPlacementOwnership ownership, IEnumerable<Vector2Int> occupiedCells, out string reason)
    {
        if (retired) { reason = "Registry is retired."; return false; }
        if (generationId != GenerationId) { reason = "Generation mismatch."; return false; }
        if (string.IsNullOrWhiteSpace(logicalObjectId) || string.IsNullOrWhiteSpace(role)) { reason = "Logical object ID and role are required."; return false; }
        if (entries.ContainsKey(logicalObjectId)) { reason = $"Duplicate logical object ID: {logicalObjectId}."; return false; }
        List<Vector2Int> cells = new List<Vector2Int>(occupiedCells ?? Array.Empty<Vector2Int>());
        if (!occupancy.TryReserve(logicalObjectId, cells, out Vector2Int conflict)) { reason = $"Cell {conflict} is occupied."; return false; }
        entries.Add(logicalObjectId, new MapObjectRegistryEntry(generationId, logicalObjectId, role, instance,
            initialCell, ownership, cells.AsReadOnly())); reason = null; return true;
    }
    public bool Unregister(Guid generationId, string logicalObjectId)
    {
        if (retired || generationId != GenerationId || !entries.Remove(logicalObjectId)) return false;
        occupancy.Release(logicalObjectId); return true;
    }
    public bool TryBindInstance(Guid generationId, string logicalObjectId, string role,
        UnityEngine.Object instance, out string reason)
    {
        if (retired) { reason = "Registry is retired."; return false; }
        if (generationId != GenerationId) { reason = "Generation mismatch."; return false; }
        if (instance == null) { reason = "Runtime instance is required."; return false; }
        if (!entries.TryGetValue(logicalObjectId, out MapObjectRegistryEntry entry))
        { reason = $"Logical object ID is not registered: {logicalObjectId}."; return false; }
        if (!string.Equals(entry.Role, role, StringComparison.Ordinal))
        { reason = $"Role mismatch for {logicalObjectId}."; return false; }
        if (entry.Instance != null && entry.Instance != instance)
        { reason = $"Runtime instance is already bound for {logicalObjectId}."; return false; }
        entries[logicalObjectId] = new MapObjectRegistryEntry(
            entry.GenerationId, entry.LogicalObjectId, entry.Role, instance,
            entry.InitialCell, entry.Ownership, entry.OccupiedCells);
        reason = null;
        return true;
    }
    public bool TryGet(string logicalObjectId, out MapObjectRegistryEntry entry) =>
        entries.TryGetValue(logicalObjectId, out entry);
    public bool IsOccupied(Vector2Int cell) => occupancy.IsOccupied(cell);
    public bool Retire(Guid generationId)
    {
        if (generationId != GenerationId || retired) return false;
        entries.Clear(); occupancy.Clear(); retired = true; return true;
    }
}
