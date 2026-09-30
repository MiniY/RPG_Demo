using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证装饰物生成的确定性、位置约束和随机流隔离。
/// </summary>
public class MapDecorationGeneratorTests
{
    /// <summary>
    /// 当前测试使用的临时地图配置。
    /// </summary>
    private MapGenerationSettings settings;

    /// <summary>
    /// 提供给装饰配置数组的临时树木 Tile（瓦片）。
    /// </summary>
    private Tile treeTile;

    /// <summary>
    /// 创建测试所需的地图配置和树木 Tile。
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<MapGenerationSettings>();
        settings.mapWidth = 64;
        settings.mapHeight = 64;
        settings.mapOrigin = Vector2Int.zero;
        settings.borderSize = 1;
        settings.seed = 20260929;
        settings.noiseScale = 0.08f;
        settings.waterThreshold = 0.32f;
        settings.shallowWaterThreshold = 0.42f;
        settings.biomeNoiseScale = 0.055f;
        settings.forestMoistureThreshold = 0.58f;
        settings.forestTemperatureThreshold = 0.45f;
        settings.mountainHeightThreshold = 0.72f;
        settings.mountainTemperatureThreshold = 0.55f;
        settings.spawnProtectionRadius = 5;
        settings.roadWidth = 2;
        settings.roadTurnChance = 0.3f;
        settings.decorationSeedOffset = 7919;
        settings.decorationNoiseScale = 0.12f;
        settings.grassDecorationDensity = 0.04f;
        settings.forestDecorationDensity = 0.38f;
        settings.decorationMinimumSpacing = 2;
        settings.decorationSpawnClearRadius = 6;
        settings.decorationExitClearRadius = 2;

        treeTile = ScriptableObject.CreateInstance<Tile>();
        settings.treeTiles = new TileBase[] { treeTile };
    }

    /// <summary>
    /// 销毁测试对象，避免 ScriptableObject 泄漏到其他测试。
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(treeTile);
        Object.DestroyImmediate(settings);
    }

    /// <summary>
    /// 验证相同地图和相同 Seed 会生成完全一致的装饰放置结果。
    /// </summary>
    [Test]
    public void SameSeedProducesSameDecorations()
    {
        MapData map = RandomMapGenerator.Generate(settings);
        MapDecorationData first = MapDecorationGenerator.Generate(map, settings);
        MapDecorationData second = MapDecorationGenerator.Generate(map, settings);

        Assert.That(first.Count, Is.EqualTo(second.Count));

        for (int index = 0; index < first.Count; index++)
        {
            Assert.That(first.Placements[index].Cell,
                Is.EqualTo(second.Placements[index].Cell));
            Assert.That(first.Placements[index].VariantIndex,
                Is.EqualTo(second.Placements[index].VariantIndex));
        }
    }

    /// <summary>
    /// 验证树木不会占用道路、出生点安全区或出口净空区。
    /// </summary>
    [Test]
    public void DecorationsAvoidGameplayCriticalCells()
    {
        settings.grassDecorationDensity = 1f;
        settings.forestDecorationDensity = 1f;
        settings.decorationMinimumSpacing = 1;

        MapData map = RandomMapGenerator.Generate(settings);
        MapDecorationData decorations = MapDecorationGenerator.Generate(map, settings);

        Assert.That(decorations.Count, Is.GreaterThan(0));

        foreach (MapDecorationPlacement placement in decorations.Placements)
        {
            MapCell cell = map.GetCell(placement.Cell);
            Assert.That(cell.IsWalkable, Is.True);
            Assert.That(cell.terrainType, Is.Not.EqualTo(MapTerrainType.Path));
            Assert.That(ChebyshevDistance(placement.Cell, map.SpawnCell),
                Is.GreaterThan(settings.decorationSpawnClearRadius));
            Assert.That(ChebyshevDistance(placement.Cell, map.ExitCell),
                Is.GreaterThan(settings.decorationExitClearRadius));
        }
    }

    /// <summary>
    /// 验证改变装饰随机偏移不会改变基础地形和道路结果。
    /// </summary>
    [Test]
    public void DecorationRandomStreamDoesNotChangeTerrain()
    {
        MapData firstMap = RandomMapGenerator.Generate(settings);
        settings.decorationSeedOffset += 1;
        MapData secondMap = RandomMapGenerator.Generate(settings);

        Assert.That(firstMap.SpawnCell, Is.EqualTo(secondMap.SpawnCell));
        Assert.That(firstMap.ExitCell, Is.EqualTo(secondMap.ExitCell));

        for (int x = firstMap.Origin.x; x < firstMap.Origin.x + firstMap.Width; x++)
        {
            for (int y = firstMap.Origin.y; y < firstMap.Origin.y + firstMap.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Assert.That(firstMap.GetCell(cell).terrainType,
                    Is.EqualTo(secondMap.GetCell(cell).terrainType));
            }
        }
    }

    /// <summary>
    /// 计算两个网格坐标的 Chebyshev Distance（切比雪夫距离）。
    /// </summary>
    /// <param name="first">第一个网格坐标。</param>
    /// <param name="second">第二个网格坐标。</param>
    /// <returns>两个坐标的最大轴向距离。</returns>
    private static int ChebyshevDistance(Vector2Int first, Vector2Int second)
    {
        return Mathf.Max(
            Mathf.Abs(first.x - second.x),
            Mathf.Abs(first.y - second.y));
    }
}
