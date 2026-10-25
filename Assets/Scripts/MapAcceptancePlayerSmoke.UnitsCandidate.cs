#if RPG_DEMO_ECOLOGY_UNITS_CANDIDATE || RPG_DEMO_ECOLOGY_REMEDIATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class MapAcceptancePlayerSmoke
{
#if RPG_DEMO_SURFACE_V4
    private const string AcceptanceProfileId = EcologyRecipeDefinition.MainV1ProfileId;
#elif RPG_DEMO_ECOLOGY_REMEDIATION
    private const string AcceptanceProfileId = EcologyRecipeDefinition.RemediationProfileId;
#else
    private const string AcceptanceProfileId = EcologyRecipeDefinition.UnitsStableCandidateProfileId;
#endif
    [Serializable] private sealed class UnitsSeedProof
    {
        public int seed, residents, moved, homeObservations, boundarySegments;
        public string generation, layout, feetVersion, recipe, configuration, normalOverview, blackOverview, normalSpawn, residentView;
        public string[] types;
        public Vector3 overviewCamera;
        public float overviewCameraSize, gameplayCameraSize, inputDistance;
        public bool actualSourcesMatch, noBoss, inputPassed, displayOnly;
        public string inputDiagnostics;
        public string contentVersion, restoredGeneration, checkpointLayout, slotSha256;
        public int acceptedAttempt, restoreAttempts, frameIntervalSamples;
        public float frameP50Milliseconds, frameP95Milliseconds, frameMaxMilliseconds;
        public long monoHeapBefore, monoHeapAfter;
        public bool actualStoreRestore;
        public string mapContentIdentity, terrainProjection, terrainPhysical, staticPhysical, initialPhysical, retainedSettingsJson;
        public int actualMapSeed;
        public long navigationSyncCalls, navigationRefreshes, navigationPhysicalChanges, staticVersionBefore, staticVersionAfter;
        public double navigationSyncMilliseconds;
        public List<UnitsRoleInput> roleInputs = new List<UnitsRoleInput>();
        public List<UnitsMotionFrame> motionFrames = new List<UnitsMotionFrame>();
    }
    [Serializable] private sealed class UnitsMotionFrame
    {
        public string file, resident, typeId, sprite;
        public float real, world;
        public Vector2 actualBodyPosition, actualFootCentre;
    }
    [Serializable] private sealed class UnitsCandidateProof
    {
        public string status = "FAIL", error, unity, buildGuid, product, persistentDataPath;
        public string profile = AcceptanceProfileId;
        public string scope = "Explicit isolated candidate: real production Ready/patrol/input, exact recorded entry store/restore, controlled original three-family combat/F, runtime frames and bounded frame intervals. Accepted attempt is recorded per source version; no historical e1 promise is transferred to R3. Human acceptance, unobservable internal contacts and long performance remain unverified.";
        public string arrangement = "No actor rescaling/extra HP/forced Boss/renaming/post-bind teleport. Camera captures temporarily pan the original camera and restore it; gameplay size unchanged. Combat isolates other AI, delivers real input via original readers per automatic physics step, and clears remaining targets through original receivers for exit validation. This is not whole-floor natural combat or natural input latency measurement.";
        public bool development, debugTypePresent;
        public List<UnitsSeedProof> seeds = new List<UnitsSeedProof>();
        public List<UnitsCombatProof> combats = new List<UnitsCombatProof>();
    }
    private IEnumerator RunUnitsArtCandidateProof(SmokeEvidence smoke, TowerRunCoordinator coordinator)
    {
        var proof = new UnitsCandidateProof { unity = Application.unityVersion, buildGuid = Application.buildGUID,
            product = Application.productName, persistentDataPath = Application.persistentDataPath, development = Debug.isDebugBuild,
            debugTypePresent = typeof(TowerRunCoordinator).Assembly.GetType("EcologyRegionDebugOverlay") != null };
        bool oldBackground = Application.runInBackground;
        var oldInputBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var operations = new Stack<IEnumerator>(); operations.Push(CollectUnitsCandidate(proof, coordinator));
        while (operations.Count > 0)
        {
            bool more; object next = null; string error = null;
            IEnumerator operation = operations.Peek();
            try { more = operation.MoveNext(); if (more) next = operation.Current; }
            catch (Exception e) { more = false; error = e.ToString(); }
            if (error != null) { proof.error = error; break; }
            if (!more) { (operations.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) { operations.Push(nested); continue; }
            yield return next;
        }
        if (proof.error == null) proof.status = "PASS";
        while (operations.Count > 0) (operations.Pop() as IDisposable)?.Dispose();
        InputSystem.settings.backgroundBehavior = oldInputBackground;
        Application.runInBackground = oldBackground;
        if (smokeKeyboard != null && smokeKeyboard.added) InputSystem.RemoveDevice(smokeKeyboard);
        File.WriteAllText(Path.Combine(outputRoot, "UnitsCandidateProof.json"), JsonUtility.ToJson(proof, true));
        smoke.status = proof.status;
        Complete(smoke, proof.error);
    }
    private IEnumerator CollectUnitsCandidate(UnitsCandidateProof proof, TowerRunCoordinator coordinator)
    {
        Application.runInBackground = true;
        if (proof.debugTypePresent != proof.development) throw new InvalidOperationException("Release/development debug type isolation failed.");
        if (Application.companyName != "CodexMapAcceptance" || Application.productName == "RPG_Demo")
            throw new InvalidOperationException("The candidate must have its own product/save identity.");
        var recipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(AcceptanceProfileId));
        if (Environment.GetEnvironmentVariable("RPG_DEMO_UNITS_NUMERICS_ONLY") == "1")
        {
            proof.scope = "ISOLATED_NUMERICS_PROTOTYPE_NOT_GAMEPLAY_OR_FINAL_CANDIDATE_ACCEPTANCE";
            CollectRetainedNumericsPrototype(recipe.Settings);
            yield break;
        }
        var types = recipe.ResolveTypes().ToDictionary(t => t.TypeId);
        if (types.Count != 12 || types.Values.Any(t => t.IsBoss) || !recipe.BossContentPending)
            throw new InvalidOperationException("The explicit candidate must contain twelve retained types and no claimed Boss.");
        var controller = FindObjectOfType<MapGenerationController>();
        var pool = FindObjectOfType<MapEcologyPopulationService>();
        int slotNumber = 0;
        foreach (int seed in EcologyRecipeDefinition.IsRemediationProfile(AcceptanceProfileId) ?
            new[] { 20260929, -1391545000, 613580675, 1118448528 } : new[] { 20260929, -1391545000, 613580675, 21517677 })
        {
            var field = typeof(TowerRunCoordinator).GetField("newRunBaseline", BindingFlags.NonPublic | BindingFlags.Instance);
            var baseline = (TowerRunCheckpointRecord)field.GetValue(coordinator);
            if (baseline == null) throw new InvalidOperationException("The original new-run baseline is not ready.");
            // Same staged NewRun inputs as the real entry, with a disclosed fixed capture seed.
            // Do not first launch an unrelated random map, which may correctly fail its finite planning budget.
            var record = baseline.DeepClone(); record.runId = Guid.NewGuid().ToString("D"); record.savedAtUtc = string.Empty;
            record.floor = new TowerRunFloorRecord { floorNumber = 0, displayName = "新手关卡" };
            record.generation = new TowerRunGenerationRecord { generatorVersion = TowerRunCoordinator.GeneratorVersion,
                configurationIdentity = TowerRunCoordinator.ConfigurationIdentity, profileId = AcceptanceProfileId,
                requestedSeed = seed, attemptIndex = 0, staticBaselineSignature = string.Empty };
            coordinator.ReturnToStartupChoice();
            MethodInfo begin = typeof(TowerRunCoordinator).GetMethod("BeginOperation", BindingFlags.NonPublic | BindingFlags.Instance);
            begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), record, false });
            yield return null;
            var context = controller.Orchestrator.Context;
            if (coordinator.State != TowerRunFlowState.Playing || !context.IsReady || context.ActivePlacementPlan.Ecology == null)
                throw new InvalidOperationException("Real candidate did not reach whole Ready: " + coordinator.FailureMessage);
            var layout = context.ActivePlacementPlan.Ecology;
#if RPG_DEMO_ECOLOGY_REMEDIATION
            var surfaceAudit = RemediationRuntimeAudit.Capture(controller, 0);
            File.WriteAllText(Path.Combine(outputRoot, "Seed_" + seed + "_SurfaceAudit.json"), JsonUtility.ToJson(surfaceAudit, true));
            if (surfaceAudit.violations.Count != 0 || surfaceAudit.decorations == 0)
                throw new InvalidOperationException("Actual rendered root or population audit failed: " + string.Join(";", surfaceAudit.violations));
#endif
            var row = new UnitsSeedProof { seed = seed, generation = layout.GenerationId.ToString("D"), layout = layout.Signature,
                feetVersion = layout.FootprintVersion, recipe = layout.RecipeIdentity, configuration = recipe.ConfigurationDigest,
                residents = layout.Residents.Count, noBoss = !layout.Regions.Any(r => r.Kind == MapEcologyRegionKind.Boss),
                types = layout.Residents.Select(r => r.TypeId).Distinct().OrderBy(s => s).ToArray(),
                actualSourcesMatch = true, gameplayCameraSize = Camera.main.orthographicSize };
            CaptureUnitsPlanningInputs(row, controller);
            var actors = layout.Residents.Select(r =>
            {
                if (!types.ContainsKey(r.TypeId) || !pool.TryGetActiveInstance(r.Id, out UnityEngine.Object entry))
                    throw new InvalidOperationException("Planned resident absent or outside candidate qualification: " + r.Id);
                var actor = ((Component)entry).gameObject;
                if (actor.GetComponent<PooledObject>().SourcePrefab != types[r.TypeId].Prefab)
                    throw new InvalidOperationException("The actual original pool source differs from the accepted type.");
                return (actor, resident: r, body: actor.GetComponent<Rigidbody2D>(), home: actor.GetComponent<EcologyResidentBinding>(),
                    combat: actor.GetComponent<EcologyCombatBinding>(), start: actor.GetComponent<Rigidbody2D>().position);
            }).ToArray();
            if (!row.noBoss || FindObjectOfType<TowerFloorRuntimeController>().TargetCount != actors.Length)
                throw new InvalidOperationException("Candidate Boss/target contract mismatch.");
            row.normalSpawn = "Seed_" + seed + "_Gameplay.png";
            if (!CaptureFrame(row.normalSpawn)) throw new InvalidOperationException("Actual gameplay frame was blank.");
            for (int step = 0; step < 120; step++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var a in actors)
                {
                    if (a.combat.MotionFaultRevision != 0 || !a.home.Home.ContainsFullFoot(types[a.resident.TypeId].ResolveMovementFoot(a.actor), a.body.position))
                        throw new InvalidOperationException("Actual original patrol foot left Home or faulted: " + a.resident.Id);
                    row.homeObservations++;
                }
                if (step % 6 == 0)
                {
                    string name = "Seed_" + seed + "_Patrol_" + (step / 6).ToString("D3") + ".png";
                    CaptureUnitsView(name, actors[0].body.position, row.gameplayCameraSize);
                    row.motionFrames.Add(new UnitsMotionFrame { file = name, resident = actors[0].resident.Id,
                        typeId = actors[0].resident.TypeId, sprite = actors[0].actor.GetComponentInChildren<SpriteRenderer>().sprite.name,
                        real = Time.realtimeSinceStartup, world = Time.time, actualBodyPosition = actors[0].body.position,
                        actualFootCentre = types[actors[0].resident.TypeId].ResolveMovementFoot(actors[0].actor).bounds.center });
                }
            }
            row.moved = actors.Count(a => Vector2.Distance(a.start, a.body.position) > .05f);
            if (row.moved == 0) throw new InvalidOperationException("Original AI did not actually patrol.");
            float oldTime = Time.timeScale; Time.timeScale = 0f;
            try
            {
                var map = context.ActiveMap;
                row.overviewCamera = controller.TilemapRenderer.Coordinates.CellToWorld(map.Origin + new Vector2Int(map.Width / 2, map.Height / 2));
                row.overviewCamera.z = Camera.main.transform.position.z; row.overviewCameraSize = map.Height * .55f;
                row.normalOverview = "Seed_" + seed + "_Overview_Normal.png";
                CaptureUnitsView(row.normalOverview, row.overviewCamera, row.overviewCameraSize);
                string before = layout.Signature; long revision = controller.EcologyRuntime.StaticVersion;
#if DEVELOPMENT_BUILD
                var overlay = controller.GetComponent<EcologyRegionDebugOverlay>();
                overlay.Visible = true; overlay.Refresh(); row.boundarySegments = overlay.SegmentCount;
                row.blackOverview = "Seed_" + seed + "_Overview_Blackline.png";
                CaptureUnitsView(row.blackOverview, row.overviewCamera, row.overviewCameraSize);
                overlay.Visible = false;
#endif
                row.displayOnly = context.ActivePlacementPlan.Ecology.Signature == before && controller.EcologyRuntime.StaticVersion == revision;
                if (!row.displayOnly) throw new InvalidOperationException("Display changed accepted gameplay data.");
                row.residentView = "Seed_" + seed + "_Resident_GameplayScale.png";
                CaptureUnitsView(row.residentView, actors[0].body.position, row.gameplayCameraSize);
            }
            finally { Time.timeScale = oldTime; }
            var player = controller.Player; Vector2 initial = player.position;
            proof.seeds.Add(row);
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            Key[] keys = { Key.D, Key.W, Key.A, Key.S };
            int direction = Array.FindIndex(directions, d => context.ActiveMap.CanTraverse(context.ActiveMap.SpawnCell, context.ActiveMap.SpawnCell + d));
            if (direction < 0) throw new InvalidOperationException("No actual input route at spawn.");
            for (int frame = 0; frame < 20; frame++)
            {
                if (!smokeKeyboard.enabled) InputSystem.EnableDevice(smokeKeyboard);
                InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState(keys[direction])); yield return null;
                yield return new WaitForFixedUpdate();
            }
            var action = player.GetComponent<PlayerAction>(); var motion = player.GetComponent<EcologyMotionBody>();
            row.inputDiagnostics = $"deviceEnabled={smokeKeyboard.enabled} key={smokeKeyboard[keys[direction]].isPressed} inputEnabled={GameInput.Instance.IsGameplayInputEnabled} " +
                $"move={GameInput.Instance.MoveInput} actionMove={action.MoveInput} locked={action.IsControlLocked} ready={motion.IsReady} fault={player.GetComponent<EcologyCombatBinding>().MotionFaultRevision} time={Time.timeScale:R}";
            InputSystem.QueueStateEvent(smokeKeyboard, new KeyboardState()); InputSystem.Update();
            row.inputDistance = Vector2.Distance(initial, player.position); row.inputPassed = row.inputDistance > .05f;
            if (!row.inputPassed) throw new InvalidOperationException("Actual Input System movement failed.");
            Debug.Log($"UNITS_CANDIDATE_PLAYER seed={seed} residents={row.residents} moved={row.moved} observations={row.homeObservations} input={row.inputDistance:R} development={proof.development}");
            int ownedSlot = slotNumber++ % 3 + 1;
            VerifyUnitsEntry(row, coordinator, controller, ownedSlot);
#if RPG_DEMO_ECOLOGY_REMEDIATION
            yield return CollectRemediationSurface(controller, row);
            if (Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_SURFACE_ONLY") == "1")
            { proof.scope = "DIAGNOSTIC_FIRST_SEED_SURFACE_ONLY_NOT_COMPLETE_PLAYER_ACCEPTANCE"; yield break; }
            RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
#endif
            row.monoHeapBefore = GC.GetTotalMemory(false);
            var navigation = (EcologyLiveNavigation)typeof(EcologyMapRuntime).GetField("navigation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller.EcologyRuntime);
            long beforeCalls = navigation.SynchronizeCalls, beforeRefreshes = navigation.Refreshes, beforeChanges = navigation.PhysicalChanges;
            double beforeMilliseconds = navigation.SynchronizeMilliseconds; row.staticVersionBefore = controller.EcologyRuntime.StaticVersion;
            var intervals = new List<float>();
            for (int frame = 0; frame < 180; frame++) { yield return null; intervals.Add(Time.unscaledDeltaTime * 1000f); }
            row.monoHeapAfter = GC.GetTotalMemory(false); intervals.Sort(); row.frameIntervalSamples = intervals.Count;
            row.navigationSyncCalls = navigation.SynchronizeCalls - beforeCalls; row.navigationRefreshes = navigation.Refreshes - beforeRefreshes;
            row.navigationPhysicalChanges = navigation.PhysicalChanges - beforeChanges; row.navigationSyncMilliseconds = navigation.SynchronizeMilliseconds - beforeMilliseconds;
            row.staticVersionAfter = controller.EcologyRuntime.StaticVersion;
            row.frameP50Milliseconds = intervals[(intervals.Count - 1) / 2];
            row.frameP95Milliseconds = intervals[Mathf.CeilToInt(intervals.Count * .95f) - 1]; row.frameMaxMilliseconds = intervals[intervals.Count - 1];
            if (seed == 20260929)
            {
                RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
                yield return CollectUnitsCombat(proof, coordinator, controller, row, "Torch");
#if !RPG_DEMO_ECOLOGY_REMEDIATION
                RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
                yield return CollectUnitsCombat(proof, coordinator, controller, row, "Barrel");
#endif
            }
#if RPG_DEMO_ECOLOGY_REMEDIATION
            // This disclosed fixed seed was observed to generate actual new Barrels in Run228.
            if (seed == 1118448528)
            {
                RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
                yield return CollectUnitsCombat(proof, coordinator, controller, row, "Barrel");
                RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
                yield return CollectUnitsCombat(proof, coordinator, controller, row, "Barrel", true);
            }
#endif
            if (seed == 613580675)
            {
                RestoreUnitsEntry(coordinator, controller, row, ownedSlot);
                yield return CollectUnitsCombat(proof, coordinator, controller, row, "TNT");
            }
        }
        coordinator.ReturnToStartupChoice();
    }

    private static void VerifyUnitsEntry(UnitsSeedProof row, TowerRunCoordinator coordinator, MapGenerationController controller, int slot)
    {
        if (!coordinator.TryCreateSaveRequest(out TowerRunCheckpointRecord entry, out string reason)) throw new InvalidOperationException(reason);
        if (entry.schemaVersion != TowerRunCheckpointRecord.EcologySchemaVersion || entry.generation.ecology == null ||
            entry.generation.ecology.layoutDigest != row.layout || entry.generation.ecology.footprintVersion != row.feetVersion)
            throw new InvalidOperationException("Actual candidate entry identity differs from its accepted layout.");
        string path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-" + slot + ".json");
        if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Player proof may only write its own isolated slots.");
        TowerRunSlotReadResult previousSlot = coordinator.ReadSlot(slot);
        TowerRunSlotWriteResult result = coordinator.TrySaveEntranceCandidate(slot, entry, previousSlot.state == TowerRunSlotState.Valid, previousSlot.confirmationToken);
        if (!result.succeeded) throw new InvalidOperationException(result.message);
        File.WriteAllBytes(Path.Combine(outputRoot, "Seed_" + row.seed + "_ExactSlot.json"), File.ReadAllBytes(path));
        File.WriteAllText(Path.Combine(outputRoot, "Seed_" + row.seed + "_ExactEntry.json"), JsonUtility.ToJson(entry, true));
        row.contentVersion = entry.generation.ecology.contentVersion; row.acceptedAttempt = entry.generation.ecology.ecologyAttempt;
        if (AcceptanceProfileId == EcologyRecipeDefinition.UnitsStableCandidateProfileId && row.seed == 21517677 && row.acceptedAttempt != 1)
            throw new InvalidOperationException("The retained measured e1 seed changed.");
        using (var hash = System.Security.Cryptography.SHA256.Create()) row.slotSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        RestoreUnitsEntry(coordinator, controller, row, slot);
    }

    private static void RestoreUnitsEntry(TowerRunCoordinator coordinator, MapGenerationController controller, UnitsSeedProof row, int slot)
    {
        TowerRunSlotReadResult read = coordinator.ReadSlot(slot);
        if (read.state != TowerRunSlotState.Valid) throw new InvalidOperationException(read.message);
        Guid? previous = controller.Orchestrator.Context.ActiveGenerationId;
        coordinator.ReturnToStartupChoice(); coordinator.BeginRestoreSlot(slot, read.confirmationToken);
        var context = controller.Orchestrator.Context;
        var accepted = (EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        if (!context.IsReady || context.ActiveGenerationId == previous || context.ActivePlacementPlan.Ecology?.Signature != row.layout ||
            accepted.AcceptedAttempt != row.acceptedAttempt || accepted.AttemptsUsed != 1)
            throw new InvalidOperationException("Actual Player entry restore did not rebuild exactly the recorded e/layout with a new runtime generation.");
        string path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-" + slot + ".json"), after;
        using (var hash = System.Security.Cryptography.SHA256.Create()) after = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        if (after != row.slotSha256) throw new InvalidOperationException("Actual restore rewrote its isolated original entry slot.");
        row.actualStoreRestore = true; row.restoreAttempts = accepted.AttemptsUsed; row.checkpointLayout = context.ActivePlacementPlan.Ecology.Signature;
        row.restoredGeneration = context.ActiveGenerationId.ToString();
    }
    private static void CaptureUnitsView(string file, Vector3 centre, float size)
    {
        Camera camera = Camera.main; Vector3 old = camera.transform.position; float oldSize = camera.orthographicSize;
        try
        {
            camera.transform.position = new Vector3(centre.x, centre.y, old.z); camera.orthographicSize = size;
            if (!CaptureFrame(file)) throw new InvalidOperationException("Runtime camera frame blank: " + file);
        }
        finally { camera.transform.position = old; camera.orthographicSize = oldSize; }
    }
}
#endif
