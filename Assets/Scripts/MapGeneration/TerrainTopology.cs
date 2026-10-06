using System;
using UnityEngine;

/// <summary>
/// 根据 MapData（地图数据）推导分层地形的连接关系和边界掩码。
/// </summary>
public static class TerrainTopology
{
    /// <summary>
    /// 计算 Sand Base（沙地底层）在指定单元需要显示的边界。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">待查询的地图网格坐标。</param>
    /// <returns>沙地底层的四方向边界掩码。</returns>
    public static TerrainBoundaryMask GetSandBoundaryMask(MapData mapData, Vector2Int cell)
    {
        return CalculateBoundaryMask(mapData, cell, UsesSandBase);
    }

    /// <summary>
    /// 计算 Grass Overlay（草地覆盖层）在指定单元需要显示的边界。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">待查询的地图网格坐标。</param>
    /// <returns>草地覆盖层的四方向边界掩码。</returns>
    public static TerrainBoundaryMask GetGrassBoundaryMask(MapData mapData, Vector2Int cell)
    {
        return CalculateBoundaryMask(mapData, cell, UsesGrassOverlay);
    }

    /// <summary>
    /// 计算 Elevation（高地层）在指定单元需要显示的边界。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">待查询的地图网格坐标。</param>
    /// <returns>岩石高地的四方向边界掩码。</returns>
    public static TerrainBoundaryMask GetElevationBoundaryMask(MapData mapData, Vector2Int cell)
    {
        return CalculateBoundaryMask(mapData, cell, UsesElevation);
    }

    /// <summary>
    /// 判断逻辑地形是否属于水域。
    /// </summary>
    /// <param name="terrainType">待检查的逻辑地形。</param>
    /// <returns>深水或浅水返回 true。</returns>
    public static bool IsWater(MapTerrainType terrainType)
    {
        return terrainType == MapTerrainType.DeepWater ||
               terrainType == MapTerrainType.ShallowWater;
    }

    /// <summary>
    /// 判断逻辑地形是否需要绘制 Sand Base（沙地底层）。
    /// </summary>
    /// <param name="terrainType">待检查的逻辑地形。</param>
    /// <returns>所有非水地形返回 true。</returns>
    public static bool UsesSandBase(MapTerrainType terrainType)
    {
        return !IsWater(terrainType);
    }

    /// <summary>
    /// 判断逻辑地形是否需要绘制 Grass Overlay（草地覆盖层）。
    /// </summary>
    /// <param name="terrainType">待检查的逻辑地形。</param>
    /// <returns>草地、森林和山地返回 true。</returns>
    public static bool UsesGrassOverlay(MapTerrainType terrainType)
    {
        return terrainType == MapTerrainType.Grass ||
               terrainType == MapTerrainType.Forest ||
               terrainType == MapTerrainType.Mountain;
    }

    /// <summary>
    /// 判断逻辑地形是否需要绘制 Elevation（高地层）。
    /// </summary>
    /// <param name="terrainType">待检查的逻辑地形。</param>
    /// <returns>山地返回 true。</returns>
    public static bool UsesElevation(MapTerrainType terrainType)
    {
        return terrainType == MapTerrainType.Mountain;
    }

    /// <summary>
    /// 按北、东、南、西顺序检查相邻单元，不属于同一视觉族时添加对应边界位。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">待查询的地图网格坐标。</param>
    /// <param name="connectsToLayer">判断一个逻辑地形是否连接当前视觉层的函数。</param>
    /// <returns>计算完成的四方向边界掩码。</returns>
    private static TerrainBoundaryMask CalculateBoundaryMask(
        MapData mapData,
        Vector2Int cell,
        Func<MapTerrainType, bool> connectsToLayer)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (connectsToLayer == null)
            throw new ArgumentNullException(nameof(connectsToLayer));

        if (!mapData.IsInside(cell))
            throw new ArgumentOutOfRangeException(nameof(cell), "只能计算地图范围内单元的边界掩码。");

        TerrainBoundaryMask mask = TerrainBoundaryMask.None;
        AddBoundaryIfDisconnected(
            mapData,
            cell + Vector2Int.up,
            TerrainBoundaryMask.North,
            connectsToLayer,
            ref mask);
        AddBoundaryIfDisconnected(
            mapData,
            cell + Vector2Int.right,
            TerrainBoundaryMask.East,
            connectsToLayer,
            ref mask);
        AddBoundaryIfDisconnected(
            mapData,
            cell + Vector2Int.down,
            TerrainBoundaryMask.South,
            connectsToLayer,
            ref mask);
        AddBoundaryIfDisconnected(
            mapData,
            cell + Vector2Int.left,
            TerrainBoundaryMask.West,
            connectsToLayer,
            ref mask);
        return mask;
    }

    /// <summary>
    /// 检查一个邻居，并在地图外或视觉族不连接时追加指定边界位。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="neighborCell">相邻网格坐标。</param>
    /// <param name="boundary">邻居断开时需要添加的边界位。</param>
    /// <param name="connectsToLayer">视觉族连接判断函数。</param>
    /// <param name="mask">正在构建的边界掩码。</param>
    private static void AddBoundaryIfDisconnected(
        MapData mapData,
        Vector2Int neighborCell,
        TerrainBoundaryMask boundary,
        Func<MapTerrainType, bool> connectsToLayer,
        ref TerrainBoundaryMask mask)
    {
        if (!mapData.IsInside(neighborCell) ||
            !connectsToLayer(mapData.GetCell(neighborCell).terrainType))
        {
            mask |= boundary;
        }
    }
}
