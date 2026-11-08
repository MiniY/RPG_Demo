using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ConsumablesIntegrationPlayTests
{
    private Component session, inventory, stats;
    private object flow;
    private MapGenerationController map;
    private string saves;
    private readonly List<GameObject> owned = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        saves = ConsumableReflection.PrivateRoot();
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(saves);
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
        session = ConsumableReflection.Session;
        inventory = (Component)ConsumableReflection.Read(session, "Inventory");
        stats = (Component)ConsumableReflection.Read(session, "PlayerStats");
        flow = ConsumableReflection.Read(session, "TowerRun");
        map = UnityEngine.Object.FindObjectsOfType<MapGenerationController>().Single();
        Assert.That(inventory, Is.Not.Null);
        Assert.That(ConsumableReflection.Read(session, "PlayerBindingFailure"), Is.EqualTo(string.Empty));
        ConsumableReflection.Call(flow, "ReturnToStartupChoice");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
            if (session != null) ConsumableReflection.Call(session, "SaveRuntimeState");
            if (flow != null) ConsumableReflection.Call(flow, "ReturnToStartupChoice");
            if (session != null) ConsumableReflection.Call(session, "BindSceneObjects");
        }
        finally
        {
            TowerRunRuntimeEnvironment.ResetOverrides();
        }
    }

    private IEnumerator Begin(int seed)
    {
        ConsumableReflection.Call(flow, "BeginNewRunWithSeed", seed);
        yield return null;
        Assert.That(ConsumableReflection.Read(flow, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(map.Orchestrator.Context.IsReady, Is.True);
    }

    [UnityTest]
    public IEnumerator AuthoredRightClickDropsOneAndUsesTheSameSettlementAcrossThreeRealSeeds()
    {
        foreach (int seed in new[] { 763125071, 1344701872, -1546111730 })
        {
            yield return Begin(seed);
            Component package = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("PackagePanelController")).Cast<Component>().Single();
            foreach (string name in new[] { "PlantCream_Small", "PlantCream_Big", "Meat" })
            {
                ScriptableObject reward = ConsumableReflection.Reward(name);
                Assert.That((bool)ConsumableReflection.Call(inventory, "TryAddItem", reward, 2L), Is.True);
                ConsumableReflection.Call(stats, "SetCurrentHealth", name == "Meat" ? 95f : 100f);
                ConsumableReflection.Call(stats, "SetCurrentMana", name == "PlantCream_Big" ? 75f : 100f);
                Assert.That((bool)ConsumableReflection.Call(session, "SaveRuntimeState"), Is.True);
                ConsumableReflection.Call(package, "OpenPackagePanel");
                yield return null;
                Assert.That((bool)ConsumableReflection.Read(package, "IsPackagePanelVisible"), Is.True);
                Component slot = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("PackageSlotUI")).Cast<Component>()
                    .Single(s => ReferenceEquals(ConsumableReflection.Read(s, "CurrentReward"), reward));
                var beforeRoots = new HashSet<int>(UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("RewardPickup")).Select(o => o.GetInstanceID()));
                int before = Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward));
                float healthBefore = Current("Health"), manaBefore = Current("Mana");
                ExecuteEvents.Execute(slot.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right }, ExecuteEvents.pointerClickHandler);
                Assert.That(Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward)), Is.EqualTo(before - 1));
                Assert.That(Current("Health"), Is.EqualTo(healthBefore));
                Assert.That(Current("Mana"), Is.EqualTo(manaBefore));
                Component ground = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("RewardPickup")).Cast<Component>()
                    .Single(p => !beforeRoots.Contains(p.GetInstanceID()));
                var sourcePrefab = (GameObject)ConsumableReflection.Read(reward, "rewardPrefab");
                Assert.That(AssetDatabase.GetAssetPath(sourcePrefab), Is.EqualTo("Assets/Prefabs/Rewards/" + name + ".prefab"));
                Assert.That(ground.GetComponent(ConsumableReflection.Type("BaseDamageable")), Is.Null);
                Assert.That(ground.GetComponentInChildren(ConsumableReflection.Type("RewardVisualController")), Is.Not.Null);
                Assert.That((int)ConsumableReflection.Read(ground, "RemainingAmount"), Is.EqualTo(1));
                Physics2D.SyncTransforms();
                foreach (Collider2D playerCollider in inventory.GetComponentsInChildren<Collider2D>())
                {
                    if (!playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy) continue;
                    ColliderDistance2D separation = ground.GetComponent<Collider2D>().Distance(playerCollider);
                    Assert.That(separation.isValid && !separation.isOverlapped && separation.distance > .02f, Is.True);
                }
                Assert.That(map.Orchestrator.Context.ActiveSpatialGeometry.IsOriginalPickupPoseClear(reward.GetType().GetField("rewardPrefab").GetValue(reward) is GameObject prefab ? prefab.transform : null, ground.transform.position), Is.True);
                ConsumableReflection.Call(package, "ClosePackagePanel");
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(ground.gameObject.activeSelf, Is.True, "Standing still after a drop must not collect it.");
                Assert.That(Current("Health"), Is.EqualTo(healthBefore));
                Assert.That(Current("Mana"), Is.EqualTo(manaBefore));
                // This test injects contact at the existing trigger seam; actual movement is a separate Player check.
                ConsumableReflection.Call(ground, "OnTriggerEnter2D", inventory.GetComponent<Collider2D>());
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(ground.gameObject.activeSelf, Is.False);
                bool returnsToInventory = name == "PlantCream_Small";
                Assert.That(Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward)), Is.EqualTo(returnsToInventory ? before : before - 1));
                Assert.That(Current("Health"), Is.EqualTo(100f));
                Assert.That(Current("Mana"), Is.EqualTo(100f));
            }
            ConsumableReflection.Call(flow, "ReturnToStartupChoice");
        }
    }

    [UnityTest]
    public IEnumerator DropCommitRejectionReturnsThePreparedLeaseAndKeepsAllOwnedState()
    {
        yield return Begin(763125071);
        ScriptableObject reward = ConsumableReflection.Reward("Meat");
        Assert.That((bool)ConsumableReflection.Call(inventory, "TryAddItem", reward, 2L), Is.True);
        string memory = JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        int active = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("RewardPickup")).Length;
        int commits = 0;
        ConsumableReflection.BindRewards(inventory, stats, map, _ => { commits++; return false; });
        object[] args = { reward, null };
        Assert.That((bool)ConsumableReflection.Call(inventory, "TryDropOne", args), Is.False);
        Assert.That(commits, Is.EqualTo(1), "The test must reach the prepared lease's commit failure, not an earlier admission rejection.");
        Assert.That(Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward)), Is.EqualTo(2));
        Assert.That(JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData")), Is.EqualTo(memory));
        Assert.That(UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("RewardPickup")).Length, Is.EqualTo(active));

        var prefab = (GameObject)ConsumableReflection.Read(reward, "rewardPrefab");
        object pool = ConsumableReflection.Read(ConsumableReflection.Type("ObjectPoolManager"), "Instance");
        var queues = (IDictionary)ConsumableReflection.Read(pool, "poolMap");
        GameObject returned = ((Queue<GameObject>)queues[prefab]).Last();
        Assert.That(returned.activeSelf, Is.False);
        var lifetime = returned.GetComponent<GenerationScopedRewardLifetime>();
        Assert.That(lifetime, Is.Not.Null);
        Assert.That(ConsumableReflection.Read(lifetime, "armed"), Is.False,
            "An inactive rejected lease must not return itself to the pool a second time on map clear.");
    }

    [UnityTest]
    public IEnumerator NoOutsidePickupPoseAndNonConsumablesLeaveInventoryAndMemoryUnchanged()
    {
        yield return Begin(763125071);
        ScriptableObject reward = ConsumableReflection.Reward("Meat");
        Assert.That((bool)ConsumableReflection.Call(inventory, "TryAddItem", reward, 2L), Is.True);
        string memory = JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        // Controlled exclusion fixture: every nearby candidate overlaps this temporary player contact shape.
        var contact = inventory.gameObject.AddComponent<BoxCollider2D>();
        contact.isTrigger = true; contact.size = Vector2.one * 1000f;
        try
        {
            Physics2D.SyncTransforms();
            object[] args = { reward, null };
            Assert.That((bool)ConsumableReflection.Call(inventory, "TryDropOne", args), Is.False);
            Assert.That(Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward)), Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData")), Is.EqualTo(memory));
        }
        finally { UnityEngine.Object.DestroyImmediate(contact); Physics2D.SyncTransforms(); }
        object[] gold = { ConsumableReflection.Reward("Gold"), null };
        Assert.That((bool)ConsumableReflection.Call(inventory, "TryDropOne", gold), Is.False);
        Assert.That(JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData")), Is.EqualTo(memory));
    }

    [UnityTest]
    public IEnumerator ACommittedDropNotificationCannotReenterAndDropAnotherUnit()
    {
        yield return Begin(763125071);
        ScriptableObject reward = ConsumableReflection.Reward("Meat");
        Assert.That((bool)ConsumableReflection.Call(inventory, "TryAddItem", reward, 2L), Is.True);
        bool? nested = null;
        Action observer = () => nested = (bool)ConsumableReflection.Call(inventory, "TryDropOne", new object[] { reward, null });
        EventInfo info = inventory.GetType().GetEvent("OnInventoryChanged");
        info.AddEventHandler(inventory, observer);
        try
        {
            Assert.That((bool)ConsumableReflection.Call(inventory, "TryDropOne", new object[] { reward, null }), Is.True);
            Assert.That(nested, Is.EqualTo(false));
            Assert.That(Convert.ToInt32(ConsumableReflection.Call(inventory, "GetItemAmount", reward)), Is.EqualTo(1));
        }
        finally { info.RemoveEventHandler(inventory, observer); }
    }

    [UnityTest]
    public IEnumerator BindingConflictsRejectWithoutOverwritingTheAuthoredPlayerAndRecoverAfterRemoval()
    {
        var duplicate = new GameObject("DuplicateInventoryFixture"); owned.Add(duplicate); duplicate.SetActive(false); duplicate.tag = "Player";
        Component second = duplicate.AddComponent(ConsumableReflection.Type("PlayerInventory"));
        ConsumableReflection.Set(second, "goldReward", ConsumableReflection.Reward("Gold"));
        ConsumableReflection.Set(second, "rewardRegistry", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/ScriptObjects/RewardRegistry.asset"));
        duplicate.SetActive(true);
        string memory = JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        ConsumableReflection.Call(session, "BindSceneObjects");
        Assert.That(ConsumableReflection.Read(session, "Inventory"), Is.Null);
        Assert.That(ConsumableReflection.Read(session, "Rewards"), Is.Null);
        Assert.That(ConsumableReflection.Read(session, "PlayerBindingFailure").ToString(), Does.Contain("多个"));
        Assert.That(JsonUtility.ToJson(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData")), Is.EqualTo(memory));
        UnityEngine.Object.DestroyImmediate(duplicate); owned.Clear();
        ConsumableReflection.Call(session, "BindSceneObjects");
        Assert.That(ConsumableReflection.Read(session, "Inventory"), Is.SameAs(inventory));
        Assert.That(ConsumableReflection.Read(session, "Rewards"), Is.Not.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MemoryCommitDeepCopiesAndOnlyAnExplicitEntranceSaveWritesASlot()
    {
        yield return Begin(763125071);
        string root = (string)ConsumableReflection.Read(flow, "slotStoreRoot");
        Assert.That(Path.GetFullPath(root).StartsWith(Path.GetFullPath(saves), StringComparison.OrdinalIgnoreCase), Is.True);
        string[] before = Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>();
        object candidate = ConsumableReflection.CallStatic("GameSaveService", "Clone", ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"));
        object candidateStats = ConsumableReflection.Read(candidate, "playerStats");
        ConsumableReflection.Set(candidateStats, "currentHealth", 72f);
        Assert.That((bool)ConsumableReflection.CallStatic("GameSaveService", "TryCommitRuntimeData", candidate), Is.True);
        ConsumableReflection.Set(candidateStats, "currentHealth", 12f);
        Assert.That(Convert.ToSingle(ConsumableReflection.Read(ConsumableReflection.Read(ConsumableReflection.CallStatic("GameSaveService", "GetOrCreateData"), "playerStats"), "currentHealth")), Is.EqualTo(72f));
        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>(), Is.EquivalentTo(before));
        object entrance = ConsumableReflection.Read(flow, "entranceCandidate");
        object result = ConsumableReflection.Call(flow, "TrySaveEntranceCandidate", 1, entrance, false, null);
        Assert.That((bool)ConsumableReflection.Read(result, "succeeded"), Is.True);
        Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length, Is.GreaterThan(before.Length));
        Assert.That(ConsumableReflection.Read(ConsumableReflection.Call(flow, "ReadSlot", 1), "record"), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator PostCommitFinalizerFailureStaysFailedAndNeverPublishesGameplayReady()
    {
        yield return Begin(763125071);
        Guid prior = map.Orchestrator.Context.ActiveGenerationId.Value;
        int ready = 0;
        Action<Guid> observer = _ => ready++;
        map.Orchestrator.GameplayReady += observer;
        var fault = map.GetComponentInParent<MapRuntimeBootstrap>().gameObject.AddComponent<ConsumablesFailReadyFinalizer>();
        try
        {
            LogAssert.Expect(LogType.Error, new Regex("Runtime ready finalization failed: CONSUMABLES_POST_COMMIT_INJECTED"));
            ConsumableReflection.Call(flow, "ReturnToStartupChoice");
            ConsumableReflection.Call(flow, "BeginNewRunWithSeed", 1344701872);
            Assert.That(map.Orchestrator.Context.IsFailed, Is.True);
            Assert.That(map.Orchestrator.Context.IsReady, Is.False);
            Assert.That(map.Orchestrator.Context.ActiveGenerationId.Value, Is.Not.EqualTo(prior));
            Assert.That(map.Orchestrator.Context.Failure.Code, Is.EqualTo("RuntimeReadyFinalizationFailed"));
            Assert.That(ConsumableReflection.Read(flow, "State").ToString(), Is.EqualTo("Failure"));
            Assert.That(ready, Is.Zero);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ADR03_EVIDENCE") ?? saves, "aa05-post-commit.json"),
                JsonUtility.ToJson(new PostCommitReport { beforeGeneration = prior.ToString("N"), afterGeneration = map.Orchestrator.Context.ActiveGenerationId.Value.ToString("N"),
                    failure = map.Orchestrator.Context.Failure.Code, phase = map.Orchestrator.Context.Phase.ToString(), readyEvents = ready, flow = ConsumableReflection.Read(flow, "State").ToString(),
                    result = "EXPECTED_FAILURE_CONFIRMED_NO_READY_NO_ROLLBACK", rule = "Existing R14 fail-closed behavior; transaction redesign not performed" }, true));
        }
        finally { map.Orchestrator.GameplayReady -= observer; UnityEngine.Object.DestroyImmediate(fault); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator MeasureTheUnchangedCombatQueryAtNaturalAndSyntheticTargetCounts()
    {
        yield return Begin(763125071);
        Component action = inventory.GetComponent(ConsumableReflection.Type("PlayerAction"));
        MethodInfo method = action.GetType().GetMethod("FindNearestDamageableInRange", ConsumableReflection.All);
        var query = (Func<UnityEngine.Object>)Delegate.CreateDelegate(typeof(Func<UnityEngine.Object>), action, method);
        MethodInfo counterMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
        Func<long> counter = counterMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counterMethod);
        long controlDelta;
        int controlEvents;
        using (var recorder = BeginQueryAllocationRecorder(128))
        {
            long controlBefore = counter != null ? counter() : -1;
            var control = new byte[1024 * 1024]; control[0] = 11; control[control.Length - 1] = 13;
            GC.KeepAlive(control);
            controlDelta = counter != null ? counter() - controlBefore : -1;
            recorder.Stop(); controlEvents = CountQueryAllocationEvents(recorder);
        }
        Assert.That(controlEvents, Is.GreaterThan(0), "The native recorder must observe the known 1 MB allocation.");
        bool byteCounterQualified = controlDelta >= 1024 * 1024;
        string evidence = Environment.GetEnvironmentVariable("ADR03_EVIDENCE") ?? saves;
        File.WriteAllText(Path.Combine(evidence, "aa04-allocation-control.txt"),
            $"payload_bytes=1048576 raw_thread_counter_delta={controlDelta} native_events={controlEvents} " +
            $"byte_counter_qualified={byteCounterQualified} native_unit=TimeNanoseconds; event values are not byte counts");
        float scale = Time.timeScale; Time.timeScale = 0f;
        var lines = new List<string> { "label,targets,median_ms,p95_ms,thread_bytes_per_call,native_allocation_events,native_events_per_call,iterations" };
        try
        {
            int made = 0;
            foreach (int extra in new[] { 0, 256, 1024 })
            {
                while (made < extra)
                {
                    var go = new GameObject("CombatQuerySynthetic" + made); owned.Add(go); go.SetActive(false);
                    go.transform.position = inventory.transform.position + Vector3.right * (5f + made * .01f);
                    var collider = go.AddComponent<BoxCollider2D>(); collider.enabled = false;
                    var plant = (Behaviour)go.AddComponent(ConsumableReflection.Type("Plant")); plant.enabled = false;
                    go.SetActive(true); made++;
                }
                for (int i = 0; i < 16; i++) query();
                int targets = UnityEngine.Object.FindObjectsOfType(ConsumableReflection.Type("BaseDamageable")).Length;
                const int iterations = 128;
                var times = new double[iterations];
                using (var recorder = BeginQueryAllocationRecorder(4096))
                {
                    Profiler.BeginSample("ADR02.AA04.UnchangedQuery");
                    long before = byteCounterQualified ? counter() : -1;
                    for (int i = 0; i < iterations; i++)
                    {
                        long start = Stopwatch.GetTimestamp(); query();
                        times[i] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                    }
                    long bytes = byteCounterQualified ? counter() - before : -1;
                    Profiler.EndSample(); recorder.Stop();
                    int events = CountQueryAllocationEvents(recorder);
                    Assert.That(events, Is.GreaterThan(0), "The native recorder must observe the actual allocating query.");
                    if (byteCounterQualified) Assert.That(bytes, Is.GreaterThanOrEqualTo(0));
                    Array.Sort(times);
                    lines.Add(string.Join(",", extra == 0 ? "natural" : "synthetic_plus_" + extra, targets,
                        times[iterations / 2].ToString("F6", CultureInfo.InvariantCulture), times[(int)(iterations * .95)].ToString("F6", CultureInfo.InvariantCulture),
                        byteCounterQualified ? (bytes / (double)iterations).ToString("F3", CultureInfo.InvariantCulture) : "NOT_AVAILABLE",
                        events, (events / (double)iterations).ToString("F3", CultureInfo.InvariantCulture), iterations));
                }
            }
            File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ADR03_EVIDENCE") ?? saves, "aa04-query.csv"), lines);
        }
        finally { Time.timeScale = scale; }
        yield return null;
    }

    private static ProfilerRecorder BeginQueryAllocationRecorder(int capacity) => ProfilerRecorder.StartNew(
        ProfilerCategory.Internal, "GC.Alloc", capacity,
        ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately);

    private static int CountQueryAllocationEvents(ProfilerRecorder recorder)
    {
        Assert.That(recorder.Valid, Is.True, "A missing recorder is unavailable measurement, not zero allocations.");
        Assert.That(recorder.Count, Is.LessThan(recorder.Capacity), "A saturated trace cannot report complete event counts.");
        Assert.That(recorder.UnitType.ToString(), Is.EqualTo("TimeNanoseconds"));
        return recorder.Count;
    }

    private float Current(string stat) => Convert.ToSingle(ConsumableReflection.Call(stats, "GetCurrent", Enum.Parse(ConsumableReflection.Type("PlayerStatType"), stat)));
    [Serializable] private sealed class PostCommitReport
    {
        public string beforeGeneration, afterGeneration, failure, phase, flow, result, rule;
        public int readyEvents;
    }
}
