using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证随机地图生成器的可复现性、出生点安全性、边界和道路连通性。
/// </summary>
public class RandomMapGeneratorTests
{
    /// <summary>
    /// 当前测试使用的临时地图配置。
    /// </summary>
    private MapGenerationSettings settings;

    /// <summary>
    /// 创建测试所需的有效地图配置。
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
        settings.mountainHeightThreshold = 0.72f;
        settings.minimumNaturalRegionSize = 3;
        settings.spawnProtectionRadius = 5;
        settings.roadWidth = 2;
        settings.roadTurnChance = 0.3f;
    }

    /// <summary>
    /// 销毁测试配置，避免测试之间共享 ScriptableObject 状态。
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(settings);
    }

    /// <summary>
    /// 验证相同 Seed（种子）会生成完全一致的地形数据。
    /// </summary>
    [Test]
    public void SameSeedProducesSameMap()
    {
        MapData firstMap = RandomMapGenerator.Generate(settings);
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
    /// 验证出生点及其保护区域不会落在水域中。
    /// </summary>
    [Test]
    public void SpawnAreaIsWalkable()
    {
        MapData map = RandomMapGenerator.Generate(settings);

        Assert.That(map.IsWalkable(map.SpawnCell), Is.True);

        for (int x = map.SpawnCell.x - settings.spawnProtectionRadius;
             x <= map.SpawnCell.x + settings.spawnProtectionRadius;
             x++)
        {
            for (int y = map.SpawnCell.y - settings.spawnProtectionRadius;
                 y <= map.SpawnCell.y + settings.spawnProtectionRadius;
                 y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Assert.That(map.IsInside(cell), Is.True);
                Assert.That(map.IsWalkable(cell), Is.True);
            }
        }
    }

    /// <summary>
    /// 验证地图外圈是深水，避免玩家从测试地图边界离开。
    /// </summary>
    [Test]
    public void BorderIsWater()
    {
        MapData map = RandomMapGenerator.Generate(settings);

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (map.IsBorder(cell, settings.borderSize))
                    Assert.That(map.GetCell(cell).terrainType, Is.EqualTo(MapTerrainType.DeepWater));
            }
        }
    }

    /// <summary>
    /// 验证浅水、森林、山地和道路遵循各自的可行走规则。
    /// </summary>
    [Test]
    public void ExtendedTerrainWalkabilityMatchesDesign()
    {
        MapData map = new MapData(6, 1, Vector2Int.zero);
        map.SetTerrain(new Vector2Int(0, 0), MapTerrainType.DeepWater);
        map.SetTerrain(new Vector2Int(1, 0), MapTerrainType.ShallowWater);
        map.SetTerrain(new Vector2Int(2, 0), MapTerrainType.Forest);
        map.SetTerrain(new Vector2Int(3, 0), MapTerrainType.Mountain);
        map.SetTerrain(new Vector2Int(4, 0), MapTerrainType.Path);
        map.SetTerrain(new Vector2Int(5, 0), MapTerrainType.Sand);

        Assert.That(map.IsWalkable(new Vector2Int(0, 0)), Is.False);
        Assert.That(map.IsWalkable(new Vector2Int(1, 0)), Is.False);
        Assert.That(map.IsWalkable(new Vector2Int(2, 0)), Is.True);
        Assert.That(map.IsWalkable(new Vector2Int(3, 0)), Is.False);
        Assert.That(map.IsWalkable(new Vector2Int(4, 0)), Is.True);
        Assert.That(map.IsWalkable(new Vector2Int(5, 0)), Is.True);
    }

    /// <summary>
    /// 验证默认 Seed（种子）会实际生成浅水、沙地、森林和山地，而不是只存在枚举定义。
    /// </summary>
    [Test]
    public void DefaultConfigurationProducesExtendedTerrain()
    {
        MapData map = RandomMapGenerator.Generate(settings);
        int shallowWaterCount = 0;
        int sandCount = 0;
        int forestCount = 0;
        int mountainCount = 0;

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                MapTerrainType terrainType = map.GetCell(new Vector2Int(x, y)).terrainType;
                if (terrainType == MapTerrainType.ShallowWater)
                    shallowWaterCount++;
                else if (terrainType == MapTerrainType.Sand)
                    sandCount++;
                else if (terrainType == MapTerrainType.Forest)
                    forestCount++;
                else if (terrainType == MapTerrainType.Mountain)
                    mountainCount++;
            }
        }

        Assert.That(shallowWaterCount, Is.GreaterThan(0));
        Assert.That(sandCount, Is.GreaterThan(0));
        Assert.That(forestCount, Is.GreaterThan(0));
        Assert.That(mountainCount, Is.GreaterThan(0));
    }

    /// <summary>
    /// 验证出生点可以沿可行走单元到达道路出口。
    /// </summary>
    [Test]
    public void SpawnCanReachExit()
    {
        MapData map = RandomMapGenerator.Generate(settings);
        HashSet<Vector2Int> reachableCells = FindReachableCells(map, map.SpawnCell);

        Assert.That(map.IsWalkable(map.ExitCell), Is.True);
        Assert.That(reachableCells.Contains(map.ExitCell), Is.True);
    }

    /// <summary>
    /// 验证地形高度阈值没有严格递增时会立即报告配置错误。
    /// </summary>
    [Test]
    public void TerrainThresholdsMustBeStrictlyIncreasing()
    {
        settings.sandHeightThreshold = settings.shallowWaterThreshold;

        System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
            () => RandomMapGenerator.Generate(settings));

        Assert.That(exception.Message, Does.Contain("water < shallowWater < sand < mountain"));
    }

    /// <summary>
    /// 使用四方向 Flood Fill（洪水填充）收集出生点可以到达的单元。
    /// </summary>
    /// <param name="map">待检查的地图。</param>
    /// <param name="startCell">检查起点。</param>
    /// <returns>可到达单元集合。</returns>
    private static HashSet<Vector2Int> FindReachableCells(MapData map, Vector2Int startCell)
    {
        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
        Queue<Vector2Int> pendingCells = new Queue<Vector2Int>();
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        visitedCells.Add(startCell);
        pendingCells.Enqueue(startCell);

        while (pendingCells.Count > 0)
        {
            Vector2Int currentCell = pendingCells.Dequeue();

            foreach (Vector2Int direction in directions)
            {
                Vector2Int nextCell = currentCell + direction;
                if (!map.IsInside(nextCell) || !map.IsWalkable(nextCell))
                    continue;

                if (visitedCells.Add(nextCell))
                    pendingCells.Enqueue(nextCell);
            }
        }

        return visitedCells;
    }
}
