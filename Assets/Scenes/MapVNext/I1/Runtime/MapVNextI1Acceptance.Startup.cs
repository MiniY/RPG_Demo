using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class StartupRow
    {
        public string phase, utc, entry, target, host, slots, legacy, preferences, persistent, company, product, boundStoreRoot, failure;
        public int preferenceReference, frame;
        public bool capability, sessionExists, rejected, ready;
    }
    [Serializable] private sealed class StartupRows { public List<StartupRow> rows = new List<StartupRow>(); }
    private static readonly StartupRows startupRows = new StartupRows();
    internal static string StartupArgument(string key) => Argument(key);
    private static void ObserveStartupQuitting() { try { ObserveStartupBoundary("ProcessQuitting"); } catch (Exception error) { Debug.LogWarning("Startup quitting evidence: "+error.GetType().Name); } }
    internal static void ObserveStartupBoundary(string phase)
    {
        string directory = Argument("--m0-startup-proof");
        if (directory == null) return;
        string owned = Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");
        if (string.IsNullOrEmpty(owned) || !Path.GetFullPath(directory).Equals(Path.GetFullPath(Path.Combine(owned,"Player")),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OwnedStartupEvidencePathRequired");
        if (phase == "GateBefore")
        {
            startupRows.rows.Clear();
            Application.quitting-=ObserveStartupQuitting;Application.quitting+=ObserveStartupQuitting;
            if (Application.companyName != "CodexMapAcceptance" || Application.productName != "RPG_M0_" + MapHostBuildStamp.BuildId + "_" + MapHostBuildStamp.StartupTarget)
                throw new InvalidOperationException("OrdinaryStartupProofRequiresBakedShadowCompanyProductBeforeSession");
        }
        var preferences = TowerRunRuntimeEnvironment.Preferences;
        var row = new StartupRow
        {
            phase=phase, utc=DateTime.UtcNow.ToString("o"), entry=MapVNextI1Bootstrap.FirstEntryPath(), target=MapHostBuildStamp.StartupTarget,
            host=MapHostBuildStamp.BuildId, slots=TowerRunRuntimeEnvironment.SlotRoot, legacy=TowerRunRuntimeEnvironment.LegacySavePath,
            preferences=preferences.GetType().FullName, preferenceReference=RuntimeHelpers.GetHashCode(preferences),
            persistent=Application.persistentDataPath, company=Application.companyName, product=Application.productName,
            capability=MapHostBuildStamp.HasExplicitI1StartupCapability, sessionExists=GameSession.Instance!=null,
            rejected=MapVNextI1Bootstrap.StartupRejected, failure=MapVNextI1Bootstrap.StartupFailure, frame=Time.frameCount
        };
        if (GameSession.Instance != null && GameSession.Instance.TowerRun != null)
        {
            var coordinator=GameSession.Instance.TowerRun;
            row.boundStoreRoot=(string)coordinator.GetType().GetField("slotStoreRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(coordinator);
            row.ready=coordinator.State==TowerRunFlowState.Playing;
        }
        var activeMap=UnityEngine.Object.FindObjectOfType<MapGenerationController>(true);
        // The public Orchestrator getter initializes state. Diagnostics may only inspect the existing field.
        var existing=activeMap==null?null:(MapIntegrationOrchestrator)typeof(MapGenerationController).GetField("orchestrator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(activeMap);
        row.ready=row.ready||(existing!=null&&existing.Context!=null&&existing.Context.IsReady);
        if(row.ready)Debug.Log("M0_STARTUP_PROOF_READY_OBSERVED");
        startupRows.rows.Add(row);Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"startup-early.json"),JsonUtility.ToJson(startupRows,true));
    }
    private IEnumerator ProveStartupAndRestore()
    {
        Require(startupRows.rows.Count==2,"ExactlyBeforeAndAfterGateRequired");
        var first=startupRows.rows[0];var after=startupRows.rows[1];
        Require(!first.sessionExists&&!after.sessionExists,"GateMustPrecedeFirstGameSession");
        if(!MapHostBuildStamp.HasExplicitI1StartupCapability)
        {
            Require(after.phase=="GateAfterNormalNoOverride" && first.slots==after.slots && first.legacy==after.legacy && first.preferenceReference==after.preferenceReference && after.preferences==typeof(TowerRunPlayerPrefsStore).FullName,"DefaultGateChangedOriginalSelectors");
            Require(Path.GetFullPath(after.slots)==Path.GetFullPath(Path.Combine(Application.persistentDataPath,"tower-run-slots")),"NormalPathMustBeOriginalBakedShadowNamespace");
        }
        else Require(after.phase=="GateAfterExplicitI1Isolation" && after.preferences==typeof(TowerRunMemoryPreferenceStore).FullName && after.slots.StartsWith(Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT"),StringComparison.OrdinalIgnoreCase),"ExplicitI1IsolationMissingBeforeSession");
        ObserveStartupBoundary("FirstSessionBindingObserved");
        var bound=startupRows.rows[startupRows.rows.Count-1];
        Require(bound.boundStoreRoot==after.slots && bound.preferences==after.preferences && bound.preferenceReference==after.preferenceReference,"FirstStoreBindingOrPreferencesChangedAfterEarlyGate");
        string source=Environment.GetEnvironmentVariable("RPG_I1_RESTORE_COPY");Require(!string.IsNullOrEmpty(source),"RealCopiedRestoreInputRequired");
        byte[] original=File.ReadAllBytes(source);var expected=JsonUtility.FromJson<TowerRunCheckpointRecord>(System.Text.Encoding.UTF8.GetString(original));
        string input=Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json");
        Require(File.Exists(input)&&MapHostCompatibility.Sha(File.ReadAllBytes(input))==MapHostCompatibility.Sha(original),"EarliestBoundStoreMustContainExactPreparedCopy");
        bool versioned=MapHostBuildStamp.HasExplicitI1StartupCapability;
        Require(expected.generation.profileId==(versioned?EcologyRecipeDefinition.VNextI1ProfileId:EcologyRecipeDefinition.MainV1ProfileId),"RestoreContentMustMatchExplicitTarget");
        var read=session.ReadSlot(1);Require(read.state==TowerRunSlotState.Valid,"CopiedSlotInvalid:"+read.message);
        session.BeginRestoreSlot(1,read.confirmationToken);
        Require(session.State==TowerRunFlowState.Playing&&Context.IsReady&&(Context.ActiveSpatialContext!=null)==versioned,"CopiedRestoreDidNotReachCorrectGameplayReady:"+session.FailureMessage);
        var entrance=Entrance();
        Require(JsonUtility.ToJson(entrance.generation)==JsonUtility.ToJson(expected.generation),"EntireCopiedGenerationChanged");
        Require(JsonUtility.ToJson(entrance.player)==JsonUtility.ToJson(expected.player),"EntireCopiedPlayerChanged");
        string world=MapHostStateSnapshot.Capture(map);File.WriteAllText(Path.Combine(output,"Restored-world.txt"),world);
        var prior=session.ReadSlot(2);var write=session.TrySaveEntranceCandidate(2,entrance,prior.state!=TowerRunSlotState.Empty,prior.confirmationToken);
        Require(write.succeeded,"ActualSaveFailed:"+write.message);
        string saved=Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-2.json");
        var persisted=session.ReadSlot(2);Require(persisted.state==TowerRunSlotState.Valid,"ActualSavedSlotInvalid");
        File.Copy(saved,Path.Combine(output,"CrossProcess-Checkpoint.json"),false);
        session.ReturnToStartupChoice();session.BeginRestoreSlot(2,persisted.confirmationToken);
        Require(session.State==TowerRunFlowState.Playing&&Context.IsReady&&(Context.ActiveSpatialContext!=null)==versioned,"ActualReopenDidNotReachReady");
        Require(MapHostStateSnapshot.Capture(map)==world,"CompleteWorldChangedAfterReopenIncludingPositionsCollisionPlanRegistryAndAttempts");
        var reopened=Entrance();Require(JsonUtility.ToJson(reopened.generation)==JsonUtility.ToJson(entrance.generation)&&JsonUtility.ToJson(reopened.player)==JsonUtility.ToJson(entrance.player),"CompleteReopenedCheckpointChanged");
        File.WriteAllText(Path.Combine(output,"Reopened-world.txt"),MapHostStateSnapshot.Capture(map));
        File.WriteAllText(Path.Combine(output,"Reopened-request.json"),JsonUtility.ToJson(reopened,true));
        Require(MapHostCompatibility.Sha(File.ReadAllBytes(source))==MapHostCompatibility.Sha(original),"CopiedSourceWasModified");
        evidence.checks.Add("Actual "+MapHostBuildStamp.StartupTarget+" early binding; copied supported content GameplayReady, actual timestamped disk save and complete-world reopen; original source unchanged.");
        ObserveStartupBoundary("RestoredSavedReopenedReady");
        yield break;
    }
}
