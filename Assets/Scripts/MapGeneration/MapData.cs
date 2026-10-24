using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapStairVisualComposition
{
    Auto,
    UpperOnly,
    UpperAndLower
}

public enum MapStairVisualSide
{
    Both,
    LeftOnly,
    RightOnly
}

public enum MapStairVisualPlacement
{
    Legacy,
    Outboard,
    BoundaryShoulders,
    WestSideAttached
}

public readonly struct MapStairConnection
{
    public MapStairConnection(Vector2Int lowerCell, Vector2Int upperCell,
        MapStairVisualComposition visualComposition = MapStairVisualComposition.Auto,
        MapStairVisualSide visualSide = MapStairVisualSide.Both,
        MapStairVisualPlacement visualPlacement = MapStairVisualPlacement.Legacy)
    {
        LowerCell = lowerCell;
        UpperCell = upperCell;
        VisualComposition = visualComposition;
        VisualSide = visualSide;
        VisualPlacement = visualPlacement;
    }

    public Vector2Int LowerCell { get; }
    public Vector2Int UpperCell { get; }
    public MapStairVisualComposition VisualComposition { get; }
    public MapStairVisualSide VisualSide { get; }
    public MapStairVisualPlacement VisualPlacement { get; }
}

/// <summary>
/// 表示一次地图生成得到的二维逻辑数据。
/// </summary>
public sealed class MapData
{
    /// <summary>
    /// 地图的宽度，单位是网格单元数量。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// 地图的高度，单位是网格单元数量。
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// 地图左下角在 Grid（网格）坐标中的原点。
    /// </summary>
    public Vector2Int Origin { get; }

    /// <summary>
    /// 玩家出生位置对应的网格坐标。
    /// </summary>
    public Vector2Int SpawnCell { get; set; }

    /// <summary>
    /// 道路最终抵达的网格坐标。
    /// </summary>
    public Vector2Int ExitCell { get; set; }

    /// <summary>
    /// 保存所有网格单元的二维数组。
    /// </summary>
    private readonly MapCell[,] cells;
    private readonly List<MapStairConnection> stairs = new List<MapStairConnection>();
    public IReadOnlyList<MapStairConnection> Stairs => stairs;
    public bool HasElevation { get; private set; }
    public int GenerationSeed { get; set; }

    /// <summary>
    /// 创建指定尺寸的空地图数据，并把所有单元初始化为草地。
    /// </summary>
    /// <param name="width">地图宽度。</param>
    /// <param name="height">地图高度。</param>
    /// <param name="origin">地图左下角网格坐标。</param>
    public MapData(int width, int height, Vector2Int origin)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "地图宽度必须大于 0。");

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "地图高度必须大于 0。");

        Width = width;
        Height = height;
        Origin = origin;
        cells = new MapCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                cells[x, y] = new MapCell { terrainType = MapTerrainType.Grass };
        }
    }

    /// <summary>
    /// 判断一个网格坐标是否位于地图范围内。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <returns>位于地图内时返回 true。</returns>
    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= Origin.x && cell.x < Origin.x + Width &&
               cell.y >= Origin.y && cell.y < Origin.y + Height;
    }

    /// <summary>
    /// 判断一个网格坐标是否位于地图边界保护带中。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <param name="borderSize">边界保护带宽度。</param>
    /// <returns>位于保护带中时返回 true。</returns>
    public bool IsBorder(Vector2Int cell, int borderSize)
    {
        if (!IsInside(cell) || borderSize <= 0)
            return false;

        int localX = cell.x - Origin.x;
        int localY = cell.y - Origin.y;
        return localX < borderSize || localY < borderSize ||
               localX >= Width - borderSize || localY >= Height - borderSize;
    }

    /// <summary>
    /// 读取指定网格单元的数据。
    /// </summary>
    /// <param name="cell">网格坐标。</param>
    /// <returns>对应的地图单元。</returns>
    public MapCell GetCell(Vector2Int cell)
    {
        EnsureInside(cell);
        return cells[cell.x - Origin.x, cell.y - Origin.y];
    }

    /// <summary>
    /// 修改指定网格单元的地形类型。
    /// </summary>
    /// <param name="cell">网格坐标。</param>
    /// <param name="terrainType">新的地形类型。</param>
    public void SetTerrain(Vector2Int cell, MapTerrainType terrainType)
    {
        EnsureInside(cell);
        cells[cell.x - Origin.x, cell.y - Origin.y].terrainType = terrainType;
    }

    public int GetElevation(Vector2Int cell) => GetCell(cell).elevation;

    public void SetElevation(Vector2Int cell, int elevation)
    {
        EnsureInside(cell);
        if (elevation < 0)
            throw new ArgumentOutOfRangeException(nameof(elevation));
        if (elevation > 0 && !IsWalkable(cell))
            throw new InvalidOperationException("Only walkable surfaces can be elevated.");
        cells[cell.x - Origin.x, cell.y - Origin.y].elevation = elevation;
        if (elevation > 0)
            HasElevation = true;
    }

    public void AddStair(Vector2Int lowerCell, Vector2Int upperCell,
        MapStairVisualComposition visualComposition = MapStairVisualComposition.Auto,
        MapStairVisualSide visualSide = MapStairVisualSide.Both,
        MapStairVisualPlacement visualPlacement = MapStairVisualPlacement.Legacy)
    {
        if (!IsInside(lowerCell) || !IsInside(upperCell) ||
            !IsWalkable(lowerCell) || !IsWalkable(upperCell) ||
            Mathf.Abs(lowerCell.x - upperCell.x) +
            Mathf.Abs(lowerCell.y - upperCell.y) != 1 ||
            GetElevation(upperCell) != GetElevation(lowerCell) + 1)
        {
            throw new ArgumentException("A stair must join adjacent walkable heights.");
        }
        if (IsStairTransition(lowerCell, upperCell))
            return;
        stairs.Add(new MapStairConnection(lowerCell, upperCell,
            visualComposition, visualSide, visualPlacement));
    }

    public bool IsStairTransition(Vector2Int from, Vector2Int to)
    {
        for (int i = 0; i < stairs.Count; i++)
        {
            MapStairConnection stair = stairs[i];
            if ((stair.LowerCell == from && stair.UpperCell == to) ||
                (stair.LowerCell == to && stair.UpperCell == from))
                return true;
        }
        return false;
    }

    public bool IsStairCell(Vector2Int cell)
    {
        for (int i = 0; i < stairs.Count; i++)
            if (stairs[i].LowerCell == cell || stairs[i].UpperCell == cell)
                return true;
        return false;
    }

    public bool CanTraverse(Vector2Int from, Vector2Int to)
    {
        if (!IsInside(from) || !IsInside(to) || !IsWalkable(from) || !IsWalkable(to) ||
            Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y) != 1)
            return false;
        return GetElevation(from) == GetElevation(to) || IsStairTransition(from, to);
    }

    public MapData Copy()
    {
        MapData copy = new MapData(Width, Height, Origin)
        {
            SpawnCell = SpawnCell,
            ExitCell = ExitCell,
            HasElevation = HasElevation,
            GenerationSeed = GenerationSeed
        };
        Array.Copy(cells, copy.cells, cells.Length);
        copy.stairs.AddRange(stairs);
        return copy;
    }

    /// <summary>
    /// 读取指定网格单元是否可行走。
    /// </summary>
    /// <param name="cell">网格坐标。</param>
    /// <returns>可行走时返回 true。</returns>
    public bool IsWalkable(Vector2Int cell)
    {
        return GetCell(cell).IsWalkable;
    }

    /// <summary>
    /// 检查网格坐标并在越界时抛出明确异常。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    private void EnsureInside(Vector2Int cell)
    {
        if (!IsInside(cell))
            throw new ArgumentOutOfRangeException(nameof(cell), $"网格坐标 {cell} 超出地图范围。");
    }
}
