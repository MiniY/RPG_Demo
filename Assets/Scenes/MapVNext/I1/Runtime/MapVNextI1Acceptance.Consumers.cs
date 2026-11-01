using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using MapVNext;

public sealed partial class MapVNextI1Acceptance
{
    // A separate predeclared positive input; old21193 crowd failures stay failures.
    public const int ConsumerFixtureSeed=7727;
    private readonly List<string> consumerTrace=new List<string> {"phase,physicsTime,id,role,state,x,y,generation,buildId"};
    private readonly Dictionary<int,Vector2> previousGround=new Dictionary<int,Vector2>();
    private static GameObject Actor(UnityEngine.Object instance)=>(instance as Component)?.gameObject??instance as GameObject;
    private GameObject Portal=>FindObjectOfType<TowerFloorRuntimeController>(true).transform.Find("TowerFloorExitPortal").gameObject;
    private void AuditGround(GameObject actor,string id,string role)
    {
        if(actor==null||!actor.activeInHierarchy)return;
        var colliders=actor.GetComponentsInChildren<Collider2D>(true).Where(c=>c.enabled&&!c.isTrigger).ToArray();
        if(colliders.Length==0)colliders=actor.GetComponentsInChildren<Collider2D>(true).Where(c=>c.enabled&&c.isTrigger&&!(c is CapsuleCollider2D)).ToArray();
        foreach(var collider in colliders)
        {
            if(collider is PolygonCollider2D)
            {
                AuditNativePolygon(collider,id);continue;
            }
            var footprint=MapVNextGroundFootprint.FromCollider(collider,out Vector2 offset);
            Transform anchor=collider.attachedRigidbody==null?collider.transform:collider.attachedRigidbody.transform;
            Vector2 position=(Vector2)anchor.position+offset;previousGround.TryGetValue(collider.GetInstanceID(),out Vector2 previous);
            if(!previousGround.ContainsKey(collider.GetInstanceID()))previous=position;
            Require(!MapVNextSurfaceQuery.SweepIntrudesRock(rock,footprint,previous,position),"RealConsumerEnteredRock:"+id);
            previousGround[collider.GetInstanceID()]=position;
        }
        if(colliders.Length==0&&actor.TryGetComponent<MapGroundFootprint>(out var declared))
            Require(Context.ActiveSpatialGeometry.IsWorldGroundPolygonClear(declared.Vertices.Select(p=>(Vector2)actor.transform.TransformPoint(p)).ToArray()),"DeclaredConsumerEnteredRock:"+id);
        consumerTrace.Add(string.Join(",",trial,F(Time.fixedTime),id,role,actor.GetComponent<MonsterAIController>()?.CurrentStateType.ToString()??"NoAI",F(actor.transform.position.x),F(actor.transform.position.y),Context.ActiveGenerationId,MapHostBuildStamp.BuildId));
    }
    private string consumerTraceWrittenPath;
    private int consumerTraceWrittenCount,consumerTraceWriteCalls;
    private long consumerTraceWrittenLength;
    private void FlushConsumerTrace()
    {
        if(string.IsNullOrEmpty(output))return;
        string path=Path.Combine(output,"real-consumers.csv"),audit=Path.Combine(output,"consumer-trace-io.csv");
        bool initial=consumerTraceWrittenPath!=path;
        if(!initial&&consumerTrace.Count<consumerTraceWrittenCount)throw new IOException("ConsumerTraceHistoryShrank");
        if(!initial&&(!File.Exists(path)||new FileInfo(path).Length!=consumerTraceWrittenLength))throw new IOException("ConsumerTraceFileChangedOutsideWriter");
        if(!initial&&consumerTrace.Count==consumerTraceWrittenCount)return;
        int added=initial?consumerTrace.Count:consumerTrace.Count-consumerTraceWrittenCount;
        long before=initial?0:consumerTraceWrittenLength,started=System.Diagnostics.Stopwatch.GetTimestamp();
        // Rewriting every accumulated row each physics tick made observation IO grow
        // quadratically. Preserve the complete history while writing each new row once.
        if(initial)File.WriteAllLines(path,consumerTrace);else File.AppendAllLines(path,consumerTrace.Skip(consumerTraceWrittenCount));
        long after=new FileInfo(path).Length,elapsed=System.Diagnostics.Stopwatch.GetTimestamp()-started;
        consumerTraceWrittenPath=path;consumerTraceWrittenCount=consumerTrace.Count;consumerTraceWrittenLength=after;
        if(initial)consumerTraceWriteCalls=0;consumerTraceWriteCalls++;
        string row=string.Join(",",consumerTraceWriteCalls,initial?"INITIAL_HISTORY":"APPEND_NEW_ROWS",added,consumerTraceWrittenCount,before,after,initial?after:after-before,elapsed,System.Diagnostics.Stopwatch.Frequency,F(Time.fixedTime),Time.frameCount,MapHostBuildStamp.BuildId)+Environment.NewLine;
        if(initial)File.WriteAllText(audit,"write,mode,newRows,totalRows,bytesBefore,bytesAfter,bytesWritten,traceWriteTicks,stopwatchFrequency,physicsTime,frame,buildId"+Environment.NewLine+row);
        else File.AppendAllText(audit,row);
    }
    private void AuditNativePolygon(Collider2D collider,string id)
    {
        // Unity's mesh is the native collider area, including concave paths; triangles
        // are convex audit pieces, never replacements for the real collider.
        bool attached=collider.attachedRigidbody!=null;Mesh mesh=collider.CreateMesh(attached,attached);Require(mesh!=null,"NativeGroundMeshUnavailable:"+id);
        try
        {
            Vector2 position=collider.attachedRigidbody!=null?collider.attachedRigidbody.position:(Vector2)collider.transform.position;
            int key=collider.GetInstanceID();Vector2 previous=previousGround.TryGetValue(key,out Vector2 old)?old:position;
            var vertices=mesh.vertices;var triangles=mesh.triangles;Require(triangles.Length>=3,"NativeGroundMeshEmpty:"+id);
            Bounds bounds=mesh.bounds;Bounds actual=collider.bounds;
            Require(Mathf.Abs(bounds.min.x-actual.min.x)<.002f&&Mathf.Abs(bounds.max.x-actual.max.x)<.002f&&Mathf.Abs(bounds.min.y-actual.min.y)<.002f&&Mathf.Abs(bounds.max.y-actual.max.y)<.002f,"NativeGroundMeshSpaceMismatch:"+id);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var piece=MapVNextGroundFootprint.Convex(new[]{(Vector2)vertices[triangles[i]]-position,(Vector2)vertices[triangles[i+1]]-position,(Vector2)vertices[triangles[i+2]]-position});
                Require(!MapVNextSurfaceQuery.SweepIntrudesRock(rock,piece,previous,position),"NativePolygonConsumerEnteredRock:"+id);
            }
            previousGround[key]=position;
        }
        finally {Destroy(mesh);}
    }
    private void AuditConsumers()
    {
        if(consumerNativeTraceFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(consumerNativeTraceFailure).Throw();
        foreach(var entry in Context.ActiveRegistry.Entries)AuditGround(Actor(entry.Instance),entry.LogicalObjectId,entry.Role);
        foreach(var reward in FindObjectsOfType<RewardPickup>())AuditGround(reward.gameObject,"reward-"+reward.GetInstanceID(),"ActualDrop");
        AuditGround(Portal,"portal","ExitPortal");
        FlushConsumerTrace();
        foreach(var binding in FindObjectsOfType<EcologyResidentBinding>())
            if(binding.GenerationId==Context.ActiveGenerationId)Require(!binding.ScopeFaulted&&binding.NavigationFailure==null,"RealResidentNavigationFault:"+binding.ResidentId+":"+binding.NavigationFailure);
    }
    private IEnumerator ObserveConsumers(int ticks)
    {
        for(int i=0;i<ticks;i++) {yield return new WaitForFixedUpdate();AuditConsumers();Require(session.State==TowerRunFlowState.Playing,"ConsumerTrialPlayerDefeatedOrSessionFailed");}
    }
    public IEnumerator RunConsumerChecksForTests()
    {
        output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");Directory.CreateDirectory(output);
        session=GameSession.Instance.TowerRun;map=FindObjectOfType<MapGenerationController>(true);
        evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase="EDITOR_REAL_CONSUMERS",slotRoot=TowerRunRuntimeEnvironment.SlotRoot};
        Exception firstFailure=null;var secondary=new List<string>();bool completed=false;var stack=new Stack<IEnumerator>();
        void Retain(Exception error) {if(firstFailure==null)firstFailure=error;else secondary.Add(error.ToString());}
        try
        {
            BindInput();BindPlayer();stack.Push(RunConsumerChecks());
            while(stack.Count>0)
            {
                bool moved=false;object current=null;
                try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error) {Retain(error);break;}
                if(!moved) {try {(stack.Pop() as IDisposable)?.Dispose();}catch(Exception error) {Retain(error);break;}continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            completed=firstFailure==null&&stack.Count==0;
        }
        finally
        {
            while(stack.Count>0) {try {(stack.Pop() as IDisposable)?.Dispose();}catch(Exception error) {Retain(error);}}
            try {CleanupInput();}catch(Exception error) {Retain(error);}
            try {FlushConsumerTrace();}catch(Exception error) {Retain(error);}
            evidence.failure=firstFailure?.ToString();evidence.status=completed&&firstFailure==null?"EDITOR_REAL_CONSUMERS_COMPLETE_HUMAN_PENDING":"INCOMPLETE_SEE_XML_FAILURE";
            try {Persist();}catch(Exception error) {Retain(error);}
            try {File.WriteAllText(Path.Combine(output,"consumer-test-lifecycle.json"),JsonUtility.ToJson(new ConsumerTestLifecycle {completed=completed&&firstFailure==null,firstFailure=firstFailure?.ToString(),secondaryFailures=secondary.ToArray()},true));}catch(Exception error) {Retain(error);}
        }
        if(firstFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
    [Serializable] private sealed class ConsumerTestLifecycle {public bool completed;public string firstFailure;public string[] secondaryFailures;}
    [Serializable] private sealed class PursuitTrialPrediction
    {
        public string buildId,generation,resident,reason,targetCollider;
        public bool predictionOnly=true,qualifies,fullHome,targetPoseCoherent,closestPointQueried,visibilityChecked,projectedVisible,sightBufferSaturated;
        public ConsumerMovementPreflight nativeFirstStep;
        public int sightHits,blockingColliderId;public string blockingColliderName,sightReason;public float blockingDistance;
        public int samples,observationPass=-1,predictionIndex=-1;
        public float observedPhysicsTime=-1f;
        public Vector2 start,target,pose,closestPoint,targetBoundsMin,targetBoundsMax,currentTargetRoot,nativeTargetRoot,expectedTargetCentre;
        public float originalAttackRange,sampleLength,closestDistance,targetBodyResidual,targetCentreResidual,targetExtentsResidual,targetRotationResidual;
    }
    private static PursuitTrialPrediction PredictPursuitTrial(EcologyResidentBinding resident,Collider2D nativeFoot,Transform targetActor,Vector2 start,Vector2 target)
        => PredictPursuitTrialCore(resident,nativeFoot,targetActor,start,target,false);
    private static PursuitTrialPrediction PredictVisiblePursuitTrial(EcologyResidentBinding resident,Collider2D nativeFoot,Transform targetActor,Vector2 start,Vector2 target)
        => PredictPursuitTrialCore(resident,nativeFoot,targetActor,start,target,true);
    private static bool OriginalProjectedSight(EcologyResidentBinding resident,Transform target,Vector2 observerRoot,Vector2 targetRoot,PursuitTrialPrediction result)
    {
        result.visibilityChecked=true;
        if(target==null||!target.gameObject.activeInHierarchy) {result.sightReason="OriginalTargetInactive";result.projectedVisible=false;return false;}
        var owner=resident.GetComponent<MonsterPerceptionController>();
        var originTransform=(Transform)typeof(MonsterPerceptionController).GetField("sightOrigin",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        Vector2 origin=observerRoot+(originTransform==null?Vector2.zero:(Vector2)originTransform.position-(Vector2)resident.transform.position);
        Vector2 targetCentre=(Vector2)typeof(MonsterPerceptionController).GetMethod("GetTargetPosition",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,new object[]{target})+targetRoot-(Vector2)target.position;
        Vector2 delta=targetCentre-origin;float squared=delta.sqrMagnitude;
        result.projectedVisible=false;result.blockingColliderId=0;result.blockingColliderName=null;result.blockingDistance=0;result.sightHits=0;result.sightBufferSaturated=false;
        if(target==null||!target.gameObject.activeInHierarchy) {result.sightReason="OriginalTargetInactive";return false;}
        if(squared>owner.DetectionRadius*owner.DetectionRadius) {result.sightReason="OriginalDetectionRadius";return false;}
        if(squared<=.0001f) {result.sightReason="OriginalCoincidentSight";result.projectedVisible=true;return true;}
        var hits=new RaycastHit2D[16];var mask=(LayerMask)typeof(MonsterPerceptionController).GetField("sightBlockingLayers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        float distance=Mathf.Sqrt(squared);int count=Physics2D.RaycastNonAlloc(origin,delta/distance,hits,distance,mask);result.sightHits=count;
        if(count==hits.Length) {result.sightBufferSaturated=true;result.sightReason="UncertifiedSaturatedOriginalSightBuffer";return false;}
        for(int i=0;i<count;i++)
        {
            var hit=hits[i];if(hit.collider==null)continue;
            if(hit.distance<=.0001f&&hit.collider.OverlapPoint(origin))continue;
            var actor=hit.collider.transform;
            if(actor==resident.transform||actor.IsChildOf(resident.transform)||actor==target||actor.IsChildOf(target)||target.IsChildOf(actor))continue;
            result.sightReason="OriginalSightBlockingCollider";result.blockingColliderId=hit.collider.GetInstanceID();result.blockingColliderName=hit.collider.name;result.blockingDistance=hit.distance;return false;
        }
        result.sightReason="OriginalProjectedSightClear";result.projectedVisible=true;return true;
    }
    private static bool OriginalNativePursuitStartClear(EcologyResidentBinding resident,Collider2D nativeFoot,Vector2 start,Vector2 target,PursuitTrialPrediction result)
    {
        var nativeBody=nativeFoot.attachedRigidbody;
        if(nativeBody==null||nativeBody!=resident.GetComponent<Rigidbody2D>()||Vector2.Distance(nativeBody.position,start)>.0001f||Vector2.Distance(nativeBody.position,resident.transform.position)>.0001f)
        {result.reason="StaleNativeResidentStart";return false;}
        // The held AI destination may still be a Patrol target. Query the prospective
        // pursuit waypoint at the current native pose without setting that destination.
        var step=InspectProspectivePursuitStep(resident,target);result.nativeFirstStep=step;
        if(step.observedHomeGeneration!=resident.GenerationId.ToString()||!step.enabled||step.locked||!step.canAct||!step.motionReady||!step.bodySimulated)
        {result.reason="OriginalNormalMovementUnavailable";return false;}
        if(!step.waypointAvailable||step.wouldStopAtTolerance||!step.castReadbackPerformed||step.requestedDistance<=0f)
        {result.reason="OriginalProspectivePursuitWaypointUnavailable";return false;}
        if(step.bodyHitCount>=8||Mathf.Abs(step.originalAllowed-step.independentBodyAllowed)>.000001f)
        {result.reason="UncertifiedOriginalNativeFirstStepReadback";return false;}
        if(step.originalAllowed+.0001f<step.requestedDistance)
        {result.reason="OriginalNativePursuitFirstStepBlockedOrClipped";return false;}
        return true;
    }
    private static bool MeasureOriginalTargetPose(MonsterAttackController attack,Transform targetActor,PursuitTrialPrediction result,out Collider2D targetCollider)
    {
        result.currentTargetRoot=targetActor.position;
        targetCollider=(Collider2D)typeof(MonsterAttackController).GetMethod("ResolveTargetCollider",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,new object[]{targetActor});
        if(targetCollider==null||!targetCollider.enabled) {result.reason="OriginalTargetColliderUnavailable";return false;}
        result.targetCollider=targetCollider.GetType().Name;result.targetBoundsMin=targetCollider.bounds.min;result.targetBoundsMax=targetCollider.bounds.max;
        var capsule=targetCollider as CapsuleCollider2D;var targetBody=targetCollider.attachedRigidbody;
        // ClosestPoint is a native query. Never translate it with a Transform from
        // another physics phase, and never repair that mismatch by moving an actor.
        if(capsule==null||targetBody==null||!targetBody.simulated||!targetActor.gameObject.activeInHierarchy||capsule.direction!=CapsuleDirection2D.Vertical||capsule.transform!=targetActor||targetBody.transform!=targetActor||
            targetBody.interpolation!=RigidbodyInterpolation2D.None||Vector3.Distance(targetActor.lossyScale,Vector3.one)>.0001f||
            Mathf.Abs(Mathf.DeltaAngle(targetActor.eulerAngles.z,0f))>.001f)
        {result.reason="UnsupportedOriginalTargetPose";return false;}
        result.nativeTargetRoot=targetBody.position;result.expectedTargetCentre=targetActor.TransformPoint(capsule.offset);
        result.targetBodyResidual=Vector2.Distance(result.nativeTargetRoot,result.currentTargetRoot);
        result.targetRotationResidual=Mathf.Abs(Mathf.DeltaAngle(targetBody.rotation,targetActor.eulerAngles.z));
        result.targetCentreResidual=Vector2.Distance(result.expectedTargetCentre,capsule.bounds.center);
        result.targetExtentsResidual=Vector2.Distance(capsule.bounds.extents,capsule.size*.5f);
        result.targetPoseCoherent=result.targetBodyResidual<.0001f&&result.targetRotationResidual<.0001f&&result.targetCentreResidual<.0001f&&result.targetExtentsResidual<.0001f;
        if(!result.targetPoseCoherent) {result.reason="StaleTargetPose";return false;}
        return true;
    }
    [Serializable] private sealed class ConsumerSetupPhase
    {
        public string buildId,generation,resident,boundary,status,scope="CANONICAL_SETUP_POSE_ONLY_NOT_ROUTE_PREDICTION_OR_SUBMISSION";
        public int frame;public float physicsTime;public bool inFixedTimeStep,sameGeneration,residentPoseCoherent,coherent;
        public Vector2 residentNativePosition,residentTransformPosition;
        public PursuitTrialPrediction targetPose;
    }
    private ConsumerSetupPhase ObserveConsumerSetupPhase(EcologyResidentBinding resident,Collider2D nativeFoot,string boundary)
    {
        var targetPose=new PursuitTrialPrediction {buildId=MapHostBuildStamp.BuildId,generation=resident.GenerationId.ToString(),resident=resident.ResidentId};
        bool targetCoherent=MeasureOriginalTargetPose(resident.GetComponent<MonsterAttackController>(),body.transform,targetPose,out _);
        var nativeBody=nativeFoot.attachedRigidbody;
        var row=new ConsumerSetupPhase {buildId=MapHostBuildStamp.BuildId,generation=Context.ActiveGenerationId.ToString(),resident=resident.ResidentId,boundary=boundary,frame=Time.frameCount,physicsTime=Time.fixedTime,inFixedTimeStep=Time.inFixedTimeStep,
            sameGeneration=Context.ActiveGenerationId==resident.GenerationId,residentNativePosition=nativeBody==null?default:nativeBody.position,residentTransformPosition=resident.transform.position,targetPose=targetPose};
        bool residentBodySupported=nativeBody!=null&&nativeBody==resident.GetComponent<Rigidbody2D>();
        row.residentPoseCoherent=residentBodySupported&&Vector2.Distance(nativeBody.position,resident.transform.position)<=.0001f;
        row.coherent=row.sameGeneration&&targetCoherent&&row.residentPoseCoherent;
        row.status=!row.sameGeneration||!residentBodySupported||(!targetCoherent&&targetPose.reason!="StaleTargetPose")?"UNSUPPORTED_OR_ERROR":row.coherent?"COHERENT":"POSE_PHASE_MISMATCH";
        File.AppendAllText(Path.Combine(output,"consumer-setup-phase.jsonl"),JsonUtility.ToJson(row)+Environment.NewLine);
        Require(row.sameGeneration,"CanonicalSetupPhaseGenerationMismatch");
        Require(residentBodySupported,"CanonicalSetupResidentNativeBodyUnsupported");
        Context.ActiveSpatialContext.Require(Context.ActiveGenerationId.Value,Context.ActiveMap);
        Require(targetCoherent||targetPose.reason=="StaleTargetPose","CanonicalSetupPhaseUnsupported:"+targetPose.reason);
        return row;
    }
    private static PursuitTrialPrediction PredictPursuitTrialCore(EcologyResidentBinding resident,Collider2D nativeFoot,Transform targetActor,Vector2 start,Vector2 target,bool requireSight)
    {
        var result=new PursuitTrialPrediction {buildId=MapHostBuildStamp.BuildId,generation=resident.GenerationId.ToString(),resident=resident.ResidentId,start=start,target=target,pose=start,currentTargetRoot=targetActor.position};
        var attack=resident.GetComponent<MonsterAttackController>();
        var barrel=resident.GetComponent<BarrelExplosionAttack>();
        // The original explosive strategy uses CanTrigger, not this melee predicate.
        if(barrel!=null&&barrel.isActiveAndEnabled) {result.reason="UnsupportedOriginalBarrelPredicate";return result;}
        if(!MeasureOriginalTargetPose(attack,targetActor,result,out Collider2D targetCollider))return result;
        result.originalAttackRange=attack.AttackRange;
        result.sampleLength=Mathf.Min(.025f,resident.GetComponent<MonsterMovementController>().MoveSpeed*Time.fixedDeltaTime);
        if(result.sampleLength<=0f||!resident.Home.ContainsFullFoot(nativeFoot,start)) {result.reason="NoPositiveHomeStartOrSpeed";return result;}
        if(requireSight&&!OriginalNativePursuitStartClear(resident,nativeFoot,start,target,result))return result;
        Vector2 delta=target-(Vector2)targetActor.position;
        // These are query segments, not simulated physics ticks. Actual AI, contact
        // solving, delays and the unchanged 180/450-tick assertions remain decisive.
        for(int sample=0;sample<180;sample++)
        {
            result.samples=sample+1;result.fullHome=resident.Home.ContainsFullFoot(nativeFoot,result.pose);
            if(requireSight&&!OriginalProjectedSight(resident,targetActor,result.pose,target,result))
            {result.reason="PredictedSightUnavailableBeforeHomeExit:"+result.sightReason;return result;}
            result.closestPointQueried=true;result.closestPoint=targetCollider.ClosestPoint(result.pose-delta)+delta;
            result.closestDistance=Vector2.Distance(result.pose,result.closestPoint);
            if(result.fullHome&&MapElevationRuntimeQuery.SameLevel(result.pose,target)&&result.closestDistance<=result.originalAttackRange+result.sampleLength)
            {result.reason=result.closestDistance<=result.originalAttackRange?"OriginalAttackReachInsideFullHome":"SamplingEnvelopeTouchesInHomeAttackReach";return result;}
            if(!result.fullHome) {result.qualifies=true;result.reason="PredictedHomeExitBeforeAttack";return result;}
            if(!resident.Home.TryPursuitWaypoint(nativeFoot,result.pose,target,resident.Excursion.MaximumHomeSteps,out Vector2 waypoint))
            {result.reason="OriginalHomeWaypointUnavailable";return result;}
            Vector2 next=Vector2.MoveTowards(result.pose,waypoint,result.sampleLength);
            if((next-result.pose).sqrMagnitude<.00000001f) {result.reason="NoPredictedProgress";return result;}
            result.pose=next;
        }
        result.reason="FinitePredictionSegmentBudgetExhausted";return result;
    }
    private IEnumerator RunConsumerChecksCore()
    {
        Ready();BindPlayer();previousGround.Clear();trial="Actual-Residents-And-Static-Roles";
        Require(Context.ActiveMap.GenerationSeed==ConsumerFixtureSeed,"PredeclaredConsumerFixtureSeedRequired");
        evidence.checks.Add("Separate positive consumer input7727 fixed before execution. Original21193 fixed-target and moving-lure failures retained; no repair or equivalent coverage claim.");
        var bindings=FindObjectsOfType<EcologyResidentBinding>().Where(b=>b.GenerationId==Context.ActiveGenerationId).ToArray();
        Require(bindings.Length==Context.ActivePlacementPlan.Ecology.Residents.Count,"RealPopulationBindingCountMismatch");
        var starts=bindings.ToDictionary(b=>b,b=>(Vector2)b.transform.position);
        yield return ObserveConsumers(120);
        Require(bindings.Any(b=>Vector2.Distance(starts[b],b.transform.position)>.04f),"NoRealMonsterMotionObserved");
        evidence.checks.Add("Actual planned/Registry residents active; native patrol motion and all role ground/sweeps observed.");
        EcologyResidentBinding candidate=null;Collider2D monsterFoot=null;Vector2 approach=default;var playerNavigation=NavigationGeometry();
        int sightTrials=0,predictionTrials=0;
        var visibilitySearch=new List<string> {"pass,physicsTime,resident,transformX,transformY,bodyX,bodyY,perception,separation,candidateCount,sightTrialsBefore,generation,buildId"};
        for(int observation=0;observation<16&&candidate==null&&sightTrials<128&&predictionTrials<128;observation++)
        {
            bool waitForCoherentTarget=false;
            foreach(var resident in bindings.Where(b=>b.GetComponent<MonsterAIController>()!=null).OrderBy(b=>b.ResidentId,StringComparer.Ordinal))
            {
                var actualFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(resident);
                float perception=resident.GetComponent<MonsterPerceptionController>().DetectionRadius-.6f;
                float separation=resident.GetComponent<MonsterAttackController>().AttackRange+1f;
                var homeCentres=resident.Home.HomeNodes.Select(resident.Home.NodeWorldPosition).ToArray();
                var options=Enumerable.Range(0,resident.Home.MovementGraph.NodeCount).Where(n=>resident.Home.MovementGraph.IsLegal(n)).Select(resident.Home.NodeWorldPosition)
                    .Where(p=>Vector2.Distance(p,resident.transform.position)>1.4f&&Vector2.Distance(p,resident.transform.position)<perception&&!resident.Home.ContainsFullFoot(actualFoot,p)&&homeCentres.All(h=>Vector2.Distance(h,p)>separation))
                    .OrderBy(p=>Mathf.Abs(Vector2.Distance(p,resident.transform.position)-(perception-.1f))).ToArray();
                Vector2 nativePosition=actualFoot.attachedRigidbody.position;
                visibilitySearch.Add(string.Join(",",observation,F(Time.fixedTime),resident.ResidentId,F(resident.transform.position.x),F(resident.transform.position.y),F(nativePosition.x),F(nativePosition.y),F(perception),F(separation),options.Length,sightTrials,Context.ActiveGenerationId.ToString(),MapHostBuildStamp.BuildId));
                foreach(Vector2 point in options.Take(16))
                {
                    if(sightTrials>=128||predictionTrials>=128)break;
                    if(playerNavigation.IsTranslationClear(foot,point,point,0)&&MapSpatialPlacement.CanPlace(Context,Context.ActiveGenerationId.Value,body.transform,point,out _)&&resident.CanPursuePosition(resident.transform.position,point))
                    {
                        predictionTrials++;
                        var prediction=PredictVisiblePursuitTrial(resident,actualFoot,body.transform,actualFoot.attachedRigidbody.position,point);
                        prediction.observationPass=observation;prediction.predictionIndex=predictionTrials;prediction.observedPhysicsTime=Time.fixedTime;
                        File.AppendAllText(Path.Combine(output,"consumer-pursuit-prediction.jsonl"),JsonUtility.ToJson(prediction)+Environment.NewLine);
                        if(prediction.reason=="StaleTargetPose"||prediction.reason=="StaleNativeResidentStart") {waitForCoherentTarget=true;break;}
                        if(!prediction.qualifies)continue;
                        sightTrials++;yield return SetupFoot(point+(FootPosition-body.position),"AI-Lawful-Visibility-Search");
                        // Choose the earliest strictly coherent phase before any actual sight or
                        // route result. A route failure must never buy another frame of waiting.
                        yield return new WaitForFixedUpdate();
                        var setupPhase=ObserveConsumerSetupPhase(resident,actualFoot,"AfterOriginalFixedWait");
                        if(setupPhase.status=="POSE_PHASE_MISMATCH")
                        {
                            yield return null;
                            setupPhase=ObserveConsumerSetupPhase(resident,actualFoot,"AfterOriginalNullControl");
                            if(!setupPhase.coherent) {waitForCoherentTarget=true;break;}
                        }
                        var perceptionOwner=resident.GetComponent<MonsterPerceptionController>();
                        bool visible=(bool)typeof(MonsterPerceptionController).GetMethod("IsTargetVisible",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(perceptionOwner,new object[]{body.transform});
                        evidence.checks.Add("Original sight trial resident="+resident.ResidentId+" target="+point+" visible="+visible+" observation="+observation);Persist();AuditConsumers();
                        if(!visible)continue;
                        if(predictionTrials>=128)break;
                        predictionTrials++;
                        Vector2 actualTarget=body.position;
                        var verified=PredictVisiblePursuitTrial(resident,actualFoot,body.transform,actualFoot.attachedRigidbody.position,actualTarget);
                        verified.observationPass=observation;verified.predictionIndex=predictionTrials;verified.observedPhysicsTime=Time.fixedTime;
                        File.AppendAllText(Path.Combine(output,"consumer-pursuit-prediction.jsonl"),JsonUtility.ToJson(verified)+Environment.NewLine);
                        if(verified.reason=="StaleTargetPose"||verified.reason=="StaleNativeResidentStart") {waitForCoherentTarget=true;break;}
                        if(!verified.qualifies||resident.Home.ContainsFullFoot(actualFoot,actualTarget)||homeCentres.Any(h=>Vector2.Distance(h,actualTarget)<=separation))continue;
                        candidate=resident;monsterFoot=actualFoot;approach=actualTarget;break;
                    }
                }
                if(candidate!=null||waitForCoherentTarget||sightTrials>=128||predictionTrials>=128)break;
            }
            File.WriteAllLines(Path.Combine(output,"consumer-visibility-search.csv"),visibilitySearch);
            evidence.checks.Add("Bounded original Patrol observation="+observation+" original sight trials="+sightTrials+"/128 selected="+(candidate!=null)+" read-only predictions="+predictionTrials+"/128");Persist();
            // Patrol continues naturally. No actor or rule is changed to manufacture a trial point.
            if(candidate==null&&sightTrials<128&&predictionTrials<128&&observation<15)yield return ObserveConsumers(12);
        }
        Require(candidate!=null&&monsterFoot!=null,"NoLegalOutsideHomePerceptionTrialPoint");
        BindDiagnosticConsumer(candidate);
        var ai=candidate.GetComponent<MonsterAIController>();
        evidence.checks.Add("Pursuit trial selected actual resident="+candidate.ResidentId+" start="+candidate.transform.position+" target="+approach+"; target is separated from all Home nodes beyond original AttackRange+1.");Persist();
        // The candidate was just checked after its lawful setup. Do not repeat a setup
        // that lets Patrol invalidate this native starting pose before observation.
        trial="Actual-AI-Pursuit";var states=new HashSet<MonsterStateType>();float chaseDistance=0f;bool outsideHome=false;
        Vector2 previous=candidate.transform.position;MonsterStateType previousState=ai.CurrentStateType;
        for(int i=0;i<180;i++)
        {
            yield return new WaitForFixedUpdate();RequireConsumerActor(candidate,"ActualPursuitActorInactiveOrRetired");
            Vector2 current=candidate.transform.position;MonsterStateType state=ai.CurrentStateType;states.Add(state);
            if(previousState==MonsterStateType.Chase&&state==MonsterStateType.Chase)chaseDistance+=Vector2.Distance(previous,current);
            outsideHome|=!candidate.Home.ContainsFullFoot(monsterFoot,monsterFoot.attachedRigidbody.position);
            previous=current;previousState=state;AuditConsumers();
            if(outsideHome&&chaseDistance>.03f)break;
        }
        Require(states.Contains(MonsterStateType.Chase)&&chaseDistance>.03f,"RealChaseStateProducedNoMotion:"+F(chaseDistance));
        Require(outsideHome&&!candidate.Home.ContainsFullFoot(monsterFoot,monsterFoot.attachedRigidbody.position),"PursuitDidNotLeaveHomeForPositiveReturnTrial");
        yield return SetupFoot((Vector2)map.TilemapRenderer.Coordinates.CellToWorld(Context.ActiveMap.SpawnCell)+(FootPosition-body.position),"Return-Target-To-Spawn-Safety");
        RecordConsumerLifecycle("AfterReturnTargetSetup");
        trial="Actual-AI-Return";bool returned=false;bool sawReturn=false;float returnDistance=0f;previous=candidate.transform.position;previousState=ai.CurrentStateType;
        for(int i=0;i<450;i++)
        {
            yield return new WaitForFixedUpdate();RequireConsumerActor(candidate,"ActualReturnActorInactiveOrRetired");
            Vector2 current=candidate.transform.position;MonsterStateType state=ai.CurrentStateType;sawReturn|=state==MonsterStateType.Return;
            if(previousState==MonsterStateType.Return&&state==MonsterStateType.Return)returnDistance+=Vector2.Distance(previous,current);
            previous=current;previousState=state;AuditConsumers();
            if(sawReturn&&state==MonsterStateType.Patrol)
            {
                Require(candidate.Home.ContainsFullFoot(monsterFoot,monsterFoot.attachedRigidbody.position),"ReturnPatrolPublishedOutsideFullHome");
                Require(candidate.TryGetHomeDistance(out int distance,out _)&&distance==0,"ReturnPatrolPublishedWithoutOriginalHomeDistanceZero");returned=true;break;
            }
        }
        Require(returned&&returnDistance>.03f,"ActualOutsideHomeReturnNotObserved:"+F(returnDistance));
        evidence.checks.Add("Actual perception -> Chase-state motion="+F(chaseDistance)+" -> outside Home -> Return-state motion="+F(returnDistance)+" -> full original Home and distance0 -> Patrol; setup logged separately.");Persist();
        yield return VerifyAnimalHurt();
        var floor=FindObjectOfType<TowerFloorRuntimeController>(true);Require(floor.State==TowerFloorExitState.Locked,"PortalMustStartLocked");
        var destructible=Context.ActiveRegistry.Entries.Where(e=>e.Role==DestructiblePlacementService.DestructibleRole).Select(e=>Actor(e.Instance)?.GetComponent<BaseDamageable>()).First(d=>d!=null);
        trial="Actual-Destructible-Receiver";destructible.TakeDamage(destructible.MaxHealth+1);yield return ObserveConsumers(2);
        Require(floor.State==TowerFloorExitState.Locked,"DestructibleMustNotCompleteMonsterTargets");
        foreach(var target in Context.ActiveRegistry.Entries.Where(e=>e.Role==MonsterPlacementService.MonsterRole).Select(e=>Actor(e.Instance)?.GetComponent<BaseDamageable>()).Where(d=>d!=null).ToArray()) target.TakeDamage(target.MaxHealth+1);
        yield return ObserveConsumers(40);Require(floor.State==TowerFloorExitState.Available&&Portal.activeInHierarchy,"ActualTargetsDidNotActivatePortal");
        Require(floor.DefeatedTargetCount==floor.TargetCount,"PortalTargetPopulationMismatch");
        var drops=FindObjectsOfType<RewardPickup>();Require(drops.Length>0,"ActualRewardReceiverProducedNoDrops");
        evidence.checks.Add("Actual BaseDamageable death receivers spawned "+drops.Length+" active original rewards; all ground footprints audited. Receiver invocation is an opt-in lifecycle fixture, not natural player combat.");
        yield return VerifyActualRewardPool(drops.OrderBy(d=>MapSpatialRewardPlacement.UsesImplicitPickupCircle(((RewardSO)typeof(RewardPickup).GetField("reward",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d)).rewardPrefab.transform)?0:1).First());
        Guid previousGeneration=Context.ActiveGenerationId.Value;var previousRegistry=Context.ActiveRegistry;
        var exitGeometry=NavigationGeometry();var exitGraph=exitGeometry.BuildNavigationGraph(foot,0);
        Vector2 legalExitApproach=Enumerable.Range(0,exitGraph.NodeCount).Where(n=>exitGraph.IsLegal(n)).Select(exitGeometry.NodeWorldPosition)
            .Where(p=>Vector2.Distance(p,Portal.transform.position)<=floor.ExitInteractionRadius&&MapSpatialPlacement.CanPlace(Context,Context.ActiveGenerationId.Value,body.transform,p,out _))
            .OrderBy(p=>Vector2.Distance(p,Portal.transform.position)).First();
        yield return SetupFoot(legalExitApproach+(FootPosition-body.position),"Legal-Portal-Interaction-Start");
        trial="Actual-F-Portal-Transition";
        // The original handler reads Transform.position, so wait for the explicit lawful
        // Rigidbody setup to reach the physics/Transform boundary before queuing input.
        yield return new WaitForFixedUpdate();yield return null;
        float interactionDistance=Vector2.Distance(body.transform.position,Portal.transform.position);
        Require(interactionDistance<=floor.ExitInteractionRadius,"ActualPortalTransformOutsideInteractionRadius:"+F(interactionDistance));
        Require(GameInput.Instance.IsGameplayInputEnabled&&!TowerRunGateHub.IsGameplayBlocked,"ActualPortalInputBlocked");
        int[] publishedFloorInputs={-603885409,-1905819578,638124224};
        session.ConfigureNextFloorInputs(Context.ActiveGenerationId.Value,publishedFloorInputs);
        evidence.checks.Add("Explicit isolated F/retry inputs replay actual a24 sequence: -603885409,-1905819578,638124224. Default Guid source unchanged; no random fallback or retry increase.");Persist();
        int interactEvents=0;Action observeInteract=()=>interactEvents++;GameInput.Instance.OnInteractPressed+=observeInteract;
        try
        {
            Feed(Key.F);
            for(int frame=0;frame<16&&interactEvents==0;frame++)yield return null;
            Require(interactEvents==1,"ActualFDidNotProduceOneOriginalGameInputEvent:"+interactEvents);
        }
        finally {Feed();GameInput.Instance.OnInteractPressed-=observeInteract;}
        yield return null;
        evidence.checks.Add("Actual F original GameInput events="+interactEvents+" Transform interaction distance="+F(interactionDistance)+" requestedSeed="+Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed+" original floor state="+floor.State+" session="+session.State);Persist();
        for(int retry=0;session.State==TowerRunFlowState.Failure&&retry<3;retry++)
        {
            string code=Context.Failure?.Code;Require(code=="VNextFormalLayoutRejected"||code=="EcologyBudgetExhaustedOnFixedTerrain","UnexpectedNextFloorFailure:"+code);
            evidence.checks.Add("Original bounded next-floor rejected input="+Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed+" reason="+code+":"+session.FailureMessage);Persist();session.RetryFailedOperation();yield return null;
        }
        if(session.State==TowerRunFlowState.Failure)
        {evidence.checks.Add("Final bounded next-floor rejected input="+Context.CreateDiagnosticSnapshot().ActiveRequest.RequestedSeed+" reason="+Context.Failure?.Code+":"+session.FailureMessage);Persist();}
        Ready();Require(Context.ActiveGenerationId!=previousGeneration&&previousRegistry.IsRetired,"NextFloorDidNotRetireOldGeneration");
        Require(Entrance().floor.floorNumber==1&&Entrance().generation.profileId==EcologyRecipeDefinition.VNextI1ProfileId,"NextFloorLostVersionOrNumber");
        Require(Entrance().generation.requestedSeed==638124224,"PublishedNextFloorInputNotAccepted");
        BindPlayer();SaveAndReopen(3);BindPlayer();evidence.checks.Add("Real F input accepted by original floor/session owner; floor1 new context and private save/reopen exact.");
        var deathCheckpoint=Entrance();var deathWrite=session.TrySaveEntranceCandidate(1,deathCheckpoint,session.ReadSlot(1).state!=TowerRunSlotState.Empty,session.ReadSlot(1).confirmationToken);Require(deathWrite.succeeded,deathWrite.message);
        var damage=body.GetComponent<PlayerDamageController>();Require(damage.TakeDamage(damage.CurrentHealth+100,body.transform),"ActualPlayerDeathReceiverRejected");
        yield return null;Require(session.State==TowerRunFlowState.DeathChoice&&damage.IsDefeated,"OriginalDeathChoiceNotReached");
        var restore=session.ReadSlot(1);Require(restore.state==TowerRunSlotState.Valid,restore.message);session.BeginRestoreSlot(1,restore.confirmationToken);Ready();BindPlayer();
        Require(!body.GetComponent<PlayerDamageController>().IsDefeated&&body.GetComponent<PlayerDamageController>().CurrentHealth>0,"DeathRestoreDidNotRestoreActualPlayer");
        SaveAndReopen(2);evidence.checks.Add("Actual player death receiver -> DeathChoice -> copied private entrance restore -> GameplayReady -> save/reopen. No original progress migration.");
        FlushConsumerTrace();Persist();
    }
    private IEnumerator VerifyAnimalHurt()
    {
        var animals=Context.ActiveRegistry.Entries.Where(e=>e.Role==AnimalPlacementService.AnimalRole).Select(e=>Actor(e.Instance)).Where(a=>a!=null&&a.activeInHierarchy&&a.GetComponent<AnimalHurtController>()?.isActiveAndEnabled==true);
        GameObject animal=null;Vector3 direction=default;
        foreach(var actor in animals)
        {
            var lease=actor.GetComponent<MapSpatialActor>();Require(lease!=null,"ActualAnimalSpatialLeaseMissing");
            float distance=(float)typeof(AnimalHurtController).GetField("knockbackDistance",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor.GetComponent<AnimalHurtController>());
            Require(distance>.03f,"PositiveAnimalKnockbackConfigurationTooSmall");
            for(int n=0;n<8;n++)
            {
                Vector3 vector=new Vector3(Mathf.Cos(n*Mathf.PI/4),Mathf.Sin(n*Mathf.PI/4),0);
                if(lease.CanMove(actor.transform.position,actor.transform.position+vector*distance)) {animal=actor;direction=vector;break;}
            }
            if(animal!=null)break;
        }
        Require(animal!=null,"NoLegalPositiveAnimalHurtDirection");trial="Actual-Animal-Hurt";Vector3 start=animal.transform.position;float peak=0;
        animal.GetComponent<AnimalHurtController>().PlayHurt(start-direction);
        for(int frame=0;frame<90;frame++)
        {
            yield return null;Require(animal.activeInHierarchy&&animal.GetComponent<AnimalHurtController>().isActiveAndEnabled,"AnimalHurtActorInactive");
            peak=Math.Max(peak,Vector2.Distance(start,animal.transform.position));AuditConsumers();
            if(peak>.03f&&Vector2.Distance(start,animal.transform.position)<.005f)break;
        }
        Require(peak>.03f,"ActualAnimalHurtProducedNoPositiveDisplacement");
        Require(Vector2.Distance(start,animal.transform.position)<.005f,"ActualAnimalHurtDidNotReturnWithinFiniteTrial");
        evidence.checks.Add("Actual active AnimalHurtController positive peak="+F(peak)+" and return to original position <.005; each rendered-frame segment audited. No lease reset or animal setup.");Persist();
    }
    private IEnumerator VerifyActualRewardPool(RewardPickup actual)
    {
        var reward=(RewardSO)typeof(RewardPickup).GetField("reward",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actual);
        Require(reward!=null&&reward.rewardPrefab!=null,"ActualRolledRewardDefinitionMissing");
        Require(MapSpatialRewardPlacement.TryFind(Context,Context.ActiveGenerationId.Value,reward.rewardPrefab.transform,actual.transform.position,actual.transform.position,out Vector2 legal),"ActualRewardHasNoLegalReusePoint");
        var seen=new HashSet<int>();bool reused=false;
        int queued=((Dictionary<GameObject,Queue<GameObject>>)typeof(ObjectPoolManager).GetField("poolMap",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ObjectPoolManager.Instance)).TryGetValue(reward.rewardPrefab,out var queue)?queue.Count:0;
        Require(queued<64,"PoolReuseTrialQueueExceedsFiniteBudget");
        for(int attempt=0;attempt<=queued+1&&!reused;attempt++)
        {
            var actor=ObjectPoolManager.SpawnPrepared(reward.rewardPrefab,legal,Quaternion.identity,item=>
            {
                MapSpatialRewardPlacement.PrepareOriginalPickupCollider(item);
                Require(MapSpatialPlacement.Prepare(Context,Context.ActiveGenerationId.Value,item,legal,out string reason),reason);
                var pickup=item.GetComponent<RewardPickup>()??item.AddComponent<RewardPickup>();pickup.Initialize(reward,37);
                var witness=item.GetComponent<MapVNextI1ActivationWitness>()??item.AddComponent<MapVNextI1ActivationWitness>();witness.ExpectedAmount=37;witness.ExpectedCircleRadius=MapSpatialRewardPlacement.UsesImplicitPickupCircle(reward.rewardPrefab.transform)?.5f:-1f;witness.Valid=false;
            });
            Require(actor.GetComponent<MapVNextI1ActivationWitness>().Valid,"RewardActivationOccurredBeforeQuantityInitialization");
            AuditGround(actor,"pool-reward-"+actor.GetInstanceID(),"ActualRewardPool");reused=!seen.Add(actor.GetInstanceID());ObjectPoolManager.ReturnOrDeactivate(actor);Require(!actor.activeInHierarchy,"RewardPoolReturnFailed");
        }
        foreach(int instance in seen)
        {var actor=Resources.FindObjectsOfTypeAll<MapVNextI1ActivationWitness>().FirstOrDefault(w=>w.gameObject.GetInstanceID()==instance);if(actor!=null)Destroy(actor);}
        Require(reused,"ActualRewardPoolReuseNotObserved");evidence.checks.Add("Real rolled reward prefab SpawnPrepared/return/reuse; OnEnable witnessed exact initialized quantity37 before activation; no reroll.");Persist();yield return null;
    }
}
