using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证地图通路宽度约束器会扩宽单格瓶颈，同时保护边界和原通路地形类型。
/// </summary>
public class MapTerrainPassageWidthEnforcerTests
{
    /// <summary>
    /// 验证竖向单格通路会沿水平方向扩宽为两格。
    /// </summary>
    [Test]
    public void VerticalSingleCellPassageExpandsToTwoCells()
    {
        MapData mapData = CreateFilledMap(9, 9, MapTerrainType.Mountain);
        SetVerticalPassage(mapData, 4, 3, 5, MapTerrainType.Path);
        SetHorizontalPassage(mapData, 2, 3, 5, MapTerrainType.Path);
        SetHorizontalPassage(mapData, 6, 3, 5, MapTerrainType.Path);

        int changedCellCount = MapTerrainPassageWidthEnforcer.Enforce(
            mapData,
            2,
            1);

        Assert.That(changedCellCount, Is.EqualTo(3));
        for (int y = 3; y <= 5; y++)
        {
            Assert.That(
                mapData.GetCell(new Vector2Int(3, y)).terrainType,
                Is.EqualTo(MapTerrainType.Path));
            Assert.That(
                mapData.GetCell(new Vector2Int(4, y)).terrainType,
                Is.EqualTo(MapTerrainType.Path));
        }
    }

    /// <summary>
    /// 验证横向单格通路会沿垂直方向扩宽为两格。
    /// </summary>
    [Test]
    public void HorizontalSingleCellPassageExpandsToTwoCells()
    {
        MapData mapData = CreateFilledMap(9, 9, MapTerrainType.Mountain);
        SetHorizontalPassage(mapData, 4, 3, 5, MapTerrainType.Sand);
        SetVerticalPassage(mapData, 2, 3, 5, MapTerrainType.Sand);
        SetVerticalPassage(mapData, 6, 3, 5, MapTerrainType.Sand);

        int changedCellCount = MapTerrainPassageWidthEnforcer.Enforce(
            mapData,
            2,
            1);

        Assert.That(changedCellCount, Is.EqualTo(3));
        for (int x = 3; x <= 5; x++)
        {
            Assert.That(
                mapData.GetCell(new Vector2Int(x, 3)).terrainType,
                Is.EqualTo(MapTerrainType.Sand));
            Assert.That(
                mapData.GetCell(new Vector2Int(x, 4)).terrainType,
                Is.EqualTo(MapTerrainType.Sand));
        }
    }

    /// <summary>
    /// 验证已经达到两格宽的通路不会被继续改写。
    /// </summary>
    [Test]
    public void ExistingTwoCellPassageRemainsUnchanged()
    {
        MapData mapData = CreateFilledMap(7, 7, MapTerrainType.Mountain);
        SetVerticalPassage(mapData, 3, 1, 5, MapTerrainType.Grass);
        SetVerticalPassage(mapData, 4, 1, 5, MapTerrainType.Grass);

        int changedCellCount = MapTerrainPassageWidthEnforcer.Enforce(
            mapData,
            2,
            1);

        Assert.That(changedCellCount, Is.EqualTo(0));
        Assert.That(CountTerrain(mapData, MapTerrainType.Grass), Is.EqualTo(10));
    }

    /// <summary>
    /// 验证多格地图边界保护带不会被通路扩宽逻辑侵入。
    /// </summary>
    [Test]
    public void ExpansionDoesNotEnterMultiCellBorder()
    {
        MapData mapData = CreateFilledMap(8, 8, MapTerrainType.DeepWater);
        SetVerticalPassage(mapData, 2, 3, 4, MapTerrainType.Forest);
        SetHorizontalPassage(mapData, 2, 2, 4, MapTerrainType.Forest);
        SetHorizontalPassage(mapData, 5, 2, 4, MapTerrainType.Forest);

        int changedCellCount = MapTerrainPassageWidthEnforcer.Enforce(
            mapData,
            2,
            2);

        Assert.That(changedCellCount, Is.EqualTo(2));
        for (int y = 2; y <= 5; y++)
        {
            Assert.That(
                mapData.GetCell(new Vector2Int(1, y)).terrainType,
                Is.EqualTo(MapTerrainType.DeepWater),
                $"边界单元 (1, {y}) 不应被通路扩宽逻辑改写。");
        }

        for (int y = 3; y <= 4; y++)
        {
            Assert.That(
                mapData.GetCell(new Vector2Int(3, y)).terrainType,
                Is.EqualTo(MapTerrainType.Forest));
        }
    }

    /// <summary>
    /// 验证扩宽后的单元会保持原通路使用的可行走地形类型。
    /// </summary>
    /// <param name="terrainType">原通路使用的可行走地形类型。</param>
    [TestCase(MapTerrainType.Path)]
    [TestCase(MapTerrainType.Sand)]
    [TestCase(MapTerrainType.Grass)]
    [TestCase(MapTerrainType.Forest)]
    public void ExpansionPreservesPassageTerrainType(MapTerrainType terrainType)
    {
        MapData mapData = CreateFilledMap(9, 9, MapTerrainType.Mountain);
        SetVerticalPassage(mapData, 4, 3, 5, terrainType);
        SetHorizontalPassage(mapData, 2, 3, 5, terrainType);
        SetHorizontalPassage(mapData, 6, 3, 5, terrainType);

        MapTerrainPassageWidthEnforcer.Enforce(mapData, 2, 1);

        for (int y = 3; y <= 5; y++)
        {
            Assert.That(
                mapData.GetCell(new Vector2Int(3, y)).terrainType,
                Is.EqualTo(terrainType));
        }
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

    /// <summary>
    /// 在指定列中写入一段竖向通路。
    /// </summary>
    /// <param name="mapData">待修改的地图数据。</param>
    /// <param name="x">通路所在列。</param>
    /// <param name="startY">通路起始纵坐标。</param>
    /// <param name="endY">通路结束纵坐标。</param>
    /// <param name="terrainType">通路使用的地形类型。</param>
    private static void SetVerticalPassage(
        MapData mapData,
        int x,
        int startY,
        int endY,
        MapTerrainType terrainType)
    {
        for (int y = startY; y <= endY; y++)
            mapData.SetTerrain(new Vector2Int(x, y), terrainType);
    }

    /// <summary>
    /// 在指定行中写入一段横向通路。
    /// </summary>
    /// <param name="mapData">待修改的地图数据。</param>
    /// <param name="y">通路所在行。</param>
    /// <param name="startX">通路起始横坐标。</param>
    /// <param name="endX">通路结束横坐标。</param>
    /// <param name="terrainType">通路使用的地形类型。</param>
    private static void SetHorizontalPassage(
        MapData mapData,
        int y,
        int startX,
        int endX,
        MapTerrainType terrainType)
    {
        for (int x = startX; x <= endX; x++)
            mapData.SetTerrain(new Vector2Int(x, y), terrainType);
    }

    /// <summary>
    /// 统计地图中指定地形类型的单元数量。
    /// </summary>
    /// <param name="mapData">待统计的地图数据。</param>
    /// <param name="terrainType">需要统计的地形类型。</param>
    /// <returns>匹配的地图单元数量。</returns>
    private static int CountTerrain(
        MapData mapData,
        MapTerrainType terrainType)
    {
        int count = 0;

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                if (mapData.GetCell(new Vector2Int(x, y)).terrainType == terrainType)
                    count++;
            }
        }

        return count;
    }
}
