using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

/// <summary>
/// 验证 Main 正式场景中的随机地图运行时生命周期。
/// </summary>
public sealed class MapGenerationRpgPlayModeTests
{
    /// <summary>
    /// 进入 Main 场景并验证自动生成、Main 系统连接和重新生成。
    /// </summary>
    [UnityTest]
    public IEnumerator SampleSceneRunsMainGameplayOnGeneratedMap()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;

        Scene scene = SceneManager.GetActiveScene();
        Assert.That(scene.path, Is.EqualTo("Assets/Scenes/SampleScene.unity"));

        GameObject legacyGrid = FindRootObject(scene, "Grid");
        GameObject runtimeRoot = FindRootObject(scene, "RandomMapRuntime");
        Assert.That(legacyGrid, Is.Not.Null);
        Assert.That(legacyGrid.activeSelf, Is.False);
        Assert.That(runtimeRoot, Is.Not.Null);
        Assert.That(runtimeRoot.activeSelf, Is.True);

        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapRuntimeBootstrap runtimeBootstrap =
            runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGeneratedObjectPlacementAdapter placementAdapter =
            runtimeRoot.GetComponent<MapGeneratedObjectPlacementAdapter>();
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);

        Assert.That(controller, Is.Not.Null);
        Assert.That(runtimeBootstrap, Is.Not.Null);
        MapRuntimeDiagnosticSnapshot bootstrapDiagnostics =
            runtimeBootstrap.DiagnosticSnapshot;
        Assert.That(bootstrapDiagnostics.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(bootstrapDiagnostics.Phase, Is.EqualTo(MapLifecyclePhase.Materializing));
        Assert.That(bootstrapDiagnostics.IsFailed, Is.False);
        Assert.That(bootstrapDiagnostics.AuthorityState.RandomGeneratedActive, Is.True);
        Assert.That(bootstrapDiagnostics.AuthorityState.LegacyStaticActive, Is.False);
        Assert.That(controller.LastGeneratedMap, Is.Not.Null,
            "Main 场景进入 Play Mode 后应自动生成随机地图。");
        Assert.That(placementAdapter, Is.Not.Null);
        Assert.That(minimap, Is.Not.Null);
        Assert.That(minimap.Player, Is.EqualTo(controller.Player));
        Assert.That(GameObject.Find("GeneratedMinimapCanvas"), Is.Not.Null,
            "正式场景应创建连接随机地图数据的小地图视图。");

        AssertPlayerAtSpawn(controller);
        AssertGeneratedCollision(controller.TilemapRenderer);
        AssertWorldObjectsPlaced(placementAdapter, controller);
        AssertMainCameraFollowsPlayer(controller.Player);

        MapData firstMap = controller.LastGeneratedMap;
        controller.RegenerateMap();
        yield return null;

        Assert.That(controller.LastGeneratedMap, Is.Not.SameAs(firstMap));
        AssertPlayerAtSpawn(controller);
        AssertWorldObjectsPlaced(placementAdapter, controller);
    }

    /// <summary>
    /// 验证 Main 玩家位于随机地图出生格中心。
    /// </summary>
    private static void AssertPlayerAtSpawn(MapGenerationController controller)
    {
        Vector3 expectedPosition = controller.TilemapRenderer.Coordinates.CellToWorld(
            controller.LastGeneratedMap,
            controller.LastGeneratedMap.SpawnCell);
        Assert.That(controller.Player.position.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
        Assert.That(controller.Player.position.y, Is.EqualTo(expectedPosition.y).Within(0.01f));
    }

    /// <summary>
    /// 验证正式随机地图碰撞层处于可用状态。
    /// </summary>
    private static void AssertGeneratedCollision(MapTilemapRenderer renderer)
    {
        Tilemap collisionTilemap = renderer.TerrainCollisionTilemap;
        Assert.That(collisionTilemap, Is.Not.Null);
        Assert.That(collisionTilemap.GetComponent<TilemapCollider2D>(), Is.Not.Null);
        Assert.That(collisionTilemap.GetComponent<CompositeCollider2D>(), Is.Not.Null);
        Assert.That(collisionTilemap.GetUsedTilesCount(), Is.GreaterThan(0));
    }

    /// <summary>
    /// 验证 Main 怪物、动物和植物已由随机地图定位，并刷新 AI 出生点。
    /// </summary>
    private static void AssertWorldObjectsPlaced(
        MapGeneratedObjectPlacementAdapter adapter,
        MapGenerationController controller)
    {
        bool foundMonsterAi = false;

        foreach (Transform target in adapter.MapAnchoredObjects)
        {
            bool isInside = controller.TilemapRenderer.Coordinates.TryWorldToCell(
                controller.LastGeneratedMap,
                target.position,
                out Vector2Int cell);

            Assert.That(isInside, Is.True);
            Assert.That(controller.LastGeneratedMap.IsWalkable(cell), Is.True);
            Assert.That(
                controller.LastGeneratedSimpleDecorations == null ||
                !controller.LastGeneratedSimpleDecorations.IsOccupied(cell),
                Is.True);

            Component monsterAi = target.GetComponent("MonsterAIController");
            if (monsterAi == null)
                continue;

            foundMonsterAi = true;
            PropertyInfo homePositionProperty = monsterAi.GetType().GetProperty(
                "HomePosition",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(homePositionProperty, Is.Not.Null);
            Vector2 homePosition = (Vector2)homePositionProperty.GetValue(monsterAi);
            // 进入物理帧后碰撞解算可能产生很小的位置修正；出生点仍应位于同一单元附近。
            Assert.That(homePosition.x, Is.EqualTo(target.position.x).Within(0.1f));
            Assert.That(homePosition.y, Is.EqualTo(target.position.y).Within(0.1f));
        }

        Assert.That(foundMonsterAi, Is.True);
    }

    /// <summary>
    /// 验证 Main 原有 Camera 与 Cinemachine 跟随目标仍然连接到 RPG 玩家。
    /// </summary>
    private static void AssertMainCameraFollowsPlayer(Transform player)
    {
        Assert.That(Camera.main, Is.Not.Null);
        Assert.That(Object.FindObjectsOfType<Camera>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectOfType<MapGenerationCameraFollow>(), Is.Null,
            "正式场景不应创建 MapGeneration 测试摄像机。");

        bool followsPlayer = false;
        foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (behaviour.GetType().Name != "CinemachineVirtualCamera")
                continue;

            PropertyInfo followProperty = behaviour.GetType().GetProperty("Follow");
            Transform followTarget = followProperty?.GetValue(behaviour) as Transform;
            if (followTarget == player ||
                (followTarget != null && followTarget.IsChildOf(player)))
            {
                followsPlayer = true;
            }
        }

        Assert.That(followsPlayer, Is.True);
    }

    /// <summary>
    /// 在当前场景根对象中按名称查找对象，包括 inactive（未激活）对象。
    /// </summary>
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
