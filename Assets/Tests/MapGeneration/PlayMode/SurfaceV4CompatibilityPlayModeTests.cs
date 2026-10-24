using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SurfaceV4CompatibilityPlayModeTests
{
    private MonoBehaviour coordinator;
    [UnityTest] public IEnumerator MainExactSlotRestoresAndArchivedProfileIdentitiesRejectWithoutChangingBytes()
    {
        string evidence = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Assert.That(evidence, Is.Not.Null.And.Not.Empty);
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(evidence, "CompatibilitySlots"));
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); yield return null;
        coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(x => x.GetType().Name == "TowerRunCoordinator");
        Call("ReturnToStartupChoice"); Call("BeginNewRun"); yield return null;
        Assert.That(Get("State").ToString(), Is.EqualTo("Playing"));
        object[] arguments = { null, null };
        Assert.That((bool)Call("TryCreateSaveRequest", arguments), Is.True);
        var original = (TowerRunCheckpointRecord)arguments[0];
        Assert.That(original.generation.profileId, Is.EqualTo(EcologyRecipeDefinition.MainV1ProfileId));
        var store = (TowerRunSlotStore)coordinator.GetType().GetField("slotStore", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
        var write = store.TryCommit(1, original, false); Assert.That(write.succeeded, Is.True, write.message);
        string r3Path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json");
        byte[] r3Bytes = File.ReadAllBytes(r3Path);
        var read = store.ReadSlot(1); Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid), read.message);
        Call("ReturnToStartupChoice"); Call("BeginRestoreSlot", 1, read.confirmationToken); yield return null;
        Assert.That(Get("State").ToString(), Is.EqualTo("Playing"));
        object[] restoredArguments = { null, null }; Assert.That((bool)Call("TryCreateSaveRequest", restoredArguments), Is.True);
        var restored = (TowerRunCheckpointRecord)restoredArguments[0];
        Assert.That(JsonUtility.ToJson(restored.generation), Is.EqualTo(JsonUtility.ToJson(original.generation)));
        Assert.That(File.ReadAllBytes(r3Path), Is.EqualTo(r3Bytes));
        var controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        int published = 0; controller.Orchestrator.GameplayReady += _ => published++;
        foreach (string profile in new[] { "terrain-v2-xiaosu-v1", "ecology-zoning-v1", "ecology-zoning-units-candidate-v1", "ecology-zoning-units-float32-candidate-v2" })
        {
            // Deliberately arranged legacy identity over a complete checkpoint. This is a
            // rejection boundary fixture, not a fabricated old save or an upgrade/migration.
            var fixture = original.DeepClone(); fixture.generation.profileId = profile;
            string path = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-2.json");
            File.WriteAllText(path, JsonUtility.ToJson(fixture)); byte[] before = File.ReadAllBytes(path);
            var rejected = store.ReadSlot(2);
            Assert.That(rejected.state, Is.EqualTo(TowerRunSlotState.Incompatible));
            Assert.That(rejected.message, Is.EqualTo(EcologyRecipeDefinition.RetiredContentMessage));
            Call("ReturnToStartupChoice"); Call("BeginRestoreSlot", 2, "rejected-fixture-no-token"); yield return null;
            Assert.That(Get("State").ToString(), Is.EqualTo("Failure"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), profile + " must not overwrite or migrate its bytes.");
            Assert.That(published, Is.Zero, profile + " cannot publish a replacement R4 generation.");
            Debug.Log("REMEDIATION_RETIRED_SLOT profile=" + profile + " state=Incompatible bytesUnchanged=True replacementPublications=0 fixture=ARRANGED_IDENTITY");
        }
        var originalR4 = original.DeepClone(); originalR4.generation.profileId = EcologyRecipeDefinition.SurfaceV4ProfileId;
        string r4ArchivePath = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-2.json");
        File.WriteAllText(r4ArchivePath, JsonUtility.ToJson(originalR4)); byte[] r4ArchiveBytes = File.ReadAllBytes(r4ArchivePath);
        var oldR4 = store.ReadSlot(2);
        Assert.That(oldR4.state, Is.EqualTo(TowerRunSlotState.Incompatible));
        Assert.That(oldR4.message, Is.EqualTo(EcologyRecipeDefinition.LegacyR4ArchiveOnlyMessage));
        Call("ReturnToStartupChoice"); Call("BeginRestoreSlot", 2, "archive-only-r4-no-token"); yield return null;
        Assert.That(Get("State").ToString(), Is.EqualTo("Failure"));
        Assert.That(File.ReadAllBytes(r4ArchivePath), Is.EqualTo(r4ArchiveBytes)); Assert.That(published, Is.Zero);
        string rawR3 = File.ReadAllText(Environment.GetEnvironmentVariable("RPG_DEMO_R3_ARCHIVE_SAMPLE"));
        string oldPath = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-3.json");
        File.WriteAllText(oldPath, rawR3); byte[] oldBytes = File.ReadAllBytes(oldPath);
        var archived = store.ReadSlot(3);
        Assert.That(archived.state, Is.EqualTo(TowerRunSlotState.Incompatible));
        Assert.That(archived.message, Is.EqualTo(EcologyRecipeDefinition.LegacyR3ArchiveOnlyMessage));
        Call("ReturnToStartupChoice"); Call("BeginRestoreSlot", 3, "archive-only-no-token"); yield return null;
        Assert.That(Get("State").ToString(), Is.EqualTo("Failure"));
        Assert.That(Get("FailureMessage").ToString(), Is.EqualTo(EcologyRecipeDefinition.LegacyR3ArchiveOnlyMessage));
        Assert.That(File.ReadAllBytes(oldPath), Is.EqualTo(oldBytes)); Assert.That(published, Is.Zero);
        Debug.Log("LEGACY_R3_ARCHIVE_ONLY rawSourceUnchanged=True replacementPublications=0 fourRetiredPolicySeparate=True");
        foreach (string archivedProfile in new[] { EcologyRecipeDefinition.RemediationProfileId, EcologyRecipeDefinition.SurfaceV4ProfileId })
        {
            string message = EcologyRecipeDefinition.ArchiveOnlyMessage(archivedProfile);
            LogAssert.Expect(LogType.Error, "Map integration failed [ArchivedEcologyContent]: " + message);
            Assert.That(controller.TryGenerateMap(new MapGenerationRequest(Guid.NewGuid(), 20260929, archivedProfile)), Is.False);
            Assert.That(published, Is.Zero, "A direct request must not bypass the archive-only slot policy.");
            var archivedRecipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(archivedProfile));
            var failure = Assert.Throws<InvalidOperationException>(() => archivedRecipe.ResolveTypes());
            Assert.That(failure.Message, Is.EqualTo(message));
        }
        Debug.Log("SURFACE_R4_EXACT_RESTORE profile=" + original.generation.profileId + " layout=" + original.generation.ecology.layoutDigest + " bytesUnchanged=True");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        TowerRunRuntimeEnvironment.ResetOverrides(); yield return null;
    }
    private object Get(string name) => coordinator.GetType().GetProperty(name).GetValue(coordinator);
    private object Call(string name, params object[] args) => coordinator.GetType().GetMethod(name).Invoke(coordinator, args);
}
