using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 验证小地图数据转换不会依赖 Unity UI（用户界面）对象。
/// </summary>
public class MapMinimapRasterizerTests
{
    /// <summary>
    /// 验证草地、水域和道路会被转换为对应颜色。
    /// </summary>
    [Test]
    public void BuildColorBufferMapsTerrainToExpectedColors()
    {
        MapData mapData = new MapData(3, 1, new Vector2Int(10, 20));
        mapData.SetTerrain(new Vector2Int(10, 20), MapTerrainType.Grass);
        mapData.SetTerrain(new Vector2Int(11, 20), MapTerrainType.Water);
        mapData.SetTerrain(new Vector2Int(12, 20), MapTerrainType.Path);

        Color32 grassColor = new Color32(1, 2, 3, 255);
        Color32 waterColor = new Color32(4, 5, 6, 255);
        Color32 pathColor = new Color32(7, 8, 9, 255);
        Color32 fallbackColor = new Color32(10, 11, 12, 255);

        Color32[] pixels = MapMinimapRasterizer.BuildColorBuffer(
            mapData,
            grassColor,
            waterColor,
            pathColor,
            fallbackColor);

        Assert.That(pixels, Has.Length.EqualTo(3));
        Assert.That(pixels[0], Is.EqualTo(grassColor));
        Assert.That(pixels[1], Is.EqualTo(waterColor));
        Assert.That(pixels[2], Is.EqualTo(pathColor));
    }

    /// <summary>
    /// 验证新增地形会在小地图中使用独立颜色，而不是落入备用颜色。
    /// </summary>
    [Test]
    public void BuildColorBufferMapsExtendedTerrainToExpectedColors()
    {
        MapData mapData = new MapData(3, 1, Vector2Int.zero);
        mapData.SetTerrain(new Vector2Int(0, 0), MapTerrainType.ShallowWater);
        mapData.SetTerrain(new Vector2Int(1, 0), MapTerrainType.Forest);
        mapData.SetTerrain(new Vector2Int(2, 0), MapTerrainType.Mountain);

        Color32 grassColor = new Color32(1, 2, 3, 255);
        Color32 waterColor = new Color32(4, 5, 6, 255);
        Color32 pathColor = new Color32(7, 8, 9, 255);
        Color32 shallowWaterColor = new Color32(10, 11, 12, 255);
        Color32 forestColor = new Color32(13, 14, 15, 255);
        Color32 mountainColor = new Color32(16, 17, 18, 255);
        Color32 fallbackColor = new Color32(19, 20, 21, 255);

        Color32[] pixels = MapMinimapRasterizer.BuildColorBuffer(
            mapData,
            grassColor,
            waterColor,
            pathColor,
            shallowWaterColor,
            forestColor,
            mountainColor,
            fallbackColor);

        Assert.That(pixels[0], Is.EqualTo(shallowWaterColor));
        Assert.That(pixels[1], Is.EqualTo(forestColor));
        Assert.That(pixels[2], Is.EqualTo(mountainColor));
    }

    /// <summary>
    /// 验证不同地图原点下的网格中心能映射到正确归一化位置。
    /// </summary>
    [Test]
    public void CellToNormalizedPositionUsesCellCentersAndOrigin()
    {
        MapData mapData = new MapData(4, 2, new Vector2Int(10, 20));

        Vector2 bottomLeft = MapMinimapRasterizer.CellToNormalizedPosition(
            mapData,
            new Vector2Int(10, 20));
        Vector2 topRight = MapMinimapRasterizer.CellToNormalizedPosition(
            mapData,
            new Vector2Int(13, 21));

        Assert.That(bottomLeft.x, Is.EqualTo(0.125f).Within(0.001f));
        Assert.That(bottomLeft.y, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(topRight.x, Is.EqualTo(0.875f).Within(0.001f));
        Assert.That(topRight.y, Is.EqualTo(0.75f).Within(0.001f));
    }

    /// <summary>
    /// 验证越界网格会被限制到小地图的边缘范围。
    /// </summary>
    [Test]
    public void CellToNormalizedPositionClampsOutsideMap()
    {
        MapData mapData = new MapData(4, 2, Vector2Int.zero);

        Vector2 normalizedPosition = MapMinimapRasterizer.CellToNormalizedPosition(
            mapData,
            new Vector2Int(100, -100));

        Assert.That(normalizedPosition.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(normalizedPosition.y, Is.EqualTo(0f).Within(0.001f));
    }
}
