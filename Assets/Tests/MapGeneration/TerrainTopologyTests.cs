using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证分层地形使用正确的视觉族和四方向边界位。
/// </summary>
public class TerrainTopologyTests
{
    /// <summary>
    /// 验证草地覆盖层把沙地和道路视为边界，同时连接森林与山地。
    /// </summary>
    [Test]
    public void GrassOverlayConnectsGrassForestAndMountain()
    {
        MapData mapData = CreateFilledMap(MapTerrainType.Grass);
        Vector2Int centerCell = new Vector2Int(1, 1);
        mapData.SetTerrain(centerCell + Vector2Int.up, MapTerrainType.Sand);
        mapData.SetTerrain(centerCell + Vector2Int.right, MapTerrainType.Forest);
        mapData.SetTerrain(centerCell + Vector2Int.down, MapTerrainType.Path);
        mapData.SetTerrain(centerCell + Vector2Int.left, MapTerrainType.Mountain);

        TerrainBoundaryMask mask =
            TerrainTopology.GetGrassBoundaryMask(mapData, centerCell);

        Assert.That(
            mask,
            Is.EqualTo(TerrainBoundaryMask.North | TerrainBoundaryMask.South));
    }

    /// <summary>
    /// 验证沙地底层连接所有非水地形，只在水域方向显示边界。
    /// </summary>
    [Test]
    public void SandBaseOnlyBreaksAtWaterOrMapOutside()
    {
        MapData mapData = CreateFilledMap(MapTerrainType.Sand);
        Vector2Int centerCell = new Vector2Int(1, 1);
        mapData.SetTerrain(centerCell, MapTerrainType.Grass);
        mapData.SetTerrain(centerCell + Vector2Int.up, MapTerrainType.DeepWater);
        mapData.SetTerrain(centerCell + Vector2Int.right, MapTerrainType.ShallowWater);
        mapData.SetTerrain(centerCell + Vector2Int.down, MapTerrainType.Path);
        mapData.SetTerrain(centerCell + Vector2Int.left, MapTerrainType.Mountain);

        TerrainBoundaryMask mask =
            TerrainTopology.GetSandBoundaryMask(mapData, centerCell);

        Assert.That(
            mask,
            Is.EqualTo(TerrainBoundaryMask.North | TerrainBoundaryMask.East));
    }

    /// <summary>
    /// 验证高地层只与相邻山地连接。
    /// </summary>
    [Test]
    public void ElevationOnlyConnectsMountainCells()
    {
        MapData mapData = CreateFilledMap(MapTerrainType.Mountain);
        Vector2Int centerCell = new Vector2Int(1, 1);
        mapData.SetTerrain(centerCell + Vector2Int.up, MapTerrainType.Grass);
        mapData.SetTerrain(centerCell + Vector2Int.left, MapTerrainType.Sand);

        TerrainBoundaryMask mask =
            TerrainTopology.GetElevationBoundaryMask(mapData, centerCell);

        Assert.That(
            mask,
            Is.EqualTo(TerrainBoundaryMask.North | TerrainBoundaryMask.West));
    }

    /// <summary>
    /// 验证地图外侧在四方向拓扑中始终被视为边界。
    /// </summary>
    [Test]
    public void MapOutsideProducesBoundaryBits()
    {
        MapData mapData = new MapData(1, 1, Vector2Int.zero);

        TerrainBoundaryMask mask =
            TerrainTopology.GetGrassBoundaryMask(mapData, Vector2Int.zero);

        Assert.That(
            mask,
            Is.EqualTo(
                TerrainBoundaryMask.North |
                TerrainBoundaryMask.East |
                TerrainBoundaryMask.South |
                TerrainBoundaryMask.West));
    }

    /// <summary>
    /// 创建一张所有单元都使用同一种地形的三乘三测试地图。
    /// </summary>
    /// <param name="terrainType">填充地图使用的逻辑地形。</param>
    /// <returns>填充完成的测试地图。</returns>
    private static MapData CreateFilledMap(MapTerrainType terrainType)
    {
        MapData mapData = new MapData(3, 3, Vector2Int.zero);

        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
                mapData.SetTerrain(new Vector2Int(x, y), terrainType);
        }

        return mapData;
    }
}
