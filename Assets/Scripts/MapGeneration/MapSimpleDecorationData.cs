using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 表示一个已经通过规则验证的简单装饰物放置结果。
/// </summary>
public readonly struct MapSimpleDecorationPlacement
{
    /// <summary>
    /// 装饰物固定样式的锚点网格坐标。
    /// </summary>
    public Vector2Int AnchorCell { get; }

    /// <summary>
    /// 装饰物所属的简单装饰类别。
    /// </summary>
    public MapSimpleDecorationType DecorationType { get; }

    /// <summary>
    /// 对应调色板中固定样式的稳定编号。
    /// </summary>
    public string VariantId { get; }

    /// <summary>
    /// 创建一个简单装饰物放置结果。
    /// </summary>
    /// <param name="anchorCell">固定样式的锚点网格坐标。</param>
    /// <param name="decorationType">简单装饰类别。</param>
    /// <param name="variantId">调色板中的稳定变体编号。</param>
    public MapSimpleDecorationPlacement(
        Vector2Int anchorCell,
        MapSimpleDecorationType decorationType,
        string variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId))
            throw new ArgumentException("简单装饰物变体编号不能为空。", nameof(variantId));

        AnchorCell = anchorCell;
        DecorationType = decorationType;
        VariantId = variantId;
    }
}

/// <summary>
/// 保存一次地图生成得到的全部简单装饰物与占地查询数据。
/// </summary>
public sealed class MapSimpleDecorationData
{
    /// <summary>
    /// 按确定性生成顺序保存的简单装饰物放置结果。
    /// </summary>
    private readonly List<MapSimpleDecorationPlacement> placements =
        new List<MapSimpleDecorationPlacement>();

    /// <summary>
    /// 保存所有装饰物占用的网格单元，供重叠检查和后续复杂景观避让。
    /// </summary>
    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    /// <summary>
    /// 只读访问简单装饰物放置结果。
    /// </summary>
    public IReadOnlyList<MapSimpleDecorationPlacement> Placements => placements;

    /// <summary>
    /// 只读访问全部占用单元。
    /// </summary>
    public IReadOnlyCollection<Vector2Int> OccupiedCells => occupiedCells;

    /// <summary>
    /// 获取简单装饰物总数量。
    /// </summary>
    public int Count => placements.Count;

    /// <summary>
    /// 统计指定类别的简单装饰物数量。
    /// </summary>
    /// <param name="decorationType">需要统计的简单装饰物类别。</param>
    /// <returns>指定类别的放置数量。</returns>
    public int CountByType(MapSimpleDecorationType decorationType)
    {
        int count = 0;

        foreach (MapSimpleDecorationPlacement placement in placements)
        {
            if (placement.DecorationType == decorationType)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 判断指定网格单元是否已被任意简单装饰物占用。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <returns>已经占用时返回 true。</returns>
    public bool IsOccupied(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    /// <summary>
    /// 追加一个放置结果并登记它的完整占地矩形。
    /// </summary>
    /// <param name="placement">需要追加的简单装饰物放置结果。</param>
    /// <param name="variant">放置结果使用的固定样式定义。</param>
    internal void Add(
        MapSimpleDecorationPlacement placement,
        MapSimpleDecorationVariant variant)
    {
        if (variant == null)
            throw new ArgumentNullException(nameof(variant));

        placements.Add(placement);

        for (int offsetX = variant.FootprintMinimum.x;
             offsetX <= variant.FootprintMaximum.x;
             offsetX++)
        {
            for (int offsetY = variant.FootprintMinimum.y;
                 offsetY <= variant.FootprintMaximum.y;
                 offsetY++)
            {
                occupiedCells.Add(
                    placement.AnchorCell + new Vector2Int(offsetX, offsetY));
            }
        }
    }
}
