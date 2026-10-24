using System.Collections.Generic;
using UnityEngine;

/// <summary>Reads the committed map's height for world-space combat checks.</summary>
public static class MapElevationRuntimeQuery
{
    private static MapGenerationController controller;

    public static bool SameLevel(Vector3 first, Vector3 second)
    {
        if (!TryGetActiveMap(out MapData map, out MapCoordinateBoundary coordinates))
            return true;
        Vector2Int firstCell = coordinates.WorldToCell(first);
        Vector2Int secondCell = coordinates.WorldToCell(second);
        return map.IsInside(firstCell) && map.IsInside(secondCell) &&
            map.GetElevation(firstCell) == map.GetElevation(secondCell);
    }

    public static bool TryGetActiveMap(out MapData map, out MapCoordinateBoundary coordinates)
    {
        if (controller == null)
            controller = Object.FindObjectOfType<MapGenerationController>();
        map = controller != null ? controller.LastGeneratedMap : null;
        coordinates = map != null && controller.TilemapRenderer != null
            ? controller.TilemapRenderer.Coordinates : null;
        return map != null && map.HasElevation && coordinates != null;
    }

    public static int GetElevation(Vector3 worldPosition)
    {
        if (!TryGetActiveMap(out MapData map, out MapCoordinateBoundary coordinates))
            return 0;
        Vector2Int cell = coordinates.WorldToCell(worldPosition);
        return map.IsInside(cell) ? map.GetElevation(cell) : 0;
    }

    // The committed MapData is the sole authority for height transitions.
    public static bool TryFindPath(MapData map, Vector2Int start, Vector2Int goal,
        List<Vector2Int> path)
    {
        path.Clear();
        if (map == null || !map.IsInside(start) || !map.IsInside(goal) ||
            !map.IsWalkable(start) || !map.IsWalkable(goal))
            return false;

        var queue = new Queue<Vector2Int>();
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(start);
        previous[start] = start;
        Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
        while (queue.Count > 0 && !previous.ContainsKey(goal))
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (previous.ContainsKey(next) || !map.CanTraverse(current, next))
                    continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(goal))
            return false;

        for (Vector2Int cell = goal; cell != start; cell = previous[cell])
            path.Add(cell);
        path.Add(start);
        path.Reverse();
        return true;
    }
}
