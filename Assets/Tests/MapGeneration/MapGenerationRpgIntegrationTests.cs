using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
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
    /// 正式主场景使用的生产随机地图运行时预制体。
    /// </summary>
    private const string RuntimePrefabPath =
        "Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab";

    /// <summary>
    /// 只用于开发和回归测试的独立场景。
    /// </summary>
    private const string DevelopmentScenePath =
        "Assets/Scenes/MapGeneration/MapGenerationTest.unity";

    /// <summary>
    /// 验证构建入口、旧地图停用、随机地图引用和生成结果。
    /// </summary>
    [Test]
    public void SampleSceneUsesRandomMapAsDefaultMapImplementation()
    {
        Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(TargetScenePath));
        Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            Assert.That(buildScene.path, Is.Not.EqualTo(DevelopmentScenePath),
                "开发测试场景不应参与正式构建流程。");
        }

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
            GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(
                integrationRoot);
            Assert.That(prefabSource, Is.Not.Null,
                "主场景随机地图必须来自生产运行时预制体，而不是复制测试场景对象。");
            Assert.That(AssetDatabase.GetAssetPath(prefabSource), Is.EqualTo(RuntimePrefabPath));

            MapGenerationController controller =
                integrationRoot.GetComponentInChildren<MapGenerationController>(true);
            MapRuntimeBootstrap runtimeBootstrap =
                integrationRoot.GetComponent<MapRuntimeBootstrap>();
            Assert.That(controller, Is.Not.Null, "主场景缺少 MapGenerationController。");
            Assert.That(runtimeBootstrap, Is.Not.Null, "主场景缺少显式 Runtime Mode 启动器。");
            Assert.That(runtimeBootstrap.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
            Assert.That(runtimeBootstrap.RandomGeneratedAuthority, Is.EqualTo(controller.gameObject));
            Assert.That(runtimeBootstrap.LegacyStaticAuthority, Is.EqualTo(legacyGrid));

            runtimeBootstrap.Initialize();
            MapRuntimeDiagnosticSnapshot bootstrapDiagnostics =
                runtimeBootstrap.DiagnosticSnapshot;
            Assert.That(bootstrapDiagnostics.Phase, Is.EqualTo(MapLifecyclePhase.Initialized));
            Assert.That(bootstrapDiagnostics.IsFailed, Is.False);
            Assert.That(bootstrapDiagnostics.AuthorityState.RandomGeneratedActive, Is.True);
            Assert.That(bootstrapDiagnostics.AuthorityState.LegacyStaticActive, Is.False);

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

            MapGeneratedObjectPlacementAdapter placementAdapter =
                integrationRoot.GetComponent<MapGeneratedObjectPlacementAdapter>();
            MerchantPlacementService merchantPlacementService =
                integrationRoot.GetComponent<MerchantPlacementService>();
            MonsterPlacementService monsterPlacementService =
                integrationRoot.GetComponent<MonsterPlacementService>();
            AnimalPlacementService animalPlacementService =
                integrationRoot.GetComponent<AnimalPlacementService>();
            Assert.That(placementAdapter, Is.Not.Null, "主场景缺少世界对象随机地图适配器。");
            Assert.That(placementAdapter.MapController, Is.EqualTo(controller));
            Assert.That(placementAdapter.PlacementAnchor, Is.EqualTo(controller.Player));
            Assert.That(placementAdapter.MapAnchoredObjects, Is.Not.Empty,
                "Main Plant 尚未接入随机地图位置。 ");
            Assert.That(merchantPlacementService, Is.Not.Null,
                "主场景缺少 Required Merchant semantic placement owner。");
            Assert.That(merchantPlacementService.Profile.MinPathSteps, Is.EqualTo(8));
            Assert.That(merchantPlacementService.Profile.MaxPathSteps, Is.EqualTo(24));
            Assert.That(merchantPlacementService.Profile.AllowedTerrains,
                Is.EqualTo(MerchantTerrainMask.Grass | MerchantTerrainMask.Path));
            Assert.That(merchantPlacementService.MerchantTarget, Is.Not.Null,
                "主场景未显式连接现有 Merchant gameplay component。");
            IMerchantPlacementTarget merchantTarget =
                merchantPlacementService.MerchantTarget as IMerchantPlacementTarget;
            Assert.That(merchantTarget, Is.Not.Null);
            AssertNotOwnedByTransitionalAdapter(
                placementAdapter,
                merchantTarget.PlacementTransform,
                "Merchant 不得继续由 Transitional authored-offset adapter 定位。");
            Assert.That(monsterPlacementService, Is.Not.Null,
                "主场景缺少 Required Monster semantic placement owner。");
            Assert.That(monsterPlacementService.Profile.Required, Is.True);
            Assert.That(monsterPlacementService.Profile.RequiredMinimum, Is.EqualTo(1));
            Assert.That(monsterPlacementService.Profile.TargetCount, Is.EqualTo(1));
            Assert.That(monsterPlacementService.Profile.Maximum, Is.EqualTo(1));
            Assert.That(monsterPlacementService.Profile.MinPathSteps, Is.EqualTo(10));
            Assert.That(monsterPlacementService.Profile.MaxPathSteps, Is.EqualTo(28));
            Assert.That(monsterPlacementService.Profile.AllowedTerrains, Is.EqualTo(
                MonsterTerrainMask.Grass |
                MonsterTerrainMask.Path |
                MonsterTerrainMask.Forest |
                MonsterTerrainMask.Sand));
            Assert.That(monsterPlacementService.MonsterTarget.GetType().Name,
                Is.EqualTo("MonsterAIController"));
            IMonsterPlacementTarget monsterTarget =
                monsterPlacementService.MonsterTarget as IMonsterPlacementTarget;
            Assert.That(monsterTarget, Is.Not.Null);
            AssertNotOwnedByTransitionalAdapter(
                placementAdapter,
                monsterTarget.PlacementTransform,
                "Monster 不得继续由 Transitional authored-offset adapter 定位。");
            CollectionAssert.DoesNotContain(
                new List<GameObject>(placementAdapter.RestartAfterPlacement),
                monsterTarget.ReinitializationTarget,
                "Monster reinitialization ownership 不得继续来自 Transitional adapter。");
            Assert.That(animalPlacementService, Is.Not.Null,
                "主场景缺少 Optional Animal semantic placement owner。");
            Assert.That(animalPlacementService.Profile.Required, Is.False);
            Assert.That(animalPlacementService.Profile.RequiredMinimum, Is.Zero);
            Assert.That(animalPlacementService.Profile.TargetCount, Is.EqualTo(4));
            Assert.That(animalPlacementService.Profile.Maximum, Is.EqualTo(4));
            Assert.That(animalPlacementService.Profile.MinPathSteps, Is.Zero);
            Assert.That(animalPlacementService.Profile.MaxPathSteps, Is.EqualTo(int.MaxValue));
            Assert.That(animalPlacementService.Profile.MinimumSpacingSteps, Is.EqualTo(1));
            Assert.That(animalPlacementService.Profile.AllowedTerrains, Is.EqualTo(
                AnimalTerrainMask.Grass |
                AnimalTerrainMask.Path |
                AnimalTerrainMask.Forest |
                AnimalTerrainMask.Sand));
            Assert.That(animalPlacementService.Bindings.Count, Is.EqualTo(4));
            foreach (AnimalPlacementBinding binding in animalPlacementService.Bindings)
            {
                Assert.That(binding.AnimalTransform, Is.Not.Null);
                Assert.That(binding.AnimalTransform.GetComponent("Animal"), Is.Not.Null);
                Assert.That(binding.AnimalTransform.GetComponent<IAnimalPlacementTarget>(), Is.Not.Null);
                AssertNotOwnedByTransitionalAdapter(
                    placementAdapter,
                    binding.AnimalTransform,
                    "Animal 不得继续由 Transitional authored-offset adapter 定位。");
            }

            controller.GenerateMap();
            Assert.That(controller.LastGeneratedMap, Is.Not.Null, "主场景随机地图生成失败。");
            Assert.That(controller.LastGeneratedSimpleDecorations, Is.Not.Null,
                "主场景随机装饰生成失败。");

            Vector3 expectedSpawnPosition = controller.TilemapRenderer.Coordinates.CellToWorld(
                controller.LastGeneratedMap,
                controller.LastGeneratedMap.SpawnCell);
            Assert.That(controller.Player.position.x, Is.EqualTo(expectedSpawnPosition.x).Within(0.01f));
            Assert.That(controller.Player.position.y, Is.EqualTo(expectedSpawnPosition.y).Within(0.01f));
            AssertMerchantPlacement(
                merchantPlacementService,
                merchantTarget,
                controller,
                runtimeBootstrap.Context);
            AssertMonsterPlacement(
                monsterPlacementService,
                monsterTarget,
                controller,
                runtimeBootstrap.Context);
            AssertAnimalPlacement(
                animalPlacementService,
                controller,
                runtimeBootstrap.Context);

            placementAdapter.PlaceObjects(controller.LastGeneratedMap);
            AssertAnchoredObjectsUseGeneratedMap(placementAdapter, controller);
            AssertMerchantPlacement(
                merchantPlacementService,
                merchantTarget,
                controller,
                runtimeBootstrap.Context);
            AssertMonsterPlacement(
                monsterPlacementService,
                monsterTarget,
                controller,
                runtimeBootstrap.Context);
            AssertAnimalPlacement(
                animalPlacementService,
                controller,
                runtimeBootstrap.Context);

            MapData firstGeneratedMap = controller.LastGeneratedMap;
            System.Guid? firstGenerationId = runtimeBootstrap.Context.ActiveGenerationId;
            MapObjectRegistry firstRegistry = runtimeBootstrap.Context.ActiveRegistry;
            controller.RegenerateMap();
            Assert.That(controller.LastGeneratedMap, Is.Not.SameAs(firstGeneratedMap),
                "主场景必须支持通过正式控制器重新生成地图。");
            Assert.That(runtimeBootstrap.Context.ActiveGenerationId, Is.Not.EqualTo(firstGenerationId));
            Assert.That(firstRegistry.IsRetired, Is.True);
            placementAdapter.PlaceObjects(controller.LastGeneratedMap);
            AssertAnchoredObjectsUseGeneratedMap(placementAdapter, controller);
            AssertMerchantPlacement(
                merchantPlacementService,
                merchantTarget,
                controller,
                runtimeBootstrap.Context);
            AssertMonsterPlacement(
                monsterPlacementService,
                monsterTarget,
                controller,
                runtimeBootstrap.Context);
            AssertAnimalPlacement(
                animalPlacementService,
                controller,
                runtimeBootstrap.Context);

            AssertMainCameraRemainsConnected(targetScene, controller.Player);
        }
        finally
        {
            EditorSceneManager.CloseScene(targetScene, true);
        }
    }

    private static void AssertMerchantPlacement(
        MerchantPlacementService service,
        IMerchantPlacementTarget target,
        MapGenerationController controller,
        MapRuntimeContext context)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.ShortestPathSteps, Is.InRange(8, 24));
        Assert.That(target.BoundPlayer, Is.EqualTo(controller.Player));
        Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
            controller.LastGeneratedMap,
            target.PlacementTransform.position,
            out Vector2Int merchantCell), Is.True);
        Assert.That(merchantCell, Is.EqualTo(service.PlacementCell));
        Assert.That(controller.LastGeneratedMap.IsWalkable(merchantCell), Is.True);
        Assert.That(service.Profile.AllowsTerrain(
            controller.LastGeneratedMap.GetCell(merchantCell).terrainType), Is.True);
        Assert.That(Mathf.Abs(service.PlacementCell.x - service.InteractionApproachCell.x) +
                    Mathf.Abs(service.PlacementCell.y - service.InteractionApproachCell.y), Is.EqualTo(1));
        Assert.That(context.ActiveRegistry.TryGet(
            MerchantPlacementService.MainMerchantLogicalObjectId,
            out MapObjectRegistryEntry merchantEntry), Is.True);
        Assert.That(merchantEntry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(merchantEntry.Instance, Is.EqualTo(service.MerchantTarget));
        Assert.That(context.ActiveRegistry.TryGet(
            MerchantPlacementService.InteractionApproachLogicalObjectId,
            out MapObjectRegistryEntry approachEntry), Is.True);
        Assert.That(approachEntry.InitialCell, Is.EqualTo(service.InteractionApproachCell));

        PropertyInfo shopCatalog = service.MerchantTarget.GetType().GetProperty("ShopCatalog");
        FieldInfo interactDistance = service.MerchantTarget.GetType().GetField(
            "interactDistance", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(shopCatalog, Is.Not.Null,
            "Stage 5A 必须复用 Main Merchant Shop gameplay。");
        Assert.That(shopCatalog.GetValue(service.MerchantTarget), Is.Not.Null);
        Assert.That(interactDistance, Is.Not.Null);
        Assert.That((float)interactDistance.GetValue(service.MerchantTarget), Is.GreaterThan(0f));
    }

    private static void AssertMonsterPlacement(
        MonsterPlacementService service,
        IMonsterPlacementTarget target,
        MapGenerationController controller,
        MapRuntimeContext context)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.ShortestPathSteps, Is.InRange(10, 28));
        Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
            controller.LastGeneratedMap,
            target.PlacementTransform.position,
            out Vector2Int monsterCell), Is.True);
        Assert.That(monsterCell, Is.EqualTo(service.PlacementCell));
        Assert.That(controller.LastGeneratedMap.IsWalkable(monsterCell), Is.True);
        Assert.That(service.Profile.AllowsTerrain(
            controller.LastGeneratedMap.GetCell(monsterCell).terrainType), Is.True);
        Assert.That(context.ActiveRegistry.TryGet(
            MonsterPlacementService.MainMonsterLogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(entry.Instance, Is.EqualTo(service.MonsterTarget));
        Assert.That(entry.InitialCell, Is.EqualTo(service.PlacementCell));
        Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        Assert.That(service.MonsterTarget.GetComponent("MonsterMovementController"), Is.Not.Null);
        Assert.That(service.MonsterTarget.GetComponent("MonsterPerceptionController"), Is.Not.Null);
        Assert.That(service.MonsterTarget.GetComponent("MonsterAttackController"), Is.Not.Null);
        Assert.That(service.MonsterTarget.GetComponent("BaseMonster"), Is.Not.Null);
    }

    private static void AssertAnimalPlacement(
        AnimalPlacementService service,
        MapGenerationController controller,
        MapRuntimeContext context)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.PlannedCount, Is.EqualTo(4));
        Assert.That(service.MaterializedCount, Is.EqualTo(4));
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        foreach (AnimalPlacementBinding binding in service.Bindings)
        {
            Assert.That(service.TryGetMaterializedCell(
                binding.LogicalObjectId,
                out Vector2Int animalCell), Is.True);
            Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
                controller.LastGeneratedMap,
                binding.AnimalTransform.position,
                out Vector2Int worldCell), Is.True);
            Assert.That(worldCell, Is.EqualTo(animalCell));
            Assert.That(controller.LastGeneratedMap.IsWalkable(animalCell), Is.True);
            Assert.That(service.Profile.AllowsTerrain(
                controller.LastGeneratedMap.GetCell(animalCell).terrainType), Is.True);
            Assert.That(occupiedCells.Add(animalCell), Is.True);
            Assert.That(context.ActiveRegistry.TryGet(
                binding.LogicalObjectId,
                out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
            Assert.That(entry.Instance, Is.EqualTo(
                binding.AnimalTransform.GetComponent<IAnimalPlacementTarget>() as UnityEngine.Object));
            Assert.That(entry.InitialCell, Is.EqualTo(animalCell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));
        }
    }

    /// <summary>
    /// 验证尚未迁移的 Main Plant 已落到随机地图的独立可行走单元。
    /// </summary>
    private static void AssertAnchoredObjectsUseGeneratedMap(
        MapGeneratedObjectPlacementAdapter adapter,
        MapGenerationController controller)
    {
        MapData mapData = controller.LastGeneratedMap;
        MapSimpleDecorationData decorations = controller.LastGeneratedSimpleDecorations;
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        foreach (Transform target in adapter.MapAnchoredObjects)
        {
            Assert.That(target, Is.Not.Null);
            bool isInside = controller.TilemapRenderer.Coordinates.TryWorldToCell(
                mapData,
                target.position,
                out Vector2Int cell);

            Assert.That(isInside, Is.True,
                $"{target.name} 位于随机地图范围外。");
            Assert.That(mapData.IsWalkable(cell), Is.True,
                $"{target.name} 没有落在可行走地形上。");
            Assert.That(decorations == null || !decorations.IsOccupied(cell), Is.True,
                $"{target.name} 与随机装饰碰撞单元重叠。");
            Assert.That(cell, Is.Not.EqualTo(mapData.SpawnCell));
            Assert.That(cell, Is.Not.EqualTo(mapData.ExitCell));
            Assert.That(occupiedCells.Add(cell), Is.True,
                $"多个 Main 场景对象占用了同一随机地图单元 {cell}。");

            Assert.That(target.GetComponent("MonsterAIController"), Is.Null,
                "Monster 已迁移到 semantic placement，不得残留在 Transitional adapter。");
            Assert.That(target.GetComponent("Animal"), Is.Null,
                "Animal 已迁移到 semantic placement，不得残留在 Transitional adapter。");
        }
    }

    private static void AssertNotOwnedByTransitionalAdapter(
        MapGeneratedObjectPlacementAdapter adapter,
        Transform semanticTarget,
        string message)
    {
        Assert.That(semanticTarget, Is.Not.Null);
        int semanticTargetInstanceId = semanticTarget.GetInstanceID();
        foreach (Transform transitionalTarget in adapter.MapAnchoredObjects)
        {
            Assert.That(transitionalTarget, Is.Not.Null);
            Assert.That(
                transitionalTarget.GetInstanceID(),
                Is.Not.EqualTo(semanticTargetInstanceId),
                message);
        }
    }

    /// <summary>
    /// 验证仍然使用 Main 原有摄像机链路，且没有引入测试摄像机。
    /// </summary>
    private static void AssertMainCameraRemainsConnected(
        Scene scene,
        Transform player)
    {
        int cameraCount = 0;
        bool followsMainPlayer = false;

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            cameraCount += rootObject.GetComponentsInChildren<Camera>(true).Length;
            Assert.That(
                rootObject.GetComponentInChildren<MapGenerationCameraFollow>(true),
                Is.Null,
                "正式主场景不应复制 MapGeneration 测试摄像机。");

            foreach (Component component in rootObject.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "CinemachineVirtualCamera")
                    continue;

                SerializedObject serializedCamera = new SerializedObject(component);
                SerializedProperty followProperty = serializedCamera.FindProperty("m_Follow");
                Transform followTarget = followProperty != null
                    ? followProperty.objectReferenceValue as Transform
                    : null;
                if (followTarget == player ||
                    (followTarget != null && followTarget.IsChildOf(player)))
                {
                    followsMainPlayer = true;
                }
            }
        }

        Assert.That(cameraCount, Is.EqualTo(1),
            "正式主场景应继续只使用 Main 原有 Camera。");
        Assert.That(followsMainPlayer, Is.True,
            "Main 原有 Cinemachine Virtual Camera 应继续跟随 RPG 玩家。");
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
