using UnityEngine;

/// <summary>
/// 保存一张地图中哪些网格已经被玩家探索过。
/// </summary>
public sealed class MapExplorationState
{
    /// <summary>
    /// 探索状态对应的地图宽度。
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// 探索状态对应的地图高度。
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// 探索状态对应的地图原点。
    /// </summary>
    public Vector2Int Origin { get; }

    /// <summary>
    /// 已经探索过的网格数量。
    /// </summary>
    public int ExploredCount { get; private set; }

    /// <summary>
    /// 按地图局部坐标保存探索状态，避免使用世界坐标作为数组下标。
    /// </summary>
    private readonly bool[] exploredCells;

    /// <summary>
    /// 根据地图尺寸创建一份全新的未探索状态。
    /// </summary>
    /// <param name="mapData">探索状态对应的地图数据。</param>
    public MapExplorationState(MapData mapData)
    {
        if (mapData == null)
            throw new System.ArgumentNullException(nameof(mapData));

        Width = mapData.Width;
        Height = mapData.Height;
        Origin = mapData.Origin;
        exploredCells = new bool[Width * Height];
    }

    /// <summary>
    /// 判断指定网格是否已经探索。
    /// </summary>
    /// <param name="cell">待查询的地图网格坐标。</param>
    /// <returns>网格位于地图内且已经探索时返回 true。</returns>
    public bool IsExplored(Vector2Int cell)
    {
        if (!IsInside(cell))
            return false;

        return exploredCells[GetLocalIndex(cell)];
    }

    /// <summary>
    /// 以圆形范围揭示玩家周围的地图网格。
    /// </summary>
    /// <param name="centerCell">探索中心网格。</param>
    /// <param name="radius">探索半径，单位是网格数量。</param>
    /// <returns>本次新揭示的网格数量。</returns>
    public int RevealAround(Vector2Int centerCell, int radius)
    {
        int clampedRadius = Mathf.Max(0, radius);
        int radiusSquared = clampedRadius * clampedRadius;
        int newlyExploredCount = 0;

        int minimumX = Mathf.Max(Origin.x, centerCell.x - clampedRadius);
        int maximumX = Mathf.Min(Origin.x + Width - 1, centerCell.x + clampedRadius);
        int minimumY = Mathf.Max(Origin.y, centerCell.y - clampedRadius);
        int maximumY = Mathf.Min(Origin.y + Height - 1, centerCell.y + clampedRadius);

        for (int x = minimumX; x <= maximumX; x++)
        {
            for (int y = minimumY; y <= maximumY; y++)
            {
                int deltaX = x - centerCell.x;
                int deltaY = y - centerCell.y;
                if (deltaX * deltaX + deltaY * deltaY > radiusSquared)
                    continue;

                Vector2Int cell = new Vector2Int(x, y);
                int localIndex = GetLocalIndex(cell);
                if (exploredCells[localIndex])
                    continue;

                exploredCells[localIndex] = true;
                ExploredCount++;
                newlyExploredCount++;
            }
        }

        return newlyExploredCount;
    }

    /// <summary>
    /// 判断网格坐标是否在当前地图范围内。
    /// </summary>
    /// <param name="cell">待检查的网格坐标。</param>
    /// <returns>网格位于地图范围内时返回 true。</returns>
    private bool IsInside(Vector2Int cell)
    {
        return cell.x >= Origin.x && cell.x < Origin.x + Width &&
               cell.y >= Origin.y && cell.y < Origin.y + Height;
    }

    /// <summary>
    /// 将世界网格坐标转换为探索数组的局部下标。
    /// </summary>
    /// <param name="cell">位于地图内的网格坐标。</param>
    /// <returns>一维探索数组下标。</returns>
    private int GetLocalIndex(Vector2Int cell)
    {
        int localX = cell.x - Origin.x;
        int localY = cell.y - Origin.y;
        return localY * Width + localX;
    }
}
