using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证地图地形嵌套规则执行器生成稳定的沙岸和草地过渡带。
/// </summary>
public class MapTerrainNestingEnforcerTests
{
    /// <summary>
    /// 验证五乘五裸岩孤岛会被规范为外圈沙地、中圈草地和中心高地。
    /// </summary>
    [Test]
    public void MountainIslandReceivesSandAndGrassTransitionBands()
    {
        MapData mapData = CreateFilledMap(7, 7, MapTerrainType.DeepWater);

        for (int x = 1; x <= 5; x++)
        {
            for (int y = 1; y <= 5; y++)
                mapData.SetTerrain(new Vector2Int(x, y), MapTerrainType.Mountain);
        }

        int changedCellCount = MapTerrainNestingEnforcer.Enforce(mapData);

        Assert.That(changedCellCount, Is.EqualTo(24));
        for (int x = 1; x <= 5; x++)
        {
            for (int y = 1; y <= 5; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                int distanceFromCenter = Mathf.Max(Mathf.Abs(x - 3), Mathf.Abs(y - 3));
                MapTerrainType expectedTerrain = distanceFromCenter == 2
                    ? MapTerrainType.Sand
                    : distanceFromCenter == 1
                        ? MapTerrainType.Grass
                        : MapTerrainType.Mountain;

                Assert.That(
                    mapData.GetCell(cell).terrainType,
                    Is.EqualTo(expectedTerrain),
                    $"单元 {cell} 的地形过渡带不正确。");
            }
        }
    }

    /// <summary>
    /// 验证道路不会被海岸带或高地过渡规则改写。
    /// </summary>
    [Test]
    public void PathCellsRemainUnchanged()
    {
        MapData mapData = CreateFilledMap(3, 3, MapTerrainType.DeepWater);
        Vector2Int pathCell = new Vector2Int(1, 1);
        mapData.SetTerrain(pathCell, MapTerrainType.Path);

        int changedCellCount = MapTerrainNestingEnforcer.Enforce(mapData);

        Assert.That(changedCellCount, Is.EqualTo(0));
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
