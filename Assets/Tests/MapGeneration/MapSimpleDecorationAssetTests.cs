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
}
