using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证可重复创建的临时场景可以使用正式运行时 Prefab 完成生成和碰撞配置。
/// </summary>
public class MapGenerationSceneTests
{
    /// <summary>
    /// 正式随机地图运行时 Prefab 的项目相对路径。
    /// </summary>
    private const string RuntimePrefabPath =
        "Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab";

    /// <summary>
    /// 验证测试场景能生成分层地形、分层简单装饰、碰撞瓦片并移动玩家。
    /// </summary>
    [Test]
    public void TestSceneGeneratesLayeredTerrainAndCollisionTilemaps()
    {
        using (MapGenerationSceneFixture fixture = CreateTestFixture())
        {
            Scene testScene = fixture.Scene;
            MapGenerationController controller = FindComponentInScene<MapGenerationController>(testScene);
            Assert.That(controller, Is.Not.Null, "测试场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "测试场景缺少 MapGenerationSettings 引用。");
            Assert.That(controller.TilemapRenderer, Is.Not.Null, "测试场景缺少 MapTilemapRenderer 引用。");
            Assert.That(controller.SimpleDecorationRenderer, Is.Not.Null,
                "测试场景缺少 MapSimpleDecorationRenderer 引用。");
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
            Tilemap groundDecorationTilemap =
                controller.SimpleDecorationRenderer.GroundDecorationTilemap;
            Tilemap canopyDecorationTilemap =
                controller.SimpleDecorationRenderer.CanopyDecorationTilemap;
            Tilemap decorationCollisionTilemap =
                controller.SimpleDecorationRenderer.DecorationCollisionTilemap;
            Assert.That(groundTilemap, Is.Not.Null, "缺少地表 Tilemap。");
            Assert.That(collisionTilemap, Is.Not.Null, "缺少碰撞 Tilemap。");
            Assert.That(waterBaseTilemap, Is.EqualTo(groundTilemap));
            Assert.That(controller.TilemapRenderer.TerrainCollisionTilemap,
                Is.EqualTo(collisionTilemap));
            Assert.That(sandBaseTilemap, Is.Not.Null, "缺少 Sand Base Tilemap。");
            Assert.That(grassOverlayTilemap, Is.Not.Null, "缺少 Grass Overlay Tilemap。");
            Assert.That(elevationTilemap, Is.Not.Null, "缺少 Elevation Tilemap。");
            Assert.That(groundDecorationTilemap, Is.Not.Null, "缺少低层简单装饰 Tilemap。");
            Assert.That(canopyDecorationTilemap, Is.Not.Null, "缺少树冠简单装饰 Tilemap。");
            Assert.That(decorationCollisionTilemap, Is.Not.Null, "缺少简单装饰碰撞 Tilemap。");

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
            Assert.That(controller.Settings.simpleDecorationPalette, Is.Not.Null);
            Assert.That(controller.Settings.simpleDecorationPalette.HasCompleteBasicSet, Is.True);
            Assert.That(controller.Settings.simpleDecorationCollisionMarkerTile,
                Is.Not.Null,
                "缺少简单装饰专用碰撞标记瓦片。");
            Tile decorationCollisionMarker =
                controller.Settings.simpleDecorationCollisionMarkerTile as Tile;
            Assert.That(decorationCollisionMarker, Is.Not.Null);
            Assert.That(decorationCollisionMarker.colliderType,
                Is.EqualTo(Tile.ColliderType.Sprite));
            Assert.That(controller.LastGeneratedSimpleDecorations, Is.Not.Null,
                "没有生成简单装饰数据。");
            Assert.That(controller.LastGeneratedSimpleDecorations.Count, Is.GreaterThan(0));
            int treeCount = controller.LastGeneratedSimpleDecorations.CountByType(
                MapSimpleDecorationType.Tree);
            int bushCount = controller.LastGeneratedSimpleDecorations.CountByType(
                MapSimpleDecorationType.Bush);
            int rockCount = controller.LastGeneratedSimpleDecorations.CountByType(
                MapSimpleDecorationType.ScatteredRock);
            Assert.That(treeCount, Is.GreaterThan(0));
            Assert.That(bushCount, Is.GreaterThan(0));
            Assert.That(rockCount, Is.GreaterThan(0));
            Assert.That(controller.SimpleDecorationRenderer.PlacementCount,
                Is.EqualTo(controller.LastGeneratedSimpleDecorations.Count));
            Assert.That(CountTiles(groundDecorationTilemap.GetTilesBlock(bounds)),
                Is.EqualTo(controller.SimpleDecorationRenderer.GroundTileCount));
            Assert.That(CountTiles(canopyDecorationTilemap.GetTilesBlock(bounds)),
                Is.EqualTo(controller.SimpleDecorationRenderer.CanopyTileCount));
            Assert.That(CountTiles(decorationCollisionTilemap.GetTilesBlock(bounds)),
                Is.EqualTo(controller.SimpleDecorationRenderer.CollisionCellCount));
            Assert.That(controller.SimpleDecorationRenderer.GroundTileCount,
                Is.GreaterThan(0));
            Assert.That(controller.SimpleDecorationRenderer.CanopyTileCount,
                Is.GreaterThan(0));
            Assert.That(controller.SimpleDecorationRenderer.CollisionCellCount,
                Is.GreaterThan(0));
            Assert.That(controller.SimpleDecorationRenderer.GroundTileCount,
                Is.EqualTo(treeCount * 3 + bushCount + rockCount));
            Assert.That(controller.SimpleDecorationRenderer.CanopyTileCount,
                Is.EqualTo(treeCount * 4));
            Assert.That(controller.SimpleDecorationRenderer.CollisionCellCount,
                Is.EqualTo(treeCount + rockCount));

            TilemapCollider2D tilemapCollider = collisionTilemap.GetComponent<TilemapCollider2D>();
            CompositeCollider2D compositeCollider = collisionTilemap.GetComponent<CompositeCollider2D>();
            Rigidbody2D collisionBody = collisionTilemap.GetComponent<Rigidbody2D>();
            TilemapRenderer groundRenderer = groundTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer sandRenderer = sandBaseTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer grassRenderer = grassOverlayTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer elevationRenderer = elevationTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer collisionRenderer = collisionTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer groundDecorationRenderer =
                groundDecorationTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer canopyDecorationRenderer =
                canopyDecorationTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer decorationCollisionRenderer =
                decorationCollisionTilemap.GetComponent<TilemapRenderer>();

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

            Assert.That(groundDecorationTilemap.GetComponent<TilemapCollider2D>(), Is.Null,
                "低层可视装饰 Tilemap 不应直接参与物理碰撞。");
            Assert.That(canopyDecorationTilemap.GetComponent<TilemapCollider2D>(), Is.Null,
                "树冠可视 Tilemap 不应直接参与物理碰撞。");
            Assert.That(groundDecorationRenderer, Is.Not.Null);
            Assert.That(groundDecorationRenderer.enabled, Is.True);
            Assert.That(groundDecorationRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.simpleDecorationGroundSortingOrder));
            Assert.That(canopyDecorationRenderer, Is.Not.Null);
            Assert.That(canopyDecorationRenderer.enabled, Is.True);
            Assert.That(canopyDecorationRenderer.sortingOrder,
                Is.EqualTo(controller.Settings.simpleDecorationCanopySortingOrder));
            Assert.That(controller.Settings.simpleDecorationGroundSortingOrder,
                Is.LessThan(controller.Settings.simpleDecorationCanopySortingOrder));

            TilemapCollider2D decorationTilemapCollider =
                decorationCollisionTilemap.GetComponent<TilemapCollider2D>();
            CompositeCollider2D decorationCompositeCollider =
                decorationCollisionTilemap.GetComponent<CompositeCollider2D>();
            Rigidbody2D decorationCollisionBody =
                decorationCollisionTilemap.GetComponent<Rigidbody2D>();
            Assert.That(decorationTilemapCollider, Is.Not.Null);
            Assert.That(decorationTilemapCollider.enabled, Is.True);
            Assert.That(decorationTilemapCollider.usedByComposite, Is.True);
            Assert.That(decorationCompositeCollider, Is.Not.Null);
            Assert.That(decorationCompositeCollider.enabled, Is.True);
            Assert.That(decorationCollisionBody, Is.Not.Null);
            Assert.That(decorationCollisionBody.bodyType, Is.EqualTo(RigidbodyType2D.Static));
            Assert.That(decorationCollisionRenderer, Is.Not.Null);
            Assert.That(decorationCollisionRenderer.enabled, Is.False);

            controller.ClearMap();
            Assert.That(controller.LastGeneratedMap, Is.Null);
            Assert.That(controller.LastGeneratedSimpleDecorations, Is.Null);
            Assert.That(controller.SimpleDecorationRenderer.PlacementCount, Is.EqualTo(0));
            Assert.That(CountTiles(groundDecorationTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(canopyDecorationTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(decorationCollisionTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(waterBaseTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(sandBaseTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(grassOverlayTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(elevationTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));
            Assert.That(CountTiles(collisionTilemap.GetTilesBlock(bounds)), Is.EqualTo(0));

            Vector3 expectedSpawnPosition = controller.TilemapRenderer.Coordinates.CellToWorld(
                mapData,
                mapData.SpawnCell);
            Assert.That(Vector2.Distance(controller.Player.position, expectedSpawnPosition), Is.LessThan(0.01f));
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
        using (MapGenerationSceneFixture fixture = CreateTestFixture())
        {
            Scene testScene = fixture.Scene;
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
    }

    /// <summary>
    /// 验证沙地单元仍可承载高地南侧崖面，保留沙地岛屿上的岩石景观。
    /// </summary>
    [Test]
    public void ElevationFacesStillRenderOnSandCells()
    {
        using (MapGenerationSceneFixture fixture = CreateTestFixture())
        {
            Scene testScene = fixture.Scene;
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
    /// 创建不保存到项目的独立测试场景，并从正式运行时 Prefab 构建最小测试环境。
    /// </summary>
    private static MapGenerationSceneFixture CreateTestFixture()
    {
        return new MapGenerationSceneFixture(RuntimePrefabPath);
    }

    /// <summary>
    /// 设置测试实例上的序列化对象引用，不修改 Prefab 资产。
    /// </summary>
    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serializedTarget = new SerializedObject(target);
        SerializedProperty property = serializedTarget.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null,
            $"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
        property.objectReferenceValue = value;
        serializedTarget.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 管理临时 Scene、Prefab 实例和最小 Player，并保证测试结束后完整清理。
    /// </summary>
    private sealed class MapGenerationSceneFixture : IDisposable
    {
        private readonly Scene previousActiveScene;
        private Scene playerScene;
        private bool ownsPlayerScene;
        private GameObject player;

        public MapGenerationSceneFixture(string runtimePrefabPath)
        {
            previousActiveScene = SceneManager.GetActiveScene();
            Scene = EditorSceneManager.NewPreviewScene();

            try
            {
                playerScene = GetPlayerScene(previousActiveScene, out ownsPlayerScene);
                Assert.That(GameObject.FindGameObjectsWithTag("Player"), Is.Empty,
                    "Player Fixture 要求 Test Runner 场景中没有其他 Player。");
                player = new GameObject("FixturePlayer");
                player.tag = "Player";
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                GameObject cameraTarget = new GameObject("CameraTarget");
                cameraTarget.transform.SetParent(player.transform, false);
                SceneManager.MoveGameObjectToScene(player, playerScene);

                GameObject runtimePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(runtimePrefabPath);
                Assert.That(runtimePrefab, Is.Not.Null,
                    $"缺少正式随机地图运行时 Prefab：{runtimePrefabPath}");

                GameObject runtimeRoot =
                    PrefabUtility.InstantiatePrefab(runtimePrefab, Scene) as GameObject;
                Assert.That(runtimeRoot, Is.Not.Null, "无法实例化正式随机地图运行时 Prefab。");

                MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
                Assert.That(bootstrap, Is.Not.Null, "正式运行时 Prefab 缺少 MapRuntimeBootstrap。");
                UnityEngine.Object.DestroyImmediate(bootstrap);

                MapGenerationController controller =
                    runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
                MapMinimapController minimap =
                    runtimeRoot.GetComponentInChildren<MapMinimapController>(true);
                Assert.That(controller, Is.Not.Null,
                    "正式运行时 Prefab 缺少 MapGenerationController。");
                Assert.That(minimap, Is.Not.Null,
                    "正式运行时 Prefab 缺少 MapMinimapController。");

                SetObjectReference(controller, "player", player.transform);
                SetObjectReference(minimap, "player", player.transform);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public Scene Scene { get; }

        private static Scene GetPlayerScene(Scene currentScene, out bool ownsScene)
        {
            if (currentScene.IsValid() &&
                currentScene.isLoaded &&
                string.IsNullOrEmpty(currentScene.path))
            {
                ownsScene = false;
                return currentScene;
            }

            Assert.That(
                !currentScene.IsValid() ||
                !currentScene.isLoaded ||
                !string.IsNullOrEmpty(currentScene.path),
                Is.True,
                "为避免污染用户未保存的非空 Scene，测试拒绝创建 Player Fixture。");

            ownsScene = true;
            return EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
        }

        public void Dispose()
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);

            if (Scene.IsValid() && Scene.isLoaded)
                EditorSceneManager.ClosePreviewScene(Scene);

            if (ownsPlayerScene && playerScene.IsValid() && playerScene.isLoaded)
                EditorSceneManager.CloseScene(playerScene, true);
            else if (player != null)
                UnityEngine.Object.DestroyImmediate(player);
        }
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
