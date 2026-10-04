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

            changedCellCount += EnforceWalkableEdgeClearance(
                mapData,
                excludedBorderSize,
                ref changedInPass);

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
    /// 检查每条横向和竖向可行走连接，保证连接至少属于一个完整的 2×2 净空块。
    /// </summary>
    /// <param name="mapData">待修正的地图数据。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <param name="changedInPass">本次扫描是否发生过修改。</param>
    /// <returns>本次扫描新增的可行走单元数量。</returns>
    private static int EnforceWalkableEdgeClearance(
        MapData mapData,
        int excludedBorderSize,
        ref bool changedInPass)
    {
        int changedCellCount = 0;

        for (int x = mapData.Origin.x; x < mapData.Origin.x + mapData.Width; x++)
        {
            for (int y = mapData.Origin.y; y < mapData.Origin.y + mapData.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!mapData.IsWalkable(cell))
                    continue;

                Vector2Int rightCell = cell + Vector2Int.right;
                if (mapData.IsInside(rightCell) && mapData.IsWalkable(rightCell))
                {
                    changedCellCount += EnsureEdgeClearance(
                        mapData,
                        cell,
                        rightCell,
                        Vector2Int.down,
                        Vector2Int.up,
                        excludedBorderSize,
                        ref changedInPass);
                }

                Vector2Int upperCell = cell + Vector2Int.up;
                if (mapData.IsInside(upperCell) && mapData.IsWalkable(upperCell))
                {
                    changedCellCount += EnsureEdgeClearance(
                        mapData,
                        cell,
                        upperCell,
                        Vector2Int.left,
                        Vector2Int.right,
                        excludedBorderSize,
                        ref changedInPass);
                }
            }
        }

        return changedCellCount;
    }

    /// <summary>
    /// 为一条可行走连接选择一侧，并补齐这一侧缺失的 2×2 净空单元。
    /// </summary>
    /// <param name="mapData">待修正的地图数据。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <param name="negativeSide">连接截面的负方向。</param>
    /// <param name="positiveSide">连接截面的正方向。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <param name="changedInPass">本次扫描是否发生过修改。</param>
    /// <returns>本次补齐的可行走单元数量。</returns>
    private static int EnsureEdgeClearance(
        MapData mapData,
        Vector2Int firstCell,
        Vector2Int secondCell,
        Vector2Int negativeSide,
        Vector2Int positiveSide,
        int excludedBorderSize,
        ref bool changedInPass)
    {
        if (HasCompleteClearanceSide(
                mapData,
                firstCell,
                secondCell,
                negativeSide) ||
            HasCompleteClearanceSide(
                mapData,
                firstCell,
                secondCell,
                positiveSide))
        {
            return 0;
        }

        if (!TryChooseClearanceSide(
                mapData,
                firstCell,
                secondCell,
                negativeSide,
                positiveSide,
                excludedBorderSize,
                out Vector2Int chosenSide))
        {
            return 0;
        }

        Vector2Int firstExpansionCell = firstCell + chosenSide;
        Vector2Int secondExpansionCell = secondCell + chosenSide;
        MapTerrainType passageTerrain = GetPassageTerrain(
            mapData.GetCell(firstCell).terrainType,
            mapData.GetCell(secondCell).terrainType);
        int changedCellCount = 0;

        if (!mapData.IsWalkable(firstExpansionCell))
        {
            mapData.SetTerrain(firstExpansionCell, passageTerrain);
            changedCellCount++;
        }

        if (!mapData.IsWalkable(secondExpansionCell))
        {
            mapData.SetTerrain(secondExpansionCell, passageTerrain);
            changedCellCount++;
        }

        if (changedCellCount > 0)
            changedInPass = true;

        return changedCellCount;
    }

    /// <summary>
    /// 判断一条连接在指定侧是否已经形成完整的 2×2 净空块。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <param name="side">需要检查的连接截面方向。</param>
    /// <returns>该侧两个截面单元都可行走时返回 true。</returns>
    private static bool HasCompleteClearanceSide(
        MapData mapData,
        Vector2Int firstCell,
        Vector2Int secondCell,
        Vector2Int side)
    {
        return mapData.IsInside(firstCell + side) &&
               mapData.IsInside(secondCell + side) &&
               mapData.IsWalkable(firstCell + side) &&
               mapData.IsWalkable(secondCell + side);
    }

    /// <summary>
    /// 在连接两侧选择可补齐且代价较低的一侧。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <param name="negativeSide">连接截面的负方向。</param>
    /// <param name="positiveSide">连接截面的正方向。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <param name="chosenSide">选出的连接截面方向。</param>
    /// <returns>存在可补齐的一侧时返回 true。</returns>
    private static bool TryChooseClearanceSide(
        MapData mapData,
        Vector2Int firstCell,
        Vector2Int secondCell,
        Vector2Int negativeSide,
        Vector2Int positiveSide,
        int excludedBorderSize,
        out Vector2Int chosenSide)
    {
        bool canUseNegative = TryEvaluateClearanceSide(
            mapData,
            firstCell,
            secondCell,
            negativeSide,
            excludedBorderSize,
            out int negativeMissingCount,
            out int negativePriority);
        bool canUsePositive = TryEvaluateClearanceSide(
            mapData,
            firstCell,
            secondCell,
            positiveSide,
            excludedBorderSize,
            out int positiveMissingCount,
            out int positivePriority);

        if (!canUseNegative && !canUsePositive)
        {
            chosenSide = default;
            return false;
        }

        if (canUseNegative && !canUsePositive)
        {
            chosenSide = negativeSide;
            return true;
        }

        if (!canUseNegative)
        {
            chosenSide = positiveSide;
            return true;
        }

        bool chooseNegative =
            negativeMissingCount < positiveMissingCount ||
            (negativeMissingCount == positiveMissingCount &&
             negativePriority <= positivePriority);
        chosenSide = chooseNegative ? negativeSide : positiveSide;
        return true;
    }

    /// <summary>
    /// 评估一侧的两个单元是否可以被补齐，并计算修改代价。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="firstCell">连接的第一个单元。</param>
    /// <param name="secondCell">连接的第二个单元。</param>
    /// <param name="side">连接截面方向。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <param name="missingCount">需要新增的可行走单元数量。</param>
    /// <param name="priority">候选地形的修改优先级。</param>
    /// <returns>该侧可在不侵入边界的前提下补齐时返回 true。</returns>
    private static bool TryEvaluateClearanceSide(
        MapData mapData,
        Vector2Int firstCell,
        Vector2Int secondCell,
        Vector2Int side,
        int excludedBorderSize,
        out int missingCount,
        out int priority)
    {
        Vector2Int firstCandidate = firstCell + side;
        Vector2Int secondCandidate = secondCell + side;
        bool firstUsable = IsWalkableClearanceCell(mapData, firstCandidate, excludedBorderSize);
        bool secondUsable = IsWalkableClearanceCell(mapData, secondCandidate, excludedBorderSize);
        missingCount = 0;
        priority = 0;

        if (!firstUsable || !secondUsable)
            return false;

        if (!mapData.IsWalkable(firstCandidate))
        {
            missingCount++;
            priority += GetExpansionPriority(mapData.GetCell(firstCandidate).terrainType);
        }

        if (!mapData.IsWalkable(secondCandidate))
        {
            missingCount++;
            priority += GetExpansionPriority(mapData.GetCell(secondCandidate).terrainType);
        }

        return true;
    }

    /// <summary>
    /// 判断单元是否可作为 2×2 净空块的一部分。
    /// </summary>
    /// <param name="mapData">待查询的地图数据。</param>
    /// <param name="cell">候选单元。</param>
    /// <param name="excludedBorderSize">不得侵入的地图边界保护带宽度。</param>
    /// <returns>单元在内部、非边界且可行走或可扩宽时返回 true。</returns>
    private static bool IsWalkableClearanceCell(
        MapData mapData,
        Vector2Int cell,
        int excludedBorderSize)
    {
        return mapData.IsInside(cell) &&
               !mapData.IsBorder(cell, excludedBorderSize) &&
               (mapData.IsWalkable(cell) ||
                IsExpandableCell(mapData, cell, excludedBorderSize));
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
        if (TerrainTopology.IsWater(terrainType))
            return 0;

        return terrainType == MapTerrainType.Mountain ? 2 : 1;
    }

    /// <summary>
    /// 根据连接两端的地形选择扩宽后使用的可行走地形。
    /// </summary>
    /// <param name="firstTerrain">连接第一个单元的地形。</param>
    /// <param name="secondTerrain">连接第二个单元的地形。</param>
    /// <returns>扩宽单元使用的地形类型。</returns>
    private static MapTerrainType GetPassageTerrain(
        MapTerrainType firstTerrain,
        MapTerrainType secondTerrain)
    {
        if (firstTerrain == MapTerrainType.Path ||
            secondTerrain == MapTerrainType.Path)
        {
            return MapTerrainType.Path;
        }

        if (IsPassageTerrain(firstTerrain))
            return firstTerrain;

        return GetPassageTerrain(secondTerrain);
    }

    /// <summary>
    /// 判断地形是否可以作为通路扩宽来源。
    /// </summary>
    /// <param name="terrainType">待检查的地形类型。</param>
    /// <returns>可行走地形返回 true。</returns>
    private static bool IsPassageTerrain(MapTerrainType terrainType)
    {
        return terrainType == MapTerrainType.Path ||
               terrainType == MapTerrainType.Sand ||
               terrainType == MapTerrainType.Forest ||
               terrainType == MapTerrainType.Grass;
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
