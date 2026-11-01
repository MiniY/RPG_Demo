using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    /// <summary>Immutable semantic rock volumes derived from MapData, in the same Grid as projection.</summary>
    public sealed class MapVNextCliffExclusion
    {
        public const string GeometryIdentity="vnext-south-rock-solid-v1";
        // The interior wall occupies its projected plane; outer corner bevels exclude native cut-outs.
        // Alpha fringe/shadow is not promoted into a new walkable support surface.
        public const float BottomInset=0f, CornerInset=4f/64f;
        [Serializable] public sealed class Region
        {public string id,kind,boundary;public int elevation;public Vector2Int owner,visual;public Vector2[] vertices;}
        public readonly Region[] Regions;
        MapVNextCliffExclusion(List<Region> regions){Regions=regions.ToArray();}
        public static MapVNextCliffExclusion Derive(MapData map,MapCoordinateBoundary coordinates)
        {
            var result=new List<Region>();
            for(int y=map.Origin.y;y<map.Origin.y+map.Height;y++)for(int x=map.Origin.x;x<map.Origin.x+map.Width;x++)
            {
                var p=new Vector2Int(x,y);int h=map.GetElevation(p);var south=p+Vector2Int.down;
                if(h<=0||!map.IsWalkable(p)||!map.IsInside(south)||map.GetElevation(south)>=h||map.IsStairTransition(p,south))continue;
                // This is the same south-face ownership used by the retained projector.
                bool left=map.IsInside(p+Vector2Int.left)&&map.GetElevation(p+Vector2Int.left)>=h;
                bool right=map.IsInside(p+Vector2Int.right)&&map.GetElevation(p+Vector2Int.right)>=h;
                Vector2 o=coordinates.CellToWorld(south), dx=(Vector2)coordinates.CellToWorld(south+Vector2Int.right)-o,dy=(Vector2)coordinates.CellToWorld(south+Vector2Int.up)-o;
                Vector2 P(float u,float v)=>o+dx*(u-.5f)+dy*(v-.5f);
                // Interior rock meets its neighbours; outer lower corners follow the native bevel.
                var vertices=new[]{P(0,1),P(1,1),P(1,right?BottomInset:BottomInset+CornerInset),P(right?1:1-CornerInset,BottomInset),P(left?0:CornerInset,BottomInset),P(0,left?BottomInset:BottomInset+CornerInset)};
                result.Add(new Region{id=$"South-H{h}-{x}-{y}",kind="SOUTH_FACE",boundary=(!left||!right)?"CONVEX_CORNER":((map.IsInside(south+Vector2Int.left)&&map.GetElevation(south+Vector2Int.left)>=h)||(map.IsInside(south+Vector2Int.right)&&map.GetElevation(south+Vector2Int.right)>=h)?"CONCAVE_STEP":"STRAIGHT"),elevation=h,owner=p,visual=south,vertices=vertices.Distinct().ToArray()});
            }
            foreach(var ramp in MapVNextStairGeometry.Derive(map,coordinates))
            {
                // The lower native shoulder is solid rock below the accepted slope boundary.
                float zero=.082975f/(.799749f+.082975f);
                result.Add(new Region{id="Shoulder-"+ramp.Id,kind="LOWER_SHOULDER",elevation=ramp.UpperElevation,owner=ramp.Upper,visual=ramp.Lower,
                    vertices=new[]{ramp.Point(zero,0),ramp.Point(1,.799749f),ramp.Point(1,0)}});
            }
            return new MapVNextCliffExclusion(result);
        }
    }
}
