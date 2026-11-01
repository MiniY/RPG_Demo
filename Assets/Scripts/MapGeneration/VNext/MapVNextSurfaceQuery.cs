using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    /// <summary>World-space ground footprint. Callers supply their own circle or convex outline.</summary>
    public sealed class MapVNextGroundFootprint
    {
        public readonly float Radius;
        public readonly Vector2[] Vertices;
        public bool IsCircle=>Vertices==null;
        MapVNextGroundFootprint(float radius,Vector2[] vertices){Radius=radius;Vertices=vertices;}
        public static MapVNextGroundFootprint Circle(float radius)
        {if(float.IsNaN(radius)||float.IsInfinity(radius)||radius<0)throw new ArgumentOutOfRangeException(nameof(radius));return new MapVNextGroundFootprint(radius,null);}
        public static MapVNextGroundFootprint Box(Vector2 halfSize)
        {if(halfSize.x<=0||halfSize.y<=0)throw new ArgumentOutOfRangeException(nameof(halfSize));return Convex(new[]{new Vector2(-halfSize.x,-halfSize.y),new Vector2(halfSize.x,-halfSize.y),halfSize,new Vector2(-halfSize.x,halfSize.y)});}
        public static MapVNextGroundFootprint Convex(Vector2[] outline)
        {
            if(outline==null||outline.Length<3||outline.Any(p=>!MapVNextSurfaceQuery.Finite(p)))throw new ArgumentException("InvalidGroundOutline");
            float sign=0;for(int i=0;i<outline.Length;i++){float c=Cross(outline[(i+1)%outline.Length]-outline[i],outline[(i+2)%outline.Length]-outline[(i+1)%outline.Length]);if(Math.Abs(c)<1e-7f)continue;if(sign!=0&&Math.Sign(c)!=Math.Sign(sign))throw new ArgumentException("GroundOutlineMustBeConvex");sign=c;}
            if(sign==0)throw new ArgumentException("DegenerateGroundOutline");return new MapVNextGroundFootprint(0,outline.ToArray());
        }
        public static MapVNextGroundFootprint FromCollider(Collider2D collider,out Vector2 groundOffset)
        {
            if(collider==null)throw new ArgumentNullException(nameof(collider));
            Transform anchor=collider.attachedRigidbody!=null?collider.attachedRigidbody.transform:collider.transform;
            groundOffset=(Vector2)collider.transform.TransformPoint(collider.offset)-(Vector2)anchor.position;
            if(collider is CircleCollider2D circle)return Circle(circle.radius*Mathf.Max(Math.Abs(circle.transform.lossyScale.x),Math.Abs(circle.transform.lossyScale.y)));
            if(collider is BoxCollider2D box)
            {Vector2 h=box.size/2;return Convex(new[]{new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),h,new Vector2(-h.x,h.y)}.Select(v=>(Vector2)box.transform.TransformVector(v)).ToArray());}
            if(collider is PolygonCollider2D polygon&&polygon.pathCount==1)return Convex(polygon.GetPath(0).Select(v=>(Vector2)polygon.transform.TransformVector(v)).ToArray());
            throw new NotSupportedException("GroundColliderRequiresExplicitFootprint:"+collider.GetType().Name);
        }
        internal static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }

    /// <summary>Generation-scoped query on the existing projection owner; no independent terrain or height state.</summary>
    public sealed class MapVNextSurfaceQuery : MonoBehaviour
    {
        // Contact is permitted up to this world distance; .002 is 0.13 of one native pixel.
        public const float ContactTolerance=.002f;
        public MapData Map {get;private set;}
        public MapCoordinateBoundary Coordinates {get;private set;}
        public MapVNextCliffExclusion Exclusion {get;private set;}
        public string Generation {get;private set;}
        public string SpatialHash {get;private set;}
        public bool Ready {get;private set;}
        sealed class Obstacle {public Vector2[] points;public Rect bounds;}
        Obstacle[] obstacles;
        MapVNextStairGeometry[] ramps;
        static Rect Bounds(IEnumerable<Vector2> p){var a=p.ToArray();return Rect.MinMaxRect(a.Min(v=>v.x),a.Min(v=>v.y),a.Max(v=>v.x),a.Max(v=>v.y));}
        internal static bool Finite(Vector2 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
        public void Invalidate(){Ready=false;Map=null;obstacles=null;Exclusion=null;Generation=null;}
        public void Rebuild(MapData map,MapTilemapRenderer terrain,string generation)
        {
            Invalidate();Map=map;Coordinates=terrain.Coordinates;Generation=generation;
            Exclusion=MapVNextCliffExclusion.Derive(map,Coordinates);ramps=MapVNextStairGeometry.Derive(map,Coordinates).ToArray();
            var edges=terrain.WaterBaseTilemap.transform.parent.GetComponentsInChildren<PolygonCollider2D>(true).Single(c=>c.name=="GeneratedCliffEdges");
            int begin=edges.pathCount;edges.pathCount=begin+Exclusion.Regions.Length;
            foreach(var region in Exclusion.Regions)edges.SetPath(begin++,region.vertices.Select(p=>(Vector2)edges.transform.InverseTransformPoint(p)).ToArray());
            obstacles=Enumerable.Range(0,edges.pathCount).Select(i=>{var p=edges.GetPath(i).Select(v=>(Vector2)edges.transform.TransformPoint(v)).ToArray();return new Obstacle{points=p,bounds=Bounds(p)};}).ToArray();
            SpatialHash=MapVNextRandomLayout.Hash(MapVNextCliffExclusion.GeometryIdentity+"|"+MapVNextRandomLayout.SemanticHash(map)+"|"+string.Join(";",Exclusion.Regions.SelectMany(r=>r.vertices).Select(p=>p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
            Ready=true;
        }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;float t=d.sqrMagnitude<1e-12f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
        static bool Inside(Vector2 p,Vector2[] poly)
        {bool hit=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)hit=!hit;return hit;}
        static bool Crosses(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float ab1=MapVNextGroundFootprint.Cross(b-a,c-a),ab2=MapVNextGroundFootprint.Cross(b-a,d-a),cd1=MapVNextGroundFootprint.Cross(d-c,a-c),cd2=MapVNextGroundFootprint.Cross(d-c,b-c);
            return ab1*ab2<0&&cd1*cd2<0;
        }
        static float SegmentPairDistance(Vector2 a,Vector2 b,Vector2 c,Vector2 d)=>Crosses(a,b,c,d)?0:Mathf.Min(SegmentDistance(a,c,d),SegmentDistance(b,c,d),SegmentDistance(c,a,b),SegmentDistance(d,a,b));
        static Vector2[] Hull(IEnumerable<Vector2> source)
        {
            var p=source.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();if(p.Length<=2)return p;
            var h=new List<Vector2>();foreach(var q in p){while(h.Count>=2&&MapVNextGroundFootprint.Cross(h[h.Count-1]-h[h.Count-2],q-h[h.Count-1])<=0)h.RemoveAt(h.Count-1);h.Add(q);}
            int low=h.Count;for(int i=p.Length-2;i>=0;i--){var q=p[i];while(h.Count>low&&MapVNextGroundFootprint.Cross(h[h.Count-1]-h[h.Count-2],q-h[h.Count-1])<=0)h.RemoveAt(h.Count-1);h.Add(q);}h.RemoveAt(h.Count-1);return h.ToArray();
        }
        static bool PolygonIntersects(Vector2[] a,Vector2[] b)
        {
            foreach(var p in a)if(Inside(p,b))return true;foreach(var p in b)if(Inside(p,a))return true;
            for(int i=0;i<a.Length;i++)for(int j=0;j<b.Length;j++)if(SegmentPairDistance(a[i],a[(i+1)%a.Length],b[j],b[(j+1)%b.Length])<1e-6f)return true;
            return false;
        }
        static bool Touches(MapVNextGroundFootprint foot,Vector2 a,Vector2 b,Vector2[] poly)
        {
            if(foot.IsCircle)
            {
                if(Inside(a,poly)||Inside(b,poly))return true;
                float radius=Mathf.Max(0,foot.Radius-ContactTolerance);
                for(int i=0;i<poly.Length;i++)if(SegmentPairDistance(a,b,poly[i],poly[(i+1)%poly.Length])<Mathf.Max(1e-6f,radius))return true;
                return false;
            }
            return PolygonIntersects(Hull(foot.Vertices.Select(p=>p+a).Concat(foot.Vertices.Select(p=>p+b))),poly);
        }
        // Read-only acceptance/consumer diagnostics reuse the exact R18 predicate without projecting a second owner.
        public static bool SweepIntrudesRock(MapVNextCliffExclusion exclusion,MapVNextGroundFootprint foot,Vector2 from,Vector2 to)
            => exclusion==null||foot==null||!Finite(from)||!Finite(to)||exclusion.Regions.Any(r=>Touches(foot,from,to,r.vertices));
        public bool IntrudesRock(MapVNextGroundFootprint foot,Vector2 position)=>SweepIntrudesRock(foot,position,position);
        public bool SweepIntrudesRock(MapVNextGroundFootprint foot,Vector2 from,Vector2 to)=>!Ready||foot==null||!Finite(from)||!Finite(to)||Exclusion.Regions.Any(r=>Touches(foot,from,to,r.vertices));
        public bool CanOccupy(MapVNextGroundFootprint foot,Vector2 position,int level,out string reason)=>CanSweep(foot,position,position,level,out reason);
        public bool CanSweep(MapVNextGroundFootprint foot,Vector2 from,Vector2 to,int level,out string reason)
        {
            reason="OK";if(!Ready){reason="SURFACE_NOT_READY";return false;}
            if(foot==null||!Finite(from)||!Finite(to)){reason="INVALID_FOOTPRINT_OR_POSITION";return false;}
            var pa=Coordinates.WorldToCell(from);var pb=Coordinates.WorldToCell(to);
            if(!Map.IsInside(pa)||!Map.IsInside(pb)||!Map.IsWalkable(pa)||!Map.IsWalkable(pb)){reason="NOT_WALKABLE_SURFACE";return false;}
            if(level>=0&&(Map.GetElevation(pa)!=level||Map.GetElevation(pb)!=level)){reason="WRONG_SURFACE_HEIGHT";return false;}
            Vector2 ext=foot.IsCircle?Vector2.one*foot.Radius:new Vector2(foot.Vertices.Max(p=>Math.Abs(p.x)),foot.Vertices.Max(p=>Math.Abs(p.y)));
            var bounds=Rect.MinMaxRect(Mathf.Min(from.x,to.x)-ext.x,Mathf.Min(from.y,to.y)-ext.y,Mathf.Max(from.x,to.x)+ext.x,Mathf.Max(from.y,to.y)+ext.y);
            foreach(var o in obstacles)if(bounds.Overlaps(o.bounds)&&Touches(foot,from,to,o.points)){reason="CLIFF_OR_SLOPE_SOLID";return false;}
            var lo=Coordinates.WorldToCell(new Vector2(bounds.xMin,bounds.yMin));var hi=Coordinates.WorldToCell(new Vector2(bounds.xMax,bounds.yMax));
            Vector2 dx=(Vector2)Coordinates.CellToWorld(pa+Vector2Int.right)-(Vector2)Coordinates.CellToWorld(pa),dy=(Vector2)Coordinates.CellToWorld(pa+Vector2Int.up)-(Vector2)Coordinates.CellToWorld(pa);
            for(int y=lo.y;y<=hi.y;y++)for(int x=lo.x;x<=hi.x;x++)
            {var cell=new Vector2Int(x,y);if(Map.IsInside(cell)&&Map.IsWalkable(cell))continue;Vector2 center=Coordinates.CellToWorld(cell);var poly=new[]{center-(dx+dy)/2,center+(dx-dy)/2,center+(dx+dy)/2,center+(-dx+dy)/2};if(Touches(foot,from,to,poly)){reason="FOOTPRINT_OVER_UNWALKABLE_CELL";return false;}}
            return true;
        }
        /// <summary>Shared pre-publication gate for spawn, placement, pool reuse and reset consumers.</summary>
        public bool TryPlace(GameObject entity,MapVNextGroundFootprint footprint,Vector2 position,int level,string expectedGeneration,out string reason,bool activate=false,Vector2 groundOffset=default(Vector2))
        {
            if(expectedGeneration!=Generation||!Ready){reason="STALE_GENERATION";return false;}
            if(entity==null){reason="MISSING_ENTITY";return false;}
            if(activate&&entity.activeSelf){reason="ACTIVATION_REQUIRES_INACTIVE_ENTITY";return false;}
            if(!Finite(groundOffset)){reason="INVALID_GROUND_OFFSET";return false;}
            if(!CanOccupy(footprint,position,level,out reason))return false;
            Vector2 bodyPosition=position-groundOffset;
            entity.transform.position=new Vector3(bodyPosition.x,bodyPosition.y,entity.transform.position.z);
            if(activate)entity.SetActive(true);return true;
        }
        public (int legal,int reachable) Reachability(float radius)
        {
            var footprint=MapVNextGroundFootprint.Circle(radius);var legal=new HashSet<Vector2Int>();var links=new Dictionary<Vector2Int,List<Vector2Int>>();
            for(int y=Map.Origin.y;y<Map.Origin.y+Map.Height;y++)for(int x=Map.Origin.x;x<Map.Origin.x+Map.Width;x++){var p=new Vector2Int(x,y);if(CanOccupy(footprint,Coordinates.CellToWorld(p),-1,out _))legal.Add(p);}
            foreach(var ramp in ramps)
            {
                var a=Coordinates.WorldToCell(ramp.Entry);var b=Coordinates.WorldToCell(ramp.Exit);
                if(!legal.Contains(a)||!legal.Contains(b)||!CanSweep(footprint,Coordinates.CellToWorld(a),ramp.Entry,ramp.LowerElevation,out _)||!CanSweep(footprint,ramp.Entry,ramp.Middle,-1,out _)||!CanSweep(footprint,ramp.Middle,ramp.Exit,-1,out _)||!CanSweep(footprint,ramp.Exit,Coordinates.CellToWorld(b),ramp.UpperElevation,out _))continue;
                void Link(Vector2Int u,Vector2Int v){if(!links.ContainsKey(u))links[u]=new List<Vector2Int>();links[u].Add(v);}Link(a,b);Link(b,a);
            }
            var seen=new HashSet<Vector2Int>();var q=new Queue<Vector2Int>();if(legal.Contains(Map.SpawnCell)){seen.Add(Map.SpawnCell);q.Enqueue(Map.SpawnCell);}
            var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(q.Count>0)
            {
                var p=q.Dequeue();var next=new List<Vector2Int>();if(links.TryGetValue(p,out var list))next.AddRange(list);
                foreach(var d in directions){var n=p+d;if(legal.Contains(n)&&Map.CanTraverse(p,n)&&CanSweep(footprint,Coordinates.CellToWorld(p),Coordinates.CellToWorld(n),-1,out _))next.Add(n);}
                foreach(var n in next)if(seen.Add(n))q.Enqueue(n);
            }
            return (legal.Count,seen.Count);
        }
    }
}
