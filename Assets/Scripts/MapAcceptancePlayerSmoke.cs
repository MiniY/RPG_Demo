#if RPG_DEMO_MONSTER_ECOLOGY_MVP || RPG_DEMO_TERRAIN_V2_MVP
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Optional Player-only smoke probe; normal launches leave it inactive.</summary>
public sealed partial class MapAcceptancePlayerSmoke : MonoBehaviour
{
    [Serializable]
    private sealed class SmokeEvidence
    {
        public string scene;
        public string initialState;
        public string finalState;
        public string profileId;
        public int effectiveSeed;
        public string generationId;
        public int monsterCount;
        public int destructibleCount;
        public int h1Cells;
        public int h2Cells;
        public int stairCount;
        public string persistentDataPath;
        public string startupImage;
        public string spawnImage;
        public float inputMovementDistance;
        public bool movementInputPassed;
        public string inputDiagnostics;
        public string status;
        public string error;
    }

    private static string outputRoot;
    private static Keyboard smokeKeyboard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartWhenRequested()
    {
        outputRoot = Environment.GetEnvironmentVariable("RPG_DEMO_ACCEPTANCE_SMOKE_ROOT");
        if (string.IsNullOrWhiteSpace(outputRoot))
            return;
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(outputRoot, "IsolatedSlots"));
        smokeKeyboard = InputSystem.AddDevice<Keyboard>("MapAcceptanceSmokeKeyboard");
        GameObject probe = new GameObject("MapAcceptancePlayerSmoke");
        DontDestroyOnLoad(probe);
        probe.AddComponent<MapAcceptancePlayerSmoke>();
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(outputRoot);
        float sceneDeadline = Time.realtimeSinceStartup + 15f;
        while (SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity" &&
               Time.realtimeSinceStartup < sceneDeadline)
            yield return null;
        SmokeEvidence evidence = new SmokeEvidence
        {
            scene = SceneManager.GetActiveScene().path,
            persistentDataPath = Application.persistentDataPath,
            status = "FAIL"
        };
        yield return null;
        TowerRunCoordinator coordinator = FindObjectOfType<TowerRunCoordinator>();
        if (coordinator == null)
        {
            Complete(evidence, "TowerRunCoordinator is missing.");
            yield break;
        }
        evidence.initialState = coordinator.State.ToString();
        if (coordinator.State != TowerRunFlowState.StartupChoice)
        {
            Complete(evidence, "Player did not start at the main menu.");
            yield break;
        }

        evidence.startupImage = "Player-Smoke-Startup.png";
        if (!CaptureFrame(evidence.startupImage))
        {
            Complete(evidence, "The startup frame was blank or could not be rendered.");
            yield break;
        }

#if RPG_DEMO_ECOLOGY_UNITS_CANDIDATE || RPG_DEMO_ECOLOGY_REMEDIATION
        yield return RunUnitsArtCandidateProof(evidence, coordinator);
        yield break;
#elif RPG_DEMO_XIAOSU_MAP_MVP
        Complete(evidence, EcologyRecipeDefinition.RetiredContentMessage);
        yield break;
#else
        coordinator.BeginNewRun();
        float deadline = Time.realtimeSinceStartup + 20f;
        while (coordinator.State != TowerRunFlowState.Playing &&
               coordinator.State != TowerRunFlowState.Failure &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        evidence.finalState = coordinator.State.ToString();
        if (coordinator.State != TowerRunFlowState.Playing)
        {
            Complete(evidence, "New adventure did not start: " + coordinator.FailureMessage);
            yield break;
        }

        MapRuntimeBootstrap bootstrap = FindObjectOfType<MapRuntimeBootstrap>();
        MapEcologyPopulationService ecology = FindObjectOfType<MapEcologyPopulationService>();
        if (bootstrap == null || !bootstrap.DiagnosticSnapshot.IsReady)
        {
            Complete(evidence, "The map did not reach GameplayReady.");
            yield break;
        }
        MapRuntimeDiagnosticSnapshot snapshot = bootstrap.DiagnosticSnapshot;
        MapGenerationAttemptDiagnostic success = snapshot.AttemptChain
            .FirstOrDefault(x => x.Outcome == MapAttemptOutcome.Succeeded);
        if (success == null)
        {
            Complete(evidence, "The successful map attempt is missing.");
            yield break;
        }
        evidence.profileId = snapshot.ActiveRequest.ProfileId;
        evidence.effectiveSeed = success.Attempt.AttemptSeed;
        evidence.generationId = snapshot.ActiveGenerationId?.ToString("D") ?? string.Empty;
        evidence.monsterCount = ecology != null && ecology.LastPlan != null
            ? ecology.LastPlan.ActualMonsters : 0;
        evidence.destructibleCount = ecology != null && ecology.LastPlan != null
            ? ecology.LastPlan.ActualDestructibles : 0;
#if RPG_DEMO_TERRAIN_V2_MVP
        MapData activeMap = bootstrap.Context.ActiveMap;
        evidence.stairCount = activeMap.Stairs.Count;
        for (int y = activeMap.Origin.y; y < activeMap.Origin.y + activeMap.Height; y++)
        for (int x = activeMap.Origin.x; x < activeMap.Origin.x + activeMap.Width; x++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!activeMap.IsWalkable(cell))
                continue;
            if (activeMap.GetElevation(cell) == 1) evidence.h1Cells++;
            if (activeMap.GetElevation(cell) == 2) evidence.h2Cells++;
        }
        if ((evidence.profileId != "terrain-v2" && evidence.profileId != EcologyRecipeDefinition.RemediationProfileId) ||
            evidence.h1Cells < 36 || evidence.h2Cells < 9 || evidence.stairCount != 4)
        {
            Complete(evidence, "The Player did not generate valid terrain-v2 heights and stairs.");
            yield break;
        }
        if (evidence.profileId == EcologyRecipeDefinition.RemediationProfileId)
        {
            var layout = bootstrap.Context.ActivePlacementPlan.Ecology;
            int actual = bootstrap.Context.ActiveRegistry.Entries.Count(entry => entry.Role == MonsterPlacementService.MonsterRole && entry.Instance != null);
            if (layout == null || layout.Regions.Where(region => !region.IsSafety).Any(region => region.AssignedPopulation < 3) ||
                evidence.monsterCount != layout.Residents.Count || evidence.monsterCount != actual)
            {
                Complete(evidence, "R3 region targets, spawned population and live Registry differ.");
                yield break;
            }
        }
#else
        if (evidence.profileId != MapEcologyProfile.ProfileId ||
            evidence.monsterCount < 2 || evidence.destructibleCount < 4)
        {
            Complete(evidence, "The Player did not generate ecology-v1 population.");
            yield break;
        }
#endif

        yield return new WaitForEndOfFrame();
        evidence.spawnImage = "Player-Smoke-Spawn.png";
        if (!CaptureFrame(evidence.spawnImage))
        {
            Complete(evidence, "The spawned frame was blank or could not be rendered.");
            yield break;
        }
        Keyboard keyboard = smokeKeyboard;
        InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
        bool previousRunInBackground = Application.runInBackground;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Vector2Int[] directions =
                { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            Key[] keys = { Key.D, Key.W, Key.A, Key.S };
            if (!directions.Any(direction => bootstrap.Context.ActiveMap.CanTraverse(
                    bootstrap.Context.ActiveMap.SpawnCell,
                    bootstrap.Context.ActiveMap.SpawnCell + direction)))
            {
                Complete(evidence, "The spawn has no adjacent walkable cell for movement.");
                yield break;
            }
            Transform player = FindObjectOfType<PlayerDamageController>().transform;
            PlayerAction action = player.GetComponent<PlayerAction>();
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                if (!bootstrap.Context.ActiveMap.CanTraverse(
                        bootstrap.Context.ActiveMap.SpawnCell,
                        bootstrap.Context.ActiveMap.SpawnCell + directions[directionIndex]))
                    continue;
                Vector2 start = player.position;
                for (int frame = 0; frame < 30; frame++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[directionIndex]));
                    InputSystem.Update();
                    yield return null;
                }
                float distance = Vector2.Distance(start, player.position);
                evidence.inputMovementDistance = Mathf.Max(evidence.inputMovementDistance, distance);
                evidence.inputDiagnostics += keys[directionIndex] +
                    ": key=" + keyboard[keys[directionIndex]].isPressed +
                    " move=" + (GameInput.Instance != null ? GameInput.Instance.MoveInput.ToString() : "no GameInput") +
                    " action=" + (action != null && action.enabled) +
                    " locked=" + (action != null && action.IsControlLocked) +
                    " time=" + Time.timeScale +
                    " distance=" + distance.ToString("F3") + "; ";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                if (distance > 0.05f)
                    break;
            }
            evidence.movementInputPassed = evidence.inputMovementDistance > 0.05f;
            if (!evidence.movementInputPassed)
            {
                Complete(evidence, "The Player did not move after a real Input System key press.");
                yield break;
            }
        }
        finally
        {
            Application.runInBackground = previousRunInBackground;
            InputSystem.settings.backgroundBehavior = previousBackground;
            if (keyboard != null && keyboard.added)
                InputSystem.RemoveDevice(keyboard);
        }
        evidence.status = "PASS";
        Debug.Log("MAP_ACCEPTANCE_PLAYER_SMOKE_PASS seed=" + evidence.effectiveSeed +
            " profile=" + evidence.profileId + " generation=" + evidence.generationId +
            " monsters=" + evidence.monsterCount +
            " destructibles=" + evidence.destructibleCount);
        Complete(evidence, null);
#endif
    }

    private static bool CaptureFrame(string fileName)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;
        const int width = 1280;
        const int height = 720;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        Canvas[] overlays = FindObjectsOfType<Canvas>(true)
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] previousCameras = overlays.Select(canvas => canvas.worldCamera).ToArray();
        float[] previousDistances = overlays.Select(canvas => canvas.planeDistance).ToArray();
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < overlays.Length; i++)
            {
                overlays[i].renderMode = RenderMode.ScreenSpaceCamera;
                overlays[i].worldCamera = camera;
                overlays[i].planeDistance = 5f;
            }
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            Color32[] samples = pixels.GetPixels32();
            bool hasVisiblePixel = samples.Any(pixel =>
                pixel.r > 24 || pixel.g > 24 || pixel.b > 24);
            if (!hasVisiblePixel)
                return false;
            File.WriteAllBytes(Path.Combine(outputRoot, fileName), pixels.EncodeToPNG());
            return true;
        }
        finally
        {
            for (int i = 0; i < overlays.Length; i++)
            {
                overlays[i].renderMode = RenderMode.ScreenSpaceOverlay;
                overlays[i].worldCamera = previousCameras[i];
                overlays[i].planeDistance = previousDistances[i];
            }
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            Destroy(pixels);
        }
    }

    private static void Complete(SmokeEvidence evidence, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            evidence.error = error;
            Debug.LogError("MAP_ACCEPTANCE_PLAYER_SMOKE_FAIL " + error);
        }
        File.WriteAllText(Path.Combine(outputRoot, "PlayerSmoke.json"),
            JsonUtility.ToJson(evidence, true));
        Application.Quit(evidence.status == "PASS" ? 0 : 2);
    }
}
#endif
