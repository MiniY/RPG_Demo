using System;
using System.Collections.Generic;
using UnityEngine;

namespace RpgDemo.RetainedTerrain.Float32V1
{
/// <summary>Plans two reachable plateaus after the legacy surface and road passes.</summary>
public static class MapElevationPlanner
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public static MapData AddTwoLevels(MapData baseMap, int seed, int protectedRadius, Func<MapData,bool> candidateQualification = null)
    {
        if (baseMap == null)
            throw new ArgumentNullException(nameof(baseMap));
        // A small layout cannot fit two platforms and two safe two-cell stair approaches.
        if (baseMap.Width < 36 || baseMap.Height < 34)
            return baseMap;

        List<Vector2Int> centers = new List<Vector2Int>();
        for (int y = baseMap.Origin.y + 9; y < baseMap.Origin.y + baseMap.Height - 9; y++)
            for (int x = baseMap.Origin.x + 10; x < baseMap.Origin.x + baseMap.Width - 10; x++)
                centers.Add(new Vector2Int(x, y));

        System.Random random = new System.Random(seed ^ 0x354C29B7);
        for (int i = centers.Count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            Vector2Int temp = centers[i];
            centers[i] = centers[swap];
            centers[swap] = temp;
        }

        HashSet<Vector2Int> baseReachable = Reachable(baseMap);
        for (int i = 0; i < centers.Count; i++)
        {
            Vector2Int center = centers[i];
            int radiusX = 5 + PositiveHash(center.x, center.y, seed) % 3;
            int radiusY = 4 + PositiveHash(center.y, center.x, seed) % 2;
            if (TryBuild(baseMap, center, radiusX, radiusY, seed,
                    protectedRadius, baseReachable, out MapData result) &&
                (candidateQualification == null || candidateQualification(result)))
                return result;
        }

        throw new InvalidOperationException(
            "The map has no room for two connected terrain heights and safe stairs.");
    }

    private static bool TryBuild(MapData baseMap, Vector2Int center,
        int radiusX, int radiusY, int seed, int protectedRadius,
        HashSet<Vector2Int> baseReachable, out MapData result)
    {
        result = null;
        HashSet<Vector2Int> first = new HashSet<Vector2Int>();
        HashSet<Vector2Int> second = new HashSet<Vector2Int>();
        Vector2Int secondCenter = center + new Vector2Int(1, 1);
        for (int y = center.y - radiusY; y <= center.y + radiusY; y++)
        {
            for (int x = center.x - radiusX; x <= center.x + radiusX; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                float nx = (float)(x - center.x) / radiusX;
                float ny = (float)(y - center.y) / radiusY;
                float shape = nx * nx + ny * ny;
                float edge = 0.88f + (PositiveHash(x, y, seed) % 17) * 0.01f;
                if (shape > edge)
                    continue;
                if (!baseMap.IsInside(cell) || baseMap.IsBorder(cell, 2) ||
                    baseMap.GetCell(cell).terrainType == MapTerrainType.Path ||
                    Near(cell, baseMap.SpawnCell, protectedRadius + 2) ||
                    Near(cell, baseMap.ExitCell, 5))
                    return false;
                first.Add(cell);

                int dx = x - secondCenter.x;
                int dy = y - secondCenter.y;
                if (dx * dx * 4 + dy * dy * 4 <= 25)
                    second.Add(cell);
            }
        }
        // The upper plateau replaces H1 cells, so validate the final playable H1 area.
        if (first.Count - second.Count < 36 || second.Count < 9)
            return false;

        if (!FindSouthStairPair(baseMap, first, null, baseReachable,
                out Vector2Int firstUpper) ||
            !FindSouthStairPair(baseMap, second, first, null,
                out Vector2Int secondUpper))
            return false;

        MapData candidate = baseMap.Copy();
        foreach (Vector2Int cell in first)
        {
            if (!candidate.IsWalkable(cell))
                candidate.SetTerrain(cell, MapTerrainType.Grass);
            candidate.SetElevation(cell, 1);
        }
        foreach (Vector2Int cell in second)
            candidate.SetElevation(cell, 2);
        for (int x = 0; x < 2; x++)
        {
            Vector2Int offset = new Vector2Int(x, 0);
            candidate.AddStair(firstUpper + offset + Vector2Int.down, firstUpper + offset);
            candidate.AddStair(secondUpper + offset + Vector2Int.down, secondUpper + offset);
        }

        HashSet<Vector2Int> reachable = Reachable(candidate);
        if (!reachable.Contains(candidate.ExitCell) ||
            !reachable.Contains(firstUpper) || !reachable.Contains(secondUpper))
            return false;
        foreach (Vector2Int cell in first)
            if (!reachable.Contains(cell))
                return false;

        int retained = 0;
        foreach (Vector2Int cell in baseReachable)
            if (reachable.Contains(cell))
                retained++;
        if (retained < baseReachable.Count * 95 / 100)
            return false;

        result = candidate;
        return true;
    }

    private static bool FindSouthStairPair(MapData map,
        HashSet<Vector2Int> upperRegion, HashSet<Vector2Int> lowerRegion,
        HashSet<Vector2Int> reachableLower, out Vector2Int upper)
    {
        List<Vector2Int> ordered = new List<Vector2Int>(upperRegion);
        ordered.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        foreach (Vector2Int cell in ordered)
        {
            Vector2Int right = cell + Vector2Int.right;
            Vector2Int behind = cell + Vector2Int.up;
            Vector2Int rightBehind = right + Vector2Int.up;
            Vector2Int lower = cell + Vector2Int.down;
            Vector2Int rightLower = right + Vector2Int.down;
            if (!upperRegion.Contains(right) || !upperRegion.Contains(behind) ||
                !upperRegion.Contains(rightBehind) || upperRegion.Contains(lower) ||
                upperRegion.Contains(rightLower))
                continue;
            if (lowerRegion != null &&
                (!lowerRegion.Contains(lower) || !lowerRegion.Contains(rightLower) ||
                 lowerRegion.Contains(lower + Vector2Int.down) == false ||
                 lowerRegion.Contains(rightLower + Vector2Int.down) == false))
                continue;
            if (lowerRegion == null &&
                (!map.IsInside(lower) || !map.IsInside(rightLower) ||
                 !map.IsWalkable(lower) || !map.IsWalkable(rightLower) ||
                 !map.IsInside(lower + Vector2Int.down) ||
                 !map.IsWalkable(lower + Vector2Int.down) ||
                 !map.IsWalkable(rightLower + Vector2Int.down) ||
                 !reachableLower.Contains(lower) || !reachableLower.Contains(rightLower)))
                continue;
            upper = cell;
            return true;
        }
        upper = default;
        return false;
    }

    private static HashSet<Vector2Int> Reachable(MapData map)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        if (!map.IsInside(map.SpawnCell) || !map.IsWalkable(map.SpawnCell))
            return visited;
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        visited.Add(map.SpawnCell);
        queue.Enqueue(map.SpawnCell);
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (visited.Contains(next) || !map.CanTraverse(current, next))
                    continue;
                visited.Add(next);
                queue.Enqueue(next);
            }
        }
        return visited;
    }

    private static bool Near(Vector2Int a, Vector2Int b, int radius) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) <= radius;

    private static int PositiveHash(int x, int y, int seed)
    {
        unchecked
        {
            uint value = (uint)seed ^ ((uint)x * 0x9E3779B9u) ^
                ((uint)y * 0x85EBCA6Bu);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            return (int)(value & 0x7FFFFFFF);
        }
    }
}

}
