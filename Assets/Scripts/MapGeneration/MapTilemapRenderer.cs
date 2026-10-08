using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

/// <summary>
/// 把 MapData（地图数据）转换为分层地形 Tilemap（瓦片地图）和隐藏碰撞 Tilemap。
/// </summary>
public class MapTilemapRenderer : MonoBehaviour
{
    /// <summary>
    /// 覆盖整张地图并显示深水或浅水的 Water Base（水体底层）。
    /// </summary>
    [SerializeField, FormerlySerializedAs("groundTilemap")]
    private Tilemap waterBaseTilemap;

    /// <summary>
    /// 覆盖所有非水单元的 Sand Base（沙地底层）。
    /// </summary>
    [SerializeField] private Tilemap sandBaseTilemap;

    /// <summary>
    /// 覆盖草地、森林和山地的 Grass Overlay（草地覆盖层）。
    /// </summary>
    [SerializeField] private Tilemap grassOverlayTilemap;

    /// <summary>
    /// 显示岩石高地顶面和南侧崖面的 Elevation（高地层）。
    /// </summary>
    [SerializeField] private Tilemap elevationTilemap;

    /// <summary>
    /// 只负责产生基础地形碰撞、不负责显示的碰撞 Tilemap。
    /// </summary>
    [SerializeField, FormerlySerializedAs("collisionTilemap")]
    private Tilemap terrainCollisionTilemap;

    /// <summary>
    /// 基于地表 Tilemap GridLayout 的唯一坐标转换边界。
    /// </summary>
    private MapCoordinateBoundary coordinateBoundary;

    /// <summary>
    /// 最近一次成功投影到 Tilemap 的地图数据，仅用于兼容现有边界消费者。
    /// </summary>
    private MapData lastRenderedMap;

    /// <summary>
    /// 获取 Water Base（水体底层），供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap WaterBaseTilemap => waterBaseTilemap;

    /// <summary>
    /// 获取 Sand Base（沙地底层），供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap SandBaseTilemap => sandBaseTilemap;

    /// <summary>
    /// 获取 Grass Overlay（草地覆盖层），供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap GrassOverlayTilemap => grassOverlayTilemap;

    /// <summary>
    /// 获取 Elevation（高地层），供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap ElevationTilemap => elevationTilemap;

    /// <summary>
    /// 获取隐藏地形碰撞层，供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap TerrainCollisionTilemap => terrainCollisionTilemap;

    /// <summary>
    /// 获取地表 Tilemap，供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap GroundTilemap => waterBaseTilemap;

    /// <summary>
    /// 获取隐藏碰撞 Tilemap，供控制器和编辑器工具使用。
    /// </summary>
    public Tilemap CollisionTilemap => terrainCollisionTilemap;

    /// <summary>
    /// 获取唯一的 Cell/World 和 Bounds 转换入口。
    /// </summary>
    public MapCoordinateBoundary Coordinates
    {
        get
        {
            if (waterBaseTilemap == null)
            {
                throw new MissingReferenceException(
                    "MapTilemapRenderer 缺少 Water Base Tilemap，无法建立坐标边界。");
            }

            if (coordinateBoundary == null || coordinateBoundary.Tilemap != waterBaseTilemap)
                coordinateBoundary = new MapCoordinateBoundary(waterBaseTilemap);

            return coordinateBoundary;
        }
    }

    /// <summary>
    /// 把地图数据批量写入四个视觉层和一个碰撞层。
    /// </summary>
    /// <param name="mapData">待渲染的地图数据。</param>
    /// <param name="settings">包含 Tile（瓦片）引用的地图配置。</param>
    public void Render(MapData mapData, MapGenerationSettings settings)
    {
        if (mapData == null)
            throw new System.ArgumentNullException(nameof(mapData));

        ValidateReferences(settings);

        BoundsInt bounds = new BoundsInt(
            mapData.Origin.x,
            mapData.Origin.y,
            0,
            mapData.Width,
            mapData.Height,
            1);

        int cellCount = mapData.Width * mapData.Height;
        TileBase[] waterTiles = new TileBase[cellCount];
        TileBase[] sandTiles = new TileBase[cellCount];
        TileBase[] grassTiles = new TileBase[cellCount];
        TileBase[] elevationTiles = new TileBase[cellCount];
        TileBase[] collisionTiles = new TileBase[mapData.Width * mapData.Height];

        for (int x = 0; x < mapData.Width; x++)
        {
            for (int y = 0; y < mapData.Height; y++)
            {
                Vector2Int cell = mapData.Origin + new Vector2Int(x, y);
                int index = y * mapData.Width + x;
                MapTerrainType terrainType = mapData.GetCell(cell).terrainType;

                waterTiles[index] = terrainType == MapTerrainType.ShallowWater
                    ? settings.shallowWaterTile
                    : settings.waterTile;

                if (TerrainTopology.UsesSandBase(terrainType))
                {
                    TerrainBoundaryMask sandMask =
                        TerrainTopology.GetSandBoundaryMask(mapData, cell);
                    sandTiles[index] = settings.sandAutotileSet.GetTile(sandMask);
                }

                if (TerrainTopology.UsesGrassOverlay(terrainType))
                {
                    TerrainBoundaryMask grassMask =
                        TerrainTopology.GetGrassBoundaryMask(mapData, cell);
                    grassTiles[index] = settings.grassAutotileSet.GetTile(grassMask);
                }

                if (TerrainTopology.UsesElevation(terrainType))
                {
                    TerrainBoundaryMask elevationMask =
                        TerrainTopology.GetElevationBoundaryMask(mapData, cell);
                    elevationTiles[index] = settings.elevationAutotileSet.GetTile(elevationMask);
                    WriteSouthElevationFace(
                        mapData,
                        cell,
                        elevationMask,
                        settings.elevationAutotileSet,
                        elevationTiles);
                }

                collisionTiles[index] = !mapData.GetCell(cell).IsWalkable
                    ? settings.collisionMarkerTile
                    : null;
            }
        }

        ApplyTiles(waterBaseTilemap, bounds, waterTiles);
        ApplyTiles(sandBaseTilemap, bounds, sandTiles);
        ApplyTiles(grassOverlayTilemap, bounds, grassTiles);
        ApplyTiles(elevationTilemap, bounds, elevationTiles);
        ApplyTiles(terrainCollisionTilemap, bounds, collisionTiles);

        ConfigureRenderers(settings);
        lastRenderedMap = mapData;
    }

    /// <summary>
    /// 清空全部分层地形和碰撞 Tilemap。
    /// </summary>
    public void Clear()
    {
        ClearTilemap(waterBaseTilemap);
        ClearTilemap(sandBaseTilemap);
        ClearTilemap(grassOverlayTilemap);
        ClearTilemap(elevationTilemap);
        ClearTilemap(terrainCollisionTilemap);
        lastRenderedMap = null;
    }

    /// <summary>
    /// 获取最近一次已渲染 MapData 在世界坐标中的边界。
    /// 所有数学转换仍统一委托给 Coordinates。
    /// </summary>
    /// <param name="worldBounds">输出的世界坐标边界。</param>
    /// <returns>已有成功渲染的地图且边界有效时返回 true。</returns>
    public bool TryGetWorldBounds(out Bounds worldBounds)
    {
        worldBounds = default;

        if (waterBaseTilemap == null || lastRenderedMap == null)
            return false;

        return Coordinates.TryCellBoundsToWorld(lastRenderedMap, out worldBounds);
    }

    /// <summary>
    /// 把高地南边界对应的崖面瓦片写入南侧相邻单元。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="topCell">高地顶面所在单元。</param>
    /// <param name="elevationMask">高地顶面的边界掩码。</param>
    /// <param name="autotileSet">高地使用的自动瓦片集合。</param>
    /// <param name="elevationTiles">待写入的高地瓦片缓冲区。</param>
    private static void WriteSouthElevationFace(
        MapData mapData,
        Vector2Int topCell,
        TerrainBoundaryMask elevationMask,
        TerrainAutotileSet autotileSet,
        TileBase[] elevationTiles)
    {
        TileBase faceTile = autotileSet.GetSouthFaceTile(elevationMask);
        if (faceTile == null)
            return;

        Vector2Int faceCell = topCell + Vector2Int.down;
        if (!mapData.IsInside(faceCell))
            return;

        // 崖面只能落在具有沙地底层的陆地单元，避免岩石覆盖深水或浅水。
        MapTerrainType faceTerrainType = mapData.GetCell(faceCell).terrainType;
        if (!TerrainTopology.UsesSandBase(faceTerrainType))
            return;

        int localX = faceCell.x - mapData.Origin.x;
        int localY = faceCell.y - mapData.Origin.y;
        int faceIndex = localY * mapData.Width + localX;
        elevationTiles[faceIndex] = faceTile;
    }

    /// <summary>
    /// 清空并批量写入一个 Tilemap（瓦片地图）。
    /// </summary>
    /// <param name="tilemap">目标瓦片地图。</param>
    /// <param name="bounds">写入区域。</param>
    /// <param name="tiles">按行排列的瓦片缓冲区。</param>
    private static void ApplyTiles(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles)
    {
        tilemap.ClearAllTiles();
        tilemap.SetTilesBlock(bounds, tiles);
        tilemap.CompressBounds();
    }

    /// <summary>
    /// 在引用有效时清空一个 Tilemap（瓦片地图）。
    /// </summary>
    /// <param name="tilemap">待清空的瓦片地图。</param>
    private static void ClearTilemap(Tilemap tilemap)
    {
        if (tilemap != null)
            tilemap.ClearAllTiles();
    }

    /// <summary>
    /// 应用四个视觉层的排序顺序，并保证碰撞层默认不显示。
    /// </summary>
    /// <param name="settings">地图配置。</param>
    private void ConfigureRenderers(MapGenerationSettings settings)
    {
        ConfigureRenderer(waterBaseTilemap, settings.waterBaseSortingOrder, true);
        ConfigureRenderer(sandBaseTilemap, settings.sandBaseSortingOrder, true);
        ConfigureRenderer(grassOverlayTilemap, settings.grassOverlaySortingOrder, true);
        ConfigureRenderer(elevationTilemap, settings.elevationSortingOrder, true);

        TilemapRenderer collisionRenderer =
            terrainCollisionTilemap.GetComponent<TilemapRenderer>();
        if (collisionRenderer != null)
            collisionRenderer.enabled = false;
    }

    /// <summary>
    /// 设置一个视觉 Tilemap（瓦片地图）的排序值和启用状态。
    /// </summary>
    /// <param name="tilemap">需要配置的视觉瓦片地图。</param>
    /// <param name="sortingOrder">目标层内排序值。</param>
    /// <param name="isEnabled">渲染器是否启用。</param>
    private static void ConfigureRenderer(Tilemap tilemap, int sortingOrder, bool isEnabled)
    {
        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            return;

        tilemapRenderer.sortingOrder = sortingOrder;
        tilemapRenderer.enabled = isEnabled;
    }

    /// <summary>
    /// 检查分层地形、碰撞 Tilemap 和所有自动瓦片引用是否完整。
    /// </summary>
    /// <param name="settings">地图配置。</param>
    private void ValidateReferences(MapGenerationSettings settings)
    {
        if (waterBaseTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Water Base Tilemap 引用。");

        if (sandBaseTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Sand Base Tilemap 引用。");

        if (grassOverlayTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Grass Overlay Tilemap 引用。");

        if (elevationTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Elevation Tilemap 引用。");

        if (terrainCollisionTilemap == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 Terrain Collision Tilemap 引用。");

        if (settings == null)
            throw new MissingReferenceException("MapTilemapRenderer 缺少 MapGenerationSettings 引用。");

        if (settings.waterTile == null || settings.shallowWaterTile == null ||
            settings.collisionMarkerTile == null)
        {
            throw new MissingReferenceException(
                "MapGenerationSettings 必须配置 waterTile、shallowWaterTile 和 collisionMarkerTile。");
        }

        if (settings.sandAutotileSet == null || !settings.sandAutotileSet.HasCompleteTopology)
            throw new MissingReferenceException("Sand Autotile Set 必须配置完整的 16 种拓扑瓦片。");

        if (settings.grassAutotileSet == null || !settings.grassAutotileSet.HasCompleteTopology)
            throw new MissingReferenceException("Grass Autotile Set 必须配置完整的 16 种拓扑瓦片。");

        if (settings.elevationAutotileSet == null ||
            !settings.elevationAutotileSet.HasCompleteTopology ||
            !settings.elevationAutotileSet.HasCompleteSouthFaces)
        {
            throw new MissingReferenceException(
                "Elevation Autotile Set 必须配置 16 种顶面瓦片和 8 种南侧崖面瓦片。");
        }
    }
}
