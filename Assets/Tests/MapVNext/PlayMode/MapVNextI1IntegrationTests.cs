using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Actual production coordinator, retained owners and Ready finalizers, with copied data only.</summary>
public sealed partial class MapVNextI1IntegrationTests
{
    private const string Scene = "Assets/Scenes/MapVNext/I1/I1Game.unity";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string Output => Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");
    private string Root => Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT");
    private MonoBehaviour coordinator;
    private MapGenerationController controller;
    private object Call(string method, params object[] args) => coordinator.GetType().GetMethod(method).Invoke(coordinator, args);
    private string State => coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString();
    private IEnumerator Load(string name)
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(Root, name));
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
#else
        yield return SceneManager.LoadSceneAsync("I1Game", LoadSceneMode.Single);
#endif
        yield return null;
        coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerRunCoordinator");
        controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        Call("ReturnToStartupChoice");
        Assert.That(TowerRunRuntimeEnvironment.SlotRoot, Does.StartWith(Root));
    }
    private void Ready(bool versioned)
    {
        Assert.That(State, Is.EqualTo("Playing"), (string)coordinator.GetType().GetProperty("FailureMessage").GetValue(coordinator));
        var context = controller.Orchestrator.Context;
        Assert.That(context.IsReady, Is.True, context.CreateDiagnosticSnapshot().StateSummary);
        Assert.That(context.ActiveSpatialContext != null, Is.EqualTo(versioned));
        Assert.That(context.ActiveRegistry.IsRetired, Is.False);
        foreach (var entry in context.ActiveRegistry.Entries)
        {
            Assert.That(entry.GenerationId, Is.EqualTo(context.ActiveGenerationId));
            var component = entry.Instance as Component;
            if (component != null && versioned)
                Assert.That(MapSpatialPlacement.CanPlace(context, context.ActiveGenerationId.Value, component.transform, component.transform.position, out string reason), Is.True, entry.LogicalObjectId + ":" + reason);
        }
    }
    private TowerRunCheckpointRecord Entrance()
    {
        object[] args = { null, null }; Assert.That((bool)Call("TryCreateSaveRequest", args), Is.True, args[1] as string);
        return (TowerRunCheckpointRecord)args[0];
    }
    private void Restore(int slot)
    {
        Call("ReturnToStartupChoice"); var read = (TowerRunSlotReadResult)Call("ReadSlot", slot);
        Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid), read.message);
        Call("BeginRestoreSlot", slot, read.confirmationToken);
    }
    private void SaveAndReopen(bool versioned, string name, int slot = 2, bool overwrite = false)
    {
        var first = Entrance(); string before = Snapshot();
        var existing = overwrite ? (TowerRunSlotReadResult)Call("ReadSlot", slot) : null;
        var write = (TowerRunSlotWriteResult)Call("TrySaveEntranceCandidate", slot, first, overwrite, existing?.confirmationToken);
        Assert.That(write.succeeded, Is.True, write.message);
        Restore(slot); Ready(versioned); var reopened = Entrance();
        Assert.That(JsonUtility.ToJson(reopened.generation), Is.EqualTo(JsonUtility.ToJson(first.generation)), "All generation and ecology fields must remain exact.");
        Assert.That(JsonUtility.ToJson(reopened.player), Is.EqualTo(JsonUtility.ToJson(first.player)));
        Assert.That(Snapshot(), Is.EqualTo(before), "Terrain, stairs, positions, collision declarations, plan occupancy and registry must match.");
        File.WriteAllText(Path.Combine(Output, name + "-checkpoint.json"), JsonUtility.ToJson(reopened, true));
        File.WriteAllText(Path.Combine(Output, name + "-world.txt"), before);
    }
    private string Snapshot() => MapHostStateSnapshot.Capture(controller);
    [UnityTest] public IEnumerator MainV1CopiedCheckpointsUseOriginalRouteAndActuallyReopen()
    {
        string fixtures = Path.Combine(Application.dataPath, "../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/OldSaveCopies/MainV1-P02");
        string[] inputs = Directory.GetFiles(fixtures, "tower-run-slot-*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray(); Assert.That(inputs.Length, Is.EqualTo(3));
        for (int i = 0; i < inputs.Length; i++)
        {
            yield return Load("MainV1-" + i);
            byte[] original = File.ReadAllBytes(inputs[i]); var expected = JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(original));
            Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot); File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json"), original);
            Restore(1); Ready(false);
            Assert.That(JsonUtility.ToJson(Entrance().generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
            SaveAndReopen(false, "MainV1-" + i); Assert.That(File.ReadAllBytes(inputs[i]), Is.EqualTo(original));
            Debug.Log("I1_MAINV1_READY_REOPEN index=" + i + " baseline=" + expected.generation.staticBaselineSignature + " exactEcology=" + JsonUtility.ToJson(expected.generation.ecology));
        }
    }
    [Serializable] private sealed class Trial { public int seed, population; public string status, reason, implementation; }
    [Serializable] private sealed class Trials { public string scope="FOUR_FIXED_INPUTS_REJECTIONS_RETAINED"; public List<Trial> inputs=new List<Trial>(); }
    [UnityTest] public IEnumerator VNextFourFixedInputsRetainRejectionsAndVerifyActualReadyRestoreAndVersionSwitch()
    {
        var report=new Trials(); int accepted=0;var unexpected=new List<string>();
        foreach(int seed in new[] {42,21193,7727,314159})
        {
            yield return Load("VNext-"+seed);
            var trial=new Trial {seed=seed,implementation=EcologyRecipeDefinition.VNextI1ImplementationId};report.inputs.Add(trial);
            Application.LogCallback record=(message,stack,type)=>
            {
                if(type!=LogType.Error||!message.StartsWith("Map generation failed [",StringComparison.Ordinal))return;
                trial.reason=message;LogAssert.Expect(LogType.Error,message);
            };
            Application.logMessageReceived+=record;
            try {Call("BeginNewRunWithSeed",seed);}
            finally {Application.logMessageReceived-=record;}
            if(State=="Failure")
            {
                trial.reason=trial.reason??((string)coordinator.GetType().GetProperty("FailureMessage").GetValue(coordinator));
                bool finite=trial.reason!=null&&(trial.reason.StartsWith("Map generation failed [VNextFormalLayoutRejected]:",StringComparison.Ordinal)||trial.reason.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:",StringComparison.Ordinal));
                trial.status=finite?"BOUNDED_REJECTION_NOT_READY":"IMPLEMENTATION_FAILURE_NOT_READY";
                if(!finite)unexpected.Add(seed+":"+trial.reason);
                Assert.That(controller.Orchestrator.Context.IsReady,Is.False);
                File.WriteAllText(Path.Combine(Output,"formal-four-inputs.json"),JsonUtility.ToJson(report,true));
                continue;
            }
            Ready(true); SaveAndReopen(true,"VNext-"+seed);accepted++;
            var context=controller.Orchestrator.Context;
            Assert.That(context.ActiveMap.Stairs.Count,Is.GreaterThan(0));
            var foot=((IEcologyMapSession)coordinator).PlayerMovementFoot;
            var graph=context.ActiveSpatialGeometry.BuildNavigationGraph(foot,0);
            foreach(var link in context.ActiveSpatialContext.NavigationLinks)Assert.That(graph.FindLink(link.From,link.To),Is.Not.Null);
            trial.population=context.ActivePlacementPlan.Ecology.Residents.Count;trial.status="READY_SAVED_REOPENED";
            File.WriteAllText(Path.Combine(Output,"formal-four-inputs.json"),JsonUtility.ToJson(report,true));
            Debug.Log("I1_VNEXT_FORMAL_READY_REOPEN seed="+seed+" population="+trial.population+" profile="+Entrance().generation.profileId);
            VerifySameSceneOldRoute(context);
        }
        Assert.That(unexpected,Is.Empty,"Unexpected generation or implementation failures are not accepted as reasonable rejections.");
        Assert.That(accepted,Is.GreaterThanOrEqualTo(2),"A playable delivery needs actual successful production inputs; rejection is not Ready.");
    }
    private void VerifySameSceneOldRoute(MapRuntimeContext context)
    {
        Guid previousGeneration = context.ActiveGenerationId.Value;
        var retiredSpace = context.ActiveSpatialContext;
        string oldFixture = Path.Combine(Application.dataPath, "../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/OldSaveCopies/MainV1-P02/tower-run-slot-1.json");
        File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json"), File.ReadAllBytes(oldFixture));
        Restore(1); Ready(false);
        Assert.That(context.ActiveGenerationId, Is.Not.EqualTo(previousGeneration));
        Assert.That(MapSpatialPlacement.CanPlace(context, previousGeneration, controller.Player, controller.Player.position, out _), Is.False);
        Assert.Throws<InvalidOperationException>(() => retiredSpace.Require(context.ActiveGenerationId.Value, context.ActiveMap));
        foreach (var entry in context.ActiveRegistry.Entries.Where(r => r.Role == AnimalPlacementService.AnimalRole))
        {
            var actor = (entry.Instance as Component)?.GetComponent<MapSpatialActor>();
            if (actor != null) Assert.That(actor.CanMove(actor.transform.position, actor.transform.position), Is.True, "Reused original animal must use the current MainV1 rule.");
        }
        Debug.Log("I1_SAME_SCENE_VNEXT_TO_MAINV1_OLD_LEASE_REJECTED_CURRENT_ANIMAL_REBOUND");
    }
    [UnityTearDown] public IEnumerator Finish()
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        yield return null;
    }
}
