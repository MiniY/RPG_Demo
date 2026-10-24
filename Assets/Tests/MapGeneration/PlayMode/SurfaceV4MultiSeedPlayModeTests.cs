using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SurfaceV4MultiSeedPlayModeTests
{
    private MonoBehaviour coordinator;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private object Call(string name, params object[] args) => coordinator.GetType().GetMethod(name).Invoke(coordinator, args);
    [UnityTest] public IEnumerator EightSeedsUseNormalNewAdventureAndMatchEveryRenderedRootAndNativeGeometry()
        => Check(new[] { 20260929, -1391545000, 613580675, 1118448528, 21517677, 20470387, 27182818, 31415926 });
    [UnityTest] public IEnumerator FailedPlayerSeedKeepsAllDeclaredStairAccess() => Check(new[] { -1391545000 });
    [UnityTest] public IEnumerator PreviouslyObservedFloorSeedsCheckStairLayers() => Check(new[]{2020360954,-1013289730,-646539229,962744271,-1026979590,-888735,-1550237997,-472906849});
    [UnityTest] public IEnumerator LastFailedSeedMustUseSameNormalOwnersAndWholeFeet() => Check(new[] { 31415926 });
    [UnityTest] public IEnumerator FailedTerrainSeedsKeepOriginalSeedAndAllNativeStairs() => Check(new[]{-1251390370,-1550237997});
    private IEnumerator Check(int[] seeds)
    {
        string output = Environment.GetEnvironmentVariable("RPG_DEMO_REMEDIATION_EVIDENCE");
        Assert.That(output, Is.Not.Null.And.Not.Empty); Directory.CreateDirectory(output);
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(output, "SurfaceSlots"));
        foreach (int seed in seeds)
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single); yield return null;
            coordinator = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(b => b.GetType().Name == "TowerRunCoordinator");
            Call("ReturnToStartupChoice");
            var baseline = ((TowerRunCheckpointRecord)coordinator.GetType().GetField("newRunBaseline", Private).GetValue(coordinator)).DeepClone();
            baseline.runId = Guid.NewGuid().ToString("D");
            baseline.generation = (TowerRunGenerationRecord)coordinator.GetType().GetMethod("CreateFreshGenerationRecord", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            baseline.generation.requestedSeed = seed;
            baseline.floor = new TowerRunFloorRecord { floorNumber = 0, displayName = "新手关卡" };
            MethodInfo begin = coordinator.GetType().GetMethod("BeginOperation", Private);
            begin.Invoke(coordinator, new object[] { Enum.Parse(begin.GetParameters()[0].ParameterType, "NewRun"), baseline, false });
            yield return null;
            Assert.That(coordinator.GetType().GetProperty("State").GetValue(coordinator).ToString(), Is.EqualTo("Playing"),
                "A fixed corpus rejection is recorded, never silently replaced by another requested seed: " + coordinator.GetType().GetProperty("FailureMessage").GetValue(coordinator));
            var controller = UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
            Assert.That(controller.Settings.remediationProjectedSurfaces, Is.True);
            var accepted=(EcologyPlanningAttemptResult)typeof(MapGenerationController).GetField("zoningAccepted",Private).GetValue(controller);
            var originalFoot=controller.Player.Find("MovementFeet").GetComponent<CircleCollider2D>();
            var graph=accepted.StaticGeometry.BuildNavigationGraph(originalFoot,0f);
            Assert.That(accepted.StaticGeometry.TryWorldNode(controller.Player.position,out int spawn),Is.True);
            var reachable=graph.BuildReturnField(new[]{spawn});
            foreach(var stair in controller.LastGeneratedMap.Stairs)
            {
                Assert.That(accepted.StaticGeometry.TryWorldNode(controller.TilemapRenderer.Coordinates.CellToWorld(stair.LowerCell),out int lower),Is.True);
                Assert.That(accepted.StaticGeometry.TryWorldNode(controller.TilemapRenderer.Coordinates.CellToWorld(stair.UpperCell),out int upper),Is.True);
                Assert.That(reachable.Distances[lower],Is.GreaterThanOrEqualTo(0),"Actual declared lower stair unreachable");
                Assert.That(reachable.Distances[upper],Is.GreaterThanOrEqualTo(0),"Actual declared upper stair unreachable");
                Assert.That(graph.CanMove(lower,upper)&&graph.CanMove(upper,lower),Is.True,"Actual full-foot stair must be bidirectional");
            }
            var report = RemediationRuntimeAudit.Capture(controller, 0);
            var native = RemediationRuntimeAudit.CompareNativeProjection(controller, controller.Player.Find("MovementFeet").GetComponent<CircleCollider2D>());
            File.WriteAllText(Path.Combine(output, "Seed" + seed + "-NativeGeometry.json"), JsonUtility.ToJson(native, true));
            Assert.That(native.testedPoses, Is.GreaterThan(0)); Assert.That(native.clearPoses, Is.GreaterThan(0));
            Assert.That(native.mismatches, Is.Empty, "Current R3 pending geometry must agree with independent actual native static physics.");
            File.WriteAllText(Path.Combine(output, "Seed" + seed + ".json"), JsonUtility.ToJson(report, true));
            Assert.That(report.profile, Is.EqualTo(EcologyRecipeDefinition.MainV1ProfileId));
            Assert.That(report.violations, Is.Empty, "Independent observation of actual rendered layers.");
            Assert.That(report.decorations, Is.GreaterThan(0), "Do not erase legitimate ground decoration to pass zero violations.");
            Assert.That(report.faceTiles, Is.GreaterThan(0)); Assert.That(report.nativeFacePaths, Is.GreaterThan(0));
            Capture(output, "Seed" + seed + "-GameCamera.png");
            Debug.Log("R3_MULTISEED_ACTUAL seed=" + seed + " actual=" + report.seed + " decorations=" + report.decorations + " plants=" + report.plants + " faceTiles=" + report.faceTiles + " nativePaths=" + report.nativeFacePaths + " population=" + report.planned + "/" + report.active + "/" + report.registered + " violations=" + report.violations.Count);
            Call("ReturnToStartupChoice"); yield return null;
        }
    }
    private static void Capture(string output, string name)
    {
        var camera = Camera.main; Assert.That(camera, Is.Not.Null);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); }
    }
    [UnityTearDown] public IEnumerator Cleanup()
    { if (coordinator != null) Call("ReturnToStartupChoice"); TowerRunRuntimeEnvironment.ResetOverrides(); yield return null; Time.timeScale = 1f; }
}
