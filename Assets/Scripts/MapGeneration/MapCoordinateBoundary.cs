using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 在 MapData 逻辑单元与 Unity 世界空间之间提供唯一的坐标转换边界。
/// </summary>
public sealed class MapCoordinateBoundary
{
    /// <summary>
    /// 提供项目实际 GridLayout 转换规则的地表 Tilemap。
    /// </summary>
    private readonly Tilemap tilemap;

    /// <summary>
    /// 使用指定 Tilemap 的 GridLayout 创建坐标边界。
    /// </summary>
    /// <param name="tilemap">权威地表 Tilemap。</param>
    public MapCoordinateBoundary(Tilemap tilemap)
    {
        this.tilemap = tilemap != null
            ? tilemap
            : throw new ArgumentNullException(nameof(tilemap));
    }

    /// <summary>
    /// 获取当前坐标边界使用的 Tilemap。
    /// </summary>
    public Tilemap Tilemap => tilemap;

    /// <summary>
    /// 把地图内的逻辑单元转换为对应的世界坐标中心。
    /// </summary>
    /// <param name="mapData">提供 Origin 和有效范围的地图数据。</param>
    /// <param name="cell">需要转换的逻辑单元。</param>
    /// <returns>逻辑单元在世界空间中的中心。</returns>
    public Vector3 CellToWorld(MapData mapData, Vector2Int cell)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (!mapData.IsInside(cell))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cell),
                $"网格坐标 {cell} 超出地图范围。Origin={mapData.Origin}，" +
                $"Size={mapData.Width}x{mapData.Height}。");
        }

        return CellToWorld(cell);
    }

    /// <summary>
    /// 尝试把地图内的逻辑单元转换为世界坐标中心。
    /// </summary>
    public bool TryCellToWorld(
        MapData mapData,
        Vector2Int cell,
        out Vector3 worldPosition)
    {
        worldPosition = default;
        if (mapData == null || !mapData.IsInside(cell))
            return false;

        worldPosition = CellToWorld(cell);
        return true;
    }

    /// <summary>
    /// 把逻辑单元转换为世界坐标中心，不对具体 MapData 范围作判断。
    /// 该入口用于读取 LegacyStatic 迁移输入；生产地图规则查询应使用带 MapData 的重载。
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    /// <summary>
    /// 把任意连续世界坐标转换为其所在逻辑单元，不会吸附或修改原世界坐标。
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        if (!IsFinite(worldPosition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldPosition),
                "世界坐标必须由有限数值组成。");
        }

        Vector3Int cell = tilemap.WorldToCell(worldPosition);
        return new Vector2Int(cell.x, cell.y);
    }

    /// <summary>
    /// 尝试把连续世界坐标转换为地图范围内的逻辑单元。
    /// </summary>
    public bool TryWorldToCell(
        MapData mapData,
        Vector3 worldPosition,
        out Vector2Int cell)
    {
        cell = default;
        if (mapData == null || !IsFinite(worldPosition))
            return false;

        cell = WorldToCell(worldPosition);
        return mapData.IsInside(cell);
    }

    /// <summary>
    /// 获取 MapData 在 Cell 空间中的半开区间边界。
    /// </summary>
    public static BoundsInt GetCellBounds(MapData mapData)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        return new BoundsInt(
            mapData.Origin.x,
            mapData.Origin.y,
            0,
            mapData.Width,
            mapData.Height,
            1);
    }

    /// <summary>
    /// 把 MapData 的 Cell 边界转换为世界空间边界。
    /// </summary>
    public Bounds CellBoundsToWorld(MapData mapData)
    {
        BoundsInt cellBounds = GetCellBounds(mapData);
        int z = cellBounds.zMin;

        Vector3 bottomLeft = tilemap.CellToWorld(
            new Vector3Int(cellBounds.xMin, cellBounds.yMin, z));
        Vector3 bottomRight = tilemap.CellToWorld(
            new Vector3Int(cellBounds.xMax, cellBounds.yMin, z));
        Vector3 topLeft = tilemap.CellToWorld(
            new Vector3Int(cellBounds.xMin, cellBounds.yMax, z));
        Vector3 topRight = tilemap.CellToWorld(
            new Vector3Int(cellBounds.xMax, cellBounds.yMax, z));

        Bounds worldBounds = new Bounds(bottomLeft, Vector3.zero);
        worldBounds.Encapsulate(bottomRight);
        worldBounds.Encapsulate(topLeft);
        worldBounds.Encapsulate(topRight);
        return worldBounds;
    }

    /// <summary>
    /// 尝试把 MapData 的 Cell 边界转换为有效的二维世界边界。
    /// </summary>
    public bool TryCellBoundsToWorld(MapData mapData, out Bounds worldBounds)
    {
        worldBounds = default;
        if (mapData == null)
            return false;

        worldBounds = CellBoundsToWorld(mapData);
        return worldBounds.size.x > 0f && worldBounds.size.y > 0f;
    }

    /// <summary>
    /// 判断连续世界坐标是否落在 MapData 的任一逻辑单元内。
    /// </summary>
    public bool ContainsWorldPosition(MapData mapData, Vector3 worldPosition)
    {
        return TryWorldToCell(mapData, worldPosition, out _);
    }

    /// <summary>
    /// 判断三维坐标是否全部为有限数值。
    /// </summary>
    private static bool IsFinite(Vector3 position)
    {
        return !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
               !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
               !float.IsNaN(position.z) && !float.IsInfinity(position.z);
    }
}
