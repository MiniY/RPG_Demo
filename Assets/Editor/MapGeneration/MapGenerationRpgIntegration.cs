using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 将独立随机地图测试场景中已经验证的运行时地图对象接入 RPG 主场景。
/// </summary>
public static class MapGenerationRpgIntegration
{
    /// <summary>
    /// RPG 正常游戏流程使用的主场景路径。
    /// </summary>
    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";

    /// <summary>
    /// 保存随机地图开发与回归测试对象的场景路径。
    /// </summary>
    private const string DevelopmentScenePath =
        "Assets/Scenes/MapGeneration/MapGenerationTest.unity";

    /// <summary>
    /// RPG 正常游戏流程使用的生产随机地图运行时预制体。
    /// </summary>
    private const string RuntimePrefabPath =
        "Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab";

    /// <summary>
    /// 接入主场景后的随机地图根对象名称。
    /// </summary>
    private const string IntegrationRootName = "RandomMapRuntime";

    /// <summary>
    /// 主场景旧静态地图 Grid（网格）的对象名称。
    /// </summary>
    private const string LegacyGridName = "Grid";

    /// <summary>
    /// 需要从旧静态布局映射到随机地图的 Main 场景组件类型。
    /// </summary>
    private static readonly HashSet<string> MapAnchoredComponentTypeNames =
        new HashSet<string>
        {
            "MonsterAIController",
            "AnimalHurtController",
            "PlantBehaviorController"
        };

    /// <summary>
    /// 移动后需要重新启用、以新位置重建运行时状态的组件类型。
    /// </summary>
    private static readonly HashSet<string> RestartAfterPlacementTypeNames =
        new HashSet<string>
        {
            "MonsterAIController"
        };

    /// <summary>
    /// 从 Unity 菜单执行 RPG 随机地图集成。
    /// </summary>
    [MenuItem("Tools/RPG Demo/Map Generation/Integrate Into RPG Scene")]
    public static void IntegrateFromMenu()
    {
        Integrate();
    }

    /// <summary>
    /// 从开发场景重建生产运行时预制体；正式游戏运行不依赖开发场景。
    /// </summary>
    [MenuItem("Tools/RPG Demo/Map Generation/Rebuild Production Runtime Prefab")]
    public static void RebuildRuntimePrefabFromMenu()
    {
        RebuildRuntimePrefabFromDevelopmentScene();
    }

    /// <summary>
    /// 从 Unity batch mode（批处理模式）执行 RPG 随机地图集成。
    /// </summary>
    public static void RunBatchIntegration()
    {
        try
        {
            Integrate();
            Debug.Log("随机地图已接入 RPG 主场景。");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 一次性重建生产预制体并重新接入 RPG 主场景。
    /// </summary>
    public static void RunBatchProductionMigration()
    {
        try
        {
            RebuildRuntimePrefabFromDevelopmentScene();
            Integrate();
            Debug.Log("生产随机地图预制体已重建并接入 RPG 主场景。");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 把开发场景中已经验证的地图运行时对象固化为生产专用预制体。
    /// </summary>
    private static void RebuildRuntimePrefabFromDevelopmentScene()
    {
        Scene developmentScene = EditorSceneManager.OpenScene(
            DevelopmentScenePath,
            OpenSceneMode.Additive);

        try
        {
            Grid sourceGrid = FindComponentInScene<Grid>(developmentScene);
            MapGenerationController sourceController =
                FindComponentInScene<MapGenerationController>(developmentScene);

            if (sourceGrid == null || sourceController == null)
            {
                throw new InvalidOperationException(
                    "MapGenerationTest 缺少 Grid 或 MapGenerationController。");
            }

            GameObject prefabRoot = new GameObject(IntegrationRootName);
            SceneManager.MoveGameObjectToScene(prefabRoot, developmentScene);
            sourceGrid.transform.SetParent(prefabRoot.transform, true);
            sourceController.transform.SetParent(prefabRoot.transform, true);

            ConfigureController(sourceController, null);
            ConfigureMinimap(
                sourceController.GetComponent<MapMinimapController>(),
                sourceController,
                null);

            MapGeneratedObjectPlacementAdapter placementAdapter =
                prefabRoot.AddComponent<MapGeneratedObjectPlacementAdapter>();
            ConfigurePlacementAdapter(
                placementAdapter,
                sourceController,
                null,
                Array.Empty<Transform>(),
                Array.Empty<GameObject>());

            EnsureRuntimePrefabFolder();
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                RuntimePrefabPath,
                out bool success);

            if (!success || prefabAsset == null)
            {
                throw new InvalidOperationException(
                    $"无法保存生产随机地图预制体：{RuntimePrefabPath}");
            }
        }
        finally
        {
            if (developmentScene.IsValid() && developmentScene.isLoaded)
                EditorSceneManager.CloseScene(developmentScene, true);
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 实例化生产随机地图预制体、绑定 Main 游戏系统并停用旧静态地图。
    /// </summary>
    private static void Integrate()
    {
        // 目标场景是唯一会被保存的场景。
        Scene targetScene = EditorSceneManager.OpenScene(
            TargetScenePath,
            OpenSceneMode.Single);

        // 重复运行迁移时先移除上一次生成的集成根对象，保持操作幂等。
        GameObject previousIntegrationRoot = FindRootObject(
            targetScene,
            IntegrationRootName);
        if (previousIntegrationRoot != null)
            UnityEngine.Object.DestroyImmediate(previousIntegrationRoot);

        // 旧 Grid 只停用、不删除，以便人工测试发现问题时快速回退。
        GameObject legacyGrid = FindRootObject(targetScene, LegacyGridName);
        if (legacyGrid == null || legacyGrid.GetComponent<Grid>() == null)
        {
            throw new InvalidOperationException(
                "SampleScene 缺少名为 Grid 的旧静态地图根对象。");
        }

        // PlayerAction 是当前 RPG 正常流程中实际使用的玩家行为入口。
        Component playerAction = FindComponentByTypeName(targetScene, "PlayerAction");
        if (playerAction == null)
            throw new InvalidOperationException("SampleScene 缺少 PlayerAction 玩家对象。");

        GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            RuntimePrefabPath);
        if (runtimePrefab == null)
        {
            throw new InvalidOperationException(
                $"缺少生产随机地图预制体：{RuntimePrefabPath}");
        }

        GameObject integrationRoot = PrefabUtility.InstantiatePrefab(
            runtimePrefab,
            targetScene) as GameObject;
        if (integrationRoot == null)
            throw new InvalidOperationException("无法实例化生产随机地图预制体。");

        integrationRoot.name = IntegrationRootName;

        MapGenerationController targetController =
            integrationRoot.GetComponentInChildren<MapGenerationController>(true);
        MapMinimapController targetMinimap =
            integrationRoot.GetComponentInChildren<MapMinimapController>(true);
        MapGeneratedObjectPlacementAdapter placementAdapter =
            integrationRoot.GetComponent<MapGeneratedObjectPlacementAdapter>();

        if (targetController == null || targetController.TilemapRenderer == null ||
            placementAdapter == null)
        {
            throw new InvalidOperationException(
                "生产随机地图预制体缺少控制器、渲染器或场景对象适配器。");
        }

        ConfigureController(targetController, playerAction.transform);
        ConfigureMinimap(targetMinimap, targetController, playerAction.transform);
        FindMapAnchoredObjects(
            targetScene,
            integrationRoot.transform,
            out Transform[] mapAnchoredObjects,
            out GameObject[] restartObjects);
        ConfigurePlacementAdapter(
            placementAdapter,
            targetController,
            playerAction.transform,
            mapAnchoredObjects,
            restartObjects);

        legacyGrid.SetActive(false);
        integrationRoot.SetActive(true);
        EditorUtility.SetDirty(legacyGrid);
        EditorUtility.SetDirty(integrationRoot);

        EditorSceneManager.MarkSceneDirty(targetScene);
        if (!EditorSceneManager.SaveScene(targetScene, TargetScenePath))
            throw new InvalidOperationException("无法保存随机地图集成后的 SampleScene。");

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 将随机地图控制器绑定到 RPG 玩家并启用进入场景时自动生成。
    /// </summary>
    /// <param name="controller">目标随机地图控制器。</param>
    /// <param name="player">RPG 主场景中的玩家变换组件。</param>
    private static void ConfigureController(
        MapGenerationController controller,
        Transform player)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectProperty(serializedController, "player", player);
        SetBooleanProperty(serializedController, "generateOnPlay", true);
        SetBooleanProperty(serializedController, "movePlayerToSpawn", true);
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    /// <summary>
    /// 让小地图使用复制后的控制器、RPG 玩家和运行时创建的 UI。
    /// </summary>
    /// <param name="minimap">目标小地图控制器。</param>
    /// <param name="controller">复制后的随机地图控制器。</param>
    /// <param name="player">RPG 主场景中的玩家变换组件。</param>
    private static void ConfigureMinimap(
        MapMinimapController minimap,
        MapGenerationController controller,
        Transform player)
    {
        if (minimap == null)
            return;

        SerializedObject serializedMinimap = new SerializedObject(minimap);
        SetObjectProperty(serializedMinimap, "mapController", controller);
        SetObjectProperty(serializedMinimap, "tilemapRenderer", controller.TilemapRenderer);
        SetObjectProperty(serializedMinimap, "player", player);
        SetObjectProperty(serializedMinimap, "minimapImage", null);
        SetObjectProperty(serializedMinimap, "playerMarker", null);
        SetObjectProperty(serializedMinimap, "fogOverlay", null);
        SetObjectProperty(serializedMinimap, "spawnMarker", null);
        SetObjectProperty(serializedMinimap, "exitMarker", null);
        SetBooleanProperty(serializedMinimap, "createRuntimeView", true);
        serializedMinimap.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(minimap);
    }

    /// <summary>
    /// 绑定生产地图控制器、Main 玩家和需要重新定位的世界对象。
    /// </summary>
    private static void ConfigurePlacementAdapter(
        MapGeneratedObjectPlacementAdapter adapter,
        MapGenerationController controller,
        Transform placementAnchor,
        Transform[] mapAnchoredObjects,
        GameObject[] restartObjects)
    {
        SerializedObject serializedAdapter = new SerializedObject(adapter);
        SetObjectProperty(serializedAdapter, "mapController", controller);
        SetObjectProperty(serializedAdapter, "placementAnchor", placementAnchor);
        SetObjectArrayProperty(
            serializedAdapter,
            "mapAnchoredObjects",
            mapAnchoredObjects);
        SetVector2IntArrayProperty(
            serializedAdapter,
            "authoredCellOffsetValues",
            CalculateAuthoredCellOffsets(
                controller,
                placementAnchor,
                mapAnchoredObjects));
        SetObjectArrayProperty(
            serializedAdapter,
            "restartAfterPlacement",
            restartObjects);
        serializedAdapter.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(adapter);
    }

    /// <summary>
    /// 记录 Main 世界对象相对玩家的原静态地图网格偏移。
    /// </summary>
    private static Vector2Int[] CalculateAuthoredCellOffsets(
        MapGenerationController controller,
        Transform placementAnchor,
        Transform[] mapAnchoredObjects)
    {
        Vector2Int[] offsets = new Vector2Int[mapAnchoredObjects.Length];
        if (controller == null || controller.TilemapRenderer == null ||
            controller.TilemapRenderer.GroundTilemap == null || placementAnchor == null)
        {
            return offsets;
        }

        Vector3Int anchorCell = controller.TilemapRenderer.GroundTilemap.WorldToCell(
            placementAnchor.position);

        for (int index = 0; index < mapAnchoredObjects.Length; index++)
        {
            Transform target = mapAnchoredObjects[index];
            if (target == null)
                continue;

            Vector3Int targetCell = controller.TilemapRenderer.GroundTilemap.WorldToCell(
                target.position);
            offsets[index] = new Vector2Int(
                targetCell.x - anchorCell.x,
                targetCell.y - anchorCell.y);
        }

        return offsets;
    }

    /// <summary>
    /// 查找 Main 场景中需要使用随机地图位置的怪物、动物和植物。
    /// </summary>
    private static void FindMapAnchoredObjects(
        Scene scene,
        Transform integrationRoot,
        out Transform[] mapAnchoredObjects,
        out GameObject[] restartObjects)
    {
        List<Transform> anchoredObjects = new List<Transform>();
        List<GameObject> objectsToRestart = new List<GameObject>();
        HashSet<Transform> seenTransforms = new HashSet<Transform>();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Component component in rootObject.GetComponentsInChildren<Component>(true))
            {
                if (component == null ||
                    component.transform == integrationRoot ||
                    component.transform.IsChildOf(integrationRoot) ||
                    !MapAnchoredComponentTypeNames.Contains(component.GetType().Name) ||
                    !seenTransforms.Add(component.transform))
                {
                    continue;
                }

                anchoredObjects.Add(component.transform);

                if (RestartAfterPlacementTypeNames.Contains(component.GetType().Name))
                    objectsToRestart.Add(component.gameObject);
            }
        }

        anchoredObjects.Sort(CompareTransformsByHierarchyPath);
        objectsToRestart.Sort((left, right) =>
            CompareTransformsByHierarchyPath(left.transform, right.transform));
        mapAnchoredObjects = anchoredObjects.ToArray();
        restartObjects = objectsToRestart.ToArray();
    }

    /// <summary>
    /// 使用稳定层级路径排序，避免迁移结果受对象遍历顺序影响。
    /// </summary>
    private static int CompareTransformsByHierarchyPath(Transform left, Transform right)
    {
        return string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right));
    }

    /// <summary>
    /// 获取 Transform（变换组件）在场景中的完整层级路径。
    /// </summary>
    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    /// <summary>
    /// 确保生产随机地图预制体目录存在。
    /// </summary>
    private static void EnsureRuntimePrefabFolder()
    {
        const string folderPath = "Assets/Prefabs/MapGeneration";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets/Prefabs", "MapGeneration");
    }

    /// <summary>
    /// 在指定场景的根对象中按名称查找对象。
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

    /// <summary>
    /// 在指定场景中按组件类型名称查找第一个组件。
    /// </summary>
    /// <param name="scene">待搜索的场景。</param>
    /// <param name="typeName">组件类型名称。</param>
    /// <returns>找到的组件；不存在时返回 null。</returns>
    private static Component FindComponentByTypeName(Scene scene, string typeName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Component[] components = rootObject.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component != null && component.GetType().Name == typeName)
                    return component;
            }
        }

        return null;
    }

    /// <summary>
    /// 在指定场景中查找第一个目标类型组件。
    /// </summary>
    /// <typeparam name="T">目标组件类型。</typeparam>
    /// <param name="scene">待搜索的场景。</param>
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
    /// 设置 SerializedObject（序列化对象）的对象引用字段。
    /// </summary>
    /// <param name="serializedObject">待修改的序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">新的对象引用。</param>
    private static void SetObjectProperty(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.objectReferenceValue = value;
    }

    /// <summary>
    /// 设置 SerializedObject（序列化对象）的布尔字段。
    /// </summary>
    /// <param name="serializedObject">待修改的序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <param name="value">新的布尔值。</param>
    private static void SetBooleanProperty(
        SerializedObject serializedObject,
        string propertyName,
        bool value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.boolValue = value;
    }

    /// <summary>
    /// 设置 SerializedObject（序列化对象）的对象引用数组字段。
    /// </summary>
    private static void SetObjectArrayProperty(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object[] values)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }

    /// <summary>
    /// 设置 SerializedObject（序列化对象）的二维整数向量数组字段。
    /// </summary>
    private static void SetVector2IntArrayProperty(
        SerializedObject serializedObject,
        string propertyName,
        Vector2Int[] values)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
            property.GetArrayElementAtIndex(index).vector2IntValue = values[index];
    }

    /// <summary>
    /// 获取必须存在的序列化字段。
    /// </summary>
    /// <param name="serializedObject">待查询的序列化对象。</param>
    /// <param name="propertyName">字段名称。</param>
    /// <returns>找到的序列化字段。</returns>
    private static SerializedProperty RequireProperty(
        SerializedObject serializedObject,
        string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{serializedObject.targetObject.GetType().Name} 缺少字段 {propertyName}。");
        }

        return property;
    }
}
