using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 把 MapDecorationData（地图装饰数据）批量写入独立的装饰 Tilemap。
/// </summary>
public class MapDecorationRenderer : MonoBehaviour
{
    /// <summary>
    /// 用于显示树木和其他环境装饰的 Tilemap（瓦片地图）。
    /// </summary>
    [SerializeField] private Tilemap decorationTilemap;

    /// <summary>
    /// 最近一次成功写入装饰层的装饰物数量。
    /// </summary>
    public int PlacementCount { get; private set; }

    /// <summary>
    /// 获取装饰 Tilemap，供测试和其他系统查询。
    /// </summary>
    public Tilemap DecorationTilemap => decorationTilemap;

    /// <summary>
    /// 把装饰数据批量渲染到 Tilemap，并应用排序顺序。
    /// </summary>
    /// <param name="decorationData">待渲染的装饰数据。</param>
    /// <param name="mapData">用于提供地图边界的地图数据。</param>
    /// <param name="settings">包含树木 Tile 和排序参数的配置。</param>
    public void Render(
        MapDecorationData decorationData,
        MapData mapData,
        MapGenerationSettings settings)
    {
        if (decorationData == null)
            throw new MissingReferenceException("MapDecorationRenderer 缺少装饰数据。");

        if (mapData == null)
            throw new MissingReferenceException("MapDecorationRenderer 缺少地图数据。");

        ValidateReferences(settings);

        BoundsInt bounds = new BoundsInt(
            mapData.Origin.x,
            mapData.Origin.y,
            0,
            mapData.Width,
            mapData.Height,
            1);
        TileBase[] decorationTiles = new TileBase[mapData.Width * mapData.Height];
        int renderedCount = 0;

        foreach (MapDecorationPlacement placement in decorationData.Placements)
        {
            if (!mapData.IsInside(placement.Cell))
                continue;

            TileBase tile = GetTreeTile(placement.VariantIndex, settings.treeTiles);
            if (tile == null)
                continue;

            int localX = placement.Cell.x - mapData.Origin.x;
            int localY = placement.Cell.y - mapData.Origin.y;
            int index = localY * mapData.Width + localX;
            if (decorationTiles[index] != null)
                continue;

            decorationTiles[index] = tile;
            renderedCount++;
        }

        decorationTilemap.ClearAllTiles();
        decorationTilemap.SetTilesBlock(bounds, decorationTiles);
        decorationTilemap.CompressBounds();
        PlacementCount = renderedCount;
        ConfigureRenderer(settings);
    }

    /// <summary>
    /// 清空装饰 Tilemap 和运行时计数。
    /// </summary>
    public void Clear()
    {
        if (decorationTilemap != null)
            decorationTilemap.ClearAllTiles();

        PlacementCount = 0;
    }

    /// <summary>
    /// 从树木 Tile 数组中安全获取指定变体。
    /// </summary>
    /// <param name="variantIndex">装饰数据记录的变体下标。</param>
    /// <param name="treeTiles">配置中的树木 Tile 数组。</param>
    /// <returns>有效的树木 Tile，全部无效时返回 null。</returns>
    private static TileBase GetTreeTile(int variantIndex, TileBase[] treeTiles)
    {
        if (treeTiles == null || treeTiles.Length == 0)
            return null;

        int startIndex = Mathf.Abs(variantIndex) % treeTiles.Length;
        for (int offset = 0; offset < treeTiles.Length; offset++)
        {
            int index = (startIndex + offset) % treeTiles.Length;
            if (treeTiles[index] != null)
                return treeTiles[index];
        }

        return null;
    }

    /// <summary>
    /// 应用装饰层排序设置。
    /// </summary>
    /// <param name="settings">地图生成配置。</param>
    private void ConfigureRenderer(MapGenerationSettings settings)
    {
        TilemapRenderer renderer = decorationTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
            return;

        renderer.sortingOrder = settings.decorationSortingOrder;
        renderer.enabled = true;
    }

    /// <summary>
    /// 检查装饰 Tilemap 和配置引用是否存在。
    /// </summary>
    /// <param name="settings">地图生成配置。</param>
    private void ValidateReferences(MapGenerationSettings settings)
    {
        if (decorationTilemap == null)
            throw new MissingReferenceException("MapDecorationRenderer 缺少 Decoration Tilemap 引用。");

        if (settings == null)
            throw new MissingReferenceException("MapDecorationRenderer 缺少 MapGenerationSettings 引用。");
    }
}
