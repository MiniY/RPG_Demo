using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证第二阶段生成的简单装饰调色板、固定样式和岩石精灵图资产映射。
/// </summary>
public class MapSimpleDecorationAssetTests
{
    /// <summary>
    /// 默认地图生成配置资产路径。
    /// </summary>
    private const string SettingsPath =
        "Assets/MapGeneration/MapGenerationSettings_Default.asset";

    /// <summary>
    /// 简单装饰调色板资产路径。
    /// </summary>
    private const string PalettePath =
        "Assets/MapGeneration/Decorations/MapSimpleDecorationPalette.asset";

    /// <summary>
    /// 散落岩石专用精灵图副本目录。
    /// </summary>
    private const string RockSpriteFolder =
        "Assets/MapGeneration/Decorations/Sprites";

    /// <summary>
    /// 玩家预制体路径，用于验证现有碰撞体能够在装饰物相邻格中通行。
    /// </summary>
    private const string PlayerPrefabPath =
        "Assets/Prefabs/Players/Warrior_Blue.prefab";

    /// <summary>
    /// 验证默认配置引用包含三棵树、四种灌木和四种散落岩石的完整调色板。
    /// </summary>
    [Test]
    public void DefaultSettingsReferenceCompleteSimpleDecorationPalette()
    {
        MapGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<MapGenerationSettings>(SettingsPath);
        MapSimpleDecorationPalette palette =
            AssetDatabase.LoadAssetAtPath<MapSimpleDecorationPalette>(PalettePath);

        Assert.That(settings, Is.Not.Null);
        Assert.That(palette, Is.Not.Null);
        Assert.That(settings.simpleDecorationPalette, Is.EqualTo(palette));
        Assert.That(palette.PaletteId, Is.EqualTo("tiny-swords-simple-decoration-v1"));
        Assert.That(palette.HasCompleteBasicSet, Is.True);
        Assert.That(palette.Variants.Count(variant =>
            variant.DecorationType == MapSimpleDecorationType.Tree), Is.EqualTo(3));
        Assert.That(palette.Variants.Count(variant =>
            variant.DecorationType == MapSimpleDecorationType.Bush), Is.EqualTo(4));
        Assert.That(palette.Variants.Count(variant =>
            variant.DecorationType == MapSimpleDecorationType.ScatteredRock), Is.EqualTo(4));

        List<string> variantIds = palette.Variants
            .Select(variant => variant.VariantId)
            .ToList();
        Assert.That(variantIds.Distinct().Count(), Is.EqualTo(variantIds.Count),
            "简单装饰调色板中的稳定变体编号必须唯一。");
    }

    /// <summary>
    /// 验证完整树木由四格树冠、三格底部和一个树桩碰撞单元组成。
    /// </summary>
    [Test]
    public void TreeVariantsUseCompleteLayeredFootprints()
    {
        MapSimpleDecorationPalette palette =
            AssetDatabase.LoadAssetAtPath<MapSimpleDecorationPalette>(PalettePath);
        Assert.That(palette, Is.Not.Null);

        foreach (MapSimpleDecorationVariant tree in palette.Variants.Where(variant =>
                     variant.DecorationType == MapSimpleDecorationType.Tree))
        {
            Assert.That(tree.IsValid(), Is.True);
            Assert.That(tree.FootprintMinimum, Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(tree.FootprintMaximum, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(tree.FootprintCellCount, Is.EqualTo(9));
            Assert.That(tree.TileParts.Count, Is.EqualTo(7));
            Assert.That(tree.TileParts.Count(part =>
                part.RenderLayer == MapSimpleDecorationRenderLayer.Canopy), Is.EqualTo(4));
            Assert.That(tree.TileParts.Count(part =>
                part.RenderLayer == MapSimpleDecorationRenderLayer.Ground), Is.EqualTo(3));
            Assert.That(tree.CollisionOffsets.Count, Is.EqualTo(1));
            Assert.That(tree.CollisionOffsets[0], Is.EqualTo(Vector2Int.zero));
            Assert.That(tree.SupportsTerrain(MapTerrainType.Grass), Is.True);
            Assert.That(tree.SupportsTerrain(MapTerrainType.Forest), Is.True);
            Assert.That(tree.SupportsTerrain(MapTerrainType.Sand), Is.False);
            Assert.That(tree.SupportsTerrain(MapTerrainType.DeepWater), Is.False);

            foreach (MapSimpleDecorationTilePart part in tree.TileParts)
            {
                Tile tile = part.Tile as Tile;
                Assert.That(tile, Is.Not.Null);
                Assert.That(tile.sprite, Is.Not.Null);
                Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None),
                    "可视树木瓦片不得直接提供碰撞，碰撞必须由隐藏碰撞层负责。");
            }
        }
    }

    /// <summary>
    /// 验证每棵树的七张 Sprite Slice（精灵切片）与 3×3 网格位置严格对应。
    /// </summary>
    [Test]
    public void TreeVariantsMapSourceSpritesToCorrectGridOffsets()
    {
        MapSimpleDecorationPalette palette =
            AssetDatabase.LoadAssetAtPath<MapSimpleDecorationPalette>(PalettePath);
        Assert.That(palette, Is.Not.Null);

        Dictionary<string, Dictionary<Vector2Int, string>> expectedSpriteNames =
            new Dictionary<string, Dictionary<Vector2Int, string>>
            {
                {
                    "tree-pine-01",
                    CreateExpectedTreeSpriteNames(0, 4, 5, 6, 16, 17, 18)
                },
                {
                    "tree-pine-02",
                    CreateExpectedTreeSpriteNames(1, 7, 8, 9, 19, 20, 21)
                },
                {
                    "tree-pine-03",
                    CreateExpectedTreeSpriteNames(2, 10, 11, 12, 22, 23, 24)
                }
            };

        foreach (MapSimpleDecorationVariant tree in palette.Variants.Where(variant =>
                     variant.DecorationType == MapSimpleDecorationType.Tree))
        {
            Assert.That(expectedSpriteNames.ContainsKey(tree.VariantId), Is.True,
                $"没有为树木变体 {tree.VariantId} 定义切片位置预期。");

            Dictionary<Vector2Int, string> expectedParts =
                expectedSpriteNames[tree.VariantId];
            foreach (MapSimpleDecorationTilePart part in tree.TileParts)
            {
                Tile tile = part.Tile as Tile;
                Assert.That(tile, Is.Not.Null);
                Assert.That(tile.sprite, Is.Not.Null);
                Assert.That(expectedParts.ContainsKey(part.Offset), Is.True,
                    $"{tree.VariantId} 出现了未定义的网格偏移 {part.Offset}。");
                Assert.That(tile.sprite.name, Is.EqualTo(expectedParts[part.Offset]),
                    $"{tree.VariantId} 在偏移 {part.Offset} 使用了错误切片。");
            }
        }
    }

    /// <summary>
    /// 验证隐藏装饰碰撞形状不会与位于相邻单元中心的现有玩家碰撞体重叠。
    /// </summary>
    [Test]
    public void DecorationCollisionShapeLeavesAdjacentCellsPassableForPlayer()
    {
        MapGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<MapGenerationSettings>(SettingsPath);
        GameObject playerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.simpleDecorationCollisionMarkerTile, Is.TypeOf<Tile>());
        Assert.That(playerPrefab, Is.Not.Null);

        Tile collisionTile = (Tile)settings.simpleDecorationCollisionMarkerTile;
        Collider2D playerCollider = playerPrefab.GetComponent<Collider2D>();
        Assert.That(playerCollider, Is.Not.Null);
        Assert.That(collisionTile.colliderType, Is.EqualTo(Tile.ColliderType.Sprite),
            "Grid（网格）碰撞会占满整格，现有玩家碰撞体会因此侵入相邻格。");
        Assert.That(collisionTile.sprite, Is.Not.Null,
            "Sprite（精灵）碰撞必须引用带物理形状的标记精灵。");

        Bounds collisionBounds = GetSpritePhysicsBounds(collisionTile.sprite);
        Bounds playerBounds = GetPlayerColliderLocalBounds(playerCollider);

        Assert.That(collisionBounds.min.x,
            Is.GreaterThan(-1f + playerBounds.max.x),
            "碰撞形状向左侵入了相邻单元。");
        Assert.That(collisionBounds.max.x,
            Is.LessThan(1f + playerBounds.min.x),
            "碰撞形状向右侵入了相邻单元。");
        Assert.That(collisionBounds.min.y,
            Is.GreaterThan(-1f + playerBounds.max.y),
            "碰撞形状向下侵入了相邻单元。");
        Assert.That(collisionBounds.max.y,
            Is.LessThan(1f + playerBounds.min.y),
            "碰撞形状向上侵入了相邻单元。");
    }

    /// <summary>
    /// 验证散落岩石使用完整单精灵副本，允许陆地但禁止水域。
    /// </summary>
    [Test]
    public void ScatteredRockAssetsUseWholeSpritesAndLandOnlyRules()
    {
        MapSimpleDecorationPalette palette =
            AssetDatabase.LoadAssetAtPath<MapSimpleDecorationPalette>(PalettePath);
        Assert.That(palette, Is.Not.Null);

        List<MapSimpleDecorationVariant> rocks = palette.Variants
            .Where(variant =>
                variant.DecorationType == MapSimpleDecorationType.ScatteredRock)
            .ToList();
        Assert.That(rocks.Count, Is.EqualTo(4));

        for (int index = 0; index < rocks.Count; index++)
        {
            MapSimpleDecorationVariant rock = rocks[index];
            Assert.That(rock.SupportsTerrain(MapTerrainType.Sand), Is.True);
            Assert.That(rock.SupportsTerrain(MapTerrainType.Grass), Is.True);
            Assert.That(rock.SupportsTerrain(MapTerrainType.Forest), Is.True);
            Assert.That(rock.SupportsTerrain(MapTerrainType.DeepWater), Is.False);
            Assert.That(rock.SupportsTerrain(MapTerrainType.ShallowWater), Is.False);
            Assert.That(rock.SupportsTerrain(MapTerrainType.Mountain), Is.False);
            Assert.That(rock.CollisionOffsets.Count, Is.EqualTo(1));
            Assert.That(rock.TileParts.Count, Is.EqualTo(1));

            Tile tile = rock.TileParts[0].Tile as Tile;
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.sprite, Is.Not.Null);
            Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.None));

            string spritePath = $"{RockSpriteFolder}/ScatteredRock_{index}.png";
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(64f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }

    /// <summary>
    /// 创建一棵树七个可见位置对应的切片名称表。
    /// </summary>
    /// <param name="top">顶部中央切片编号。</param>
    /// <param name="middleLeft">中层左侧切片编号。</param>
    /// <param name="middleCenter">中层中央切片编号。</param>
    /// <param name="middleRight">中层右侧切片编号。</param>
    /// <param name="bottomLeft">底层左侧切片编号。</param>
    /// <param name="bottomCenter">底层中央切片编号。</param>
    /// <param name="bottomRight">底层右侧切片编号。</param>
    /// <returns>网格偏移到切片名称的映射。</returns>
    private static Dictionary<Vector2Int, string> CreateExpectedTreeSpriteNames(
        int top,
        int middleLeft,
        int middleCenter,
        int middleRight,
        int bottomLeft,
        int bottomCenter,
        int bottomRight)
    {
        return new Dictionary<Vector2Int, string>
        {
            { new Vector2Int(0, 2), $"Tree_{top}" },
            { new Vector2Int(-1, 1), $"Tree_{middleLeft}" },
            { new Vector2Int(0, 1), $"Tree_{middleCenter}" },
            { new Vector2Int(1, 1), $"Tree_{middleRight}" },
            { new Vector2Int(-1, 0), $"Tree_{bottomLeft}" },
            { new Vector2Int(0, 0), $"Tree_{bottomCenter}" },
            { new Vector2Int(1, 0), $"Tree_{bottomRight}" }
        };
    }

    /// <summary>
    /// 计算 Sprite（精灵）的全部 Physics Shape（物理形状）包围盒。
    /// </summary>
    /// <param name="sprite">需要检查的精灵。</param>
    /// <returns>以精灵枢轴为原点的物理形状包围盒。</returns>
    private static Bounds GetSpritePhysicsBounds(Sprite sprite)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        Assert.That(shapeCount, Is.GreaterThan(0),
            "碰撞标记精灵必须包含 Physics Shape（物理形状）。");

        List<Vector2> points = new List<Vector2>();
        bool hasPoint = false;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            points.Clear();
            sprite.GetPhysicsShape(shapeIndex, points);
            foreach (Vector2 point in points)
            {
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
                hasPoint = true;
            }
        }

        Assert.That(hasPoint, Is.True);
        Bounds bounds = new Bounds();
        bounds.SetMinMax(minimum, maximum);
        return bounds;
    }

    /// <summary>
    /// 读取玩家 BoxCollider2D（二维盒形碰撞体）或 CapsuleCollider2D（二维胶囊碰撞体）的局部包围盒。
    /// </summary>
    /// <param name="collider">玩家预制体上的二维碰撞体。</param>
    /// <returns>包含 Offset（偏移）的局部碰撞包围盒。</returns>
    private static Bounds GetPlayerColliderLocalBounds(Collider2D collider)
    {
        Vector2 size = Vector2.zero;
        if (collider is BoxCollider2D boxCollider)
            size = boxCollider.size;
        else if (collider is CapsuleCollider2D capsuleCollider)
            size = capsuleCollider.size;
        else
            Assert.Fail($"暂不支持玩家碰撞体类型 {collider.GetType().Name}。");

        return new Bounds(collider.offset, size);
    }
}
