using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据最终 MapData（地图数据）确定性地生成树木、灌木和散落岩石。
/// </summary>
public static class MapSimpleDecorationGenerator
{
    /// <summary>
    /// 用于把世界种子转换为简单装饰物随机流的固定混合常量。
    /// </summary>
    private const int SimpleDecorationSeedSalt = 0x4D415044;

    /// <summary>
    /// 固定类别处理顺序；体积更大的树木优先占地。
    /// </summary>
    private static readonly MapSimpleDecorationType[] GenerationOrder =
    {
        MapSimpleDecorationType.Tree,
        MapSimpleDecorationType.Bush,
        MapSimpleDecorationType.ScatteredRock
    };

    /// <summary>
    /// 根据最终地形生成不会占用道路和关键位置的简单装饰数据。
    /// </summary>
    /// <param name="mapData">已经完成地形和道路修正的地图数据。</param>
    /// <param name="settings">包含简单装饰规则和调色板的地图配置。</param>
    /// <returns>确定性的简单装饰物放置结果。</returns>
    public static MapSimpleDecorationData Generate(
        MapData mapData,
        MapGenerationSettings settings)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        MapSimpleDecorationData decorationData = new MapSimpleDecorationData();
        MapSimpleDecorationPalette palette = settings.simpleDecorationPalette;
        if (palette == null)
            return decorationData;

        foreach (MapSimpleDecorationType decorationType in GenerationOrder)
        {
            GenerateDecorationType(
                decorationType,
                mapData,
                settings,
                palette,
                decorationData);
        }

        return decorationData;
    }

    /// <summary>
    /// 使用独立随机流生成一个简单装饰类别。
    /// </summary>
    /// <param name="decorationType">当前需要生成的简单装饰类别。</param>
    /// <param name="mapData">最终地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="palette">简单装饰调色板。</param>
    /// <param name="decorationData">正在累积的简单装饰结果。</param>
    private static void GenerateDecorationType(
        MapSimpleDecorationType decorationType,
        MapData mapData,
        MapGenerationSettings settings,
        MapSimpleDecorationPalette palette,
        MapSimpleDecorationData decorationData)
    {
        List<MapSimpleDecorationVariant> variants =
            CollectValidVariants(palette, decorationType);
        if (variants.Count == 0)
            return;

        // 每个类别拥有独立随机流，新增灌木不会重排已经生成的树木随机序列。
        System.Random random = new System.Random(
            DeriveSimpleDecorationSeed(
                settings.seed,
                settings.simpleDecorationSeedOffset,
                decorationType));
        Vector2 noiseOffset = CreateNoiseOffset(random);
        int minimumSpacing = Mathf.Max(1, settings.simpleDecorationMinimumSpacing);

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int anchorCell = new Vector2Int(x, y);
                MapTerrainType terrainType = mapData.GetCell(anchorCell).terrainType;
                float baseDensity = GetBaseDensity(
                    decorationType,
                    terrainType,
                    settings);
                if (baseDensity <= 0f)
                    continue;

                float densityNoise = SampleNoise(
                    anchorCell,
                    mapData.Origin,
                    noiseOffset,
                    settings.simpleDecorationNoiseScale);
                float placementProbability = Mathf.Clamp01(
                    baseDensity * Mathf.Lerp(0.45f, 1.55f, densityNoise));

                if (random.NextDouble() > placementProbability)
                    continue;

                MapSimpleDecorationVariant variant =
                    SelectWeightedVariant(variants, random);
                if (!CanPlaceVariant(
                        anchorCell,
                        variant,
                        mapData,
                        settings,
                        decorationData,
                        minimumSpacing))
                {
                    continue;
                }

                MapSimpleDecorationPlacement placement =
                    new MapSimpleDecorationPlacement(
                        anchorCell,
                        decorationType,
                        variant.VariantId);
                decorationData.Add(placement, variant);
            }
        }
    }

    /// <summary>
    /// 收集调色板中指定类别的全部有效变体。
    /// </summary>
    /// <param name="palette">待读取的简单装饰调色板。</param>
    /// <param name="decorationType">需要收集的简单装饰类别。</param>
    /// <returns>保持调色板序列化顺序的有效变体列表。</returns>
    private static List<MapSimpleDecorationVariant> CollectValidVariants(
        MapSimpleDecorationPalette palette,
        MapSimpleDecorationType decorationType)
    {
        List<MapSimpleDecorationVariant> variants =
            new List<MapSimpleDecorationVariant>();

        foreach (MapSimpleDecorationVariant variant in palette.Variants)
        {
            if (variant != null && variant.DecorationType == decorationType &&
                variant.IsValid())
            {
                variants.Add(variant);
            }
        }

        return variants;
    }

    /// <summary>
    /// 按变体权重从当前类别中选择一种固定样式。
    /// </summary>
    /// <param name="variants">可供选择的有效变体。</param>
    /// <param name="random">当前类别专用的伪随机流。</param>
    /// <returns>选中的固定装饰物变体。</returns>
    private static MapSimpleDecorationVariant SelectWeightedVariant(
        IReadOnlyList<MapSimpleDecorationVariant> variants,
        System.Random random)
    {
        int totalWeight = 0;
        foreach (MapSimpleDecorationVariant variant in variants)
            totalWeight += variant.SelectionWeight;

        int selectedWeight = random.Next(totalWeight);
        foreach (MapSimpleDecorationVariant variant in variants)
        {
            selectedWeight -= variant.SelectionWeight;
            if (selectedWeight < 0)
                return variant;
        }

        return variants[variants.Count - 1];
    }

    /// <summary>
    /// 检查一个固定样式的完整占地是否满足地形、关键区域和间距限制。
    /// </summary>
    /// <param name="anchorCell">候选装饰物锚点。</param>
    /// <param name="variant">候选固定样式定义。</param>
    /// <param name="mapData">最终地图数据。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <param name="decorationData">已经生成的简单装饰结果。</param>
    /// <param name="minimumSpacing">任意两个占地之间的最小网格距离。</param>
    /// <returns>整个占地都可用时返回 true。</returns>
    private static bool CanPlaceVariant(
        Vector2Int anchorCell,
        MapSimpleDecorationVariant variant,
        MapData mapData,
        MapGenerationSettings settings,
        MapSimpleDecorationData decorationData,
        int minimumSpacing)
    {
        for (int offsetX = variant.FootprintMinimum.x;
             offsetX <= variant.FootprintMaximum.x;
             offsetX++)
        {
            for (int offsetY = variant.FootprintMinimum.y;
                 offsetY <= variant.FootprintMaximum.y;
                 offsetY++)
            {
                Vector2Int cell =
                    anchorCell + new Vector2Int(offsetX, offsetY);

                if (!mapData.IsInside(cell) || !mapData.IsWalkable(cell))
                    return false;

                MapTerrainType terrainType = mapData.GetCell(cell).terrainType;
                if (terrainType == MapTerrainType.Path ||
                    !variant.SupportsTerrain(terrainType))
                {
                    return false;
                }

                if (ChebyshevDistance(cell, mapData.SpawnCell) <=
                    settings.simpleDecorationSpawnClearRadius)
                {
                    return false;
                }

                if (ChebyshevDistance(cell, mapData.ExitCell) <=
                    settings.simpleDecorationExitClearRadius)
                {
                    return false;
                }

                if (!IsSpacingAvailable(
                        cell,
                        decorationData,
                        minimumSpacing))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 检查候选占地单元周围是否已经存在过近的简单装饰物。
    /// </summary>
    /// <param name="cell">候选占地中的一个网格单元。</param>
    /// <param name="decorationData">已经生成的简单装饰结果。</param>
    /// <param name="minimumSpacing">最小网格间距。</param>
    /// <returns>满足间距限制时返回 true。</returns>
    private static bool IsSpacingAvailable(
        Vector2Int cell,
        MapSimpleDecorationData decorationData,
        int minimumSpacing)
    {
        int radius = Mathf.Max(0, minimumSpacing - 1);

        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                if (decorationData.IsOccupied(
                        cell + new Vector2Int(offsetX, offsetY)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 获取指定装饰类别在当前地形上的基础生成概率。
    /// </summary>
    /// <param name="decorationType">简单装饰类别。</param>
    /// <param name="terrainType">候选锚点地形。</param>
    /// <param name="settings">地图生成配置。</param>
    /// <returns>范围为 0 到 1 的基础生成概率。</returns>
    private static float GetBaseDensity(
        MapSimpleDecorationType decorationType,
        MapTerrainType terrainType,
        MapGenerationSettings settings)
    {
        switch (decorationType)
        {
            case MapSimpleDecorationType.Tree:
                return terrainType == MapTerrainType.Forest
                    ? settings.forestTreeDensity
                    : terrainType == MapTerrainType.Grass
                        ? settings.grassTreeDensity
                        : 0f;

            case MapSimpleDecorationType.Bush:
                return terrainType == MapTerrainType.Forest
                    ? settings.forestBushDensity
                    : terrainType == MapTerrainType.Grass
                        ? settings.grassBushDensity
                        : 0f;

            case MapSimpleDecorationType.ScatteredRock:
                switch (terrainType)
                {
                    case MapTerrainType.Sand:
                        return settings.sandRockDensity;
                    case MapTerrainType.Grass:
                        return settings.grassRockDensity;
                    case MapTerrainType.Forest:
                        return settings.forestRockDensity;
                    default:
                        return 0f;
                }

            default:
                return 0f;
        }
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
    /// 根据世界种子、配置偏移和类别派生独立的简单装饰物种子。
    /// </summary>
    /// <param name="worldSeed">世界基础种子。</param>
    /// <param name="decorationOffset">简单装饰配置偏移。</param>
    /// <param name="decorationType">需要生成的简单装饰类别。</param>
    /// <returns>当前类别伪随机流使用的整数种子。</returns>
    private static int DeriveSimpleDecorationSeed(
        int worldSeed,
        int decorationOffset,
        MapSimpleDecorationType decorationType)
    {
        unchecked
        {
            int mixedSeed = worldSeed ^ SimpleDecorationSeedSalt;
            mixedSeed = mixedSeed * 397 + decorationOffset;
            mixedSeed = mixedSeed * 397 + (int)decorationType;
            return mixedSeed;
        }
    }

    /// <summary>
    /// 从随机流创建简单装饰密度噪声使用的二维偏移量。
    /// </summary>
    /// <param name="random">当前类别专用的伪随机流。</param>
    /// <returns>密度噪声偏移量。</returns>
    private static Vector2 CreateNoiseOffset(System.Random random)
    {
        return new Vector2(
            (float)(random.NextDouble() * 100000.0),
            (float)(random.NextDouble() * 100000.0));
    }

    /// <summary>
    /// 按地图局部坐标采样简单装饰物密度噪声。
    /// </summary>
    /// <param name="cell">待采样的地图坐标。</param>
    /// <param name="origin">地图原点。</param>
    /// <param name="offset">当前类别噪声通道偏移。</param>
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
