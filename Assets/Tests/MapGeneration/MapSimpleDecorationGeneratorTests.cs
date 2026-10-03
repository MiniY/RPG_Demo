using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证简单装饰生成的确定性、完整占地、地形限制和随机流隔离。
/// </summary>
public class MapSimpleDecorationGeneratorTests
{
    /// <summary>
    /// 当前测试使用的临时地图配置。
    /// </summary>
    private MapGenerationSettings settings;

    /// <summary>
    /// 当前测试使用的临时简单装饰调色板。
    /// </summary>
    private MapSimpleDecorationPalette palette;

    /// <summary>
    /// 提供给全部测试变体的临时 Tile（瓦片）。
    /// </summary>
    private Tile visualTile;

    /// <summary>
    /// 创建测试所需的配置、调色板和固定装饰样式。
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
        settings.sandHeightThreshold = 0.48f;
        settings.biomeNoiseScale = 0.055f;
        settings.forestMoistureThreshold = 0.58f;
        settings.forestTemperatureThreshold = 0.45f;
        settings.mountainHeightThreshold = 0.72f;
        settings.mountainTemperatureThreshold = 0.55f;
        settings.spawnProtectionRadius = 5;
        settings.roadWidth = 2;
        settings.roadTurnChance = 0.3f;
        settings.simpleDecorationSeedOffset = 7919;
        settings.simpleDecorationNoiseScale = 0.12f;
        settings.grassTreeDensity = 0.018f;
        settings.forestTreeDensity = 0.1f;
        settings.grassBushDensity = 0.025f;
        settings.forestBushDensity = 0.06f;
        settings.sandRockDensity = 0.025f;
        settings.grassRockDensity = 0.012f;
        settings.forestRockDensity = 0.008f;
        settings.simpleDecorationMinimumSpacing = 2;
        settings.simpleDecorationSpawnClearRadius = 6;
        settings.simpleDecorationExitClearRadius = 2;

        visualTile = ScriptableObject.CreateInstance<Tile>();
        palette = ScriptableObject.CreateInstance<MapSimpleDecorationPalette>();
        palette.Configure(
            "test-simple-decoration-v1",
            new[]
            {
                CreateTreeVariant(),
                CreateSingleCellVariant(
                    "bush-test-01",
                    MapSimpleDecorationType.Bush,
                    MapSimpleDecorationTerrainMask.Grass |
                    MapSimpleDecorationTerrainMask.Forest,
                    false),
                CreateSingleCellVariant(
                    "rock-test-01",
                    MapSimpleDecorationType.ScatteredRock,
                    MapSimpleDecorationTerrainMask.Grass |
                    MapSimpleDecorationTerrainMask.Forest |
                    MapSimpleDecorationTerrainMask.Sand,
                    true)
            });
        settings.simpleDecorationPalette = palette;
    }

    /// <summary>
    /// 销毁测试对象，避免 ScriptableObject（可编程对象）泄漏到其他测试。
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(visualTile);
        Object.DestroyImmediate(palette);
        Object.DestroyImmediate(settings);
    }

    /// <summary>
    /// 验证相同地图、配置和 Seed（种子）会得到完全一致的简单装饰结果。
    /// </summary>
    [Test]
    public void SameSeedProducesSameSimpleDecorations()
    {
        MapData map = RandomMapGenerator.Generate(settings);
        MapSimpleDecorationData first =
            MapSimpleDecorationGenerator.Generate(map, settings);
        MapSimpleDecorationData second =
            MapSimpleDecorationGenerator.Generate(map, settings);

        Assert.That(first.Count, Is.EqualTo(second.Count));

        for (int index = 0; index < first.Count; index++)
        {
            Assert.That(first.Placements[index].AnchorCell,
                Is.EqualTo(second.Placements[index].AnchorCell));
            Assert.That(first.Placements[index].DecorationType,
                Is.EqualTo(second.Placements[index].DecorationType));
            Assert.That(first.Placements[index].VariantId,
                Is.EqualTo(second.Placements[index].VariantId));
        }
    }

    /// <summary>
    /// 验证每个装饰物的完整 Footprint（占地范围）都避开水域、道路和关键区域。
    /// </summary>
    [Test]
    public void CompleteFootprintsAvoidInvalidAndGameplayCriticalCells()
    {
        settings.grassTreeDensity = 1f;
        settings.forestTreeDensity = 1f;
        settings.grassBushDensity = 1f;
        settings.forestBushDensity = 1f;
        settings.sandRockDensity = 1f;
        settings.grassRockDensity = 1f;
        settings.forestRockDensity = 1f;
        settings.simpleDecorationMinimumSpacing = 1;
        settings.simpleDecorationSpawnClearRadius = 2;
        settings.simpleDecorationExitClearRadius = 1;

        MapData map = CreateMixedTerrainMap(24, 24);
        MapSimpleDecorationData decorations =
            MapSimpleDecorationGenerator.Generate(map, settings);
        HashSet<Vector2Int> verifiedCells = new HashSet<Vector2Int>();

        Assert.That(decorations.Count, Is.GreaterThan(0));

        foreach (MapSimpleDecorationPlacement placement in decorations.Placements)
        {
            MapSimpleDecorationVariant variant =
                palette.FindVariant(placement.VariantId);
            Assert.That(variant, Is.Not.Null);

            for (int offsetX = variant.FootprintMinimum.x;
                 offsetX <= variant.FootprintMaximum.x;
                 offsetX++)
            {
                for (int offsetY = variant.FootprintMinimum.y;
                     offsetY <= variant.FootprintMaximum.y;
                     offsetY++)
                {
                    Vector2Int cell = placement.AnchorCell +
                                      new Vector2Int(offsetX, offsetY);
                    Assert.That(map.IsInside(cell), Is.True);
                    Assert.That(map.IsWalkable(cell), Is.True);
                    Assert.That(map.GetCell(cell).terrainType,
                        Is.Not.EqualTo(MapTerrainType.Path));
                    Assert.That(variant.SupportsTerrain(
                        map.GetCell(cell).terrainType), Is.True);
                    Assert.That(ChebyshevDistance(cell, map.SpawnCell),
                        Is.GreaterThan(settings.simpleDecorationSpawnClearRadius));
                    Assert.That(ChebyshevDistance(cell, map.ExitCell),
                        Is.GreaterThan(settings.simpleDecorationExitClearRadius));
                    Assert.That(verifiedCells.Add(cell), Is.True,
                        $"简单装饰占地发生重叠：{cell}。");
                }
            }
        }
    }

    /// <summary>
    /// 验证散落岩石只会出现在陆地上，绝不会直接生成在深水或浅水中。
    /// </summary>
    [Test]
    public void ScatteredRocksNeverAppearInWater()
    {
        DisableTreesAndBushes();
        settings.sandRockDensity = 1f;
        settings.grassRockDensity = 1f;
        settings.forestRockDensity = 1f;
        settings.simpleDecorationMinimumSpacing = 1;
        settings.simpleDecorationSpawnClearRadius = 0;
        settings.simpleDecorationExitClearRadius = 0;

        MapData map = CreateFilledMap(16, 16, MapTerrainType.DeepWater);
        map.SpawnCell = new Vector2Int(0, 0);
        map.ExitCell = new Vector2Int(15, 15);

        for (int x = 3; x <= 12; x++)
        {
            for (int y = 3; y <= 12; y++)
            {
                map.SetTerrain(
                    new Vector2Int(x, y),
                    y <= 6 ? MapTerrainType.Sand : MapTerrainType.Grass);
            }
        }

        MapSimpleDecorationData decorations =
            MapSimpleDecorationGenerator.Generate(map, settings);
        IReadOnlyList<MapSimpleDecorationPlacement> rocks = decorations.Placements
            .Where(placement =>
                placement.DecorationType == MapSimpleDecorationType.ScatteredRock)
            .ToList();

        Assert.That(rocks.Count, Is.GreaterThan(0));
        foreach (MapSimpleDecorationPlacement rock in rocks)
        {
            Assert.That(TerrainTopology.IsWater(
                map.GetCell(rock.AnchorCell).terrainType), Is.False);
        }
    }

    /// <summary>
    /// 验证修改灌木密度不会重排树木类别自己的随机序列和放置结果。
    /// </summary>
    [Test]
    public void BushDensityDoesNotChangeTreePlacements()
    {
        MapData map = CreateFilledMap(24, 24, MapTerrainType.Forest);
        map.SpawnCell = new Vector2Int(0, 0);
        map.ExitCell = new Vector2Int(23, 23);
        settings.forestTreeDensity = 0.2f;
        settings.grassTreeDensity = 0.2f;
        settings.forestBushDensity = 0f;
        settings.grassBushDensity = 0f;
        settings.sandRockDensity = 0f;
        settings.grassRockDensity = 0f;
        settings.forestRockDensity = 0f;

        MapSimpleDecorationData withoutBushes =
            MapSimpleDecorationGenerator.Generate(map, settings);

        settings.forestBushDensity = 1f;
        settings.grassBushDensity = 1f;
        MapSimpleDecorationData withBushes =
            MapSimpleDecorationGenerator.Generate(map, settings);

        List<MapSimpleDecorationPlacement> firstTrees = withoutBushes.Placements
            .Where(placement =>
                placement.DecorationType == MapSimpleDecorationType.Tree)
            .ToList();
        List<MapSimpleDecorationPlacement> secondTrees = withBushes.Placements
            .Where(placement =>
                placement.DecorationType == MapSimpleDecorationType.Tree)
            .ToList();

        Assert.That(secondTrees.Count, Is.EqualTo(firstTrees.Count));
        for (int index = 0; index < firstTrees.Count; index++)
        {
            Assert.That(secondTrees[index].AnchorCell,
                Is.EqualTo(firstTrees[index].AnchorCell));
            Assert.That(secondTrees[index].VariantId,
                Is.EqualTo(firstTrees[index].VariantId));
        }
    }

    /// <summary>
    /// 验证完整 3×3 树木占地会被登记，其他装饰不能进入透明角落或树冠范围。
    /// </summary>
    [Test]
    public void TreeFootprintReservesAllNineCells()
    {
        settings.grassTreeDensity = 1f;
        settings.forestTreeDensity = 1f;
        settings.grassBushDensity = 0f;
        settings.forestBushDensity = 0f;
        settings.sandRockDensity = 0f;
        settings.grassRockDensity = 0f;
        settings.forestRockDensity = 0f;
        settings.simpleDecorationMinimumSpacing = 1;
        settings.simpleDecorationSpawnClearRadius = 0;
        settings.simpleDecorationExitClearRadius = 0;

        MapData map = CreateFilledMap(16, 16, MapTerrainType.Grass);
        map.SpawnCell = new Vector2Int(0, 0);
        map.ExitCell = new Vector2Int(15, 15);
        MapSimpleDecorationData decorations =
            MapSimpleDecorationGenerator.Generate(map, settings);
        int treeCount = decorations.CountByType(MapSimpleDecorationType.Tree);

        Assert.That(treeCount, Is.GreaterThan(0));
        Assert.That(decorations.OccupiedCells.Count, Is.EqualTo(treeCount * 9));
    }

    /// <summary>
    /// 验证修改简单装饰随机偏移不会改变基础地形和道路结果。
    /// </summary>
    [Test]
    public void SimpleDecorationRandomStreamDoesNotChangeTerrain()
    {
        MapData firstMap = RandomMapGenerator.Generate(settings);
        settings.simpleDecorationSeedOffset += 1;
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
    /// 创建测试使用的 3×3 树木固定样式。
    /// </summary>
    /// <returns>占地为九格、树桩中心产生碰撞的树木变体。</returns>
    private MapSimpleDecorationVariant CreateTreeVariant()
    {
        return new MapSimpleDecorationVariant(
            "tree-test-01",
            MapSimpleDecorationType.Tree,
            MapSimpleDecorationTerrainMask.Grass |
            MapSimpleDecorationTerrainMask.Forest,
            1,
            new Vector2Int(-1, 0),
            new Vector2Int(1, 2),
            new[]
            {
                new MapSimpleDecorationTilePart(
                    Vector2Int.zero,
                    visualTile,
                    MapSimpleDecorationRenderLayer.Ground)
            },
            new[] { Vector2Int.zero });
    }

    /// <summary>
    /// 创建一个单格灌木或散落岩石固定样式。
    /// </summary>
    /// <param name="variantId">稳定变体编号。</param>
    /// <param name="decorationType">简单装饰类别。</param>
    /// <param name="allowedTerrains">允许出现的地形集合。</param>
    /// <param name="hasCollision">锚点是否需要产生碰撞。</param>
    /// <returns>创建完成的单格固定样式。</returns>
    private MapSimpleDecorationVariant CreateSingleCellVariant(
        string variantId,
        MapSimpleDecorationType decorationType,
        MapSimpleDecorationTerrainMask allowedTerrains,
        bool hasCollision)
    {
        return new MapSimpleDecorationVariant(
            variantId,
            decorationType,
            allowedTerrains,
            1,
            Vector2Int.zero,
            Vector2Int.zero,
            new[]
            {
                new MapSimpleDecorationTilePart(
                    Vector2Int.zero,
                    visualTile,
                    MapSimpleDecorationRenderLayer.Ground)
            },
            hasCollision ? new[] { Vector2Int.zero } : new Vector2Int[0]);
    }

    /// <summary>
    /// 创建同时包含水域、沙地、草地、森林和道路的测试地图。
    /// </summary>
    /// <param name="width">地图宽度。</param>
    /// <param name="height">地图高度。</param>
    /// <returns>完成地形分区和关键位置设置的测试地图。</returns>
    private static MapData CreateMixedTerrainMap(int width, int height)
    {
        MapData map = CreateFilledMap(width, height, MapTerrainType.Grass);
        map.SpawnCell = new Vector2Int(2, 2);
        map.ExitCell = new Vector2Int(width - 3, height - 3);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    map.SetTerrain(cell, MapTerrainType.DeepWater);
                else if (y <= 5)
                    map.SetTerrain(cell, MapTerrainType.Sand);
                else if (y >= height / 2)
                    map.SetTerrain(cell, MapTerrainType.Forest);
            }
        }

        for (int y = 1; y < height - 1; y++)
            map.SetTerrain(new Vector2Int(width / 2, y), MapTerrainType.Path);

        map.SetTerrain(map.SpawnCell, MapTerrainType.Grass);
        map.SetTerrain(map.ExitCell, MapTerrainType.Grass);
        return map;
    }

    /// <summary>
    /// 创建并填充指定尺寸的测试地图。
    /// </summary>
    /// <param name="width">测试地图宽度。</param>
    /// <param name="height">测试地图高度。</param>
    /// <param name="terrainType">所有单元使用的初始地形。</param>
    /// <returns>填充完成的测试地图。</returns>
    private static MapData CreateFilledMap(
        int width,
        int height,
        MapTerrainType terrainType)
    {
        MapData map = new MapData(width, height, Vector2Int.zero);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                map.SetTerrain(new Vector2Int(x, y), terrainType);
        }

        return map;
    }

    /// <summary>
    /// 将树木和灌木密度设置为零，只测试散落岩石。
    /// </summary>
    private void DisableTreesAndBushes()
    {
        settings.grassTreeDensity = 0f;
        settings.forestTreeDensity = 0f;
        settings.grassBushDensity = 0f;
        settings.forestBushDensity = 0f;
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
