using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [Serializable] private sealed class HomeBoundarySample
    {public Vector2 position,waypoint,footMin,footMax;public bool completeHome,measured;public int steps;}
    [Serializable] private sealed class HomeBoundaryEvidence
    {public string buildId,generation,mapDigest,ecologyIdentity,resident,instance;public int homeRegion;public int[] surfaceNodes,homeNodes;public Vector2[] surfaceCentres;public HomeBoundarySample rejected;public List<HomeBoundarySample> samples=new List<HomeBoundarySample>();}
    [UnityTest] public IEnumerator ActualNativeOutsideHomeRequiresPositiveDistanceEvenNearGridCentre()
    {
        yield return Load("Home-boundary-recorded-a17");Call("BeginNewRunWithSeed",21193);Ready(true);yield return null;
        var context=controller.Orchestrator.Context;
        var resident=UnityEngine.Object.FindObjectsOfType<EcologyResidentBinding>().Single(b=>b.GenerationId==context.ActiveGenerationId&&b.ResidentId=="ecology.resident.1003.003");
        var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(resident);
        var region=context.ActivePlacementPlan.Ecology.Regions.Single(r=>r.Id==resident.HomeRegionId);
        var record=new HomeBoundaryEvidence {buildId=MapHostBuildStamp.BuildId,generation=context.ActiveGenerationId.ToString(),mapDigest=MapVNext.MapVNextRandomLayout.SemanticHash(context.ActiveMap),ecologyIdentity=JsonUtility.ToJson(Entrance().generation.ecology),resident=resident.ResidentId,instance=resident.name,homeRegion=region.Id,surfaceNodes=region.Nodes.ToArray(),homeNodes=resident.Home.HomeNodes.ToArray(),surfaceCentres=region.Nodes.Select(resident.Home.NodeWorldPosition).ToArray()};
        Vector2 initial=resident.transform.position;
        var positions=new[]{new Vector2(4.52769756f,50.5210838f),new Vector2(4.5f,50.5f),new Vector2(4.52f,50.52f),new Vector2(4.53f,50.51f),new Vector2(4.535f,50.5f),new Vector2(4.54f,50.5f)};
        foreach(Vector2 point in positions)
        {
            var row=new HomeBoundarySample {position=point,completeHome=resident.Home.ContainsFullFoot(nativeFoot,point),footMin=point+(Vector2)nativeFoot.bounds.min-initial,footMax=point+(Vector2)nativeFoot.bounds.max-initial};
            row.measured=resident.Home.TryMeasureHomeDistance(nativeFoot,point,out row.steps,out row.waypoint);record.samples.Add(row);
        }
        record.rejected=new HomeBoundarySample {position=new Vector2(-1000f,-1000f)};
        record.rejected.measured=resident.Home.TryMeasureHomeDistance(nativeFoot,record.rejected.position,out record.rejected.steps,out record.rejected.waypoint);
        File.WriteAllText(Path.Combine(Output,"actual-native-home-boundary.json"),JsonUtility.ToJson(record,true));
        Assert.That(resident.transform.position,Is.EqualTo((Vector3)initial),"Queries must not move the actual resident.");
        Assert.That(record.rejected.measured,Is.False,"An invalid connector must remain rejected.");
        Assert.That(record.rejected.steps,Is.EqualTo(-1),"Failure must not be clamped into a positive result.");
        Assert.That(record.samples[0].completeHome,Is.False,"The recorded Windows pose must still expose the whole-foot boundary.");
        Assert.That(record.samples.All(r=>r.measured),Is.True,"Every diagnostic position must be physically navigable.");
        Assert.That(record.samples.Where(r=>r.completeHome).All(r=>r.steps==0),Is.True,"Only a complete original Home footprint has zero distance.");
        Assert.That(record.samples.Where(r=>!r.completeHome).All(r=>r.steps>0),Is.True,"An outside full foot needs a positive final connector even within 0.035 of the centre.");
    }
    [UnityTest] public IEnumerator RecordedInHomeAttackCannotQualifyAsPositiveReturnTrial()
    {
        yield return Load("Pursuit-recorded-a21");Call("BeginNewRunWithSeed",21193);Ready(true);yield return null;
        var context=controller.Orchestrator.Context;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var predict=type.GetMethod("PredictPursuitTrial",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Component Named(GameObject actor,string name)=>actor.GetComponents<Component>().First(c=>c!=null&&Inherits(c.GetType(),name));
        bool Inherits(Type current,string name) {for(var t=current;t!=null;t=t.BaseType)if(t.Name==name)return true;return false;}
        object Property(Component actor,string name)=>actor.GetType().GetProperty(name).GetValue(actor);
        var target=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(c=>c.GetType().Name=="PlayerAction").transform;
        var bindings=UnityEngine.Object.FindObjectsOfType<EcologyResidentBinding>().Where(b=>b.GenerationId==context.ActiveGenerationId).ToArray();
        string StateOf(EcologyResidentBinding b)
        {
            var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(b);
            var combat=b.GetComponent<EcologyCombatBinding>();
            return b.transform.position.ToString("R")+"|"+nativeFoot.attachedRigidbody.position.ToString("R")+"|"+Property(Named(b.gameObject,"MonsterAIController"),"CurrentStateType")+"|"+Property(Named(b.gameObject,"BaseDamageable"),"CurrentHealth")+"|"+combat.Lease+"|"+combat.MotionFaultRevision+"|"+b.ScopeFaulted+"|"+b.Excursion.HasPermit+"|"+b.Excursion.ReturnLatched;
        }
        var before=bindings.ToDictionary(b=>b,StateOf);Vector3 playerBefore=target.position;
        object hpBefore=Property(Named(target.gameObject,"PlayerDamageController"),"CurrentHealth");long leaseBefore=target.GetComponent<EcologyCombatBinding>().Lease;
        object Query(string id,Vector2 start,Vector2 destination)
        {
            var resident=bindings.Single(b=>b.ResidentId==id);
            var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(resident);
            return predict.Invoke(null,new object[]{resident,nativeFoot,target,start,destination});
        }
        var red=bindings.Single(b=>b.ResidentId=="ecology.resident.1000.001");
        var redFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(red);
        Assert.That(red.Home.ContainsFullFoot(redFoot,new Vector2(42.8116112f,45.3092155f)),Is.True,"Recorded Windows attack stop remains a lawful full Home pose.");
        Assert.That(red.Home.HomeNodes.Min(n=>Vector2.Distance(red.Home.NodeWorldPosition(n),new Vector2(42.5f,43.5f))),Is.GreaterThan((float)Property(Named(red.gameObject,"MonsterAttackController"),"AttackRange")+1f),"Reproduce the insufficient original centre-separation criterion.");
        Assert.That(red.Home.ContainsFullFoot(redFoot,new Vector2(43.53f,46.50f)),Is.True,"The recorded Red prediction starts from a lawful full Home pose.");
        object negative=Query(red.ResidentId,new Vector2(43.53f,46.50f),new Vector2(42.5f,43.5f));
        object positive=Query("ecology.resident.1003.003",new Vector2(2.5f,50.79f),new Vector2(5.5f,52.5f));
        File.WriteAllText(Path.Combine(Output,"pursuit-prediction-regression.json"),"{\"negative\":"+JsonUtility.ToJson(negative)+",\"positive\":"+JsonUtility.ToJson(positive)+"}");
        Assert.That((bool)negative.GetType().GetField("qualifies").GetValue(negative),Is.False,"An original in-Home attack stop must not qualify as a positive return trial.");
        Assert.That((string)negative.GetType().GetField("reason").GetValue(negative),Is.EqualTo("OriginalAttackReachInsideFullHome").Or.EqualTo("SamplingEnvelopeTouchesInHomeAttackReach"),"Reject because the original attack envelope is reached before Home exit, not another failed premise.");
        Assert.That((bool)negative.GetType().GetField("fullHome").GetValue(negative),Is.True);
        float attackRange=(float)negative.GetType().GetField("originalAttackRange").GetValue(negative);
        float sampleLength=(float)negative.GetType().GetField("sampleLength").GetValue(negative);
        Assert.That((float)negative.GetType().GetField("closestDistance").GetValue(negative),Is.LessThanOrEqualTo(attackRange+sampleLength));
        Assert.That((bool)positive.GetType().GetField("qualifies").GetValue(positive),Is.True,"The recorded valid Purple route must remain eligible; prediction is not actual traversal proof.");
        Assert.That(bindings.All(b=>StateOf(b)==before[b]),Is.True,"Read-only prediction must not move/reset actors or change AI, health, lease, faults or excursion state.");
        Assert.That(target.position,Is.EqualTo(playerBefore));Assert.That(Property(Named(target.gameObject,"PlayerDamageController"),"CurrentHealth"),Is.EqualTo(hpBefore));Assert.That(target.GetComponent<EcologyCombatBinding>().Lease,Is.EqualTo(leaseBefore));
    }

    [UnityTest] public IEnumerator NativeTargetPoseMismatchMustRejectBeforeAnyClosestPointQuery()
    {
        yield return Load("Native-target-phase-regression");Call("BeginNewRunWithSeed",21193);Ready(true);yield return null;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var predict=type.GetMethod("PredictPursuitTrial",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var resident=UnityEngine.Object.FindObjectsOfType<EcologyResidentBinding>().Single(b=>b.GenerationId==controller.Orchestrator.Context.ActiveGenerationId&&b.ResidentId=="ecology.resident.1003.003");
        var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(resident);
        var player=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(b=>b.GetType().Name=="PlayerAction");
        var original=player.GetComponent<CapsuleCollider2D>();var target=new GameObject("Separate_QueryOnly_OriginalCapsule_PhaseFixture");
        target.transform.position=new Vector3(-1000,-1000,0);var rb=target.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.interpolation=RigidbodyInterpolation2D.None;
        var capsule=target.AddComponent<CapsuleCollider2D>();capsule.size=original.size;capsule.offset=original.offset;capsule.direction=original.direction;capsule.isTrigger=true;
        Component Named(GameObject actor,string name)=>actor.GetComponents<Component>().First(comp=>comp!=null&&Inherits(comp.GetType(),name));
        bool Inherits(Type typeOf,string name) {for(var t=typeOf;t!=null;t=t.BaseType)if(t.Name==name)return true;return false;}
        object Property(Component actor,string name)=>actor.GetType().GetProperty(name).GetValue(actor);
        string StateSnapshot()
        {
            var combat=resident.GetComponent<EcologyCombatBinding>();var playerCombat=player.GetComponent<EcologyCombatBinding>();
            return resident.transform.position.ToString("R")+"|"+nativeFoot.attachedRigidbody.position.ToString("R")+"|"+Property(Named(resident.gameObject,"MonsterAIController"),"CurrentStateType")+"|"+Property(Named(resident.gameObject,"BaseDamageable"),"CurrentHealth")+"|"+combat.Lease+"|"+combat.MotionFaultRevision+"|"+resident.ScopeFaulted+"|"+resident.Excursion.HasPermit+"|"+resident.Excursion.ReturnLatched+"|"+player.transform.position.ToString("R")+"|"+player.GetComponent<Rigidbody2D>().position.ToString("R")+"|"+Property(Named(player.gameObject,"PlayerDamageController"),"CurrentHealth")+"|"+playerCombat.Lease+"|"+playerCombat.MotionFaultRevision;
        }
        object Query()
        {
            string stateBefore=StateSnapshot();
            Vector3 actorBefore=resident.transform.position,playerBefore=player.transform.position,queryBefore=target.transform.position;
            Vector2 bodyBefore=nativeFoot.attachedRigidbody.position;long lease=resident.GetComponent<EcologyCombatBinding>().Lease;
            var answer=predict.Invoke(null,new object[]{resident,nativeFoot,target.transform,new Vector2(2.5f,50.79f),new Vector2(5.5f,52.5f)});
            Assert.That(resident.transform.position,Is.EqualTo(actorBefore));Assert.That(nativeFoot.attachedRigidbody.position,Is.EqualTo(bodyBefore));Assert.That(player.transform.position,Is.EqualTo(playerBefore));Assert.That(target.transform.position,Is.EqualTo(queryBefore));Assert.That(resident.GetComponent<EcologyCombatBinding>().Lease,Is.EqualTo(lease));
            Assert.That(StateSnapshot(),Is.EqualTo(stateBefore),"Each synchronous query preserves actual actor/player AI, health, lease, fault, excursion and poses; natural observation frames are outside this assertion.");
            return answer;
        }
        object Field(object row,string name)=>row.GetType().GetField(name).GetValue(row);
        string PhaseJson(object row)=>"{\"frame\":"+Time.frameCount+",\"physicsTime\":"+Time.fixedTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"inFixedTimeStep\":"+(Time.inFixedTimeStep?"true":"false")+",\"query\":"+JsonUtility.ToJson(row)+"}";
        try
        {
            yield return new WaitForFixedUpdate();yield return null;
            object baseline=Query();string baselinePhase=PhaseJson(baseline);Assert.That((bool)Field(baseline,"targetPoseCoherent"),Is.True);Assert.That((bool)Field(baseline,"qualifies"),Is.True);
            rb.position=new Vector2(-900,-900);
            Assert.That(Vector2.Distance(rb.position,target.transform.position)>.01f||Vector2.Distance(capsule.bounds.center,target.transform.TransformPoint(capsule.offset))>.01f,Is.True,"Must actually exercise a native/Transform phase mismatch; no unrelated false result counts.");
            object stale=Query();string stalePhase=PhaseJson(stale);Assert.That((string)Field(stale,"reason"),Is.EqualTo("StaleTargetPose"));Assert.That((bool)Field(stale,"qualifies"),Is.False);Assert.That((int)Field(stale,"samples"),Is.Zero);Assert.That((bool)Field(stale,"closestPointQueried"),Is.False);
            yield return new WaitForFixedUpdate();
            object fixedOnly=Query();string fixedPhase=PhaseJson(fixedOnly);
            File.WriteAllText(Path.Combine(Output,"native-target-fixed-only-phase.json"),fixedPhase);
            yield return null;
            object coherent=Query();string coherentPhase=PhaseJson(coherent);
            File.WriteAllText(Path.Combine(Output,"native-target-phase-regression.json"),"{\"baseline\":"+baselinePhase+",\"stale\":"+stalePhase+",\"fixedOnly\":"+fixedPhase+",\"coherent\":"+coherentPhase+"}");
            Assert.That((bool)Field(fixedOnly,"targetPoseCoherent"),Is.True,"Strict Fixed-only phase is measured independently of consumer success; both phase observations are retained if unsupported.");
            Assert.That((bool)Field(coherent,"targetPoseCoherent"),Is.True);Assert.That((bool)Field(coherent,"qualifies"),Is.True,"Natural phase synchronization must retain the original Purple query positive.");
        }
        finally {UnityEngine.Object.Destroy(target);}
    }
    [UnityTest] public IEnumerator FiniteFloorInputsAreCopiedScopedAndCannotFallBackAfterExhaustion()
    {
        yield return Load("Finite-floor-input-policy");Call("BeginNewRunWithSeed",21193);Ready(true);
        var context=controller.Orchestrator.Context;Guid generation=context.ActiveGenerationId.Value;string before=Snapshot();
        var configure=coordinator.GetType().GetMethod("ConfigureNextFloorInputs");
        var factory=coordinator.GetType().GetMethod("CreateFreshGenerationRecord",Private);
        var queueField=coordinator.GetType().GetField("nextFloorInputs",Private);
        void Reject(Guid source,int[] values,string message)
        {
            var error=Assert.Throws<System.Reflection.TargetInvocationException>(()=>configure.Invoke(coordinator,new object[]{source,values}));
            Assert.That(error.InnerException.Message,Is.EqualTo(message));
        }
        Reject(Guid.NewGuid(),new[]{17},"NextFloorInputConfigurationRejected");
        Reject(generation,new int[0],"NextFloorInputConfigurationRejected");
        Reject(generation,new[]{1,2,3,4,5},"NextFloorInputConfigurationRejected");
        var inputs=new[]{17,73,4099};configure.Invoke(coordinator,new object[]{generation,inputs});inputs[0]=999;
        Reject(generation,new[]{42},"NextFloorInputConfigurationRejected");
        foreach(int expected in new[]{17,73,4099})
        {
            var record=(TowerRunGenerationRecord)factory.Invoke(coordinator,new object[]{true});
            Assert.That(record.requestedSeed,Is.EqualTo(expected));
        }
        var exhausted=Assert.Throws<System.Reflection.TargetInvocationException>(()=>factory.Invoke(coordinator,new object[]{true}));
        Assert.That(exhausted.InnerException.Message,Is.EqualTo("NextFloorInputSequenceExhausted"));
        Assert.That(Snapshot(),Is.EqualTo(before),"Record-factory checks must not publish, move or regenerate the world.");
        Call("ReturnToStartupChoice");Assert.That(queueField.GetValue(coordinator),Is.Null);
        string copies=Path.Combine(Application.dataPath,"../Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Authoring/OldSaveCopies/MainV1-P02");
        string copy=Directory.GetFiles(copies,"tower-run-slot-*.json").OrderBy(path=>path,StringComparer.Ordinal).First();byte[] original=File.ReadAllBytes(copy);
        Directory.CreateDirectory(TowerRunRuntimeEnvironment.SlotRoot);File.WriteAllBytes(Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json"),original);
        Restore(1);Ready(false);Assert.That(queueField.GetValue(coordinator),Is.Null);
        Reject(controller.Orchestrator.Context.ActiveGenerationId.Value,new[]{17},"NextFloorInputConfigurationRejected");
        Assert.That(File.ReadAllBytes(copy),Is.EqualTo(original));
        File.WriteAllText(Path.Combine(Output,"finite-floor-input-policy.txt"),"INPUT_FACTORY_AND_SCOPE_REGRESSION_ONLY: wrong generation/count/rearm rejected; copied finite17/73/4099; exhaustion explicitly throws without Guid fallback; factory does not change world; cancellation clears; real MainV1 copied checkpoint restores and rejects VNext policy. Actual F/retry is a separate formal consumer case. No source identity or save expectation rewritten.");
    }
    [UnityTest] public IEnumerator ProjectedOriginalSightMatchesNativeOracleAndRejectsBlockedBoundaryWithoutMovingActors()
    {
        yield return Load("Original-sight-projection");Call("BeginNewRunWithSeed",21193);Ready(true);yield return new WaitForFixedUpdate();yield return null;
        var context=controller.Orchestrator.Context;
        var resident=UnityEngine.Object.FindObjectsOfType<EcologyResidentBinding>().Single(b=>b.GenerationId==context.ActiveGenerationId&&b.ResidentId=="ecology.resident.1003.003");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var resultType=type.GetNestedType("PursuitTrialPrediction",System.Reflection.BindingFlags.NonPublic);
        var projected=type.GetMethod("OriginalProjectedSight",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var perception=resident.GetComponents<MonoBehaviour>().Single(c=>c.GetType().Name=="MonsterPerceptionController");
        var target=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(c=>c.GetType().Name=="PlayerAction").transform;
        var targetBody=target.GetComponent<Rigidbody2D>();var residentBody=resident.GetComponent<Rigidbody2D>();
        var actualMethod=perception.GetType().GetMethod("IsTargetVisible",Private);var centreMethod=perception.GetType().GetMethod("GetTargetPosition",Private);
        Vector2 centre=(Vector2)centreMethod.Invoke(perception,new object[]{target});Vector2 centreOffset=centre-targetBody.position;
        var sightOrigin=(Transform)perception.GetType().GetField("sightOrigin",Private).GetValue(perception);Vector2 sightOffset=sightOrigin==null?Vector2.zero:(Vector2)sightOrigin.position-residentBody.position;
        string BiologicalState()
        {
            var ai=resident.GetComponents<MonoBehaviour>().Single(c=>c.GetType().Name=="MonsterAIController");
            var damage=resident.GetComponents<MonoBehaviour>().First(c=>c.GetType().GetProperty("CurrentHealth")!=null);
            var playerDamage=target.GetComponents<MonoBehaviour>().Single(c=>c.GetType().Name=="PlayerDamageController");
            return ai.GetType().GetProperty("CurrentStateType").GetValue(ai)+"|"+damage.GetType().GetProperty("CurrentHealth").GetValue(damage)+"|"+playerDamage.GetType().GetProperty("CurrentHealth").GetValue(playerDamage)+"|"+resident.ScopeFaulted+"|"+resident.Excursion.HasPermit+"|"+resident.Excursion.ReturnLatched;
        }
        string biological=BiologicalState();
        string world=Snapshot();Vector3 residentPose=resident.transform.position,targetPose=target.position;Vector2 nativePose=residentBody.position,playerNative=targetBody.position;
        var actorBinding=resident.GetComponent<EcologyCombatBinding>();var playerBinding=target.GetComponent<EcologyCombatBinding>();long actorLease=actorBinding.Lease,playerLease=playerBinding.Lease,actorFault=actorBinding.MotionFaultRevision,playerFault=playerBinding.MotionFaultRevision;
        object Query(Vector2 observerRoot,Vector2 targetRoot,out bool visible)
        {
            var report=Activator.CreateInstance(resultType,true);visible=(bool)projected.Invoke(null,new object[]{resident,target,observerRoot,targetRoot,report});return report;
        }
        bool original=(bool)actualMethod.Invoke(perception,new object[]{target});object current=Query(nativePose,playerNative,out bool currentVisible);
        Assert.That((bool)resultType.GetField("sightBufferSaturated").GetValue(current),Is.False);
        Assert.That(currentVisible,Is.EqualTo(original),"Current native pose query must match the original private sight oracle.");
        object zero=Query(centre-sightOffset,playerNative,out bool zeroVisible);Assert.That(zeroVisible,Is.True);
        float radius=(float)perception.GetType().GetProperty("DetectionRadius").GetValue(perception);
        object distant=Query(centre+Vector2.right*(radius+2f)-sightOffset,playerNative,out bool distantVisible);Assert.That(distantVisible,Is.False);
        object blocked=null;Vector2 witnessOrigin=default,witnessTarget=default;
        var edges=UnityEngine.Object.FindObjectsOfType<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");int checkedEdges=0;
        for(int path=0;path<edges.pathCount&&blocked==null&&checkedEdges<32;path++)
        {
            var points=edges.GetPath(path);
            for(int index=0;index<points.Length&&blocked==null&&checkedEdges<32;index++)
            {
                Vector2 a=edges.transform.TransformPoint(points[index]+edges.offset),b=edges.transform.TransformPoint(points[(index+1)%points.Length]+edges.offset);
                if((a-b).sqrMagnitude<.01f)continue;checkedEdges++;
                Vector2 middle=(a+b)*.5f,direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x)*.2f;
                foreach(int sign in new[]{1,-1})
                {
                    Vector2 origin=middle+normal*sign,destination=middle-normal*sign;
                    object answer=Query(origin-sightOffset,destination-centreOffset,out bool visible);
                    if(!visible&&(string)resultType.GetField("sightReason").GetValue(answer)=="OriginalSightBlockingCollider")
                    {blocked=answer;witnessOrigin=origin;witnessTarget=destination;break;}
                }
            }
        }
        Assert.That(blocked,Is.Not.Null,"Actual native cliff boundary must provide a certified blocked ray; no actor is moved there.");
        Assert.That(resident.transform.position,Is.EqualTo(residentPose));Assert.That(target.position,Is.EqualTo(targetPose));Assert.That(residentBody.position,Is.EqualTo(nativePose));Assert.That(targetBody.position,Is.EqualTo(playerNative));
        Assert.That(actorBinding.Lease,Is.EqualTo(actorLease));Assert.That(playerBinding.Lease,Is.EqualTo(playerLease));Assert.That(actorBinding.MotionFaultRevision,Is.EqualTo(actorFault));Assert.That(playerBinding.MotionFaultRevision,Is.EqualTo(playerFault));Assert.That(Snapshot(),Is.EqualTo(world));Assert.That(BiologicalState(),Is.EqualTo(biological));
        File.WriteAllText(Path.Combine(Output,"original-sight-projection.json"),"{\"scope\":\"SYNCHRONOUS_FROZEN_SCENE_QUERY_NOT_TRAVERSAL_OR_HISTORICAL_CAUSE\",\"originalActualVisible\":"+original.ToString().ToLowerInvariant()+",\"current\":"+JsonUtility.ToJson(current)+",\"coincident\":"+JsonUtility.ToJson(zero)+",\"distant\":"+JsonUtility.ToJson(distant)+",\"blocked\":"+JsonUtility.ToJson(blocked)+",\"witnessOrigin\":"+JsonUtility.ToJson(witnessOrigin)+",\"witnessTarget\":"+JsonUtility.ToJson(witnessTarget)+"}");
    }

    [UnityTest] public IEnumerator NativeFirstPursuitStepUsesProspectiveWaypointAndRejectsOriginalCastBlockerWithoutMovingActors()
    {
        yield return Load("Native-prospective-first-step");Call("BeginNewRunWithSeed",21193);Ready(true);yield return new WaitForFixedUpdate();yield return null;
        var context=controller.Orchestrator.Context;
        var bindings=UnityEngine.Object.FindObjectsOfType<EcologyResidentBinding>().Where(b=>b.GenerationId==context.ActiveGenerationId).ToArray();
        var resident=bindings.Single(b=>b.ResidentId=="ecology.resident.1003.003");
        var nativeFoot=(Collider2D)typeof(EcologyResidentBinding).GetField("movementFoot",Private).GetValue(resident);var nativeBody=nativeFoot.attachedRigidbody;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var resultType=type.GetNestedType("PursuitTrialPrediction",System.Reflection.BindingFlags.NonPublic);
        var query=type.GetMethod("OriginalNativePursuitStartClear",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var mover=resident.GetComponents<MonoBehaviour>().Single(b=>b.GetType().Name=="MonsterMovementController");
        var originalCast=mover.GetType().GetMethod("GetAllowedMoveDistance",Private);var destinationField=mover.GetType().GetField("destination",Private);
        object Field(object row,string name)=>row.GetType().GetField(name).GetValue(row);
        string StateOf(EcologyResidentBinding actor)
        {
            var actorBody=actor.GetComponent<Rigidbody2D>();var combat=actor.GetComponent<EcologyCombatBinding>();
            var ai=actor.GetComponents<MonoBehaviour>().Single(b=>b.GetType().Name=="MonsterAIController");
            var damage=actor.GetComponents<MonoBehaviour>().First(b=>b.GetType().GetProperty("CurrentHealth")!=null);
            return actor.transform.position.ToString("R")+"|"+actorBody.position.ToString("R")+"|"+ai.GetType().GetProperty("CurrentStateType").GetValue(ai)+"|"+damage.GetType().GetProperty("CurrentHealth").GetValue(damage)+"|"+combat.Lease+"|"+combat.MotionFaultRevision+"|"+actor.ScopeFaulted+"|"+actor.NavigationFailure+"|"+actor.Excursion.HasPermit+"|"+actor.Excursion.ReturnLatched;
        }
        var before=bindings.ToDictionary(b=>b,StateOf);var homes=bindings.ToDictionary(b=>b,b=>b.Home);string world=Snapshot();
        var player=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(b=>b.GetType().Name=="PlayerAction");
        Vector3 playerPose=player.transform.position;Vector2 playerNative=player.GetComponent<Rigidbody2D>().position;
        var playerDamage=player.GetComponents<MonoBehaviour>().Single(b=>b.GetType().Name=="PlayerDamageController");object playerHp=playerDamage.GetType().GetProperty("CurrentHealth").GetValue(playerDamage);
        var playerCombat=player.GetComponent<EcologyCombatBinding>();long playerLease=playerCombat.Lease,playerFault=playerCombat.MotionFaultRevision;
        Vector2 start=nativeBody.position,heldDestination=(Vector2)destinationField.GetValue(mover),prospective=default;object clear=null;int tried=0;
        object Query(Vector2 target,out bool accepted)
        {
            var answer=Activator.CreateInstance(resultType,true);accepted=(bool)query.Invoke(null,new object[]{resident,nativeFoot,start,target,answer});return answer;
        }
        foreach(Vector2 point in Enumerable.Range(0,resident.Home.MovementGraph.NodeCount).Where(resident.Home.MovementGraph.IsLegal).Select(resident.Home.NodeWorldPosition).Where(point=>Vector2.Distance(start,point)>.3f&&Vector2.Distance(start,point)<5f).OrderBy(point=>Vector2.Distance(start,point)).Take(32))
        {
            tried++;object answer=Query(point,out bool accepted);if(!accepted)continue;
            var step=Field(answer,"nativeFirstStep");Vector2 direction=(Vector2)Field(step,"direction");
            if(Mathf.Max(Mathf.Abs(direction.x),Mathf.Abs(direction.y))<.99f)continue;
            prospective=point;clear=answer;break;
        }
        Assert.That(clear,Is.Not.Null,"A genuine clear current native step is required; the finite query does not move actors to construct one.");
        object clearStep=Field(clear,"nativeFirstStep");Vector2 directionOfStep=(Vector2)Field(clearStep,"direction");float requested=(float)Field(clearStep,"requestedDistance");
        Assert.That((bool)Field(clearStep,"prospectivePursuit"),Is.True);Assert.That((Vector2)Field(clearStep,"evaluatedDestination"),Is.EqualTo(prospective));
        Assert.That((Vector2)Field(clearStep,"destination"),Is.EqualTo(heldDestination),"The held AI destination is observed, not replaced by the prospective query.");
        Assert.That(resident.Home.TryPursuitWaypoint(nativeFoot,start,prospective,resident.Excursion.MaximumHomeSteps,out Vector2 waypoint),Is.True);
        Assert.That((Vector2)Field(clearStep,"waypoint"),Is.EqualTo(waypoint));
        float oracleClear=(float)originalCast.Invoke(mover,new object[]{directionOfStep,requested});Assert.That((float)Field(clearStep,"originalAllowed"),Is.EqualTo(oracleClear));
        var blocker=new GameObject("QueryOnly_Native_FirstStep_Blocker_Not_A_Traversal_Peer");blocker.SetActive(false);object blocked=null,after=null;float oracleBlocked=-1;int blockerId=0;
        try
        {
            Vector2 axis=Mathf.Abs(directionOfStep.x)>=Mathf.Abs(directionOfStep.y)?Vector2.right*Mathf.Sign(directionOfStep.x):Vector2.up*Mathf.Sign(directionOfStep.y);
            float extent=Mathf.Abs(axis.x)*nativeFoot.bounds.extents.x+Mathf.Abs(axis.y)*nativeFoot.bounds.extents.y;
            blocker.layer=resident.gameObject.layer;blocker.transform.position=(Vector2)nativeFoot.bounds.center+axis*(extent+.05f+.005f);
            var blockerBody=blocker.AddComponent<Rigidbody2D>();blockerBody.bodyType=RigidbodyType2D.Kinematic;blockerBody.interpolation=RigidbodyInterpolation2D.None;
            var shape=blocker.AddComponent<BoxCollider2D>();shape.size=Vector2.one*.1f;shape.isTrigger=false;blocker.SetActive(true);blockerId=shape.GetInstanceID();
            Assert.That(Vector2.Distance(shape.bounds.center,blocker.transform.position),Is.LessThan(.0001f),"The query-only collider must actually have a coherent native pose; no SyncTransforms is used.");
            oracleBlocked=(float)originalCast.Invoke(mover,new object[]{directionOfStep,requested});Assert.That(oracleBlocked,Is.LessThanOrEqualTo(.0001f),"The original native Cast must actually be blocked; a reason-only assertion is insufficient.");
            blocked=Query(prospective,out bool accepted);Assert.That(accepted,Is.False);Assert.That((string)Field(blocked,"reason"),Is.EqualTo("OriginalNativePursuitFirstStepBlockedOrClipped"));
            var blockedStep=Field(blocked,"nativeFirstStep");Assert.That((float)Field(blockedStep,"originalAllowed"),Is.EqualTo(oracleBlocked));
            Assert.That(((Array)Field(blockedStep,"bodyHits")).Cast<object>().Any(hit=>(int)Field(hit,"id")==blockerId),Is.True,"Bind the rejected query to the actual test collider, not another incidental obstacle.");
            shape.enabled=false;blockerBody.simulated=false;
            after=Query(prospective,out bool clearAgain);Assert.That(clearAgain,Is.True);Assert.That((float)Field(Field(after,"nativeFirstStep"),"originalAllowed"),Is.EqualTo(oracleClear));
        }
        finally
        {
            var shape=blocker.GetComponent<Collider2D>();if(shape!=null)shape.enabled=false;var blockerBody=blocker.GetComponent<Rigidbody2D>();if(blockerBody!=null)blockerBody.simulated=false;UnityEngine.Object.Destroy(blocker);
        }
        Assert.That(bindings.All(b=>StateOf(b)==before[b]&&ReferenceEquals(b.Home,homes[b])),Is.True,"Synchronous queries must preserve native poses, AI, health, lease, faults, excursion and bound navigation references.");
        Assert.That((Vector2)destinationField.GetValue(mover),Is.EqualTo(heldDestination));Assert.That(Snapshot(),Is.EqualTo(world));
        Assert.That(player.transform.position,Is.EqualTo(playerPose));Assert.That(player.GetComponent<Rigidbody2D>().position,Is.EqualTo(playerNative));Assert.That(playerDamage.GetType().GetProperty("CurrentHealth").GetValue(playerDamage),Is.EqualTo(playerHp));Assert.That(playerCombat.Lease,Is.EqualTo(playerLease));Assert.That(playerCombat.MotionFaultRevision,Is.EqualTo(playerFault));
        File.WriteAllText(Path.Combine(Output,"original-native-first-step-regression.json"),"{\"scope\":\"SYNCHRONOUS_CURRENT_NATIVE_QUERY_WITH_TEMPORARY_OWNED_BLOCKER_NOT_ACTUAL_PEER_MOTION_OR_FUTURE_TRAVERSAL\",\"buildId\":\""+MapHostBuildStamp.BuildId+"\",\"generation\":\""+resident.GenerationId+"\",\"tried\":"+tried+",\"blockerId\":"+blockerId+",\"oracleClear\":"+oracleClear.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"oracleBlocked\":"+oracleBlocked.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"clear\":"+JsonUtility.ToJson(clear)+",\"blocked\":"+JsonUtility.ToJson(blocked)+",\"afterRemoval\":"+JsonUtility.ToJson(after)+"}");
    }

}
