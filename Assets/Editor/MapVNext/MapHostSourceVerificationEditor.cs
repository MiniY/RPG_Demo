using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Keep interactive Test Runner runs and all BuildPipeline entry points strict.</summary>
[InitializeOnLoad]
public sealed class MapHostSourceVerificationEditor : IPreprocessBuildWithReport
{
    private const string TestRunKey = "RPG_Demo.HostSourceVerification.TestRunActive";
    private static readonly TestRunnerApi api;
    private static readonly TestCallbacks callbacks;
    static MapHostSourceVerificationEditor()
    {
        MapHostCompatibility.EditorTestRunActive = () => SessionState.GetBool(TestRunKey, false);
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        callbacks = new TestCallbacks();
        api.RegisterCallbacks(callbacks, 10000);
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseApi;
    }
    private static void ReleaseApi()
    {
        if (api == null) return;
        api.UnregisterCallbacks(callbacks);
        UnityEngine.Object.DestroyImmediate(api);
    }
    private sealed class TestCallbacks : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) => SessionState.SetBool(TestRunKey, true);
        public void RunFinished(ITestResultAdaptor result) => SessionState.SetBool(TestRunKey, false);
        public void OnError(string message) => SessionState.SetBool(TestRunKey, false);
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { MapVNextI1Build.VerifySourceInputsForBuild(); }
        catch (Exception error) { throw new BuildFailedException("Host source verification failed: " + error.Message); }
    }
}
