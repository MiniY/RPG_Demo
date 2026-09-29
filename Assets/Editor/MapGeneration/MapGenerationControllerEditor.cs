using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 为 MapGenerationController 提供可视化的编辑器按钮。
/// </summary>
[CustomEditor(typeof(MapGenerationController))]
public class MapGenerationControllerEditor : Editor
{
    /// <summary>
    /// 绘制默认字段和地图生成操作按钮。
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGenerationController controller = (MapGenerationController)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Map Generation Tools（地图生成工具）", EditorStyles.boldLabel);

        if (controller.Settings == null || controller.TilemapRenderer == null)
        {
            EditorGUILayout.HelpBox(
                "请先配置 Settings 和 Tilemap Renderer，再执行生成。",
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate（生成）"))
                ExecuteWithUndo(controller, "Generate Random Map", controller.GenerateMap);

            if (GUILayout.Button("Clear（清空）"))
                ExecuteWithUndo(controller, "Clear Random Map", controller.ClearMap);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate（重新生成）"))
                ExecuteWithUndo(controller, "Regenerate Random Map", controller.RegenerateMap);

            if (GUILayout.Button("Randomize Seed（随机种子）"))
                RandomizeSeed(controller);
        }

        EditorGUILayout.HelpBox(
            "生成和清空会修改当前测试场景中的两个 Tilemap。请保存场景前确认结果。",
            MessageType.Info);
    }

    /// <summary>
    /// 在 Unity Undo（撤销）系统中执行地图操作并标记场景已修改。
    /// </summary>
    /// <param name="controller">地图生成控制器。</param>
    /// <param name="undoName">撤销操作名称。</param>
    /// <param name="action">要执行的地图操作。</param>
    private static void ExecuteWithUndo(
        MapGenerationController controller,
        string undoName,
        System.Action action)
    {
        if (controller.TilemapRenderer != null)
        {
            if (controller.TilemapRenderer.GroundTilemap != null)
                Undo.RegisterCompleteObjectUndo(controller.TilemapRenderer.GroundTilemap, undoName);

            if (controller.TilemapRenderer.CollisionTilemap != null)
                Undo.RegisterCompleteObjectUndo(controller.TilemapRenderer.CollisionTilemap, undoName);
        }

        if (controller.Player != null)
            Undo.RecordObject(controller.Player, undoName);

        Undo.RecordObject(controller, undoName);
        action.Invoke();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    /// <summary>
    /// 修改配置中的 Seed（种子）并标记配置资产已修改。
    /// </summary>
    /// <param name="controller">地图生成控制器。</param>
    private static void RandomizeSeed(MapGenerationController controller)
    {
        if (controller.Settings == null)
            return;

        Undo.RecordObject(controller.Settings, "Randomize Map Seed");
        controller.RandomizeSeed();
        EditorUtility.SetDirty(controller.Settings);
    }
}
