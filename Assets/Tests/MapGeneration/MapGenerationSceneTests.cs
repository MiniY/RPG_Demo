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
    /// 验证测试场景能生成地表、碰撞瓦片，并把玩家移动到出生点。
    /// </summary>
    [Test]
    public void TestSceneGeneratesGroundAndCollisionTilemaps()
    {
        Scene testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);

        try
        {
            MapGenerationController controller = FindComponentInScene<MapGenerationController>(testScene);
            Assert.That(controller, Is.Not.Null, "测试场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "测试场景缺少 MapGenerationSettings 引用。");
            Assert.That(controller.TilemapRenderer, Is.Not.Null, "测试场景缺少 MapTilemapRenderer 引用。");
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
            Assert.That(groundTilemap, Is.Not.Null, "缺少地表 Tilemap。");
            Assert.That(collisionTilemap, Is.Not.Null, "缺少碰撞 Tilemap。");

            BoundsInt bounds = new BoundsInt(
                mapData.Origin.x,
                mapData.Origin.y,
                0,
                mapData.Width,
                mapData.Height,
                1);

            Assert.That(CountTiles(groundTilemap.GetTilesBlock(bounds)), Is.EqualTo(mapData.Width * mapData.Height));
            Assert.That(CountTiles(collisionTilemap.GetTilesBlock(bounds)), Is.GreaterThan(0));

            TilemapCollider2D tilemapCollider = collisionTilemap.GetComponent<TilemapCollider2D>();
            CompositeCollider2D compositeCollider = collisionTilemap.GetComponent<CompositeCollider2D>();
            Rigidbody2D collisionBody = collisionTilemap.GetComponent<Rigidbody2D>();
            TilemapRenderer groundRenderer = groundTilemap.GetComponent<TilemapRenderer>();
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
            Assert.That(collisionRenderer, Is.Not.Null, "碰撞 Tilemap 缺少 TilemapRenderer。");
            Assert.That(collisionRenderer != null && !collisionRenderer.enabled, Is.True);

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
