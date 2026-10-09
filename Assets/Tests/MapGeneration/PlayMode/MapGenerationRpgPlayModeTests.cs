using System.Collections;
using System.Reflection;
using Cinemachine;
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
        CanonicalPlayerProvider playerProvider =
            runtimeRoot.GetComponent<CanonicalPlayerProvider>();
        PlayerSpawnService playerSpawnService =
            runtimeRoot.GetComponent<PlayerSpawnService>();
        MapDependentReinitializationService reinitializationService =
            runtimeRoot.GetComponent<MapDependentReinitializationService>();
        PlayerFollowCameraProvider cameraProvider =
            runtimeRoot.GetComponent<PlayerFollowCameraProvider>();
        CameraBindingService cameraBindingService =
            runtimeRoot.GetComponent<CameraBindingService>();
        MerchantPlacementService merchantPlacementService =
            runtimeRoot.GetComponent<MerchantPlacementService>();
        MonsterPlacementService monsterPlacementService =
            runtimeRoot.GetComponent<MonsterPlacementService>();
        AnimalPlacementService animalPlacementService =
            runtimeRoot.GetComponent<AnimalPlacementService>();
        PlantPlacementService plantPlacementService =
            runtimeRoot.GetComponent<PlantPlacementService>();
        DestructiblePlacementService destructiblePlacementService =
            runtimeRoot.GetComponent<DestructiblePlacementService>();
        MapGeneratedObjectPlacementAdapter placementAdapter =
            runtimeRoot.GetComponent<MapGeneratedObjectPlacementAdapter>();
        MapMinimapController minimap =
            runtimeRoot.GetComponentInChildren<MapMinimapController>(true);

        Assert.That(controller, Is.Not.Null);
        Assert.That(runtimeBootstrap, Is.Not.Null);
        Assert.That(playerProvider, Is.Not.Null);
        Assert.That(playerSpawnService, Is.Not.Null);
        Assert.That(reinitializationService, Is.Not.Null);
        Assert.That(cameraProvider, Is.Not.Null);
        Assert.That(cameraBindingService, Is.Not.Null);
        Assert.That(merchantPlacementService, Is.Not.Null);
        Assert.That(monsterPlacementService, Is.Not.Null);
        Assert.That(animalPlacementService, Is.Not.Null);
        Assert.That(plantPlacementService, Is.Not.Null);
        Assert.That(destructiblePlacementService, Is.Not.Null);
        MapRuntimeDiagnosticSnapshot bootstrapDiagnostics =
            runtimeBootstrap.DiagnosticSnapshot;
        Assert.That(bootstrapDiagnostics.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
        Assert.That(bootstrapDiagnostics.Phase, Is.EqualTo(MapLifecyclePhase.BindingCamera));
        Assert.That(bootstrapDiagnostics.IsFailed, Is.False);
        Assert.That(bootstrapDiagnostics.AuthorityState.RandomGeneratedActive, Is.True);
        Assert.That(bootstrapDiagnostics.AuthorityState.LegacyStaticActive, Is.False);
        Assert.That(controller.LastGeneratedMap, Is.Not.Null,
            "Main 场景进入 Play Mode 后应自动生成随机地图。");
        Assert.That(placementAdapter, Is.Not.Null);
        Assert.That(minimap, Is.Not.Null);
        Assert.That(playerSpawnService.SpawnCount, Is.EqualTo(1));
        Assert.That(playerSpawnService.ReadyGenerationId, Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(reinitializationService.ReinitializationCount, Is.EqualTo(1));
        Assert.That(reinitializationService.ReadyGenerationId, Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(cameraBindingService.BindingCount, Is.EqualTo(1));
        Assert.That(cameraBindingService.TrackingRefreshCount, Is.EqualTo(1));
        Assert.That(cameraBindingService.ReadyGenerationId, Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(merchantPlacementService.MaterializationCount, Is.EqualTo(1));
        Assert.That(merchantPlacementService.ReadyGenerationId,
            Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(monsterPlacementService.MaterializationCount, Is.EqualTo(1));
        Assert.That(monsterPlacementService.ReadyGenerationId,
            Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(animalPlacementService.MaterializationCount, Is.EqualTo(1));
        Assert.That(animalPlacementService.ReadyGenerationId,
            Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(plantPlacementService.MaterializationCount, Is.EqualTo(1));
        Assert.That(plantPlacementService.ReadyGenerationId,
            Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(destructiblePlacementService.MaterializationCount, Is.EqualTo(1));
        Assert.That(destructiblePlacementService.ReadyGenerationId,
            Is.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Transform canonicalPlayer = controller.Player;
        Assert.That(canonicalPlayer, Is.Not.Null);
        Component damageController = canonicalPlayer.GetComponent("PlayerDamageController");
        Component inventory = canonicalPlayer.GetComponent("PlayerInventory");
        Assert.That(damageController, Is.Not.Null);
        Assert.That(inventory, Is.Not.Null);
        float healthBeforeRegeneration = GetFloatProperty(damageController, "CurrentHealth");
        int inventoryStacksBeforeRegeneration = GetCollectionCount(inventory, "ItemStacks");
        Assert.That(minimap.Player, Is.EqualTo(controller.Player));
        Assert.That(GameObject.Find("GeneratedMinimapCanvas"), Is.Not.Null,
            "正式场景应创建连接随机地图数据的小地图视图。");

        AssertPlayerAtSpawn(controller);
        AssertGeneratedCollision(controller.TilemapRenderer);
        AssertTransitionalObjectsPlaced(placementAdapter, controller);
        AssertMainCameraFollowsPlayer(controller.Player);
        AssertCurrentCameraBinding(controller, cameraProvider, cameraBindingService);
        AssertCurrentMerchantPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            merchantPlacementService);
        AssertCurrentMonsterPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            monsterPlacementService);
        AssertCurrentAnimalPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            animalPlacementService);
        AssertCurrentPlantPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            plantPlacementService);
        AssertCurrentDestructiblePlacement(
            controller,
            runtimeBootstrap.Context,
            destructiblePlacementService);

        MapData firstMap = controller.LastGeneratedMap;
        MapObjectRegistry firstRegistry = runtimeBootstrap.Context.ActiveRegistry;
        UnityEngine.Object monsterInstance = monsterPlacementService.MonsterTarget;
        UnityEngine.Object[] animalInstances = new UnityEngine.Object[animalPlacementService.Bindings.Count];
        for (int index = 0; index < animalInstances.Length; index++)
        {
            animalInstances[index] = animalPlacementService.Bindings[index].AnimalTransform
                .GetComponent<IAnimalPlacementTarget>() as UnityEngine.Object;
        }
        UnityEngine.Object[] plantInstances = new UnityEngine.Object[plantPlacementService.Bindings.Count];
        for (int index = 0; index < plantInstances.Length; index++)
        {
            plantInstances[index] = plantPlacementService.Bindings[index].PlantTransform
                .GetComponent<IPlantPlacementTarget>() as UnityEngine.Object;
        }
        Component persistentPlant = plantPlacementService.Bindings[0].PlantTransform.GetComponent("Plant");
        Assert.That(persistentPlant, Is.Not.Null);
        MethodInfo takeDamage = persistentPlant.GetType().GetMethod(
            "TakeDamage",
            new[] { typeof(float) });
        Assert.That(takeDamage, Is.Not.Null);
        takeDamage.Invoke(persistentPlant, new object[] { 1f });
        float plantHealthBeforeRegeneration = GetFloatProperty(persistentPlant, "CurrentHealth");
        bool plantDefeatedBeforeRegeneration = GetBoolProperty(persistentPlant, "IsDefeated");
        DestructiblePlacementResult defeatedPlacement =
            destructiblePlacementService.LastPopulationPlan.Placements[0];
        Assert.That(destructiblePlacementService.TryGetActiveInstance(
            defeatedPlacement.LogicalObjectId,
            out UnityEngine.Object destructibleInstance), Is.True);
        Component destructibleDamageable =
            ((Component)destructibleInstance).GetComponent("BaseDamageable");
        Assert.That(destructibleDamageable, Is.Not.Null);
        MethodInfo destructibleTakeDamage = destructibleDamageable.GetType().GetMethod(
            "TakeDamage",
            new[] { typeof(float) });
        Assert.That(destructibleTakeDamage, Is.Not.Null);
        float destructibleMaxHealth = GetFloatProperty(destructibleDamageable, "MaxHealth");
        destructibleTakeDamage.Invoke(destructibleDamageable, new object[] { destructibleMaxHealth });
        yield return null;
        Assert.That(destructiblePlacementService.ActiveCount,
            Is.EqualTo(destructiblePlacementService.LastPopulationPlan.ActualCount - 1));
        Assert.That(runtimeBootstrap.Context.ActiveRegistry.TryGet(
            defeatedPlacement.LogicalObjectId, out _), Is.False);
        controller.RegenerateMap();
        yield return null;

        Assert.That(controller.LastGeneratedMap, Is.Not.SameAs(firstMap));
        Assert.That(controller.Player, Is.SameAs(canonicalPlayer));
        Assert.That(playerSpawnService.SpawnCount, Is.EqualTo(2));
        MapRuntimeDiagnosticSnapshot regeneratedDiagnostics = runtimeBootstrap.DiagnosticSnapshot;
        Assert.That(regeneratedDiagnostics.Phase, Is.EqualTo(MapLifecyclePhase.BindingCamera));
        Assert.That(regeneratedDiagnostics.IsFailed, Is.False);
        Assert.That(reinitializationService.ReinitializationCount, Is.EqualTo(2));
        Assert.That(reinitializationService.ReadyGenerationId, Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(cameraBindingService.BindingCount, Is.EqualTo(2));
        Assert.That(cameraBindingService.TrackingRefreshCount, Is.EqualTo(2));
        Assert.That(cameraBindingService.ReadyGenerationId, Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(merchantPlacementService.MaterializationCount, Is.EqualTo(2));
        Assert.That(merchantPlacementService.ReadyGenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(monsterPlacementService.MaterializationCount, Is.EqualTo(2));
        Assert.That(monsterPlacementService.ReadyGenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(animalPlacementService.MaterializationCount, Is.EqualTo(2));
        Assert.That(animalPlacementService.ReadyGenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(plantPlacementService.MaterializationCount, Is.EqualTo(2));
        Assert.That(plantPlacementService.ReadyGenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(destructiblePlacementService.MaterializationCount, Is.EqualTo(2));
        Assert.That(destructiblePlacementService.ReadyGenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(firstRegistry.IsRetired, Is.True);
        Assert.That(destructiblePlacementService.ActiveCount,
            Is.EqualTo(destructiblePlacementService.LastPopulationPlan.ActualCount));
        Assert.That(runtimeBootstrap.Context.ActiveRegistry.TryGet(
            defeatedPlacement.LogicalObjectId,
            out MapObjectRegistryEntry regeneratedDestructibleEntry), Is.True);
        Assert.That(regeneratedDestructibleEntry.GenerationId,
            Is.EqualTo(regeneratedDiagnostics.ActiveGenerationId));
        Assert.That(regeneratedDiagnostics.ActiveGenerationId,
            Is.Not.EqualTo(bootstrapDiagnostics.ActiveGenerationId));
        Assert.That(GetFloatProperty(damageController, "CurrentHealth"), Is.EqualTo(healthBeforeRegeneration));
        Assert.That(GetCollectionCount(inventory, "ItemStacks"), Is.EqualTo(inventoryStacksBeforeRegeneration));
        Assert.That(GetFloatProperty(persistentPlant, "CurrentHealth"),
            Is.EqualTo(plantHealthBeforeRegeneration));
        Assert.That(GetBoolProperty(persistentPlant, "IsDefeated"),
            Is.EqualTo(plantDefeatedBeforeRegeneration));
        Transform persistentPlantVisual = persistentPlant.transform.Find("Visual");
        Assert.That(persistentPlantVisual, Is.Not.Null);
        Assert.That(persistentPlantVisual.gameObject.activeSelf, Is.True,
            "Plant map reinitialization must cancel transient damage flash without resetting health.");
        AssertPlayerAtSpawn(controller);
        AssertTransitionalObjectsPlaced(placementAdapter, controller);
        AssertCurrentCameraBinding(controller, cameraProvider, cameraBindingService);
        AssertCurrentMerchantPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            merchantPlacementService);
        AssertCurrentMonsterPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            monsterPlacementService);
        AssertCurrentAnimalPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            animalPlacementService);
        AssertCurrentPlantPlacement(
            controller,
            runtimeBootstrap.Context,
            placementAdapter,
            plantPlacementService);
        AssertCurrentDestructiblePlacement(
            controller,
            runtimeBootstrap.Context,
            destructiblePlacementService);
        Assert.That(monsterPlacementService.MonsterTarget, Is.SameAs(monsterInstance));
        for (int index = 0; index < animalInstances.Length; index++)
        {
            Assert.That(
                animalPlacementService.Bindings[index].AnimalTransform
                    .GetComponent<IAnimalPlacementTarget>(),
                Is.SameAs(animalInstances[index]));
        }
        for (int index = 0; index < plantInstances.Length; index++)
        {
            Assert.That(
                plantPlacementService.Bindings[index].PlantTransform
                    .GetComponent<IPlantPlacementTarget>(),
                Is.SameAs(plantInstances[index]));
        }
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
    /// 验证已迁移的 production Roles 不再由 Transitional adapter 定位。
    /// </summary>
    private static void AssertTransitionalObjectsPlaced(
        MapGeneratedObjectPlacementAdapter adapter,
        MapGenerationController controller)
    {
        Assert.That(adapter.MapAnchoredObjects, Is.Empty,
            "Stage 5D 后 Transitional production role count 必须为零。");
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

            Assert.That(target.GetComponent("MonsterAIController"), Is.Null,
                "Monster 不得继续由 Transitional authored-offset adapter 定位。");
            Assert.That(target.GetComponent("Animal"), Is.Null,
                "Animal 不得继续由 Transitional authored-offset adapter 定位。");
            Assert.That(target.GetComponent("Plant"), Is.Null,
                "Plant 不得继续由 Transitional authored-offset adapter 定位。");
        }
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

    private static void AssertCurrentCameraBinding(
        MapGenerationController controller,
        PlayerFollowCameraProvider provider,
        CameraBindingService service)
    {
        Assert.That(provider.TryResolve(out PlayerFollowCameraBinding binding, out string reason),
            Is.True, reason);
        Transform cameraTarget = controller.Player.Find("CameraTarget");
        Assert.That(cameraTarget, Is.Not.Null);
        Assert.That(binding.VirtualCamera.Follow, Is.EqualTo(cameraTarget));
        Assert.That(service.ReadyFollowTarget, Is.EqualTo(cameraTarget));
        Assert.That(controller.TilemapRenderer.Coordinates.TryCellBoundsToWorld(
            controller.LastGeneratedMap, out Bounds expectedBounds), Is.True);
        AssertBounds(service.AppliedWorldBounds, expectedBounds);
        AssertBounds(binding.BoundsCollider.bounds, expectedBounds);
        Assert.That(binding.VirtualCamera.PreviousStateIsValid, Is.True,
            "After one rendered frame, Cinemachine must have consumed the reset state for the Current Generation.");
    }

    private static void AssertCurrentMerchantPlacement(
        MapGenerationController controller,
        MapRuntimeContext context,
        MapGeneratedObjectPlacementAdapter placementAdapter,
        MerchantPlacementService service)
    {
        Assert.That(service.MerchantTarget, Is.Not.Null);
        IMerchantPlacementTarget target = service.MerchantTarget as IMerchantPlacementTarget;
        Assert.That(target, Is.Not.Null);
        Assert.That(target.BoundPlayer, Is.EqualTo(controller.Player));
        Assert.That(service.ShortestPathSteps, Is.InRange(8, 24));
        Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
            controller.LastGeneratedMap,
            target.PlacementTransform.position,
            out Vector2Int merchantCell), Is.True);
        Assert.That(merchantCell, Is.EqualTo(service.PlacementCell));
        Assert.That(controller.LastGeneratedMap.IsWalkable(merchantCell), Is.True);
        Assert.That(service.Profile.AllowsTerrain(
            controller.LastGeneratedMap.GetCell(merchantCell).terrainType), Is.True);
        CollectionAssert.DoesNotContain(
            new System.Collections.Generic.List<Transform>(placementAdapter.MapAnchoredObjects),
            target.PlacementTransform,
            "Merchant must not remain in Transitional authored-offset positioning.");
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
        FieldInfo interactionDistance = service.MerchantTarget.GetType().GetField(
            "interactDistance", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(shopCatalog, Is.Not.Null);
        Assert.That(shopCatalog.GetValue(service.MerchantTarget), Is.Not.Null,
            "Main Merchant shop catalog must remain connected.");
        Assert.That(interactionDistance, Is.Not.Null);
        Assert.That((float)interactionDistance.GetValue(service.MerchantTarget),
            Is.GreaterThan(0f), "Main Merchant interaction baseline must remain configured.");
    }

    private static void AssertCurrentMonsterPlacement(
        MapGenerationController controller,
        MapRuntimeContext context,
        MapGeneratedObjectPlacementAdapter placementAdapter,
        MonsterPlacementService service)
    {
        Assert.That(service.MonsterTarget.GetType().Name, Is.EqualTo("MonsterAIController"));
        MonoBehaviour monsterAi = service.MonsterTarget;
        Assert.That(service.Profile.Required, Is.True);
        Assert.That(service.Profile.RequiredMinimum, Is.EqualTo(1));
        Assert.That(service.Profile.TargetCount, Is.EqualTo(1));
        Assert.That(service.Profile.Maximum, Is.EqualTo(1));
        Assert.That(service.Profile.MinPathSteps, Is.EqualTo(10));
        Assert.That(service.Profile.MaxPathSteps, Is.EqualTo(28));
        Assert.That(service.ShortestPathSteps, Is.InRange(10, 28));
        Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
            controller.LastGeneratedMap,
            monsterAi.transform.position,
            out Vector2Int monsterCell), Is.True);
        Assert.That(controller.LastGeneratedMap.IsWalkable(monsterCell), Is.True);
        Assert.That(service.Profile.AllowsTerrain(
            controller.LastGeneratedMap.GetCell(monsterCell).terrainType), Is.True);
        int monsterTransformInstanceId = monsterAi.transform.GetInstanceID();
        foreach (Transform transitionalTarget in placementAdapter.MapAnchoredObjects)
        {
            Assert.That(transitionalTarget, Is.Not.Null);
            Assert.That(
                transitionalTarget.GetInstanceID(),
                Is.Not.EqualTo(monsterTransformInstanceId),
                "Monster must not remain in Transitional authored-offset positioning.");
        }
        CollectionAssert.DoesNotContain(
            new System.Collections.Generic.List<GameObject>(placementAdapter.RestartAfterPlacement),
            monsterAi.gameObject,
            "Monster reinitialization must be owned by its semantic placement service.");
        Assert.That(context.ActiveRegistry.TryGet(
            MonsterPlacementService.MainMonsterLogicalObjectId,
            out MapObjectRegistryEntry entry), Is.True);
        Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(entry.Instance, Is.EqualTo(service.MonsterTarget));
        Assert.That(entry.InitialCell, Is.EqualTo(service.PlacementCell));
        Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));

        PropertyInfo homePositionProperty = monsterAi.GetType().GetProperty(
            "HomePosition",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(homePositionProperty, Is.Not.Null);
        Vector2 homePosition = (Vector2)homePositionProperty.GetValue(monsterAi);
        Vector3 expectedHome = controller.TilemapRenderer.Coordinates.CellToWorld(
            controller.LastGeneratedMap,
            service.PlacementCell);
        Assert.That(homePosition.x, Is.EqualTo(expectedHome.x).Within(0.01f));
        Assert.That(homePosition.y, Is.EqualTo(expectedHome.y).Within(0.01f));
        PropertyInfo currentTargetProperty = monsterAi.GetType().GetProperty("CurrentTarget");
        PropertyInfo currentStateProperty = monsterAi.GetType().GetProperty("CurrentStateType");
        Assert.That(currentTargetProperty, Is.Not.Null);
        Assert.That(currentTargetProperty.GetValue(monsterAi), Is.Null);
        Assert.That(currentStateProperty, Is.Not.Null);
        Assert.That(currentStateProperty.GetValue(monsterAi).ToString(), Is.EqualTo("Patrol"));
        Assert.That(monsterAi.GetComponent("MonsterMovementController"), Is.Not.Null);
        Assert.That(monsterAi.GetComponent("MonsterPerceptionController"), Is.Not.Null);
        Assert.That(monsterAi.GetComponent("MonsterAttackController"), Is.Not.Null);
        Assert.That(monsterAi.GetComponent("BaseMonster"), Is.Not.Null);
    }

    private static void AssertCurrentAnimalPlacement(
        MapGenerationController controller,
        MapRuntimeContext context,
        MapGeneratedObjectPlacementAdapter placementAdapter,
        AnimalPlacementService service)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.PlannedCount, Is.EqualTo(4));
        Assert.That(service.MaterializedCount, Is.EqualTo(4));
        Assert.That(service.Profile.Required, Is.False);
        Assert.That(service.Profile.RequiredMinimum, Is.Zero);
        Assert.That(service.Profile.TargetCount, Is.EqualTo(4));
        Assert.That(service.Profile.Maximum, Is.EqualTo(4));

        System.Collections.Generic.HashSet<Vector2Int> occupied =
            new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (AnimalPlacementBinding binding in service.Bindings)
        {
            Assert.That(binding.AnimalTransform, Is.Not.Null);
            Assert.That(binding.AnimalTransform.gameObject.activeInHierarchy, Is.True);
            IAnimalPlacementTarget target =
                binding.AnimalTransform.GetComponent<IAnimalPlacementTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.ReinitializationTarget.GetComponent("Animal"), Is.Not.Null);
            Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
                controller.LastGeneratedMap,
                target.PlacementTransform.position,
                out Vector2Int animalCell), Is.True);
            Assert.That(service.TryGetMaterializedCell(
                binding.LogicalObjectId,
                out Vector2Int committedCell), Is.True);
            Assert.That(animalCell, Is.EqualTo(committedCell));
            Assert.That(controller.LastGeneratedMap.IsWalkable(animalCell), Is.True);
            Assert.That(service.Profile.AllowsTerrain(
                controller.LastGeneratedMap.GetCell(animalCell).terrainType), Is.True);
            Assert.That(occupied.Add(animalCell), Is.True);
            Assert.That(context.ActiveRegistry.TryGet(
                binding.LogicalObjectId,
                out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
            Assert.That(entry.Instance, Is.SameAs(target as UnityEngine.Object));
            Assert.That(entry.InitialCell, Is.EqualTo(animalCell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));

            foreach (Transform transitionalTarget in placementAdapter.MapAnchoredObjects)
            {
                Assert.That(transitionalTarget, Is.Not.Null);
                Assert.That(transitionalTarget, Is.Not.SameAs(target.PlacementTransform),
                    "Animal must not remain in Transitional authored-offset positioning.");
            }
        }
    }

    private static void AssertCurrentDestructiblePlacement(
        MapGenerationController controller,
        MapRuntimeContext context,
        DestructiblePlacementService service)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.Profile.RequiredMinimum, Is.EqualTo(4));
        Assert.That(service.Profile.TargetCount, Is.EqualTo(8));
        Assert.That(service.Profile.Maximum, Is.EqualTo(8));
        Assert.That(service.Profile.MinPathSteps, Is.EqualTo(8));
        Assert.That(service.Profile.AllowedTerrains,
            Is.EqualTo(DestructibleTerrainMask.Grass | DestructibleTerrainMask.Forest));
        Assert.That(service.LastPopulationPlan.ActualCount,
            Is.GreaterThanOrEqualTo(service.Profile.RequiredMinimum));
        Assert.That(service.LastPopulationPlan.ActualCount,
            Is.LessThanOrEqualTo(service.Profile.TargetCount));
        foreach (DestructiblePlacementResult placement in
                 service.LastPopulationPlan.Placements)
        {
            Assert.That(service.TryGetActiveInstance(
                placement.LogicalObjectId,
                out UnityEngine.Object instance), Is.True);
            Component damageable = ((Component)instance).GetComponent("BaseDamageable");
            Assert.That(damageable, Is.Not.Null);
            Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
                controller.LastGeneratedMap,
                ((Component)instance).transform.position,
                out Vector2Int cell), Is.True);
            Assert.That(cell, Is.EqualTo(placement.PlacementCell));
            Assert.That(controller.LastGeneratedMap.IsWalkable(cell), Is.True);
            Assert.That(service.Profile.AllowsTerrain(
                controller.LastGeneratedMap.GetCell(cell).terrainType), Is.True);
            Assert.That(context.ActiveRegistry.TryGet(
                placement.LogicalObjectId,
                out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
            Assert.That(entry.Instance, Is.SameAs(instance));
            Assert.That(entry.InitialCell, Is.EqualTo(cell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.GenerationScoped));
        }
    }

    private static void AssertCurrentPlantPlacement(
        MapGenerationController controller,
        MapRuntimeContext context,
        MapGeneratedObjectPlacementAdapter placementAdapter,
        PlantPlacementService service)
    {
        Assert.That(service.ReadyGenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(service.PlannedCount, Is.EqualTo(17));
        Assert.That(service.MaterializedCount, Is.EqualTo(17));
        Assert.That(service.Profile.Required, Is.False);
        Assert.That(service.Profile.RequiredMinimum, Is.Zero);
        Assert.That(service.Profile.TargetCount, Is.EqualTo(17));
        Assert.That(service.Profile.Maximum, Is.EqualTo(17));

        System.Collections.Generic.HashSet<Vector2Int> occupied =
            new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (PlantPlacementBinding binding in service.Bindings)
        {
            Assert.That(binding.PlantTransform, Is.Not.Null);
            Assert.That(binding.PlantTransform.gameObject.activeInHierarchy, Is.True);
            IPlantPlacementTarget target =
                binding.PlantTransform.GetComponent<IPlantPlacementTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.ReinitializationTarget.GetComponent("Plant"), Is.Not.Null);
            Assert.That(target.ReinitializationTarget.GetComponent("PlantBehaviorController"), Is.Not.Null);
            Assert.That(controller.TilemapRenderer.Coordinates.TryWorldToCell(
                controller.LastGeneratedMap,
                target.PlacementTransform.position,
                out Vector2Int plantCell), Is.True);
            Assert.That(service.TryGetMaterializedCell(
                binding.LogicalObjectId,
                out Vector2Int committedCell), Is.True);
            Assert.That(plantCell, Is.EqualTo(committedCell));
            Assert.That(controller.LastGeneratedMap.IsWalkable(plantCell), Is.True);
            Assert.That(service.Profile.AllowsTerrain(
                controller.LastGeneratedMap.GetCell(plantCell).terrainType), Is.True);
            Assert.That(occupied.Add(plantCell), Is.True);
            Assert.That(context.ActiveRegistry.TryGet(
                binding.LogicalObjectId,
                out MapObjectRegistryEntry entry), Is.True);
            Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
            Assert.That(entry.Instance, Is.SameAs(target as UnityEngine.Object));
            Assert.That(entry.InitialCell, Is.EqualTo(plantCell));
            Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.SceneBound));

            foreach (Transform transitionalTarget in placementAdapter.MapAnchoredObjects)
            {
                Assert.That(transitionalTarget, Is.Not.Null);
                Assert.That(transitionalTarget, Is.Not.SameAs(target.PlacementTransform),
                    "Plant must not remain in Transitional authored-offset positioning.");
            }
        }
    }

    private static void AssertBounds(Bounds actual, Bounds expected)
    {
        Assert.That(actual.min.x, Is.EqualTo(expected.min.x).Within(0.01f));
        Assert.That(actual.min.y, Is.EqualTo(expected.min.y).Within(0.01f));
        Assert.That(actual.max.x, Is.EqualTo(expected.max.x).Within(0.01f));
        Assert.That(actual.max.y, Is.EqualTo(expected.max.y).Within(0.01f));
    }

    private static float GetFloatProperty(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} must remain observable.");
        return (float)property.GetValue(component);
    }

    private static bool GetBoolProperty(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} must remain observable.");
        return (bool)property.GetValue(component);
    }

    private static int GetCollectionCount(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} must remain observable.");
        object collection = property.GetValue(component);
        PropertyInfo count = collection?.GetType().GetProperty("Count");
        Assert.That(count, Is.Not.Null, $"{component.GetType().Name}.{propertyName} must expose Count.");
        return (int)count.GetValue(collection);
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
