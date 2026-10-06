using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 以幂等方式创建简单装饰调色板，并把测试场景升级为三层简单装饰结构。
/// </summary>
public static class MapGenerationStageTwoMigration
{
    /// <summary>
    /// 随机地图测试场景的项目相对路径。
    /// </summary>
    private const string TestScenePath =
        "Assets/Scenes/MapGeneration/MapGenerationTest.unity";

    /// <summary>
    /// 默认地图生成配置的项目相对路径。
    /// </summary>
    private const string SettingsPath =
        "Assets/MapGeneration/MapGenerationSettings_Default.asset";

    /// <summary>
    /// 简单装饰资产的根目录。
    /// </summary>
    private const string DecorationFolder =
        "Assets/MapGeneration/Decorations";

    /// <summary>
    /// 简单装饰专用 Sprite（精灵图）副本目录。
    /// </summary>
    private const string DecorationSpriteFolder =
        DecorationFolder + "/Sprites";

    /// <summary>
    /// 简单装饰可视 Tile（瓦片）目录。
    /// </summary>
    private const string DecorationTileFolder =
        DecorationFolder + "/Tiles";

    /// <summary>
    /// 简单装饰调色板资产路径。
    /// </summary>
    private const string PalettePath =
        DecorationFolder + "/MapSimpleDecorationPalette.asset";

    /// <summary>
    /// 已经切割为 64 像素网格的树木图集路径。
    /// </summary>
    private const string TreeSourcePath =
        "Assets/_Resources/TinySwords/Resources/Trees/Tree.png";

    /// <summary>
    /// 灌木动画图集所在目录。
    /// </summary>
    private const string BushSourceFolder =
        "Assets/_Resources/TinySwordsFreePack/Terrain/Decorations/Bushes";

    /// <summary>
    /// 陆地岩石原图所在目录。
    /// </summary>
    private const string RockSourceFolder =
        "Assets/_Resources/TinySwordsFreePack/Terrain/Decorations/Rocks";

    /// <summary>
    /// 在 Unity 菜单中执行第二阶段简单装饰迁移。
    /// </summary>
    [MenuItem("Tools/RPG Demo/Map Generation/Migrate Stage Two Simple Decorations")]
    public static void MigrateFromMenu()
    {
        Migrate();
        Debug.Log("MapGeneration 第二阶段简单装饰迁移完成。");
    }

    /// <summary>
    /// 提供给 Unity Batch Mode（批处理模式）的迁移入口。
    /// </summary>
    public static void RunBatchMigration()
    {
        try
        {
            Migrate();
            Debug.Log("MapGeneration 第二阶段简单装饰批处理迁移完成。");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 创建简单装饰瓦片与调色板、升级配置并迁移测试场景。
    /// </summary>
    private static void Migrate()
    {
        EnsureAssetFolders();
        MapSimpleDecorationPalette palette = CreateOrUpdatePalette();
        TileBase decorationCollisionMarker =
            CreateOrUpdateDecorationCollisionMarkerTile();
        UpgradeSettings(palette, decorationCollisionMarker);
        UpgradeTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 确保简单装饰资产、精灵图副本和瓦片目录存在。
    /// </summary>
    private static void EnsureAssetFolders()
    {
        EnsureFolder("Assets/MapGeneration", "Decorations");
        EnsureFolder(DecorationFolder, "Sprites");
        EnsureFolder(DecorationFolder, "Tiles");
    }

    /// <summary>
    /// 在父目录下创建一个尚不存在的 Unity 资产目录。
    /// </summary>
    /// <param name="parentFolder">父目录项目相对路径。</param>
    /// <param name="folderName">需要创建的子目录名称。</param>
    private static void EnsureFolder(string parentFolder, string folderName)
    {
        string folderPath = parentFolder + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// 创建或更新包含树木、灌木和散落岩石固定样式的调色板。
    /// </summary>
    /// <returns>完成配置的简单装饰调色板资产。</returns>
    private static MapSimpleDecorationPalette CreateOrUpdatePalette()
    {
        List<MapSimpleDecorationVariant> variants =
            new List<MapSimpleDecorationVariant>
            {
                CreateTreeVariant(
                    "tree-pine-01",
                    new[] { 0, 4, 5, 6, 16, 17, 18 }),
                CreateTreeVariant(
                    "tree-pine-02",
                    new[] { 1, 7, 8, 9, 19, 20, 21 }),
                CreateTreeVariant(
                    "tree-pine-03",
                    new[] { 2, 10, 11, 12, 22, 23, 24 })
            };

        for (int bushIndex = 1; bushIndex <= 4; bushIndex++)
            variants.Add(CreateBushVariant(bushIndex));

        for (int rockIndex = 1; rockIndex <= 4; rockIndex++)
            variants.Add(CreateRockVariant(rockIndex));

        MapSimpleDecorationPalette palette =
            AssetDatabase.LoadAssetAtPath<MapSimpleDecorationPalette>(PalettePath);
        if (palette == null)
        {
            palette = ScriptableObject.CreateInstance<MapSimpleDecorationPalette>();
            AssetDatabase.CreateAsset(palette, PalettePath);
        }

        palette.Configure(
            "tiny-swords-simple-decoration-v1",
            variants.ToArray());
        EditorUtility.SetDirty(palette);
        return palette;
    }

    /// <summary>
    /// 从七个非透明切片创建一种完整 3×3 树木固定样式。
    /// </summary>
    /// <param name="variantId">树木固定样式稳定编号。</param>
    /// <param name="spriteIndices">顶部、树体三格和树桩三格的切片编号。</param>
    /// <returns>完成映射的树木固定样式。</returns>
    private static MapSimpleDecorationVariant CreateTreeVariant(
        string variantId,
        IReadOnlyList<int> spriteIndices)
    {
        if (spriteIndices == null || spriteIndices.Count != 7)
            throw new ArgumentException("树木固定样式必须提供七个非透明切片。", nameof(spriteIndices));

        TileBase[] tiles = new TileBase[spriteIndices.Count];
        for (int index = 0; index < spriteIndices.Count; index++)
        {
            string spriteName = $"Tree_{spriteIndices[index]}";
            string tilePath = $"{DecorationTileFolder}/{spriteName}.asset";
            tiles[index] = CreateOrUpdateTile(
                tilePath,
                LoadSprite(TreeSourcePath, spriteName));
        }

        MapSimpleDecorationTilePart[] parts =
        {
            new MapSimpleDecorationTilePart(
                new Vector2Int(0, 2),
                tiles[0],
                MapSimpleDecorationRenderLayer.Canopy),
            new MapSimpleDecorationTilePart(
                new Vector2Int(-1, 1),
                tiles[1],
                MapSimpleDecorationRenderLayer.Canopy),
            new MapSimpleDecorationTilePart(
                new Vector2Int(0, 1),
                tiles[2],
                MapSimpleDecorationRenderLayer.Canopy),
            new MapSimpleDecorationTilePart(
                new Vector2Int(1, 1),
                tiles[3],
                MapSimpleDecorationRenderLayer.Canopy),
            new MapSimpleDecorationTilePart(
                new Vector2Int(-1, 0),
                tiles[4],
                MapSimpleDecorationRenderLayer.Ground),
            new MapSimpleDecorationTilePart(
                Vector2Int.zero,
                tiles[5],
                MapSimpleDecorationRenderLayer.Ground),
            new MapSimpleDecorationTilePart(
                new Vector2Int(1, 0),
                tiles[6],
                MapSimpleDecorationRenderLayer.Ground)
        };

        return new MapSimpleDecorationVariant(
            variantId,
            MapSimpleDecorationType.Tree,
            MapSimpleDecorationTerrainMask.Grass |
            MapSimpleDecorationTerrainMask.Forest,
            1,
            new Vector2Int(-1, 0),
            new Vector2Int(1, 2),
            parts,
            new[] { Vector2Int.zero });
    }

    /// <summary>
    /// 从一套灌木动画图集中选择首帧，创建静态灌木固定样式。
    /// </summary>
    /// <param name="bushIndex">从一开始的灌木图集编号。</param>
    /// <returns>不产生碰撞的单格灌木固定样式。</returns>
    private static MapSimpleDecorationVariant CreateBushVariant(int bushIndex)
    {
        string sourcePath = $"{BushSourceFolder}/Bushe{bushIndex}.png";
        string spriteName = $"Bushe{bushIndex}_0_0";
        TileBase tile = CreateOrUpdateTile(
            $"{DecorationTileFolder}/Bush_{bushIndex - 1}.asset",
            LoadSprite(sourcePath, spriteName));

        return new MapSimpleDecorationVariant(
            $"bush-green-{bushIndex:00}",
            MapSimpleDecorationType.Bush,
            MapSimpleDecorationTerrainMask.Grass |
            MapSimpleDecorationTerrainMask.Forest,
            1,
            Vector2Int.zero,
            Vector2Int.zero,
            new[]
            {
                new MapSimpleDecorationTilePart(
                    Vector2Int.zero,
                    tile,
                    MapSimpleDecorationRenderLayer.Ground)
            },
            Array.Empty<Vector2Int>());
    }

    /// <summary>
    /// 复制一张陆地岩石原图为 Single Sprite（单精灵），并创建散落岩石样式。
    /// </summary>
    /// <param name="rockIndex">从一开始的岩石原图编号。</param>
    /// <returns>允许出现在沙地、草地和森林的单格岩石固定样式。</returns>
    private static MapSimpleDecorationVariant CreateRockVariant(int rockIndex)
    {
        string sourcePath = $"{RockSourceFolder}/Rock{rockIndex}.png";
        string spritePath = $"{DecorationSpriteFolder}/ScatteredRock_{rockIndex - 1}.png";
        Sprite sprite = CreateOrUpdateSingleSpriteCopy(sourcePath, spritePath);
        TileBase tile = CreateOrUpdateTile(
            $"{DecorationTileFolder}/ScatteredRock_{rockIndex - 1}.asset",
            sprite);

        return new MapSimpleDecorationVariant(
            $"scattered-rock-{rockIndex:00}",
            MapSimpleDecorationType.ScatteredRock,
            MapSimpleDecorationTerrainMask.Sand |
            MapSimpleDecorationTerrainMask.Grass |
            MapSimpleDecorationTerrainMask.Forest,
            1,
            Vector2Int.zero,
            Vector2Int.zero,
            new[]
            {
                new MapSimpleDecorationTilePart(
                    Vector2Int.zero,
                    tile,
                    MapSimpleDecorationRenderLayer.Ground)
            },
            new[] { Vector2Int.zero });
    }

    /// <summary>
    /// 从多精灵图集中按名称读取一个已经切割的 Sprite（精灵图）。
    /// </summary>
    /// <param name="assetPath">源图集的项目相对路径。</param>
    /// <param name="spriteName">需要读取的切片名称。</param>
    /// <returns>找到的精灵图切片。</returns>
    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is Sprite sprite && sprite.name == spriteName)
                return sprite;
        }

        throw new InvalidOperationException(
            $"在图集 {assetPath} 中找不到精灵图切片 {spriteName}。");
    }

    /// <summary>
    /// 复制原始岩石图片并把副本配置为单精灵，避免修改用户原图切割数据。
    /// </summary>
    /// <param name="sourcePath">原始岩石图片路径。</param>
    /// <param name="targetPath">简单装饰专用副本路径。</param>
    /// <returns>副本导入得到的完整单精灵。</returns>
    private static Sprite CreateOrUpdateSingleSpriteCopy(
        string sourcePath,
        string targetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                throw new InvalidOperationException(
                    $"无法复制散落岩石素材：{sourcePath} -> {targetPath}");
            }
        }

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"无法读取精灵图导入器：{targetPath}");

        if (ConfigureSingleSpriteImporter(importer))
            importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
        if (sprite == null)
            throw new InvalidOperationException($"无法加载散落岩石单精灵：{targetPath}");

        return sprite;
    }

    /// <summary>
    /// 创建或更新一个只负责显示、不直接提供碰撞的 Tile（瓦片）资产。
    /// </summary>
    /// <param name="tilePath">目标瓦片资产路径。</param>
    /// <param name="sprite">瓦片需要显示的精灵图。</param>
    /// <returns>完成配置的可视瓦片资产。</returns>
    private static TileBase CreateOrUpdateTile(
        string tilePath,
        Sprite sprite,
        Tile.ColliderType colliderType = Tile.ColliderType.None)
    {
        if (sprite == null)
            throw new ArgumentNullException(nameof(sprite));

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        bool requiresUpdate =
            tile.sprite != sprite ||
            tile.color != Color.white ||
            tile.transform != Matrix4x4.identity ||
            tile.flags != TileFlags.LockAll ||
            tile.colliderType != colliderType;
        if (requiresUpdate)
        {
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.flags = TileFlags.LockAll;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
        }

        return tile;
    }

    /// <summary>
    /// 按简单装饰资源标准配置 Single Sprite（单精灵）导入器。
    /// </summary>
    /// <param name="importer">待检查和更新的纹理导入器。</param>
    /// <returns>导入器配置发生变化时返回 true。</returns>
    private static bool ConfigureSingleSpriteImporter(TextureImporter importer)
    {
        bool requiresUpdate =
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 64f) ||
            importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled ||
            !importer.alphaIsTransparency ||
            importer.textureCompression != TextureImporterCompression.Uncompressed;
        if (!requiresUpdate)
            return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        return true;
    }

    /// <summary>
    /// 把简单装饰调色板、密度、间距和排序参数写入默认配置资产。
    /// </summary>
    /// <param name="palette">刚创建或更新的简单装饰调色板。</param>
    /// <param name="decorationCollisionMarker">简单装饰使用的较小碰撞标记瓦片。</param>
    private static void UpgradeSettings(
        MapSimpleDecorationPalette palette,
        TileBase decorationCollisionMarker)
    {
        MapGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<MapGenerationSettings>(SettingsPath);
        if (settings == null)
            throw new InvalidOperationException($"找不到地图配置资产：{SettingsPath}");

        SerializedObject serializedSettings = new SerializedObject(settings);
        SetIntegerProperty(
            serializedSettings,
            "generatorVersion",
            MapGenerationSettings.CurrentGeneratorVersion);
        SetObjectProperty(
            serializedSettings,
            "simpleDecorationPalette",
            palette);
        SetObjectProperty(
            serializedSettings,
            "simpleDecorationCollisionMarkerTile",
            decorationCollisionMarker);
        SetIntegerProperty(serializedSettings, "simpleDecorationSeedOffset", 7919);
        SetFloatProperty(serializedSettings, "simpleDecorationNoiseScale", 0.12f);
        SetFloatProperty(serializedSettings, "grassTreeDensity", 0.018f);
        SetFloatProperty(serializedSettings, "forestTreeDensity", 0.1f);
        SetFloatProperty(serializedSettings, "grassBushDensity", 0.025f);
        SetFloatProperty(serializedSettings, "forestBushDensity", 0.06f);
        SetFloatProperty(serializedSettings, "sandRockDensity", 0.025f);
        SetFloatProperty(serializedSettings, "grassRockDensity", 0.012f);
        SetFloatProperty(serializedSettings, "forestRockDensity", 0.008f);
        SetIntegerProperty(serializedSettings, "simpleDecorationMinimumSpacing", 2);
        SetIntegerProperty(serializedSettings, "simpleDecorationSpawnClearRadius", 6);
        SetIntegerProperty(serializedSettings, "simpleDecorationExitClearRadius", 2);
        SetIntegerProperty(serializedSettings, "roadWidth", 2);
        SetIntegerProperty(serializedSettings, "minimumPassageWidth", 2);
        SetIntegerProperty(serializedSettings, "simpleDecorationGroundSortingOrder", 4);
        SetIntegerProperty(serializedSettings, "simpleDecorationCanopySortingOrder", 6);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    /// <summary>
    /// 创建使用半格物理形状的简单装饰碰撞标记瓦片。
    /// </summary>
    /// <returns>配置完成的装饰碰撞标记瓦片。</returns>
    private static TileBase CreateOrUpdateDecorationCollisionMarkerTile()
    {
        const string texturePath =
            DecorationSpriteFolder + "/DecorationCollisionMarker.png";
        const string tilePath =
            DecorationTileFolder + "/DecorationCollisionMarker.asset";

        EnsureDecorationCollisionMarkerTexture(texturePath);
        AssetDatabase.ImportAsset(
            texturePath,
            ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer =
            AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                $"无法读取简单装饰碰撞标记精灵导入器：{texturePath}");
        }

        if (ConfigureSingleSpriteImporter(importer))
            importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        if (sprite == null)
        {
            throw new InvalidOperationException(
                $"无法加载简单装饰碰撞标记精灵：{texturePath}");
        }

        return CreateOrUpdateTile(
            tilePath,
            sprite,
            Tile.ColliderType.Sprite);
    }

    /// <summary>
    /// 生成一张透明画布上的居中小矩形，供碰撞标记使用。
    /// </summary>
    /// <param name="texturePath">目标精灵纹理路径。</param>
    private static void EnsureDecorationCollisionMarkerTexture(string texturePath)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) != null)
            return;

        Texture2D texture =
            new Texture2D(64, 64, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[64 * 64];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = new Color32(255, 255, 255, 0);

        for (int x = 16; x < 48; x++)
        {
            for (int y = 20; y < 44; y++)
                pixels[y * 64 + x] = new Color32(255, 255, 255, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        string absolutePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            texturePath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(
            texturePath,
            ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>
    /// 重用旧装饰层，并补齐简单装饰树冠层和隐藏碰撞层。
    /// </summary>
    private static void UpgradeTestScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        Grid grid = FindComponentInScene<Grid>(scene);
        MapGenerationController controller =
            FindComponentInScene<MapGenerationController>(scene);
        MapSimpleDecorationRenderer decorationRenderer =
            FindComponentInScene<MapSimpleDecorationRenderer>(scene);

        if (grid == null)
            throw new InvalidOperationException("MapGenerationTest 场景缺少 Grid（网格）组件。");

        if (controller == null)
            throw new InvalidOperationException("MapGenerationTest 场景缺少 MapGenerationController。");

        if (decorationRenderer == null)
        {
            throw new InvalidOperationException(
                "MapGenerationTest 场景缺少 MapSimpleDecorationRenderer。");
        }

        Tilemap groundDecoration = EnsureTilemap(
            grid.transform,
            "GeneratedSimpleDecorationGround",
            "GeneratedDecoration",
            4,
            true);
        Tilemap canopyDecoration = EnsureTilemap(
            grid.transform,
            "GeneratedSimpleDecorationCanopy",
            null,
            6,
            true);
        Tilemap decorationCollision = EnsureTilemap(
            grid.transform,
            "GeneratedSimpleDecorationCollision",
            null,
            0,
            false);
        EnsureCompositeCollision(decorationCollision.gameObject);

        SerializedObject serializedRenderer = new SerializedObject(decorationRenderer);
        SetObjectProperty(
            serializedRenderer,
            "groundDecorationTilemap",
            groundDecoration);
        SetObjectProperty(
            serializedRenderer,
            "canopyDecorationTilemap",
            canopyDecoration);
        SetObjectProperty(
            serializedRenderer,
            "decorationCollisionTilemap",
            decorationCollision);
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(decorationRenderer);

        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectProperty(
            serializedController,
            "simpleDecorationRenderer",
            decorationRenderer);
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        groundDecoration.transform.SetSiblingIndex(4);
        canopyDecoration.transform.SetSiblingIndex(5);
        decorationCollision.transform.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            throw new InvalidOperationException("无法保存迁移后的 MapGenerationTest 场景。");
    }

    /// <summary>
    /// 查找、重命名或创建一个 Grid（网格）的直接子 Tilemap（瓦片地图）。
    /// </summary>
    /// <param name="gridTransform">父级 Grid（网格）变换组件。</param>
    /// <param name="targetName">迁移后的对象名称。</param>
    /// <param name="legacyName">可以重用的旧对象名称。</param>
    /// <param name="sortingOrder">目标 Sorting Order（排序顺序）。</param>
    /// <param name="rendererEnabled">渲染器是否启用。</param>
    /// <returns>找到或创建的 Tilemap（瓦片地图）。</returns>
    private static Tilemap EnsureTilemap(
        Transform gridTransform,
        string targetName,
        string legacyName,
        int sortingOrder,
        bool rendererEnabled)
    {
        Transform tilemapTransform = gridTransform.Find(targetName);
        if (tilemapTransform == null && !string.IsNullOrEmpty(legacyName))
            tilemapTransform = gridTransform.Find(legacyName);

        GameObject tilemapObject;
        if (tilemapTransform == null)
        {
            tilemapObject = new GameObject(targetName);
            tilemapObject.transform.SetParent(gridTransform, false);
        }
        else
        {
            tilemapObject = tilemapTransform.gameObject;
            tilemapObject.name = targetName;
        }

        Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
        if (tilemap == null)
            tilemap = tilemapObject.AddComponent<Tilemap>();

        TilemapRenderer tilemapRenderer = tilemapObject.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();

        tilemapRenderer.sortingOrder = sortingOrder;
        tilemapRenderer.enabled = rendererEnabled;
        return tilemap;
    }

    /// <summary>
    /// 为隐藏装饰碰撞层补齐静态刚体、瓦片碰撞体和复合碰撞体。
    /// </summary>
    /// <param name="collisionObject">需要配置的隐藏碰撞层对象。</param>
    private static void EnsureCompositeCollision(GameObject collisionObject)
    {
        Rigidbody2D body = collisionObject.GetComponent<Rigidbody2D>();
        if (body == null)
            body = collisionObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        CompositeCollider2D compositeCollider =
            collisionObject.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
            compositeCollider = collisionObject.AddComponent<CompositeCollider2D>();

        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        compositeCollider.isTrigger = false;

        TilemapCollider2D tilemapCollider =
            collisionObject.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
            tilemapCollider = collisionObject.AddComponent<TilemapCollider2D>();

        tilemapCollider.usedByComposite = true;
        tilemapCollider.isTrigger = false;
    }

    /// <summary>
    /// 在指定场景的根对象和子对象中查找一个组件。
    /// </summary>
    /// <typeparam name="T">需要查找的组件类型。</typeparam>
    /// <param name="scene">目标场景。</param>
    /// <returns>找到的组件；不存在时返回 null。</returns>
    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            T component = rootObject.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// 为 SerializedObject（序列化对象）的对象引用字段赋值。
    /// </summary>
    /// <param name="serializedObject">目标序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">目标对象引用。</param>
    private static void SetObjectProperty(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.objectReferenceValue = value;
    }

    /// <summary>
    /// 为 SerializedObject（序列化对象）的整数值字段赋值。
    /// </summary>
    /// <param name="serializedObject">目标序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">目标整数值。</param>
    private static void SetIntegerProperty(
        SerializedObject serializedObject,
        string propertyName,
        int value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.intValue = value;
    }

    /// <summary>
    /// 为 SerializedObject（序列化对象）的浮点值字段赋值。
    /// </summary>
    /// <param name="serializedObject">目标序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">目标浮点值。</param>
    private static void SetFloatProperty(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.floatValue = value;
    }

    /// <summary>
    /// 获取必需的序列化字段，并在字段不存在时报告明确错误。
    /// </summary>
    /// <param name="serializedObject">待读取的序列化对象。</param>
    /// <param name="propertyName">必需字段名称。</param>
    /// <returns>找到的序列化字段。</returns>
    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{serializedObject.targetObject.name} 缺少序列化字段 {propertyName}。");
        }

        return property;
    }
}
