using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapVNext
{
    /// <summary>Explicit VNext adapter over retained terrain projection; MapData owns terrain and heights.</summary>
    public static class MapVNextProjection
    {
        public static int TopRole(MapData map,Vector2Int p,int h)
        {
            bool Filled(Vector2Int q)=>map.IsInside(q)&&map.IsWalkable(q)&&map.GetElevation(q)>=h;
            bool n=!Filled(p+Vector2Int.up),s=!Filled(p+Vector2Int.down);
            bool w=!Filled(p+Vector2Int.left),e=!Filled(p+Vector2Int.right);
            foreach(var edge in map.Stairs)
            {
                if(edge.UpperCell!=p)continue;
                bool bottom=!map.IsStairTransition(edge.LowerCell+Vector2Int.down,edge.UpperCell+Vector2Int.down);
                // The lower shoulder keeps the actual perimeter; a south cap exists only at a real drop. The upper shoulder opens into the plateau.
                // This role follows the atomic pair and its side, independent of fixture cells/seed.
                if(!bottom)
                {if(edge.UpperCell.x>edge.LowerCell.x)w=false;else e=false;}
            }
            int x=w?(e?3:0):(e?2:1);
            return (n?(s?28:4):(s?20:12))+x;
        }
        public static void Render(MapData map, MapTilemapRenderer terrain, MapGenerationSettings settings, MapVNextAssets assets,string generation=null, MapSpatialContext formalContext=null)
        {
            var surface=terrain.GetComponent<MapVNextSurfaceQuery>();
            if(surface==null && formalContext==null)surface=terrain.gameObject.AddComponent<MapVNextSurfaceQuery>();
            surface?.Invalidate();
            MapVNextFixture.Validate(map);
            terrain.RenderStandardTerrainV21(map,settings,assets.SourcePalette);
            Transform grid=terrain.WaterBaseTilemap.transform.parent;
            foreach(var tm in grid.GetComponentsInChildren<Tilemap>(true))
            {
                int h=-1;bool ground=tm==terrain.GrassOverlayTilemap;
                bool top=tm.name.StartsWith("GeneratedElevation")&&tm.name.EndsWith("Top");
                bool face=tm.name.StartsWith("GeneratedElevation")&&tm.name.EndsWith("Face");
                if(top||face)
                {string suffix=top?"Top":"Face";if(!int.TryParse(tm.name.Substring(18,tm.name.Length-18-suffix.Length),out h))continue;}
                if(!ground&&h<0)continue;
                foreach(var cell in tm.cellBounds.allPositionsWithin)
                {
                    Tile tile=tm.GetTile<Tile>(cell);if(tile==null)continue;
                    var xy=new Vector2Int(cell.x,cell.y);
                    int level=ground&&map.IsInside(xy)?map.GetElevation(xy):h;
                    if(level<0||level>4)throw new NotSupportedException("UnsupportedGrassElevation");
                    int role=assets.Role(tile);
                    if(top)role=TopRole(map,xy,level);
                    if(face)
                    {if(role>=20&&role<=23)role+=14;else if(role>=28&&role<=31)role+=12;}
                    tm.SetTile(cell,assets.Native(level,role));
                }
            }
            foreach(var s in map.Stairs)
            {
                if(map.IsStairTransition(s.LowerCell+Vector2Int.down,s.UpperCell+Vector2Int.down))continue;
                int high=map.GetElevation(s.UpperCell);
                var face=grid.Find("GeneratedElevation"+high+"Face").GetComponent<Tilemap>();
                bool west=s.UpperCell.x>s.LowerCell.x;
                face.SetTile(V(s.LowerCell+Vector2Int.up),assets.Native(high,west?32:33));
                face.SetTile(V(s.LowerCell),assets.Native(high,west?38:39));
            }
            if(formalContext!=null) {formalContext.Require(formalContext.GenerationId,map);formalContext.ProjectBoundaries(terrain);}
            else {MapVNextStairGeometry.ProjectBoundaries(map,terrain,assets);surface.Rebuild(map,terrain,generation??Guid.NewGuid().ToString("N"));}
            terrain.TerrainCollisionTilemap.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
            Physics2D.SyncTransforms();
        }
        public static Vector3Int V(Vector2Int v)=>new Vector3Int(v.x,v.y,0);
    }
}
