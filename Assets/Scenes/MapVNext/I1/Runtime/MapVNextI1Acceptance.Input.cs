using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using MapVNext;

public sealed partial class MapVNextI1Acceptance
{
    private int samples,dualSamples;
    private bool inputSaved;
    private Vector2 queuedMove;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
    private void BindInput()
    {
        previousBackground=InputSystem.settings.backgroundBehavior;previousRunInBackground=Application.runInBackground;inputSaved=true;
#if UNITY_EDITOR
        previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>("I1ActualInputEvidence");
        if(autoDefense)defenseMouse=InputSystem.AddDevice<Mouse>("I1ActualBattleInputEvidence");
    }
    private void CleanupInput()
    {
        StopDefenseObservation();
        if(defenseMouse!=null&&defenseMouse.added) {InputSystem.QueueStateEvent(defenseMouse,new MouseState());InputSystem.RemoveDevice(defenseMouse);}defenseMouse=null;
        if(keyboard!=null&&keyboard.added) {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}keyboard=null;
        if(!inputSaved)return;
        InputSystem.settings.backgroundBehavior=previousBackground;Application.runInBackground=previousRunInBackground;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
#endif
        inputSaved=false;
    }
    private void BindPlayer()
    {
        foot=session.PlayerMovementFoot as CircleCollider2D;Require(foot!=null&&Mathf.Abs(foot.radius-.30f)<.000001f,"CanonicalPointThreeFootChanged");
        body=foot.attachedRigidbody;action=body.GetComponent<PlayerAction>();Require(action!=null,"OriginalPlayerActionMissing");
        rock=MapVNextCliffExclusion.Derive(Context.ActiveMap,map.TilemapRenderer.Coordinates);
    }
    private void Feed(params Key[] held)
    {
        keys=held.Length==0?"None":string.Join("+",held.Select(k=>k.ToString()));
        queuedMove=new Vector2((held.Contains(Key.D)?1:0)-(held.Contains(Key.A)?1:0),(held.Contains(Key.W)?1:0)-(held.Contains(Key.S)?1:0)).normalized;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));ReleaseDefenseMouse();
    }
    private IEnumerator Release()
    {
        Feed();for(int i=0;i<16;i++) {yield return null;yield return new WaitForFixedUpdate();if(action==null||action.MoveInput.sqrMagnitude<.000001f)break;}
        Require(action==null||action.MoveInput.sqrMagnitude<.000001f,"EffectiveInputDidNotRelease");
    }
    private void Sample(Vector2 previousFoot)
    {
        Ready();Context.ActiveSpatialContext.Require(Context.ActiveGenerationId.Value,Context.ActiveMap);
        Vector2 p=FootPosition,m=action.MoveInput;
        Require(!MapVNextSurfaceQuery.SweepIntrudesRock(rock,MapVNextGroundFootprint.Circle(foot.radius*Mathf.Abs(foot.transform.lossyScale.x)),previousFoot,p),"ActualFootOrSweepIntrudedRock:"+trial);
        var cell=map.TilemapRenderer.Coordinates.WorldToCell(p);Require(Context.ActiveMap.IsInside(cell),"ActualFootOutsideMap");
        trace.Add(string.Join(",",trial,keys,F(Time.fixedTime),Time.frameCount,F(body.position.x),F(body.position.y),F(p.x),F(p.y),F(m.x),F(m.y),Context.ActiveMap.GetElevation(cell),Context.ActiveGenerationId,MapHostBuildStamp.BuildId));
        if(Mathf.Abs(m.x)>.5f&&Mathf.Abs(m.y)>.5f)dualSamples++;samples++;
    }
    private static bool SameInput(Vector2 first,Vector2 second)=>(first-second).sqrMagnitude<.000001f;
    private bool EffectiveAcknowledged()
    {
        if(keyboard==null||GameInput.Instance==null||action==null)return false;
        var nativeKeys=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0)).normalized;
        return SameInput(nativeKeys,queuedMove)&&SameInput(GameInput.Instance.MoveInput,queuedMove)&&SameInput(action.MoveInput,queuedMove);
    }
    private bool RouteClear(MapCandidateCollisionGeometry geometry,Vector2 start,Vector2 end)
    {
        if(!geometry.IsTranslationClear(foot,start,end,0))return false;
        Vector2 delta=end-start,offset=FootPosition-body.position;
        float radius=foot.radius*Mathf.Abs(foot.transform.lossyScale.x);
        var hurt=body.GetComponent<PlayerHurtController>();
        // Same original mask, trigger rule, circle and forward skin. This is a
        // query from an arbitrary origin, not a future-pose Collider.Cast.
        if(delta.sqrMagnitude<.000001f)
        {
            var overlaps=Physics2D.OverlapCircleAll(start+offset,radius,hurt.MovementBlockingLayers);
            return overlaps.Length<=256&&!overlaps.Any(c=>c!=null&&!c.isTrigger&&c.attachedRigidbody!=body);
        }
        var hits=Physics2D.CircleCastAll(start+offset,radius,delta.normalized,
            delta.magnitude+hurt.MovementCollisionSkin,hurt.MovementBlockingLayers);
        if(hits.Length>256)return false;
        return !hits.Any(h=>h.collider!=null&&!h.collider.isTrigger&&h.collider.attachedRigidbody!=body&&
            h.distance<delta.magnitude+hurt.MovementCollisionSkin-.00001f);
    }
    private bool CanHandoff(MapCandidateCollisionGeometry geometry,Vector2 position,Vector2 target,Vector2? onward)
    {
        if(Vector2.Distance(position,target)>=.22f||!RouteClear(geometry,position,target))return false;
        if(!onward.HasValue)return true;
        Vector2 leg=onward.Value-target;
        float cross=leg.sqrMagnitude<.000001f?0f:Mathf.Abs(leg.x*(position.y-target.y)-leg.y*(position.x-target.x))/leg.magnitude;
        return cross<.02f&&RouteClear(geometry,position,onward.Value);
    }
    private Key[] HandoffCorrection(MapCandidateCollisionGeometry geometry,Vector2 target,Vector2? onward)
    {
        float speed=(float)typeof(PlayerAction).GetField("moveSpeed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(action);
        float step=speed*Time.fixedDeltaTime;Require(step>0f,"OriginalPlayerStepUnavailable");
        var directions=new[]{new Vector2(0,-1),new Vector2(0,1),new Vector2(-1,0),new Vector2(1,0),new Vector2(-1,-1).normalized,new Vector2(1,-1).normalized,new Vector2(-1,1).normalized,new Vector2(1,1).normalized};
        var queue=new Queue<(Vector2 position,int first,int depth)>();queue.Enqueue((body.position,-1,0));
        var visited=new HashSet<Vector2Int>();int evaluated=0;int level=Context.ActiveMap.GetElevation(map.TilemapRenderer.Coordinates.WorldToCell(FootPosition));
        // Finite local query only; every executed correction still consumes the
        // original waypoint's physics budget and uses original full-speed keys.
        while(queue.Count>0&&evaluated++<512)
        {
            var current=queue.Dequeue();if(current.depth>=4)continue;
            for(int d=0;d<directions.Length;d++)
            {
                Vector2 next=current.position+directions[d]*step;
                if(Vector2.Distance(next,target)>.55f||!visited.Add(new Vector2Int(Mathf.RoundToInt(next.x*100000f),Mathf.RoundToInt(next.y*100000f))))continue;
                var cell=map.TilemapRenderer.Coordinates.WorldToCell(next+FootPosition-body.position);
                if(!Context.ActiveMap.IsInside(cell)||Context.ActiveMap.GetElevation(cell)!=level||!RouteClear(geometry,current.position,next))continue;
                int first=current.first<0?d:current.first;
                if(CanHandoff(geometry,next,target,onward))return KeysFor(directions[first],0f);
                queue.Enqueue((next,first,current.depth+1));
            }
        }
        throw new InvalidOperationException("NoLawfulFiniteInputHandoffCorrection:"+trial+":"+body.position);
    }
    private static Key[] KeysFor(Vector2 delta,float deadZone)
    {
        var held=new List<Key>();if(Mathf.Abs(delta.x)>deadZone)held.Add(delta.x>0?Key.D:Key.A);
        if(Mathf.Abs(delta.y)>deadZone)held.Add(delta.y>0?Key.W:Key.S);return held.ToArray();
    }
    private IEnumerator MoveBodyTo(Vector2 target,bool capture,Vector2? onward=null)
    {
        Exception samplingFailure=null;
        var walkBinding=body==null?null:body.GetComponent<EcologyCombatBinding>();
        var walkBarrier=walkBinding==null?null:walkBinding.Barrier;
        long walkLease=walkBinding==null?-1:walkBinding.Lease;
        var walkGeneration=Context.ActiveGenerationId;
        long Step()
        {
            if(samplingFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(samplingFailure).Throw();
            var current=body==null?null:body.GetComponent<EcologyCombatBinding>();
            var stats=GameSession.Instance==null?null:GameSession.Instance.PlayerStats;
            float health=stats==null?float.NaN:stats.CurrentHealth;
            string failure=health<=0f?"OriginalPlayerDiedDuringPositiveWalk":
                (stats==null||current==null||current!=walkBinding||walkBarrier==null||current.Barrier!=walkBarrier||
                 current.Lease!=walkLease||!walkGeneration.HasValue||Context.ActiveGenerationId!=walkGeneration?"OriginalPositiveWalkOwnerRetired":null);
            if(failure!=null)throw new InvalidOperationException(failure+":"+trial+":health="+F(health)+
                ":expectedLease="+walkLease+":actualLease="+(current==null?-1:current.Lease)+
                ":barrierNull="+(current==null||current.Barrier==null)+":expectedGeneration="+walkGeneration+
                ":actualGeneration="+Context.ActiveGenerationId);
            return walkBarrier.CurrentStep;
        }
        long initialStep=Step();
        int limit=Math.Min(2400,240+Mathf.CeilToInt(Vector2.Distance(body.position,target)/.035f));bool arrived=false,correcting=false;
        var geometry=NavigationGeometry();int captureAt=12;
        IEnumerator ObserveEveryStep()
        {
            Vector2 previous=FootPosition;long last=initialStep;
            while(true)
            {
                yield return new WaitForFixedUpdate();
                try
                {
                    long current=Step();if(current==last)continue;last=current;
                    Sample(previous);ObserveDefenseStep();previous=FootPosition;
                }
                catch(Exception error) {if(samplingFailure==null)samplingFailure=error;yield break;}
            }
        }
        var observer=StartCoroutine(ObserveEveryStep());
        long Used()=>Step()-initialStep;
        IEnumerator Tick()
        {
            Require(Used()<limit,"ActualInputFiniteStepBudgetExhausted:"+trial+":"+target+":actual="+body.position);
            yield return new WaitForFixedUpdate();if(samplingFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(samplingFailure).Throw();
        }
        IEnumerator Acknowledge()
        {
            for(int waiting=0;!EffectiveAcknowledged()&&waiting<16;waiting++) {yield return null;yield return Tick();}
            Require(EffectiveAcknowledged(),"EffectiveQueuedInputNotAcknowledged:"+trial+":"+keys);
        }
        IEnumerator Apply(params Key[] held)
        {
            Vector2 expected=new Vector2((held.Contains(Key.D)?1:0)-(held.Contains(Key.A)?1:0),(held.Contains(Key.W)?1:0)-(held.Contains(Key.S)?1:0)).normalized;
            if(!SameInput(queuedMove,expected))Feed(held);else ReleaseDefenseMouse();
            if(held.Length>0)FeedDefense();yield return Tick();yield return Acknowledge();
        }
        // A queued direction may still be pending when this waypoint is entered.
        // Do not coalesce it with another event before the original owner sees it.
        try
        {
        yield return Acknowledge();
        while(Used()<limit)
        {
            Vector2 delta=target-body.position;
            if(delta.magnitude<.115f||correcting)
            {
                if(!SameInput(action.MoveInput,Vector2.zero))yield return Apply();
                if(CanHandoff(geometry,body.position,target,onward)) {arrived=true;break;}
                correcting=Vector2.Distance(body.position,target)<.22f;
                if(correcting)
                {yield return Apply(HandoffCorrection(geometry,target,onward));yield return Apply();continue;}
            }
            var held=KeysFor(target-body.position,.045f);
            Require(held.Length>0,"UnresolvedActualInputWaypointDeadZone:"+trial);
            Vector2 expected=new Vector2((held.Contains(Key.D)?1:0)-(held.Contains(Key.A)?1:0),(held.Contains(Key.W)?1:0)-(held.Contains(Key.S)?1:0)).normalized;
            if(!SameInput(action.MoveInput,Vector2.zero)&&!SameInput(action.MoveInput,expected))
            {yield return Apply();continue;}
            yield return Apply(held);
            if(capture&&Used()>=captureAt)
            {
                yield return Apply();yield return CaptureFrame("Motion/"+Context.ActiveMap.GenerationSeed+"-"+samples.ToString("D6")+".png");
                captureAt=(int)Used()+12;
            }
        }
        if(!SameInput(action.MoveInput,Vector2.zero)&&Used()<limit)yield return Apply();
        if(samplingFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(samplingFailure).Throw();
        Require(arrived,"ActualInputTargetNotReached:"+trial+":"+target+":actual="+body.position);
        }
        finally {StopCoroutine(observer);}
    }
    private IEnumerator MoveAlongLink(MapNavigationLink link,bool capture,Vector2? finalOnward=null)
    {
        for(int index=1;index<link.Points.Count;index++)
            yield return MoveBodyTo(link.Points[index],capture,index+1<link.Points.Count?link.Points[index+1]:finalOnward);
    }
    public IEnumerator RunRecordedHandoffForTests()
    {
        output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");System.IO.Directory.CreateDirectory(output);
        session=GameSession.Instance.TowerRun;map=FindObjectOfType<MapGenerationController>(true);
        evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase="SEPARATE_RECORDED_HANDOFF_REGRESSION",slotRoot=TowerRunRuntimeEnvironment.SlotRoot};
        autoDefense=true;BindInput();BindPlayer();trial="Recorded-7727-Handoff";
        var motion=body.GetComponent<EcologyMotionBody>();var colliders=FindObjectsOfType<Collider2D>(true).ToDictionary(c=>c.GetInstanceID());
        var nativeRows=new List<string> {"step,physicsTime,phase,kind,bodyX,bodyY,targetX,targetY,colliderId,colliderName,layer,normalX,normalY,separation,distance,health,controlLocked,lease,motionFault,trial,generation,buildId"};
        bool diagnosticTruncated=false;Vector2 selected=body.position;
        void NativeRow(long step,string stage,EcologyMotionKind kind,Vector2 target,int colliderId,Vector2 normal,float separation,float distance=0f)
        {
            if(nativeRows.Count>=40000) {diagnosticTruncated=true;return;}
            colliders.TryGetValue(colliderId,out var contact);
            nativeRows.Add(string.Join(",",step,F(Time.fixedTime),stage,kind,F(body.position.x),F(body.position.y),F(target.x),F(target.y),colliderId,contact==null?"":contact.name.Replace(",","_"),contact==null?-1:contact.gameObject.layer,F(normal.x),F(normal.y),F(separation),F(distance),F(body.GetComponent<PlayerDamageController>().CurrentHealth),action.IsControlLocked,body.GetComponent<EcologyCombatBinding>().Lease,body.GetComponent<EcologyCombatBinding>().MotionFaultRevision,trial,Context.ActiveGenerationId,MapHostBuildStamp.BuildId));
        }
        void ObserveNative(EcologyMotionTrace row)
        {
            if(row.Phase=="Selected")selected=row.Position;
            NativeRow(row.Step,row.Phase,row.Kind,row.Position,row.ContactColliderId,row.ContactNormal,row.ContactSeparation);
            if(row.Phase!="CastAllowed"||(row.Position-body.position).sqrMagnitude>.000001f||(selected-body.position).sqrMagnitude<.0001f)return;
            var hurt=body.GetComponent<PlayerHurtController>();var filter=new ContactFilter2D();filter.SetLayerMask(hurt.MovementBlockingLayers);filter.useTriggers=false;
            var hits=new RaycastHit2D[256];Vector2 delta=selected-body.position;int count=foot.Cast(delta.normalized,filter,hits,delta.magnitude+hurt.MovementCollisionSkin,true);
            if(count>=hits.Length)diagnosticTruncated=true;
            for(int i=0;i<count;i++)NativeRow(row.Step,"INDEPENDENT_NATIVE_CAST",row.Kind,selected,hits[i].collider==null?0:hits[i].collider.GetInstanceID(),hits[i].normal,0f,hits[i].distance);
        }
        var stack=new Stack<IEnumerator>();stack.Push(RunRecordedHandoffCore());Exception firstFailure=null;
        var secondaryFailures=new List<string>();bool completed=false;
        void Retain(Exception error)
        {if(firstFailure==null)firstFailure=error;else secondaryFailures.Add(error.ToString());}
        try
        {
            try {BeginDefenseObservation();ExportMap(7727);motion.OnTrace+=ObserveNative;}catch(Exception error) {Retain(error);}
            while(stack.Count>0&&firstFailure==null)
            {
                bool moved=false;object current=null;
                try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error) {Retain(error);break;}
                if(!moved)
                {
                    try {(stack.Pop() as IDisposable)?.Dispose();}catch(Exception error) {Retain(error);break;}
                    continue;
                }
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            completed=firstFailure==null&&stack.Count==0;
        }
        finally
        {
            while(stack.Count>0)
            {try {(stack.Pop() as IDisposable)?.Dispose();}catch(Exception error) {Retain(error);}}
            try {motion.OnTrace-=ObserveNative;}catch(Exception error) {Retain(error);}
            try {System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"recorded-handoff-native.csv"),nativeRows);}catch(Exception error) {Retain(error);}
            try {CleanupInput();}catch(Exception error) {Retain(error);}
            evidence.failure=firstFailure?.ToString();
            if(firstFailure!=null||!completed)evidence.status="INCOMPLETE_SEE_XML_FAILURE";
            try {Persist();}catch(Exception error) {Retain(error);}
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(output,"recorded-handoff-lifecycle.json"),
                    JsonUtility.ToJson(new HandoffLifecycle {completed=completed&&firstFailure==null,firstFailure=firstFailure?.ToString(),secondaryFailures=secondaryFailures.ToArray(),nativeRows=nativeRows.Count-1,diagnosticTruncated=diagnosticTruncated},true));
            }
            catch(Exception error) {Retain(error);}
        }
        if(firstFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
    }
    [Serializable] private sealed class HandoffLifecycle
    {public bool completed,diagnosticTruncated;public int nativeRows;public string firstFailure;public string[] secondaryFailures;public string nativeDistanceSemantics="distance is measured only for INDEPENDENT_NATIVE_CAST; other stages use 0 for not sampled. separation is the original contact separation; cast rows use 0 for not applicable.";}
    private IEnumerator RunRecordedHandoffCore()
    {
        var geometry=NavigationGeometry();Vector2 target=new Vector2(28.8396026f,9.65f),onward=new Vector2(27.4896026f,8.30f),recorded=new Vector2(28.8582554f,9.785552f);
        var ramp=MapVNextStairGeometry.Derive(Context.ActiveMap,map.TilemapRenderer.Coordinates).Single(r=>r.Id=="H0-H1-West-27-7");
        var graph=geometry.BuildNavigationGraph(foot,0);var link=graph.FindLink(Node(map.TilemapRenderer.Coordinates.WorldToCell(ramp.Entry)),Node(map.TilemapRenderer.Coordinates.WorldToCell(ramp.Exit)));
        Require(link!=null&&link.Points.Any(p=>Vector2.Distance(p,target)<.0001f)&&link.Points.Any(p=>Vector2.Distance(p,onward)<.0001f),"RecordedOriginalLinkNoLongerMatches");
        Require(Vector2.Distance(recorded,target)<.22f&&!CanHandoff(geometry,recorded,target,onward),"RecordedUnsafePoseMustFailForwardHandoffDespiteLegacyDistance");
        Require(CanHandoff(geometry,target,target,onward),"NominalOriginalLinkMustStillQualify");
        yield return SetupFoot(recorded+FootPosition-body.position,"Separate-Recorded-Handoff-Only-Not-Spawn-Walk");yield return new WaitForFixedUpdate();yield return null;
        long lease=body.GetComponent<EcologyCombatBinding>().Lease;Vector2 before=FootPosition;Feed(Key.D,Key.W);
        for(int wait=0;wait<16&&!EffectiveAcknowledged();wait++) {yield return null;yield return new WaitForFixedUpdate();Sample(before);before=FootPosition;}
        Require(EffectiveAcknowledged()&&SameInput(action.MoveInput,new Vector2(1,1).normalized),"RecordedDiagonalInputWasNotActuallyExercised");
        Feed(Key.D);Require(!EffectiveAcknowledged()&&SameInput(action.MoveInput,new Vector2(1,1).normalized),"QueuedDAndEffectiveDiagonalMismatchWasNotExercised");
        yield return MoveBodyTo(target,false,onward);
        Vector2 settled=body.position;Require(EffectiveAcknowledged()&&SameInput(action.MoveInput,Vector2.zero)&&CanHandoff(geometry,settled,target,onward),"ActualCorrectedHandoffMustBeReleasedAndForwardClear");
        trial="Recorded-Down-To-Middle";yield return MoveBodyTo(onward,false,link.Points[1]);
        trial="Recorded-Down-To-Entry";yield return MoveBodyTo(link.Points[1],false);
        yield return DrainOriginalPositiveAttack();
        Require(Context.ActiveMap.GetElevation(map.TilemapRenderer.Coordinates.WorldToCell(FootPosition))==0,"ActualRecordedDownhillDidNotReachLowerMapDataLevel");
        Require(body.GetComponent<EcologyCombatBinding>().Lease==lease&&body.GetComponent<EcologyCombatBinding>().MotionFaultRevision==0,"RecordedCorrectionCannotResetLeaseOrFault");
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"recorded-handoff-regression.json"),JsonUtility.ToJson(new HandoffRegression {buildId=MapHostBuildStamp.BuildId,generation=Context.ActiveGenerationId.ToString(),capturedUnsafe=recorded,target=target,onward=onward,actualSettled=settled,queuedEffectiveMismatchExercised=true,originalDistancePassed=true,unsafeForwardRejected=true,actualForwardQualified=true,actualLowerHeight=0,lease=lease},true));
        evidence.status="REGRESSION_ONLY_NOT_FULL_SPAWN_WALK";Persist();
    }
    [Serializable] private sealed class HandoffRegression
    {public string buildId,generation;public Vector2 capturedUnsafe,target,onward,actualSettled;public bool queuedEffectiveMismatchExercised,originalDistancePassed,unsafeForwardRejected,actualForwardQualified;public int actualLowerHeight;public long lease;}
    private MapCandidateCollisionGeometry NavigationGeometry()
    {
        var solids=new List<MapPlannedSolidGeometry>();
        foreach(var entry in Context.ActiveRegistry.Entries)
        {
            if(entry.Role!=MerchantPlacementService.MerchantRole&&entry.Role!=PlantPlacementService.PlantRole&&entry.Role!=DestructiblePlacementService.DestructibleRole&&entry.Role!=AnimalPlacementService.AnimalRole)continue;
            Transform actor=(entry.Instance as Component)?.transform??(entry.Instance as GameObject)?.transform;
            if(actor!=null&&actor.gameObject.activeInHierarchy)solids.Add(MapPlannedSolidGeometry.CaptureLive(entry.LogicalObjectId,actor));
        }
        return Context.ActiveSpatialGeometry.WithPlannedSolids(solids);
    }
    private IEnumerator NavigateTo(int target,Vector2? finalOnward=null)
    {
        var geometry=NavigationGeometry();var graph=geometry.BuildNavigationGraph(foot,0);var field=graph.BuildReturnField(new[]{target});
        int node=Enumerable.Range(0,graph.NodeCount).Where(n=>graph.IsLegal(n)&&field.Distances[n]>=0&&Vector2.Distance(geometry.NodeWorldPosition(n),body.position)<1.7f&&geometry.IsTranslationClear(foot,body.position,geometry.NodeWorldPosition(n),0))
            .OrderBy(n=>Vector2.Distance(geometry.NodeWorldPosition(n),body.position)).DefaultIfEmpty(-1).First();
        Require(node>=0,"NoActualLocalConnectorToFormalNavigation");yield return MoveBodyTo(geometry.NodeWorldPosition(node),false,node==target?finalOnward:null);
        for(int steps=0;node!=target&&steps<graph.NodeCount;steps++)
        {
            int next=field.Next[node];Require(next>=0,"FormalInputRouteHasNoNextNode");var link=graph.FindLink(node,next);
            if(link!=null)yield return MoveAlongLink(link,false,next==target?finalOnward:null);
            else yield return MoveBodyTo(geometry.NodeWorldPosition(next),false,next==target?finalOnward:null);
            node=next;
        }
        Require(node==target,"FormalInputRouteExceededFiniteNodeBudget");
    }
    private int Node(Vector2Int cell)=>(cell.y-Context.ActiveMap.Origin.y)*Context.ActiveMap.Width+cell.x-Context.ActiveMap.Origin.x;
    private IEnumerator WalkAllStairs(int seed,bool capture)
    {
        var ramps=MapVNextStairGeometry.Derive(Context.ActiveMap,map.TilemapRenderer.Coordinates);
        if(autoDefense)BeginDefenseObservation();
        try
        {
        foreach(var ramp in ramps)
        {
            int from=Node(map.TilemapRenderer.Coordinates.WorldToCell(ramp.Entry)),to=Node(map.TilemapRenderer.Coordinates.WorldToCell(ramp.Exit));
            var graph=NavigationGeometry().BuildNavigationGraph(foot,0);var link=graph.FindLink(from,to);Require(link!=null,"ActualFootStairLinkMissing");
            trial="Approach-"+ramp.Id;yield return NavigateTo(from,link.Points[1]);int beforeDual=dualSamples;
            trial="Stair-Up-"+ramp.Id;yield return MoveAlongLink(link,capture,link.Points[link.Points.Count-2]);
            Require(Context.ActiveMap.GetElevation(map.TilemapRenderer.Coordinates.WorldToCell(FootPosition))==ramp.UpperElevation,"ActualUpstairsHeightMismatch");
            trial="Stair-Down-"+ramp.Id;yield return MoveAlongLink(link.Reverse(),capture);
            Require(Context.ActiveMap.GetElevation(map.TilemapRenderer.Coordinates.WorldToCell(FootPosition))==ramp.LowerElevation,"ActualDownstairsHeightMismatch");
            Require(dualSamples-beforeDual>20,"NoEffectiveDiagonalInputEvidence");evidence.checks.Add("Continuous actual-input stair both ways seed="+seed+" "+ramp.Id+" dual="+(dualSamples-beforeDual));
        }
        trial="Continuous-Return-To-Spawn";yield return NavigateTo(Node(Context.ActiveMap.SpawnCell));
        evidence.checks.Add("Continuous real input from spawn through all declared stairs and back seed="+seed);
        if(autoDefense)yield return DrainOriginalPositiveAttack();
        }
        finally {StopDefenseObservation();}
        foreach(var ramp in ramps)
        foreach(Vector2 start in new[]{ramp.Entry,ramp.Point(-1.3f,.5f)})
        {
            yield return SetupFoot(start,"Separate-Horizontal-Negative");trial="Horizontal-Negative-"+ramp.Id;int maximum=ramp.LowerElevation;
            for(int tick=0;tick<64;tick++)
            {
                Vector2 before=FootPosition;Feed(ramp.West?Key.D:Key.A);yield return new WaitForFixedUpdate();Sample(before);
                maximum=Math.Max(maximum,Context.ActiveMap.GetElevation(map.TilemapRenderer.Coordinates.WorldToCell(FootPosition)));
                if(capture&&tick%16==0)yield return CaptureFrame("Negatives/"+seed+"-"+samples.ToString("D6")+".png");
            }
            yield return Release();Require(maximum==ramp.LowerElevation&&Vector2.Dot(FootPosition-ramp.Corner,ramp.Direction)<ramp.Width,"PureHorizontalInputCrossedLevel");
            evidence.checks.Add("A/D-only cross-level blocked seed="+seed+" "+ramp.Id);
        }
        Persist();
    }
    private IEnumerator SetupFoot(Vector2 destination,string purpose)
    {
        yield return Release();Vector2 root=body.position+destination-FootPosition;
        Require(MapSpatialPlacement.CanPlace(Context,Context.ActiveGenerationId.Value,body.transform,root,out string reason),"IllegalExplicitTrialStart:"+reason);
        trace.Add("# SETUP_ONLY_NOT_MOVEMENT,"+purpose+","+Context.ActiveGenerationId+","+F(body.position.x)+","+F(body.position.y)+","+F(root.x)+","+F(root.y));
        body.position=root;body.velocity=Vector2.zero;Physics2D.SyncTransforms();
        // Explicit lawful fixture relocation is outside every positive route. Keep the current
        // generation/barrier/lease, and rebase only the native observer's starting pose.
        var hurt=body.GetComponent<PlayerHurtController>();body.GetComponent<EcologyMotionBody>().Configure(foot,hurt.MovementBlockingLayers,hurt.MovementCollisionSkin);
        Require(body.GetComponent<EcologyCombatBinding>().MotionFaultRevision==0,"TrialSetupCannotErasePriorMotionFaults");
    }
}
