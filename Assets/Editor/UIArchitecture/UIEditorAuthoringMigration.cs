using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One explicit migration in the owned candidate; never executes on import or Play.</summary>
public static class UIEditorAuthoringMigration
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    [Serializable] private sealed class MigrationEvidence
    {
        public string status, failure;
        public int existingVisualComponents, visualComponentsPreserved, pages;
        public string catalog;
    }
    public static void Run()
    {
        var evidence = new MigrationEvidence();
        string output = Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");
        try
        {
            if (Environment.GetEnvironmentVariable("RPG_UI_AUTHORING_MIGRATION") != "UIEditorAuthoring-20261105" ||
                string.IsNullOrEmpty(output) || !Application.dataPath.Replace('\\', '/').EndsWith("/UIA/Candidate/Assets"))
                throw new InvalidOperationException("Explicit owned Editor authoring request required");
            EditorSceneManager.OpenScene(ScenePath);
            UIRoot root = UnityEngine.Object.FindObjectOfType<UIRoot>();
            if (root == null || root.PageCatalog != null) throw new InvalidOperationException("Missing UI root or scene already migrated");
            SerializedObject rootData = new SerializedObject(root);
            var canvas = Ref<Canvas>(rootData, "rootCanvas");
            var hud = Ref<GameObject>(rootData, "hudRoot");
            var package = Ref<PackagePanelController>(rootData, "package");
            var shop = Ref<ShopPanelController>(rootData, "shop");
            var existingVisuals = canvas.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && (c is UnityEngine.UI.Graphic || c is UnityEngine.UI.Selectable ||
                    c is UnityEngine.UI.LayoutGroup || c is UnityEngine.UI.LayoutElement || c is UnityEngine.UI.CanvasScaler))
                .ToDictionary(c => c, EditorJsonUtility.ToJson);
            evidence.existingVisualComponents = existingVisuals.Count;
            var layers = new UILayerController(canvas, hud);
            var views = root.gameObject.AddComponent<TowerRunUiViews>();
            views.BuildAuthoringLayout();
            var viewData = new SerializedObject(views);
            var packageData = new SerializedObject(package);
            var shopData = new SerializedObject(shop);
            var packagePage = Ref<GameObject>(packageData, "packagePanel").AddComponent<PackageUIPage>();
            SetRef(packagePage, "controller", package); Identity(packagePage, UIRoutes.Package.Id);
            var shopPage = Ref<GameObject>(shopData, "shopPanelRoot").AddComponent<ShopUIPage>();
            SetRef(shopPage, "controller", shop); SetRef(shopPage, "grid", Ref<ShopGridUI>(shopData, "shopGridUI"));
            SetRef(shopPage, "presentation", shop.GetComponent<ShopMerchantPresentationUI>()); Identity(shopPage, UIRoutes.Shop.Id);
            var settingsPage = Ref<GameObject>(viewData, "settingsOverlay").AddComponent<SettingsUIPage>();
            Identity(settingsPage, UIRoutes.Settings.Id, Ref<UnityEngine.UI.Button>(viewData, "audioTabButton"));
            var flowPage = Ref<GameObject>(viewData, "flowOverlay").AddComponent<FlowUIPage>();
            Identity(flowPage, UIRoutes.Flow.Id, Ref<UnityEngine.UI.Button>(viewData, "primaryButton"));
            var dialog = Ref<GameObject>(viewData, "overwriteDialog").GetComponent<UIConfirmationDialog>();
            Identity(dialog, "ui.confirmation", Ref<UnityEngine.UI.Button>(viewData, "overwriteCancelButton"));
            dialog.transform.SetParent(layers.Bindings.InactiveStaging, false);
            var catalog = ScriptableObject.CreateInstance<UIPageCatalog>();
            if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
            if (!AssetDatabase.IsValidFolder("Assets/UI/Configuration")) AssetDatabase.CreateFolder("Assets/UI", "Configuration");
            const string catalogPath = "Assets/UI/Configuration/UIPageCatalog_Default.asset";
            if (File.Exists(catalogPath)) throw new InvalidOperationException("Preserve existing UI catalog; migration requires a new destination");
            SetCatalog(catalog);
            AssetDatabase.CreateAsset(catalog, catalogPath);
            rootData.Update(); rootData.FindProperty("pageCatalog").objectReferenceValue = catalog;
            var pageObjects = new UIPage[] { packagePage, shopPage, settingsPage, flowPage, dialog };
            var pageArray = rootData.FindProperty("pages"); pageArray.arraySize = pageObjects.Length;
            for (int i = 0; i < pageObjects.Length; i++) pageArray.GetArrayElementAtIndex(i).objectReferenceValue = pageObjects[i];
            var layerData = rootData.FindProperty("layerBindings");
            BindLayer(layerData, "Window", layers.Bindings.Window); BindLayer(layerData, "System", layers.Bindings.System);
            BindLayer(layerData, "Modal", layers.Bindings.Modal); BindLayer(layerData, "Blocking", layers.Bindings.Blocking);
            BindLayer(layerData, "SystemActions", layers.Bindings.SystemActions);
            BindLayer(layerData, "InactiveStaging", layers.Bindings.InactiveStaging); BindLayer(layerData, "Scrim", layers.Bindings.Scrim);
            rootData.ApplyModifiedPropertiesWithoutUndo();
            foreach (UIPage page in pageObjects)
                if (!page.IsTemplate) layers.Attach(catalog.CreateDefinition(page), page.gameObject);
            var action = Ref<RectTransform>(rootData, "settingsAction");
            action.SetParent(layers.SystemActions, true); action.localScale = Vector3.one;
            var effects = Ref<RectTransform>(rootData, "shopEffects");
            effects.SetParent(shopPage.transform, false); UILayerController.Stretch(effects);
            root.ValidateConfiguration(); views.ValidateBindings();
            foreach (var pair in existingVisuals)
            {
                if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                    throw new InvalidOperationException("Existing visual/control data changed: " + (pair.Key == null ? "destroyed" : pair.Key.name));
                evidence.visualComponentsPreserved++;
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            if (!EditorSceneManager.SaveScene(root.gameObject.scene, ScenePath)) throw new InvalidOperationException("Scene save failed");
            AssetDatabase.SaveAssetIfDirty(catalog);
            EditorSceneManager.OpenScene(ScenePath);
            root = UnityEngine.Object.FindObjectOfType<UIRoot>();
            root.ValidateConfiguration(); root.GetComponent<TowerRunUiViews>().ValidateBindings();
            evidence.pages = root.Pages.Count; evidence.catalog = catalogPath; evidence.status = "AUTHORED_SAVED_REOPENED_VISUAL_COMPONENTS_PRESERVED";
            File.WriteAllText(Path.Combine(output, "authoring-migration.json"), JsonUtility.ToJson(evidence, true));
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            evidence.status = "FAIL"; evidence.failure = error.ToString();
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "authoring-migration.json"), JsonUtility.ToJson(evidence, true));
            Debug.LogException(error); EditorApplication.Exit(2);
        }
    }
    private static T Ref<T>(SerializedObject data, string field) where T : UnityEngine.Object => (T)data.FindProperty(field).objectReferenceValue;
    private static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Identity(UIPage page, string id, UnityEngine.UI.Selectable focus = null)
    {
        var data = new SerializedObject(page); data.FindProperty("pageId").stringValue = id;
        data.FindProperty("defaultFocus").objectReferenceValue = focus; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void BindLayer(SerializedProperty data, string field, UnityEngine.Object value) => data.FindPropertyRelative(field).objectReferenceValue = value;
    private static void SetCatalog(UIPageCatalog catalog)
    {
        var data = new SerializedObject(catalog); var rows = data.FindProperty("pages"); rows.arraySize = 5;
        Rule(rows.GetArrayElementAtIndex(0), "ui.package", "背包", UILayer.Window, .35f, true, false, "PrimaryWorkspace");
        Rule(rows.GetArrayElementAtIndex(1), "ui.shop", "商店", UILayer.Window, .35f, true, false, "PrimaryWorkspace");
        Rule(rows.GetArrayElementAtIndex(2), "ui.settings", "设置", UILayer.System, .50f, true, true, "");
        Rule(rows.GetArrayElementAtIndex(3), "ui.flow", "冒险流程", UILayer.System, .82f, false, false, "");
        rows.GetArrayElementAtIndex(3).FindPropertyRelative("BackPolicy").intValue = (int)UIBackPolicy.Reject;
        Rule(rows.GetArrayElementAtIndex(4), "ui.confirmation", "确认弹窗模板", UILayer.Modal, .55f, false, false, "");
        rows.GetArrayElementAtIndex(4).FindPropertyRelative("TemplateOnly").boolValue = true;
        rows.GetArrayElementAtIndex(4).FindPropertyRelative("Cache").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo(); catalog.Validate();
    }
    private static void Rule(SerializedProperty row, string id, string name, UILayer layer, float dim, bool gameplay, bool pause, string workspace)
    {
        row.FindPropertyRelative("Id").stringValue = id; row.FindPropertyRelative("DisplayName").stringValue = name;
        row.FindPropertyRelative("Layer").intValue = (int)layer; row.FindPropertyRelative("Workspace").stringValue = workspace;
        row.FindPropertyRelative("BackPolicy").intValue = (int)UIBackPolicy.Close; row.FindPropertyRelative("DimAlpha").floatValue = dim;
        row.FindPropertyRelative("BlockGameplay").boolValue = gameplay; row.FindPropertyRelative("PauseWorld").boolValue = pause;
        row.FindPropertyRelative("Cache").boolValue = true; row.FindPropertyRelative("TemplateOnly").boolValue = false;
    }
}
