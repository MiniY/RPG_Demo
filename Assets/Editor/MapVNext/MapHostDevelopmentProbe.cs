using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Explicit non-batch acceptance of daily Play and strict Test Runner/build boundaries.</summary>
[InitializeOnLoad]
public static class MapHostDevelopmentProbe
{
    private const string Key = "RPG_Demo.HostDevelopmentProbe.";
    private static TestRunnerApi api;
    [Serializable] private sealed class Result
    {
        public string status, failure, phase, sourceResult, compatibilityResult, buildFailure;
        public bool batchMode, playing, gameplayReady, explicitStrictRejected, missingResourceRejected, invalidManifestRejected;
        public bool restoredAfterLayoutChange, formalTestsPassed, buildRejected, testStateReleased;
        public int passedTests, failedTests;
    }
    static MapHostDevelopmentProbe()
    {
        if (SessionState.GetString(Key + "Output", "") == "") return;
        Install();
        if (Phase == "tests-wait") RegisterCallbacks();
    }
    private static string Phase { get => SessionState.GetString(Key + "Phase", ""); set => SessionState.SetString(Key + "Phase", value); }
    private static string Output => SessionState.GetString(Key + "Output", "");
    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");
        string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts")) + Path.DirectorySeparatorChar;
        if (Application.isBatchMode || string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ExplicitNonBatchOwnedEvidenceRequired");
        if (Application.companyName != "CodexShopEditorAcceptance") throw new InvalidOperationException("PrivateEditorNamespaceRequired");
        Directory.CreateDirectory(output);
        SessionState.SetString(Key + "Output", output);
        SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
        Phase = "play";
        Write(new Result { status = "RUNNING", phase = Phase });
        Install();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }
    private static void Install()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= ReleaseApi;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseApi;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 180)
                throw new TimeoutException("DevelopmentProbeDeadline");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (Phase == "play" && EditorApplication.isPlaying)
            {
                var coordinator = UnityEngine.Object.FindObjectOfType<TowerRunCoordinator>();
                if (coordinator == null) return;
                ProveDailyPlay(coordinator);
                Phase = "tests-start";
                EditorApplication.isPlaying = false;
            }
            else if (Phase == "tests-start" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Phase = "tests-wait";
                RegisterCallbacks();
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                    testNames = new[] { "MapHostSourcePolicyPlayTests.FormalTestsRequireStrictSourceValidation" } }));
            }
            else if (Phase == "tests-done" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ProveBuildRejection();
                Finish(0);
            }
        }
        catch (Exception error)
        {
            Result result = Read(); result.status = "FAIL"; result.failure = error.ToString(); result.phase = Phase; Write(result);
            Debug.LogException(error); Finish(2);
        }
    }
    private static void ProveDailyPlay(TowerRunCoordinator coordinator)
    {
        TowerRunRuntimeEnvironment.ConfigureTestIsolation(Path.Combine(Output, "Runtime"));
        coordinator.BeginNewRunWithSeed(21193);
        var map = UnityEngine.Object.FindObjectOfType<MapGenerationController>();
        Require(coordinator.State == TowerRunFlowState.Playing && map.Orchestrator.Context.IsReady,
            "DailyPlayDidNotReachReady: " + coordinator.FailureMessage);
        Result result = Read(); result.batchMode = Application.isBatchMode; result.playing = Application.isPlaying;
        result.gameplayReady = true; result.sourceResult = MapHostCompatibility.LastStrictSourceResult;
        result.compatibilityResult = MapHostCompatibility.LastCompatibilityResult;
        Require(result.sourceResult == "NOT_EXECUTED_EDITOR_DEVELOPMENT", "DailyPlayWasIncorrectlyMarkedSealed");
        Require(coordinator.TryCreateSaveRequest(out var candidate, out string saveMessage), "DailySaveCandidateMissing: " + saveMessage);
        string generation = JsonUtility.ToJson(candidate.generation);
        var saved = coordinator.TrySaveEntranceCandidate(1, candidate, false);
        Require(saved.succeeded, "DailyPrivateSaveFailed: " + saved.message);
        coordinator.ReturnToStartupChoice();
        var slot = coordinator.ReadSlot(1);
        Require(slot.state == TowerRunSlotState.Valid, "DailyPrivateSlotInvalid: " + slot.message);
        coordinator.BeginRestoreSlot(1, slot.confirmationToken);
        Require(coordinator.State == TowerRunFlowState.Playing && map.Orchestrator.Context.IsReady,
            "DailyRestoreDidNotReachReady: " + coordinator.FailureMessage);
        Require(coordinator.TryCreateSaveRequest(out var reopened, out saveMessage) && JsonUtility.ToJson(reopened.generation) == generation,
            "DailyRestoreChangedGeneration: " + saveMessage);
        result.restoredAfterLayoutChange = true;
        Write(result);
        var recipe = (EcologyRecipeDefinition)typeof(MapGenerationController).GetField("zoningRecipe",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(map);
        Require(recipe != null, "ActiveRecipeMissing");
        result.explicitStrictRejected = Rejects(() => recipe.VerifyRetainedSourceFiles(true), "UnapprovedOriginalManifestDifference");
        Require(result.explicitStrictRejected, "ExplicitStrictAcceptedDrift");
        var fixture = new TextAsset("{\"schema\":1,\"profile\":\"fixture\",\"configurationDigest\":\"fixture\",\"catalog\":[],\"files\":[{\"path\":\"Assets/HostProbe-MustNotExist.asset\",\"sha256\":\"missing-fixture\"}]}");
        try
        {
            var manifest = new EcologyRetainedContentManifest(fixture, "fixture", "fixture", Array.Empty<EcologyEcotypeDefinition>());
            result.missingResourceRejected = Rejects(() => manifest.VerifySourceFiles(false), "RetainedDependencyMissing");
            result.invalidManifestRejected = Rejects(() => new EcologyRetainedContentManifest(fixture, "wrong-profile", "fixture", Array.Empty<EcologyEcotypeDefinition>()), "RetainedContentManifestMismatch");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
        Require(result.missingResourceRejected && result.invalidManifestRejected, "DevelopmentLostRequiredContentChecks");
        Write(result);
    }
    private static void RegisterCallbacks()
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks(), -10000);
    }
    private static void ReleaseApi()
    {
        if (api != null) UnityEngine.Object.DestroyImmediate(api);
        api = null;
    }
    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor tests)
        {
            Result result = Read(); result.passedTests = tests.PassCount; result.failedTests = tests.FailCount;
            result.formalTestsPassed = tests.PassCount == 1 && tests.FailCount == 0;
            var provider = typeof(MapHostCompatibility).GetProperty("EditorTestRunActive")?.GetValue(null) as Func<bool>;
            result.testStateReleased = provider != null && !provider();
            Write(result); Phase = "tests-done";
        }
    }
    private static void ProveBuildRejection()
    {
        Result result = Read();
        Require(result.formalTestsPassed && result.testStateReleased, "InteractiveFormalTestsOrCleanupFailed");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" }, target = BuildTarget.StandaloneWindows64,
            locationPathName = Path.Combine(Output, "ExpectedRejectedBuild/Rejected.exe") });
        result.buildFailure = string.Join("\n", report.steps.SelectMany(step => step.messages).Select(message => message.content));
        result.buildRejected = report.summary.result == BuildResult.Failed && result.buildFailure.Contains("UnapprovedOriginalManifestDifference");
        Require(result.buildRejected, "StandardBuildDidNotRejectExactSourceDrift: " + result.buildFailure);
        result.status = "PASS_DAILY_PLAY_AND_STRICT_BOUNDARIES"; result.phase = "complete"; Write(result);
    }
    private static bool Rejects(Action action, string code)
    {
        try { action(); return false; }
        catch (InvalidOperationException error) { return error.Message.Contains(code); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static Result Read() => JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(Output, "development-probe.json")));
    private static void Write(Result result) => File.WriteAllText(Path.Combine(Output, "development-probe.json"), JsonUtility.ToJson(result, true));
    private static void Finish(int code)
    {
        SessionState.EraseString(Key + "Output"); SessionState.EraseString(Key + "Phase");
        EditorApplication.update -= Tick; ReleaseApi(); EditorApplication.Exit(code);
    }
}
