using System;
using UnityEngine;

/// <summary>
/// 修正逻辑地图中的单格瓶颈，保证玩家碰撞体可以通过所有横向和竖向通路。
/// </summary>
public static class MapTerrainPassageWidthEnforcer
{
    /// <summary>
    /// 让地图中的轴向可行走通路达到指定的最小宽度。
    /// </summary>
    /// <param name="mapData">已经完成道路和可达性处理的地图数据。</param>
    /// <param name="minimumPassageWidth">要求的最小通路宽度。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <returns>本次扩宽的地图单元数量。</returns>
    public static int Enforce(
        MapData mapData,
        int minimumPassageWidth,
        int excludedBorderSize)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (minimumPassageWidth < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPassageWidth),
                "最小通路宽度必须至少为 2。");
        }

        if (excludedBorderSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(excludedBorderSize),
                "地图边界保护带宽度必须至少为 1。");
        }

        int changedCellCount = 0;
        bool changedInPass;
        int passCount = 0;
        int maximumPassCount = mapData.Width * mapData.Height;

        // 扩宽会把不可行走单元变为可行走单元，因此重复扫描直到拓扑稳定。
        do
        {
            changedInPass = false;
            passCount++;

            for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
            {
                for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!mapData.IsWalkable(cell) ||
                        mapData.IsBorder(cell, excludedBorderSize))
                        continue;

                    changedCellCount += EnsureAxisWidth(
                        mapData,
                        cell,
                        Vector2Int.up,
                        Vector2Int.left,
                        Vector2Int.right,
                        minimumPassageWidth,
                        excludedBorderSize,
                        ref changedInPass);
                    changedCellCount += EnsureAxisWidth(
                        mapData,
                        cell,
                        Vector2Int.right,
                        Vector2Int.down,
                        Vector2Int.up,
                        minimumPassageWidth,
                        excludedBorderSize,
                        ref changedInPass);
                }
            }

            if (passCount > maximumPassCount && changedInPass)
            {
                throw new InvalidOperationException(
                    "最小通路宽度约束未能在有限扫描次数内稳定。");
            }
        }
        while (changedInPass);

        return changedCellCount;
    }

    /// <summary>
    /// 检查某个轴向是否形成通路，并扩展其垂直截面宽度。
    /// </summary>
    /// <param name="mapData">待修正的地图数据。</param>
    /// <param name="cell">当前检查的中心单元。</param>
    /// <param name="axisDirection">通路延伸方向。</param>
    /// <param name="negativeWidthDirection">截面负方向。</param>
    /// <param name="positiveWidthDirection">截面正方向。</param>
    /// <param name="minimumPassageWidth">要求的最小通路宽度。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <param name="changedInPass">本次扫描是否发生过修改。</param>
    /// <returns>本次检查新增的可行走单元数量。</returns>
    private static int EnsureAxisWidth(
        MapData mapData,
        Vector2Int cell,
        Vector2Int axisDirection,
        Vector2Int negativeWidthDirection,
        Vector2Int positiveWidthDirection,
        int minimumPassageWidth,
        int excludedBorderSize,
        ref bool changedInPass)
    {
        if (!mapData.IsInside(cell + axisDirection) ||
            !mapData.IsInside(cell - axisDirection) ||
            !mapData.IsWalkable(cell + axisDirection) ||
            !mapData.IsWalkable(cell - axisDirection))
        {
            return 0;
        }

        int currentWidth = MeasureCrossSectionWidth(
            mapData,
            cell,
            negativeWidthDirection,
            positiveWidthDirection);
        int changedCellCount = 0;

        while (currentWidth < minimumPassageWidth)
        {
            if (!TryChooseExpansionCell(
                    mapData,
                    cell,
                    negativeWidthDirection,
                    positiveWidthDirection,
                    excludedBorderSize,
                    out Vector2Int expansionCell))
            {
                break;
            }

            MapTerrainType passageTerrain =
                GetPassageTerrain(mapData.GetCell(cell).terrainType);
            mapData.SetTerrain(expansionCell, passageTerrain);
            currentWidth++;
            changedCellCount++;
            changedInPass = true;
        }

        return changedCellCount;
    }

    /// <summary>
    /// 测量中心单元所在的连续可行走截面宽度。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">截面中心单元。</param>
    /// <param name="negativeDirection">截面负方向。</param>
    /// <param name="positiveDirection">截面正方向。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <returns>连续可行走单元的数量。</returns>
    private static int MeasureCrossSectionWidth(
        MapData mapData,
        Vector2Int cell,
        Vector2Int negativeDirection,
        Vector2Int positiveDirection)
    {
        int width = 1;

        Vector2Int nextCell = cell + negativeDirection;
        while (mapData.IsInside(nextCell) && mapData.IsWalkable(nextCell))
        {
            width++;
            nextCell += negativeDirection;
        }

        nextCell = cell + positiveDirection;
        while (mapData.IsInside(nextCell) && mapData.IsWalkable(nextCell))
        {
            width++;
            nextCell += positiveDirection;
        }

        return width;
    }

    /// <summary>
    /// 在截面两侧选择一个可以安全扩宽的非边界单元。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">截面中心单元。</param>
    /// <param name="negativeDirection">截面负方向。</param>
    /// <param name="positiveDirection">截面正方向。</param>
    /// <param name="expansionCell">选出的扩宽单元。</param>
    /// <returns>找到可扩宽单元时返回 true。</returns>
    private static bool TryChooseExpansionCell(
        MapData mapData,
        Vector2Int cell,
        Vector2Int negativeDirection,
        Vector2Int positiveDirection,
        int excludedBorderSize,
        out Vector2Int expansionCell)
    {
        Vector2Int negativeCell = cell + negativeDirection;
        Vector2Int positiveCell = cell + positiveDirection;
        bool canUseNegative = IsExpandableCell(
            mapData,
            negativeCell,
            excludedBorderSize);
        bool canUsePositive = IsExpandableCell(
            mapData,
            positiveCell,
            excludedBorderSize);

        if (!canUseNegative && !canUsePositive)
        {
            expansionCell = default;
            return false;
        }

        if (canUseNegative && canUsePositive)
        {
            // 优先填水，尽量保留高地；同优先级固定选择负方向以保证确定性。
            int negativePriority = GetExpansionPriority(mapData.GetCell(negativeCell).terrainType);
            int positivePriority = GetExpansionPriority(mapData.GetCell(positiveCell).terrainType);
            expansionCell = negativePriority <= positivePriority
                ? negativeCell
                : positiveCell;
            return true;
        }

        expansionCell = canUseNegative ? negativeCell : positiveCell;
        return true;
    }

    /// <summary>
    /// 判断单元是否可以被扩宽为可行走地形。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">候选扩宽单元。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <returns>候选单元位于内部且当前不可行走时返回 true。</returns>
    private static bool IsExpandableCell(
        MapData mapData,
        Vector2Int cell,
        int excludedBorderSize)
    {
        return mapData.IsInside(cell) &&
               !mapData.IsBorder(cell, excludedBorderSize) &&
               !mapData.IsWalkable(cell);
    }

    /// <summary>
    /// 计算扩宽候选的优先级，数值越小越优先被填充。
    /// </summary>
    /// <param name="terrainType">候选单元原有地形。</param>
    /// <returns>候选优先级。</returns>
    private static int GetExpansionPriority(MapTerrainType terrainType)
    {
        return TerrainTopology.IsWater(terrainType) ? 0 : 1;
    }

    /// <summary>
    /// 获取扩宽时应复制到新单元的可行走地形类型。
    /// </summary>
    /// <param name="terrainType">通路中心单元的地形。</param>
    /// <returns>用于扩宽的地形类型。</returns>
    private static MapTerrainType GetPassageTerrain(MapTerrainType terrainType)
    {
        if (terrainType == MapTerrainType.Path ||
            terrainType == MapTerrainType.Sand ||
            terrainType == MapTerrainType.Forest ||
            terrainType == MapTerrainType.Grass)
        {
            return terrainType;
        }

        throw new InvalidOperationException(
            $"不可行走地形 {terrainType} 不能作为通路扩宽的来源。");
    }
}
