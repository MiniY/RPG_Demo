using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared deterministic primitives for role-specific semantic placement policies.</summary>
internal static class MapSemanticPlacementPrimitives
{
    internal static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };

    internal static List<Vector2Int> BuildFootprint(
        Vector2Int anchor,
        IReadOnlyList<Vector2Int> offsets)
    {
        List<Vector2Int> cells = new List<Vector2Int>(offsets.Count);
        foreach (Vector2Int offset in offsets)
            cells.Add(anchor + offset);
        return cells;
    }

    internal static HashSet<Vector2Int> CollectNavigationBlockers(
        MapPlacementPlan plan,
        params string[] blockingRoles)
    {
        HashSet<string> roles = new HashSet<string>(
            blockingRoles ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (!roles.Contains(reservation.Role))
                continue;
            foreach (Vector2Int cell in reservation.OccupiedCells)
                blocked.Add(cell);
        }
        return blocked;
    }

    internal static Dictionary<Vector2Int, int> BuildWalkableDistances(
        MapData map,
        Vector2Int start,
        HashSet<Vector2Int> blocked)
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        if (!map.IsInside(start) || !map.IsWalkable(start) || blocked.Contains(start))
            return distances;

        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        distances.Add(start, 0);
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            int nextDistance = distances[current] + 1;
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (!map.IsInside(next) || !map.IsWalkable(next) ||
                    blocked.Contains(next) || distances.ContainsKey(next))
                {
                    continue;
                }
                distances.Add(next, nextDistance);
                pending.Enqueue(next);
            }
        }
        return distances;
    }

    internal static uint StableScore(
        int mapSeed,
        string role,
        string logicalObjectId,
        Vector2Int cell)
    {
        unchecked
        {
            uint hash = 2166136261;
            Mix(ref hash, mapSeed);
            Mix(ref hash, role);
            Mix(ref hash, logicalObjectId);
            Mix(ref hash, cell.x);
            Mix(ref hash, cell.y);
            return hash;
        }
    }

    internal static int CompareCells(Vector2Int left, Vector2Int right)
    {
        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
    }

    internal static string FormatRejections(Dictionary<string, int> rejections)
    {
        if (rejections.Count == 0)
            return "NoCellsEvaluated";
        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> rejection in rejections)
            parts.Add($"{rejection.Key}:{rejection.Value}");
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619;
        }
    }

    private static void Mix(ref uint hash, string value)
    {
        foreach (char character in value)
            Mix(ref hash, character);
    }
}
