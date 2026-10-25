#if RPG_DEMO_SURFACE_V4
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class MapAcceptancePlayerSmoke
{
    [Serializable] private sealed class SurfaceV4VisualProof
    {
        public string status = "INCOMPLETE", scope = "Actual versioned R4 Player; original source sprites. Controlled capture camera only; no gameplay actor reposition for visual samples. Before/after shadows differ only in renderer visibility.";
        public int seed, shoreCells, waterRockGroups, highlandShadows, objectShadows, visualColliderCount, nativeRootPairs;
        public List<string> nativeRootViolations=new List<string>();
        public float shadowTint;
        public string terrainBefore, terrainAfter, layoutBefore, layoutAfter, physicsBefore, physicsAfter;
        public long staticBefore, staticAfter;
        public List<string> sourceLimits = new List<string>(), missingInThisSeed = new List<string>();
        public List<SurfaceV4VisualFrame> frames = new List<SurfaceV4VisualFrame>();
    }
    [Serializable] private sealed class SurfaceV4VisualFrame
    {
        public string file, category, tilemap;
        public Vector2 position, cameraPosition;
        public float real, world, animationTime;
        public int animationFrame, animationFrameCount;
    }
    private Vector3 CaptureSurfaceV4View(MapGenerationController controller,string file,Vector3 target,float size)
    {
        // Evidence-only camera: keep its normal size inside this map's bounds.
        // Target and camera centres are recorded separately; actors never move.
        var map=controller.Orchestrator.Context.ActiveMap;
        Vector3 a=controller.TilemapRenderer.Coordinates.CellToWorld(map.Origin);
        Vector3 b=controller.TilemapRenderer.Coordinates.CellToWorld(map.Origin+new Vector2Int(map.Width-1,map.Height-1));
        float halfWidth=size*Camera.main.aspect;
        float minX=Mathf.Min(a.x,b.x)-.5f,maxX=Mathf.Max(a.x,b.x)+.5f;
        float minY=Mathf.Min(a.y,b.y)-.5f,maxY=Mathf.Max(a.y,b.y)+.5f;
        Vector3 at=new Vector3(maxX-minX>=2*halfWidth?Mathf.Clamp(target.x,minX+halfWidth,maxX-halfWidth):(minX+maxX)*.5f,
            maxY-minY>=2*size?Mathf.Clamp(target.y,minY+size,maxY-size):(minY+maxY)*.5f,target.z);
        CaptureUnitsView(file,at,size);return at;
    }
    private static string SurfaceV4TerrainDigest(MapData map)
    {
        var text = new StringBuilder();
        for(int y=map.Origin.y;y<map.Origin.y+map.Height;y++) for(int x=map.Origin.x;x<map.Origin.x+map.Width;x++)
        { var c=new Vector2Int(x,y);text.Append((int)map.GetCell(c).terrainType).Append(':').Append(map.GetElevation(c)).Append(';'); }
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
    }
    private static string SurfaceV4Physics(Transform root) => string.Join(";",root.GetComponentsInChildren<Collider2D>(true)
        .OrderBy(c=>c.GetInstanceID()).Select(c=>c.GetInstanceID()+":"+c.enabled+":"+c.isTrigger+":"+c.shapeCount));
    private IEnumerator CollectSurfaceV4VisualEvidence(MapGenerationController controller, UnitsSeedProof seed)
    {
        var visual=controller.TilemapRenderer.GroundTilemap.transform.parent.GetComponentInChildren<MapSurfaceV4Visuals>(true);
        RequireUnits(visual!=null,"R4 visual owner missing");
        var map=controller.Orchestrator.Context.ActiveMap;
        var proof=new SurfaceV4VisualProof { seed=seed.seed,shoreCells=visual.ShoreCells,waterRockGroups=visual.WaterRocks,
            highlandShadows=visual.HighlandShadows,objectShadows=visual.ObjectShadows,
            visualColliderCount=visual.GetComponentsInChildren<Collider2D>(true).Length,
            shadowTint=((MapSurfaceV4Settings)controller.Settings).lightShadowTint,
            terrainBefore=SurfaceV4TerrainDigest(map),layoutBefore=controller.Orchestrator.Context.ActivePlacementPlan.Ecology.Signature,
            staticBefore=controller.EcologyRuntime.StaticVersion,
            physicsBefore=SurfaceV4Physics(controller.TilemapRenderer.transform.parent) };
        proof.sourceLimits.AddRange(visual.AssetLimits);
        // Independent native geometry check: roots can extend beyond a logical
        // tile even when the anchor cell itself looks legal.
        var obstacles=controller.TilemapRenderer.GroundTilemap.transform.parent.GetComponentsInChildren<Collider2D>(true)
            .Where(c=>c.enabled && !c.isTrigger && c.shapeCount>0 &&
                (c.name.StartsWith("GeneratedCliff") || c.name=="GeneratedLegacyMountainFaces_R4" || c.gameObject==controller.TilemapRenderer.TerrainCollisionTilemap.gameObject))
            .Where(c=>!(c is TilemapCollider2D t && t.usedByComposite)).ToArray();
        var roots=controller.SimpleDecorationRenderer.DecorationCollisionTilemap.GetComponents<Collider2D>()
            .Where(c=>c.enabled && !c.isTrigger && c.shapeCount>0 && !(c is TilemapCollider2D t && t.usedByComposite))
            .Select(c=>(label:"DecorationNativeRootUnion",collider:c)).ToList();
        foreach(var entry in controller.Orchestrator.Context.ActiveRegistry.Entries.Where(e=>e.Role==PlantPlacementService.PlantRole && e.Instance is Component))
            foreach(var c in ((Component)entry.Instance).GetComponentsInChildren<Collider2D>(true).Where(c=>c.enabled && !c.isTrigger && c.shapeCount>0))
                roots.Add((entry.LogicalObjectId,c));
        foreach(var root in roots)foreach(var wall in obstacles)
        {
            var gap=root.collider.Distance(wall);proof.nativeRootPairs++;
            if(!gap.isValid || gap.isOverlapped && gap.distance<-.00001f)
                proof.nativeRootViolations.Add(root.label+"/"+wall.name+" gap="+gap.distance.ToString("R"));
        }
        File.WriteAllText(Path.Combine(outputRoot,"Seed_"+seed.seed+"_R4VisualProof.json"),JsonUtility.ToJson(proof,true));
        RequireUnits(proof.nativeRootViolations.Count==0,"Actual native vegetation root overlaps terrain: "+string.Join(";",proof.nativeRootViolations));
        RequireUnits(proof.visualColliderCount==0 && proof.shoreCells>0,"Visual owner must be display only and preserve actual shoreline");
        var layers=visual.GetComponentsInChildren<Tilemap>(true);
        Vector3 World(Vector2Int cell)=>controller.TilemapRenderer.Coordinates.CellToWorld(cell);
        var samples=new List<(string category,Vector2Int cell,Tilemap layer)>();
        void ShoreSample(string category,Func<MapSurfaceV4Visuals.FoamPlacement,bool> predicate)
        {
            var entry=visual.Placements.Where(p=>p.group=="shore" && predicate(p)).OrderBy(p=>(World(p.cell)-controller.Player.position).sqrMagnitude).FirstOrDefault();
            if(entry==null){proof.missingInThisSeed.Add(category);return;}
            string layer=entry.piece.StartsWith("edge")?"SurfaceV4Shore"+entry.piece.Substring(4):"SurfaceV4Corner"+entry.piece.Substring(8);
            samples.Add((category,entry.cell,layers.Single(t=>t.name==layer)));
        }
        ShoreSample("Straight",p=>p.cardinalMask==1||p.cardinalMask==2||p.cardinalMask==4||p.cardinalMask==8);
        ShoreSample("InnerCorner",p=>p.cardinalMask==3||p.cardinalMask==6||p.cardinalMask==12||p.cardinalMask==9);
        ShoreSample("OuterDiagonal",p=>p.piece.StartsWith("diagonal"));
        ShoreSample("NarrowChannel",p=>p.cardinalMask==5||p.cardinalMask==10);
        var rock=visual.Placements.FirstOrDefault(p=>p.group.StartsWith("rock@"));
        if(rock!=null)samples.Add(("WaterRock",rock.cell,layers.Single(t=>t.name=="SurfaceV4WaterRocks")));
        else proof.missingInThisSeed.Add("WaterRock");
        foreach(var sample in samples)
        {
            var cell=new Vector3Int(sample.cell.x,sample.cell.y,0);
            int count=sample.category=="Straight"?32:sample.category=="WaterRock"?24:16;
            for(int i=0;i<count;i++)
            {
                string file="R4Visual_"+seed.seed+"_"+sample.category+"_"+i.ToString("D3")+".png";
                Vector3 cameraAt=CaptureSurfaceV4View(controller,file,World(sample.cell),seed.gameplayCameraSize);
                proof.frames.Add(new SurfaceV4VisualFrame { file=file,category=sample.category,position=World(sample.cell),cameraPosition=cameraAt,
                    real=Time.realtimeSinceStartup,world=Time.time,tilemap=sample.layer.name,
                    animationFrame=sample.layer.GetAnimationFrame(cell),animationFrameCount=sample.layer.GetAnimationFrameCount(cell),
                    animationTime=sample.layer.GetAnimationTime(cell) });
                yield return new WaitForSecondsRealtime(.08f);
            }
            var clip=proof.frames.Where(f=>f.category==sample.category).ToArray();
            RequireUnits(clip.All(f=>f.animationFrameCount>1) && clip.Select(f=>f.animationFrame).Distinct().Count()>1,
                "R4 actual tile animation did not advance: "+sample.category);
        }
        var open=new List<Vector2Int>();
        for(int y=map.Origin.y+2;y<map.Origin.y+map.Height-2;y++)for(int x=map.Origin.x+2;x<map.Origin.x+map.Width-2;x++)
        {
            var c=new Vector2Int(x,y);bool water=true;
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(!TerrainTopology.IsWater(map.GetCell(c+new Vector2Int(dx,dy)).terrainType))water=false;
            if(water && !visual.Placements.Any(p=>p.cell==c))open.Add(c);
        }
        if(open.Count>0)
        {
            var c=open.OrderBy(p=>(World(p)-controller.Player.position).sqrMagnitude).First();
            string file="R4Visual_"+seed.seed+"_OpenWater.png";Vector3 cameraAt=CaptureSurfaceV4View(controller,file,World(c),seed.gameplayCameraSize);
            proof.frames.Add(new SurfaceV4VisualFrame { file=file,category="OpenWaterNoIsolatedFoam",position=World(c),cameraPosition=cameraAt,real=Time.realtimeSinceStartup,world=Time.time });
        }
        else proof.missingInThisSeed.Add("OpenWater");
        var shadows=visual.GetComponentsInChildren<SpriteRenderer>(true);
        var enabled=shadows.Select(s=>s.enabled).ToArray();
        try
        {
            foreach(string category in new[]{"FaceShadow","ObjectShadow"})
            {
                var selected=shadows.FirstOrDefault(s=>s.name.StartsWith(category));
                if(selected==null){proof.missingInThisSeed.Add(category);continue;}
                Vector3 at=selected.transform.position;
                for(int state=0;state<2;state++)
                {
                    for(int i=0;i<shadows.Length;i++)shadows[i].enabled=state==1 && enabled[i];
                    string file="R4Shadow_"+seed.seed+"_"+category+(state==0?"_Before.png":"_After.png");
                    Vector3 cameraAt=CaptureSurfaceV4View(controller,file,at,seed.gameplayCameraSize);
                    proof.frames.Add(new SurfaceV4VisualFrame{file=file,category=category+(state==0?"/Before":"/After"),position=at,cameraPosition=cameraAt,real=Time.realtimeSinceStartup,world=Time.time});
                }
            }
        }
        finally { for(int i=0;i<shadows.Length;i++)shadows[i].enabled=enabled[i]; }
        proof.terrainAfter=SurfaceV4TerrainDigest(map);proof.layoutAfter=controller.Orchestrator.Context.ActivePlacementPlan.Ecology.Signature;
        proof.staticAfter=controller.EcologyRuntime.StaticVersion;proof.physicsAfter=SurfaceV4Physics(controller.TilemapRenderer.transform.parent);
        RequireUnits(proof.terrainBefore==proof.terrainAfter && proof.layoutBefore==proof.layoutAfter && proof.staticBefore==proof.staticAfter && proof.physicsBefore==proof.physicsAfter,
            "R4 visual capture changed terrain, ecology or native physics");
        proof.status="PASS";
        File.WriteAllText(Path.Combine(outputRoot,"Seed_"+seed.seed+"_R4VisualProof.json"),JsonUtility.ToJson(proof,true));
    }
}
#endif
