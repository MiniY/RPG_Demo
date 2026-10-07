using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证随机地图已经成为 RPG 主场景的默认地图实现。
/// </summary>
public class MapGenerationRpgIntegrationTests
{
    /// <summary>
    /// RPG 正常游戏流程使用的主场景路径。
    /// </summary>
    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";

    /// <summary>
    /// 随机地图运行时根对象名称。
    /// </summary>
    private const string IntegrationRootName = "RandomMapRuntime";

    /// <summary>
    /// 验证构建入口、旧地图停用、随机地图引用和生成结果。
    /// </summary>
    [Test]
    public void SampleSceneUsesRandomMapAsDefaultMapImplementation()
    {
        Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(TargetScenePath));
        Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);

        Scene targetScene = EditorSceneManager.OpenScene(
            TargetScenePath,
            OpenSceneMode.Additive);

        try
        {
            GameObject legacyGrid = FindRootObject(targetScene, "Grid");
            GameObject integrationRoot = FindRootObject(
                targetScene,
                IntegrationRootName);

            Assert.That(legacyGrid, Is.Not.Null, "旧静态地图 Grid 应保留作为回退实现。");
            Assert.That(legacyGrid.activeSelf, Is.False, "旧静态地图 Grid 不应默认启用。");
            Assert.That(integrationRoot, Is.Not.Null, "主场景缺少随机地图运行时根对象。");
            Assert.That(integrationRoot.activeSelf, Is.True, "随机地图运行时根对象必须启用。");

            MapGenerationController controller =
                integrationRoot.GetComponentInChildren<MapGenerationController>(true);
            Assert.That(controller, Is.Not.Null, "主场景缺少 MapGenerationController。");
            Assert.That(controller.Settings, Is.Not.Null, "主场景缺少地图生成配置。");
            Assert.That(controller.Player, Is.Not.Null, "随机地图控制器没有绑定 RPG 玩家。");
            Assert.That(controller.Player.GetComponent("PlayerAction"), Is.Not.Null,
                "随机地图控制器绑定的对象不是 RPG PlayerAction 玩家。");

            AssertRendererReferences(controller.TilemapRenderer);
            AssertDecorationReferences(controller.SimpleDecorationRenderer);

            MapMinimapController minimap =
                integrationRoot.GetComponentInChildren<MapMinimapController>(true);
            Assert.That(minimap, Is.Not.Null, "主场景缺少 MapMinimapController。");
            Assert.That(minimap.MapController, Is.EqualTo(controller));
            Assert.That(minimap.TilemapRenderer, Is.EqualTo(controller.TilemapRenderer));
            Assert.That(minimap.Player, Is.EqualTo(controller.Player));

            controller.GenerateMap();
            Assert.That(controller.LastGeneratedMap, Is.Not.Null, "主场景随机地图生成失败。");
            Assert.That(controller.LastGeneratedSimpleDecorations, Is.Not.Null,
                "主场景随机装饰生成失败。");

            Vector3 expectedSpawnPosition = controller.TilemapRenderer.GetCellCenterWorld(
                controller.LastGeneratedMap.SpawnCell);
            Assert.That(controller.Player.position.x, Is.EqualTo(expectedSpawnPosition.x).Within(0.01f));
            Assert.That(controller.Player.position.y, Is.EqualTo(expectedSpawnPosition.y).Within(0.01f));
        }
        finally
        {
            EditorSceneManager.CloseScene(targetScene, true);
        }
    }

    /// <summary>
    /// 验证基础地形渲染层与碰撞组件完整。
    /// </summary>
    /// <param name="renderer">待验证的地图渲染器。</param>
    private static void AssertRendererReferences(MapTilemapRenderer renderer)
    {
        Assert.That(renderer, Is.Not.Null, "主场景缺少 MapTilemapRenderer。");
        Assert.That(renderer.WaterBaseTilemap, Is.Not.Null);
        Assert.That(renderer.SandBaseTilemap, Is.Not.Null);
        Assert.That(renderer.GrassOverlayTilemap, Is.Not.Null);
        Assert.That(renderer.ElevationTilemap, Is.Not.Null);
        Assert.That(renderer.TerrainCollisionTilemap, Is.Not.Null);
        Assert.That(renderer.TerrainCollisionTilemap.GetComponent<TilemapCollider2D>(),
            Is.Not.Null,
            "地形碰撞层缺少 TilemapCollider2D。");
        Assert.That(renderer.TerrainCollisionTilemap.GetComponent<CompositeCollider2D>(),
            Is.Not.Null,
            "地形碰撞层缺少 CompositeCollider2D。");
    }

    /// <summary>
    /// 验证简单装饰渲染层与碰撞组件完整。
    /// </summary>
    /// <param name="renderer">待验证的简单装饰渲染器。</param>
    private static void AssertDecorationReferences(MapSimpleDecorationRenderer renderer)
    {
        Assert.That(renderer, Is.Not.Null, "主场景缺少 MapSimpleDecorationRenderer。");
        Assert.That(renderer.GroundDecorationTilemap, Is.Not.Null);
        Assert.That(renderer.CanopyDecorationTilemap, Is.Not.Null);
        Assert.That(renderer.DecorationCollisionTilemap, Is.Not.Null);
        Assert.That(renderer.DecorationCollisionTilemap.GetComponent<TilemapCollider2D>(),
            Is.Not.Null,
            "装饰碰撞层缺少 TilemapCollider2D。");
        Assert.That(renderer.DecorationCollisionTilemap.GetComponent<CompositeCollider2D>(),
            Is.Not.Null,
            "装饰碰撞层缺少 CompositeCollider2D。");
    }

    /// <summary>
    /// 在指定场景根对象中按名称查找对象。
    /// </summary>
    /// <param name="scene">待搜索的场景。</param>
    /// <param name="objectName">目标根对象名称。</param>
    /// <returns>找到的根对象；不存在时返回 null。</returns>
    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
                return rootObject;
        }

        return null;
    }
}
