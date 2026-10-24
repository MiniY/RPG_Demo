using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapPopulationRegion
{
    internal MapPopulationRegion(MapTerrainType terrain, Vector2Int anchor,
        IReadOnlyList<Vector2Int> cells)
    {
        Terrain = terrain;
        Anchor = anchor;
        Cells = cells;
    }

    public MapTerrainType Terrain { get; }
    public Vector2Int Anchor { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }
    public int Area => Cells.Count;
}

/// <summary>Shared terrain-derived connected regions reachable from the current player spawn.</summary>
public sealed class MapPopulationRegionQuery
{
    private readonly Dictionary<Vector2Int, int> spawnDistances;
    private readonly List<MapPopulationRegion> regions;

    private MapPopulationRegionQuery(Dictionary<Vector2Int, int> spawnDistances,
        List<MapPopulationRegion> regions)
    {
        this.spawnDistances = spawnDistances;
        this.regions = regions;
    }

    public IReadOnlyDictionary<Vector2Int, int> SpawnDistances => spawnDistances;
    public IReadOnlyList<MapPopulationRegion> Regions => regions;
    public int ReachableCount => spawnDistances.Count;

    public static MapPopulationRegionQuery Build(MapData map, MapPlacementPlan plan)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));

        HashSet<Vector2Int> blockers = MapSemanticPlacementPrimitives.CollectNavigationBlockers(
            plan,
            "CollisionDecoration",
            MerchantPlacementService.MerchantRole,
            PlantPlacementService.PlantRole,
            DestructiblePlacementService.DestructibleRole);
        Dictionary<Vector2Int, int> distances =
            MapSemanticPlacementPrimitives.BuildWalkableDistances(map, map.SpawnCell, blockers);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<MapPopulationRegion> regions = new List<MapPopulationRegion>();

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        {
            Vector2Int anchor = new Vector2Int(x, y);
            if (!distances.ContainsKey(anchor) || !visited.Add(anchor))
                continue;

            MapTerrainType terrain = map.GetCell(anchor).terrainType;
            int elevation = map.GetElevation(anchor);
            Queue<Vector2Int> pending = new Queue<Vector2Int>();
            List<Vector2Int> cells = new List<Vector2Int>();
            pending.Enqueue(anchor);
            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                cells.Add(current);
                foreach (Vector2Int direction in MapSemanticPlacementPrimitives.CardinalDirections)
                {
                    Vector2Int next = current + direction;
                    if (!distances.ContainsKey(next) || visited.Contains(next) ||
                        map.GetCell(next).terrainType != terrain ||
                        map.GetElevation(next) != elevation)
                        continue;
                    visited.Add(next);
                    pending.Enqueue(next);
                }
            }
            regions.Add(new MapPopulationRegion(terrain, anchor, cells.AsReadOnly()));
        }

        return new MapPopulationRegionQuery(distances, regions);
    }
}
