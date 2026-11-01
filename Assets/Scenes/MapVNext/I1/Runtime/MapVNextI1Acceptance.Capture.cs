using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MapVNext;

public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class ShapeRow {public string kind;public bool trigger,enabled;public Vector2 centre;public float radius;public Vector2[] vertices;}
    [Serializable] private sealed class EntityRow {public string id,role,instance;public Vector2 position;public List<ShapeRow> shapes=new List<ShapeRow>();}
    [Serializable] private sealed class CellRow {public Vector2Int cell;public string terrain;public int elevation;public bool walkable;}
    [Serializable] private sealed class World
    {
        public string buildId,generation,mapDigest,hostCheck;public int seed,width,height;public Vector2Int origin,spawn,exit;
        public List<CellRow> cells=new List<CellRow>();public MapVNextCliffExclusion.Region[] rock;
        public List<EntityRow> entities=new List<EntityRow>();
    }
    private void FrameMetadata(string name,string capturePhase=null)
    {
        Vector2 p=foot==null?Vector2.zero:FootPosition,b=body==null?Vector2.zero:body.position;
        frames.Add(string.Join(",",name,F(Time.fixedTime),Time.frameCount,capturePhase??trial,keys,F(b.x),F(b.y),F(p.x),F(p.y),map==null?"":Context.ActiveGenerationId.ToString(),MapHostBuildStamp.BuildId));
    }
    private IEnumerator CaptureFrame(string name,string capturePhase=null)
    {
        yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
        try {Require(texture!=null&&texture.width>0,"ActualPlayerFrameUnavailable");string path=Path.Combine(output,name);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());FrameMetadata(name,capturePhase);}
        finally {if(texture!=null)Destroy(texture);}
    }
    private void CaptureOverview(string name)
    {
        var camera=Camera.main;Require(camera!=null,"ActualGameCameraMissing");
        var bound=map.TilemapRenderer.Coordinates.CellBoundsToWorld(Context.ActiveMap);
        Vector3 position=camera.transform.position;float size=camera.orthographicSize;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var overlays=FindObjectsOfType<Canvas>(true).Where(c=>c.enabled).ToArray();
        var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24);var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        try
        {
            foreach(var canvas in overlays)canvas.enabled=false;
            camera.transform.position=new Vector3(bound.center.x,bound.center.y,position.z);
            camera.orthographicSize=Mathf.Max(bound.extents.y+2,(bound.extents.x+2)/camera.aspect);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            string path=Path.Combine(output,name);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());FrameMetadata(name,"Overview");
        }
        finally
        {
            foreach(var canvas in overlays)if(canvas!=null)canvas.enabled=true;
            camera.transform.position=position;camera.orthographicSize=size;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);Destroy(texture);
        }
        evidence.checks.Add("Overview is actual same-Player render with temporary camera extent and hidden UI; no entity relocation; "+name);
    }
    private void ExportMap(int seed)
    {
        var data=Context.ActiveMap;var world=new World {buildId=MapHostBuildStamp.BuildId,generation=Context.ActiveGenerationId.ToString(),mapDigest=MapVNextRandomLayout.SemanticHash(data),hostCheck=MapHostCompatibility.LastCompatibilityResult,seed=data.GenerationSeed,width=data.Width,height=data.Height,origin=data.Origin,spawn=data.SpawnCell,exit=data.ExitCell,rock=MapVNextCliffExclusion.Derive(data,map.TilemapRenderer.Coordinates).Regions};
        for(int y=data.Origin.y;y<data.Origin.y+data.Height;y++)for(int x=data.Origin.x;x<data.Origin.x+data.Width;x++)
        {var cell=new Vector2Int(x,y);world.cells.Add(new CellRow {cell=cell,terrain=data.GetCell(cell).terrainType.ToString(),elevation=data.GetElevation(cell),walkable=data.IsWalkable(cell)});}
        foreach(var entry in Context.ActiveRegistry.Entries)
        {
            Transform actor=(entry.Instance as Component)?.transform??(entry.Instance as GameObject)?.transform;
            var entity=new EntityRow {id=entry.LogicalObjectId,role=entry.Role,instance=actor==null?"CONSTRAINT_NO_INSTANCE":actor.name,position=actor==null?Vector2.zero:(Vector2)actor.position};world.entities.Add(entity);
            if(actor==null)continue;
            foreach(var collider in actor.GetComponentsInChildren<Collider2D>(true))
            {
                var shape=new ShapeRow {kind=collider.GetType().Name,trigger=collider.isTrigger,enabled=collider.enabled,centre=collider.transform.TransformPoint(collider.offset)};
                if(collider is CircleCollider2D circle)shape.radius=circle.radius*Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x),Mathf.Abs(circle.transform.lossyScale.y));
                else if(collider is BoxCollider2D box)
                {Vector2 h=box.size/2;shape.vertices=new[]{new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),h,new Vector2(-h.x,h.y)}.Select(p=>(Vector2)box.transform.TransformPoint(p+box.offset)).ToArray();}
                else if(collider is PolygonCollider2D polygon&&polygon.pathCount==1)shape.vertices=polygon.GetPath(0).Select(p=>(Vector2)polygon.transform.TransformPoint(p+polygon.offset)).ToArray();
                else shape.kind+="_DISPLAY_BOUNDS_ONLY_NOT_GROUND_PROOF";
                entity.shapes.Add(shape);
            }
            var declared=actor.GetComponent<MapGroundFootprint>();if(declared!=null)entity.shapes.Add(new ShapeRow {kind="ExplicitGroundPolygon",vertices=declared.Vertices.Select(p=>(Vector2)actor.TransformPoint(p)).ToArray()});
        }
        string directory=Path.Combine(output,"World");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"Seed-"+seed+"-"+world.generation+".json"),JsonUtility.ToJson(world,true));
    }
}
