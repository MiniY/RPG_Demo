using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Bounded evidence owned by the I1 acceptance runner; never a gameplay state owner.
public sealed partial class MapVNextI1Acceptance
{
    private const int ResidentRingCapacity=64;
    private const BindingFlags ResidentFields=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] private sealed class ResidentRecentEvent
    {
        public string label,residentId,capturedGeneration,actualGeneration,phase,kind,navigationFailure,worldTimeRaw,fixedTimeRaw,lastWorldTimeRaw;
        public int instanceId,frame,homeRegion,footId;public long sequence,nativeStep,capturedLease,lease,combatGeneration,combatResidentId,motionFault,submittedStep,observedStep,nativeLease,nativeCombatGeneration,nativeCombatResidentId;
        public bool identityMatches,nativeProvided,navigationFailureIsNull,scopeFaulted,active,enabled,retired,defeated,scopeOpen,contactDuringStep,lastWorldTimeFinite,hasPermit,returnLatched,excursionFault,defeatEvent;
        public float health,rotation,submittedRotation,fixedDeltaTime;public double elapsed,departedAt;
        public Vector2 nativePosition,position,velocity,submittedStart,submittedTarget,previousPosition;
    }
    private sealed class ResidentObservationOwner
    {
        public EcologyResidentBinding Actor;public EcologyMotionBody Motion;public EcologyCombatBinding Combat;
        public Rigidbody2D Body;public Collider2D Foot;public BaseDamageable Damage;
        public string ResidentId;public Guid Generation;public long Lease,CombatGeneration,CombatResidentId;
        public int InstanceId,Next,Count;public long Received,NativeReceived,DefeatEvents;
        public readonly ResidentRecentEvent[] Ring=new ResidentRecentEvent[ResidentRingCapacity];
        public Action<EcologyMotionTrace> Trace;public Action<BaseDamageable> Defeated;
    }
    [Serializable] private sealed class ResidentOwnerIndex
    {public string residentId,generation;public int instanceId,retained,next;public long lease,received,nativeReceived,defeatEvents,overwritten;}
    [Serializable] private sealed class ResidentGroupIndex
    {
        public string buildId,generation,scope,firstFaultResident,firstFaultPhase;public int seed,capacity;public bool frozen,detached;
        public string[] expectedResidents,diagnosticErrors;public ResidentOwnerIndex[] owners;
    }
    [Serializable] private sealed class ResidentFaultReplay
    {
        public string scope="PURE_HOME_QUERY_REPLAY_AFTER_RAW_RING_FREEZE_NOT_PROOF_OF_PRODUCER_BRANCH",error,homeGeneration,footprintIdentity;
        public bool performed,fullHome,measured;public int homeSteps=-1;public Vector2 waypoint;public NativeHomeObservation nativeHome;
        public ResidentRecentEvent firstFault;
    }
    private List<ResidentObservationOwner> residentGroup;
    private readonly List<string> residentGroupErrors=new List<string>();
    private string[] residentGroupExpected;private Guid residentGroupGeneration;private int residentGroupSeed;
    private bool residentGroupFrozen,residentGroupDetached,residentGroupFlushAttempted;
    private ResidentFaultReplay residentGroupFault;
    private static object ResidentField(object owner,Type type,string name)=>type.GetField(name,ResidentFields).GetValue(owner);
    private void RetainResidentObservationError(Exception error,string stage)
    {if(residentGroupErrors.Count<16)residentGroupErrors.Add(stage+": "+error);}
    private ResidentRecentEvent CaptureResidentEvent(ResidentObservationOwner owner,string label,EcologyMotionTrace native=null,bool defeatEvent=false)
    {
        var actor=owner.Actor;var combat=owner.Combat;var motion=owner.Motion;var excursion=actor.Excursion;
        double last=excursion==null?double.NaN:(double)ResidentField(excursion,typeof(EcologyExcursionState),"lastWorldTime");
        var row=new ResidentRecentEvent {label=label,residentId=owner.ResidentId,capturedGeneration=owner.Generation.ToString(),actualGeneration=actor.GenerationId.ToString(),instanceId=owner.InstanceId,
            sequence=++owner.Received,nativeLease=native?.Lease??-1,nativeCombatGeneration=native?.GenerationId??-1,nativeCombatResidentId=native?.ResidentId??-1,nativeProvided=native!=null,nativeStep=native?.Step??-1,phase=native?.Phase??label,kind=native==null?"NotProvided":native.Kind.ToString(),
            frame=Time.frameCount,worldTimeRaw=Time.timeAsDouble.ToString("R",CultureInfo.InvariantCulture),fixedTimeRaw=Time.fixedTimeAsDouble.ToString("R",CultureInfo.InvariantCulture),
            fixedDeltaTime=Time.fixedDeltaTime,lastWorldTimeRaw=last.ToString("R",CultureInfo.InvariantCulture),lastWorldTimeFinite=!double.IsNaN(last)&&!double.IsInfinity(last),
            capturedLease=owner.Lease,lease=combat.Lease,combatGeneration=combat.GenerationId,combatResidentId=combat.ResidentId,motionFault=combat.MotionFaultRevision,
            identityMatches=actor.GenerationId==owner.Generation&&actor.ResidentId==owner.ResidentId&&combat.Lease==owner.Lease&&combat.GenerationId==owner.CombatGeneration&&combat.ResidentId==owner.CombatResidentId,
            nativePosition=native?.Position??Vector2.zero,position=owner.Body.position,velocity=owner.Body.velocity,rotation=owner.Body.rotation,
            scopeFaulted=actor.ScopeFaulted,navigationFailure=actor.NavigationFailure,navigationFailureIsNull=actor.NavigationFailure==null,
            active=actor.gameObject.activeInHierarchy,enabled=actor.isActiveAndEnabled,retired=(bool)ResidentField(actor,typeof(EcologyResidentBinding),"retired"),
            defeated=owner.Damage.IsDefeated,health=owner.Damage.CurrentHealth,scopeOpen=combat.Barrier!=null&&combat.Barrier.IsScopeOpen,defeatEvent=defeatEvent,homeRegion=actor.HomeRegionId,footId=owner.Foot.GetInstanceID(),
            submittedStep=(long)ResidentField(motion,typeof(EcologyMotionBody),"submittedStep"),observedStep=(long)ResidentField(motion,typeof(EcologyMotionBody),"observedStep"),
            submittedStart=(Vector2)ResidentField(motion,typeof(EcologyMotionBody),"submittedStart"),submittedTarget=(Vector2)ResidentField(motion,typeof(EcologyMotionBody),"submittedTarget"),
            previousPosition=(Vector2)ResidentField(motion,typeof(EcologyMotionBody),"previousPosition"),submittedRotation=(float)ResidentField(motion,typeof(EcologyMotionBody),"submittedRotation"),
            contactDuringStep=(bool)ResidentField(motion,typeof(EcologyMotionBody),"contactDuringStep"),hasPermit=excursion!=null&&excursion.HasPermit,returnLatched=excursion!=null&&excursion.ReturnLatched,
            excursionFault=excursion!=null&&excursion.NavigationFault,elapsed=excursion?.Elapsed??0d,departedAt=excursion?.DepartedAt??0d};
        if(native!=null)owner.NativeReceived++;
        if(defeatEvent)owner.DefeatEvents++;
        owner.Ring[owner.Next]=row;owner.Next=(owner.Next+1)%ResidentRingCapacity;owner.Count=Math.Min(ResidentRingCapacity,owner.Count+1);
        return row;
    }
    private void ObserveResidentEvent(ResidentObservationOwner owner,string label,EcologyMotionTrace native=null,bool defeatEvent=false)
    {
        if(residentGroupFrozen||residentGroupDetached)return;
        try
        {
            var row=CaptureResidentEvent(owner,label,native,defeatEvent);
            // Domain/cast rejection alone is not a resident ScopeFault transition.
            bool actualFault=row.scopeFaulted||native!=null&&(native.Phase=="ResidentNavigationFault"||native.Phase.StartsWith("ResidentObservationFault:",StringComparison.Ordinal));
            if(!actualFault)return;
            residentGroupFrozen=true;residentGroupFault=new ResidentFaultReplay {firstFault=row};
            foreach(var other in residentGroup)if(other!=owner)
                try {CaptureResidentEvent(other,"FirstFaultPassiveSnapshot");}catch(Exception error) {RetainResidentObservationError(error,"freeze "+other.ResidentId);}
            // All raw rings are frozen before the one fault owner's pure geometry replay.
            if(!row.identityMatches) {residentGroupFault.error="Captured identity no longer matches; no foreign Home queried.";return;}
            try
            {
                var home=owner.Actor.Home;residentGroupFault.homeGeneration=home.HomeGraph.GenerationId.ToString();residentGroupFault.footprintIdentity=home.HomeGraph.FootprintIdentity;
                residentGroupFault.nativeHome=InspectNativeHomeObservation(owner.Actor,owner.Foot,row.position,native?.Step??owner.Combat.Barrier?.CurrentStep??-1);
                residentGroupFault.fullHome=home.ContainsFullFoot(owner.Foot,row.position);
                residentGroupFault.measured=home.TryMeasureHomeDistance(owner.Foot,row.position,out residentGroupFault.homeSteps,out residentGroupFault.waypoint);
                residentGroupFault.performed=true;
            }
            catch(Exception error) {residentGroupFault.error=error.ToString();}
        }
        catch(Exception error) {RetainResidentObservationError(error,"observe "+owner.ResidentId+" "+label);}
    }
    private void BeginResidentGroupObservation(int seed)
    {
        if(residentGroup!=null)throw new InvalidOperationException("Resident group observation already owned.");
        residentGroup=new List<ResidentObservationOwner>();residentGroupSeed=seed;residentGroupGeneration=Context.ActiveGenerationId??throw new InvalidOperationException("Resident observation requires an explicit active Generation.");
        residentGroupExpected=Context.ActivePlacementPlan.Ecology.Residents.Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var actors=FindObjectsOfType<EcologyResidentBinding>().Where(x=>x.GenerationId==residentGroupGeneration).OrderBy(x=>x.ResidentId,StringComparer.Ordinal).ToArray();
        if(!actors.Select(x=>x.ResidentId).SequenceEqual(residentGroupExpected))throw new InvalidOperationException("Resident observation set differs from the exact active placement plan.");
        foreach(var actor in actors)
        {
            var combat=actor.GetComponent<EcologyCombatBinding>();
            var owner=new ResidentObservationOwner {Actor=actor,Motion=actor.GetComponent<EcologyMotionBody>(),Combat=combat,Body=actor.GetComponent<Rigidbody2D>(),
                Foot=(Collider2D)ResidentField(actor,typeof(EcologyResidentBinding),"movementFoot"),Damage=actor.GetComponent<BaseDamageable>(),ResidentId=actor.ResidentId,Generation=actor.GenerationId,
                Lease=combat.Lease,CombatGeneration=combat.GenerationId,CombatResidentId=combat.ResidentId,InstanceId=actor.GetInstanceID()};
            residentGroup.Add(owner); // Own partial setup before either subscription.
            if(owner.Motion==null||owner.Body==null||owner.Foot==null||owner.Damage==null)throw new InvalidOperationException("Missing native resident observation owner: "+owner.ResidentId);
            owner.Trace=item=>ObserveResidentEvent(owner,"OriginalNative",item);
            owner.Defeated=target=>{if(ReferenceEquals(target,owner.Damage))ObserveResidentEvent(owner,"OriginalOnDefeated",defeatEvent:true);};
            owner.Motion.OnTrace+=owner.Trace;owner.Damage.OnDefeated+=owner.Defeated;
            ObserveResidentEvent(owner,"ObservationBound");
        }
    }
    private void ScanResidentGroupObservation(string label)
    {
        if(residentGroup==null||residentGroupDetached||residentGroupFrozen)return;
        foreach(var owner in residentGroup)
        {
            // Direct property only: never call CanAct, IsLive, IsReady or a scope delegate.
            try {if(owner.Actor.ScopeFaulted)ObserveResidentEvent(owner,label);}
            catch(Exception error) {RetainResidentObservationError(error,"passive scope scan "+owner.ResidentId);}
            if(residentGroupFrozen)break;
        }
    }
    private void StopResidentGroupObservation()
    {
        if(residentGroup==null||residentGroupDetached)return;
        ScanResidentGroupObservation("FinallyPassiveScopeScan");
        var errors=new List<Exception>();
        foreach(var owner in residentGroup)
        {
            try {if(!ReferenceEquals(owner.Motion,null)&&owner.Trace!=null)owner.Motion.OnTrace-=owner.Trace;}catch(Exception error) {errors.Add(error);}
            try {if(!ReferenceEquals(owner.Damage,null)&&owner.Defeated!=null)owner.Damage.OnDefeated-=owner.Defeated;}catch(Exception error) {errors.Add(error);}
        }
        residentGroupDetached=true;
        if(errors.Count>0)throw new AggregateException("Resident group detach failed.",errors);
    }
    private void FlushResidentGroupObservation()
    {
        if(residentGroup==null||residentGroupFlushAttempted)return;
        residentGroupFlushAttempted=true; // No blind append retry after a partial write.
        string directory=Path.Combine(output,"ResidentGroup",residentGroupSeed+"-"+residentGroupGeneration);
        var index=new ResidentGroupIndex {buildId=MapHostBuildStamp.BuildId,generation=residentGroupGeneration.ToString(),seed=residentGroupSeed,capacity=ResidentRingCapacity,
            scope="LAST_64_EVENTS_PER_OWNER_NOT_64_PHYSICS_STEPS_OR_CONTINUOUS_HISTORY; FIRST_FAULT_FREEZE_HAS_PER_OWNER_NATIVE_STEPS",frozen=residentGroupFrozen,detached=residentGroupDetached,
            firstFaultResident=residentGroupFault?.firstFault?.residentId,firstFaultPhase=residentGroupFault?.firstFault?.phase,expectedResidents=residentGroupExpected,
            diagnosticErrors=residentGroupErrors.ToArray(),owners=residentGroup.Select(x=>new ResidentOwnerIndex {residentId=x.ResidentId,generation=x.Generation.ToString(),instanceId=x.InstanceId,lease=x.Lease,
                retained=x.Count,next=x.Next,received=x.Received,nativeReceived=x.NativeReceived,defeatEvents=x.DefeatEvents,overwritten=Math.Max(0,x.Received-x.Count)}).ToArray()};
        var errors=new List<Exception>();
        void Write(string name,Action<StreamWriter> emit)
        {
            try {Directory.CreateDirectory(directory);using(var stream=new FileStream(Path.Combine(directory,name),FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(stream))emit(writer);}
            catch(Exception error) {errors.Add(error);}
        }
        Write("index.json",writer=>writer.Write(JsonUtility.ToJson(index,true)));
        Write("recent-native.jsonl",writer=>{foreach(var owner in residentGroup)for(int i=0;i<owner.Count;i++)writer.WriteLine(JsonUtility.ToJson(owner.Ring[(owner.Next-owner.Count+i+ResidentRingCapacity)%ResidentRingCapacity]));});
        if(residentGroupFault!=null)Write("first-fault-query.json",writer=>writer.Write(JsonUtility.ToJson(residentGroupFault,true)));
        if(residentGroupErrors.Count>0)errors.Add(new InvalidOperationException("Resident observation errors: "+string.Join(";",residentGroupErrors)));
        if(errors.Count>0)throw new AggregateException("Resident evidence write unconfirmed; bounded rings retained in memory and no blind retry permitted.",errors);
    }
}
