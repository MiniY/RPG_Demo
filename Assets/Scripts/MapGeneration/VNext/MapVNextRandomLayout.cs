using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MapVNext
{
    [Serializable]
    public sealed class MapVNextLayoutConfig
    {
        public int width=96,height=72,blockSize=4,minRoots=2,maxRoots=4,maxElevation=4,maxAttempts=12,placementTrials=128;
        public string identity="map-vnext-random-layout-config-v1";
        public string Digest() => MapVNextRandomLayout.Hash(JsonUtility.ToJson(this));
    }
    [Serializable] public sealed class LayoutAttempt { public int attempt,layoutStream,stairStream;public string status,reason;public float milliseconds; }
    [Serializable] public sealed class LayoutPlatform
    {
        public int id,parentId,elevation,footprintArea,sameHeightArea,x,y,width,height,turns;
        public string normalizedShapeHash;
    }
    [Serializable] public sealed class LayoutStair
    {
        public int platformId,parentId,lowerHeight,upperHeight;public string side;
        public Vector2Int lower,upper;
    }
    [Serializable] public sealed class LayoutSummary
    {
        public string identity,configuration,configurationHash,semanticHash,status,reason;
        public int seed,attempt,maxElevation,reachableCells,walkableCells,roots;
        public int surfaceWalkableCells,surfaceReachableCells;public string spatialSemanticHash;
        public float milliseconds;
        public LayoutAttempt[] attempts;public LayoutPlatform[] platforms;public LayoutStair[] stairs;
    }
    public sealed class MapVNextLayoutResult { public MapData Map;public LayoutSummary Summary; }

    /// <summary>A candidate strategy only. Temporary footprint sets are discarded; MapData is the committed terrain authority.</summary>
    public static partial class MapVNextRandomLayout
    {
        public const string Identity="map-vnext-p2-random-20261029-r7";
        public static readonly int[] Seeds={21193,1,-1,0,42,103,2026,7727,31013,65537,104729,int.MaxValue,
            314159,271828,8675309,123456789,int.MinValue,-2026,-42,17,73,4099,65521,987654321};
        public static readonly int[] PilotSeeds={21193,42,-2026};
        public static readonly int[] ReplaySeeds={21193,-1,int.MinValue};
        static readonly Vector2Int[] Dirs={Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
        sealed class Region {public int id,parent,height;public HashSet<Vector2Int> macro;}
        sealed class Rejected : Exception {public Rejected(string message):base(message){}}
        sealed class Port {public Vector2Int low,high;public HashSet<Vector2Int> reservation;}

        public static int Mix(int seed,int attempt,int channel)
        {
            unchecked
            {
                uint x=(uint)seed ^ ((uint)attempt*0x9e3779b9u) ^ (uint)channel;
                x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return (int)x;
            }
        }
        public static string Hash(string value)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
        static IEnumerable<Vector2Int> Ordered(IEnumerable<Vector2Int> cells)=>cells.OrderBy(p=>p.y).ThenBy(p=>p.x);
        static RectInt Bounds(IEnumerable<Vector2Int> cells)
        {var a=cells.ToArray();int x=a.Min(p=>p.x),y=a.Min(p=>p.y);return new RectInt(x,y,a.Max(p=>p.x)-x+1,a.Max(p=>p.y)-y+1);}
        static void Shuffle<T>(IList<T> values,System.Random random)
        {for(int i=values.Count-1;i>0;i--){int j=random.Next(i+1);T t=values[i];values[i]=values[j];values[j]=t;}}
        static bool Connected(HashSet<Vector2Int> cells)
        {
            if(cells.Count==0)return false;var seen=new HashSet<Vector2Int>();var q=new Queue<Vector2Int>();
            var first=Ordered(cells).First();q.Enqueue(first);seen.Add(first);
            while(q.Count>0){var p=q.Dequeue();foreach(var d in Dirs)if(cells.Contains(p+d)&&seen.Add(p+d))q.Enqueue(p+d);}
            return seen.Count==cells.Count;
        }
        static HashSet<Vector2Int> Shape(RectInt box,System.Random rng)
        {
            var cells=new HashSet<Vector2Int>();
            foreach(var p in box.allPositionsWithin)cells.Add(p);
            var corners=new List<int>{0,1,2,3};Shuffle(corners,rng);
            int cuts=1+rng.Next(3);
            foreach(int corner in corners.Take(cuts))
            {
                int w=1+rng.Next(Math.Max(1,(box.width-2)/2)),h=1+rng.Next(Math.Max(1,(box.height-2)/2));
                var cut=new RectInt(corner%2==0?box.xMin:box.xMax-w,corner<2?box.yMin:box.yMax-h,w,h);
                foreach(var p in cut.allPositionsWithin)cells.Remove(p);
            }
            return cells;
        }
        static HashSet<Vector2Int> Erode(HashSet<Vector2Int> parent)
        {
            var result=new HashSet<Vector2Int>();
            foreach(var p in Ordered(parent))
            {bool supported=true;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(!parent.Contains(p+new Vector2Int(x,y)))supported=false;if(supported)result.Add(p);}
            return result;
        }
        static HashSet<Vector2Int> Reservation(Vector2Int low,Vector2Int direction)
        {
            // Native slope spans u=0..1, v=-.083..2.073; .30 foot uses the accepted
            // approach (-1.5,-.95) and exit (1.85,2.4). Reserve surrounding full cells.
            var result=new HashSet<Vector2Int>();
            for(int u=-3;u<=3;u++)for(int y=-2;y<=4;y++)result.Add(low+direction*u+Vector2Int.up*y);
            return result;
        }
        static bool ClearPort(MapData map,Vector2Int low,Vector2Int direction,int height)
        {
            for(int u=-3;u<=3;u++)for(int y=-2;y<=4;y++)
            {
                // The native shoulder also requires a closed cliff immediately below
                // its two lanes. A concave turn below it must not become a same-level bypass.
                if(u>0&&y < -1)continue;
                var p=low+direction*u+Vector2Int.up*y;
                if(!map.IsInside(p)||!map.IsWalkable(p)||map.GetElevation(p)!=(u<=0?height-1:height))return false;
            }
            return true;
        }
        static List<Port> Ports(MapData map,Region region,HashSet<Vector2Int> used,int block)
        {
            var list=new List<Port>();
            for(int y=2;y<map.Height-4;y++)for(int x=3;x<map.Width-3;x++)
            {
                var low=new Vector2Int(x,y);
                if(map.GetElevation(low)!=region.height-1)continue;
                foreach(var direction in new[]{Vector2Int.right,Vector2Int.left})
                {
                    var high=low+direction;
                    if(map.GetElevation(high)!=region.height||!region.macro.Contains(new Vector2Int(high.x/block,high.y/block)))continue;
                    if(!ClearPort(map,low,direction,region.height))continue;
                    var reservation=Reservation(low,direction);
                    if(reservation.Any(used.Contains))continue;
                    list.Add(new Port{low=low,high=high,reservation=reservation});
                }
            }
            return list;
        }
        public static bool HasLegalPort(MapData map,int height)
        {
            for(int y=2;y<map.Height-4;y++)for(int x=3;x<map.Width-3;x++)
            foreach(var d in new[]{Vector2Int.right,Vector2Int.left})
                if(ClearPort(map,new Vector2Int(x,y),d,height))return true;
            return false;
        }
        static void Pair(MapData map,Port port)
        {
            var d=port.high-port.low;
            for(int lane=0;lane<2;lane++)map.AddStair(port.low+Vector2Int.up*lane,port.high+Vector2Int.up*lane,
                MapStairVisualComposition.UpperAndLower,d.x>0?MapStairVisualSide.LeftOnly:MapStairVisualSide.RightOnly);
        }
        static MapData Build(int seed,MapVNextLayoutConfig cfg,int attempt,out List<Region> regions,out List<LayoutStair> links)
        {
            int block=cfg.blockSize,gw=cfg.width/block,gh=cfg.height/block;
            var rng=new System.Random(Mix(seed,attempt,0x13572b19));
            var stairsRng=new System.Random(Mix(seed,attempt,0x24683c2a));
            regions=new List<Region>();var roots=new List<Region>();var occupied=new HashSet<Vector2Int>();
            int count=rng.Next(cfg.minRoots,cfg.maxRoots+1);
            for(int root=0;root<count;root++)
            {
                HashSet<Vector2Int> chosen=null;
                for(int trial=0;trial<cfg.placementTrials;trial++)
                {
                    int w=rng.Next(5,10),h=rng.Next(5,10);
                    var box=new RectInt(rng.Next(1,gw-w-1),rng.Next(1,gh-h-1),w,h);
                    var shape=Shape(box,rng);bool disjoint=true;
                    foreach(var p in shape)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(occupied.Contains(p+new Vector2Int(x,y)))disjoint=false;
                    if(!disjoint)continue;chosen=shape;break;
                }
                if(chosen==null)throw new Rejected("ROOT_PLACEMENT_NO_CLEAR_SPACE:"+root);
                var region=new Region{id=regions.Count+1,parent=0,height=1,macro=chosen};regions.Add(region);roots.Add(region);occupied.UnionWith(chosen);
                int requested=rng.Next(1,cfg.maxElevation+1);
                for(int height=2;height<=requested;height++)
                {
                    var inner=Erode(region.macro);
                    if(inner.Count<6||!Connected(inner))break;
                    var bounds=Bounds(inner);
                    if(bounds.width<2||bounds.height<3)break;
                    // Cropping an already supported footprint produces off-centre terraces,
                    // without a second height grid or unsupported multi-grade cliff.
                    int l=bounds.width>3?rng.Next(2):0,rr=bounds.width>3?rng.Next(2):0;
                    int b=bounds.height>4?rng.Next(2):0,t=bounds.height>4?rng.Next(2):0;
                    var crop=new RectInt(bounds.x+l,bounds.y+b,bounds.width-l-rr,bounds.height-b-t);
                    inner.RemoveWhere(p=>!crop.Contains(p));
                    if(inner.Count<6||!Connected(inner)||Bounds(inner).width<2||Bounds(inner).height<3)break;
                    region=new Region{id=regions.Count+1,parent=region.id,height=height,macro=inner};regions.Add(region);
                }
            }
            var map=new MapData(cfg.width,cfg.height,Vector2Int.zero){GenerationSeed=seed,SpawnCell=new Vector2Int(2,2)};
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)if(map.IsBorder(new Vector2Int(x,y),1))map.SetTerrain(new Vector2Int(x,y),MapTerrainType.Mountain);
            foreach(var region in regions)
            foreach(var p in Ordered(region.macro))
            for(int y=0;y<block;y++)for(int x=0;x<block;x++)map.SetElevation(p*block+new Vector2Int(x,y),region.height);
            links=new List<LayoutStair>();var reserved=new HashSet<Vector2Int>();
            foreach(var region in regions.OrderBy(r=>r.height).ThenBy(r=>r.id))
            {
                var candidates=Ports(map,region,reserved,block);
                if(candidates.Count==0)throw new Rejected("NO_LEGAL_SIDE_PORT:P"+region.id+":H"+region.height);
                var port=candidates[stairsRng.Next(candidates.Count)];Pair(map,port);reserved.UnionWith(port.reservation);
                links.Add(new LayoutStair{platformId=region.id,parentId=region.parent,lowerHeight=region.height-1,upperHeight=region.height,
                    side=port.high.x>port.low.x?"West":"East",lower=port.low,upper=port.high});
            }
            var highest=regions.OrderByDescending(v=>v.height).ThenBy(v=>v.id).First();
            map.ExitCell=links.First(l=>l.platformId==highest.id).upper+Vector2Int.up*2;
            Validate(map,cfg);return map;
        }
        public static void Validate(MapData map,MapVNextLayoutConfig cfg)
        {
            MapVNextFixture.Validate(map);
            var used=new HashSet<Vector2Int>();
            foreach(var s in map.Stairs)
            {
                if(map.IsStairTransition(s.LowerCell+Vector2Int.down,s.UpperCell+Vector2Int.down))continue;
                if(!ClearPort(map,s.LowerCell,s.UpperCell-s.LowerCell,map.GetElevation(s.UpperCell)))throw new InvalidOperationException("P2_PORT_CLEARANCE_MISSING");
                foreach(var p in Reservation(s.LowerCell,s.UpperCell-s.LowerCell))if(!used.Add(p))throw new InvalidOperationException("P2_SLOPE_RESERVATIONS_OVERLAP");
            }
            int walkable=0;
            for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
            {
                var p=new Vector2Int(x,y);if(!map.IsWalkable(p))continue;walkable++;
                if(map.GetElevation(p)<0||map.GetElevation(p)>cfg.maxElevation)throw new NotSupportedException("P2_HEIGHT_OUT_OF_RANGE");
                foreach(var d in Dirs)if(map.IsInside(p+d)&&map.IsWalkable(p+d)&&Math.Abs(map.GetElevation(p)-map.GetElevation(p+d))>1)throw new InvalidOperationException("P2_UNSUPPORTED_MULTI_GRADE_DROP");
            }
            if(Reachable(map)!=walkable)throw new InvalidOperationException("P2_UNREACHABLE_PLAYABLE_CELLS");
        }
        public static int Reachable(MapData map)
        {
            var seen=new HashSet<Vector2Int>{map.SpawnCell};var q=new Queue<Vector2Int>();q.Enqueue(map.SpawnCell);
            while(q.Count>0){var p=q.Dequeue();foreach(var d in Dirs)if(map.CanTraverse(p,p+d)&&seen.Add(p+d))q.Enqueue(p+d);}
            return seen.Count;
        }
        public static string SemanticHash(MapData map)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(map.Width);writer.Write(map.Height);writer.Write(map.Origin.x);writer.Write(map.Origin.y);
                for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){var p=map.Origin+new Vector2Int(x,y);writer.Write((int)map.GetCell(p).terrainType);writer.Write(map.GetElevation(p));}
                foreach(var s in map.Stairs.OrderBy(s=>s.LowerCell.y).ThenBy(s=>s.LowerCell.x).ThenBy(s=>s.UpperCell.y).ThenBy(s=>s.UpperCell.x))
                {writer.Write(s.LowerCell.x);writer.Write(s.LowerCell.y);writer.Write(s.UpperCell.x);writer.Write(s.UpperCell.y);}
                writer.Write(map.SpawnCell.x);writer.Write(map.SpawnCell.y);writer.Write(map.ExitCell.x);writer.Write(map.ExitCell.y);writer.Flush();
                using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        static LayoutPlatform Describe(MapData map,Region r,int block)
        {
            var cells=new HashSet<Vector2Int>();
            // Derive each region's committed footprint from MapData, including supported
            // child terraces. The builder's macro mask only identifies this component.
            foreach(var m in r.macro)for(int y=0;y<block;y++)for(int x=0;x<block;x++)
            {var p=m*block+new Vector2Int(x,y);if(map.GetElevation(p)>=r.height)cells.Add(p);}
            var box=Bounds(cells);int turns=0;
            for(int y=box.yMin;y<=box.yMax;y++)for(int x=box.xMin;x<=box.xMax;x++)
            {int n=0;foreach(var d in new[]{new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(-1,0),Vector2Int.zero})if(cells.Contains(new Vector2Int(x,y)+d))n++;if(n==1||n==3)turns++;}
            string normalized=string.Join(";",Ordered(cells).Select(p=>(p.x-box.xMin)+":"+(p.y-box.yMin)));
            return new LayoutPlatform{id=r.id,parentId=r.parent,elevation=r.height,footprintArea=cells.Count,sameHeightArea=cells.Count(p=>map.GetElevation(p)==r.height),
                x=box.xMin,y=box.yMin,width=box.width,height=box.height,turns=turns,normalizedShapeHash=Hash(normalized)};
        }
        public static MapVNextLayoutResult Generate(int seed,MapVNextLayoutConfig cfg=null)
        {
            cfg=cfg??new MapVNextLayoutConfig();
            if(cfg.maxElevation<1||cfg.maxElevation>4)throw new NotSupportedException("P2_SUPPORTS_H0_TO_H4_ONLY");
            if(cfg.blockSize!=4||cfg.width<64||cfg.height<56||cfg.width%4!=0||cfg.height%4!=0||cfg.minRoots<1||cfg.maxRoots<cfg.minRoots||cfg.maxAttempts<1||cfg.maxAttempts>32)
                throw new ArgumentException("P2_UNSUPPORTED_CONFIGURATION");
            var attempts=new List<LayoutAttempt>();var clock=System.Diagnostics.Stopwatch.StartNew();
            for(int attempt=0;attempt<cfg.maxAttempts;attempt++)
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var record=new LayoutAttempt{attempt=attempt,layoutStream=Mix(seed,attempt,0x13572b19),stairStream=Mix(seed,attempt,0x24683c2a)};
                try
                {
                    var map=Build(seed,cfg,attempt,out var regions,out var links);watch.Stop();record.status="SUCCESS";record.reason="ALL_CONSTRAINTS_SATISFIED";record.milliseconds=(float)watch.Elapsed.TotalMilliseconds;attempts.Add(record);
                    clock.Stop();var platforms=regions.Select(v=>Describe(map,v,cfg.blockSize)).ToArray();
                    return new MapVNextLayoutResult{Map=map,Summary=new LayoutSummary{identity=Identity,configuration=JsonUtility.ToJson(cfg),configurationHash=cfg.Digest(),seed=seed,attempt=attempt,
                        semanticHash=SemanticHash(map),status="SUCCESS",reason="ALL_PLAYABLE_PLATFORMS_CONNECTED",maxElevation=platforms.Max(p=>p.elevation),
                        roots=platforms.Count(p=>p.parentId==0),reachableCells=Reachable(map),walkableCells=Enumerable.Range(0,map.Width*map.Height).Count(i=>map.IsWalkable(new Vector2Int(i%map.Width,i/map.Width))),
                        platforms=platforms,stairs=links.ToArray(),attempts=attempts.ToArray(),milliseconds=(float)clock.Elapsed.TotalMilliseconds}};
                }
                catch(Rejected failure){watch.Stop();record.status="REJECTED";record.reason=failure.Message;record.milliseconds=(float)watch.Elapsed.TotalMilliseconds;attempts.Add(record);}
            }
            clock.Stop();return new MapVNextLayoutResult{Summary=new LayoutSummary{identity=Identity,configuration=JsonUtility.ToJson(cfg),configurationHash=cfg.Digest(),seed=seed,status="REJECTED",reason="BOUNDED_ATTEMPTS_EXHAUSTED",attempts=attempts.ToArray(),platforms=new LayoutPlatform[0],stairs=new LayoutStair[0],milliseconds=(float)clock.Elapsed.TotalMilliseconds}};
        }
    }
}
