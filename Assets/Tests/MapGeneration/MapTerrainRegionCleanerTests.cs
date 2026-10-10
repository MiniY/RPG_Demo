using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证自然区域清理器的连通分量、面积和排除规则。
/// </summary>
public class MapTerrainRegionCleanerTests
{
    /// <summary>
    /// 验证单个孤立沙地会被周围占多数的草地替换。
    /// </summary>
    [Test]
    public void IsolatedNaturalCellIsReplacedByNeighborMajority()
    {
        MapData mapData = CreateFilledMap(5, 5, MapTerrainType.Grass);
        Vector2Int isolatedCell = new Vector2Int(2, 2);
        mapData.SetTerrain(isolatedCell, MapTerrainType.Sand);

        int changedCount = MapTerrainRegionCleaner.Clean(
            mapData,
            3,
            0,
            Vector2Int.zero,
            -1);

        Assert.That(changedCount, Is.EqualTo(1));
        Assert.That(mapData.GetCell(isolatedCell).terrainType, Is.EqualTo(MapTerrainType.Grass));
    }

    /// <summary>
    /// 验证长度为三的一格宽自然区域达到最小面积后仍然保留。
    /// </summary>
    [Test]
    public void NarrowRegionAtMinimumSizeIsPreserved()
    {
        MapData mapData = CreateFilledMap(5, 5, MapTerrainType.Grass);
        mapData.SetTerrain(new Vector2Int(2, 1), MapTerrainType.Sand);
        mapData.SetTerrain(new Vector2Int(2, 2), MapTerrainType.Sand);
        mapData.SetTerrain(new Vector2Int(2, 3), MapTerrainType.Sand);

        int changedCount = MapTerrainRegionCleaner.Clean(
            mapData,
            3,
            0,
            Vector2Int.zero,
            -1);

        Assert.That(changedCount, Is.EqualTo(0));
        Assert.That(
            mapData.GetCell(new Vector2Int(2, 2)).terrainType,
            Is.EqualTo(MapTerrainType.Sand));
    }

    /// <summary>
    /// 验证地图外圈和出生保护区不会参与自然区域清理。
    /// </summary>
    [Test]
    public void BorderAndSpawnProtectionCellsAreExcluded()
    {
        MapData mapData = CreateFilledMap(5, 5, MapTerrainType.Grass);
        Vector2Int borderCell = new Vector2Int(0, 2);
        Vector2Int protectedCell = new Vector2Int(2, 2);
        mapData.SetTerrain(borderCell, MapTerrainType.Sand);
        mapData.SetTerrain(protectedCell, MapTerrainType.Forest);

        int changedCount = MapTerrainRegionCleaner.Clean(
            mapData,
            3,
            1,
            protectedCell,
            0);

        Assert.That(changedCount, Is.EqualTo(0));
        Assert.That(mapData.GetCell(borderCell).terrainType, Is.EqualTo(MapTerrainType.Sand));
        Assert.That(mapData.GetCell(protectedCell).terrainType, Is.EqualTo(MapTerrainType.Forest));
    }

    /// <summary>
    /// 验证道路不属于自然地形清理对象，即使只有一个单元也必须保留。
    /// </summary>
    [Test]
    public void PathCellsAreExcludedFromNaturalRegionCleanup()
    {
        MapData mapData = CreateFilledMap(5, 5, MapTerrainType.Grass);
        Vector2Int pathCell = new Vector2Int(2, 2);
        mapData.SetTerrain(pathCell, MapTerrainType.Path);

        int changedCount = MapTerrainRegionCleaner.Clean(
            mapData,
            3,
            0,
            Vector2Int.zero,
            -1);

        Assert.That(changedCount, Is.EqualTo(0));
        Assert.That(mapData.GetCell(pathCell).terrainType, Is.EqualTo(MapTerrainType.Path));
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
        MapData mapData = new MapData(width, height, Vector2Int.zero);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                mapData.SetTerrain(new Vector2Int(x, y), terrainType);
        }

        return mapData;
    }
}
