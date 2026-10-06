using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 使用四方向 Connected Component（连通分量）清理面积过小的自然地形区域。
/// </summary>
public static class MapTerrainRegionCleaner
{
    /// <summary>
    /// 四方向 Flood Fill（洪水填充）使用的固定邻居顺序。
    /// </summary>
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    /// <summary>
    /// 把小于指定面积的自然地形区域替换为周围占多数的地形。
    /// </summary>
    /// <param name="mapData">需要清理的地图数据。</param>
    /// <param name="minimumRegionSize">保留自然区域所需的最小单元数量。</param>
    /// <param name="excludedBorderSize">不参与清理的地图外圈宽度。</param>
    /// <param name="protectedCenter">不参与清理的出生保护区中心。</param>
    /// <param name="protectedRadius">出生保护区使用的方形半径。</param>
    /// <returns>本次被替换的地图单元数量。</returns>
    public static int Clean(
        MapData mapData,
        int minimumRegionSize,
        int excludedBorderSize,
        Vector2Int protectedCenter,
        int protectedRadius)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (minimumRegionSize <= 1)
            return 0;

        MapTerrainType[,] terrainSnapshot = CreateTerrainSnapshot(mapData);
        bool[,] visitedCells = new bool[mapData.Width, mapData.Height];
        Dictionary<Vector2Int, MapTerrainType> pendingReplacements =
            new Dictionary<Vector2Int, MapTerrainType>();

        for (int localX = 0; localX < mapData.Width; localX++)
        {
            for (int localY = 0; localY < mapData.Height; localY++)
            {
                if (visitedCells[localX, localY])
                    continue;

                Vector2Int startCell = mapData.Origin + new Vector2Int(localX, localY);
                if (IsExcluded(
                        mapData,
                        startCell,
                        excludedBorderSize,
                        protectedCenter,
                        protectedRadius))
                {
                    visitedCells[localX, localY] = true;
                    continue;
                }

                MapTerrainType regionType = terrainSnapshot[localX, localY];
                if (regionType == MapTerrainType.Path)
                {
                    visitedCells[localX, localY] = true;
                    continue;
                }

                List<Vector2Int> regionCells = CollectRegion(
                    mapData,
                    terrainSnapshot,
                    visitedCells,
                    startCell,
                    regionType,
                    excludedBorderSize,
                    protectedCenter,
                    protectedRadius);

                if (regionCells.Count >= minimumRegionSize)
                    continue;

                if (!TryChooseReplacement(
                        mapData,
                        terrainSnapshot,
                        regionCells,
                        regionType,
                        excludedBorderSize,
                        protectedCenter,
                        protectedRadius,
                        out MapTerrainType replacementType))
                {
                    continue;
                }

                foreach (Vector2Int regionCell in regionCells)
                    pendingReplacements[regionCell] = replacementType;
            }
        }

        foreach (KeyValuePair<Vector2Int, MapTerrainType> replacement in pendingReplacements)
            mapData.SetTerrain(replacement.Key, replacement.Value);

        return pendingReplacements.Count;
    }

    /// <summary>
    /// 复制清理前的逻辑地形，保证结果不受扫描和替换顺序影响。
    /// </summary>
    /// <param name="mapData">需要复制的地图数据。</param>
    /// <returns>按局部坐标排列的地形快照。</returns>
    private static MapTerrainType[,] CreateTerrainSnapshot(MapData mapData)
    {
        MapTerrainType[,] snapshot = new MapTerrainType[mapData.Width, mapData.Height];

        for (int localX = 0; localX < mapData.Width; localX++)
        {
            for (int localY = 0; localY < mapData.Height; localY++)
            {
                Vector2Int cell = mapData.Origin + new Vector2Int(localX, localY);
                snapshot[localX, localY] = mapData.GetCell(cell).terrainType;
            }
        }

        return snapshot;
    }

    /// <summary>
    /// 从指定单元收集同一种地形的四方向连通区域。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="terrainSnapshot">清理前的地形快照。</param>
    /// <param name="visitedCells">已经访问过的局部单元。</param>
    /// <param name="startCell">连通区域起点。</param>
    /// <param name="regionType">本次收集的地形类型。</param>
    /// <param name="excludedBorderSize">不参与清理的外圈宽度。</param>
    /// <param name="protectedCenter">出生保护区中心。</param>
    /// <param name="protectedRadius">出生保护区半径。</param>
    /// <returns>属于同一连通区域的所有网格坐标。</returns>
    private static List<Vector2Int> CollectRegion(
        MapData mapData,
        MapTerrainType[,] terrainSnapshot,
        bool[,] visitedCells,
        Vector2Int startCell,
        MapTerrainType regionType,
        int excludedBorderSize,
        Vector2Int protectedCenter,
        int protectedRadius)
    {
        List<Vector2Int> regionCells = new List<Vector2Int>();
        Queue<Vector2Int> pendingCells = new Queue<Vector2Int>();
        MarkVisited(mapData, visitedCells, startCell);
        pendingCells.Enqueue(startCell);

        while (pendingCells.Count > 0)
        {
            Vector2Int currentCell = pendingCells.Dequeue();
            regionCells.Add(currentCell);

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighborCell = currentCell + direction;
                if (!mapData.IsInside(neighborCell) ||
                    IsVisited(mapData, visitedCells, neighborCell) ||
                    IsExcluded(
                        mapData,
                        neighborCell,
                        excludedBorderSize,
                        protectedCenter,
                        protectedRadius) ||
                    GetSnapshotTerrain(mapData, terrainSnapshot, neighborCell) != regionType)
                {
                    continue;
                }

                MarkVisited(mapData, visitedCells, neighborCell);
                pendingCells.Enqueue(neighborCell);
            }
        }

        return regionCells;
    }

    /// <summary>
    /// 统计连通区域外围地形，并选择数量最多且枚举值最小的稳定替换类型。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="terrainSnapshot">清理前的地形快照。</param>
    /// <param name="regionCells">待替换的小型连通区域。</param>
    /// <param name="regionType">区域原有地形类型。</param>
    /// <param name="excludedBorderSize">不参与清理的外圈宽度。</param>
    /// <param name="protectedCenter">出生保护区中心。</param>
    /// <param name="protectedRadius">出生保护区半径。</param>
    /// <param name="replacementType">选出的替换地形。</param>
    /// <returns>至少存在一种有效外围地形时返回 true。</returns>
    private static bool TryChooseReplacement(
        MapData mapData,
        MapTerrainType[,] terrainSnapshot,
        List<Vector2Int> regionCells,
        MapTerrainType regionType,
        int excludedBorderSize,
        Vector2Int protectedCenter,
        int protectedRadius,
        out MapTerrainType replacementType)
    {
        Dictionary<MapTerrainType, int> neighborCounts =
            new Dictionary<MapTerrainType, int>();

        foreach (Vector2Int regionCell in regionCells)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighborCell = regionCell + direction;
                if (!mapData.IsInside(neighborCell) ||
                    IsExcluded(
                        mapData,
                        neighborCell,
                        excludedBorderSize,
                        protectedCenter,
                        protectedRadius))
                {
                    continue;
                }

                MapTerrainType neighborType =
                    GetSnapshotTerrain(mapData, terrainSnapshot, neighborCell);
                if (neighborType == regionType || neighborType == MapTerrainType.Path)
                    continue;

                neighborCounts.TryGetValue(neighborType, out int currentCount);
                neighborCounts[neighborType] = currentCount + 1;
            }
        }

        replacementType = regionType;
        int highestCount = 0;

        foreach (KeyValuePair<MapTerrainType, int> neighborCount in neighborCounts)
        {
            if (neighborCount.Value > highestCount ||
                (neighborCount.Value == highestCount &&
                 (int)neighborCount.Key < (int)replacementType))
            {
                replacementType = neighborCount.Key;
                highestCount = neighborCount.Value;
            }
        }

        return highestCount > 0;
    }

    /// <summary>
    /// 判断单元是否属于不参与自然区域清理的边界或出生保护区。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <param name="excludedBorderSize">排除的外圈宽度。</param>
    /// <param name="protectedCenter">出生保护区中心。</param>
    /// <param name="protectedRadius">出生保护区半径。</param>
    /// <returns>单元需要被排除时返回 true。</returns>
    private static bool IsExcluded(
        MapData mapData,
        Vector2Int cell,
        int excludedBorderSize,
        Vector2Int protectedCenter,
        int protectedRadius)
    {
        if (excludedBorderSize > 0 && mapData.IsBorder(cell, excludedBorderSize))
            return true;

        if (protectedRadius < 0)
            return false;

        int distance = Mathf.Max(
            Mathf.Abs(cell.x - protectedCenter.x),
            Mathf.Abs(cell.y - protectedCenter.y));
        return distance <= protectedRadius;
    }

    /// <summary>
    /// 按世界网格坐标读取地形快照。
    /// </summary>
    /// <param name="mapData">提供地图原点的地图数据。</param>
    /// <param name="terrainSnapshot">待读取的局部地形快照。</param>
    /// <param name="cell">世界网格坐标。</param>
    /// <returns>对应的逻辑地形。</returns>
    private static MapTerrainType GetSnapshotTerrain(
        MapData mapData,
        MapTerrainType[,] terrainSnapshot,
        Vector2Int cell)
    {
        return terrainSnapshot[cell.x - mapData.Origin.x, cell.y - mapData.Origin.y];
    }

    /// <summary>
    /// 判断指定世界网格坐标是否已经访问。
    /// </summary>
    /// <param name="mapData">提供地图原点的地图数据。</param>
    /// <param name="visitedCells">访问状态数组。</param>
    /// <param name="cell">世界网格坐标。</param>
    /// <returns>已经访问时返回 true。</returns>
    private static bool IsVisited(MapData mapData, bool[,] visitedCells, Vector2Int cell)
    {
        return visitedCells[cell.x - mapData.Origin.x, cell.y - mapData.Origin.y];
    }

    /// <summary>
    /// 把指定世界网格坐标标记为已经访问。
    /// </summary>
    /// <param name="mapData">提供地图原点的地图数据。</param>
    /// <param name="visitedCells">访问状态数组。</param>
    /// <param name="cell">世界网格坐标。</param>
    private static void MarkVisited(MapData mapData, bool[,] visitedCells, Vector2Int cell)
    {
        visitedCells[cell.x - mapData.Origin.x, cell.y - mapData.Origin.y] = true;
    }
}
