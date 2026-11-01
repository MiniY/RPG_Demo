using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [Serializable] private sealed class LegacyLifecycleProof
    {
        public string scope = "CONTROLLED_ORIGINAL_DAMAGE_AND_TRANSITION_HANDLERS_NOT_NATURAL_COMBAT_OR_F_INPUT";
        public string buildId, input, copiedSlot, originalGeneration, deathRestoreGeneration, floorGeneration;
        public string profile, contentVersion, runId, deathDiskSlot, floorDiskSlot;
        public int initialFloor, nextFloor;
        public bool originalGeometryWithoutVNextR18, retiredDeathRegistry, retiredFloorRegistry, sourceBytesUnchanged;
        public List<string> boundedTransitionRejections = new List<string>();
    }

    [UnityTest] public IEnumerator MainV1CopiedCheckpointDeathTransitionSaveReopenUseOriginalRules()
    {
        yield return Load("MainV1-Lifecycle");
        string input = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/OldSaveCopies/MainV1-P02/tower-run-slot-1.json"));
        byte[] original = File.ReadAllBytes(input);
        var expected = JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(original));
        Assert.That(expected.generation.profileId, Is.EqualTo("ecology-zoning-units-surface-v4-main-v1"));
        Assert.That(expected.generation.ecology.contentVersion, Is.EqualTo(MapHostBuildStamp.OldContentVersion));
        string copiedSlot = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json");
        Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot); File.WriteAllBytes(copiedSlot, original);
        Restore(1); Ready(false);
        var context = controller.Orchestrator.Context;
        var proof = new LegacyLifecycleProof { buildId = MapHostBuildStamp.BuildId, input = input, copiedSlot = copiedSlot,
            originalGeneration = context.ActiveGenerationId.Value.ToString("D"), profile = expected.generation.profileId,
            contentVersion = expected.generation.ecology.contentVersion, runId = expected.runId, initialFloor = expected.floor.floorNumber };
        Assert.That(JsonUtility.ToJson(Entrance().generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
        MapObjectRegistry deathRegistry = context.ActiveRegistry;
        MonoBehaviour damage = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "PlayerDamageController");
        var receiver = damage.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(Transform) });
        Assert.That(receiver, Is.Not.Null); receiver.Invoke(damage, new object[] { float.MaxValue, null });
        yield return null;
        Assert.That(State, Is.EqualTo("DeathChoice"), "Original damage receiver must cause real death flow before restoration.");
        Restore(1); Ready(false);
        Assert.That(deathRegistry.IsRetired, Is.True);
        proof.retiredDeathRegistry = deathRegistry.IsRetired;
        proof.deathRestoreGeneration = context.ActiveGenerationId.Value.ToString("D");
        Assert.That(proof.deathRestoreGeneration, Is.Not.EqualTo(proof.originalGeneration));
        SaveAndReopen(false, "MainV1-Lifecycle-AfterDeath", 2);
        var afterDeath = Entrance();
        Assert.That(afterDeath.runId, Is.EqualTo(expected.runId));
        Assert.That(afterDeath.floor.floorNumber, Is.EqualTo(expected.floor.floorNumber));
        Assert.That(JsonUtility.ToJson(afterDeath.generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
        proof.deathDiskSlot = VerifyLegacyLifecycleDisk(2, afterDeath);

        // Original receivers and transition handler exercise lifecycle compatibility, not player combat or F travel.
        MapObjectRegistry previousRegistry = context.ActiveRegistry; Guid previousGeneration = context.ActiveGenerationId.Value;
        foreach (var entry in context.ActiveRegistry.Entries.Where(e => e.Role == MonsterPlacementService.MonsterRole).ToArray())
        {
            var target = (Component)entry.Instance;
            target.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(target, new object[] { float.MaxValue });
        }
        var floor = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(p => p.GetType().Name == "TowerFloorRuntimeController");
        Assert.That(floor.GetType().GetProperty("State").GetValue(floor).ToString(), Is.EqualTo("Available"));
        Application.LogCallback record = (message, stack, kind) =>
        {
            if (kind == LogType.Error && message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:", StringComparison.Ordinal))
            { proof.boundedTransitionRejections.Add(message); LogAssert.Expect(LogType.Error, message); }
        };
        Application.logMessageReceived += record;
        try
        {
            coordinator.GetType().GetMethod("HandleExitUseRequested", Private).Invoke(coordinator, new object[] { previousGeneration });
            for (int retry = 0; !context.IsReady && retry < 3; retry++)
            {
                Assert.That(context.Failure.Code, Is.EqualTo("EcologyBudgetExhaustedOnFixedTerrain"));
                Call("RetryFailedOperation");
            }
        }
        finally { Application.logMessageReceived -= record; }
        Ready(false);
        Assert.That(previousRegistry.IsRetired, Is.True);
        Assert.That(context.ActiveGenerationId.Value, Is.Not.EqualTo(previousGeneration));
        var next = Entrance();
        Assert.That(next.generation.profileId, Is.EqualTo(expected.generation.profileId));
        Assert.That(next.generation.ecology.contentVersion, Is.EqualTo(expected.generation.ecology.contentVersion));
        Assert.That(next.runId, Is.EqualTo(expected.runId));
        Assert.That(next.floor.floorNumber, Is.EqualTo(expected.floor.floorNumber + 1));
        proof.floorGeneration = context.ActiveGenerationId.Value.ToString("D");
        proof.nextFloor = next.floor.floorNumber; proof.retiredFloorRegistry = previousRegistry.IsRetired;
        SaveAndReopen(false, "MainV1-Lifecycle-AfterFloor", 3);
        var reopened = Entrance();
        Assert.That(reopened.runId, Is.EqualTo(next.runId));
        Assert.That(JsonUtility.ToJson(reopened.floor), Is.EqualTo(JsonUtility.ToJson(next.floor)));
        proof.floorDiskSlot = VerifyLegacyLifecycleDisk(3, reopened);
        Assert.That(File.ReadAllBytes(input), Is.EqualTo(original));
        Assert.That(File.ReadAllBytes(copiedSlot), Is.EqualTo(original));
        Assert.That(context.ActiveSpatialContext, Is.Null);
        proof.sourceBytesUnchanged = true; proof.originalGeometryWithoutVNextR18 = true;
        File.WriteAllText(Path.Combine(Output, "MainV1-Lifecycle-proof.json"), JsonUtility.ToJson(proof, true));
        Debug.Log("I1_MAINV1_DEATH_FLOOR_SAVED_REOPENED originalGeometry=True newVNextR18=False migration=False floor=" + proof.nextFloor);
    }

    private string VerifyLegacyLifecycleDisk(int slot, TowerRunCheckpointRecord expected)
    {
        string path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-" + slot + ".json");
        var disk = JsonUtility.FromJson<TowerRunCheckpointRecord>(File.ReadAllText(path));
        Assert.That(disk.savedAtUtc, Is.Not.Empty);
        Assert.That(disk.runId, Is.EqualTo(expected.runId));
        Assert.That(JsonUtility.ToJson(disk.floor), Is.EqualTo(JsonUtility.ToJson(expected.floor)));
        Assert.That(JsonUtility.ToJson(disk.generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
        Assert.That(JsonUtility.ToJson(disk.player), Is.EqualTo(JsonUtility.ToJson(expected.player)));
        File.Copy(path, Path.Combine(Output, "MainV1-Lifecycle-DiskSlot" + slot + ".json"), false);
        return path;
    }
}
