using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

/// <summary>
/// 把 MapSimpleDecorationData（地图简单装饰数据）写入分层 Tilemap（瓦片地图）。
/// </summary>
public sealed class MapSimpleDecorationRenderer : MonoBehaviour
{
    /// <summary>
    /// 显示树桩、灌木和散落岩石的低层 Tilemap（瓦片地图）。
    /// </summary>
    [FormerlySerializedAs("decorationTilemap")]
    [SerializeField] private Tilemap groundDecorationTilemap;

    /// <summary>
    /// 显示树冠并覆盖玩家的高层 Tilemap（瓦片地图）。
    /// </summary>
    [SerializeField] private Tilemap canopyDecorationTilemap;

    /// <summary>
    /// 保存树桩和散落岩石阻挡区域的隐藏碰撞 Tilemap（瓦片地图）。
    /// </summary>
    [SerializeField] private Tilemap decorationCollisionTilemap;

    /// <summary>
    /// 最近一次成功渲染的简单装饰物数量。
    /// </summary>
    public int PlacementCount { get; private set; }

    /// <summary>
    /// 最近一次写入低层装饰 Tilemap 的瓦片数量。
    /// </summary>
    public int GroundTileCount { get; private set; }

    /// <summary>
    /// 最近一次写入高层树冠 Tilemap 的瓦片数量。
    /// </summary>
    public int CanopyTileCount { get; private set; }

    /// <summary>
    /// 最近一次写入隐藏碰撞 Tilemap 的网格数量。
    /// </summary>
    public int CollisionCellCount { get; private set; }

    /// <summary>
    /// 获取低层简单装饰 Tilemap（瓦片地图）。
    /// </summary>
    public Tilemap GroundDecorationTilemap => groundDecorationTilemap;

    /// <summary>
    /// 获取高层树冠 Tilemap（瓦片地图）。
    /// </summary>
    public Tilemap CanopyDecorationTilemap => canopyDecorationTilemap;

    /// <summary>
    /// 获取隐藏装饰碰撞 Tilemap（瓦片地图）。
    /// </summary>
    public Tilemap DecorationCollisionTilemap => decorationCollisionTilemap;

    /// <summary>
    /// 把简单装饰数据渲染到低层、高层和隐藏碰撞层。
    /// </summary>
    /// <param name="decorationData">待渲染的简单装饰数据。</param>
    /// <param name="mapData">用于验证网格边界的地图数据。</param>
    /// <param name="settings">包含装饰调色板、碰撞瓦片和排序参数的配置。</param>
    public void Render(
        MapSimpleDecorationData decorationData,
        MapData mapData,
        MapGenerationSettings settings)
    {
        if (decorationData == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少简单装饰数据。");
        }

        if (mapData == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少地图数据。");
        }

        ValidateReferences(settings);
        Clear();

        foreach (MapSimpleDecorationPlacement placement in decorationData.Placements)
        {
            MapSimpleDecorationVariant variant =
                settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            if (variant == null || variant.DecorationType != placement.DecorationType)
                continue;

            bool renderedAnyPart = RenderVisualParts(
                placement,
                variant,
                mapData);
            RenderCollisionCells(placement, variant, mapData, settings);

            if (renderedAnyPart)
                PlacementCount++;
        }

        groundDecorationTilemap.CompressBounds();
        canopyDecorationTilemap.CompressBounds();
        decorationCollisionTilemap.CompressBounds();
        ConfigureRenderers(settings);
    }

    /// <summary>
    /// 清空全部简单装饰 Tilemap（瓦片地图）和运行时计数。
    /// </summary>
    public void Clear()
    {
        if (groundDecorationTilemap != null)
            groundDecorationTilemap.ClearAllTiles();

        if (canopyDecorationTilemap != null)
            canopyDecorationTilemap.ClearAllTiles();

        if (decorationCollisionTilemap != null)
            decorationCollisionTilemap.ClearAllTiles();

        PlacementCount = 0;
        GroundTileCount = 0;
        CanopyTileCount = 0;
        CollisionCellCount = 0;
    }

    /// <summary>
    /// 把固定样式的可视部件写入低层或树冠层。
    /// </summary>
    /// <param name="placement">当前装饰物放置结果。</param>
    /// <param name="variant">当前放置结果使用的固定样式。</param>
    /// <param name="mapData">用于验证网格边界的地图数据。</param>
    /// <returns>至少成功写入一个可视瓦片时返回 true。</returns>
    private bool RenderVisualParts(
        MapSimpleDecorationPlacement placement,
        MapSimpleDecorationVariant variant,
        MapData mapData)
    {
        bool renderedAnyPart = false;

        foreach (MapSimpleDecorationTilePart tilePart in variant.TileParts)
        {
            Vector2Int cell = placement.AnchorCell + tilePart.Offset;
            if (!mapData.IsInside(cell) || tilePart.Tile == null)
                continue;

            Vector3Int tileCell = new Vector3Int(cell.x, cell.y, 0);
            Tilemap targetTilemap = tilePart.RenderLayer ==
                                    MapSimpleDecorationRenderLayer.Canopy
                ? canopyDecorationTilemap
                : groundDecorationTilemap;

            if (targetTilemap.HasTile(tileCell))
                continue;

            targetTilemap.SetTile(tileCell, tilePart.Tile);
            renderedAnyPart = true;

            if (tilePart.RenderLayer == MapSimpleDecorationRenderLayer.Canopy)
                CanopyTileCount++;
            else
                GroundTileCount++;
        }

        return renderedAnyPart;
    }

    /// <summary>
    /// 把固定样式定义的阻挡位置写入隐藏碰撞层。
    /// </summary>
    /// <param name="placement">当前装饰物放置结果。</param>
    /// <param name="variant">当前放置结果使用的固定样式。</param>
    /// <param name="mapData">用于验证网格边界的地图数据。</param>
    /// <param name="settings">包含隐藏碰撞标记瓦片的地图配置。</param>
    private void RenderCollisionCells(
        MapSimpleDecorationPlacement placement,
        MapSimpleDecorationVariant variant,
        MapData mapData,
        MapGenerationSettings settings)
    {
        foreach (Vector2Int collisionOffset in variant.CollisionOffsets)
        {
            Vector2Int cell = placement.AnchorCell + collisionOffset;
            if (!mapData.IsInside(cell))
                continue;

            Vector3Int tileCell = new Vector3Int(cell.x, cell.y, 0);
            if (decorationCollisionTilemap.HasTile(tileCell))
                continue;

            decorationCollisionTilemap.SetTile(
                tileCell,
                settings.simpleDecorationCollisionMarkerTile);
            CollisionCellCount++;
        }
    }

    /// <summary>
    /// 应用低层、树冠层和隐藏碰撞层的渲染设置。
    /// </summary>
    /// <param name="settings">地图生成配置。</param>
    private void ConfigureRenderers(MapGenerationSettings settings)
    {
        ConfigureRenderer(
            groundDecorationTilemap,
            settings.simpleDecorationGroundSortingOrder,
            true);
        ConfigureRenderer(
            canopyDecorationTilemap,
            settings.simpleDecorationCanopySortingOrder,
            true);
        ConfigureRenderer(decorationCollisionTilemap, 0, false);
    }

    /// <summary>
    /// 配置指定 Tilemap（瓦片地图）的排序值和显示状态。
    /// </summary>
    /// <param name="tilemap">需要配置的 Tilemap。</param>
    /// <param name="sortingOrder">目标 Sorting Order（排序顺序）。</param>
    /// <param name="isEnabled">是否启用可视渲染。</param>
    private static void ConfigureRenderer(
        Tilemap tilemap,
        int sortingOrder,
        bool isEnabled)
    {
        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            return;

        tilemapRenderer.sortingOrder = sortingOrder;
        tilemapRenderer.enabled = isEnabled;
    }

    /// <summary>
    /// 检查三个 Tilemap（瓦片地图）和地图配置引用是否完整。
    /// </summary>
    /// <param name="settings">地图生成配置。</param>
    private void ValidateReferences(MapGenerationSettings settings)
    {
        if (groundDecorationTilemap == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少 Ground Decoration Tilemap 引用。");
        }

        if (canopyDecorationTilemap == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少 Canopy Decoration Tilemap 引用。");
        }

        if (decorationCollisionTilemap == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少 Decoration Collision Tilemap 引用。");
        }

        if (settings == null)
        {
            throw new MissingReferenceException(
                "MapSimpleDecorationRenderer 缺少 MapGenerationSettings 引用。");
        }

        if (settings.simpleDecorationPalette == null)
        {
            throw new MissingReferenceException(
                "MapGenerationSettings 缺少 MapSimpleDecorationPalette 引用。");
        }

        if (settings.simpleDecorationCollisionMarkerTile == null)
        {
            throw new MissingReferenceException(
                "MapGenerationSettings 缺少 Simple Decoration Collision Marker Tile 引用。");
        }
    }
}
