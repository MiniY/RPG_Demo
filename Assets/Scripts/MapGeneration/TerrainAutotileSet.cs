using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 保存四方向 Terrain Boundary Mask（地形边界掩码）对应的具名 Tile（瓦片）。
/// </summary>
[CreateAssetMenu(
    fileName = "TerrainAutotileSet",
    menuName = "RPG Demo/Map Generation/Terrain Autotile Set")]
public sealed class TerrainAutotileSet : ScriptableObject
{
    /// <summary>
    /// 所有有效边界位组成的掩码。
    /// </summary>
    private const TerrainBoundaryMask AllBoundaries =
        TerrainBoundaryMask.North |
        TerrainBoundaryMask.East |
        TerrainBoundaryMask.South |
        TerrainBoundaryMask.West;

    /// <summary>
    /// 当前瓦片集合的稳定标识，用于配置快照和诊断信息。
    /// </summary>
    [SerializeField] private string setId = "terrain";

    /// <summary>
    /// 无边界的内陆瓦片。
    /// </summary>
    [Header("Topology Tiles（拓扑瓦片）")]
    [SerializeField] private TileBase interior;

    /// <summary>
    /// 只有北边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase north;

    /// <summary>
    /// 只有东边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase east;

    /// <summary>
    /// 同时具有北、东边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northEast;

    /// <summary>
    /// 只有南边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase south;

    /// <summary>
    /// 同时具有北、南边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northSouth;

    /// <summary>
    /// 同时具有东、南边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase eastSouth;

    /// <summary>
    /// 同时具有北、东、南边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northEastSouth;

    /// <summary>
    /// 只有西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase west;

    /// <summary>
    /// 同时具有北、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northWest;

    /// <summary>
    /// 同时具有东、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase eastWest;

    /// <summary>
    /// 同时具有北、东、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northEastWest;

    /// <summary>
    /// 同时具有南、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase southWest;

    /// <summary>
    /// 同时具有北、南、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northSouthWest;

    /// <summary>
    /// 同时具有东、南、西边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase eastSouthWest;

    /// <summary>
    /// 四个方向都具有边界的瓦片。
    /// </summary>
    [SerializeField] private TileBase northEastSouthWest;

    /// <summary>
    /// 南侧高地边缘下方使用的西侧崖面瓦片。
    /// </summary>
    [Header("Optional South Faces（可选南侧崖面）")]
    [SerializeField] private TileBase southFaceWest;

    /// <summary>
    /// 南侧高地边缘下方使用的中央崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase southFaceCenter;

    /// <summary>
    /// 南侧高地边缘下方使用的东侧崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase southFaceEast;

    /// <summary>
    /// 南侧高地边缘下方使用的东西双边崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase southFaceWestEast;

    /// <summary>
    /// 同时具有北边界时使用的西侧崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase enclosedSouthFaceWest;

    /// <summary>
    /// 同时具有北边界时使用的中央崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase enclosedSouthFaceCenter;

    /// <summary>
    /// 同时具有北边界时使用的东侧崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase enclosedSouthFaceEast;

    /// <summary>
    /// 同时具有北边界时使用的东西双边崖面瓦片。
    /// </summary>
    [SerializeField] private TileBase enclosedSouthFaceWestEast;

    /// <summary>
    /// 获取当前瓦片集合的稳定标识。
    /// </summary>
    public string SetId => setId;

    /// <summary>
    /// 检查 16 种四方向拓扑瓦片是否全部配置。
    /// </summary>
    public bool HasCompleteTopology
    {
        get
        {
            for (int maskValue = 0; maskValue <= (int)AllBoundaries; maskValue++)
            {
                if (GetTile((TerrainBoundaryMask)maskValue) == null)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 检查高地南侧使用的 8 种崖面瓦片是否全部配置。
    /// </summary>
    public bool HasCompleteSouthFaces =>
        southFaceWest != null &&
        southFaceCenter != null &&
        southFaceEast != null &&
        southFaceWestEast != null &&
        enclosedSouthFaceWest != null &&
        enclosedSouthFaceCenter != null &&
        enclosedSouthFaceEast != null &&
        enclosedSouthFaceWestEast != null;

    /// <summary>
    /// 根据四方向边界组合返回对应的拓扑瓦片。
    /// </summary>
    /// <param name="mask">当前单元的地形边界掩码。</param>
    /// <returns>与掩码匹配的瓦片；槽位未配置时返回 null。</returns>
    public TileBase GetTile(TerrainBoundaryMask mask)
    {
        TerrainBoundaryMask normalizedMask = mask & AllBoundaries;

        switch (normalizedMask)
        {
            case TerrainBoundaryMask.None:
                return interior;
            case TerrainBoundaryMask.North:
                return north;
            case TerrainBoundaryMask.East:
                return east;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.East:
                return northEast;
            case TerrainBoundaryMask.South:
                return south;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.South:
                return northSouth;
            case TerrainBoundaryMask.East | TerrainBoundaryMask.South:
                return eastSouth;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.East | TerrainBoundaryMask.South:
                return northEastSouth;
            case TerrainBoundaryMask.West:
                return west;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.West:
                return northWest;
            case TerrainBoundaryMask.East | TerrainBoundaryMask.West:
                return eastWest;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.East | TerrainBoundaryMask.West:
                return northEastWest;
            case TerrainBoundaryMask.South | TerrainBoundaryMask.West:
                return southWest;
            case TerrainBoundaryMask.North | TerrainBoundaryMask.South | TerrainBoundaryMask.West:
                return northSouthWest;
            case TerrainBoundaryMask.East | TerrainBoundaryMask.South | TerrainBoundaryMask.West:
                return eastSouthWest;
            default:
                return northEastSouthWest;
        }
    }

    /// <summary>
    /// 根据包含南边界的完整掩码返回高地向南延伸一格的崖面瓦片。
    /// </summary>
    /// <param name="mask">高地顶面使用的完整边界掩码。</param>
    /// <returns>匹配的南侧崖面；没有南边界或槽位未配置时返回 null。</returns>
    public TileBase GetSouthFaceTile(TerrainBoundaryMask mask)
    {
        TerrainBoundaryMask normalizedMask = mask & AllBoundaries;
        if ((normalizedMask & TerrainBoundaryMask.South) == 0)
            return null;

        bool hasNorthBoundary = (normalizedMask & TerrainBoundaryMask.North) != 0;
        bool hasEastBoundary = (normalizedMask & TerrainBoundaryMask.East) != 0;
        bool hasWestBoundary = (normalizedMask & TerrainBoundaryMask.West) != 0;

        if (hasNorthBoundary)
        {
            if (hasWestBoundary && hasEastBoundary)
                return enclosedSouthFaceWestEast;

            if (hasWestBoundary)
                return enclosedSouthFaceWest;

            return hasEastBoundary
                ? enclosedSouthFaceEast
                : enclosedSouthFaceCenter;
        }

        if (hasWestBoundary && hasEastBoundary)
            return southFaceWestEast;

        if (hasWestBoundary)
            return southFaceWest;

        return hasEastBoundary ? southFaceEast : southFaceCenter;
    }
}
