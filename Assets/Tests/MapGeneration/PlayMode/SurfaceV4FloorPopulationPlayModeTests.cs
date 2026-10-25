using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Observe the authorized remediation entry through the original scene and floor lifecycle.</summary>
public sealed class SurfaceV4FloorPopulationPlayModeTests
{
    [Serializable] private sealed class Row
    {
        public int floor, requestedSeed, actualSeed, terrainAttempt, ecologyAttempt, regionCount;
        public int planned, spawned, registered, targetCount, budget, minimumGroup;
        public string profile, generation, layout, difficulty, arrangement;
        public List<RegionRow> regions = new List<RegionRow>();
        public List<string> rejectionReasons = new List<string>();
    }
    [Serializable] private sealed class RegionRow
    {
        public int regionId, effectiveArea, surfaceArea, constructiveCapacity, demand, target, planned, spawned, registered;
        public string type, actualPrefab, density, areaEvidence;
    }
    [Serializable] private sealed class Report
    {
        public string phase = "SURFACE_R4_NORMAL_ENTRY", status = "INCOMPLETE";
        public string scope = "Real SampleScene original new-run/floor lifecycle. Isolated slots. No population, seed or actor rescaling changes. To exercise the next generation, already observed monsters are defeated through their original receivers and the original exit-request handler is invoked; this is NOT natural player combat or F input proof.";
        public List<Row> floors = new List<Row>();
        public List<string> failures = new List<string>();
    }
    private string output;
    private readonly Report report = new Report();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static MonoBehaviour Behaviour(string name) => UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(x => x.GetType().Name == name);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
    private void Save() => File.WriteAllText(Path.Combine(output, "PopulationAudit.json"), JsonUtility.ToJson(report, true));

    [UnitySetUp] public IEnumerator Prepare()
    {
        output = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Assert.That(output, Is.Not.Null.And.Not.Empty, "An explicit isolated evidence path is required.");
        Directory.CreateDirectory(output);
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(output, "IsolatedSlots"));
        Application.logMessageReceived+=RecordGenerationFailure;
        yield return null;
    }
    private void RecordGenerationFailure(string message,string stack,LogType type)
    {
        if(type!=LogType.Error || !message.StartsWith("Map generation failed"))return;
        if (message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:", StringComparison.Ordinal) &&
            (message.Contains("InsufficientConstructiveCapacity") || message.Contains("CriticalRouteBlocked") ||
             (message.Contains("InsufficientSafeSpace") && message.Contains("NoQualifiedMerchantInBirthSafety"))))
            LogAssert.Expect(LogType.Error, message);
        report.failures.Add("Observed finite generation rejection: " + message);
        var owner=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).FirstOrDefault(x=>x.GetType().Name=="TowerRunCoordinator");
        if(owner==null)return;
        var entry=(TowerRunCheckpointRecord)owner.GetType().GetField("stagedRecord",Private).GetValue(owner);
        if(entry!=null)File.WriteAllText(Path.Combine(output,"FailedFloorStagedRecord.json"),JsonUtility.ToJson(entry,true));
    }
    [UnityTearDown] public IEnumerator Finish()
    {
        Application.logMessageReceived-=RecordGenerationFailure;
        Save(); TowerRunRuntimeEnvironment.ResetOverrides(); Time.timeScale = 1f;
        foreach (var c in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Where(x => x.GetType().Name == "TowerRunCoordinator")) Call(c, "ReturnToStartupChoice");
        yield return null;
    }

    [UnityTest] public IEnumerator KnownRejectedFloorSeedFailsClosedThenOriginalRetryReachesReady()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); yield return null;
        var coordinator = Behaviour("TowerRunCoordinator"); Call(coordinator, "ReturnToStartupChoice");
        var record = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("newRunBaseline", Private).GetValue(coordinator)).DeepClone();
        record.runId = Guid.NewGuid().ToString("D");
        record.floor = new TowerRunFloorRecord { floorNumber = 3, displayName = "第 3 层" };
        record.generation.profileId = EcologyRecipeDefinition.MainV1ProfileId; record.generation.requestedSeed = -337455008;
        record.generation.staticBaselineSignature = string.Empty;
        MethodInfo begin = coordinator.GetType().GetMethod("BeginOperation", Private);
        begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), record, false });
        yield return null;
        Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Failure"));
        Assert.That(UnityEngine.Object.FindObjectOfType<MapGenerationController>(true).Orchestrator.Context.IsReady, Is.False);
        Assert.That(coordinator.GetType().GetProperty("CanRetryFailedOperation").GetValue(coordinator), Is.True);
        for (int attempt = 0; attempt < 3 && coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString() == "Failure"; attempt++)
        { Call(coordinator, "RetryFailedOperation"); yield return null; }
        Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Playing"));
        Row row = Capture(coordinator, UnityEngine.Object.FindObjectOfType<MapGenerationController>(true), 3);
        Assert.That(row.spawned, Is.EqualTo(row.planned)); Assert.That(row.registered, Is.EqualTo(row.planned));
        report.floors.Add(row); Save();
    }

    [UnityTest] public IEnumerator NormalR4EntryAndThreeLaterFloorsKeepGroupsAndExactPopulationCounts()
    {
        foreach (string selected in new[] { "DEFAULT_ENTRY" })
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); yield return null;
            MonoBehaviour coordinator = Behaviour("TowerRunCoordinator");
            Call(coordinator, "ReturnToStartupChoice");
            if (selected == "DEFAULT_ENTRY") Call(coordinator, "BeginNewRun");
            else
            {
                // Select only the explicit profile; leave its fresh requested seed and floor/players alone.
                var baseline = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("newRunBaseline", Private).GetValue(coordinator)).DeepClone();
                var fresh = coordinator.GetType().GetMethod("CreateFreshGenerationRecord", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                baseline.runId = Guid.NewGuid().ToString("D"); baseline.generation = (TowerRunGenerationRecord)fresh;
                baseline.generation.profileId = selected;
                baseline.floor = new TowerRunFloorRecord { floorNumber = 0, displayName = "新手关卡" };
                MethodInfo begin = coordinator.GetType().GetMethod("BeginOperation", Private);
                begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), baseline, false });
            }
            yield return null;
            for (int floor = 0; floor <= 3; floor++)
            {
                int retries = 0;
                while ((string)coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString() == "Failure" && retries++ < 3)
                {
                    report.failures.Add(selected + " floor=" + floor + " original finite rejection: " + coordinator.GetType().GetProperty("FailureMessage").GetValue(coordinator)); Save();
                    Call(coordinator, "RetryFailedOperation"); yield return null;
                }
                Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Playing"), selected + " did not reach normal Ready.");
                var controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
                Row row = Capture(coordinator, controller, floor); report.floors.Add(row); Save();
                Assert.That(row.profile, Is.EqualTo(EcologyRecipeDefinition.MainV1ProfileId));
                Assert.That(row.budget, Is.EqualTo(new[] { 24, 28, 32, 36 }[floor]));
                Assert.That(row.planned, Is.LessThanOrEqualTo(row.budget));
                CaptureCamera(selected == "DEFAULT_ENTRY" ? "Default" : "Units", floor);
                Assert.That(row.spawned, Is.EqualTo(row.planned)); Assert.That(row.registered, Is.EqualTo(row.planned));
                Assert.That(row.targetCount, Is.EqualTo(row.planned));
                foreach (var region in row.regions)
                {
                    Assert.That(region.target, Is.GreaterThanOrEqualTo(3));
                    Assert.That(region.target, Is.LessThanOrEqualTo(region.constructiveCapacity));
                    Assert.That(region.planned, Is.EqualTo(region.target));
                    Assert.That(region.spawned, Is.EqualTo(region.planned)); Assert.That(region.registered, Is.EqualTo(region.planned));
                }
                if (floor == 3) break;
                var context = controller.Orchestrator.Context;
                // Counts were captured before clearing; this controls only the test's exit prerequisite.
                foreach (var entry in context.ActiveRegistry.Entries.Where(x => x.Role == MonsterPlacementService.MonsterRole).ToArray())
                {
                    var actor = (Component)entry.Instance;
                    float hp = (float)actor.GetType().GetProperty("CurrentHealth").GetValue(actor);
                    actor.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(actor, new object[] { hp + 1f });
                }
                yield return null;
                coordinator.GetType().GetMethod("HandleExitUseRequested", Private).Invoke(coordinator, new object[] { context.ActiveGenerationId.Value });
                yield return null;
            }
        }
        report.status = "PASS"; Save();
    }

    private Row Capture(MonoBehaviour coordinator, MapGenerationController controller, int expectedFloor)
    {
        object[] args = { null, null }; Assert.That((bool)Call(coordinator, "TryCreateSaveRequest", args), Is.True);
        var entry = (TowerRunCheckpointRecord)args[0]; var context = controller.Orchestrator.Context;
        Assert.That(entry.floor.floorNumber, Is.EqualTo(expectedFloor)); Assert.That(context.IsReady, Is.True);
        var population = controller.GetComponentInParent<MapEcologyPopulationService>(); var plan = population.LastPlan;
        var registry = context.ActiveRegistry.Entries.Where(x => x.Role == MonsterPlacementService.MonsterRole).ToArray();
        var row = new Row { floor = entry.floor.floorNumber, profile = entry.generation.profileId, requestedSeed = entry.generation.requestedSeed,
            actualSeed = context.ActiveMap.GenerationSeed, terrainAttempt = entry.generation.attemptIndex,
            ecologyAttempt = entry.generation.ecology?.ecologyAttempt ?? -1, generation = context.ActiveGenerationId.ToString(),
            planned = plan.ActualMonsters, registered = registry.Count(x => x.Instance != null),
            targetCount = (int)Behaviour("TowerFloorRuntimeController").GetType().GetProperty("TargetCount").GetValue(Behaviour("TowerFloorRuntimeController")),
            difficulty = "R4 floor policy: budget 24+4*min(floor,6), regional cap 8+min(floor,4)", arrangement = report.scope };
        row.spawned = plan.Placements.Where(p => p.Role == MonsterPlacementService.MonsterRole).Count(p => population.TryGetActiveInstance(p.LogicalId, out var actor) && ((Component)actor).gameObject.activeInHierarchy);
        var layout = context.ActivePlacementPlan.Ecology;
        if (layout == null)
        {
            row.regionCount = 0; row.budget = population.Profile.Monster.Maximum; row.minimumGroup = 0;
            foreach (var group in plan.Placements.Where(p => p.Role == MonsterPlacementService.MonsterRole).GroupBy(p => p.Prefab.name))
                row.regions.Add(new RegionRow { regionId = -1, type = group.Key, actualPrefab = group.Key, planned = group.Count(), target = group.Count(),
                    spawned = group.Count(p => population.TryGetActiveInstance(p.LogicalId, out var actor) && ((Component)actor).gameObject.activeInHierarchy),
                    registered = registry.Count(r => group.Any(p => p.LogicalId == r.LogicalObjectId) && r.Instance != null),
                    effectiveArea = -1, constructiveCapacity = -1, demand = -1, surfaceArea = -1,
                    density = "LEGACY_TOTAL_AREA/320; no per-territory minimum", areaEvidence = "NOT_APPLICABLE_UNZONED: grouping by prefab only; not a generated territory" });
        }
        else
        {
            EcologyTerritoryRecipe recipe = Resources.Load<EcologyRecipeDefinition>(EcologyRecipeDefinition.ResourcePathFor(row.profile)).ResolveTerritoryRecipe(expectedFloor);
            row.layout = layout.Signature; row.regionCount = layout.Regions.Count; row.budget = recipe.TotalBudget; row.minimumGroup = recipe.MinimumGroup;
            Assert.That(layout.RecipeIdentity, Does.StartWith(recipe.Identity + ":"), "The accepted layout identity must include this floor's policy followed by its catalog digest.");
            foreach (var region in layout.Regions.Where(r => !r.IsSafety))
            {
                var residents = layout.Residents.Where(r => r.HomeRegionId == region.Id).ToArray();
                var first = residents[0]; Assert.That(population.TryGetActiveInstance(first.Id, out var firstActor), Is.True);
                var home = ((Component)firstActor).GetComponent<EcologyResidentBinding>().Home;
                row.regions.Add(new RegionRow { regionId = region.Id, type = region.TypeId, actualPrefab = SourceName((Component)firstActor),
                    surfaceArea = region.Nodes.Count, effectiveArea = home.HomeNodes.Count, constructiveCapacity = region.ConstructiveCapacity,
                    demand = Math.Min(region.ConstructiveCapacity, Math.Min(recipe.MaximumPerRegion, Math.Max(recipe.MinimumGroup, home.HomeNodes.Count / 48))), target = region.AssignedPopulation, planned = residents.Length,
                    spawned = residents.Count(r => population.TryGetActiveInstance(r.Id, out var actor) && ((Component)actor).gameObject.activeInHierarchy),
                    registered = registry.Count(r => residents.Any(x => x.Id == r.LogicalObjectId) && r.Instance != null), density = "1/48",
                    areaEvidence = "Observed original resident Home.HomeNodes; connected legal activity nodes, NOT region surface length; compare accepted geometry during follow-up if differing from target" });
            }
        }
        return row;
    }
    private static string SourceName(Component actor)
    {
        Component pooled = actor.GetComponent("PooledObject");
        return ((GameObject)pooled.GetType().GetProperty("SourcePrefab").GetValue(pooled)).name;
    }
    private void CaptureCamera(string profile, int floor)
    {
        var camera = Camera.main; if (camera == null) return;
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, profile + "-Floor" + floor + ".png"), image.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); }
    }
}
