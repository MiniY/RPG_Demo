using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 表示一个需要写入装饰 Tilemap（瓦片地图）的树木放置结果。
/// </summary>
public readonly struct MapDecorationPlacement
{
    /// <summary>
    /// 装饰物所在的网格坐标。
    /// </summary>
    public Vector2Int Cell { get; }

    /// <summary>
    /// 装饰物在树木 Tile（瓦片）数组中的变体下标。
    /// </summary>
    public int VariantIndex { get; }

    /// <summary>
    /// 创建一个树木装饰物放置结果。
    /// </summary>
    /// <param name="cell">装饰物所在的网格坐标。</param>
    /// <param name="variantIndex">树木 Tile 数组下标。</param>
    public MapDecorationPlacement(Vector2Int cell, int variantIndex)
    {
        Cell = cell;
        VariantIndex = variantIndex;
    }
}

/// <summary>
/// 保存一次地图生成得到的全部装饰物放置结果。
/// </summary>
public sealed class MapDecorationData
{
    /// <summary>
    /// 装饰物放置结果列表，顺序由确定性的网格扫描决定。
    /// </summary>
    private readonly List<MapDecorationPlacement> placements =
        new List<MapDecorationPlacement>();

    /// <summary>
    /// 只读访问装饰物放置结果。
    /// </summary>
    public IReadOnlyList<MapDecorationPlacement> Placements => placements;

    /// <summary>
    /// 获取装饰物总数量。
    /// </summary>
    public int Count => placements.Count;

    /// <summary>
    /// 向结果集合追加一个装饰物。
    /// </summary>
    /// <param name="placement">需要追加的放置结果。</param>
    internal void Add(MapDecorationPlacement placement)
    {
        placements.Add(placement);
    }
}

/// <summary>
/// 根据最终 MapData（地图数据）确定性地生成树木装饰物位置。
/// </summary>
public static class MapDecorationGenerator
{
    /// <summary>
    /// 用于把世界种子转换为装饰随机流的固定混合常量。
    /// </summary>
    private const int DecorationSeedSalt = 0x4D415044;

    /// <summary>
    /// 根据最终地形生成不会改变道路和关键位置的树木装饰数据。
    /// </summary>
    /// <param name="mapData">已经完成地形和道路修正的地图数据。</param>
    /// <param name="settings">包含装饰规则的地图配置。</param>
    /// <returns>确定性的装饰物放置结果。</returns>
    public static MapDecorationData Generate(
        MapData mapData,
        MapGenerationSettings settings)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        MapDecorationData decorationData = new MapDecorationData();
        if (settings.treeTiles == null || settings.treeTiles.Length == 0)
            return decorationData;

        // 装饰物使用独立随机流，调整装饰密度不会改写地形和道路结果。
        System.Random random = new System.Random(
            DeriveDecorationSeed(settings.seed, settings.decorationSeedOffset));
        Vector2 noiseOffset = CreateNoiseOffset(random);
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        int minimumSpacing = Mathf.Max(1, settings.decorationMinimumSpacing);

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                // 先过滤不可行走地形、道路和关键位置，再计算装饰概率。
                if (!CanPlaceOnCell(cell, mapData, settings))
                    continue;

                MapTerrainType terrainType = mapData.GetCell(cell).terrainType;
                float baseDensity = GetBaseDensity(terrainType, settings);
                if (baseDensity <= 0f)
                    continue;

                float densityNoise = SampleNoise(
                    cell,
                    mapData.Origin,
                    noiseOffset,
                    settings.decorationNoiseScale);
                float placementProbability = Mathf.Clamp01(
                    baseDensity * Mathf.Lerp(0.45f, 1.55f, densityNoise));

                if (random.NextDouble() > placementProbability)
                    continue;

                // 最小间距形成疏密有致的簇，同时防止树木完全重叠。
                if (!IsSpacingAvailable(cell, occupiedCells, minimumSpacing))
                    continue;

                int variantIndex = random.Next(settings.treeTiles.Length);
                decorationData.Add(new MapDecorationPlacement(cell, variantIndex));
                occupiedCells.Add(cell);
            }
        }

        return decorationData;
    }

    /// <summary>
    /// 判断一个网格单元是否满足装饰物候选条件。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <param name="mapData">地图数据。</param>
    /// <param name="settings">地图配置。</param>
    /// <returns>满足放置条件时返回 true。</returns>
    private static bool CanPlaceOnCell(
        Vector2Int cell,
        MapData mapData,
        MapGenerationSettings settings)
    {
        if (!mapData.IsWalkable(cell))
            return false;

        MapTerrainType terrainType = mapData.GetCell(cell).terrainType;
        if (terrainType == MapTerrainType.Path)
            return false;

        if (ChebyshevDistance(cell, mapData.SpawnCell) <=
            settings.decorationSpawnClearRadius)
        {
            return false;
        }

        if (ChebyshevDistance(cell, mapData.ExitCell) <=
            settings.decorationExitClearRadius)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 获取当前地形的基础装饰概率。
    /// </summary>
    /// <param name="terrainType">当前地形类型。</param>
    /// <param name="settings">地图配置。</param>
    /// <returns>基础放置概率。</returns>
    private static float GetBaseDensity(
        MapTerrainType terrainType,
        MapGenerationSettings settings)
    {
        switch (terrainType)
        {
            case MapTerrainType.Forest:
                return settings.forestDecorationDensity;
            case MapTerrainType.Grass:
                return settings.grassDecorationDensity;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// 检查候选单元周围是否已经存在过近的装饰物。
    /// </summary>
    /// <param name="cell">待检查的候选单元。</param>
    /// <param name="occupiedCells">已经放置装饰物的单元集合。</param>
    /// <param name="minimumSpacing">最小网格间距。</param>
    /// <returns>满足间距限制时返回 true。</returns>
    private static bool IsSpacingAvailable(
        Vector2Int cell,
        HashSet<Vector2Int> occupiedCells,
        int minimumSpacing)
    {
        int radius = Mathf.Max(0, minimumSpacing - 1);

        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                if (occupiedCells.Contains(cell + new Vector2Int(offsetX, offsetY)))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 计算两个网格坐标的 Chebyshev Distance（切比雪夫距离）。
    /// </summary>
    /// <param name="first">第一个网格坐标。</param>
    /// <param name="second">第二个网格坐标。</param>
    /// <returns>两个坐标在方形邻域中的最大轴向距离。</returns>
    private static int ChebyshevDistance(Vector2Int first, Vector2Int second)
    {
        return Mathf.Max(
            Mathf.Abs(first.x - second.x),
            Mathf.Abs(first.y - second.y));
    }

    /// <summary>
    /// 根据世界种子和装饰偏移派生独立的装饰种子。
    /// </summary>
    /// <param name="worldSeed">世界基础种子。</param>
    /// <param name="decorationOffset">装饰配置偏移。</param>
    /// <returns>装饰随机流使用的整数种子。</returns>
    private static int DeriveDecorationSeed(int worldSeed, int decorationOffset)
    {
        unchecked
        {
            int mixedSeed = worldSeed ^ DecorationSeedSalt;
            mixedSeed = mixedSeed * 397 + decorationOffset;
            return mixedSeed;
        }
    }

    /// <summary>
    /// 从随机流创建装饰密度噪声使用的二维偏移量。
    /// </summary>
    /// <param name="random">装饰专用伪随机流。</param>
    /// <returns>密度噪声偏移量。</returns>
    private static Vector2 CreateNoiseOffset(System.Random random)
    {
        return new Vector2(
            (float)(random.NextDouble() * 100000.0),
            (float)(random.NextDouble() * 100000.0));
    }

    /// <summary>
    /// 按地图局部坐标采样装饰密度噪声。
    /// </summary>
    /// <param name="cell">待采样的地图坐标。</param>
    /// <param name="origin">地图原点。</param>
    /// <param name="offset">噪声通道偏移量。</param>
    /// <param name="scale">噪声采样缩放。</param>
    /// <returns>范围约为 0 到 1 的噪声值。</returns>
    private static float SampleNoise(
        Vector2Int cell,
        Vector2Int origin,
        Vector2 offset,
        float scale)
    {
        float sampleX = (cell.x - origin.x + offset.x) * scale;
        float sampleY = (cell.y - origin.y + offset.y) * scale;
        return Mathf.PerlinNoise(sampleX, sampleY);
    }
}
