using System;
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
    /// 保存完整随机地图运行时对象的源测试场景路径。
    /// </summary>
    private const string SourceScenePath =
        "Assets/Scenes/MapGeneration/MapGenerationTest.unity";

    /// <summary>
    /// 接入主场景后的随机地图根对象名称。
    /// </summary>
    private const string IntegrationRootName = "RandomMapRuntime";

    /// <summary>
    /// 主场景旧静态地图 Grid（网格）的对象名称。
    /// </summary>
    private const string LegacyGridName = "Grid";

    /// <summary>
    /// 从 Unity 菜单执行 RPG 随机地图集成。
    /// </summary>
    [MenuItem("Tools/RPG Demo/Map Generation/Integrate Into RPG Scene")]
    public static void IntegrateFromMenu()
    {
        Integrate();
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
    /// 复制随机地图运行时子树、重新绑定玩家并停用旧静态地图。
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

        Scene sourceScene = default;

        try
        {
            // 源场景只在内存中打开，不会被保存或修改到磁盘。
            sourceScene = EditorSceneManager.OpenScene(
                SourceScenePath,
                OpenSceneMode.Additive);

            Grid sourceGrid = FindComponentInScene<Grid>(sourceScene);
            MapGenerationController sourceController =
                FindComponentInScene<MapGenerationController>(sourceScene);

            if (sourceGrid == null || sourceController == null)
            {
                throw new InvalidOperationException(
                    "MapGenerationTest 缺少 Grid 或 MapGenerationController。");
            }

            // 临时父对象让 Grid 与控制器在一次克隆中保留彼此的序列化引用。
            GameObject sourceContainer = new GameObject("RandomMapRuntimeSource");
            SceneManager.MoveGameObjectToScene(sourceContainer, sourceScene);
            sourceGrid.transform.SetParent(sourceContainer.transform, true);
            sourceController.transform.SetParent(sourceContainer.transform, true);

            // 克隆对象随后移入目标场景；测试场景原对象会随源场景关闭而丢弃。
            GameObject integrationRoot = UnityEngine.Object.Instantiate(sourceContainer);
            integrationRoot.name = IntegrationRootName;
            SceneManager.MoveGameObjectToScene(integrationRoot, targetScene);

            MapGenerationController targetController =
                integrationRoot.GetComponentInChildren<MapGenerationController>(true);
            MapMinimapController targetMinimap =
                integrationRoot.GetComponentInChildren<MapMinimapController>(true);

            if (targetController == null || targetController.TilemapRenderer == null)
            {
                throw new InvalidOperationException(
                    "复制后的随机地图运行时对象缺少控制器或渲染器。");
            }

            ConfigureController(targetController, playerAction.transform);
            ConfigureMinimap(targetMinimap, targetController, playerAction.transform);

            legacyGrid.SetActive(false);
            integrationRoot.SetActive(true);
            EditorUtility.SetDirty(legacyGrid);
            EditorUtility.SetDirty(integrationRoot);

            EditorSceneManager.MarkSceneDirty(targetScene);
            if (!EditorSceneManager.SaveScene(targetScene, TargetScenePath))
                throw new InvalidOperationException("无法保存随机地图集成后的 SampleScene。");
        }
        finally
        {
            // 丢弃源测试场景中的临时分组，确保测试场景文件保持原样。
            if (sourceScene.IsValid() && sourceScene.isLoaded)
                EditorSceneManager.CloseScene(sourceScene, true);
        }

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
