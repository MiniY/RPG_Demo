using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MapVNext;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class MapVNextCliffTests
{
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    [UnityTest] public IEnumerator ActualFoot_CannotOccupyRenderedSouthWallCore()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextRandom.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;var d=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
        Assert.IsTrue(d.PlayerSpawnValidatedBeforeActivation);Assert.IsTrue(d.LoadRandom(21193));yield return null;
        string dest=Environment.GetEnvironmentVariable("RPG_CLIFF_EVIDENCE");Directory.CreateDirectory(dest);
        var face=d.Terrain.WaterBaseTilemap.transform.parent.Find("GeneratedElevation1Face").GetComponent<Tilemap>();
        var positions=new List<Vector3Int>();foreach(var pos in face.cellBounds.allPositionsWithin)positions.Add(pos);
        var cells=positions.Where(c=>face.GetTile(c)!=null&&d.Assets.Role(face.GetTile<Tile>(c))==35&&Enumerable.Range(-2,5).All(x=>face.GetTile(c+new Vector3Int(x,0,0))!=null)).OrderBy(c=>c.y).ThenBy(c=>c.x).ToArray();
        Assert.Greater(cells.Length,0,"Need actual long south-face interior, away from stair");
        Vector3Int cell=cells[0];Vector2 wall=face.GetCellCenterWorld(cell);
        var edges=face.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");
        var keyboard=InputSystem.AddDevice<Keyboard>("CliffReproductionKeyboard");
        typeof(MapVNextDemo).GetField("automationKeyboard",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(d,keyboard);
        Call(d,"PlaceFoot",wall+Vector2.down*1.1f,"RESET_CLIFF_BASELINE_PROBE");
        d.View.orthographicSize=6.5f;d.View.transform.position=new Vector3(wall.x,wall.y,-10);
        d.Overlay.enabled=false;
        try
        {
            for(int i=0;i<32;i++)
            {
                Call(d,"Feed",new object[]{new[]{Key.W}});
                yield return null;yield return new WaitForFixedUpdate();Call(d,"TraceR3",i);
            }
            yield return (IEnumerator)Call(d,"Release");
            var rt=new RenderTexture(1280,720,24);d.View.targetTexture=rt;d.View.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(dest,"south-wall-clean.png"),tex.EncodeToPNG());d.View.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllLines(Path.Combine(dest,"movement.csv"),d.Trace);
            Vector2 foot=d.Foot.bounds.center;
            // Independently check the opaque interior of the actual face tile, not cell elevation or the future query.
            var core=new Rect(wall.x-.35f,wall.y-.25f,.7f,.5f);
            bool intrusion=Vector2.Distance(foot,new Vector2(Mathf.Clamp(foot.x,core.xMin,core.xMax),Mathf.Clamp(foot.y,core.yMin,core.yMax)))<d.Foot.radius-.002f;
            File.WriteAllText(Path.Combine(dest,"reproduction.json"),JsonUtility.ToJson(new Repro{build=MapVNextRandomLayout.Identity,seed=21193,cell=cell,wall=wall,foot=foot,footRadius=d.Foot.radius,actualHeight=d.ActualElevation,wallCore=core,intrusion=intrusion,wallCenterHasCollider=edges.OverlapPoint(wall),colliderPaths=edges.pathCount,colliderEnabled=edges.enabled,isTrigger=edges.isTrigger,layer=edges.gameObject.layer,layerIgnored=Physics2D.GetIgnoreLayerCollision(edges.gameObject.layer,d.Foot.gameObject.layer)},true));
            Assert.IsFalse(intrusion,"Actual .30 foot occupies rendered south wall while remaining on the same lower elevation");
        }
        finally{InputSystem.RemoveDevice(keyboard);}
    }
    [UnityTest] public IEnumerator AllFootprints_RejectWholeOccupancySweepAndActivationBeforePublication()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextRandom.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        var d=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();Assert.IsTrue(d.LoadRandom(21193));var query=d.Terrain.GetComponent<MapVNextSurfaceQuery>();
        var region=query.Exclusion.Regions.First(r=>r.kind=="SOUTH_FACE"&&r.visual==new Vector2Int(14,3));Vector2 wall=d.Terrain.Coordinates.CellToWorld(region.visual);
        var shapes=new[]{MapVNextGroundFootprint.Circle(.1f),MapVNextGroundFootprint.Circle(.3f),MapVNextGroundFootprint.Circle(.65f),MapVNextGroundFootprint.Box(new Vector2(.7f,.45f)),MapVNextGroundFootprint.Convex(new[]{new Vector2(0,-.3f),new Vector2(.5f,0),new Vector2(0,.3f),new Vector2(-.5f,0)})};
        foreach(var shape in shapes)
        {
            Assert.IsFalse(query.CanOccupy(shape,wall,-1,out _));Assert.IsTrue(query.CanOccupy(shape,wall+Vector2.down*1.5f,-1,out _));
            Assert.IsFalse(query.CanSweep(shape,wall+Vector2.down*1.5f,wall+Vector2.up*2,-1,out _),"Both endpoints are safe, whole segment crosses rock");
        }
        var circle=MapVNextGroundFootprint.Circle(.3f);
        Vector2 outside=d.Terrain.Coordinates.CellToWorld(d.Map.Origin+new Vector2Int(31,-3));
        Assert.IsFalse(query.CanOccupy(circle,outside,-1,out _),"Outside-map requests must reject without indexing MapData");
        Assert.IsFalse(query.CanSweep(circle,wall+Vector2.down*1.5f,outside,-1,out _));
        Assert.IsFalse(query.CanOccupy(MapVNextGroundFootprint.Circle(2),d.Terrain.Coordinates.CellToWorld(d.Map.Origin+Vector2Int.one),-1,out _),"Center inside but footprint extends past border");
        Assert.IsFalse(query.CanOccupy(circle,wall+Vector2.down*.6f,-1,out _),"Center outside rock, actual radius reaches inside");
        var observedLegacyBandContact=new Vector2(11.67383f,6.400349f);
        Assert.IsFalse(query.SweepIntrudesRock(circle,observedLegacyBandContact,observedLegacyBandContact),"Real R6 contact remains outside projected rock");
        Assert.IsFalse(query.CanOccupy(circle,observedLegacyBandContact,0,out _),"Navigation keeps its original proactive boundary clearance");
        Assert.IsTrue(query.CanSweep(circle,wall+new Vector2(-.5f,-1.2f),wall+new Vector2(.5f,-1.2f),-1,out _));
        var token=new GameObject("Independent_NoPhysics_Static_And_Pool_Probe");token.SetActive(false);
        try
        {
            var previous=token.transform.position;
            Assert.IsFalse(query.TryPlace(token,circle,wall,0,query.Generation,out _,true));Assert.IsFalse(token.activeSelf);Assert.AreEqual(previous,token.transform.position);
            Assert.IsTrue(query.TryPlace(token,circle,wall+Vector2.down*1.5f,0,query.Generation,out _,true));Assert.IsTrue(token.activeSelf);
            token.SetActive(false);previous=token.transform.position;
            Assert.IsFalse(query.TryPlace(token,circle,wall,0,query.Generation,out _,true));Assert.IsFalse(token.activeSelf);Assert.AreEqual(previous,token.transform.position);
            Assert.IsTrue(query.TryPlace(token,circle,wall+new Vector2(.5f,-1.5f),0,query.Generation,out _,true));
            token.SetActive(false);var trigger=token.AddComponent<CircleCollider2D>();trigger.isTrigger=true;trigger.radius=.24f;trigger.offset=Vector2.down*.4f;
            var actual=MapVNextGroundFootprint.FromCollider(trigger,out var offset);Assert.Less(Vector2.Distance(new Vector2(0,-.4f),offset),.00001f);
            Assert.IsFalse(query.TryPlace(token,actual,wall,0,query.Generation,out _,true,offset));Assert.IsFalse(token.activeSelf);
            Vector2 legal=wall+Vector2.down*1.5f;Assert.IsTrue(query.TryPlace(token,actual,legal,0,query.Generation,out _,true,offset));Physics2D.SyncTransforms();Assert.Less(Vector2.Distance(legal,trigger.bounds.center),.0001f);
            string old=query.Generation;int oldPaths=query.Exclusion.Regions.Length;
            Assert.IsTrue(d.LoadRandom(42));query=d.Terrain.GetComponent<MapVNextSurfaceQuery>();Assert.AreNotEqual(old,query.Generation);
            token.SetActive(false);Assert.IsFalse(query.TryPlace(token,circle,legal,0,old,out string rejection,true));Assert.AreEqual("STALE_GENERATION",rejection);
            Assert.AreEqual(1,d.Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Count(c=>c.name=="GeneratedCliffEdges"));
            Assert.IsTrue(d.LoadRandom(21193));Assert.AreEqual(oldPaths,query.Exclusion.Regions.Length);
            Vector2 before=d.Foot.bounds.center;var exception=Assert.Throws<TargetInvocationException>(()=>Call(d,"PlaceFoot",wall,"REJECTED_RESET_PROBE"));Assert.That(exception.InnerException.Message,Does.StartWith("RESET_REJECTED:"));Assert.Less(Vector2.Distance(before,d.Foot.bounds.center),.00001f);
        }
        finally{UnityEngine.Object.DestroyImmediate(token);}
    }
    [UnityTest] public IEnumerator NativeRock_AllSupportedHeightsAndNonzeroOriginHaveSameProjectedCoverage()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextRandom.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        var d=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();var offset=new Vector2Int(-37,19);
        for(int h=0;h<4;h++)
        {
            var source=MapVNextFixture.Build(MapVNextFixture.Cases()[h]);var translated=new MapData(source.Width,source.Height,offset);
            for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++){var p=new Vector2Int(x,y);translated.SetTerrain(p+offset,source.GetCell(p).terrainType);translated.SetElevation(p+offset,source.GetElevation(p));}
            foreach(var stair in source.Stairs)translated.AddStair(stair.LowerCell+offset,stair.UpperCell+offset,stair.VisualComposition,stair.VisualSide,stair.VisualPlacement);
            translated.SpawnCell=source.SpawnCell+offset;translated.ExitCell=source.ExitCell+offset;
            MapVNextProjection.Render(translated,d.Terrain,d.Assets.SourceSettings,d.Assets,"translated-"+h);
            var q=d.Terrain.GetComponent<MapVNextSurfaceQuery>();var region=q.Exclusion.Regions.First(r=>r.elevation==h+1&&r.visual.x==10+offset.x&&r.owner.y==5+offset.y);
            Vector2 core=d.Terrain.Coordinates.CellToWorld(region.visual);Assert.IsFalse(q.CanOccupy(MapVNextGroundFootprint.Circle(.12f),core,-1,out _));
            var face=d.Terrain.WaterBaseTilemap.transform.parent.Find("GeneratedElevation"+(h+1)+"Face").GetComponent<Tilemap>();Assert.IsNotNull(face.GetTile(new Vector3Int(region.visual.x,region.visual.y,0)));
            var collider=d.Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges");Assert.IsTrue(collider.OverlapPoint(core));
            Assert.IsTrue(q.CanOccupy(MapVNextGroundFootprint.Circle(.3f),d.Terrain.Coordinates.CellToWorld(translated.ExitCell),h+1,out _),"Legal grass top remains usable");
        }
    }
    [UnityTest] public IEnumerator ActualDifferentSizedDynamicColliders_CannotMoveThroughRock()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextRandom.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        var d=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();Assert.IsTrue(d.LoadRandom(21193));var q=d.Terrain.GetComponent<MapVNextSurfaceQuery>();Vector2 wall=d.Terrain.Coordinates.CellToWorld(new Vector2Int(14,3));
        foreach(float radius in new[]{.12f,.65f})
        {
            var actor=new GameObject("Independent_Dynamic_Circle_Probe_"+radius);actor.SetActive(false);var body=actor.AddComponent<Rigidbody2D>();body.gravityScale=0;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var foot=actor.AddComponent<CircleCollider2D>();foot.radius=radius;foot.sharedMaterial=d.Foot.sharedMaterial;
            try
            {
                var shape=MapVNextGroundFootprint.FromCollider(foot,out var offset);Assert.IsTrue(q.TryPlace(actor,shape,wall+Vector2.down*1.5f,0,q.Generation,out _,true,offset));Physics2D.SyncTransforms();
                Vector2 previous=foot.bounds.center;for(int i=0;i<48;i++){body.MovePosition(body.position+Vector2.up*.12f);yield return new WaitForFixedUpdate();Vector2 at=foot.bounds.center;Assert.IsFalse(q.IntrudesRock(shape,at));Assert.IsTrue(q.CanSweep(shape,previous,at,-1,out _));previous=at;}
                Assert.Less(foot.bounds.center.y,wall.y-.3f);
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
        }
        // Formal Player stays at its safe spawn; these independent probes never need its activation lifecycle.
    }
    [UnityTest] public IEnumerator FirstOnEnable_ObservesLegalGroundAndCurrentGeneration()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextRandom.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        var d=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();Assert.IsTrue(d.PlayerSpawnValidatedBeforeActivation);
        var q=d.Terrain.GetComponent<MapVNextSurfaceQuery>();Vector2 wall=d.Terrain.Coordinates.CellToWorld(new Vector2Int(14,3));
        var actor=new GameObject("Independent_Activation_Observer");actor.SetActive(false);
        try
        {
            var foot=actor.AddComponent<CircleCollider2D>();foot.radius=.2f;foot.offset=Vector2.down*.4f;foot.isTrigger=true;
            var observer=actor.AddComponent<CliffActivationObserver>();observer.Query=q;observer.Foot=foot;observer.ExpectedGeneration=q.Generation;
            var shape=MapVNextGroundFootprint.FromCollider(foot,out var offset);
            Assert.IsFalse(q.TryPlace(actor,shape,wall,0,q.Generation,out _,true,offset));Assert.AreEqual(0,observer.Calls);
            Assert.IsTrue(q.TryPlace(actor,shape,wall+Vector2.down*1.5f,0,q.Generation,out _,true,offset));Assert.AreEqual(1,observer.Calls);Assert.IsTrue(observer.LegalAtFirstEnable);
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Serializable] sealed class Repro{public string build;public int seed,actualHeight,colliderPaths,layer;public Vector3Int cell;public Vector2 wall,foot;public float footRadius;public Rect wallCore;public bool intrusion,wallCenterHasCollider,colliderEnabled,isTrigger,layerIgnored;}
}

public sealed class CliffActivationObserver:MonoBehaviour
{
    public MapVNextSurfaceQuery Query;public CircleCollider2D Foot;public string ExpectedGeneration;public int Calls;public bool LegalAtFirstEnable;
    void OnEnable(){Calls++;Vector2 p=Foot.transform.TransformPoint(Foot.offset);LegalAtFirstEnable=Query.Ready&&Query.Generation==ExpectedGeneration&&Query.CanOccupy(MapVNextGroundFootprint.FromCollider(Foot,out _),p,-1,out _);}
}
