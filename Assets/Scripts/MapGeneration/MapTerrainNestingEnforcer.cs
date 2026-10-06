using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 强制最终地图遵守 Water（水）到 Sand（沙地）、Grass（草地）再到 Mountain（山地）的可见嵌套关系。
/// </summary>
public static class MapTerrainNestingEnforcer
{
    /// <summary>
    /// 构建完整视觉过渡带时使用的八方向邻居偏移。
    /// </summary>
    private static readonly Vector2Int[] SurroundingDirections =
    {
        new Vector2Int(-1, 1),
        Vector2Int.up,
        new Vector2Int(1, 1),
        Vector2Int.left,
        Vector2Int.right,
        new Vector2Int(-1, -1),
        Vector2Int.down,
        new Vector2Int(1, -1)
    };

    /// <summary>
    /// 为水岸保留一格沙地，并为高地保留一格草地过渡带。
    /// </summary>
    /// <param name="mapData">已经完成道路和可达性处理的最终地图数据。</param>
    /// <returns>本次被调整的地图单元总数。</returns>
    public static int Enforce(MapData mapData)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        int changedCellCount = CreateSandShoreline(mapData);
        changedCellCount += CreateGrassElevationMargin(mapData);
        return changedCellCount;
    }

    /// <summary>
    /// 把八方向紧邻水域的草地覆盖层单元同时转换为沙地，形成完整海岸带。
    /// </summary>
    /// <param name="mapData">待规范化的地图数据。</param>
    /// <returns>转换为沙地的单元数量。</returns>
    private static int CreateSandShoreline(MapData mapData)
    {
        List<Vector2Int> shorelineCells = new List<Vector2Int>();

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                MapTerrainType terrainType = mapData.GetCell(cell).terrainType;
                if (!TerrainTopology.UsesGrassOverlay(terrainType))
                    continue;

                if (HasWaterOrMapOutsideNeighbor(mapData, cell))
                    shorelineCells.Add(cell);
            }
        }

        foreach (Vector2Int shorelineCell in shorelineCells)
            mapData.SetTerrain(shorelineCell, MapTerrainType.Sand);

        return shorelineCells.Count;
    }

    /// <summary>
    /// 把高地边缘同时转换为草地，使岩石高地外围始终保留可见草地。
    /// </summary>
    /// <param name="mapData">已经生成沙岸的地图数据。</param>
    /// <returns>转换为草地的单元数量。</returns>
    private static int CreateGrassElevationMargin(MapData mapData)
    {
        List<Vector2Int> elevationMarginCells = new List<Vector2Int>();

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (mapData.GetCell(cell).terrainType != MapTerrainType.Mountain)
                    continue;

                if (HasNonGrassOverlayNeighbor(mapData, cell))
                    elevationMarginCells.Add(cell);
            }
        }

        foreach (Vector2Int elevationMarginCell in elevationMarginCells)
            mapData.SetTerrain(elevationMarginCell, MapTerrainType.Grass);

        return elevationMarginCells.Count;
    }

    /// <summary>
    /// 判断指定单元八方向是否紧邻水域或地图外部。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="cell">待检查的地图单元。</param>
    /// <returns>存在水域或地图外部邻居时返回 true。</returns>
    private static bool HasWaterOrMapOutsideNeighbor(MapData mapData, Vector2Int cell)
    {
        foreach (Vector2Int direction in SurroundingDirections)
        {
            Vector2Int neighborCell = cell + direction;
            if (!mapData.IsInside(neighborCell) ||
                TerrainTopology.IsWater(mapData.GetCell(neighborCell).terrainType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断指定高地单元八方向是否存在不使用草地覆盖层的邻居。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="cell">待检查的高地单元。</param>
    /// <returns>高地边缘需要转换为草地时返回 true。</returns>
    private static bool HasNonGrassOverlayNeighbor(MapData mapData, Vector2Int cell)
    {
        foreach (Vector2Int direction in SurroundingDirections)
        {
            Vector2Int neighborCell = cell + direction;
            if (!mapData.IsInside(neighborCell) ||
                !TerrainTopology.UsesGrassOverlay(mapData.GetCell(neighborCell).terrainType))
            {
                return true;
            }
        }

        return false;
    }
}
