using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reviewed implementation identity and explicit private startup are separate contracts.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class MapVNextI1Bootstrap : MonoBehaviour
{
    public const string I1ScenePath = "Assets/Scenes/MapVNext/I1/I1Game.unity";
    public EcologyRecipeDefinition VNextRecipe;
    public EcologyRecipeDefinition MainV1Recipe;
    public EcologyRecipeDefinition LegacyVNextRecipe;
    public EcologyRecipeDefinition PreviousCurrentVNextRecipe;
    public MapGenerationController Controller;
    public string RuntimeRoot { get; private set; }
    public static bool StartupRejected { get; private set; }
    public static string StartupFailure { get; private set; }

    public static string FirstEntryPath()
    {
#if UNITY_EDITOR
        var selected = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
        return selected != null ? UnityEditor.AssetDatabase.GetAssetPath(selected) : SceneManager.GetActiveScene().path;
#else
        return SceneUtility.GetScenePathByBuildIndex(0);
#endif
    }
    public static void ValidateStartupTarget(string entry, bool capability, string requested)
    {
        if (requested != null && requested != "normal" && requested != "i1") throw new InvalidOperationException("UnknownStartupRequest");
        bool i1Entry = entry == I1ScenePath;
        if (capability != i1Entry || requested == "i1" && !capability || requested == "normal" && capability)
            throw new InvalidOperationException("I1StartupCapabilityOrEntryMismatch");
    }
    // This callback executes after subsystem reset and before GameSession's BeforeSceneLoad.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void IsolateBeforeSession()
    {
        StartupRejected = false; StartupFailure = null;
        try
        {
            MapVNextI1Acceptance.ObserveStartupBoundary("GateBefore");
            string entry = FirstEntryPath();
            string requested = MapVNextI1Acceptance.StartupArgument("--m0-startup-request");
            bool explicitI1 = MapHostBuildStamp.HasExplicitI1StartupCapability || entry == I1ScenePath ||
                requested == "i1" || MapVNextI1Acceptance.StartupArgument("--i1-accept") != null;
            ValidateStartupTarget(entry, MapHostBuildStamp.HasExplicitI1StartupCapability, requested);
            if (!explicitI1)
            {
                MapVNextI1Acceptance.ObserveStartupBoundary("GateAfterNormalNoOverride");
                return;
            }
            if (!MapHostBuildStamp.HasExplicitI1StartupCapability) throw new InvalidOperationException("ExplicitI1BuildCapabilityRequired");
            if (Environment.GetEnvironmentVariable("RPG_MAP_VNEXT_ISOLATED") == "1") throw new InvalidOperationException("I1CannotMixStandaloneFixtureStartup");
            var source = Resources.Load<TextAsset>("MapVNextI1Host");
            if (source == null) throw new InvalidOperationException("ReviewedI1HostMissing");
            MapHostCompatibility.ValidateDocument(source.text, EcologyRecipeDefinition.VNextI1ProfileId, MapHostBuildStamp.NewContentVersion);
            string requestedRoot = Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT");
            string root = Path.GetFullPath(string.IsNullOrWhiteSpace(requestedRoot) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RPG_Demo-I1", MapHostBuildStamp.BuildId) : requestedRoot);
            RequirePrivateRoot(root);
            TowerRunRuntimeEnvironment.ConfigureTestIsolation(root, new TowerRunMemoryPreferenceStore());
            MapVNextI1Acceptance.ObserveStartupBoundary("GateAfterExplicitI1Isolation");
        }
        catch (Exception error)
        {
            // Unity may continue other callbacks after an exception/quit request. Never leave formal IO selected.
            string quarantine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RPG_Demo-I1", "RejectedStartup", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            TowerRunRuntimeEnvironment.ConfigureTestIsolation(quarantine, new TowerRunMemoryPreferenceStore());
            StartupRejected = true; StartupFailure = error.GetType().Name + ":" + error.Message;
            try { MapVNextI1Acceptance.ObserveStartupBoundary("RejectedPrivateQuarantine"); }
            catch (Exception secondary) { Debug.LogWarning("Startup evidence secondary failure: " + secondary.GetType().Name); }
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(2);
#endif
            throw;
        }
    }
    private static void RequirePrivateRoot(string root)
    {
        string formal = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData/LocalLow/DefaultCompany/RPG_Demo"));
        if (root.Equals(formal, StringComparison.OrdinalIgnoreCase) || root.StartsWith(formal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("I1CannotUseFormalDataDirectory");
    }
    private void Awake()
    {
        if (StartupRejected) { enabled = false; return; }
        if (Environment.GetEnvironmentVariable("RPG_MAP_VNEXT_ISOLATED") == "1") throw new InvalidOperationException("I1FormalSceneCannotUseStandaloneFixtureMode");
        RuntimeRoot = Path.GetFullPath(Path.GetDirectoryName(TowerRunRuntimeEnvironment.SlotRoot));
        RequirePrivateRoot(RuntimeRoot);
        var coordinator = GameSession.Instance == null ? null : GameSession.Instance.TowerRun;
        if (VNextRecipe == null || LegacyVNextRecipe == null || PreviousCurrentVNextRecipe == null || MainV1Recipe == null || Controller == null || coordinator == null ||
            VNextRecipe.DeclaredProfileId != EcologyRecipeDefinition.VNextI1ProfileId || PreviousCurrentVNextRecipe.DeclaredProfileId != EcologyRecipeDefinition.VNextI1ProfileId || LegacyVNextRecipe.DeclaredProfileId != EcologyRecipeDefinition.VNextI1ProfileId || MainV1Recipe.DeclaredProfileId != EcologyRecipeDefinition.MainV1ProfileId)
            throw new InvalidOperationException("I1ExplicitContentInputsMissing");
        if (!MapHostBuildStamp.IsReviewedCompatibleHost) throw new InvalidOperationException("I1HostMustBeExplicitlySealed");
        if (GetComponent<MapVNextI1Panel>() == null) gameObject.AddComponent<MapVNextI1Panel>();
        Controller.ConfigureRetainedEcologyRecipes(new[] { VNextRecipe, PreviousCurrentVNextRecipe, LegacyVNextRecipe, MainV1Recipe });
        coordinator.ConfigureNewRunProfile(EcologyRecipeDefinition.VNextI1ProfileId);
        Debug.Log("I1_ISOLATION slots=" + TowerRunRuntimeEnvironment.SlotRoot + " prefs=memory host=" + MapHostBuildStamp.BuildId);
    }
}
