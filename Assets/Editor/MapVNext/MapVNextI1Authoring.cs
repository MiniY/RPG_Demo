using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MapVNext;

public static class MapVNextI1Authoring
{
    public const string AssetRoot = "Assets/MapGeneration/MapVNext/I1";
    public const string ScenePath = "Assets/Scenes/MapVNext/I1/I1Game.unity";
    public static void Prepare()
    {
        try
        {
            Directory.CreateDirectory(AssetRoot + "/Resources"); AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<EcologyRecipeDefinition>(AssetRoot + "/Recipe.asset") != null) throw new InvalidOperationException("I1AlreadyAuthored");
            var old = Resources.Load<EcologyRecipeDefinition>("Ecology/SurfaceV4MainV1/Recipe");
            var settings = UnityEngine.Object.Instantiate(old.Settings); settings.name = "Settings_VNextI1_64x64";
            settings.remediationProjectedSurfaces = false;
            AssetDatabase.CreateAsset(settings, AssetRoot + "/Settings.asset");
            var strategy = ScriptableObject.CreateInstance<MapVNextFormalTerrain>(); strategy.name = "Terrain_VNextI1";
            strategy.BaseTerrain = old.TerrainGenerator; strategy.Assets = AssetDatabase.LoadAssetAtPath<MapVNextAssets>("Assets/MapGeneration/MapVNext/MapVNextAssets.asset");
            AssetDatabase.CreateAsset(strategy, AssetRoot + "/Terrain.asset");
            AssetDatabase.SaveAssets();
            // Stable serialized asset bytes bind the policy; Editor instance IDs are not content identity.
            string configuration = MapHostCompatibility.Sha(System.Text.Encoding.UTF8.GetBytes("vnext-i1-formal:64x64:retained-base-float32:side-native-r7:rock-v1:own-foot:weighted-manhattan-stairs:" + MapHostCompatibility.Sha(File.ReadAllBytes(AssetRoot + "/Settings.asset"))));
            var oldManifest = Resources.Load<TextAsset>("Ecology/SurfaceV4MainV1/RetainedContent_MainV1");
            string candidateManifest = oldManifest.text.Replace(EcologyRecipeDefinition.MainV1ProfileId, EcologyRecipeDefinition.VNextI1ProfileId).Replace(old.ConfigurationDigest, configuration);
            File.WriteAllText(AssetRoot + "/Resources/CandidateContent_I1.json", candidateManifest, new System.Text.UTF8Encoding(false)); AssetDatabase.Refresh();
            var recipe = UnityEngine.Object.Instantiate(old); recipe.name = "Recipe_VNextI1";
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("candidateProfileId").stringValue = EcologyRecipeDefinition.VNextI1ProfileId;
            serialized.FindProperty("configurationDigest").stringValue = configuration;
            serialized.FindProperty("retainedSettings").objectReferenceValue = settings;
            serialized.FindProperty("retainedTerrain").objectReferenceValue = strategy;
            serialized.FindProperty("retainedContentManifest").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetRoot + "/Resources/CandidateContent_I1.json");
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(recipe, AssetRoot + "/Recipe.asset"); AssetDatabase.SaveAssets();
            if (!AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", ScenePath)) throw new InvalidOperationException("I1SceneCopyFailed");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var entry = new GameObject("I1 显式内容与隔离入口").AddComponent<MapVNextI1Bootstrap>();
            entry.Controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
            entry.VNextRecipe = recipe; entry.MainV1Recipe = old;
            // An inactive candidate variant prevents OnEnable/trigger activation before the production finalizer prepares its pose.
            var floor = UnityEngine.Object.FindObjectOfType<TowerFloorRuntimeController>(true);
            var floorSerialized = new SerializedObject(floor);
            var originalPortal = (GameObject)floorSerialized.FindProperty("exitPortalPrefab").objectReferenceValue;
            var inactivePortal = UnityEngine.Object.Instantiate(originalPortal); inactivePortal.SetActive(false);
            var portalAsset = PrefabUtility.SaveAsPrefabAsset(inactivePortal, AssetRoot + "/ExitPortal_Inactive.prefab");
            UnityEngine.Object.DestroyImmediate(inactivePortal);
            floorSerialized.FindProperty("exitPortalPrefab").objectReferenceValue = portalAsset; floorSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/VersionedRuns");
            File.WriteAllText("Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/VersionedRuns/authoring.json", JsonUtility.ToJson(new Result { status = "AUTHORED_NOT_RUNTIME_VERIFIED", configDigest = configuration, candidateContent = "content-r1:" + MapHostCompatibility.Sha(System.Text.Encoding.UTF8.GetBytes(candidateManifest)), scene = ScenePath }, true));
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    [Serializable] private sealed class Result { public string status, configDigest, candidateContent, scene; }
}
