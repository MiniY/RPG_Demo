using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    /// <summary>Continuous boundary projection of the existing MapData atomic pairs. No height state.</summary>
    public sealed class MapVNextStairGeometry
    {
        public readonly Vector2Int Lower, Upper;
        public readonly bool West;
        public readonly int LowerElevation, UpperElevation;
        public readonly Vector2 Corner;
        public readonly float Width, Height;
        public string Id => $"H{LowerElevation}-H{UpperElevation}-{(West?"West":"East")}-{Lower.x}-{Lower.y}";
        public Vector2 Direction => West ? Vector2.right : Vector2.left;
        public Vector2 Entry => Point(-1.5f,-.95f);
        public Vector2 Middle => Point(.5f,1.05f);
        public Vector2 Exit => Point(1.85f,2.40f);
        public Vector2[][] Segments => new[]
        {
            new[]{Point(0f,1.103180f),Point(1f,2.072635f)},
            new[]{Point(0f,-.082975f),Point(1f,.799749f)},
            new[]{Point(1f,.799749f),Point(1f,0f)}
        };
        MapVNextStairGeometry(MapData map, MapCoordinateBoundary coordinates, MapStairConnection s)
        {
            Lower=s.LowerCell;Upper=s.UpperCell;West=Upper.x>Lower.x;
            LowerElevation=map.GetElevation(Lower);UpperElevation=map.GetElevation(Upper);
            Vector2 center=coordinates.CellToWorld(Lower);
            Width=coordinates.CellToWorld(Lower+Vector2Int.right).x-center.x;
            Height=coordinates.CellToWorld(Lower+Vector2Int.up).y-center.y;
            if(Width<=0||Height<=0)throw new InvalidOperationException("UnsupportedStairGridOrientation");
            Corner=center+new Vector2(West?-.5f*Width:.5f*Width,-.5f*Height);
        }
        public Vector2 Point(float u,float v)=>Corner+Direction*(u*Width)+Vector2.up*(v*Height);
        public Vector2 Local(Vector2 p)=>new Vector2(Vector2.Dot(p-Corner,Direction)/Width,(p.y-Corner.y)/Height);
        public string Phase(Vector2 foot)
        {
            Vector2 p=Local(foot);
            if(p.x<0)return "LOWER_APPROACH";
            if(p.x<=1)return "SLOPE";
            return "UPPER_LANDING";
        }
        public static List<MapVNextStairGeometry> Derive(MapData map,MapCoordinateBoundary coordinates)
        {
            MapVNextFixture.Validate(map);
            return map.Stairs.Where(s=>!map.IsStairTransition(s.LowerCell+Vector2Int.down,s.UpperCell+Vector2Int.down))
                .Select(s=>new MapVNextStairGeometry(map,coordinates,s)).OrderBy(s=>s.UpperElevation).ThenBy(s=>s.Lower.x).ToList();
        }
        public static Vector2[][] BuildWorldBoundaries(MapData map, MapTilemapRenderer terrain)
        {
            var paths = new List<Vector2[]>(); Transform space = terrain.TerrainCollisionTilemap.transform.parent;
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++) for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                var p = new Vector2Int(x, y); if (!map.IsWalkable(p)) continue;
                MapCandidateCollisionGeometry.AppendCliffEdge(map, terrain.WaterBaseTilemap, space, p, p + Vector2Int.right, true, paths);
                MapCandidateCollisionGeometry.AppendCliffEdge(map, terrain.WaterBaseTilemap, space, p, p + Vector2Int.up, false, paths);
            }
            paths = paths.Select(path => path.Select(p => (Vector2)space.TransformPoint(p)).ToArray()).ToList();
            foreach (var ramp in Derive(map, terrain.Coordinates))
            {
                Vector2 wall = ramp.Point(1, 2.5f); float bottom = ramp.Point(1, 2).y; int matches = 0;
                foreach (var path in paths)
                {
                    Vector2 centre = path.Aggregate(Vector2.zero, (a, b) => a + b) / path.Length;
                    if (path.Length != 4 || Vector2.Distance(centre, wall) > .0001f) continue;
                    for (int i = 0; i < path.Length; i++) if (Math.Abs(path[i].y - bottom) < .0001f) path[i].y = ramp.Segments[0][1].y;
                    matches++;
                }
                if (matches != 1) throw new InvalidOperationException("FormalUpperMouthBindingMismatch:" + ramp.Id);
                foreach (var line in ramp.Segments)
                {
                    Vector2 a = line[0], b = line[1], n = Vector2.Perpendicular(b - a).normalized * Math.Min(ramp.Width, ramp.Height) * .035f;
                    paths.Add(new[] { a + n, b + n, b - n, a - n });
                }
            }
            paths.AddRange(MapVNextCliffExclusion.Derive(map, terrain.Coordinates).Regions.Select(r => r.vertices));
            return paths.ToArray();
        }
        static void JoinUpperMouth(PolygonCollider2D edges,MapVNextStairGeometry ramp)
        {
            // The retained cell edge starts at v=2; native shoulder calibration ends at
            // v=2.072635. Join that same boundary at the visible endpoint, preserving all
            // other original cliff paths. This is a contour seam, not an extra opening.
            Vector2 wall=ramp.Point(1f,2.5f);float bottom=ramp.Point(1f,2f).y;
            float end=ramp.Segments[0][1].y;int matches=0;
            for(int i=0;i<edges.pathCount;i++)
            {
                var local=edges.GetPath(i);if(local.Length!=4)continue;
                var world=local.Select(p=>(Vector2)edges.transform.TransformPoint(p)).ToArray();
                Vector2 center=world.Aggregate(Vector2.zero,(a,b)=>a+b)/4f;
                if(Vector2.Distance(center,wall)>.0001f)continue;
                for(int j=0;j<world.Length;j++)if(Math.Abs(world[j].y-bottom)<.0001f)world[j].y=end;
                edges.SetPath(i,world.Select(p=>(Vector2)edges.transform.InverseTransformPoint(p)).ToArray());matches++;
            }
            if(matches!=1)throw new InvalidOperationException("UpperMouthOriginalEdgeBindingMismatch:"+ramp.Id);
        }
        public static void ProjectBoundaries(MapData map,MapTilemapRenderer terrain,MapVNextAssets assets)
        {
            Transform grid=terrain.WaterBaseTilemap.transform.parent;
            var edges=grid.GetComponentsInChildren<PolygonCollider2D>(true).Single(c=>c.name=="GeneratedCliffEdges");
            // RenderStandardTerrainV21 has just rebuilt this collider. Append exactly once per render;
            // no Player adapter can add/remove paths or retain another generation's boundary.
            var ramps=Derive(map,terrain.Coordinates);
            foreach(var ramp in ramps)JoinUpperMouth(edges,ramp);
            int begin=edges.pathCount;
            edges.pathCount=begin+ramps.Count*3;
            foreach(var ramp in ramps)
            foreach(var line in ramp.Segments)
            {
                Vector2 a=line[0],b=line[1];
                Vector2 normal=Vector2.Perpendicular(b-a).normalized*Math.Min(ramp.Width,ramp.Height)*.035f;
                edges.SetPath(begin++,new[]{a+normal,b+normal,b-normal,a-normal}.Select(p=>(Vector2)edges.transform.InverseTransformPoint(p)).ToArray());
            }
            // Same accepted friction as the serialized .30 foot; no runtime shape or input changes.
            edges.sharedMaterial=assets.PlayerPrefab.transform.Find("MovementFeet").GetComponent<CircleCollider2D>().sharedMaterial;
        }
    }
}
