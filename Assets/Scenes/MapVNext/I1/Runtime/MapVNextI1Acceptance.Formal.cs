using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MapVNext;

public sealed partial class MapVNextI1Acceptance
{
    private IEnumerator ProveFormalNewGame()
    {
        Require(!MapHostBuildStamp.HasExplicitI1StartupCapability, "FormalEntryMustUseOrdinaryBuild");
        Require(MapVNextI1Bootstrap.FirstEntryPath() == "Assets/Scenes/SampleScene.unity", "FormalEntryMustBeSampleScene");
        Require(startupRows.rows.Count == 2, "FormalStartupBoundariesMissing");
        var before = startupRows.rows[0];
        var after = startupRows.rows[1];
        Require(!before.sessionExists && !after.sessionExists && after.phase == "GateAfterNormalNoOverride",
            "FormalEntryMustPrecedeSessionWithoutPrivateI1Startup");
        Require(before.slots == after.slots && before.legacy == after.legacy &&
            before.preferenceReference == after.preferenceReference, "FormalEntryChangedStorageSelectors");
        Require(after.slots == Path.Combine(Application.persistentDataPath, "tower-run-slots"), "FormalStorageRootMismatch");
        Require(FindObjectOfType<MapVNextI1Bootstrap>() == null && FindObjectOfType<MapVNextI1Panel>() == null,
            "FormalSceneContainsReviewEntry");

        autoDefense = true;
        BindInput();
        yield return CaptureFrame("Screenshots/Formal-Startup.png", "OrdinaryStartup");
        var ui = FindObjectOfType<TowerRunUiController>();
        Require(ui != null, "OrdinaryStartupUiMissing");
        var button = (Button)typeof(TowerRunUiController).GetField("primaryButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        Require(button != null && button.interactable, "OrdinaryNewGameButtonUnavailable");
        button.onClick.Invoke();
        var ordinarySnapshot = Context.CreateDiagnosticSnapshot();
        var ordinary = new InputRow { requestedSeed = ordinarySnapshot.ActiveRequest.RequestedSeed };
        evidence.inputs.Add(ordinary);
        Require(ordinarySnapshot.ActiveRequest.ProfileId == EcologyRecipeDefinition.VNextI1ProfileId, "OrdinaryNewGameStillUsesOldMap");
        RecordFormalReady(ordinary);
        ordinary.status = "ORDINARY_BUTTON_VNEXT_READY";
        ordinary.spawnImage = "Screenshots/Formal-Ordinary-NewGame.png";
        yield return CaptureFrame(ordinary.spawnImage, "OrdinaryNewGameButton");
        SaveAndReopen(2);
        Persist();

        int accepted = 0;
        foreach (int seed in new[] { 21193, 8675309, 7727 })
        {
            session.ReturnToStartupChoice();
            var row = new InputRow { requestedSeed = seed };
            evidence.inputs.Add(row);
            session.BeginNewRunWithSeed(seed);
            if (session.State == TowerRunFlowState.Failure)
            {
                RequireFormalRejection(row);
                Persist();
                continue;
            }
            RecordFormalReady(row);
            accepted++;
            row.spawnImage = "Screenshots/Seed-" + seed + "-Spawn.png";
            row.overviewImage = "Screenshots/Seed-" + seed + "-Overview.png";
            yield return CaptureFrame(row.spawnImage, "FormalSpawn");
            CaptureOverview(row.overviewImage);
            ExportMap(seed);
            SaveAndReopen(2);
            BindPlayer();
            row.movementGeneration = Context.ActiveGenerationId.ToString();
            ExportMap(seed);
            if (seed == 21193)
                yield return ObserveStairWalk(seed, true, false);
            row.status = "FORMAL_DEFAULT_READY_SAVED_REOPENED";
            Persist();
        }
        Require(accepted >= 2, "FormalDefaultNeedsTwoPlayableRepresentativeInputs");
        File.WriteAllText(Path.Combine(output, "Formal-world.txt"), MapHostStateSnapshot.Capture(map));
        evidence.checks.Add("Ordinary SampleScene button selected VNext. Default build kept original storage selectors in a baked shadow namespace. Representative seeds reached GameplayReady, saved and reopened. No I1 review bootstrap or panel.");
        ObserveStartupBoundary("FormalDefaultNewGameVerified");
    }

    private void RecordFormalReady(InputRow row)
    {
        Ready();
        BindPlayer();
        var snapshot = Context.CreateDiagnosticSnapshot();
        var success = snapshot.AttemptChain.Last(a => a.Outcome == MapAttemptOutcome.Succeeded);
        row.actualSeed = success.Attempt.AttemptSeed;
        row.attempt = success.Attempt.AttemptIndex;
        row.profile = snapshot.ActiveRequest.ProfileId;
        row.generation = Context.ActiveGenerationId.ToString();
        row.mapDigest = MapVNextRandomLayout.SemanticHash(Context.ActiveMap);
        row.ecologyIdentity = JsonUtility.ToJson(Entrance().generation.ecology);
        row.monsters = Context.ActiveRegistry.Entries.Count(e => e.Role == MonsterPlacementService.MonsterRole && e.Instance != null);
        row.destructibles = Context.ActiveRegistry.Entries.Count(e => e.Role == DestructiblePlacementService.DestructibleRole && e.Instance != null);
        row.stairs = Context.ActiveMap.Stairs.Count;
        Require(row.monsters == Context.ActivePlacementPlan.Ecology.Residents.Count, "FormalPopulationMismatch");
    }

    private void RequireFormalRejection(InputRow row)
    {
        Require(!Context.IsReady && Context.Failure != null &&
            (Context.Failure.Code == "VNextFormalLayoutRejected" || Context.Failure.Code == "EcologyBudgetExhaustedOnFixedTerrain"),
            "UnexpectedFormalNewGameFailure:" + session.FailureMessage);
        row.profile = Context.CreateDiagnosticSnapshot().ActiveRequest.ProfileId;
        row.reason = Context.Failure.Code + ":" + session.FailureMessage;
        row.status = "BOUNDED_REJECTION_NOT_READY";
    }
}
