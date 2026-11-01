using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapVNext
{
    public sealed partial class MapVNextDemo
    {
        [Serializable] class ProjectionRow
        {
            public string layer,tile,sprite,owner;public int x,y,cellElevation,projectionElevation,role,n,e,s,w;public Rect rect;
        }
        [Serializable] class ProjectionRows {public string identity,id,mapDigest,generation;public ProjectionRow[] cells;public RampRow[] ramps;public int cliffPaths;}
        [Serializable] class RampRow {public string id;public Vector2Int lower,upper;public Vector2 entry,middle,exit;public Vector2[] upperEdge,lowerEdge,root;}
        void SaveProjectionReadback()
        {
            var rows=new List<ProjectionRow>();Transform grid=Terrain.WaterBaseTilemap.transform.parent;
            foreach(var tm in grid.GetComponentsInChildren<Tilemap>(true))
            {
                if(tm!=Terrain.GrassOverlayTilemap&&!(tm.name.StartsWith("GeneratedElevation")&&(tm.name.EndsWith("Top")||tm.name.EndsWith("Face"))))continue;
                int level=-1;
                if(tm.name.StartsWith("GeneratedElevation"))int.TryParse(tm.name.Substring(18,tm.name.Length-18-(tm.name.EndsWith("Top")?3:4)),out level);
                foreach(var c in tm.cellBounds.allPositionsWithin)
                {
                    Tile tile=tm.GetTile<Tile>(c);if(tile==null)continue;
                    var p=new Vector2Int(c.x,c.y);int H(Vector2Int a)=>Map.IsInside(a)?Map.GetElevation(a):-1;
                    rows.Add(new ProjectionRow{layer=tm.name,x=c.x,y=c.y,cellElevation=H(p),projectionElevation=level,
                        role=Assets.Role(tile),tile=tile.name,sprite=tile.sprite.name,rect=tile.sprite.rect,
                        n=H(p+Vector2Int.up),e=H(p+Vector2Int.right),s=H(p+Vector2Int.down),w=H(p+Vector2Int.left),
                        owner=tm.name.EndsWith("Face")?"upper plateau cliff / shoulder":tm==Terrain.GrassOverlayTilemap?"terrain underlay":"elevation top; higher neighbours continue foundation"});
                }
            }
            var data=new ProjectionRows{identity=MapVNextAssets.Identity,id=Current.Id,mapDigest=MapVNextFixture.Digest(Map),generation=Generation,cells=rows.ToArray(),
                ramps=ramps.Select(r=>new RampRow{id=r.Id,lower=r.Lower,upper=r.Upper,entry=r.Entry,middle=r.Middle,exit=r.Exit,
                    upperEdge=r.Segments[0],lowerEdge=r.Segments[1],root=r.Segments[2]}).ToArray(),
                cliffPaths=grid.GetComponentsInChildren<PolygonCollider2D>().Single(c=>c.name=="GeneratedCliffEdges").pathCount};
            string dest=Path.Combine(output,"Projection",Current.Id+".json");Directory.CreateDirectory(Path.GetDirectoryName(dest));File.WriteAllText(dest,JsonUtility.ToJson(data,true));
        }
        IEnumerator CaptureR3Diagnostics()
        {
            SaveProjectionReadback();Vector3 position=View.transform.position;float size=View.orthographicSize;
            yield return Capture("BeforeMotion/"+Current.Id+"-clean.png",true);
            Vector2 point=activeRamp.Point(.5f,1.1f);View.orthographicSize=2.8f;View.transform.position=new Vector3(point.x,point.y,-10);
            yield return Capture("Interfaces/"+Current.Id+"-clean.png",true);
            var lines=new GameObject("TemporaryCellDiagnostics");lines.transform.SetParent(transform,false);
            Vector2Int center=Terrain.Coordinates.WorldToCell(point);
            for(int i=-4;i<=4;i++)
            {
                AddGridLine(new Vector3(center.x+i,center.y-4,0),new Vector3(center.x+i,center.y+4,0));
                AddGridLine(new Vector3(center.x-4,center.y+i,0),new Vector3(center.x+4,center.y+i,0));
            }
            void AddGridLine(Vector3 a,Vector3 b)
            {
                var go=new GameObject("cell");go.transform.SetParent(lines.transform,false);var l=go.AddComponent<LineRenderer>();
                l.sharedMaterial=trajectoryMaterial;l.positionCount=2;l.SetPosition(0,a);l.SetPosition(1,b);
                l.startWidth=l.endWidth=.012f;l.startColor=l.endColor=new Color(1,1,1,.4f);l.sortingOrder=999;
            }
            yield return Capture("Interfaces/"+Current.Id+"-grid.png",false);Destroy(lines);
            View.orthographicSize=size;View.transform.position=position;
            var renderers=Terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<TilemapRenderer>();
            var states=renderers.Select(r=>r.enabled).ToArray();int upper=Current.Lower+1;
            for(int i=0;i<renderers.Length;i++)if(renderers[i].name.StartsWith("GeneratedElevation"+upper))renderers[i].enabled=false;
            yield return Capture("Layers/"+Current.Id+"-lower-only.png",true);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=states[i]&&renderers[i].name.StartsWith("GeneratedElevation"+upper);
            yield return Capture("Layers/"+Current.Id+"-upper-only.png",true);
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=states[i];
            yield return Capture("Layers/"+Current.Id+"-composite.png",true);
        }
        IEnumerator CaptureTrueWaterBoundary()
        {
            Load(1);yield return null;var water=new Vector2Int(3,3);
            Map.SetElevation(water,0);Map.SetTerrain(water,MapTerrainType.DeepWater);
            MapVNextProjection.Render(Map,Terrain,settings,Assets);
            Caption.text="RENDER-ONLY REGRESSION / real H1 water-facing cap and rock retained / not a playable-connection claim";
            View.transform.position=new Vector3(4,4,-10);View.orthographicSize=3.4f;
            gallery=true;yield return Capture("Regressions/TrueWaterAndDownwardBoundary.png",false);
            Map.SetTerrain(water,MapTerrainType.Grass);Map.SetElevation(water,1);
        }
    }
}
