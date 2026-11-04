using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class MapVNextI1IntegrationTests
{
    private IEnumerator LoadFormalScene(string name)
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(Root, name));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
#endif
        yield return null;
        coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerRunCoordinator");
        controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        Call("ReturnToStartupChoice");
        Assert.That(TowerRunRuntimeEnvironment.SlotRoot, Does.StartWith(Root));
        Assert.That(UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Any(p =>
            p.GetType().Name == "MapVNextI1Bootstrap" || p.GetType().Name == "MapVNextI1Panel"), Is.False);
    }

    [UnityTest]
    public IEnumerator FormalDefaultOrdinaryNewGameButtonUsesVNext()
    {
        yield return LoadFormalScene("FormalButton");
        var ui = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerRunUiController");
        var button = (Button)ui.GetType().GetField("primaryButton", Private).GetValue(ui);
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        var snapshot = controller.Orchestrator.Context.CreateDiagnosticSnapshot();
        Assert.That(snapshot.ActiveRequest.ProfileId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ProfileId));
        Debug.Log("FORMAL_ORDINARY_BUTTON profile=" + snapshot.ActiveRequest.ProfileId + " state=" + State);
        Ready(true);
        SaveAndReopen(true, "FormalOrdinaryButton");
    }

    [UnityTest]
    public IEnumerator FormalDefaultSeededRunSavesReopensAndPreservesRecordedOldProfile()
    {
        yield return LoadFormalScene("FormalRestore");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        SaveAndReopen(true, "FormalDefault");
        VerifySameSceneOldRoute(controller.Orchestrator.Context);
        var oldRecordField = coordinator.GetType().GetField("currentRecord", Private);
        var oldRecord = (TowerRunCheckpointRecord)oldRecordField.GetValue(coordinator);
        var legacyRecord = oldRecord.DeepClone();
        legacyRecord.schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion;
        legacyRecord.generation.profileId = "default";
        try
        {
            oldRecordField.SetValue(coordinator, legacyRecord);
            var next = (TowerRunGenerationRecord)coordinator.GetType().GetMethod("CreateNextFloorGenerationRecord", Private).Invoke(coordinator, null);
            Assert.That(next.profileId, Is.EqualTo(EcologyRecipeDefinition.MainV1ProfileId), "Legacy-schema floor changes retain their pre-promotion default.");
        }
        finally { oldRecordField.SetValue(coordinator, oldRecord); }
        Call("ReturnToStartupChoice");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        Assert.That(Entrance().generation.profileId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ProfileId));
    }

    [UnityTest]
    public IEnumerator FormalDefaultExplicitRejectedSeedStaysStrictAndRandomOperationRetries()
    {
        yield return LoadFormalScene("FormalRandomRetry");
        const int rejectedSeed = -1254467573;
        LogAssert.Expect(LogType.Error, PlanningRejectionPattern());
        Call("BeginNewRunWithSeed", rejectedSeed);
        Assert.That(State, Is.EqualTo("Failure"), "The recorded rejected seed must remain a strict input.");
        var rejected = controller.Orchestrator.Context.CreateDiagnosticSnapshot();
        Assert.That(rejected.ActiveRequest.RequestedSeed, Is.EqualTo(rejectedSeed));
        Assert.That(rejected.AttemptChain.Count, Is.EqualTo(1));
        var record = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("stagedRecord", Private).GetValue(coordinator)).DeepClone();
        var kind = coordinator.GetType().GetField("operationKind", Private).GetValue(coordinator);
        var previousFailure = controller.Orchestrator.Context.Failure;
        var settingsField = typeof(MapGenerationController).GetField("settings", Private);
        var originalSettings = settingsField.GetValue(controller);
        try
        {
            settingsField.SetValue(controller, null);
            LogAssert.Expect(LogType.Error, "MapGenerationController is missing MapGenerationSettings.");
            coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
            Assert.That(State, Is.EqualTo("Failure"));
            Assert.That(controller.Orchestrator.Context.Failure, Is.SameAs(previousFailure), "An early configuration return retains the prior diagnostic and must not trigger random retries.");
        }
        finally { settingsField.SetValue(controller, originalSettings); }
        Call("ReturnToStartupChoice");
        coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
        Ready(true);
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed, Is.Not.EqualTo(rejectedSeed));
        SaveAndReopen(true, "FormalRandomRetry");
    }

    [UnityTest]
    public IEnumerator FormalRandomRetriesKeepRejectedCandidatesOutOfErrorLog()
    {
        yield return LoadFormalScene("FormalRetryLogs");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        // Replay the user's four rejected inputs and successful fifth input through the real retry loop.
        BeginRecordedRandomCandidates(-367763121, -1644553391, -93201598, 887863686, 666268681);
        Ready(true);
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed,
            Is.EqualTo(666268681));
        SaveAndReopen(true, "FormalRetryLogs");
    }

    [UnityTest]
    public IEnumerator FormalRandomRetryExhaustionReportsOneErrorAndExplicitRetryRemainsStrict()
    {
        yield return LoadFormalScene("FormalRetryExhausted");
        Call("BeginNewRunWithSeed", 21193);
        Ready(true);
        const int rejectedSeed = -1254467573;
        LogAssert.Expect(LogType.Error, new Regex(@"^Random map generation failed after 12 candidate inputs \[(VNextFormalLayoutRejected|EcologyBudgetExhaustedOnFixedTerrain)\]:"));
        BeginRecordedRandomCandidates(Enumerable.Repeat(rejectedSeed, 12).ToArray());
        Assert.That(State, Is.EqualTo("Failure"));
        Assert.That(controller.Orchestrator.Context.IsReady, Is.False);
        Assert.That(controller.Orchestrator.Context.Failure, Is.Not.Null);
        var queue = (Queue<int>)coordinator.GetType().GetField("nextFloorInputs", Private).GetValue(coordinator);
        Assert.That(queue.Count, Is.Zero, "Exactly twelve candidate inputs must exhaust the existing budget.");

        Call("ReturnToStartupChoice");
        LogAssert.Expect(LogType.Error, PlanningRejectionPattern());
        Call("BeginNewRunWithSeed", rejectedSeed);
        Assert.That(State, Is.EqualTo("Failure"));
        Assert.That(controller.Orchestrator.Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed,
            Is.EqualTo(rejectedSeed));
    }

    private void BeginRecordedRandomCandidates(params int[] seeds)
    {
        var record = Entrance().DeepClone();
        record.schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion;
        record.generation.requestedSeed = seeds[0];
        record.generation.attemptIndex = 0;
        record.generation.staticBaselineSignature = string.Empty;
        record.generation.ecology = null;
        var queueField = coordinator.GetType().GetField("nextFloorInputs", Private);
        // Fixture injection only: production fixed-input queues keep their existing four-input limit and route.
        queueField.SetValue(coordinator, new Queue<int>(seeds.Skip(1)));
        var kindField = coordinator.GetType().GetField("operationKind", Private);
        var kind = Enum.Parse(kindField.FieldType, "FloorTransition");
        coordinator.GetType().GetMethod("BeginRandomOperation", Private).Invoke(coordinator, new[] { kind, record });
    }

    private static Regex PlanningRejectionPattern()
    {
        return new Regex(@"^Map generation failed \[(VNextFormalLayoutRejected|EcologyBudgetExhaustedOnFixedTerrain)\]:");
    }
}
