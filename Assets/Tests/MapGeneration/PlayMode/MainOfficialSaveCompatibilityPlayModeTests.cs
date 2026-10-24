using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class MainOfficialSaveCompatibilityPlayModeTests
{
    private MonoBehaviour coordinator;
    private object Call(string name, params object[] args) => coordinator.GetType().GetMethod(name).Invoke(coordinator, args);
    [Test] public void NormalDefaultsKeepOfficialDirectoryAndLegacySidecarVisible()
    {
        TowerRunRuntimeEnvironment.ResetOverrides();
        Assert.That(TowerRunRuntimeEnvironment.SlotRoot, Is.EqualTo(Path.Combine(Application.persistentDataPath, "tower-run-slots")));
        Assert.That(TowerRunRuntimeEnvironment.LegacySavePath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "save.json")));
    }
    [UnityTest, Category("OfficialSaveFixture")]
    public IEnumerator ThreeRealDefaultCheckpointClonesRestoreRuntimeAndCommitExactDataWithoutWritingTheirSources()
    {
        string fixtures = Environment.GetEnvironmentVariable("RPG_DEMO_OFFICIAL_SLOT_FIXTURES");
        string evidence = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Assert.That(fixtures, Is.Not.Null.And.Not.Empty, "Requires immutable copied official slots; never point tests at original user slots.");
        string[] files = Directory.GetFiles(fixtures, "*.json").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.That(files.Length, Is.EqualTo(3));
        for (int i = 0; i < files.Length; i++)
        {
            byte[] before = File.ReadAllBytes(files[i]);
            var expected = JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(before));
            Assert.That(expected.generation.profileId, Is.EqualTo("default"));
            TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(evidence, "OfficialClone" + i));
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); yield return null;
            coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(x => x.GetType().Name == "TowerRunCoordinator");
            Call("ReturnToStartupChoice");
            Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);
            string slot = Path.Combine(TowerRunRuntimeEnvironment.SlotRoot, "tower-run-slot-1.json"); File.WriteAllBytes(slot, before);
            var read = (TowerRunSlotReadResult)Call("ReadSlot", 1);
            Assert.That(read.state, Is.EqualTo(TowerRunSlotState.Valid), read.message);
            Call("BeginRestoreSlot", 1, read.confirmationToken); yield return null;
            Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Playing"));
            object[] args = { null, null }; Assert.That((bool)Call("TryCreateSaveRequest", args), Is.True);
            var restored = (TowerRunCheckpointRecord)args[0];
            Assert.That(JsonUtility.ToJson(restored.generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
            Assert.That(restored.runId, Is.EqualTo(expected.runId));
            Assert.That(restored.floor.floorNumber, Is.EqualTo(expected.floor.floorNumber));
            // Capture the bound runtime components, independently of the staged entrance record.
            var assembly = coordinator.GetType().Assembly;
            var saveService = assembly.GetType("GameSaveService", true);
            object runtimeData = saveService.GetMethod("GetOrCreateData").Invoke(null, null);
            string runtimeName = (string)runtimeData.GetType().GetField("playerName").GetValue(runtimeData);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            object[] capture = { expected.runId, runtimeName, expected.floor, expected.generation,
                coordinator.GetType().GetField("stats", fields).GetValue(coordinator),
                coordinator.GetType().GetField("inventory", fields).GetValue(coordinator),
                coordinator.GetType().GetField("shops", fields).GetValue(coordinator), null, null };
            Assert.That((bool)assembly.GetType("TowerRunRuntimeStateBridge", true).GetMethod("TryCapture").Invoke(null, capture), Is.True, (string)capture[8]);
            var live = (TowerRunCheckpointRecord)capture[7];
            Assert.That(live.playerName, Is.EqualTo(expected.playerName));
            AssertPlayerData(live, expected);
            AssertPlayerData(restored, live);
            var committed = (TowerRunSlotWriteResult)Call("TrySaveEntranceCandidate", 2, restored, false, null);
            Assert.That(committed.succeeded, Is.True, committed.message);
            var reread = (TowerRunSlotReadResult)Call("ReadSlot", 2);
            Assert.That(reread.state, Is.EqualTo(TowerRunSlotState.Valid), reread.message);
            Assert.That(reread.record.runId, Is.EqualTo(expected.runId));
            Assert.That(reread.record.playerName, Is.EqualTo(expected.playerName));
            Assert.That(JsonUtility.ToJson(reread.record.generation), Is.EqualTo(JsonUtility.ToJson(expected.generation)));
            AssertPlayerData(reread.record, live);
            Assert.That(reread.record.savedAtUtc, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(slot), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(files[i]), Is.EqualTo(before));
            Debug.Log("MAIN_OFFICIAL_CLONE_PASS index=" + i + " baseline=" + restored.generation.staticBaselineSignature + " floor=" + restored.floor.floorNumber);
            Call("ReturnToStartupChoice");
        }
    }
    private static void AssertPlayerData(TowerRunCheckpointRecord actual, TowerRunCheckpointRecord expected)
    {
        Assert.That(JsonUtility.ToJson(actual.player.stats), Is.EqualTo(JsonUtility.ToJson(expected.player.stats)));
        Assert.That(actual.player.inventory.OrderBy(x => x.rewardId).Select(x => x.rewardId + "=" + x.amount),
            Is.EqualTo(expected.player.inventory.OrderBy(x => x.rewardId).Select(x => x.rewardId + "=" + x.amount)));
        Assert.That(actual.player.shops.OrderBy(x => x.catalogId).SelectMany(x => x.productStocks.OrderBy(y => y.productId).Select(y => x.catalogId + "/" + y.productId + "=" + y.quantity)),
            Is.EqualTo(expected.player.shops.OrderBy(x => x.catalogId).SelectMany(x => x.productStocks.OrderBy(y => y.productId).Select(y => x.catalogId + "/" + y.productId + "=" + y.quantity))));
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (coordinator != null) Call("ReturnToStartupChoice");
        TowerRunRuntimeEnvironment.ResetOverrides(); yield return null;
    }
}
