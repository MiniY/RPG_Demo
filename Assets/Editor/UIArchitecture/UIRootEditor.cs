using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIRoot))]
public sealed class UIRootEditor : Editor
{
    private Editor catalogEditor;
    private bool rulesExpanded;
    private bool bindingsExpanded;
    [MenuItem("Tools/UI/管理当前场景 UI")]
    private static void SelectRoot()
    {
        var root = UnityEngine.Object.FindObjectOfType<UIRoot>();
        if (root == null) { Debug.LogWarning("当前场景没有 UIRoot。"); return; }
        UIManagementWindow.ShowFor(root);
    }
    public override void OnInspectorGUI()
    {
        var root = (UIRoot)target;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("UI 系统总览", EditorStyles.boldLabel);
        try { root.ValidateConfiguration(); EditorGUILayout.HelpBox("配置与场景页面绑定有效。", MessageType.Info); }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        if (root.Pages != null)
            foreach (UIPage page in root.Pages)
            {
                if (page == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(page.PageId + (page.IsTemplate ? "（模板）" : ""));
                if (GUILayout.Button("定位页面", GUILayout.Width(80))) { Selection.activeGameObject = page.gameObject; EditorGUIUtility.PingObject(page); }
                EditorGUILayout.EndHorizontal();
            }
        if (root.PageCatalog != null)
        {
            if (GUILayout.Button("选择集中配置资产")) { Selection.activeObject = root.PageCatalog; EditorGUIUtility.PingObject(root.PageCatalog); }
            rulesExpanded = EditorGUILayout.Foldout(rulesExpanded, "集中页面规则（直接编辑配置资产）", true);
            if (rulesExpanded)
            {
                CreateCachedEditor(root.PageCatalog, null, ref catalogEditor);
                catalogEditor.OnInspectorGUI();
            }
        }
        EditorGUILayout.HelpBox("布局保存在场景中，可选择页面及子控件编辑。页面根在进入 Play 后由 UIManager 接管显隐。", MessageType.None);
        if (Application.isPlaying && root.Manager != null)
        {
            EditorGUILayout.LabelField("登记 / 活动 / 待完成确认", root.Manager.RegisteredCount + " / " + root.Manager.OpenCount + " / " + root.Manager.PendingConfirmations);
            if (GUILayout.Button("输出当前管理器快照")) root.DumpState();
        }
        bindingsExpanded = EditorGUILayout.Foldout(bindingsExpanded, "场景引用与层容器绑定", true);
        if (bindingsExpanded) DrawDefaultInspector();
    }
    private void OnDisable() { if (catalogEditor != null) DestroyImmediate(catalogEditor); }
}

[CustomEditor(typeof(UIPage), true)]
public sealed class UIPageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var page = (UIPage)target;
        var root = page.GetComponentInParent<UIRoot>(true);
        try
        {
            page.ValidateBindings();
            if (root == null || root.PageCatalog == null) throw new InvalidOperationException("页面未绑定到场景 UIRoot 的配置。");
            var rule = root.PageCatalog.Find(page.PageId);
            EditorGUILayout.HelpBox("层级：" + rule.Layer + "；返回：" + rule.BackPolicy + "；压暗：" + rule.DimAlpha +
                "；缓存：" + rule.Cache + "；阻断输入：" + rule.BlockGameplay + "；暂停世界：" + rule.PauseWorld, MessageType.Info);
            if (GUILayout.Button("定位集中配置")) { Selection.activeObject = root.PageCatalog; EditorGUIUtility.PingObject(root.PageCatalog); }
            if (GUILayout.Button("返回 UI 系统总览")) Selection.activeGameObject = root.gameObject;
        }
        catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
    }
}
