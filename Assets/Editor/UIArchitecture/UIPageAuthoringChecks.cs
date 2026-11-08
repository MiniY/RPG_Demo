using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UIPageAuthoringChecks
{
    public static void RunCase(string name)
    {
        var previousActive = SceneManager.GetActiveScene();
        if (!previousActive.IsValid() || !previousActive.isLoaded)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool ownsScene = !scene.IsValid() || !scene.isLoaded;
        if (ownsScene) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        UIPageCatalog clone = null;
        try
        {
            UIRoot root = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<UIRoot>(true)).SingleOrDefault();
            Require(root != null, "Scene UI root is missing");
            root.ValidateConfiguration();
            var views = root.GetComponent<TowerRunUiViews>();
            Require(views != null, "System views must be authored before Play"); views.ValidateBindings();
            if (name == "SavedSceneHasPagesAndLayers")
            {
                Require(root.Pages.Count == 5 && root.Pages.Count(p => p.IsTemplate) == 1, "Expected four scene pages and one dialog template");
                Require(root.GetComponentsInChildren<UIPage>(true).Length == 5, "Scene page components are missing or duplicated");
                Require(root.GetComponentsInChildren<Canvas>(true).Count(c => c.name == "WindowLayer") == 1, "Window layer must be authored once");
                foreach (UIPage page in root.Pages)
                    Require(page.IsTemplate || page.transform.parent.name == root.PageCatalog.Find(page.PageId).Layer + "Layer", "Page is outside its configured layer");
                return;
            }
            clone = UnityEngine.Object.Instantiate(root.PageCatalog);
            if (name == "RuntimeDefinitionDoesNotMutateCatalog")
            {
                var page = root.FindPage<FlowUIPage>(UIRoutes.Flow.Id); var rule = clone.Find(page.PageId);
                UILayer before = rule.Layer; UIPageDefinition definition = clone.CreateDefinition(page);
                definition.Layer = UILayer.Blocking; definition.DimAlpha = .1f;
                Require(rule.Layer == before && Math.Abs(rule.DimAlpha - .82f) < .0001f, "Runtime routing wrote into shared authoring rules");
                return;
            }
            var data = new SerializedObject(clone); var entries = data.FindProperty("pages");
            if (name == "DuplicateCatalogIdRejected") entries.GetArrayElementAtIndex(1).FindPropertyRelative("Id").stringValue = "ui.package";
            else if (name == "InvalidTemplatePolicyRejected") entries.GetArrayElementAtIndex(4).FindPropertyRelative("Cache").boolValue = true;
            else if (name == "InvalidDimmingRejected") entries.GetArrayElementAtIndex(0).FindPropertyRelative("DimAlpha").floatValue = float.NaN;
            else if (name == "UncachedScenePageRejected") entries.GetArrayElementAtIndex(0).FindPropertyRelative("Cache").boolValue = false;
            else if (name == "MissingSceneBindingRejected")
            {
                var rootData = new SerializedObject(root); var property = rootData.FindProperty("pages").GetArrayElementAtIndex(0);
                RejectMissingReference(rootData, property, root.ValidateConfiguration); return;
            }
            else if (name == "MissingRootReferencesRejected")
            {
                foreach (string field in new[] { "rootCanvas", "eventSystem", "package", "shop", "settingsButton" })
                {
                    var rootData = new SerializedObject(root); RejectMissingReference(rootData, rootData.FindProperty(field), root.ValidateConfiguration);
                }
                return;
            }
            else if (name == "MissingSystemControlRejected")
            {
                var viewData = new SerializedObject(views);
                RejectMissingReference(viewData, viewData.FindProperty("volumeSlider"), root.ValidateConfiguration); return;
            }
            else if (name == "MissingConfirmationControlsRejected")
            {
                var dialog = root.Pages.OfType<UIConfirmationDialog>().Single();
                foreach (string field in new[] { "confirmButton", "cancelButton", "details" })
                {
                    var dialogData = new SerializedObject(dialog);
                    int before = root.GetComponentsInChildren<UIConfirmationDialog>(true).Length;
                    RejectMissingReference(dialogData, dialogData.FindProperty(field), root.ValidateConfiguration,
                        () => UIConfirmationDialog.ShowAsync(root, default, dialog.gameObject, "missing binding test"));
                    Require(root.GetComponentsInChildren<UIConfirmationDialog>(true).Length == before, "Rejected template leaked a clone");
                }
                return;
            }
            else throw new ArgumentException("Unknown UI authoring case: " + name);
            data.ApplyModifiedPropertiesWithoutUndo(); ExpectRejected(clone.Validate);
        }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            if (ownsScene && !EditorSceneManager.CloseScene(scene, true)) throw new InvalidOperationException("Test scene cleanup failed");
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void RejectMissingReference(SerializedObject data, SerializedProperty property, params Action[] validators)
    {
        UnityEngine.Object original = property.objectReferenceValue;
        try
        {
            property.objectReferenceValue = null; data.ApplyModifiedPropertiesWithoutUndo();
            foreach (Action validate in validators) ExpectRejected(validate);
        }
        finally { data.Update(); property.objectReferenceValue = original; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static void ExpectRejected(Action action)
    {
        bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Invalid authoring data was accepted");
    }
}
