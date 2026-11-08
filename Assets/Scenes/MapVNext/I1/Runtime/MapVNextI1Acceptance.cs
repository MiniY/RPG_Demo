using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using MapVNext;

/// <summary>Explicit opt-in evidence over the formal session. Never publishes Ready or owns terrain.</summary>
public sealed partial class MapVNextI1Acceptance : MonoBehaviour
{
    [Serializable] private sealed class InputRow
    {
        public int requestedSeed,actualSeed,attempt,monsters,destructibles,stairs;
        public string status,reason,profile,generation,movementGeneration,mapDigest,ecologyIdentity,spawnImage,overviewImage;
    }
    [Serializable] private sealed class Evidence
    {
        public string status="RUNNING",failure,buildId,implementationDigest,phase,slotRoot,sourceFingerprint;
        public string playerChecks="Embedded host group vs compiled stamp; source filesystem integrity not executed in Player.";
        public string inputScope="InputSystem queued keyboard -> GameInput -> original PlayerAction -> Rigidbody2D. Optional Mouse Battle self-defense, when enabled, uses the original damage pipeline and is separately logged in combat-input.csv. Positive routes walk from spawn; no health, AI or collision overrides. Separate negative trial setup is explicitly logged and not counted as movement.";
        public List<InputRow> inputs=new List<InputRow>();public List<string> checks=new List<string>();
    }
    private TowerRunCoordinator session;
    private MapGenerationController map;
    private CircleCollider2D foot;
    private Rigidbody2D body;
    private PlayerAction action;
    private Keyboard keyboard;
    private MapVNextCliffExclusion rock;
    private InputSettings.BackgroundBehavior previousBackground;
    private bool previousRunInBackground;
    private string output,phase,keys="None",trial="Idle";
    private Evidence evidence;
    private Exception terminalFirstFailure;
    private readonly List<string> trace=new List<string> {"phase,keys,physicsTime,frame,bodyX,bodyY,footX,footY,moveX,moveY,height,generation,buildId"};
    private readonly List<string> frames=new List<string> {"image,physicsTime,frame,phase,keys,bodyX,bodyY,footX,footY,generation,buildId"};
    private MapRuntimeContext Context => map.Orchestrator.Context;
    private Vector2 FootPosition => foot.bounds.center;
    private static string F(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static void Require(bool condition,string reason) {if(!condition) throw new InvalidOperationException(reason);}
    private static string Argument(string name)
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BeginWhenExplicitlyRequested()
    {
        if(!MapHostBuildStamp.IsReviewedCompatibleHost || MapVNextI1Bootstrap.StartupRejected || (Argument("--i1-accept")==null && Argument("--m0-startup-proof")==null)) return;
        var actor=new GameObject("I1ExplicitAcceptance");actor.AddComponent<MapVNextI1Acceptance>();
    }
    private IEnumerator Start()
    {
        string requestedOutput=Argument("--m0-startup-proof")??Argument("--i1-accept");if(requestedOutput==null) yield break;output=requestedOutput;
        phase=Argument("--i1-phase")??(Argument("--m0-startup-proof")!=null?"startup":"pilot");Directory.CreateDirectory(output);
        evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase=phase,slotRoot=TowerRunRuntimeEnvironment.SlotRoot,sourceFingerprint=Environment.GetEnvironmentVariable("RPG_I1_SOURCE_FINGERPRINT")};
        var stack=new Stack<IEnumerator>();stack.Push(Run());Exception first=null;var secondary=new List<string>();bool coreCompleted=false;
        void Retain(Exception error,string stage) {if(first==null)first=error;else if(!ReferenceEquals(first,error))secondary.Add(stage+": "+error);}
        void Attempt(Action operation,string stage) {try {operation();}catch(Exception error) {Retain(error,stage);}}
        try
        {
            while(first==null&&stack.Count>0)
            {
                if(diagnosticHeartbeatFailure!=null) {Retain(diagnosticHeartbeatFailure,"passive heartbeat");break;}
                object current=null;bool moved=false;
                try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error) {Retain(error,"original Player sequence");break;}
                if(!moved) {var ended=stack.Pop();Attempt(()=>{(ended as IDisposable)?.Dispose();},"completed Player iterator");continue;}
                if(current is IEnumerator nested)stack.Push(nested);
                else {diagnosticLastYield=current?.GetType().FullName??"NextFrame";yield return current;}
            }
            coreCompleted=first==null&&stack.Count==0;
        }
        finally
        {
            while(stack.Count>0) {var pending=stack.Pop();Attempt(()=>{(pending as IDisposable)?.Dispose();},"pending Player iterator");}
            Attempt(FlushConsumerTrace,"retain real consumer trace");if(keyboard!=null)Attempt(CleanupInput,"restore original input");
            if(diagnosticHeartbeatFailure!=null)Retain(diagnosticHeartbeatFailure,"passive heartbeat");
            foreach(string detail in secondary)evidence.checks.Add("SECONDARY_PLAYER_CLEANUP: "+detail);
            first=PersistTerminalEvidence(first,secondary,coreCompleted,"AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING","INCOMPLETE");
            foreach(string detail in secondary)Debug.LogWarning("Secondary Player cleanup: "+detail);
        }
        Debug.Log("I1_PLAYER_ACCEPTANCE "+evidence.status+" "+first);
        Application.Quit(first==null&&coreCompleted?0:2);
    }
    private IEnumerator Run()
    {
        yield return null;session=GameSession.Instance.TowerRun;map=FindObjectOfType<MapGenerationController>(true);
        Require(session!=null&&map!=null&&session.State==TowerRunFlowState.StartupChoice,"FormalStartupChoiceMissing");
        if(phase=="startup") {yield return ProveStartupAndRestore();yield break;}
        if(phase=="formal") {yield return ProveFormalNewGame();yield break;}
#if UNITY_EDITOR || RPG_SHOP_INTEGRATION_TESTS
        if(phase=="shop") {yield return ProveShopIntegration();if(Environment.GetEnvironmentVariable("RPG_UI_PERFORMANCE")=="1")yield return ProveUIPerformance();if(Environment.GetEnvironmentVariable("RPG_UI_ARCHITECTURE_ACCEPTANCE")=="1")yield return ProveUIArchitecture();yield break;}
#endif
        Require(TowerRunRuntimeEnvironment.SlotRoot.StartsWith(Environment.GetEnvironmentVariable("RPG_I1_RUNTIME_ROOT")??"UNSPECIFIED",StringComparison.OrdinalIgnoreCase),"PrivateDataRootNotAppliedBeforeStartup");
        autoDefense=true;BindInput();if(capturePlayerFrames)yield return CaptureFrame("Startup.png","Startup");
        if(phase=="restore") {yield return RestoreInFreshProcess();yield break;}
        int[] seeds=phase=="full"?MapVNextRandomLayout.Seeds:new[] {42,21193,7727,314159};int successful=0,consumerTrials=0;
        foreach(int seed in seeds)
        {
            session.ReturnToStartupChoice();var row=new InputRow {requestedSeed=seed};evidence.inputs.Add(row);
            session.BeginNewRunWithSeed(seed);
            if(session.State==TowerRunFlowState.Failure)
            {
                row.reason=Context.Failure?.Code+":"+session.FailureMessage;
                bool bounded=Context.Failure!=null&&(Context.Failure.Code=="VNextFormalLayoutRejected"||Context.Failure.Code=="EcologyBudgetExhaustedOnFixedTerrain");
                row.status=bounded?"BOUNDED_REJECTION_NOT_READY":"IMPLEMENTATION_FAILURE_NOT_READY";Persist();
                Require(bounded,"UnexpectedGenerationFailure:"+row.reason);continue;
            }
            Ready();successful++;BindPlayer();
            var snapshot=Context.CreateDiagnosticSnapshot();var accepted=snapshot.AttemptChain.Last(a=>a.Outcome==MapAttemptOutcome.Succeeded);
            row.actualSeed=accepted.Attempt.AttemptSeed;row.attempt=accepted.Attempt.AttemptIndex;row.profile=snapshot.ActiveRequest.ProfileId;row.generation=Context.ActiveGenerationId.ToString();
            row.mapDigest=MapVNextRandomLayout.SemanticHash(Context.ActiveMap);row.ecologyIdentity=JsonUtility.ToJson(Entrance().generation.ecology);
            row.monsters=Context.ActiveRegistry.Entries.Count(e=>e.Role==MonsterPlacementService.MonsterRole&&e.Instance!=null);
            row.destructibles=Context.ActiveRegistry.Entries.Count(e=>e.Role==DestructiblePlacementService.DestructibleRole&&e.Instance!=null);row.stairs=Context.ActiveMap.Stairs.Count;
            Require(row.monsters==Context.ActivePlacementPlan.Ecology.Residents.Count,"PlannedPopulationAndRegistryDiffer");
            yield return null;
            if(capturePlayerFrames)
            {
                row.spawnImage="Screenshots/Seed-"+seed+"-Spawn.png";yield return CaptureFrame(row.spawnImage,"Spawn");
                row.overviewImage="Screenshots/Seed-"+seed+"-Overview.png";CaptureOverview(row.overviewImage);
            }
            ExportMap(seed);SaveAndReopen(2);BindPlayer();row.movementGeneration=Context.ActiveGenerationId.ToString();ExportMap(seed);
            if(phase=="pilot"||successful<=12) yield return ObserveStairWalk(seed,phase=="pilot"&&capturePlayerFrames,false);
            row.status="READY_SAVED_REOPENED";Persist();
            if(phase=="pilot"&&seed==ConsumerFixtureSeed) {RestoreCompletedWalkForConsumerFixture(seed);consumerTrials++;yield return RunConsumerChecks();Ready();}
        }
        Require(phase!="pilot"||consumerTrials==1,"PredeclaredConsumerInputUnavailableNoFallback");
        Require(successful>=2,"FewerThanTwoRealPlayableInputs");
        Require(combatTrace.Any(r=>r.Contains(",GameInputBattle,"))&&combatTrace.Any(r=>r.Contains(",PlayerAttackPerformed-"))&&combatTrace.Any(r=>r.Contains(",OriginalDamage-health-")),"OriginalBattleInputAndActualDamageNotObserved");
        evidence.checks.Add("All attempted inputs retained; rejection is not Ready. Successful inputs use formal owners and new content identity.");
    }
    private void Ready()
    {Require(session.State==TowerRunFlowState.Playing&&Context.IsReady&&Context.ActiveSpatialContext!=null,"FormalVNextReadyMissing:"+session.FailureMessage);}
    private TowerRunCheckpointRecord Entrance()
    {Require(session.TryCreateSaveRequest(out var record,out string reason),reason);return record;}
    private void SaveAndReopen(int slot)
    {
        var before=Entrance();var read=session.ReadSlot(slot);
        var write=session.TrySaveEntranceCandidate(slot,before,read.state!=TowerRunSlotState.Empty,read.confirmationToken);
        Require(write.succeeded,"PrivateSaveFailed:"+write.message);var persisted=session.ReadSlot(slot);Require(persisted.state==TowerRunSlotState.Valid,"PrivateReadFailed:"+persisted.message);
        session.ReturnToStartupChoice();session.BeginRestoreSlot(slot,persisted.confirmationToken);Ready();var after=Entrance();
        Require(JsonUtility.ToJson(before.generation)==JsonUtility.ToJson(after.generation),"ReopenedGenerationIdentityChanged");
        Require(JsonUtility.ToJson(before.player)==JsonUtility.ToJson(after.player),"ReopenedPlayerDataChanged");
        evidence.checks.Add("Saved/reopened seed="+before.generation.requestedSeed+" floor="+before.floor.floorNumber);
        // Preserve the original store's validated timestamp and exact persisted checkpoint bytes.
        File.Copy(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-"+slot+".json"),Path.Combine(output,"CrossProcess-Checkpoint.json"),true);
    }
    public IEnumerator RunRecordedFullNavigationForTests()
    {
        output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");Directory.CreateDirectory(output);
        phase="full";capturePlayerFrames=false;recordedFullNavigationDiagnostic=true;
        evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase="EDITOR_FULL_SEQUENCE_NATIVE_DIAGNOSTIC",slotRoot=TowerRunRuntimeEnvironment.SlotRoot};
        evidence.checks.Add("Editor batch diagnostic preserves original full input order and Battle; frame captures skipped because WaitForEndOfFrame is unsupported. Windows screenshot timing is not matched.");
        var pending=new Stack<IEnumerator>();pending.Push(Run());Exception first=null;var secondary=new List<string>();bool coreCompleted=false;
        void Retain(Exception error,string stage) {if(first==null)first=error;else secondary.Add(stage+": "+error);}
        void Attempt(Action operation,string stage) {try {operation();}catch(Exception error) {Retain(error,stage);}}
        try
        {
            while(first==null&&pending.Count>0)
            {
                bool moved=false;object current=null;
                try {moved=pending.Peek().MoveNext();if(moved)current=pending.Peek().Current;}
                catch(Exception error) {Retain(error,"original full sequence");break;}
                if(!moved) {var ended=pending.Pop();Attempt(()=>{(ended as IDisposable)?.Dispose();},"completed full iterator");continue;}
                if(current is IEnumerator nested)pending.Push(nested);else {diagnosticLastYield=current?.GetType().FullName??"NextFrame";yield return current;}
            }
            coreCompleted=first==null&&pending.Count==0;
        }
        finally
        {
            while(pending.Count>0) {var remaining=pending.Pop();Attempt(()=>{(remaining as IDisposable)?.Dispose();},"pending full iterator");}
            if(diagnosticHeartbeatFailure!=null)Retain(diagnosticHeartbeatFailure,"passive heartbeat");
            Attempt(StopConsumerLifecycleObservation,"detach resident observation");
            Attempt(FlushConsumerTrace,"retain real consumer trace");if(keyboard!=null)Attempt(CleanupInput,"restore original input");
            recordedFullNavigationDiagnostic=false;
            foreach(string detail in secondary)evidence.checks.Add("SECONDARY_FULL_DIAGNOSTIC_CLEANUP: "+detail);
            first=PersistTerminalEvidence(first,secondary,coreCompleted,"EDITOR_FULL_DIAGNOSTIC_COMPLETE_SEE_XML","INCOMPLETE_SEE_XML");
            foreach(string detail in secondary)Debug.LogWarning("Secondary full diagnostic cleanup: "+detail);
        }
        if(first!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
    private IEnumerator RestoreInFreshProcess()
    {
        string path=Environment.GetEnvironmentVariable("RPG_I1_RESTORE_COPY");Require(!string.IsNullOrEmpty(path),"RestoreCopyMissing");
        var expected=JsonUtility.FromJson<TowerRunCheckpointRecord>(File.ReadAllText(path));Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);
        File.Copy(path,Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json"),false);
        var read=session.ReadSlot(1);Require(read.state==TowerRunSlotState.Valid,read.message);session.BeginRestoreSlot(1,read.confirmationToken);Ready();BindPlayer();
        Require(JsonUtility.ToJson(Entrance().generation)==JsonUtility.ToJson(expected.generation),"FreshProcessGenerationMismatch");
        Require(JsonUtility.ToJson(Entrance().player)==JsonUtility.ToJson(expected.player),"FreshProcessPlayerMismatch");
        yield return CaptureFrame("FreshProcess-Restored.png","FreshProcessRestored");SaveAndReopen(2);evidence.checks.Add("Fresh Player process loaded copied VNext checkpoint to actual GameplayReady and saved/reopened.");
    }
    private Exception PersistTerminalEvidence(Exception first,List<string> secondary,bool coreCompleted,string successStatus,string incompleteStatus)
    {
        void Retain(Exception error,string stage)
        {
            if(first==null)first=error;else if(!ReferenceEquals(first,error))secondary.Add(stage+": "+error);
            evidence.checks.Add("TERMINAL_EVIDENCE_IO_FAILURE "+stage+": "+error);
        }
        void Attempt(Action operation,string stage) {try {operation();}catch(Exception error) {Retain(error,stage);}}
        void Refresh() {evidence.failure=first?.ToString();evidence.status=first!=null?"FAIL":coreCompleted?successStatus:incompleteStatus;}
        // A complete marker is written only after both final trace writes were attempted.
        Attempt(()=>File.WriteAllLines(Path.Combine(output,"movement.csv"),trace),"final movement");
        Attempt(()=>File.WriteAllLines(Path.Combine(output,"frames.csv"),frames),"final frames");
        Refresh();
        try {File.WriteAllText(Path.Combine(output,"i1-player-evidence.json"),JsonUtility.ToJson(evidence,true));}
        catch(Exception error)
        {
            Retain(error,"final state JSON");Refresh();
            evidence.checks.Add("TERMINAL_CANONICAL_JSON_UNCONFIRMED; any older JSON must not be treated as terminal success.");
            // A separately named failure marker does not claim that the locked canonical file changed.
            Attempt(()=>
            {
                using(var stream=new FileStream(Path.Combine(output,"i1-terminal-failure.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(evidence,true));
            },"terminal failure sidecar");
            Debug.LogWarning("I1_TERMINAL_STATE_UNCONFIRMED; memory status=FAIL; first="+first);
        }
        if(terminalFirstFailure==null)terminalFirstFailure=first;
        return first;
    }
    private void Persist()
    {if(evidence==null||output==null)return;File.WriteAllText(Path.Combine(output,"i1-player-evidence.json"),JsonUtility.ToJson(evidence,true));File.WriteAllLines(Path.Combine(output,"movement.csv"),trace);File.WriteAllLines(Path.Combine(output,"frames.csv"),frames);}
    private void OnDestroy()
    {
        Exception first=terminalFirstFailure;var secondary=new List<string>();bool cleanupFailed=false;
        void Attempt(Action operation,string stage)
        {try {operation();}catch(Exception error) {cleanupFailed=true;if(first==null)first=error;else if(!ReferenceEquals(first,error))secondary.Add(stage+": "+error);}}
        Attempt(StopResidentGroupObservation,"detach all resident traces");
        Attempt(FlushResidentGroupObservation,"flush resident group rings");
        Attempt(StopConsumerLifecycleObservation,"detach resident trace");
        Attempt(FlushConsumerTrace,"flush consumer trace");
        Attempt(CleanupInput,"restore input");
        bool terminalAttempted=evidence!=null&&output!=null&&(cleanupFailed||evidence.status=="RUNNING");
        if(terminalAttempted)
        {
            foreach(string detail in secondary)evidence.checks.Add("SECONDARY_DESTROY_CLEANUP: "+detail);
            first=PersistTerminalEvidence(first,secondary,false,"COMPLETE","INCOMPLETE_SEE_XML_OR_PLAYER_FAILURE");
        }
        if(first!=null&&(cleanupFailed||terminalAttempted))System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
    }
}
