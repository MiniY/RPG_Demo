using UnityEditor;
using UnityEngine;

/// <summary>One editor overview over the actual root inspector; it stores no parallel page state.</summary>
public sealed class UIManagementWindow : EditorWindow
{
    [SerializeField] private UIRoot root;
    private Editor inspector;
    private Vector2 scroll;
    public static void ShowFor(UIRoot value)
    {
        var window = GetWindow<UIManagementWindow>("UI 系统管理");
        window.root = value; window.minSize = new Vector2(560, 420);
        window.Show(); window.Focus();
    }
    private void OnGUI()
    {
        root = (UIRoot)EditorGUILayout.ObjectField("场景 UI 根", root, typeof(UIRoot), true);
        if (root == null)
        {
            EditorGUILayout.HelpBox("请打开 SampleScene，并通过 Tools/UI/管理当前场景 UI 选择其 UIRoot。", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("当前状态", Application.isPlaying ? "Play：使用实际 UIManager 状态" : "编辑模式：页面与布局已保存在场景中");
        if (GUILayout.Button("在场景中定位 UICanvas")) { Selection.activeGameObject = root.gameObject; EditorGUIUtility.PingObject(root); }
        UnityEditor.Editor.CreateCachedEditor(root, null, ref inspector);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        inspector.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }
    private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
}
