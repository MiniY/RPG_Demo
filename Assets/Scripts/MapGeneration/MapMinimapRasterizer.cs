using UnityEngine;

/// <summary>
/// 把 MapData（地图数据）转换为小地图使用的颜色像素。
/// </summary>
public static class MapMinimapRasterizer
{
    /// <summary>
    /// 根据包含沙地的完整地形颜色表生成从左下角开始排列的颜色缓冲区。
    /// </summary>
    /// <param name="mapData">待转换的地图数据。</param>
    /// <param name="grassColor">草地颜色。</param>
    /// <param name="waterColor">深水颜色。</param>
    /// <param name="sandColor">自然沙地颜色。</param>
    /// <param name="pathColor">道路颜色。</param>
    /// <param name="shallowWaterColor">浅水颜色。</param>
    /// <param name="forestColor">森林颜色。</param>
    /// <param name="mountainColor">山地颜色。</param>
    /// <param name="fallbackColor">未识别地形的备用颜色。</param>
    /// <returns>可以直接交给 Texture2D（二维纹理）的颜色数组。</returns>
    public static Color32[] BuildColorBuffer(
        MapData mapData,
        Color32 grassColor,
        Color32 waterColor,
        Color32 sandColor,
        Color32 pathColor,
        Color32 shallowWaterColor,
        Color32 forestColor,
        Color32 mountainColor,
        Color32 fallbackColor)
    {
        return BuildColorBufferInternal(
            mapData,
            grassColor,
            waterColor,
            sandColor,
            pathColor,
            shallowWaterColor,
            forestColor,
            mountainColor,
            fallbackColor);
    }

    /// <summary>
    /// 根据完整地形颜色表生成从左下角开始排列的颜色缓冲区。
    /// </summary>
    /// <param name="mapData">待转换的地图数据。</param>
    /// <param name="grassColor">草地颜色。</param>
    /// <param name="waterColor">深水颜色。</param>
    /// <param name="pathColor">道路颜色。</param>
    /// <param name="shallowWaterColor">浅水颜色。</param>
    /// <param name="forestColor">森林颜色。</param>
    /// <param name="mountainColor">山地颜色。</param>
    /// <param name="fallbackColor">未识别地形的备用颜色。</param>
    /// <returns>可以直接交给 Texture2D（二维纹理）的颜色数组。</returns>
    public static Color32[] BuildColorBuffer(
        MapData mapData,
        Color32 grassColor,
        Color32 waterColor,
        Color32 pathColor,
        Color32 shallowWaterColor,
        Color32 forestColor,
        Color32 mountainColor,
        Color32 fallbackColor)
    {
        return BuildColorBufferInternal(
            mapData,
            grassColor,
            waterColor,
            pathColor,
            pathColor,
            shallowWaterColor,
            forestColor,
            mountainColor,
            fallbackColor);
    }

    /// <summary>
    /// 保留旧版颜色参数重载，避免已有测试或外部调用立即失效。
    /// </summary>
    /// <param name="mapData">待转换的地图数据。</param>
    /// <param name="grassColor">草地颜色。</param>
    /// <param name="waterColor">深水颜色。</param>
    /// <param name="pathColor">道路颜色。</param>
    /// <param name="fallbackColor">备用颜色。</param>
    /// <returns>可以直接交给 Texture2D（二维纹理）的颜色数组。</returns>
    public static Color32[] BuildColorBuffer(
        MapData mapData,
        Color32 grassColor,
        Color32 waterColor,
        Color32 pathColor,
        Color32 fallbackColor)
    {
        return BuildColorBufferInternal(
            mapData,
            grassColor,
            waterColor,
            pathColor,
            pathColor,
            waterColor,
            grassColor,
            fallbackColor,
            fallbackColor);
    }

    /// <summary>
    /// 使用完整的地形颜色表构建颜色缓冲区的内部实现。
    /// </summary>
    /// <param name="mapData">待转换的地图数据。</param>
    /// <param name="grassColor">草地颜色。</param>
    /// <param name="waterColor">深水颜色。</param>
    /// <param name="sandColor">自然沙地颜色。</param>
    /// <param name="pathColor">道路颜色。</param>
    /// <param name="shallowWaterColor">浅水颜色。</param>
    /// <param name="forestColor">森林颜色。</param>
    /// <param name="mountainColor">山地颜色。</param>
    /// <param name="fallbackColor">备用颜色。</param>
    /// <returns>可以直接交给 Texture2D（二维纹理）的颜色数组。</returns>
    private static Color32[] BuildColorBufferInternal(
        MapData mapData,
        Color32 grassColor,
        Color32 waterColor,
        Color32 sandColor,
        Color32 pathColor,
        Color32 shallowWaterColor,
        Color32 forestColor,
        Color32 mountainColor,
        Color32 fallbackColor)
    {
        if (mapData == null)
            throw new System.ArgumentNullException(nameof(mapData));

        Color32[] pixels = new Color32[mapData.Width * mapData.Height];

        for (int localY = 0; localY < mapData.Height; localY++)
        {
            for (int localX = 0; localX < mapData.Width; localX++)
            {
                Vector2Int cell = mapData.Origin + new Vector2Int(localX, localY);
                int pixelIndex = localY * mapData.Width + localX;
                pixels[pixelIndex] = GetTerrainColor(
                    mapData.GetCell(cell).terrainType,
                    grassColor,
                    waterColor,
                    sandColor,
                    pathColor,
                    shallowWaterColor,
                    forestColor,
                    mountainColor,
                    fallbackColor);
            }
        }

        return pixels;
    }

    /// <summary>
    /// 把地图网格坐标转换为小地图中的归一化位置。
    /// </summary>
    /// <param name="mapData">包含地图尺寸和原点的地图数据。</param>
    /// <param name="cell">待转换的网格坐标。</param>
    /// <returns>左下角为零、右上角为一的归一化坐标。</returns>
    public static Vector2 CellToNormalizedPosition(MapData mapData, Vector2Int cell)
    {
        if (mapData == null)
            throw new System.ArgumentNullException(nameof(mapData));

        float normalizedX = (cell.x - mapData.Origin.x + 0.5f) / mapData.Width;
        float normalizedY = (cell.y - mapData.Origin.y + 0.5f) / mapData.Height;
        return new Vector2(Mathf.Clamp01(normalizedX), Mathf.Clamp01(normalizedY));
    }

    /// <summary>
    /// 根据地形类型选择对应颜色。
    /// </summary>
    /// <param name="terrainType">地形类型。</param>
    /// <param name="grassColor">草地颜色。</param>
    /// <param name="waterColor">深水颜色。</param>
    /// <param name="sandColor">自然沙地颜色。</param>
    /// <param name="pathColor">道路颜色。</param>
    /// <param name="shallowWaterColor">浅水颜色。</param>
    /// <param name="forestColor">森林颜色。</param>
    /// <param name="mountainColor">山地颜色。</param>
    /// <param name="fallbackColor">备用颜色。</param>
    /// <returns>对应的像素颜色。</returns>
    private static Color32 GetTerrainColor(
        MapTerrainType terrainType,
        Color32 grassColor,
        Color32 waterColor,
        Color32 sandColor,
        Color32 pathColor,
        Color32 shallowWaterColor,
        Color32 forestColor,
        Color32 mountainColor,
        Color32 fallbackColor)
    {
        switch (terrainType)
        {
            case MapTerrainType.DeepWater:
                return waterColor;
            case MapTerrainType.ShallowWater:
                return shallowWaterColor;
            case MapTerrainType.Path:
                return pathColor;
            case MapTerrainType.Sand:
                return sandColor;
            case MapTerrainType.Forest:
                return forestColor;
            case MapTerrainType.Mountain:
                return mountainColor;
            case MapTerrainType.Grass:
                return grassColor;
            default:
                return fallbackColor;
        }
    }
}
