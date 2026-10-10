using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 描述一个简单装饰物变体中的单个可视瓦片。
/// </summary>
[Serializable]
public sealed class MapSimpleDecorationTilePart
{
    /// <summary>
    /// 相对于装饰物锚点的网格偏移。
    /// </summary>
    [SerializeField] private Vector2Int offset;

    /// <summary>
    /// 写入 Tilemap（瓦片地图）的可视瓦片。
    /// </summary>
    [SerializeField] private TileBase tile;

    /// <summary>
    /// 此瓦片所属的地表层或树冠层。
    /// </summary>
    [SerializeField] private MapSimpleDecorationRenderLayer renderLayer;

    /// <summary>
    /// 获取相对于装饰物锚点的网格偏移。
    /// </summary>
    public Vector2Int Offset => offset;

    /// <summary>
    /// 获取需要渲染的瓦片。
    /// </summary>
    public TileBase Tile => tile;

    /// <summary>
    /// 获取此瓦片所属的渲染层。
    /// </summary>
    public MapSimpleDecorationRenderLayer RenderLayer => renderLayer;

    /// <summary>
    /// 创建一个简单装饰物可视瓦片部件。
    /// </summary>
    /// <param name="offset">相对于装饰物锚点的网格偏移。</param>
    /// <param name="tile">需要渲染的瓦片。</param>
    /// <param name="renderLayer">瓦片所属的渲染层。</param>
    public MapSimpleDecorationTilePart(
        Vector2Int offset,
        TileBase tile,
        MapSimpleDecorationRenderLayer renderLayer)
    {
        this.offset = offset;
        this.tile = tile;
        this.renderLayer = renderLayer;
    }
}

/// <summary>
/// 描述一种固定的简单装饰物样式、占地、地形限制和碰撞位置。
/// </summary>
[Serializable]
public sealed class MapSimpleDecorationVariant
{
    /// <summary>
    /// 跨版本保存时使用的稳定变体编号。
    /// </summary>
    [SerializeField] private string variantId;

    /// <summary>
    /// 当前变体所属的简单装饰物类别。
    /// </summary>
    [SerializeField] private MapSimpleDecorationType decorationType;

    /// <summary>
    /// 当前变体允许出现的地形集合。
    /// </summary>
    [SerializeField] private MapSimpleDecorationTerrainMask allowedTerrains;

    /// <summary>
    /// 同类别多个变体之间进行加权随机选择时使用的权重。
    /// </summary>
    [SerializeField, Min(1)] private int selectionWeight = 1;

    /// <summary>
    /// 相对于锚点的占地矩形最小偏移，边界包含在占地内。
    /// </summary>
    [SerializeField] private Vector2Int footprintMinimum;

    /// <summary>
    /// 相对于锚点的占地矩形最大偏移，边界包含在占地内。
    /// </summary>
    [SerializeField] private Vector2Int footprintMaximum;

    /// <summary>
    /// 组成当前固定样式的全部可视瓦片部件。
    /// </summary>
    [SerializeField] private MapSimpleDecorationTilePart[] tileParts =
        Array.Empty<MapSimpleDecorationTilePart>();

    /// <summary>
    /// 需要写入隐藏碰撞层的锚点相对偏移。
    /// </summary>
    [SerializeField] private Vector2Int[] collisionOffsets = Array.Empty<Vector2Int>();

    /// <summary>
    /// 获取稳定变体编号。
    /// </summary>
    public string VariantId => variantId;

    /// <summary>
    /// 获取简单装饰物类别。
    /// </summary>
    public MapSimpleDecorationType DecorationType => decorationType;

    /// <summary>
    /// 获取允许出现的地形位标记。
    /// </summary>
    public MapSimpleDecorationTerrainMask AllowedTerrains => allowedTerrains;

    /// <summary>
    /// 获取变体选择权重。
    /// </summary>
    public int SelectionWeight => Mathf.Max(1, selectionWeight);

    /// <summary>
    /// 获取占地矩形最小偏移。
    /// </summary>
    public Vector2Int FootprintMinimum => footprintMinimum;

    /// <summary>
    /// 获取占地矩形最大偏移。
    /// </summary>
    public Vector2Int FootprintMaximum => footprintMaximum;

    /// <summary>
    /// 获取组成当前样式的全部可视瓦片。
    /// </summary>
    public IReadOnlyList<MapSimpleDecorationTilePart> TileParts => tileParts;

    /// <summary>
    /// 获取需要写入隐藏碰撞层的偏移集合。
    /// </summary>
    public IReadOnlyList<Vector2Int> CollisionOffsets => collisionOffsets;

    /// <summary>
    /// 获取占地矩形中的网格单元总数。
    /// </summary>
    public int FootprintCellCount =>
        (footprintMaximum.x - footprintMinimum.x + 1) *
        (footprintMaximum.y - footprintMinimum.y + 1);

    /// <summary>
    /// 创建一种固定的简单装饰物变体。
    /// </summary>
    /// <param name="variantId">跨版本使用的稳定变体编号。</param>
    /// <param name="decorationType">简单装饰物类别。</param>
    /// <param name="allowedTerrains">允许出现的地形集合。</param>
    /// <param name="selectionWeight">同类别变体选择权重。</param>
    /// <param name="footprintMinimum">占地矩形最小偏移。</param>
    /// <param name="footprintMaximum">占地矩形最大偏移。</param>
    /// <param name="tileParts">组成固定样式的可视瓦片。</param>
    /// <param name="collisionOffsets">需要产生碰撞的偏移。</param>
    public MapSimpleDecorationVariant(
        string variantId,
        MapSimpleDecorationType decorationType,
        MapSimpleDecorationTerrainMask allowedTerrains,
        int selectionWeight,
        Vector2Int footprintMinimum,
        Vector2Int footprintMaximum,
        MapSimpleDecorationTilePart[] tileParts,
        Vector2Int[] collisionOffsets)
    {
        this.variantId = variantId;
        this.decorationType = decorationType;
        this.allowedTerrains = allowedTerrains;
        this.selectionWeight = Mathf.Max(1, selectionWeight);
        this.footprintMinimum = footprintMinimum;
        this.footprintMaximum = footprintMaximum;
        this.tileParts = tileParts ?? Array.Empty<MapSimpleDecorationTilePart>();
        this.collisionOffsets = collisionOffsets ?? Array.Empty<Vector2Int>();
    }

    /// <summary>
    /// 判断指定逻辑地形是否允许承载当前装饰物变体。
    /// </summary>
    /// <param name="terrainType">待检查的逻辑地形。</param>
    /// <returns>允许放置时返回 true。</returns>
    public bool SupportsTerrain(MapTerrainType terrainType)
    {
        MapSimpleDecorationTerrainMask terrainMask = ToTerrainMask(terrainType);
        return terrainMask != MapSimpleDecorationTerrainMask.None &&
               (allowedTerrains & terrainMask) != 0;
    }

    /// <summary>
    /// 判断一个锚点相对偏移是否位于当前占地矩形中。
    /// </summary>
    /// <param name="offset">待检查的锚点相对偏移。</param>
    /// <returns>位于占地矩形内时返回 true。</returns>
    public bool ContainsFootprintOffset(Vector2Int offset)
    {
        return offset.x >= footprintMinimum.x && offset.x <= footprintMaximum.x &&
               offset.y >= footprintMinimum.y && offset.y <= footprintMaximum.y;
    }

    /// <summary>
    /// 检查变体是否包含有效编号、占地范围、地形和可视瓦片。
    /// </summary>
    /// <returns>满足生成与渲染要求时返回 true。</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(variantId) ||
            allowedTerrains == MapSimpleDecorationTerrainMask.None ||
            footprintMaximum.x < footprintMinimum.x ||
            footprintMaximum.y < footprintMinimum.y ||
            tileParts == null || tileParts.Length == 0)
        {
            return false;
        }

        foreach (MapSimpleDecorationTilePart tilePart in tileParts)
        {
            if (tilePart == null || tilePart.Tile == null ||
                !ContainsFootprintOffset(tilePart.Offset))
            {
                return false;
            }
        }

        if (collisionOffsets == null)
            return false;

        foreach (Vector2Int collisionOffset in collisionOffsets)
        {
            if (!ContainsFootprintOffset(collisionOffset))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 把逻辑地形转换为简单装饰物使用的地形位标记。
    /// </summary>
    /// <param name="terrainType">待转换的逻辑地形。</param>
    /// <returns>对应的地形位标记；不支持时返回 None（无）。</returns>
    private static MapSimpleDecorationTerrainMask ToTerrainMask(
        MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.Grass:
                return MapSimpleDecorationTerrainMask.Grass;
            case MapTerrainType.Forest:
                return MapSimpleDecorationTerrainMask.Forest;
            case MapTerrainType.Sand:
                return MapSimpleDecorationTerrainMask.Sand;
            default:
                return MapSimpleDecorationTerrainMask.None;
        }
    }
}

/// <summary>
/// 保存随机地图可用的树木、灌木和散落岩石固定样式集合。
/// </summary>
[CreateAssetMenu(
    fileName = "MapSimpleDecorationPalette",
    menuName = "RPG Demo/Map Simple Decoration Palette")]
public sealed class MapSimpleDecorationPalette : ScriptableObject
{
    /// <summary>
    /// 存档和配置中识别此装饰调色板的稳定编号。
    /// </summary>
    [SerializeField] private string paletteId = "tiny-swords-simple-decoration-v1";

    /// <summary>
    /// 调色板中所有固定装饰物变体。
    /// </summary>
    [SerializeField] private MapSimpleDecorationVariant[] variants =
        Array.Empty<MapSimpleDecorationVariant>();

    /// <summary>
    /// 获取装饰调色板稳定编号。
    /// </summary>
    public string PaletteId => paletteId;

    /// <summary>
    /// 获取所有固定装饰物变体。
    /// </summary>
    public IReadOnlyList<MapSimpleDecorationVariant> Variants => variants;

    /// <summary>
    /// 检查调色板是否至少包含有效的树木、灌木和散落岩石变体。
    /// </summary>
    public bool HasCompleteBasicSet =>
        HasValidVariant(MapSimpleDecorationType.Tree) &&
        HasValidVariant(MapSimpleDecorationType.Bush) &&
        HasValidVariant(MapSimpleDecorationType.ScatteredRock);

    /// <summary>
    /// 使用固定编号和变体集合配置此调色板资产。
    /// </summary>
    /// <param name="newPaletteId">新的稳定调色板编号。</param>
    /// <param name="newVariants">新的固定装饰物变体集合。</param>
    public void Configure(
        string newPaletteId,
        MapSimpleDecorationVariant[] newVariants)
    {
        paletteId = newPaletteId;
        variants = newVariants ?? Array.Empty<MapSimpleDecorationVariant>();
    }

    /// <summary>
    /// 按稳定编号查找一个有效的简单装饰物变体。
    /// </summary>
    /// <param name="variantId">待查找的稳定变体编号。</param>
    /// <returns>找到的有效变体；不存在时返回 null。</returns>
    public MapSimpleDecorationVariant FindVariant(string variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId) || variants == null)
            return null;

        foreach (MapSimpleDecorationVariant variant in variants)
        {
            if (variant != null && variant.IsValid() && variant.VariantId == variantId)
                return variant;
        }

        return null;
    }

    /// <summary>
    /// 判断调色板是否包含指定类别的有效变体。
    /// </summary>
    /// <param name="decorationType">待检查的简单装饰物类别。</param>
    /// <returns>至少存在一个有效变体时返回 true。</returns>
    public bool HasValidVariant(MapSimpleDecorationType decorationType)
    {
        if (variants == null)
            return false;

        foreach (MapSimpleDecorationVariant variant in variants)
        {
            if (variant != null && variant.DecorationType == decorationType &&
                variant.IsValid())
            {
                return true;
            }
        }

        return false;
    }
}
