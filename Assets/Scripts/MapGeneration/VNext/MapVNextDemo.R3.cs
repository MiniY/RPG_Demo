using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace MapVNext
{
    public sealed partial class MapVNextDemo
    {
        List<MapVNextStairGeometry> ramps;
        MapVNextStairGeometry activeRamp;
        readonly List<Vector3> trajectory=new List<Vector3>();
        LineRenderer trajectoryLine;
        Material trajectoryMaterial;
        string heldKeys="None", trialPhase="ROUNDTRIP";
        readonly List<string> FrameTrace=new List<string>{"screenshot,frame,physicsTime,gameTime,utc,keys,moveX,moveY,footX,footY,generation,case,identity"};
        void RecordFrame(string path)
        {
            Vector2 input=FinalMoveInput,p=FootPosition;
            FrameTrace.Add(string.Join(",",path,Time.frameCount,F(Time.fixedTime),F(Time.time),DateTime.UtcNow.ToString("O"),heldKeys,F(input.x),F(input.y),F(p.x),F(p.y),Generation,Current.Id,BuildIdentity));
        }
        int physicalSample;
        object actionComponent;
        System.Reflection.PropertyInfo moveProperty;
        Vector2 FinalMoveInput => (Vector2)moveProperty.GetValue(actionComponent);
        Vector2 FootPosition => Foot.bounds.center;

        void BindR3()
        {
            approachGraphs.Clear();
            ramps=MapVNextStairGeometry.Derive(Map,Terrain.Coordinates);
            activeRamp=ramps.First();
            if(actionComponent==null)
            {actionComponent=Player.GetComponent("PlayerAction");moveProperty=actionComponent.GetType().GetProperty("MoveInput");}
            if(trajectoryLine==null)
            {
                var go=new GameObject("AcceptanceFootTrajectory");go.transform.SetParent(transform,false);
                trajectoryLine=go.AddComponent<LineRenderer>();trajectoryLine.useWorldSpace=true;
                trajectoryMaterial=new Material(Shader.Find("Sprites/Default"));trajectoryLine.sharedMaterial=trajectoryMaterial;
                trajectoryLine.startWidth=trajectoryLine.endWidth=.045f;
                trajectoryLine.startColor=trajectoryLine.endColor=new Color(1f,.3f,.9f,.9f);trajectoryLine.sortingOrder=1000;
            }
            trajectory.Clear();trajectoryLine.positionCount=0;
        }
        void TraceR3(int tick)
        {
            Vector2 p=FootPosition,m=FinalMoveInput;Vector2Int c=Terrain.Coordinates.WorldToCell(p);
            var contacts=new ContactPoint2D[12];int count=Foot.GetContacts(contacts);
            string normal=string.Join("|",contacts.Take(count).Select(v=>v.collider.name+":"+F(v.normal.x)+":"+F(v.normal.y)));
            Trace.Add(string.Join(",",Current.Id,tick,heldKeys,F(p.x),F(p.y),ActualElevation,Generation,Player.GetInstanceID(),Foot.GetInstanceID(),
                BuildIdentity,physicalSample++,F(Time.fixedTime),DateTime.UtcNow.ToString("O"),F(m.x),F(m.y),
                c.x,c.y,Map.GetCell(c).terrainType,activeRamp?.Id??"None",activeRamp?.Phase(p)??"None",trialPhase,count,normal,F(body.position.x),F(body.position.y)));
            trajectory.Add(new Vector3(p.x,p.y,-.1f));
            trajectoryLine.positionCount=trajectory.Count;trajectoryLine.SetPosition(trajectory.Count-1,trajectory[trajectory.Count-1]);
        }
        static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
        CaseEvidence NewEvidence()=>new CaseEvidence{id=Current.Id,side=Current.Side,context=Current.Context,seed=Current.Seed,
            generation=Generation,mapDigest=MapVNextFixture.Digest(Map),lower=Current.SeedChain?0:Current.Lower,
            upper=Current.SeedChain?4:Current.Lower+1,playerId=Player.GetInstanceID(),footId=Foot.GetInstanceID(),radius=Foot.radius};

        IEnumerator MoveToR3(Vector2 target,bool capture,CaseEvidence e)
        {
            for(int tick=0;tick<240;tick++)
            {
                Vector2 delta=target-FootPosition;
                if(delta.magnitude<.115f)
                {
                    // Screenshot encoding can leave queued input pending for another frame.
                    // Wait for the real PlayerAction vector to settle; correct with actual keys if needed.
                    yield return Release();TraceR3(e.ticks++);
                    if(Vector2.Distance(FootPosition,target)<.20f)break;
                    continue;
                }
                var keys=new List<Key>();
                if(Math.Abs(delta.x)>.065f)keys.Add(delta.x>0?Key.D:Key.A);
                if(Math.Abs(delta.y)>.065f)keys.Add(delta.y>0?Key.W:Key.S);
                Feed(keys.ToArray());yield return fixedFrame;TraceR3(e.ticks++);
                Vector2 input=FinalMoveInput;
                if(Math.Abs(input.x)>.5f&&Math.Abs(input.y)>.5f)e.dualInputSamples++;
                if(capture&&e.ticks%10==0&&!string.IsNullOrEmpty(output))
                {yield return Capture("Motion/"+Current.Id+"-"+e.ticks.ToString("D5")+"-debug.png",false);
                 yield return Capture("Motion/"+Current.Id+"-"+e.ticks.ToString("D5")+"-clean.png",true);}
            }
            yield return Release();
            if(Vector2.Distance(FootPosition,target)>.20f)PersistFailure(Current.Id+":r3-target-not-reached",target);
            Require(Vector2.Distance(FootPosition,target)<.20f,Current.Id+":r3-target-not-reached:"+target);
        }
        IEnumerator WalkSlope(MapVNextStairGeometry r,bool up,bool pause,bool capture,CaseEvidence e)
        {
            activeRamp=r;trialPhase=up?"UPHILL":"DOWNHILL";
            Vector2 start=FootPosition;
            Require(Vector2.Distance(start,up?r.Entry:r.Exit)<.22f,"SlopeMustBeginAtRecordedMouth");
            yield return MoveToR3(r.Middle,capture,e);
            Vector2 local=r.Local(FootPosition);
            Require(local.x>.30f&&local.x<.70f&&ActualElevation==r.LowerElevation,"SlopeMiddleMustRemainOnItsMapDataLowerCell");
            if(pause)
            {
                trialPhase="PAUSE_IN_SLOPE";Vector2 stopped=FootPosition;
                for(int i=0;i<10;i++){yield return fixedFrame;TraceR3(e.ticks++);}
                e.pauseDrift=Vector2.Distance(stopped,FootPosition);e.pauseStable=e.pauseDrift<.025f;
                Require(e.pauseStable,"SlopePauseDrift");
                trialPhase="REVERSE_IN_SLOPE";
                yield return MoveToR3(r.Point(.12f,.67f),capture,e);e.reversed=true;
                trialPhase="RESUME_UPHILL";yield return MoveToR3(r.Middle,capture,e);
                trialPhase="UPHILL";
            }
            yield return MoveToR3(up?r.Exit:r.Entry,capture,e);
            Vector2 movement=FootPosition-start;
            Require(Math.Abs(movement.x)>r.Width*3.1f&&Math.Abs(movement.y)>r.Height*3.1f,"SlopeDisplacementMustMatchVisibleDiagonalSpan");
            Require(Math.Sign(movement.x)==(r.West?1:-1)*(up?1:-1)&&Math.Sign(movement.y)==(up?1:-1),"SlopeDirectionMismatch");
            Require(ActualElevation==(up?r.UpperElevation:r.LowerElevation),"SlopeEndpointMapDataHeightMismatch");
            if(up){e.uphill=true;e.upDelta=movement;}else{e.downhill=true;e.downDelta=movement;}
        }
        IEnumerator HorizontalNegatives(MapVNextStairGeometry r,bool exhaustive,bool capture,CaseEvidence e)
        {
            // Separate trials have explicit initial placement. No placement occurs inside WalkSlope.
            var starts=new List<Vector2>{r.Entry,r.Point(-1.3f,.5f)};
            if(exhaustive){starts.Add(r.Point(-1.3f,1f));starts.Add(r.Point(-1.3f,1.5f));}
            foreach(Vector2 point in starts)
            {
                yield return Release();PlaceFoot(point,"RESET_SEPARATE_HORIZONTAL_NEGATIVE");
                trajectory.Clear();trajectoryLine.positionCount=0;trialPhase="HORIZONTAL_NEGATIVE";
                int maxElevation=ActualElevation;Vector2 before=FootPosition;
                for(int i=0;i<64;i++)
                {
                    Feed(r.West?Key.D:Key.A);yield return fixedFrame;TraceR3(e.ticks++);
                    maxElevation=Math.Max(maxElevation,ActualElevation);
                    if(capture&&i%16==0&&!string.IsNullOrEmpty(output))yield return Capture("Negatives/"+Current.Id+"-"+e.horizontalTrials+"-"+i+".png",false);
                }
                yield return Release();e.horizontalTrials++;
                bool blocked=maxElevation==r.LowerElevation&&Vector2.Dot(FootPosition-r.Corner,r.Direction)<r.Width;
                if(!blocked)PersistFailure(Current.Id+":horizontal-bypass",before);
                Require(blocked,Current.Id+":horizontal-bypass");e.horizontalBlockedCount++;
            }
            e.horizontalBlocked=e.horizontalBlockedCount==e.horizontalTrials;
        }
        IEnumerator RoundTripR3(bool wallProof,bool capture)
        {
            if(automationKeyboard==null)automationKeyboard=InputSystem.AddDevice<Keyboard>("MapVNextR3Keyboard");
            var e=NewEvidence();var r=ramps.Single();
            yield return WalkSlope(r,true,wallProof,capture,e);
            yield return WalkSlope(r,false,false,capture,e);
            Require(e.dualInputSamples>20,"EffectiveTwoComponentInputWasNotObserved");
            e.corridorOrdered=true;
            yield return HorizontalNegatives(r,wallProof,capture,e);
            if(wallProof)
            {
                trialPhase="CLOSED_CLIFF_NEGATIVE";PlaceFoot(r.Point(-.3f,3.5f),"RESET_SEPARATE_CLOSED_CLIFF_NEGATIVE");
                Vector2 before=FootPosition;int max=ActualElevation;
                for(int i=0;i<40;i++){Feed(r.West?Key.D:Key.A);yield return fixedFrame;TraceR3(e.ticks++);max=Math.Max(max,ActualElevation);}
                yield return Release();e.cliffBlocked=max==r.LowerElevation&&Vector2.Distance(before,FootPosition)<1.5f;
                if(!e.cliffBlocked)PersistFailure(Current.Id+":closed-cliff",before);
                Require(e.cliffBlocked,"ClosedCliffBypass");
            }
            e.status="PASS";Evidence.Add(e);
        }
        IEnumerator OnLevelTo(Vector2 target,CaseEvidence e) { return OnLevelToWithClearance(target,e); }
        IEnumerator SeedRoundTripR3()
        {
            if(automationKeyboard==null)automationKeyboard=InputSystem.AddDevice<Keyboard>("MapVNextR3SeedKeyboard");
            var e=NewEvidence();Vector2 start=FootPosition;
            foreach(var r in ramps){trialPhase="LEVEL_APPROACH";yield return OnLevelTo(r.Entry,e);yield return WalkSlope(r,true,false,false,e);}
            yield return OnLevelTo(Center(Map.ExitCell),e);Require(ActualElevation==4,"SeedH4Missing");
            foreach(var r in ramps.AsEnumerable().Reverse()){trialPhase="LEVEL_APPROACH";yield return OnLevelTo(r.Exit,e);yield return WalkSlope(r,false,false,false,e);}
            yield return OnLevelTo(start,e);Require(ActualElevation==0,"SeedReturnH0Missing");
            e.corridorOrdered=true;e.status="PASS";Evidence.Add(e);
        }
    }
}
