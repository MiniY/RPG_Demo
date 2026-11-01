using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace MapVNext
{
    public sealed partial class MapVNextDemo
    {
        public static readonly int[] CliffSeeds={42,21193,7727,314159};
        [Serializable] public sealed class CliffTrial
        {public int seed,steps,actualHeight,setupAttempts,boundaryClearanceContactSamples;public string region,shape,keys,status,generation,clean,debug,boundary;public Vector2 start,end,requestedStart;public float radius;public bool centerCovered,footIntruded;}
        [Serializable] public sealed class PlacementTrial
        {public int seed;public string category,shape,reason,generation;public Vector2 requested;public bool expectedAllowed,allowed,active;}
        [Serializable] sealed class CliffRun
        {public string identity,status,geometryIdentity;public float contactTolerance;public int[] seeds;public CliffTrial[] trials;public PlacementTrial[] placements;public SurfaceRow[] surfaces;}
        [Serializable] public sealed class SurfaceRow
        {public int seed,logicalWalkable,surfaceLegal,surfaceReachable,southFaces,shoulders,projectedFaceCoreChecks,colliderPaths;public string generation,terrainHash,spatialHash;}
        public readonly List<CliffTrial> CliffTrials=new List<CliffTrial>();
        public readonly List<PlacementTrial> PlacementTrials=new List<PlacementTrial>();
        public readonly List<SurfaceRow> SurfaceRows=new List<SurfaceRow>();
        GameObject cliffDiagnostics,placementProbes,cliffFootVisual;
        Sprite placementProbeSprite;
        bool monitoringCliff;bool cliffIntrusion;int cliffFixedSamples,cliffBoundaryContacts;Vector2 previousCliffFoot;
        bool showCliffDiagnostics;
        int cliffPreviewIndex;
        MapVNextSurfaceQuery Surface=>Terrain.GetComponent<MapVNextSurfaceQuery>();
        MapVNextCliffExclusion.Region[] CliffSamples()
        {
            var faces=Surface.Exclusion.Regions.Where(r=>r.kind=="SOUTH_FACE").ToArray();
            bool Interior(MapVNextCliffExclusion.Region r)=>Enumerable.Range(-2,5).All(x=>faces.Any(n=>n.owner==r.owner+Vector2Int.right*x));
            var chosen=new List<MapVNextCliffExclusion.Region>();
            void Add(IEnumerable<MapVNextCliffExclusion.Region> source){var item=source.FirstOrDefault(r=>!chosen.Contains(r));if(item!=null)chosen.Add(item);}
            Add(faces.Where(Interior).OrderBy(r=>r.visual.y).ThenBy(r=>r.visual.x));
            Add(faces.Where(Interior).OrderByDescending(r=>r.elevation).ThenBy(r=>r.visual.y).ThenBy(r=>r.visual.x));
            Add(faces.Where(r=>Map.GetElevation(r.owner+Vector2Int.left)<r.elevation).OrderBy(r=>r.visual.y).ThenBy(r=>r.visual.x));
            Add(faces.Where(r=>Map.GetElevation(r.visual+Vector2Int.right)>=r.elevation||Map.GetElevation(r.visual+Vector2Int.left)>=r.elevation).OrderBy(r=>r.visual.y).ThenBy(r=>r.visual.x));
            return chosen.ToArray();
        }
        void RecordSurface()
        {
            var grid=Terrain.WaterBaseTilemap.transform.parent;var edges=grid.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");int checks=0;
            // Independent projection readback: every rendered plain rock core must be inside actual physics.
            foreach(var tm in grid.GetComponentsInChildren<Tilemap>())if(tm.name.StartsWith("GeneratedElevation")&&tm.name.EndsWith("Face"))
            foreach(var p in tm.cellBounds.allPositionsWithin)
            {
                var tile=tm.GetTile<Tile>(p);if(tile==null)continue;int role=Assets.Role(tile);
                if(role<34||role>43||role==38||role==39)continue;
                Vector2 center=tm.GetCellCenterWorld(p);Require(edges.OverlapPoint(center),"RENDERED_ROCK_CORE_NOT_COVERED:"+Current.Id+":"+p+":"+role);checks++;
            }
            SurfaceRows.Add(new SurfaceRow{seed=randomSeed,generation=Generation,terrainHash=Layout.semanticHash,spatialHash=Surface.SpatialHash,logicalWalkable=Layout.walkableCells,
                surfaceLegal=Layout.surfaceWalkableCells,surfaceReachable=Layout.surfaceReachableCells,southFaces=Surface.Exclusion.Regions.Count(r=>r.kind=="SOUTH_FACE"),shoulders=ramps.Count,projectedFaceCoreChecks=checks,colliderPaths=edges.pathCount});
            if(!string.IsNullOrEmpty(output))
            {string path=Path.Combine(output,"CliffGeometry",Current.Id+".json");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(new GeometryReadback{generation=Generation,spatialHash=Surface.SpatialHash,regions=Surface.Exclusion.Regions,colliderPaths=Enumerable.Range(0,edges.pathCount).Select(i=>new PathRow{world=edges.GetPath(i).Select(p=>(Vector2)edges.transform.TransformPoint(p)).ToArray()}).ToArray()},true));}
        }
        [Serializable] sealed class GeometryReadback{public string generation,spatialHash;public MapVNextCliffExclusion.Region[] regions;public PathRow[] colliderPaths;}
        [Serializable] sealed class PathRow{public Vector2[] world;}
        IEnumerator HoldAtCliff(MapVNextCliffExclusion.Region region,string label,Vector2 start,Key[] keys,int ticks)
        {
            trialPhase="CLIFF_"+label;activeRamp=null;Vector2 requested=start;int setupAttempts=0;
            int expectedLevel=label=="UPPER_S_EDGE"?region.elevation:region.elevation-1;Vector2 wall=Center(region.visual);
            var starts=new[]{requested,new Vector2(wall.x,requested.y),new Vector2(wall.x-.35f,requested.y),new Vector2(wall.x+.35f,requested.y)}.Distinct().ToArray();
            bool qualified=false;foreach(var candidate in starts)
            {setupAttempts++;if(Surface.CanOccupy(MapVNextGroundFootprint.Circle(Foot.radius),candidate,expectedLevel,out _)){start=candidate;qualified=true;break;}}
            Require(qualified,"CLIFF_TEST_SETUP_HAS_NO_LEGAL_SAME_LEVEL_START:"+region.id+":"+label);
            PlaceFoot(start,"RESET_CLIFF_SEPARATE_TRIAL_NOT_TRAVERSAL_REQUESTED="+requested+"_ATTEMPTS="+setupAttempts);yield return Release();
            var row=new CliffTrial{seed=randomSeed,region=region.id,shape=label,keys=string.Join("+",keys),generation=Generation,start=FootPosition,requestedStart=requested,setupAttempts=setupAttempts,radius=Foot.radius,actualHeight=ActualElevation,boundary=region.boundary};
            bool primary=region==CliffSamples()[0];bool motion=primary&&(label=="LOWER_W_HOLD"||label=="DIAGONAL_WD");
            cliffIntrusion=false;cliffFixedSamples=0;cliffBoundaryContacts=0;previousCliffFoot=FootPosition;monitoringCliff=true;
            for(int i=0;i<ticks;i++)
            {
                Feed(keys);yield return null;yield return fixedFrame;
                if(motion&&i%8==0)yield return Capture("CliffMotion/"+Current.Id+"-"+region.id+"-"+label+"-"+i.ToString("D3")+".png",true);
            }
            yield return Release();monitoringCliff=false;row.steps=cliffFixedSamples;row.boundaryClearanceContactSamples=cliffBoundaryContacts;row.footIntruded=cliffIntrusion;row.end=FootPosition;row.actualHeight=ActualElevation;
            var edges=Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");row.centerCovered=edges.OverlapPoint(Center(region.visual));
            if(label=="LOWER_W_HOLD"||(primary&&(label=="UPPER_S_EDGE"||label=="DIAGONAL_WD")))
            {
            row.clean="Cliff/"+Current.Id+"-"+region.id+"-"+label+"-clean.png";row.debug=row.clean.Replace("-clean","-debug");
            showCliffDiagnostics=false;RebuildCliffDiagnostics();yield return Capture(row.clean,true);
            showCliffDiagnostics=true;RebuildCliffDiagnostics();yield return Capture(row.debug,false);showCliffDiagnostics=false;RebuildCliffDiagnostics();
            }
            row.status=row.footIntruded?"FAIL":"PASS";CliffTrials.Add(row);Require(!row.footIntruded,"CLIFF_FOOTPRINT_INTRUSION:"+region.id+":"+label);
        }
        IEnumerator CaptureR1CollisionReplay(MapVNextCliffExclusion.Region region)
        {
            // Explicit diagnostic reconstruction, never a manual gameplay mode. Retained edge/stair
            // prefix is unchanged from r1; only the new exclusion suffix is disabled for this test.
            var edges=Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");
            int full=edges.pathCount,old=full-Surface.Exclusion.Regions.Length;edges.pathCount=old;Physics2D.SyncTransforms();
            Vector2 wall=Center(region.visual);PlaceFoot(wall+Vector2.down*1.1f,"RESET_DIAGNOSTIC_R1_COLLISION_REPLAY");trialPhase="DIAGNOSTIC_R1_GEOMETRY_EXPECTED_FAIL";
            for(int i=0;i<32;i++){Feed(Key.W);yield return null;yield return fixedFrame;TraceR3(i);}
            yield return Release();Require(Surface.IntrudesRock(MapVNextGroundFootprint.Circle(Foot.radius),FootPosition),"R1_COLLISION_REPLAY_DID_NOT_REPRODUCE");
            yield return Capture("Comparison/S-21193-r1-collision-replay-clean.png",true);
            File.WriteAllText(Path.Combine(output,"r1-collision-replay.json"),JsonUtility.ToJson(new BaselineReplay{actualBuild=BuildIdentity,baselineGeometryBuild="map-vnext-p2-random-20261029-r1",mode="DIAGNOSTIC_RECONSTRUCTION_NOT_ORIGINAL_EXE",seed=randomSeed,
                region=region.id,terrainHash=Layout.semanticHash,foot=FootPosition,camera=View.transform.position,cameraSize=View.orthographicSize,prefixPaths=old,newFullPaths=full,expectedIntrusion=true},true));
            MapVNextProjection.Render(Map,Terrain,settings,Assets,Generation);PlaceFoot(wall+Vector2.down*1.1f,"RESET_AFTER_DIAGNOSTIC_R1_REPLAY");
        }
        [Serializable] sealed class BaselineReplay{public string actualBuild,baselineGeometryBuild,mode,region,terrainHash;public int seed,prefixPaths,newFullPaths;public Vector2 foot;public Vector3 camera;public float cameraSize;public bool expectedIntrusion;}
        IEnumerator CliffRoundTrip()
        {
            captureCameraFixed=true;overview=false;var samples=CliffSamples();Require(samples.Length>=2,"CLIFF_SAMPLE_VARIATION_MISSING");
            foreach(var region in samples)
            {
                Vector2 wall=Center(region.visual);View.orthographicSize=7.5f;
                View.transform.position=new Vector3(wall.x,wall.y+1.16000557f,-10); // r1 original follow camera at the failed pose.
                if(randomSeed==21193&&region==samples[0])yield return CaptureR1CollisionReplay(region);
                yield return HoldAtCliff(region,"LOWER_W_HOLD",wall+Vector2.down*1.1f,new[]{Key.W},64);
                yield return HoldAtCliff(region,"UPPER_S_EDGE",wall+Vector2.up*1.1f,new[]{Key.S},48);
                yield return HoldAtCliff(region,"ALONG_D",wall+new Vector2(-.7f,-1.1f),new[]{Key.D},24);
                yield return HoldAtCliff(region,"ALONG_A",wall+new Vector2(.7f,-1.1f),new[]{Key.A},24);
                yield return HoldAtCliff(region,"DIAGONAL_WD",wall+new Vector2(-.7f,-1.1f),new[]{Key.W,Key.D},48);
                yield return HoldAtCliff(region,"DIAGONAL_WA",wall+new Vector2(.7f,-1.1f),new[]{Key.W,Key.A},48);
            }
            DemonstratePlacement(samples[0]);showCliffDiagnostics=true;RebuildCliffDiagnostics();yield return Capture("Placement/"+Current.Id+"-probes-debug.png",false);
            showCliffDiagnostics=false;RebuildCliffDiagnostics();if(placementProbes!=null)DestroyImmediate(placementProbes);
            captureCameraFixed=false;activeRamp=ramps.First();PlaceFoot(Center(Map.SpawnCell),"RESET_AFTER_CLIFF_TESTS");
        }
        void DemonstratePlacement(MapVNextCliffExclusion.Region region)
        {
            if(placementProbes!=null)DestroyImmediate(placementProbes);placementProbes=new GameObject("DiagnosticPlacementProbes_NOT_FORMAL_ECOLOGY");
            var cases=new[]{("small-no-Rigidbody",MapVNextGroundFootprint.Circle(.12f)),("large-static-box",MapVNextGroundFootprint.Box(new Vector2(.65f,.45f))),
                ("trigger-drop-probe",MapVNextGroundFootprint.Circle(.22f)),("pool-reactivation-probe",MapVNextGroundFootprint.Box(new Vector2(.3f,.25f)))};
            Vector2 wall=Center(region.visual);
            int index=0;foreach(var item in cases)
            {
                var token=new GameObject("Probe_"+item.Item1);token.transform.SetParent(placementProbes.transform,false);token.SetActive(false);
                if(item.Item1.Contains("trigger")){var trigger=token.AddComponent<CircleCollider2D>();trigger.radius=.22f;trigger.isTrigger=true;}
                var sprite=token.AddComponent<SpriteRenderer>();if(placementProbeSprite==null)placementProbeSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width/.3f);sprite.sprite=placementProbeSprite;sprite.color=new Color(.3f,.8f,1);sprite.sortingOrder=999;token.transform.localScale=Vector3.one;
                foreach(bool legal in new[]{false,true})
                {
                    Vector2 requested=legal?wall+new Vector2(index*.8f-1.2f,-2):wall;
                    bool allowed=Surface.TryPlace(token,item.Item2,requested,Map.GetElevation(Terrain.Coordinates.WorldToCell(requested)),Generation,out string reason,true);
                    PlacementTrials.Add(new PlacementTrial{seed=randomSeed,category=item.Item1,shape=item.Item2.IsCircle?"circle":"convex",generation=Generation,requested=requested,expectedAllowed=legal,allowed=allowed,reason=reason,active=token.activeSelf});
                    Require(allowed==legal&&token.activeSelf==legal,"PLACEMENT_PROBE_CONTRACT:"+item.Item1);
                }
                index++;
            }
        }
        void CliffManualKeys(Keyboard k)
        {
            if(k.cKey.wasPressedThisFrame)
            {
                var samples=CliffSamples();var sample=samples[cliffPreviewIndex++%samples.Length];Vector2 wall=Center(sample.visual);
                PlaceFoot(wall+Vector2.down*1.1f,"USER_CLIFF_STATION_SAFE_RESET_NOT_TRAVERSAL");captureCameraFixed=true;View.orthographicSize=7.5f;View.transform.position=new Vector3(wall.x,wall.y+1.16000557f,-10);
                RebuildCliffDiagnostics();
                layoutError="Cliff station "+sample.id+" / W wall / S upper edge via stairs / F2 diagnostics / O placement probes";
            }
            if(k.f2Key.wasPressedThisFrame){showCliffDiagnostics=!showCliffDiagnostics;RebuildCliffDiagnostics();}
            if(k.oKey.wasPressedThisFrame){DemonstratePlacement(CliffSamples()[0]);showCliffDiagnostics=true;RebuildCliffDiagnostics();}
        }
        void RebuildCliffDiagnostics()
        {
            if(cliffFootVisual!=null)DestroyImmediate(cliffFootVisual);cliffFootVisual=null;
            if(cliffDiagnostics!=null)DestroyImmediate(cliffDiagnostics);cliffDiagnostics=null;if(!showCliffDiagnostics)return;
            cliffDiagnostics=new GameObject("DiagnosticRockNavigationFootprint_NOT_GAME_ENTITIES");
            void Line(string name,Vector2[] p,Color color,float width)
            {var go=new GameObject(name);go.transform.SetParent(cliffDiagnostics.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=trajectoryMaterial;line.useWorldSpace=true;line.positionCount=p.Length+1;line.SetPositions(p.Concat(new[]{p[0]}).Select(v=>new Vector3(v.x,v.y,-.2f)).ToArray());line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sortingOrder=1002;
                if(name=="actual-foot"){cliffFootVisual=go;line.transform.SetParent(Foot.transform,false);line.transform.localPosition=Foot.offset;line.useWorldSpace=false;line.SetPositions(p.Concat(new[]{p[0]}).Select(v=>(Vector3)(v-FootPosition)).ToArray());}
            }
            foreach(var region in Surface.Exclusion.Regions)if(Vector2.Distance(Center(region.visual),View.transform.position)<18)Line(region.id,region.vertices,new Color(1,.2f,.1f,.85f),.025f);
            var edges=Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");
            for(int i=0;i<edges.pathCount;i++){var path=edges.GetPath(i).Select(v=>(Vector2)edges.transform.TransformPoint(v)).ToArray();if(Vector2.Distance(path[0],View.transform.position)<18)Line("actual-physics",path,new Color(1,1,.2f,.5f),.012f);}
            Line("actual-foot",Enumerable.Range(0,40).Select(i=>(Vector2)Foot.bounds.center+new Vector2(Mathf.Cos(i*Mathf.PI/20),Mathf.Sin(i*Mathf.PI/20))*Foot.radius).ToArray(),Color.cyan,.03f);
            var center=Terrain.Coordinates.WorldToCell(View.transform.position);var footprint=MapVNextGroundFootprint.Circle(Foot.radius);
            for(int y=-7;y<=7;y++)for(int x=-11;x<=11;x++){Vector2 p=Center(center+new Vector2Int(x,y));if(!Surface.CanOccupy(footprint,p,-1,out _))continue;Line("legal-node",new[]{p+Vector2.left*.05f,p+Vector2.up*.05f,p+Vector2.right*.05f,p+Vector2.down*.05f},Color.green,.015f);}
        }
        void FixedUpdate()
        {
            if(!monitoringCliff||Foot==null||!Surface.Ready)return;
            var footprint=MapVNextGroundFootprint.Circle(Foot.radius);Vector2 at=FootPosition;
            if(Surface.SweepIntrudesRock(footprint,previousCliffFoot,at))cliffIntrusion=true;
            // Existing thin edge bands include proactive clearance beyond the actual rock plane.
            // Record their solver contacts separately; never relax the rock region or its .002 tolerance.
            if(!Surface.CanSweep(footprint,previousCliffFoot,at,-1,out _))cliffBoundaryContacts++;
            previousCliffFoot=at;TraceR3(cliffFixedSamples++);
        }
        void WriteCliffEvidence(string failure)
        {
            File.WriteAllText(Path.Combine(output,"cliff-evidence.json"),JsonUtility.ToJson(new CliffRun{identity=BuildIdentity,status=failure==null?"PASS":"FAIL",geometryIdentity=MapVNextCliffExclusion.GeometryIdentity,
                contactTolerance=MapVNextSurfaceQuery.ContactTolerance,seeds=CliffSeeds,trials=CliffTrials.ToArray(),placements=PlacementTrials.ToArray(),surfaces=SurfaceRows.ToArray()},true));
        }
    }
}
