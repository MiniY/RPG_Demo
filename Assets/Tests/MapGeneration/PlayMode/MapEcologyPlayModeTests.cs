using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class MapEcologyPlayModeTests
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "RPG_Demo-Ecology-" + Guid.NewGuid().ToString("N"));
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        TowerRunRuntimeEnvironment.ResetOverrides();
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [UnityTest]
    public IEnumerator FixedSeedsProduceBoundedReachableClusteredPopulations()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
        MonoBehaviour coordinator = FindCoordinator();
        Assert.That(coordinator, Is.Not.Null);
        Invoke(coordinator, "ReturnToStartupChoice");
        Invoke(coordinator, "BeginNewRun");
        yield return null;

        GameObject runtimeRoot = SceneManager.GetActiveScene().GetRootGameObjects()
            .Single(root => root.name == "RandomMapRuntime");
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapEcologyPopulationService ecology = runtimeRoot.GetComponent<MapEcologyPopulationService>();
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(ecology?.Profile, Is.Not.Null);
        Assert.That(bootstrap, Is.Not.Null);

        int[] seeds = { 20260929, -1391545000, 613580675, 839235445, -1071926194 };
        string firstSignature = null;
        foreach (int seed in seeds)
        {
            BeginSeed(coordinator, seed);
            Assert.That(bootstrap.Context.Phase, Is.EqualTo(MapLifecyclePhase.Ready),
                "Ecology seed failed: " + seed);
            MapEcologyPopulationPlan population = ecology.LastPlan;
            Assert.That(population, Is.Not.Null);
            Assert.That(population.ActualMonsters,
                Is.InRange(ecology.Profile.Monster.RequiredMinimum, ecology.Profile.Monster.Maximum));
            Assert.That(population.ActualDestructibles,
                Is.InRange(ecology.Profile.Destructible.RequiredMinimum, ecology.Profile.Destructible.Maximum));
            Assert.That(ecology.ActiveCount,
                Is.EqualTo(population.ActualMonsters + population.ActualDestructibles));
            Assert.That(population.Placements.Select(x => x.TypeId).Distinct().Count(),
                Is.GreaterThanOrEqualTo(3));

            MapData map = bootstrap.Context.ActiveMap;
            MapPlacementPlan plan = bootstrap.Context.ActivePlacementPlan;
            MapPopulationRegionQuery reachable = MapPopulationRegionQuery.Build(map, plan);
            foreach (MapEcologyPlacementResult placed in population.Placements)
            {
                Assert.That(map.IsInside(placed.Cell) && map.IsWalkable(placed.Cell), Is.True);
                if (placed.Role == MonsterPlacementService.MonsterRole)
                    Assert.That(reachable.SpawnDistances.ContainsKey(placed.Cell), Is.True,
                        "A portal target must remain reachable after Destructible placement.");
                Assert.That(bootstrap.Context.ActiveRegistry.TryGet(placed.LogicalId, out var entry), Is.True);
                Assert.That(entry.Instance, Is.Not.Null);
                Assert.That(entry.Ownership, Is.EqualTo(MapPlacementOwnership.GenerationScoped));
            }
            foreach (var group in population.Placements.GroupBy(x => x.Role + "/" + x.TypeId))
            {
                int radius = group.First().Role == MonsterPlacementService.MonsterRole
                    ? ecology.Profile.Monster.ClusterRadius : ecology.Profile.Destructible.ClusterRadius;
                foreach (MapEcologyPlacementResult a in group)
                foreach (MapEcologyPlacementResult b in group)
                    Assert.That(Mathf.Max(Mathf.Abs(a.Cell.x - b.Cell.x), Mathf.Abs(a.Cell.y - b.Cell.y)),
                        Is.LessThanOrEqualTo(radius * 2), "One type should form a local cluster.");
            }

            string signature = string.Join(";", population.Placements.Select(x =>
                x.Role + "/" + x.TypeId + "/" + x.Prefab.name + "/" + x.Cell.x + "," + x.Cell.y));
            Debug.Log("Ecology seed=" + seed + " monsters=" + population.ActualMonsters +
                      " destructibles=" + population.ActualDestructibles + " signature=" + signature);
            if (firstSignature == null)
            {
                firstSignature = signature;
                MapEcologyPlacementResult firstMonster = population.Placements.First(x =>
                    x.Role == MonsterPlacementService.MonsterRole);
                Assert.That(bootstrap.Context.ActiveRegistry.TryGet(firstMonster.LogicalId,
                    out MapObjectRegistryEntry defeatedEntry), Is.True);
                Component defeatedActor = defeatedEntry.Instance as Component;
                Assert.That(defeatedActor, Is.Not.Null);
                MethodInfo takeDamage = defeatedActor.GetType().GetMethod("TakeDamage",
                    new[] { typeof(float) });
                Assert.That(takeDamage, Is.Not.Null);
                takeDamage.Invoke(defeatedActor, new object[] { float.MaxValue });
                Assert.That(bootstrap.Context.ActiveRegistry.TryGet(firstMonster.LogicalId, out _),
                    Is.False, "A defeated Monster must unregister exactly once.");
                BeginSeed(coordinator, seed);
                string repeated = string.Join(";", ecology.LastPlan.Placements.Select(x =>
                    x.Role + "/" + x.TypeId + "/" + x.Prefab.name + "/" + x.Cell.x + "," + x.Cell.y));
                Assert.That(repeated, Is.EqualTo(firstSignature));
                Assert.That(!defeatedActor.gameObject.activeInHierarchy ||
                    bootstrap.Context.ActiveRegistry.Entries.Any(x => x.Instance == defeatedActor),
                    Is.True, "A defeated corpse must not persist outside the rebuilt registry.");
            }
            else
                Assert.That(signature, Is.Not.EqualTo(firstSignature));
        }
    }

    [UnityTest]
    public IEnumerator ExistingDefaultSlotRestoresThroughStrictLegacyRecipe()
    {
        string source = Environment.GetEnvironmentVariable("RPG_DEMO_LEGACY_SLOT_SOURCE");
        if (string.IsNullOrEmpty(source))
            Assert.Ignore("Set RPG_DEMO_LEGACY_SLOT_SOURCE to a read-only pre-ecology slot fixture.");
        Assert.That(File.Exists(source), Is.True);
        byte[] original = File.ReadAllBytes(source);
        string originalHash = Hash(original);
        Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);
        File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,
            "tower-run-slot-1.json"), original);

        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
        MonoBehaviour coordinator = FindCoordinator();
        Assert.That(coordinator, Is.Not.Null);
        Invoke(coordinator, "ReturnToStartupChoice");
        TowerRunSlotReadResult slot = (TowerRunSlotReadResult)Invoke(coordinator, "ReadSlot", 1);
        Assert.That(slot.state, Is.EqualTo(TowerRunSlotState.Valid), slot.message);
        Assert.That(slot.record.generation.profileId, Is.EqualTo("default"));
        Assert.That(slot.record.generation.staticBaselineSignature,
            Does.StartWith("TRB2-"));
        Invoke(coordinator, "BeginRestoreSlot", 1, slot.confirmationToken);
        yield return null;
        Assert.That(coordinator.GetType().GetProperty("State")?.GetValue(coordinator)?.ToString(),
            Is.EqualTo("Playing"));

        GameObject runtimeRoot = SceneManager.GetActiveScene().GetRootGameObjects()
            .Single(root => root.name == "RandomMapRuntime");
        MapGenerationController controller =
            runtimeRoot.GetComponentInChildren<MapGenerationController>(true);
        MapRuntimeBootstrap bootstrap = runtimeRoot.GetComponent<MapRuntimeBootstrap>();
        MapGenerationController boundController = (MapGenerationController)coordinator.GetType()
            .GetField("mapController", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(coordinator);
        Assert.That(boundController, Is.SameAs(controller),
            "The persistent TowerRunCoordinator is bound to a different scene runtime.");
        Assert.That(bootstrap.Context.Phase, Is.EqualTo(MapLifecyclePhase.Ready));
        Assert.That(bootstrap.DiagnosticSnapshot.ActiveRequest.ProfileId, Is.EqualTo("default"));
        Assert.That(TowerRunMapBaseline.Compute(bootstrap.Context.ActiveMap,
            bootstrap.Context.ActivePlacementPlan, controller.LastGeneratedSimpleDecorations),
            Is.EqualTo(slot.record.generation.staticBaselineSignature));
        Assert.That(bootstrap.Context.ActiveRegistry.Entries.Count(
            entry => entry.Role == MonsterPlacementService.MonsterRole), Is.EqualTo(1));
        Assert.That(File.Exists(source), Is.True);
        Assert.That(Hash(File.ReadAllBytes(source)), Is.EqualTo(originalHash));
        Debug.Log("Legacy TRB2 slot restored through default placement recipe; source SHA256=" + originalHash);
    }

    private static MonoBehaviour FindCoordinator() =>
        UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
            .FirstOrDefault(x => x != null && x.GetType().Name == "TowerRunCoordinator");

    private static void BeginSeed(MonoBehaviour coordinator, int seed)
    {
        FieldInfo current = coordinator.GetType().GetField("currentRecord",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(current, Is.Not.Null);
        TowerRunCheckpointRecord record =
            ((TowerRunCheckpointRecord)current.GetValue(coordinator)).DeepClone();
        record.schemaVersion = TowerRunCheckpointRecord.CurrentSchemaVersion;
        record.generation.generatorVersion = 1;
        record.generation.configurationIdentity = "random-map-default-v1";
        record.generation.ecology = null;
        record.generation.profileId = MapEcologyProfile.ProfileId;
        record.generation.requestedSeed = seed;
        record.generation.attemptIndex = 0;
        record.generation.staticBaselineSignature = string.Empty;
        MethodInfo begin = coordinator.GetType().GetMethod("BeginOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(begin, Is.Not.Null);
        object kind = Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun");
        begin.Invoke(coordinator, new[] { kind, record, false });
    }

    private static object Invoke(MonoBehaviour target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, args);
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}
