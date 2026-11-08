using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Reuse the existing production coordinator/scene/isolation helpers in this partial test class.
public sealed partial class MapVNextI1IntegrationTests
{
    [UnityTest] public IEnumerator GroundConnectivityCurrent7727RemainsStrictlyRejected()
    {
        yield return LoadFormalScene("GroundConnectivityCurrent7727");
        LogAssert.Expect(LogType.Error, new Regex(@"^Map generation failed \[EcologyBudgetExhaustedOnFixedTerrain\]:"));
        Call("BeginNewRunWithSeed", 7727);
        Assert.That(State, Is.EqualTo("Failure"));
        Assert.That(controller.Orchestrator.Context.IsReady, Is.False);
        var snapshot = controller.Orchestrator.Context.CreateDiagnosticSnapshot();
        Assert.That(snapshot.ActiveRequest.RequestedSeed, Is.EqualTo(7727));
        Assert.That(snapshot.ActiveRequest.EcologyEntry, Is.Null, "An explicit new run must not borrow a legacy save identity.");
        Assert.That(snapshot.AttemptChain.Count, Is.EqualTo(1), "Strict input cannot silently consume a second seed.");
        Assert.That(controller.Orchestrator.Context.Failure.Code, Is.EqualTo("EcologyBudgetExhaustedOnFixedTerrain"));
    }

    [UnityTest] public IEnumerator GroundConnectivityOldCheckpointCopiesRestoreExactLegacyIdentity()
    {
        string directory = Environment.GetEnvironmentVariable("RPG_MAP_OLD_SAVE_FIXTURES");
        Assert.That(directory, Is.Not.Null.And.Not.Empty);
        string[] inputs = Directory.GetFiles(directory, "*-checkpoint.json").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.That(inputs.Length, Is.EqualTo(5));
        // Caller binds the five original on-disk slot copies; raw Entrance checkpoints are not persisted saves.
        for (int index = 0; index < inputs.Length; index++)
        {
            yield return Load("GroundCompatibility-" + index);
            byte[] original = File.ReadAllBytes(inputs[index]);
            TowerRunCheckpointRecord expected = JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(original));
            Assert.That(expected.savedAtUtc, Is.Not.Null.And.Not.Empty, "Only original persisted slot records are valid restore fixtures.");
            Assert.That(expected.generation.ecology.contentVersion, Is.EqualTo(MapHostBuildStamp.PreviousCurrentContentVersion));
            Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);
            File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json"), original);
            Restore(1); Ready(true);
            TowerRunCheckpointRecord actual = Entrance();
            Assert.That(JsonUtility.ToJson(actual.generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)), "TRB3, attempt and all ecology identities must remain exact.");
            Assert.That(controller.Orchestrator.Context.ActiveSpatialContext.ImplementationId, Is.EqualTo(EcologyRecipeDefinition.VNextI1ImplementationId));
            Assert.That(File.ReadAllBytes(inputs[index]), Is.EqualTo(original));
            File.WriteAllText(Path.Combine(Output, "GroundCompatibility-" + index + "-checkpoint.json"), JsonUtility.ToJson(actual, true));
            Debug.Log("GROUND_LEGACY_RESTORE seed=" + expected.generation.requestedSeed + " baseline=" + expected.generation.staticBaselineSignature);
        }
    }
}
