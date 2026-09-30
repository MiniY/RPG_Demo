using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 把 MapData（地图数据）转换为地表 Tilemap（瓦片地图）和隐藏碰撞 Tilemap。
/// </summary>
public class MapTilemapRenderer : MonoBehaviour
{
    /// <summary>
    /// 负责显示草地、水域和道路的地表 Tilemap。
    /// </summary>
    [SerializeField] private Tilemap groundTilemap;

    /// <summary>
    /// 只负责产生碰撞、不负责显示的碰撞 Tilemap。
    /// </summary>
    [SerializeField] private Tilemap collisionTilemap;

    /// <summary>
    /// 获取地表 Tilemap，供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap GroundTilemap => groundTilemap;

    /// <summary>
    /// 获取隐藏碰撞 Tilemap，供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap CollisionTilemap => collisionTilemap;

    /// <summary>
    /// 把地图数据批量写入两个 Tilemap。
    /// </summary>
    /// <param name="mapData">待渲染的地图数据。</param>
    /// <param name="settings">包含 Tile（瓦片）引用的地图配置。</param>
    public void Render(MapData mapData, MapGenerationSettings settings)
    {
        ValidateReferences(settings);

        BoundsInt bounds = new BoundsInt(
            mapData.Origin.x,
            mapData.Origin.y,
            0,
            mapData.Width,
            mapData.Height,
            1);

        TileBase[] groundTiles = new TileBase[mapData.Width * mapData.Height];
        TileBase[] collisionTiles = new TileBase[mapData.Width * mapData.Height];

        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                Vector2Int cell = mapData.Origin + new Vector2Int(x, y);
                int index = y * mapData.Width + x;
                MapTerrainType terrainType = mapData.GetCell(cell).terrainType;

                groundTiles[index] = GetVisualTile(terrainType, settings);
                collisionTiles[index] = !mapData.GetCell(cell).IsWalkable
                    ? settings.collisionMarkerTile
                    : null;
            }
        }

        groundTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();
        groundTilemap.SetTilesBlock(bounds, groundTiles);
        collisionTilemap.SetTilesBlock(bounds, collisionTiles);
        groundTilemap.CompressBounds();
        collisionTilemap.CompressBounds();

        ConfigureRenderers(settings);
    }

    /// <summary>
    /// 清空地表和碰撞 Tilemap。
    /// </summary>
    public void Clear()
    {
        if (groundTilemap != null)
            groundTilemap.ClearAllTiles();

        if (collisionTilemap != null)
            collisionTilemap.ClearAllTiles();
    }

    /// <summary>
    /// 把出生网格坐标转换为地表 Tilemap 的世界坐标中心。
    /// </summary>
    /// <param name="cell">出生网格坐标。</param>
    /// <returns>对应的世界坐标。</returns>
    public Vector3 GetCellCenterWorld(Vector2Int cell)
    {
        if (groundTilemap == null)
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        return groundTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
    }

    /// <summary>
    /// 获取当前地表 Tilemap（瓦片地图）在世界坐标中的边界。
    /// </summary>
    /// <param name="worldBounds">输出的世界坐标边界。</param>
    /// <returns>地表存在有效瓦片时返回 true。</returns>
    public bool TryGetWorldBounds(out Bounds worldBounds)
    {
        worldBounds = default;

        if (groundTilemap == null)
            return false;

        BoundsInt cellBounds = groundTilemap.cellBounds;
        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            return false;

        int z = cellBounds.zMin;
        Vector3 bottomLeft = groundTilemap.CellToWorld(
            new Vector3Int(cellBounds.xMin, cellBounds.yMin, z));
        Vector3 bottomRight = groundTilemap.CellToWorld(
            new Vector3Int(cellBounds.xMax, cellBounds.yMin, z));
        Vector3 topLeft = groundTilemap.CellToWorld(
            new Vector3Int(cellBounds.xMin, cellBounds.yMax, z));
        Vector3 topRight = groundTilemap.CellToWorld(
            new Vector3Int(cellBounds.xMax, cellBounds.yMax, z));

        worldBounds = new Bounds(bottomLeft, Vector3.zero);
        worldBounds.Encapsulate(bottomRight);
        worldBounds.Encapsulate(topLeft);
        worldBounds.Encapsulate(topRight);
        return worldBounds.size.x > 0f && worldBounds.size.y > 0f;
    }

    /// <summary>
    /// 获取指定地形的显示 Tile（瓦片）。
    /// </summary>
    /// <param name="terrainType">地形类型。</param>
    /// <param name="settings">地图配置。</param>
    /// <returns>显示用瓦片。</returns>
    private static TileBase GetVisualTile(MapTerrainType terrainType, MapGenerationSettings settings)
    {
        switch (terrainType)
        {
            case MapTerrainType.DeepWater:
                return settings.waterTile;
            case MapTerrainType.ShallowWater:
                return settings.shallowWaterTile;
            case MapTerrainType.Path:
                return settings.pathTile;
            case MapTerrainType.Forest:
                return settings.forestTile;
            case MapTerrainType.Mountain:
                return settings.mountainTile;
            default:
                return settings.grassTile;
        }
    }

    /// <summary>
    /// 应用地表排序顺序，并保证碰撞层默认不显示。
    /// </summary>
    /// <param name="settings">地图配置。</param>
    private void ConfigureRenderers(MapGenerationSettings settings)
    {
        TilemapRenderer groundRenderer = groundTilemap.GetComponent<TilemapRenderer>();
        if (groundRenderer != null)
        {
            groundRenderer.sortingOrder = settings.groundSortingOrder;
            groundRenderer.enabled = true;
        }

        TilemapRenderer collisionRenderer = collisionTilemap.GetComponent<TilemapRenderer>();
        if (collisionRenderer != null)
            collisionRenderer.enabled = false;
    }

    /// <summary>
    /// 检查地表、碰撞 Tilemap 和所有地形 Tile 引用是否完整。
    /// </summary>
    /// <param name="settings">地图配置。</param>
    private void ValidateReferences(MapGenerationSettings settings)
    {
        if (groundTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Ground Tilemap 引用。");

        if (collisionTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Collision Tilemap 引用。");

        if (settings == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 MapGenerationSettings 引用。");

        if (settings.grassTile == null || settings.waterTile == null ||
            settings.shallowWaterTile == null || settings.pathTile == null ||
            settings.forestTile == null || settings.mountainTile == null ||
            settings.collisionMarkerTile == null)
        {
            throw new MissingReferenceException(
                "MapGenerationSettings 必须配置 grassTile、waterTile、shallowWaterTile、pathTile、" +
                "forestTile、mountainTile 和 collisionMarkerTile。");
        }
    }
}
