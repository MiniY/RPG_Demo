using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 以幂等方式把随机地图测试场景和默认配置升级到第一阶段的分层地形结构。
/// </summary>
public static class MapGenerationStageOneMigration
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
    /// 自动瓦片集合资产所在的目录。
    /// </summary>
    private const string AutotileFolder = "Assets/MapGeneration/Terrain";

    /// <summary>
    /// 草地自动瓦片集合的项目相对路径。
    /// </summary>
    private const string GrassAutotilePath =
        AutotileFolder + "/GrassAutotileSet.asset";

    /// <summary>
    /// 沙地自动瓦片集合的项目相对路径。
    /// </summary>
    private const string SandAutotilePath =
        AutotileFolder + "/SandAutotileSet.asset";

    /// <summary>
    /// 岩石高地自动瓦片集合的项目相对路径。
    /// </summary>
    private const string ElevationAutotilePath =
        AutotileFolder + "/ElevationAutotileSet.asset";

    /// <summary>
    /// 0 到 15 掩码对应的序列化字段名称。
    /// </summary>
    private static readonly string[] TopologyPropertyNames =
    {
        "interior",
        "north",
        "east",
        "northEast",
        "south",
        "northSouth",
        "eastSouth",
        "northEastSouth",
        "west",
        "northWest",
        "eastWest",
        "northEastWest",
        "southWest",
        "northSouthWest",
        "eastSouthWest",
        "northEastSouthWest"
    };

    /// <summary>
    /// 草地图集中 0 到 15 掩码对应的 Tile（瓦片）编号。
    /// </summary>
    private static readonly int[] GrassTileIndices =
    {
        11, 1, 12, 2, 19, 27, 20, 28,
        10, 0, 13, 3, 18, 26, 21, 29
    };

    /// <summary>
    /// 沙地图集中 0 到 15 掩码对应的 Tile（瓦片）编号。
    /// </summary>
    private static readonly int[] SandTileIndices =
    {
        15, 6, 16, 7, 23, 31, 24, 32,
        14, 5, 17, 8, 22, 30, 25, 33
    };

    /// <summary>
    /// 高地顶面图集中 0 到 15 掩码对应的 Tile（瓦片）编号。
    /// </summary>
    private static readonly int[] ElevationTileIndices =
    {
        5, 1, 6, 2, 9, 17, 10, 18,
        4, 0, 7, 3, 8, 16, 11, 19
    };

    /// <summary>
    /// 高地南侧崖面使用的八个序列化字段名称。
    /// </summary>
    private static readonly string[] SouthFacePropertyNames =
    {
        "southFaceWest",
        "southFaceCenter",
        "southFaceEast",
        "southFaceWestEast",
        "enclosedSouthFaceWest",
        "enclosedSouthFaceCenter",
        "enclosedSouthFaceEast",
        "enclosedSouthFaceWestEast"
    };

    /// <summary>
    /// 高地南侧崖面字段对应的 Tile（瓦片）编号。
    /// </summary>
    private static readonly int[] SouthFaceTileIndices =
    {
        12, 13, 14, 15, 20, 21, 22, 23
    };

    /// <summary>
    /// 在 Unity 菜单中执行第一阶段迁移。
    /// </summary>
    [MenuItem("Tools/RPG Demo/Map Generation/Migrate Stage One")]
    public static void MigrateFromMenu()
    {
        Migrate();
        Debug.Log("MapGeneration 第一阶段迁移完成。");
    }

    /// <summary>
    /// 提供给 Unity Batch Mode（批处理模式）的迁移入口。
    /// </summary>
    public static void RunBatchMigration()
    {
        try
        {
            Migrate();
            Debug.Log("MapGeneration 第一阶段批处理迁移完成。");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 创建自动瓦片资产、升级默认配置并迁移测试场景。
    /// </summary>
    private static void Migrate()
    {
        EnsureAutotileFolder();

        TerrainAutotileSet grassAutotileSet = CreateOrUpdateAutotileSet(
            GrassAutotilePath,
            "tiny-swords-grass-v1",
            "Tilemap_Flat",
            GrassTileIndices,
            null);
        TerrainAutotileSet sandAutotileSet = CreateOrUpdateAutotileSet(
            SandAutotilePath,
            "tiny-swords-sand-v1",
            "Tilemap_Flat",
            SandTileIndices,
            null);
        TerrainAutotileSet elevationAutotileSet = CreateOrUpdateAutotileSet(
            ElevationAutotilePath,
            "tiny-swords-elevation-v1",
            "Tilemap_Elevation",
            ElevationTileIndices,
            SouthFaceTileIndices);

        UpgradeSettings(
            grassAutotileSet,
            sandAutotileSet,
            elevationAutotileSet);
        UpgradeTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 确保自动瓦片资产目录存在。
    /// </summary>
    private static void EnsureAutotileFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MapGeneration"))
            AssetDatabase.CreateFolder("Assets", "MapGeneration");

        if (!AssetDatabase.IsValidFolder(AutotileFolder))
            AssetDatabase.CreateFolder("Assets/MapGeneration", "Terrain");
    }

    /// <summary>
    /// 创建或更新一个自动瓦片集合，并写入 16 个具名拓扑槽位。
    /// </summary>
    /// <param name="assetPath">自动瓦片集合资产路径。</param>
    /// <param name="setId">自动瓦片集合稳定标识。</param>
    /// <param name="tilePrefix">现有 Tile（瓦片）资产的名称前缀。</param>
    /// <param name="topologyTileIndices">0 到 15 掩码对应的瓦片编号。</param>
    /// <param name="southFaceTileIndices">可选的八个南侧崖面瓦片编号。</param>
    /// <returns>创建或更新完成的自动瓦片集合。</returns>
    private static TerrainAutotileSet CreateOrUpdateAutotileSet(
        string assetPath,
        string setId,
        string tilePrefix,
        int[] topologyTileIndices,
        int[] southFaceTileIndices)
    {
        TerrainAutotileSet autotileSet =
            AssetDatabase.LoadAssetAtPath<TerrainAutotileSet>(assetPath);

        if (autotileSet == null)
        {
            autotileSet = ScriptableObject.CreateInstance<TerrainAutotileSet>();
            AssetDatabase.CreateAsset(autotileSet, assetPath);
        }

        SerializedObject serializedSet = new SerializedObject(autotileSet);
        SetStringProperty(serializedSet, "setId", setId);

        for (int maskValue = 0; maskValue < TopologyPropertyNames.Length; maskValue++)
        {
            TileBase tile = LoadTerrainTile(tilePrefix, topologyTileIndices[maskValue]);
            SetObjectProperty(serializedSet, TopologyPropertyNames[maskValue], tile);
        }

        if (southFaceTileIndices != null)
        {
            for (int faceIndex = 0; faceIndex < SouthFacePropertyNames.Length; faceIndex++)
            {
                TileBase faceTile = LoadTerrainTile(tilePrefix, southFaceTileIndices[faceIndex]);
                SetObjectProperty(serializedSet, SouthFacePropertyNames[faceIndex], faceTile);
            }
        }

        serializedSet.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(autotileSet);
        return autotileSet;
    }

    /// <summary>
    /// 把新地形阈值、自动瓦片集合和排序值写入默认配置资产。
    /// </summary>
    /// <param name="grassAutotileSet">草地自动瓦片集合。</param>
    /// <param name="sandAutotileSet">沙地自动瓦片集合。</param>
    /// <param name="elevationAutotileSet">高地自动瓦片集合。</param>
    private static void UpgradeSettings(
        TerrainAutotileSet grassAutotileSet,
        TerrainAutotileSet sandAutotileSet,
        TerrainAutotileSet elevationAutotileSet)
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
        SetStringProperty(
            serializedSettings,
            "terrainPaletteId",
            "tiny-swords-grass-sand-rock-v1");
        SetFloatProperty(serializedSettings, "sandHeightThreshold", 0.48f);
        SetIntegerProperty(serializedSettings, "minimumNaturalRegionSize", 3);
        SetObjectProperty(serializedSettings, "grassAutotileSet", grassAutotileSet);
        SetObjectProperty(serializedSettings, "sandAutotileSet", sandAutotileSet);
        SetObjectProperty(serializedSettings, "elevationAutotileSet", elevationAutotileSet);
        SetIntegerProperty(serializedSettings, "waterBaseSortingOrder", -20);
        SetIntegerProperty(serializedSettings, "sandBaseSortingOrder", -10);
        SetIntegerProperty(serializedSettings, "grassOverlaySortingOrder", 0);
        SetIntegerProperty(serializedSettings, "elevationSortingOrder", 2);
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    /// <summary>
    /// 重用现有地表和碰撞对象，并补齐第一阶段需要的分层 Tilemap（瓦片地图）。
    /// </summary>
    private static void UpgradeTestScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        Grid grid = FindComponentInScene<Grid>(scene);
        MapTilemapRenderer mapRenderer = FindComponentInScene<MapTilemapRenderer>(scene);

        if (grid == null)
            throw new InvalidOperationException("MapGenerationTest 场景缺少 Grid（网格）组件。");

        if (mapRenderer == null)
            throw new InvalidOperationException("MapGenerationTest 场景缺少 MapTilemapRenderer。");

        Tilemap waterBase = EnsureTilemap(
            grid.transform,
            "GeneratedWaterBase",
            "GeneratedGround",
            -20,
            true);
        Tilemap sandBase = EnsureTilemap(
            grid.transform,
            "GeneratedSandBase",
            null,
            -10,
            true);
        Tilemap grassOverlay = EnsureTilemap(
            grid.transform,
            "GeneratedGrassOverlay",
            null,
            0,
            true);
        Tilemap elevation = EnsureTilemap(
            grid.transform,
            "GeneratedElevation",
            null,
            2,
            true);
        Tilemap terrainCollision = EnsureTilemap(
            grid.transform,
            "GeneratedTerrainCollision",
            "GeneratedCollision",
            0,
            false);

        waterBase.transform.SetSiblingIndex(0);
        sandBase.transform.SetSiblingIndex(1);
        grassOverlay.transform.SetSiblingIndex(2);
        elevation.transform.SetSiblingIndex(3);
        terrainCollision.transform.SetAsLastSibling();

        SerializedObject serializedRenderer = new SerializedObject(mapRenderer);
        SetObjectProperty(serializedRenderer, "waterBaseTilemap", waterBase);
        SetObjectProperty(serializedRenderer, "sandBaseTilemap", sandBase);
        SetObjectProperty(serializedRenderer, "grassOverlayTilemap", grassOverlay);
        SetObjectProperty(serializedRenderer, "elevationTilemap", elevation);
        SetObjectProperty(serializedRenderer, "terrainCollisionTilemap", terrainCollision);
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mapRenderer);

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
    /// <param name="sortingOrder">目标排序值。</param>
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
    /// 从 GrassTiles（草地瓦片）目录加载指定编号的现有 Tile（瓦片）。
    /// </summary>
    /// <param name="tilePrefix">瓦片名称前缀。</param>
    /// <param name="tileIndex">瓦片编号。</param>
    /// <returns>加载成功的瓦片资产。</returns>
    private static TileBase LoadTerrainTile(string tilePrefix, int tileIndex)
    {
        string tilePath = $"Assets/Sprites/GrassTiles/{tilePrefix}_{tileIndex}.asset";
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
        if (tile == null)
            throw new InvalidOperationException($"找不到地形瓦片：{tilePath}");

        return tile;
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
    /// 为 SerializedObject（序列化对象）的对象引用字段赋值并校验字段存在。
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
    /// 为 SerializedObject（序列化对象）的字符串字段赋值。
    /// </summary>
    /// <param name="serializedObject">目标序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">目标字符串。</param>
    private static void SetStringProperty(
        SerializedObject serializedObject,
        string propertyName,
        string value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.stringValue = value;
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
