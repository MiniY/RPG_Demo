using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Read-only callbacks observe the original movement owner. The opt-in entry
// drives the existing real-input walk; observation never resets leases, changes
// colliders, issues attacks or suppresses AI.
public sealed partial class MapVNextI1Acceptance
{
    private bool recordedFullNavigationDiagnostic;
    private Exception diagnosticHeartbeatFailure;
    private string diagnosticLastYield="NotStarted";
    private double nextDiagnosticHeartbeat;
    private bool diagnosticHeartbeatStarted;
    private void Update()
    {
        if(string.IsNullOrEmpty(output)||evidence==null||diagnosticHeartbeatFailure!=null||Time.realtimeSinceStartupAsDouble<nextDiagnosticHeartbeat)return;
        nextDiagnosticHeartbeat=Time.realtimeSinceStartupAsDouble+1d;
        try
        {
            var binding=body==null?null:body.GetComponent<EcologyCombatBinding>();
            var barrier=binding==null?null:binding.Barrier;
            var motion=body==null?null:body.GetComponent<EcologyMotionBody>();
            var health=body==null?null:body.GetComponent<PlayerDamageController>();
            // Do not call CanAct, IsLive, IsReady or any owner scope predicate.
            // Their callbacks are executable owner code; these are passive fields only.
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            object MotionField(string name)=>motion==null?null:typeof(EcologyMotionBody).GetField(name,flags).GetValue(motion);
            string Csv(object value)=>(value?.ToString()??"Unavailable").Replace(",","_").Replace("\r"," ").Replace("\n"," ");
            string row=string.Join(",",DateTime.UtcNow.ToString("O"),Time.realtimeSinceStartupAsDouble.ToString("R",System.Globalization.CultureInfo.InvariantCulture),Time.frameCount,F(Time.fixedTime),F(Time.timeScale),
                Csv(session?.State),map==null||Context.ActiveMap==null?"Unavailable":Context.ActiveMap.GenerationSeed.ToString(),Csv(map==null?null:Context.ActiveGenerationId),Csv(trial),Csv(diagnosticLastYield),
                TowerRunGateHub.IsWorldPaused,TowerRunGateHub.IsGameplayBlocked,body==null?"Unavailable":F(body.position.x),body==null?"Unavailable":F(body.position.y),health==null?"Unavailable":F(health.CurrentHealth),Csv(health==null?(object)null:health.IsDefeated),
                Csv(binding==null?(object)null:binding.isActiveAndEnabled),Csv(barrier==null?(object)null:barrier.IsScopeOpen),Csv(barrier==null?(object)null:barrier.CurrentStep),Csv(binding==null?(object)null:binding.Lease),Csv(binding==null?(object)null:binding.GenerationId),Csv(binding==null?(object)null:binding.MotionFaultRevision),
                Csv(motion==null?(object)null:motion.isActiveAndEnabled),Csv(MotionField("legacyReleased")),Csv(MotionField("awaitingPreparedActivation")),Csv(MotionField("boundLease")),motion!=null&&ReferenceEquals(MotionField("barrier"),barrier),false,false,MapHostBuildStamp.BuildId)+Environment.NewLine;
            string path=Path.Combine(output,"passive-heartbeat.csv");
            if(!diagnosticHeartbeatStarted)File.WriteAllText(path,"utc,wallSeconds,frame,physicsTime,timeScale,session,seed,generation,trial,lastYield,worldPaused,gameplayBlocked,bodyX,bodyY,health,defeated,bindingEnabled,barrierOpen,barrierStep,lease,bindingGeneration,motionFault,motionEnabled,legacyReleased,awaitingPreparedActivation,boundLease,sameBarrier,motionReadyProvided,bindingLiveProvided,buildId"+Environment.NewLine+row);
            else File.AppendAllText(path,row);
            diagnosticHeartbeatStarted=true;
        }
        catch(Exception error) {diagnosticHeartbeatFailure=error;}
    }

    public IEnumerator RunStairDiagnosticForTests() => ObserveStairWalk(21193,false,true);
    public IEnumerator RunRecorded8675309NavigationDiagnosticForTests() => ObserveStairWalk(8675309,false,true);
    private IEnumerator ObserveStairWalk(int seed,bool capture,bool editorSetup)
    {
        if(editorSetup)
        {
            output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");Directory.CreateDirectory(output);
            session=GameSession.Instance.TowerRun;map=FindObjectOfType<MapGenerationController>(true);
            evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase="EDITOR_STAIR_NATIVE_DIAGNOSTIC",slotRoot=TowerRunRuntimeEnvironment.SlotRoot};
            if(seed==8675309)autoDefense=true; // Match the original Windows full sequence's Battle input.
            BindInput();BindPlayer();
        }
        string nativeOutput=Path.Combine(output,"NativeMotion",seed+"-"+Context.ActiveGenerationId);Directory.CreateDirectory(nativeOutput);
        var motion=body.GetComponent<EcologyMotionBody>();var binding=body.GetComponent<EcologyCombatBinding>();
        var rows=new List<string> {"step,physicsTime,phase,kind,bodyX,bodyY,targetX,targetY,ready,live,lease,bindingGeneration,barrierStep,controlLocked,motionFault,terrainClear,mask,skin,colliderId,colliderName,layer,distance,normalX,normalY,separation"};
        var blocked=new Dictionary<string,int>();Vector2 selected=body.position;
        int actualMask=body.GetComponent<PlayerHurtController>().MovementBlockingLayers;float actualSkin=body.GetComponent<PlayerHurtController>().MovementCollisionSkin;
        var colliders=FindObjectsOfType<Collider2D>(true).ToDictionary(c=>c.GetInstanceID());
        void SaveColliderTable(string name)
        {
            var table=new List<string> {"id,name,layer,bodyType,x,y,enabled,trigger"};
            foreach(var c in FindObjectsOfType<Collider2D>(true))table.Add(string.Join(",",c.GetInstanceID(),c.name.Replace(",","_"),c.gameObject.layer,c.attachedRigidbody==null?"StaticNoBody":c.attachedRigidbody.bodyType.ToString(),F(c.bounds.center.x),F(c.bounds.center.y),c.enabled,c.isTrigger));
            File.WriteAllLines(Path.Combine(nativeOutput,name),table);
        }
        SaveColliderTable("native-colliders-before.csv");
        bool recordedResidentDiagnostic=seed==8675309&&(editorSetup||recordedFullNavigationDiagnostic||phase=="full");
        void Record(EcologyMotionTrace item)
        {
            if(rows.Count>=100000)return;
            if(rows.Count%1000==0)File.WriteAllLines(Path.Combine(nativeOutput,"native-motion-diagnostic-progress.csv"),rows);
            if(item.Phase=="Selected")selected=item.Position;
            bool clear=Context.ActiveSpatialGeometry.IsTranslationClear(foot,body.position,selected,0);
            rows.Add(string.Join(",",item.Step,F(Time.fixedTime),item.Phase,item.Kind,F(body.position.x),F(body.position.y),F(item.Position.x),F(item.Position.y),motion.IsReady,binding.IsLive,binding.Lease,binding.GenerationId,binding.Barrier?.CurrentStep??-1,action.IsControlLocked,binding.MotionFaultRevision,clear,actualMask,F(actualSkin),item.ContactColliderId,colliders.TryGetValue(item.ContactColliderId,out var contact)?contact.name.Replace(",","_"):"",contact==null?-1:contact.gameObject.layer,0,F(item.ContactNormal.x),F(item.ContactNormal.y),F(item.ContactSeparation)));
            if(item.Phase!="CastAllowed"||(item.Position-body.position).sqrMagnitude>.000001f||(selected-body.position).sqrMagnitude<.0001f)return;
            var filter=new ContactFilter2D();filter.SetLayerMask(body.GetComponent<PlayerHurtController>().MovementBlockingLayers);filter.useTriggers=false;
            var hits=new RaycastHit2D[256];Vector2 delta=selected-body.position;
            int count=foot.Cast(delta.normalized,filter,hits,delta.magnitude+body.GetComponent<PlayerHurtController>().MovementCollisionSkin,true);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(hit.collider==null)continue;
                string name=hit.collider.name.Replace(",","_");blocked[name]=blocked.TryGetValue(name,out int n)?n+1:1;
                rows.Add(string.Join(",",item.Step,F(Time.fixedTime),"INDEPENDENT_NATIVE_CAST",item.Kind,F(body.position.x),F(body.position.y),F(selected.x),F(selected.y),motion.IsReady,binding.IsLive,binding.Lease,binding.GenerationId,binding.Barrier?.CurrentStep??-1,action.IsControlLocked,binding.MotionFaultRevision,clear,actualMask,F(actualSkin),hit.collider.GetInstanceID(),name,hit.collider.gameObject.layer,F(hit.distance),F(hit.normal.x),F(hit.normal.y),0));
            }
        }
        IEnumerator PollState()
        {
            while(true)
            {
                yield return new WaitForFixedUpdate();
                ScanResidentGroupObservation("PostFixedPassiveScopeScan");
                if(rows.Count<100000)rows.Add(string.Join(",",binding.Barrier?.CurrentStep??-1,F(Time.fixedTime),"FIXED_STATE",trial,F(body.position.x),F(body.position.y),F(action.MoveInput.x),F(action.MoveInput.y),motion.IsReady,binding.IsLive,binding.Lease,binding.GenerationId,binding.Barrier?.CurrentStep??-1,action.IsControlLocked,binding.MotionFaultRevision,"",actualMask,F(actualSkin),0,"",0,0,0,0,0));
            }
        }
        Coroutine poll=null;bool playerTraceAttached=false;
        var stack=new Stack<IEnumerator>();stack.Push(WalkAllStairs(seed,capture));Exception failure=null;
        var secondary=new List<string>();
        void Retain(Exception error,string stage)
        {if(failure==null)failure=error;else secondary.Add(stage+": "+error);}
        void Attempt(Action operation,string stage)
        {try {operation();}catch(Exception error) {Retain(error,stage);}}
        try
        {
            // Begin ownership before either original trace subscription can be installed.
            try
            {
                if(recordedResidentDiagnostic)
                {
                    var resident=FindObjectsOfType<EcologyResidentBinding>().Single(a=>a.GenerationId==Context.ActiveGenerationId&&a.ResidentId=="ecology.resident.1000.001");
                    bufferedConsumerLifecycle=new List<string>();bufferedConsumerNative=new List<string>();bufferedFlushAttempted=false;
                    BeginResidentGroupObservation(seed);ExportMap(seed);BindDiagnosticConsumer(resident);
                }
                motion.OnTrace+=Record;playerTraceAttached=true;poll=StartCoroutine(PollState());
            }
            catch(Exception error) {Retain(error,"diagnostic setup");}
            while(failure==null&&stack.Count>0)
            {
                bool moved=false;object current=null;
                try {ScanResidentGroupObservation("BeforeOriginalIteratorPassiveScopeScan");moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error) {Retain(error,"original walk");break;}
                if(!moved) {var ended=stack.Pop();Attempt(()=>{(ended as IDisposable)?.Dispose();},"completed iterator");continue;}
                if(current is IEnumerator nested)stack.Push(nested);else {diagnosticLastYield=current?.GetType().FullName??"NextFrame";yield return current;}
            }
        }
        finally
        {
            if(diagnosticHeartbeatFailure!=null)Retain(diagnosticHeartbeatFailure,"passive heartbeat");
            // Every owner gets a cleanup attempt even when an earlier Dispose or write fails.
            while(stack.Count>0) {var pending=stack.Pop();Attempt(()=>{(pending as IDisposable)?.Dispose();},"pending iterator");}
            Attempt(()=>{if(poll!=null)StopCoroutine(poll);},"stop poll");
            Attempt(()=>{if(playerTraceAttached)motion.OnTrace-=Record;},"detach player trace");
            if(recordedResidentDiagnostic)
            {
                Attempt(StopResidentGroupObservation,"detach all resident traces");
                Attempt(FlushResidentGroupObservation,"flush resident group rings");
                Attempt(StopConsumerLifecycleObservation,"detach resident trace");
                Attempt(FlushBufferedConsumerObservation,"flush resident batches");
            }
            Attempt(()=>SaveColliderTable("native-colliders-after.csv"),"collider table");
            Attempt(()=>File.WriteAllLines(Path.Combine(nativeOutput,"native-motion-diagnostic.csv"),rows),"native player rows");
            Attempt(()=>evidence.checks.Add("READ_ONLY_NATIVE_DIAGNOSTIC rows="+rows.Count+" blocked="+string.Join(";",blocked.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value))),"row summary");
            Attempt(()=>evidence.checks.Add("Final readonly state ready="+motion.IsReady+" live="+binding.IsLive+" lease="+binding.Lease+" bindingGeneration="+binding.GenerationId+" step="+binding.Barrier?.CurrentStep+" locked="+action.IsControlLocked+" fault="+binding.MotionFaultRevision),"state summary");
            if(editorSetup)Attempt(()=>{evidence.status="DIAGNOSTIC_CAPTURE_ONLY_SEE_XML_FOR_FUNCTIONAL_RESULT";},"status");
            if(failure!=null)evidence.checks.Add("RECORDED_FIRST_FAILURE: "+failure);
            foreach(string detail in secondary)evidence.checks.Add("SECONDARY_DIAGNOSTIC_CLEANUP_FAILURE: "+detail);
            Attempt(Persist,"persist diagnostic evidence");
            if(editorSetup)Attempt(CleanupInput,"restore original input settings");
            foreach(string detail in secondary)Debug.LogWarning("Secondary diagnostic cleanup: "+detail);
        }
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
