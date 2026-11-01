using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    public static partial class MapVNextRandomLayout
    {
        /// <summary>Retains every base material, road, water, origin and protected endpoint. Never fills water to fit a plateau.</summary>
        public static MapData GenerateOnTerrain(MapData basis, int seed, int protectedRadius, int maxAttempts = 12)
        {
            if (basis == null || basis.Width < 36 || basis.Height < 34 || basis.HasElevation || maxAttempts < 1 || maxAttempts > 32)
                throw new ArgumentException("UnsupportedFormalVNextBase");
            var failures = new List<string>();
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var map = basis.Copy(); var rng = new System.Random(Mix(seed, attempt, 0x13572b19));
                    var stairsRng = new System.Random(Mix(seed, attempt, 0x24683c2a));
                    var regions = new List<HashSet<Vector2Int>>(); var heights = new List<int>(); var allRoots = new HashSet<Vector2Int>();
                    int requestedRoots = rng.Next(1, 3);
                    for (int root = 0; root < requestedRoots; root++)
                    {
                        HashSet<Vector2Int> chosen = SupportedRoot(map, protectedRadius, allRoots, rng);
                        if (chosen == null) throw new Rejected("FORMAL_ROOT_NO_SUPPORTED_LAND:" + root);
                        allRoots.UnionWith(chosen); regions.Add(chosen); heights.Add(1);
                        foreach (var cell in chosen) map.SetElevation(cell, 1);
                        var parent = chosen;
                        int requestedHeight = rng.Next(1, 5);
                        for (int height = 2; height <= requestedHeight; height++)
                        {
                            // World-cell erosion keeps the already accepted support/clear-landing invariant.
                            var inner = new HashSet<Vector2Int>(parent.Where(p =>
                            {
                                for (int y = -3; y <= 3; y++) for (int x = -3; x <= 3; x++)
                                    if (!parent.Contains(p + new Vector2Int(x, y))) return false;
                                return true;
                            }));
                            if (inner.Count < 36 || !Connected(inner) || Bounds(inner).width < 6 || Bounds(inner).height < 6) break;
                            regions.Add(inner); heights.Add(height); parent = inner;
                            foreach (var cell in inner) map.SetElevation(cell, height);
                        }
                    }
                    for (int i = 0; i < regions.Count; i++) foreach (var cell in regions[i]) map.SetElevation(cell, heights[i]);
                    var reserved = new HashSet<Vector2Int>();
                    for (int i = 0; i < regions.Count; i++)
                    {
                        int level = heights[i]; var ports = new List<Port>();
                        foreach (var high in Ordered(regions[i])) foreach (var d in new[] { Vector2Int.right, Vector2Int.left })
                        {
                            var low = high - d;
                            if (map.GetElevation(high) != level || !ClearPort(map, low, d, level)) continue;
                            var reservation = Reservation(low, d);
                            if (!reservation.Any(reserved.Contains)) ports.Add(new Port { low = low, high = high, reservation = reservation });
                        }
                        if (ports.Count == 0) throw new Rejected("FORMAL_NO_CLEAR_SIDE_PORT:H" + level);
                        var port = ports[stairsRng.Next(ports.Count)]; Pair(map, port); reserved.UnionWith(port.reservation);
                    }
                    MapVNextFixture.Validate(map);
                    int walkable = 0; for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++) if (map.IsWalkable(map.Origin + new Vector2Int(x, y))) walkable++;
                    if (Reachable(map) != walkable) throw new Rejected("FORMAL_UNREACHABLE_LAND");
                    return map;
                }
                catch (Rejected failure) { failures.Add(attempt + ":" + failure.Message); }
            }
            throw new MapPlanningException("VNextFormalLayoutRejected", string.Join(";", failures), false);
        }
        private static HashSet<Vector2Int> SupportedRoot(MapData map, int protectedRadius, HashSet<Vector2Int> occupied, System.Random random)
        {
            bool Eligible(Vector2Int p) => map.IsInside(p) && !map.IsBorder(p, 2) && map.IsWalkable(p) && map.GetElevation(p) == 0 &&
                map.GetCell(p).terrainType != MapTerrainType.Path &&
                Mathf.Max(Math.Abs(p.x-map.SpawnCell.x), Math.Abs(p.y-map.SpawnCell.y)) > protectedRadius+2 &&
                Mathf.Max(Math.Abs(p.x-map.ExitCell.x), Math.Abs(p.y-map.ExitCell.y)) > 5 && !occupied.Contains(p) &&
                Dirs.All(d => map.IsInside(p+d) && map.IsWalkable(p+d) && !occupied.Contains(p+d));
            var anchors = new List<Port>();
            for (int y=map.Origin.y+2;y<map.Origin.y+map.Height-4;y++)
            for (int x=map.Origin.x+3;x<map.Origin.x+map.Width-3;x++)
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left })
            {
                var low=new Vector2Int(x,y); bool clear=true;
                for(int u=-3;clear&&u<=3;u++) for(int v=-2;clear&&v<=4;v++)
                {
                    if(u>0&&v < -1)continue;var p=low+d*u+Vector2Int.up*v;
                    clear=map.IsInside(p)&&map.IsWalkable(p)&&map.GetElevation(p)==0&&(u<=0||Eligible(p));
                }
                if(clear)anchors.Add(new Port { low=low,high=low+d });
            }
            Shuffle(anchors,random);
            for(int trial=0;trial<128&&anchors.Count>0;trial++)
            {
                var anchor=anchors[trial%anchors.Count];var d=anchor.high-anchor.low;
                int maximum=trial%3==0?25:17,w=random.Next(8,maximum),h=random.Next(8,maximum);
                int left=d.x>0?anchor.low.x+1:anchor.low.x-w;
                int bottom=anchor.low.y-1-random.Next(h-5);
                var cells=Shape(new RectInt(left,bottom,w,h),random);
                cells.RemoveWhere(p=>!Eligible(p));
                var required=new HashSet<Vector2Int>();
                for(int u=1;u<=3;u++)for(int v=-1;v<=4;v++)required.Add(anchor.low+d*u+Vector2Int.up*v);
                cells.UnionWith(required);
                // Opening by a 3x3 square removes unsupported narrow appendages without filling water or paths.
                var centers=new HashSet<Vector2Int>(cells.Where(p=>
                {
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(!cells.Contains(p+new Vector2Int(x,y)))return false;
                    return true;
                }));
                var opened=new HashSet<Vector2Int>();
                foreach(var center in centers)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)opened.Add(center+new Vector2Int(x,y));
                if(!required.All(opened.Contains))continue;
                var connected=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();connected.Add(anchor.high);queue.Enqueue(anchor.high);
                while(queue.Count>0){var p=queue.Dequeue();foreach(var step in Dirs)if(opened.Contains(p+step)&&connected.Add(p+step))queue.Enqueue(p+step);}
                if(connected.Count<48||!required.All(connected.Contains))continue;
                var candidate=map.Copy();foreach(var p in connected)candidate.SetElevation(p,1);
                if(ClearPort(candidate,anchor.low,d,1))return connected;
            }
            return null;
        }
    }
}
