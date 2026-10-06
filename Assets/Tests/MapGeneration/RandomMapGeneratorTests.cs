using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证随机地图生成器的可复现性、出生点安全性、边界和道路连通性。
/// </summary>
public class RandomMapGeneratorTests
{
    /// <summary>
    /// 检查可见地形过渡带时使用的八方向邻居偏移。
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
    /// 验证问题种子中的岛屿始终保留可见沙岸，并在高地外围保留草地过渡带。
    /// </summary>
    [Test]
    public void ReportedSeedPreservesVisibleSandAndGrassBands()
    {
        settings.seed = -1391545000;

        MapData map = RandomMapGenerator.Generate(settings);
        List<string> violations = FindTerrainNestingViolations(map);

        Assert.That(
            violations,
            Is.Empty,
            "发现破坏 Water -> Sand -> Grass -> Mountain 可见嵌套关系的单元：\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// 验证用户报告的 Seed（种子）不会生成只能容纳单格的横向或竖向瓶颈。
    /// </summary>
    [Test]
    public void ReportedSeedContainsNoSingleCellPassages()
    {
        settings.seed = 613580675;

        MapData map = RandomMapGenerator.Generate(settings);
        List<string> violations = FindSingleCellPassages(map);

        Assert.That(
            violations,
            Is.Empty,
            "发现现有玩家碰撞体无法穿过的单格通路：\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// 验证用户报告的地图中，每条相邻可行走连接都属于至少一个完整的 2×2 净空块。
    /// </summary>
    /// <param name="seed">用户观察到错位单格通路的 Seed（种子）。</param>
    [TestCase(613580675)]
    [TestCase(839235445)]
    public void ReportedSeedsContainNoUnsupportedWalkableEdges(int seed)
    {
        settings.seed = seed;

        MapData map = RandomMapGenerator.Generate(settings);
        List<string> violations = FindUnsupportedWalkableEdges(map);

        Assert.That(
            violations,
            Is.Empty,
            $"Seed={seed} 存在不属于任何 2×2 净空块的可行走连接：\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// 验证一组代表性 Seed（种子）同时满足可见地形嵌套和最小通路宽度约束。
    /// </summary>
    /// <param name="seed">本次回归测试使用的固定种子。</param>
    [TestCase(-1391545000)]
    [TestCase(613580675)]
    [TestCase(20260929)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void RepresentativeSeedsPreserveTerrainTopologyAndPassageWidth(int seed)
    {
        settings.seed = seed;

        MapData map = RandomMapGenerator.Generate(settings);
        List<string> nestingViolations = FindTerrainNestingViolations(map);
        List<string> passageViolations = FindSingleCellPassages(map);

        Assert.That(
            nestingViolations,
            Is.Empty,
            $"Seed={seed} 破坏了 Water -> Sand -> Grass -> Mountain 可见嵌套关系：\n" +
            string.Join("\n", nestingViolations));
        Assert.That(
            passageViolations,
            Is.Empty,
            $"Seed={seed} 生成了现有玩家碰撞体无法穿过的单格通路：\n" +
            string.Join("\n", passageViolations));
    }

    /// <summary>
    /// 扫描一组固定 Seed（种子），排查未被单个报告样本覆盖的错位可行走连接。
    /// </summary>
    [Test]
    public void SeedSweepContainsNoUnsupportedWalkableEdges()
    {
        for (int index = 0; index < 64; index++)
        {
            settings.seed = unchecked(index * 7919 - 104729);
            MapData map = RandomMapGenerator.Generate(settings);
            List<string> violations = FindUnsupportedWalkableEdges(map);

            Assert.That(
                violations,
                Is.Empty,
                $"Seed={settings.seed} 生成了不属于任何 2×2 净空块的可行走连接：\n" +
                string.Join("\n", violations));
        }
    }

    /// <summary>
    /// 查找草地直接接触水域或高地缺少草地过渡带的单元。
    /// </summary>
    /// <param name="map">待检查的最终地图。</param>
    /// <returns>所有违反可见地形嵌套关系的诊断文本。</returns>
    private static List<string> FindTerrainNestingViolations(MapData map)
    {
        List<string> violations = new List<string>();

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                MapTerrainType terrainType = map.GetCell(cell).terrainType;

                foreach (Vector2Int direction in SurroundingDirections)
                {
                    Vector2Int neighborCell = cell + direction;
                    if (!map.IsInside(neighborCell))
                        continue;

                    MapTerrainType neighborType = map.GetCell(neighborCell).terrainType;
                    if (TerrainTopology.UsesGrassOverlay(terrainType) &&
                        TerrainTopology.IsWater(neighborType))
                    {
                        violations.Add($"{cell} 的 {terrainType} 直接接触 {neighborCell} 的 {neighborType}。");
                        break;
                    }

                    if (terrainType == MapTerrainType.Mountain &&
                        !TerrainTopology.UsesGrassOverlay(neighborType))
                    {
                        violations.Add($"{cell} 的 Mountain 缺少草地过渡带，邻居 {neighborCell} 是 {neighborType}。");
                        break;
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 查找两侧均为阻挡地形、轴向仍然连通的单格宽瓶颈。
    /// </summary>
    /// <param name="map">待检查的最终地图。</param>
    /// <returns>全部横向和竖向单格通路的诊断文本。</returns>
    private static List<string> FindSingleCellPassages(MapData map)
    {
        List<string> violations = new List<string>();

        for (int x = map.Origin.x + 1; x < map.Origin.x + map.Width - 1; x++)
        {
            for (int y = map.Origin.y + 1; y < map.Origin.y + map.Height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!map.IsWalkable(cell))
                    continue;

                bool connectsVertically =
                    map.IsWalkable(cell + Vector2Int.up) &&
                    map.IsWalkable(cell + Vector2Int.down);
                bool blockedHorizontally =
                    !map.IsWalkable(cell + Vector2Int.left) &&
                    !map.IsWalkable(cell + Vector2Int.right);
                bool connectsHorizontally =
                    map.IsWalkable(cell + Vector2Int.left) &&
                    map.IsWalkable(cell + Vector2Int.right);
                bool blockedVertically =
                    !map.IsWalkable(cell + Vector2Int.up) &&
                    !map.IsWalkable(cell + Vector2Int.down);

                if (connectsVertically && blockedHorizontally)
                {
                    violations.Add(
                        $"{cell} 是竖向单格通路，地形为 {map.GetCell(cell).terrainType}。");
                }
                else if (connectsHorizontally && blockedVertically)
                {
                    violations.Add(
                        $"{cell} 是横向单格通路，地形为 {map.GetCell(cell).terrainType}。");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 查找没有任何一侧形成完整 2×2 净空块的横向或竖向可行走连接。
    /// </summary>
    /// <param name="map">待检查的最终地图。</param>
    /// <returns>全部错位单格连接的诊断文本。</returns>
    private static List<string> FindUnsupportedWalkableEdges(MapData map)
    {
        List<string> violations = new List<string>();

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!map.IsWalkable(cell))
                    continue;

                Vector2Int rightCell = cell + Vector2Int.right;
                if (IsWalkableInside(map, rightCell))
                {
                    bool supportedAbove =
                        IsWalkableInside(map, cell + Vector2Int.up) &&
                        IsWalkableInside(map, rightCell + Vector2Int.up);
                    bool supportedBelow =
                        IsWalkableInside(map, cell + Vector2Int.down) &&
                        IsWalkableInside(map, rightCell + Vector2Int.down);
                    if (!supportedAbove && !supportedBelow)
                    {
                        violations.Add(
                            $"横向连接 {cell} -> {rightCell} 缺少 2×2 净空块。\n" +
                            DescribeNeighborhood(map, cell, rightCell));
                    }
                }

                Vector2Int upperCell = cell + Vector2Int.up;
                if (IsWalkableInside(map, upperCell))
                {
                    bool supportedLeft =
                        IsWalkableInside(map, cell + Vector2Int.left) &&
                        IsWalkableInside(map, upperCell + Vector2Int.left);
                    bool supportedRight =
                        IsWalkableInside(map, cell + Vector2Int.right) &&
                        IsWalkableInside(map, upperCell + Vector2Int.right);
                    if (!supportedLeft && !supportedRight)
                    {
                        violations.Add(
                            $"竖向连接 {cell} -> {upperCell} 缺少 2×2 净空块。\n" +
                            DescribeNeighborhood(map, cell, upperCell));
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 判断指定坐标位于地图内且对应地形可行走。
    /// </summary>
    /// <param name="map">待查询的地图。</param>
    /// <param name="cell">待查询的网格坐标。</param>
    /// <returns>坐标有效且可行走时返回 true。</returns>
    private static bool IsWalkableInside(MapData map, Vector2Int cell)
    {
        return map.IsInside(cell) && map.IsWalkable(cell);
    }

    /// <summary>
    /// 输出目标连接周围的五乘五地形，用于定位错位通路形态。
    /// </summary>
    /// <param name="map">待查询的地图。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <returns>包含坐标、可行走状态和地形缩写的诊断文本。</returns>
    private static string DescribeNeighborhood(
        MapData map,
        Vector2Int firstCell,
        Vector2Int secondCell)
    {
        int minimumX = Mathf.Min(firstCell.x, secondCell.x) - 2;
        int maximumX = Mathf.Max(firstCell.x, secondCell.x) + 2;
        int minimumY = Mathf.Min(firstCell.y, secondCell.y) - 2;
        int maximumY = Mathf.Max(firstCell.y, secondCell.y) + 2;
        List<string> rows = new List<string>();

        for (int y = maximumY; y >= minimumY; y--)
        {
            List<string> columns = new List<string>();
            for (int x = minimumX; x <= maximumX; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!map.IsInside(cell))
                {
                    columns.Add("OUT");
                    continue;
                }

                MapTerrainType terrainType = map.GetCell(cell).terrainType;
                string marker = cell == firstCell || cell == secondCell ? "*" : " ";
                columns.Add($"{marker}{GetTerrainAbbreviation(terrainType)}");
            }

            rows.Add($"y={y}: " + string.Join(" ", columns));
        }

        return string.Join("\n", rows);
    }

    /// <summary>
    /// 获取诊断输出使用的地形缩写。
    /// </summary>
    /// <param name="terrainType">待转换的地形类型。</param>
    /// <returns>便于阅读的三字符地形缩写。</returns>
    private static string GetTerrainAbbreviation(MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.DeepWater:
                return "DWT";
            case MapTerrainType.ShallowWater:
                return "SWT";
            case MapTerrainType.Sand:
                return "SND";
            case MapTerrainType.Path:
                return "PTH";
            case MapTerrainType.Forest:
                return "FOR";
            case MapTerrainType.Mountain:
                return "MTN";
            default:
                return "GRS";
        }
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
