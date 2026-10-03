using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证独立地图测试场景可以使用真实组件完成生成和碰撞配置。
/// </summary>
public class MapGenerationSceneTests
{
    /// <summary>
    /// 独立地图测试场景的项目相对路径。
    /// </summary>
    private const string TestScenePath = "Assets/Scenes/MapGeneration/MapGenerationTest.unity";

    /// <summary>
    /// 验证测试场景能生成四层地形、碰撞瓦片，并把玩家移动到出生点。
    /// </summary>
    [Test]
    public void TestSceneGeneratesLayeredTerrainAndCollisionTilemaps()
    {
        Scene testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);

        try
        {
            MapGenerationController controller = FindComponentInScene<MapGenerationController>(testScene);
            Assert.That(controller, Is.Not.Null, "测试场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "测试场景缺少 MapGenerationSettings 引用。");
            Assert.That(controller.TilemapRenderer, Is.Not.Null, "测试场景缺少 MapTilemapRenderer 引用。");
            Assert.That(controller.DecorationRenderer, Is.Not.Null, "测试场景缺少 MapDecorationRenderer 引用。");
            Assert.That(controller.Player, Is.Not.Null, "测试场景缺少玩家引用。");

            MapMinimapController minimapController =
                FindComponentInScene<MapMinimapController>(testScene);
            Assert.That(minimapController, Is.Not.Null, "测试场景缺少 MapMinimapController。");
            Assert.That(minimapController.MapController, Is.EqualTo(controller));
            Assert.That(minimapController.TilemapRenderer, Is.EqualTo(controller.TilemapRenderer));
            Assert.That(minimapController.Player, Is.EqualTo(controller.Player));

            controller.GenerateMap();

            MapData mapData = controller.LastGeneratedMap;
            Assert.That(mapData, Is.Not.Null, "地图生成没有产生 MapData。");

            Tilemap groundTilemap = controller.TilemapRenderer.GroundTilemap;
            Tilemap collisionTilemap = controller.TilemapRenderer.CollisionTilemap;
            Tilemap waterBaseTilemap = controller.TilemapRenderer.WaterBaseTilemap;
            Tilemap sandBaseTilemap = controller.TilemapRenderer.SandBaseTilemap;
            Tilemap grassOverlayTilemap = controller.TilemapRenderer.GrassOverlayTilemap;
            Tilemap elevationTilemap = controller.TilemapRenderer.ElevationTilemap;
            Tilemap decorationTilemap = controller.DecorationRenderer.DecorationTilemap;
            Assert.That(groundTilemap, Is.Not.Null, "缺少地表 Tilemap。");
            Assert.That(collisionTilemap, Is.Not.Null, "缺少碰撞 Tilemap。");
            Assert.That(waterBaseTilemap, Is.EqualTo(groundTilemap));
            Assert.That(controller.TilemapRenderer.TerrainCollisionTilemap,
                Is.EqualTo(collisionTilemap));
            Assert.That(sandBaseTilemap, Is.Not.Null, "缺少 Sand Base Tilemap。");
            Assert.That(grassOverlayTilemap, Is.Not.Null, "缺少 Grass Overlay Tilemap。");
            Assert.That(elevationTilemap, Is.Not.Null, "缺少 Elevation Tilemap。");
            Assert.That(decorationTilemap, Is.Not.Null, "缺少装饰 Tilemap。");

            Assert.That(controller.Settings.generatorVersion,
                Is.EqualTo(MapGenerationSettings.CurrentGeneratorVersion));
            Assert.That(controller.Settings.sandAutotileSet, Is.Not.Null);
            Assert.That(controller.Settings.grassAutotileSet, Is.Not.Null);
            Assert.That(controller.Settings.elevationAutotileSet, Is.Not.Null);
            Assert.That(controller.Settings.sandAutotileSet.HasCompleteTopology, Is.True);
            Assert.That(controller.Settings.grassAutotileSet.HasCompleteTopology, Is.True);
            Assert.That(controller.Settings.elevationAutotileSet.HasCompleteTopology, Is.True);
            Assert.That(controller.Settings.elevationAutotileSet.HasCompleteSouthFaces, Is.True);

            BoundsInt bounds = new BoundsInt(
                mapData.Origin.x,
                mapData.Origin.y,
                0,
                mapData.Width,
                mapData.Height,
                1);

            Assert.That(
                CountTiles(waterBaseTilemap.GetTilesBlock(bounds)),
                Is.EqualTo(mapData.Width * mapData.Height));
            Assert.That(CountTiles(sandBaseTilemap.GetTilesBlock(bounds)), Is.GreaterThan(0));
            Assert.That(CountTiles(grassOverlayTilemap.GetTilesBlock(bounds)), Is.GreaterThan(0));
            Assert.That(CountTiles(elevationTilemap.GetTilesBlock(bounds)), Is.GreaterThan(0));
            Assert.That(CountTiles(collisionTilemap.GetTilesBlock(bounds)), Is.GreaterThan(0));
            Assert.That(controller.LastGeneratedDecorations, Is.Not.Null, "没有生成装饰数据。");
            Assert.That(controller.LastGeneratedDecorations.Count, Is.GreaterThan(0));
            Assert.That(controller.DecorationRenderer.PlacementCount,
                Is.EqualTo(controller.LastGeneratedDecorations.Count));
            Assert.That(CountTiles(decorationTilemap.GetTilesBlock(bounds)),
                Is.EqualTo(controller.DecorationRenderer.PlacementCount));

            TilemapCollider2D tilemapCollider = collisionTilemap.GetComponent<TilemapCollider2D>();
            CompositeCollider2D compositeCollider = collisionTilemap.GetComponent<CompositeCollider2D>();
            Rigidbody2D collisionBody = collisionTilemap.GetComponent<Rigidbody2D>();
            TilemapRenderer groundRenderer = groundTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer sandRenderer = sandBaseTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer grassRenderer = grassOverlayTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer elevationRenderer = elevationTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer collisionRenderer = collisionTilemap.GetComponent<TilemapRenderer>();

            Assert.That(tilemapCollider, Is.Not.Null, "碰撞 Tilemap 缺少 TilemapCollider2D。");
            Assert.That(tilemapCollider.enabled, Is.True);
            Assert.That(tilemapCollider.usedByComposite, Is.True);
            Assert.That(compositeCollider, Is.Not.Null, "碰撞 Tilemap 缺少 CompositeCollider2D。");
            Assert.That(compositeCollider.enabled, Is.True);
            Assert.That(collisionBody, Is.Not.Null, "碰撞 Tilemap 缺少 Rigidbody2D。");
            Assert.That(collisionBody.bodyType, Is.EqualTo(RigidbodyType2D.Static));
            Assert.That(groundRenderer, Is.Not.Null, "地表 Tilemap 缺少 TilemapRenderer。");
            Assert.That(groundRenderer != null && groundRenderer.enabled, Is.True);
            Assert.That(groundRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.waterBaseSortingOrder));
            Assert.That(sandRenderer, Is.Not.Null);
            Assert.That(sandRenderer.enabled, Is.True);
            Assert.That(sandRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.sandBaseSortingOrder));
            Assert.That(grassRenderer, Is.Not.Null);
            Assert.That(grassRenderer.enabled, Is.True);
            Assert.That(grassRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.grassOverlaySortingOrder));
            Assert.That(elevationRenderer, Is.Not.Null);
            Assert.That(elevationRenderer.enabled, Is.True);
            Assert.That(elevationRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.elevationSortingOrder));
            Assert.That(collisionRenderer, Is.Not.Null, "碰撞 Tilemap 缺少 TilemapRenderer。");
            Assert.That(collisionRenderer != null && !collisionRenderer.enabled, Is.True);

            Assert.That(decorationTilemap.GetComponent<TilemapCollider2D>(), Is.Null,
                "装饰 Tilemap 不应默认参与物理碰撞。");
            TilemapRenderer decorationRenderer = decorationTilemap.GetComponent<TilemapRenderer>();
            Assert.That(decorationRenderer, Is.Not.Null, "装饰 Tilemap 缺少 TilemapRenderer。");
            Assert.That(decorationRenderer != null && decorationRenderer.enabled, Is.True);

            controller.ClearMap();
            Assert.That(controller.LastGeneratedMap, Is.Null);
            Assert.That(controller.LastGeneratedDecorations, Is.Null);
            Assert.That(controller.DecorationRenderer.PlacementCount, Is.EqualTo(0));
            Assert.That(CountTiles(decorationTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(waterBaseTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(sandBaseTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(grassOverlayTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(elevationTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(collisionTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));

            Vector3 expectedSpawnPosition = controller.TilemapRenderer.GetCellCenterWorld(mapData.SpawnCell);
            Assert.That(Vector2.Distance(controller.Player.position, expectedSpawnPosition), Is.LessThan(0.01f));
        }
        finally
        {
            if (testScene.IsValid() && testScene.isLoaded)
                EditorSceneManager.CloseScene(testScene, true);
        }
    }

    /// <summary>
    /// 验证高地南侧崖面不会跨格写入逻辑水域。
    /// </summary>
    /// <param name="waterType">需要验证的深水或浅水类型。</param>
    [TestCase(MapTerrainType.DeepWater)]
    [TestCase(MapTerrainType.ShallowWater)]
    public void ElevationFacesDoNotRenderOnWaterCells(MapTerrainType waterType)
    {
        Scene testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);

        try
        {
            MapGenerationController controller = FindComponentInScene<MapGenerationController>(testScene);
            Assert.That(controller, Is.Not.Null, "测试场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "测试场景缺少 MapGenerationSettings 引用。");
            Assert.That(controller.TilemapRenderer, Is.Not.Null, "测试场景缺少 MapTilemapRenderer 引用。");

            MapData mapData = CreateFilledMap(3, 3, MapTerrainType.Sand);
            Vector2Int mountainCell = new Vector2Int(1, 1);
            Vector2Int waterCell = mountainCell + Vector2Int.down;
            mapData.SetTerrain(mountainCell, MapTerrainType.Mountain);
            mapData.SetTerrain(waterCell, waterType);

            controller.TilemapRenderer.Render(mapData, controller.Settings);

            Tilemap elevationTilemap = controller.TilemapRenderer.ElevationTilemap;
            Assert.That(
                elevationTilemap.GetTile(new Vector3Int(mountainCell.x, mountainCell.y, 0)),
                Is.Not.Null,
                "山地单元必须保留高地顶面瓦片。");
            Assert.That(
                elevationTilemap.GetTile(new Vector3Int(waterCell.x, waterCell.y, 0)),
                Is.Null,
                "高地南侧崖面不得写入深水或浅水单元。");
        }
        finally
        {
            if (testScene.IsValid() && testScene.isLoaded)
                EditorSceneManager.CloseScene(testScene, true);
        }
    }

    /// <summary>
    /// 验证沙地单元仍可承载高地南侧崖面，保留沙地岛屿上的岩石景观。
    /// </summary>
    [Test]
    public void ElevationFacesStillRenderOnSandCells()
    {
        Scene testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);

        try
        {
            MapGenerationController controller = FindComponentInScene<MapGenerationController>(testScene);
            Assert.That(controller, Is.Not.Null, "测试场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "测试场景缺少 MapGenerationSettings 引用。");
            Assert.That(controller.TilemapRenderer, Is.Not.Null, "测试场景缺少 MapTilemapRenderer 引用。");

            MapData mapData = CreateFilledMap(3, 3, MapTerrainType.Sand);
            Vector2Int mountainCell = new Vector2Int(1, 1);
            Vector2Int sandCell = mountainCell + Vector2Int.down;
            mapData.SetTerrain(mountainCell, MapTerrainType.Mountain);

            controller.TilemapRenderer.Render(mapData, controller.Settings);

            Tilemap elevationTilemap = controller.TilemapRenderer.ElevationTilemap;
            Assert.That(
                elevationTilemap.GetTile(new Vector3Int(sandCell.x, sandCell.y, 0)),
                Is.Not.Null,
                "具有沙地底层的单元必须继续允许显示高地南侧崖面。");
        }
        finally
        {
            if (testScene.IsValid() && testScene.isLoaded)
                EditorSceneManager.CloseScene(testScene, true);
        }
    }

    /// <summary>
    /// 验证摄像机限制范围会把视口保持在地图边界内。
    /// </summary>
    [Test]
    public void CameraLimitsKeepViewportInsideMapBounds()
    {
        Bounds mapBounds = new Bounds(
            new Vector3(32f, 32f, 0f),
            new Vector3(64f, 64f, 0f));

        MapGenerationCameraFollow.CalculateCameraLimits(
            mapBounds,
            5f,
            2f,
            Vector2.zero,
            out Vector2 minimumCameraPosition,
            out Vector2 maximumCameraPosition);

        Assert.That(minimumCameraPosition.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(minimumCameraPosition.y, Is.EqualTo(5f).Within(0.001f));
        Assert.That(maximumCameraPosition.x, Is.EqualTo(54f).Within(0.001f));
        Assert.That(maximumCameraPosition.y, Is.EqualTo(59f).Within(0.001f));

        Vector3 bottomLeft = MapGenerationCameraFollow.ClampPositionToBounds(
            new Vector3(-10f, -10f, -7f),
            minimumCameraPosition,
            maximumCameraPosition);
        Vector3 topRight = MapGenerationCameraFollow.ClampPositionToBounds(
            new Vector3(100f, 100f, -7f),
            minimumCameraPosition,
            maximumCameraPosition);

        Assert.That(bottomLeft.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(bottomLeft.y, Is.EqualTo(5f).Within(0.001f));
        Assert.That(bottomLeft.z, Is.EqualTo(-7f).Within(0.001f));
        Assert.That(topRight.x, Is.EqualTo(54f).Within(0.001f));
        Assert.That(topRight.y, Is.EqualTo(59f).Within(0.001f));
        Assert.That(topRight.z, Is.EqualTo(-7f).Within(0.001f));
    }

    /// <summary>
    /// 统计 Tilemap 区域内非空瓦片的数量。
    /// </summary>
    /// <param name="tiles">待统计的瓦片数组。</param>
    /// <returns>非空瓦片数量。</returns>
    private static int CountTiles(TileBase[] tiles)
    {
        int count = 0;

        foreach (TileBase tile in tiles)
        {
            if (tile != null)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 创建并填充指定尺寸的测试地图。
    /// </summary>
    /// <param name="width">测试地图宽度。</param>
    /// <param name="height">测试地图高度。</param>
    /// <param name="terrainType">所有单元使用的初始地形。</param>
    /// <returns>填充完成的测试地图。</returns>
    private static MapData CreateFilledMap(
        int width,
        int height,
        MapTerrainType terrainType)
    {
        MapData mapData = new MapData(width, height, Vector2Int.zero);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                mapData.SetTerrain(new Vector2Int(x, y), terrainType);
        }

        return mapData;
    }

    /// <summary>
    /// 在指定场景的根对象及其子对象中查找组件。
    /// </summary>
    /// <typeparam name="T">要查找的组件类型。</typeparam>
    /// <param name="scene">目标场景。</param>
    /// <returns>找到的组件，找不到时返回 null。</returns>
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
}
