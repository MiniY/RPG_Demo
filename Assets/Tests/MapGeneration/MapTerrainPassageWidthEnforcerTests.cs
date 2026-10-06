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

        Assert.That(changedCellCount, Is.GreaterThan(0));
        AssertAllWalkableEdgesHaveClearance(mapData);
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

        Assert.That(changedCellCount, Is.GreaterThan(0));
        AssertAllWalkableEdgesHaveClearance(mapData);
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
    /// 验证上下区域错位连接形成的 S 形夹点会获得完整的 2×2 净空块。
    /// </summary>
    [Test]
    public void StaggeredVerticalPassageReceivesTwoByTwoClearance()
    {
        MapData mapData = CreateFilledMap(9, 9, MapTerrainType.Mountain);
        FillRectangle(mapData, 1, 3, 5, 7, MapTerrainType.Grass);
        FillRectangle(mapData, 4, 6, 1, 3, MapTerrainType.Grass);
        Vector2Int leftConnector = new Vector2Int(3, 4);
        Vector2Int rightConnector = new Vector2Int(4, 4);
        mapData.SetTerrain(leftConnector, MapTerrainType.Grass);
        mapData.SetTerrain(rightConnector, MapTerrainType.Grass);

        Assert.That(
            HasClearanceSupport(mapData, leftConnector, rightConnector),
            Is.False,
            "测试夹具必须先形成用户报告的错位单格连接。");

        MapTerrainPassageWidthEnforcer.Enforce(mapData, 2, 1);

        Assert.That(
            HasClearanceSupport(mapData, leftConnector, rightConnector),
            Is.True,
            "错位连接修复后必须属于至少一个完整的 2×2 净空块。");
    }

    /// <summary>
    /// 验证左右区域错位连接形成的横向旋转形态也会获得完整的 2×2 净空块。
    /// </summary>
    [Test]
    public void StaggeredHorizontalPassageReceivesTwoByTwoClearance()
    {
        MapData mapData = CreateFilledMap(9, 9, MapTerrainType.Mountain);
        FillRectangle(mapData, 1, 3, 1, 3, MapTerrainType.Sand);
        FillRectangle(mapData, 5, 7, 4, 6, MapTerrainType.Sand);
        Vector2Int lowerConnector = new Vector2Int(4, 3);
        Vector2Int upperConnector = new Vector2Int(4, 4);
        mapData.SetTerrain(lowerConnector, MapTerrainType.Sand);
        mapData.SetTerrain(upperConnector, MapTerrainType.Sand);

        Assert.That(
            HasClearanceSupport(mapData, lowerConnector, upperConnector),
            Is.False,
            "测试夹具必须先形成旋转后的错位单格连接。");

        MapTerrainPassageWidthEnforcer.Enforce(mapData, 2, 1);

        Assert.That(
            HasClearanceSupport(mapData, lowerConnector, upperConnector),
            Is.True,
            "旋转后的错位连接修复后必须属于完整的 2×2 净空块。");
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

        Assert.That(changedCellCount, Is.GreaterThan(0));
        AssertAllWalkableEdgesHaveClearance(mapData);
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
    /// 用指定地形填充闭区间矩形。
    /// </summary>
    /// <param name="mapData">待修改的地图数据。</param>
    /// <param name="minimumX">矩形最小横坐标。</param>
    /// <param name="maximumX">矩形最大横坐标。</param>
    /// <param name="minimumY">矩形最小纵坐标。</param>
    /// <param name="maximumY">矩形最大纵坐标。</param>
    /// <param name="terrainType">矩形使用的地形类型。</param>
    private static void FillRectangle(
        MapData mapData,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY,
        MapTerrainType terrainType)
    {
        for (int x = minimumX; x <= maximumX; x++)
        {
            for (int y = minimumY; y <= maximumY; y++)
                mapData.SetTerrain(new Vector2Int(x, y), terrainType);
        }
    }

    /// <summary>
    /// 判断两个相邻单元是否至少在一侧形成完整的 2×2 净空块。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <returns>连接拥有完整净空块时返回 true。</returns>
    private static bool HasClearanceSupport(
        MapData mapData,
        Vector2Int firstCell,
        Vector2Int secondCell)
    {
        Vector2Int delta = secondCell - firstCell;
        Assert.That(
            Mathf.Abs(delta.x) + Mathf.Abs(delta.y),
            Is.EqualTo(1),
            "净空检查只接受横向或竖向相邻单元。");

        Vector2Int firstSide = delta.x != 0
            ? Vector2Int.up
            : Vector2Int.right;
        Vector2Int secondSide = -firstSide;
        return IsWalkableInside(mapData, firstCell + firstSide) &&
               IsWalkableInside(mapData, secondCell + firstSide) ||
               IsWalkableInside(mapData, firstCell + secondSide) &&
               IsWalkableInside(mapData, secondCell + secondSide);
    }

    /// <summary>
    /// 验证地图中的每条轴向可行走连接都拥有完整的 2×2 净空块。
    /// </summary>
    /// <param name="mapData">待检查的地图数据。</param>
    private static void AssertAllWalkableEdgesHaveClearance(MapData mapData)
    {
        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!mapData.IsWalkable(cell))
                    continue;

                Vector2Int rightCell = cell + Vector2Int.right;
                if (IsWalkableInside(mapData, rightCell))
                {
                    Assert.That(
                        HasClearanceSupport(mapData, cell, rightCell),
                        Is.True,
                        $"横向连接 {cell} -> {rightCell} 缺少 2×2 净空块。");
                }

                Vector2Int upperCell = cell + Vector2Int.up;
                if (IsWalkableInside(mapData, upperCell))
                {
                    Assert.That(
                        HasClearanceSupport(mapData, cell, upperCell),
                        Is.True,
                        $"竖向连接 {cell} -> {upperCell} 缺少 2×2 净空块。");
                }
            }
        }
    }

    /// <summary>
    /// 判断指定坐标位于地图内且可行走。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">待查询的网格坐标。</param>
    /// <returns>坐标有效且可行走时返回 true。</returns>
    private static bool IsWalkableInside(MapData mapData, Vector2Int cell)
    {
        return mapData.IsInside(cell) && mapData.IsWalkable(cell);
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
