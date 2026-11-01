using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;

// Read-only evidence for the original consumer trial. No health, scope, AI or lease writes.
public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class ConsumerLifecycleSnapshot
    {
        public string label,source,phase,sessionState,contextPhase,generation,residentId,instance,actorState,navigationFailure,buildId,diagnosticError,actorGeneration;
        public double time;public float physicsTime,playerHealth,actorHealth;
        public bool contextReady,contextFailed,registryRetired,playerDefeated,playerLive,playerScopeOpen,actorActive,actorEnabled,actorRetired,actorScopeFaulted,actorDefeated,actorLive,actorScopeOpen,conditionProvided,condition;
        public ConsumerMovementPreflight movementPreflight;public bool movementPreflightProvided,nativeHomeObservationProvided;
        public NativeHomeObservation nativeHomeObservation;
        public long playerLease,playerFault,actorLease,actorFault;
        public Vector2 playerPosition,chaseProgressPosition;public string originalTarget,originalVisibleTarget;
        public bool originalTargetConfirmed,originalTargetVisible;public double chaseRetryAt;public int frame;
        public bool fullHome,homeMeasured,excursionFault,returnLatched,hasPermit;public int homeSteps,homeRegion;public Vector2 actorPosition,actorTransformPosition,actorRigidbodyPosition,nativePosition,homeWaypoint,footMin,footMax;public bool nativePoseProvided;public long nativeStep;
    }
    [Serializable] private sealed class NativeHomeObservation
    {
        public long submittedStep,observedStep;
        public bool contactDuringStep,certifiedStraight,exitObserved,witnessFullHome,witnessMeasured;
        public Vector2 start,submittedTarget,witness;
        public float exitFraction;public int witnessDistance;
        public string scope="PURE_HOME_QUERY_REPLAY_NOT_A_SUBMISSION_OR_CONFIRMATION_OF_PRODUCER_BRANCH";
    }
    private NativeHomeObservation InspectNativeHomeObservation(EcologyResidentBinding actor,Collider2D foot,Vector2 end,long step)
    {
        var motion=actor.GetComponent<EcologyMotionBody>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object Read(string name)=>typeof(EcologyMotionBody).GetField(name,flags).GetValue(motion);
        var row=new NativeHomeObservation {submittedStep=(long)Read("submittedStep"),observedStep=(long)Read("observedStep"),contactDuringStep=(bool)Read("contactDuringStep"),submittedTarget=(Vector2)Read("submittedTarget"),witnessDistance=-1};
        row.start=row.submittedStep==step?(Vector2)Read("submittedStart"):(Vector2)Read("previousPosition");
        var rigidbody=foot.attachedRigidbody;
        row.certifiedStraight=row.submittedStep==step?(end-row.submittedTarget).sqrMagnitude<.00000001f&&Mathf.Abs(Mathf.DeltaAngle(rigidbody.rotation,(float)Read("submittedRotation")))<.0001f&&!row.contactDuringStep:(end-row.start).sqrMagnitude<.00000001f&&!row.contactDuringStep;
        row.exitObserved=actor.Home.TryObservedHomeExit(foot,row.start,end,out row.exitFraction,out row.witness);
        if(!row.exitObserved)row.exitFraction=-1f; // No witness; keep JSON finite without inventing an exit fraction.
        if(row.exitObserved)
        {
            row.witnessFullHome=actor.Home.ContainsFullFoot(foot,row.witness);
            row.witnessMeasured=actor.Home.TryMeasureHomeDistance(foot,row.witness,out row.witnessDistance,out _);
        }
        return row;
    }
    [Serializable] private sealed class ConsumerCastHit
    {public int id,layer,bodyId;public string name,resident,generation;public float distance;public Vector2 normal;public bool separatingOverlap;}
    [Serializable] private sealed class ConsumerMovementPreflight
    {
        public bool prospectivePursuit,enabled,hasDestination,locked,waypointAvailable,pursuitMovement,canAct,needsCompleteHome,wouldStopAtTolerance,castReadbackPerformed,motionReady,inFixedStep,bodySimulated;
        public float speed,stoppingDistance,skin,waypointDistance,requestedDistance,originalAllowed,independentBodyAllowed,independentFootAllowed;
        public int mask,bodyConstraints,bodyHitCount,footHitCount;public long barrierStep,intentStep,submittedStep;
        public string navigationStatus,bodyType,observedHomeGeneration,observedHomeProjectionIdentity,scope="READONLY_PRECAST_REPLAY_NOT_A_MOVEMENT_SUBMISSION";
        public Vector2 destination,evaluatedDestination,nativePosition,waypoint,direction;
        public ConsumerCastHit[] bodyHits,footHits;
    }
    private static ConsumerMovementPreflight InspectOriginalMovementPreflight(EcologyResidentBinding actor)
        => InspectMovementPreflight(actor,false,default);
    private static ConsumerMovementPreflight InspectProspectivePursuitStep(EcologyResidentBinding actor,Vector2 target)
        => InspectMovementPreflight(actor,true,target);
    private static ConsumerMovementPreflight InspectMovementPreflight(EcologyResidentBinding actor,bool prospectivePursuit,Vector2 target)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var mover=actor.GetComponent<MonsterMovementController>();var nativeBody=actor.GetComponent<Rigidbody2D>();
        var foot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",flags).GetValue(actor);
        var motion=actor.GetComponent<EcologyMotionBody>();var combat=actor.GetComponent<EcologyCombatBinding>();
        var home=actor.Home;var excursion=actor.Excursion;
        // Mirror the original predicate without its exception-to-ScopeFaulted write.
        bool CanActWithoutMutation() => actor.IsBound&&actor.isActiveAndEnabled&&
            !(bool)typeof(EcologyResidentBinding).GetField("retired",flags).GetValue(actor)&&!actor.ScopeFaulted&&
            ((Func<bool>)typeof(EcologyResidentBinding).GetField("currentScope",flags).GetValue(actor))()&&
            ((Func<bool>)typeof(EcologyResidentBinding).GetField("gameplayReady",flags).GetValue(actor))();
        var row=new ConsumerMovementPreflight {enabled=mover.isActiveAndEnabled,locked=mover.IsMovementLocked,navigationStatus=mover.NavigationStatus.ToString(),speed=mover.MoveSpeed,
            stoppingDistance=(float)typeof(MonsterMovementController).GetField("stoppingDistance",flags).GetValue(mover),skin=mover.MovementCollisionSkin,mask=mover.MovementBlockingLayers,
            hasDestination=(bool)typeof(MonsterMovementController).GetField("hasDestination",flags).GetValue(mover),destination=(Vector2)typeof(MonsterMovementController).GetField("destination",flags).GetValue(mover),
            pursuitMovement=(bool)typeof(EcologyResidentBinding).GetField("pursuitMovement",flags).GetValue(actor),canAct=CanActWithoutMutation(),motionReady=motion.IsReady,inFixedStep=Time.inFixedTimeStep,
            bodySimulated=nativeBody.simulated,bodyConstraints=(int)nativeBody.constraints,bodyType=nativeBody.bodyType.ToString(),barrierStep=combat.Barrier==null?-1:combat.Barrier.CurrentStep,
            intentStep=(long)typeof(EcologyMotionBody).GetField("intentStep",flags).GetValue(motion),submittedStep=(long)typeof(EcologyMotionBody).GetField("submittedStep",flags).GetValue(motion)};
        row.prospectivePursuit=prospectivePursuit;row.evaluatedDestination=prospectivePursuit?target:row.destination;row.nativePosition=nativeBody.position;
        if(prospectivePursuit)row.scope="PROSPECTIVE_PURSUIT_NATIVE_FIRST_STEP_REPLAY_NOT_AN_ACTUAL_SUBMISSION_OR_FUTURE_PATH";
        row.observedHomeGeneration=home==null?null:home.HomeGraph.GenerationId.ToString();
        row.observedHomeProjectionIdentity=home==null?null:home.HomeGraph.ProjectionIdentity;
        row.waypoint=nativeBody.position;
        // A diagnostic must not synchronize/rebind residents or clear their fault fields.
        // Query the already-bound navigation only; this replay may precede the mover's own sync.
        if(row.canAct&&home!=null&&foot!=null&&excursion!=null)
        {
            if(prospectivePursuit)
            {if(excursion.MayChase)row.waypointAvailable=home.TryPursuitWaypoint(foot,nativeBody.position,target,excursion.MaximumHomeSteps,out row.waypoint);}
            else if(excursion.ReturnLatched)row.waypointAvailable=home.TryMeasureHomeDistance(foot,nativeBody.position,out _,out row.waypoint);
            else if(row.pursuitMovement)row.waypointAvailable=home.TryPursuitWaypoint(foot,nativeBody.position,row.destination,excursion.MaximumHomeSteps,out row.waypoint);
            else if(home.TryHomeNode(row.destination,out int patrolNode))row.waypointAvailable=home.TryPatrolWaypoint(foot,nativeBody.position,patrolNode,out row.waypoint);
        }
        row.waypointDistance=Vector2.Distance(nativeBody.position,row.waypoint);
        row.needsCompleteHome=excursion!=null&&excursion.ReturnLatched&&home!=null&&home.TryMeasureHomeDistance(foot,nativeBody.position,out int homeDistance,out _)&&homeDistance>0;
        row.wouldStopAtTolerance=row.waypointAvailable&&row.waypointDistance<=row.stoppingDistance&&!row.needsCompleteHome;
        if(row.waypointAvailable&&row.waypointDistance>.000001f)
        {
            row.direction=(row.waypoint-nativeBody.position)/row.waypointDistance;row.requestedDistance=Mathf.Min(row.speed*Time.fixedDeltaTime,row.waypointDistance);
            if(row.requestedDistance>0f)
            {
                row.castReadbackPerformed=true;
                row.originalAllowed=(float)typeof(MonsterMovementController).GetMethod("GetAllowedMoveDistance",flags).Invoke(mover,new object[]{row.direction,row.requestedDistance});
                var filter=(ContactFilter2D)typeof(MonsterMovementController).GetField("movementContactFilter",flags).GetValue(mover);
                var bodyHits=new RaycastHit2D[8];var footHits=new RaycastHit2D[8];float distance=row.requestedDistance+row.skin;
                row.bodyHitCount=nativeBody.Cast(row.direction,filter,bodyHits,distance);row.footHitCount=foot.Cast(row.direction,filter,footHits,distance,true);
                float Allowed(RaycastHit2D[] hits,int count,out ConsumerCastHit[] recorded)
                {
                    float allowed=row.requestedDistance;var records=new System.Collections.Generic.List<ConsumerCastHit>();
                    for(int index=0;index<count;index++)
                    {
                        var hit=hits[index];if(hit.collider==null)continue;
                        bool separating=hit.distance<=0f&&Vector2.Dot(hit.normal,row.direction)>0f;
                        var owner=hit.collider.GetComponentInParent<EcologyResidentBinding>();
                        records.Add(new ConsumerCastHit {id=hit.collider.GetInstanceID(),name=hit.collider.name,layer=hit.collider.gameObject.layer,bodyId=hit.collider.attachedRigidbody==null?0:hit.collider.attachedRigidbody.GetInstanceID(),resident=owner==null?null:owner.ResidentId,generation=owner==null?null:owner.GenerationId.ToString(),distance=hit.distance,normal=hit.normal,separatingOverlap=separating});
                        if(!separating)allowed=Mathf.Min(allowed,Mathf.Max(0f,hit.distance-row.skin));
                    }
                    recorded=records.ToArray();return allowed;
                }
                row.independentBodyAllowed=Allowed(bodyHits,row.bodyHitCount,out row.bodyHits);row.independentFootAllowed=Allowed(footHits,row.footHitCount,out row.footHits);
            }
        }
        return row;
    }
    private System.Collections.Generic.List<string> bufferedConsumerLifecycle,bufferedConsumerNative;
    private bool bufferedFlushAttempted;
    private Exception consumerNativeTraceFailure;
    private EcologyResidentBinding diagnosticConsumer;
    private TowerRunCoordinator diagnosticSession;
    private PlayerDamageController diagnosticPlayer;
    private EcologyMotionBody diagnosticMotion;
    private Action<EcologyMotionTrace> diagnosticMotionTrace;
    private Action diagnosticSessionChanged;
    private Action<PlayerDamageController,DamageInfo> diagnosticPlayerDamaged;
    private Action<PlayerDamageController> diagnosticPlayerDefeated;
    private void RecordConsumerLifecycle(string label,string source="",bool? condition=null,Vector2? nativePosition=null,long nativeStep=-1)
    {
        if(string.IsNullOrEmpty(output))return;
        var row=new ConsumerLifecycleSnapshot {label=label,source=source,phase=trial,time=Time.timeAsDouble,physicsTime=Time.fixedTime,buildId=MapHostBuildStamp.BuildId,conditionProvided=condition.HasValue,condition=condition??false,nativePoseProvided=nativePosition.HasValue,nativePosition=nativePosition??Vector2.zero,nativeStep=nativeStep};
        try
        {
            row.sessionState=session.State.ToString();row.contextPhase=Context.Phase.ToString();row.generation=Context.ActiveGenerationId.ToString();
            row.contextReady=Context.IsReady;row.contextFailed=Context.IsFailed;row.registryRetired=Context.ActiveRegistry==null||Context.ActiveRegistry.IsRetired;
            var player=body==null?null:body.GetComponent<PlayerDamageController>();var playerBinding=body==null?null:body.GetComponent<EcologyCombatBinding>();
            row.frame=Time.frameCount;row.playerPosition=body==null?Vector2.zero:body.position;
            row.playerHealth=player==null?-1f:player.CurrentHealth;row.playerDefeated=player!=null&&player.IsDefeated;
            row.playerLive=playerBinding!=null&&playerBinding.IsLive;row.playerLease=playerBinding==null?-1:playerBinding.Lease;row.playerFault=playerBinding==null?-1:playerBinding.MotionFaultRevision;row.playerScopeOpen=playerBinding!=null&&playerBinding.Barrier!=null&&playerBinding.Barrier.IsScopeOpen;
            var actor=diagnosticConsumer;
            if(actor!=null)
            {
                var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
                if(bufferedConsumerLifecycle==null&&actor.gameObject.activeInHierarchy&&nativeFoot.enabled&&nativeFoot.attachedRigidbody.simulated) {row.movementPreflight=InspectOriginalMovementPreflight(actor);row.movementPreflightProvided=true;}
                row.actorGeneration=actor.GenerationId.ToString();row.actorTransformPosition=actor.transform.position;row.actorRigidbodyPosition=nativeFoot.attachedRigidbody.position;row.actorPosition=nativePosition??row.actorRigidbodyPosition;row.homeRegion=actor.HomeRegionId;row.footMin=nativeFoot.bounds.min;row.footMax=nativeFoot.bounds.max;
                row.fullHome=actor.Home.ContainsFullFoot(nativeFoot,row.actorPosition);row.homeMeasured=actor.Home.TryMeasureHomeDistance(nativeFoot,row.actorPosition,out row.homeSteps,out row.homeWaypoint);
                if(nativePosition.HasValue&&bufferedConsumerLifecycle!=null) {row.nativeHomeObservation=InspectNativeHomeObservation(actor,nativeFoot,nativePosition.Value,nativeStep);row.nativeHomeObservationProvided=true;}
                row.excursionFault=actor.Excursion.NavigationFault;row.returnLatched=actor.Excursion.ReturnLatched;row.hasPermit=actor.Excursion.HasPermit;
                row.residentId=actor.ResidentId;row.instance=actor.name;row.actorActive=actor.gameObject.activeInHierarchy;row.actorEnabled=actor.enabled;
                row.actorRetired=(bool)typeof(EcologyResidentBinding).GetField("retired",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
                row.actorScopeFaulted=actor.ScopeFaulted;row.navigationFailure=actor.NavigationFailure;
                var ai=actor.GetComponent<MonsterAIController>();row.actorState=ai?.CurrentStateType.ToString();
                if(ai!=null)
                {
                    row.originalTarget=ai.CurrentTarget==null?null:ai.CurrentTarget.name;row.originalTargetConfirmed=ai.IsTargetConfirmed;row.originalTargetVisible=ai.IsCurrentTargetVisible;
                    var perception=actor.GetComponent<MonsterPerceptionController>();row.originalVisibleTarget=perception.CurrentVisibleTarget==null?null:perception.CurrentVisibleTarget.name;
                    row.chaseRetryAt=(double)typeof(MonsterAIController).GetField("chaseRetryAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ai);
                    row.chaseProgressPosition=(Vector2)typeof(MonsterAIController).GetField("chaseProgressPosition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ai);
                }
                var damage=actor.GetComponent<BaseDamageable>();var combat=actor.GetComponent<EcologyCombatBinding>();row.actorHealth=damage==null?-1f:damage.CurrentHealth;row.actorDefeated=damage!=null&&damage.IsDefeated;
                row.actorLive=combat!=null&&combat.IsLive;row.actorLease=combat==null?-1:combat.Lease;row.actorFault=combat==null?-1:combat.MotionFaultRevision;row.actorScopeOpen=combat!=null&&combat.Barrier!=null&&combat.Barrier.IsScopeOpen;
            }
        }
        catch(Exception error) {row.diagnosticError=error.ToString();}
        try {string serialized=JsonUtility.ToJson(row);if(bufferedConsumerLifecycle!=null)bufferedConsumerLifecycle.Add(serialized);else File.AppendAllText(Path.Combine(output,"consumer-lifecycle.jsonl"),serialized+Environment.NewLine);}
        catch(Exception error) {evidence.checks.Add("CONSUMER_DIAGNOSTIC_WRITE_FAILED:"+error.GetType().Name);}
    }
    private void BindDiagnosticConsumer(EcologyResidentBinding actor)
    {
        diagnosticConsumer=actor;diagnosticMotion=actor.GetComponent<EcologyMotionBody>();
        consumerNativeTraceFailure=null;
        var colliderNames=FindObjectsOfType<Collider2D>(true).ToDictionary(c=>c.GetInstanceID());
        string nativePath=Path.Combine(output,"consumer-native-motion.csv");
        if(!File.Exists(nativePath))File.WriteAllText(nativePath,"step,physicsTime,frame,trial,stage,kind,tracePositionX,tracePositionY,bodyX,bodyY,contactId,contactName,contactLayer,normalX,normalY,separation,actorGeneration,contextGeneration,buildId"+Environment.NewLine);
        diagnosticMotionTrace=item=>
        {
            try
            {
                colliderNames.TryGetValue(item.ContactColliderId,out var contact);
                Vector2 native=actor.GetComponent<Rigidbody2D>().position;
                string nativeRow=string.Join(",",item.Step,F(Time.fixedTime),Time.frameCount,trial,item.Phase,item.Kind,F(item.Position.x),F(item.Position.y),F(native.x),F(native.y),item.ContactColliderId,contact==null?"":contact.name.Replace(",","_"),contact==null?-1:contact.gameObject.layer,F(item.ContactNormal.x),F(item.ContactNormal.y),F(item.ContactSeparation),actor.GenerationId,Context.ActiveGenerationId,MapHostBuildStamp.BuildId);
                if(bufferedConsumerNative!=null)bufferedConsumerNative.Add(nativeRow);else File.AppendAllText(nativePath,nativeRow+Environment.NewLine);
            }
            catch(Exception error) {if(consumerNativeTraceFailure==null)consumerNativeTraceFailure=error;}
            if(item.Phase=="PhysicsObserved"||item.Phase.Contains("Fault"))RecordConsumerLifecycle("OriginalNative-"+item.Phase+"-step-"+item.Step,nativePosition:item.Position,nativeStep:item.Step);
        };
        diagnosticMotion.OnTrace+=diagnosticMotionTrace;
        var region=Context.ActivePlacementPlan.Ecology.Regions.Single(r=>r.Id==actor.HomeRegionId);
        File.WriteAllText(Path.Combine(output,"consumer-home-surface.txt"),"generation="+Context.ActiveGenerationId+"\nresident="+actor.ResidentId+"\nregion="+region.Id+"\nsurfaceNodes="+string.Join(";",region.Nodes)+"\nhomeNodes="+string.Join(";",actor.Home.HomeNodes)+"\ncentres="+string.Join(";",region.Nodes.Select(n=>actor.Home.NodeWorldPosition(n).ToString("R")))+"\n");
        RecordConsumerLifecycle("PursuitCandidateSelected");
    }
    private void RequireConsumerActor(EcologyResidentBinding actor,string reason)
    {
        bool valid=actor.CanAct;
        if(!valid) {diagnosticConsumer=actor;RecordConsumerLifecycle(reason,condition:false);}
        Require(valid,reason);
    }
    private IEnumerator RunConsumerChecks()
    {
        diagnosticConsumer=null;diagnosticSession=session;diagnosticPlayer=body.GetComponent<PlayerDamageController>();
        diagnosticSessionChanged=()=>RecordConsumerLifecycle("OriginalSessionStateChanged");
        diagnosticPlayerDamaged=(player,damage)=>RecordConsumerLifecycle("OriginalPlayerDamaged",damage.SourceTransform==null?"NoSource":damage.SourceTransform.name);
        diagnosticPlayerDefeated=player=>RecordConsumerLifecycle("OriginalPlayerDefeated");
        diagnosticSession.StateChanged+=diagnosticSessionChanged;diagnosticPlayer.OnDamaged+=diagnosticPlayerDamaged;diagnosticPlayer.OnDefeated+=diagnosticPlayerDefeated;
        try {RecordConsumerLifecycle("ConsumerFixtureBegin");yield return RunConsumerChecksCore();}
        finally {try {RecordConsumerLifecycle("ConsumerFixtureEnd");}finally {StopConsumerLifecycleObservation();}}
    }
    private void FlushBufferedConsumerBatch(string name,ref System.Collections.Generic.List<string> batch,System.Collections.Generic.List<Exception> errors)
    {
        if(batch==null)return;
        try {File.AppendAllLines(Path.Combine(output,name),batch);batch=null;}
        catch(Exception original)
        {
            errors.Add(original);
            // A thrown append can already be partial. Preserve the complete unconfirmed
            // batch separately; never append it again and silently duplicate evidence.
            try
            {
                string pending=Path.Combine(output,name+".unconfirmed-batch");
                using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false)))
                    foreach(string line in batch)writer.WriteLine(line);
                if(evidence!=null)evidence.checks.Add("UNCONFIRMED_DIAGNOSTIC_BATCH: "+name+"; original append may be partial; full batch retained; no retry.");
            }
            catch(Exception secondary) {errors.Add(secondary);}
        }
    }
    private void FlushBufferedConsumerObservation()
    {
        if(bufferedConsumerLifecycle==null&&bufferedConsumerNative==null)return;
        if(bufferedFlushAttempted)throw new InvalidOperationException("Unconfirmed diagnostic append must not be retried.");
        bufferedFlushAttempted=true;
        var errors=new System.Collections.Generic.List<Exception>();
        FlushBufferedConsumerBatch("consumer-lifecycle.jsonl",ref bufferedConsumerLifecycle,errors);
        FlushBufferedConsumerBatch("consumer-native-motion.csv",ref bufferedConsumerNative,errors);
        if(errors.Count>0)throw new AggregateException("Diagnostic batch write failed; unconfirmed original batches retained when possible.",errors);
    }
    private void StopConsumerLifecycleObservation()
    {
        if(diagnosticMotion!=null&&diagnosticMotionTrace!=null)diagnosticMotion.OnTrace-=diagnosticMotionTrace;diagnosticMotion=null;diagnosticMotionTrace=null;
        if(diagnosticSession!=null&&diagnosticSessionChanged!=null)diagnosticSession.StateChanged-=diagnosticSessionChanged;
        if(diagnosticPlayer!=null) {if(diagnosticPlayerDamaged!=null)diagnosticPlayer.OnDamaged-=diagnosticPlayerDamaged;if(diagnosticPlayerDefeated!=null)diagnosticPlayer.OnDefeated-=diagnosticPlayerDefeated;}
        diagnosticSession=null;diagnosticPlayer=null;diagnosticSessionChanged=null;diagnosticPlayerDamaged=null;diagnosticPlayerDefeated=null;diagnosticConsumer=null;
    }
}
