using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stage 7 contracts for compatibility retirement and GameplayReady ownership.</summary>
public sealed class MapCompatibilityRetirementTests
{
    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void TransitionalAdapterIsInertEvenWhenStaleMigrationDataExists()
    {
        GameObject adapterObject = new GameObject("TransitionalAdapter");
        GameObject staleTarget = new GameObject("StaleLegacyTarget");
        try
        {
            MapGeneratedObjectPlacementAdapter adapter =
                adapterObject.AddComponent<MapGeneratedObjectPlacementAdapter>();
            SerializedObject serializedAdapter = new SerializedObject(adapter);
            SerializedProperty targets = serializedAdapter.FindProperty("mapAnchoredObjects");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = staleTarget.transform;
            SerializedProperty offsets = serializedAdapter.FindProperty("authoredCellOffsetValues");
            offsets.arraySize = 1;
            offsets.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(9, -4);
            serializedAdapter.ApplyModifiedPropertiesWithoutUndo();

            Vector3 originalPosition = new Vector3(14f, -3f, 2f);
            staleTarget.transform.position = originalPosition;
            MapData map = new MapData(4, 4, Vector2Int.zero)
            {
                SpawnCell = Vector2Int.zero,
                ExitCell = new Vector2Int(3, 3)
            };

            adapter.PlaceObjects(map);

            Assert.That(adapter.RuntimePlacementEnabled, Is.False);
            Assert.That(adapter.ProductionRoleCount, Is.EqualTo(1),
                "Stale migration data remains observable for cleanup diagnostics.");
            Assert.That(staleTarget.transform.position, Is.EqualTo(originalPosition),
                "The retired adapter must not consume authored position/offset data or run nearest-cell repair.");
        }
        finally
        {
            Object.DestroyImmediate(staleTarget);
            Object.DestroyImmediate(adapterObject);
        }
    }

    [Test]
    public void GameplayReadyDoesNotRequireTransitionalAdapter()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject runtimeRoot = FindRootObject(scene, "RandomMapRuntime");
            MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
            MapGenerationController controller =
                runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
            MapGeneratedObjectPlacementAdapter adapter =
                runtimeRoot.GetComponent<MapGeneratedObjectPlacementAdapter>();
            Assert.That(adapter, Is.Not.Null,
                "The adapter asset remains as migration diagnostics; Stage 7 does not delete it.");
            Assert.That(adapter.ProductionRoleCount, Is.Zero);
            Object.DestroyImmediate(adapter);

            bootstrap.Initialize();
            int gameplayReadyCount = 0;
            System.Guid readyGenerationId = System.Guid.Empty;
            controller.Orchestrator.GameplayReady += generationId =>
            {
                gameplayReadyCount++;
                readyGenerationId = generationId;
            };

            controller.GenerateMap();

            MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;
            Assert.That(snapshot.Phase, Is.EqualTo(MapLifecyclePhase.Ready));
            Assert.That(snapshot.IsReady, Is.True);
            Assert.That(snapshot.IsFailed, Is.False);
            Assert.That(gameplayReadyCount, Is.EqualTo(1));
            Assert.That(readyGenerationId, Is.EqualTo(snapshot.ActiveGenerationId));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SampleSceneRandomAuthorityExcludesActiveLegacyRenderingCollisionAndBounds()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject legacyGrid = FindRootObject(scene, "Grid");
            GameObject runtimeRoot = FindRootObject(scene, "RandomMapRuntime");
            MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
            bootstrap.Initialize();

            Assert.That(bootstrap.Mode, Is.EqualTo(MapRuntimeMode.RandomGenerated));
            Assert.That(legacyGrid.activeInHierarchy, Is.False);
            foreach (Renderer renderer in legacyGrid.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.gameObject.activeInHierarchy, Is.False);
            foreach (Collider2D collider in legacyGrid.GetComponentsInChildren<Collider2D>(true))
                Assert.That(collider.gameObject.activeInHierarchy, Is.False);
            Assert.That(bootstrap.DiagnosticSnapshot.AuthorityState.LegacyStaticActive, Is.False);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
                return rootObject;
        }

        Assert.Fail($"Scene {scene.path} is missing root object {objectName}.");
        return null;
    }
}
