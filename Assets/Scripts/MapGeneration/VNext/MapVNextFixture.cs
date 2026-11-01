using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MapVNext
{
    [Serializable]
    public sealed class StairCase
    {
        public string Id, Side, Context;
        public int Lower, Seed, Width, Height, PortOffset;
        public bool SeedChain;
        public Vector2Int LowerPort, UpperPort;
        public override string ToString() => Id;
    }

    /// <summary>Finite acceptance fixtures, not a production generation recipe.</summary>
    public static class MapVNextFixture
    {
        public static readonly int[] FixedSeeds = { 21193, 1, -1, 0, 42, 103, 2026, 7727, 31013, 65537, 104729, int.MaxValue };
        public static readonly string[] Contexts = { "OpenLanding", "LongCliff", "CornerClearance" };

        public static List<StairCase> Cases()
        {
            var result = new List<StairCase>();
            for (int i = 0; i < 60; i++)
            {
                int pair = i % 4, side = i / 4 % 2, context = i / 8 % 3, variation = i / 24;
                result.Add(new StairCase { Id = $"V{i:00}-H{pair}-H{pair + 1}-{(side == 0 ? "West" : "East")}-{Contexts[context]}",
                    Side = side == 0 ? "West" : "East", Context = Contexts[context], Lower = pair, Seed = FixedSeeds[i % 12],
                    Width = 6 + variation + (context == 1 ? 1 : 0), Height = 6 + variation,
                    PortOffset = context == 2 ? 1 : 2 + variation });
            }
            foreach (int seed in FixedSeeds)
                result.Add(new StairCase { Id = "S-" + seed, Side = "Alternating", Context = "NestedFourLevels", Seed = seed, SeedChain = true });
            return result;
        }

        public static MapData Build(StairCase c)
        {
            MapData map;
            if (c.SeedChain)
            {
                map = Floor(48, 36, 0);
                var rng = new System.Random(c.Seed ^ 0x354C29B7);
                for (int h = 1; h <= 4; h++)
                {
                    var rect = new RectInt(2 + h * 4 + rng.Next(2), 3 + h * 3 + rng.Next(2), 44 - h * 8 - rng.Next(2), 30 - h * 6 - rng.Next(2));
                    Raise(map, rect, h);
                    bool west = (h + (rng.Next(2))) % 2 == 0;
                    int y = rect.yMin + 1 + rng.Next(Math.Max(1, rect.height - 3));
                    int x = west ? rect.xMin - 1 : rect.xMax;
                    Pair(map, new Vector2Int(x, y), west ? Vector2Int.right : Vector2Int.left);
                }
                map.SpawnCell = new Vector2Int(3, 5);
                map.ExitCell = new Vector2Int(23, 17);
            }
            else
            {
                if (c.Lower < 0 || c.Lower > 3) throw new NotSupportedException("UnsupportedAdjacentPair");
                map = Floor(24, 18, c.Lower);
                var core = new RectInt(8, 5, c.Width, c.Height);
                Raise(map, core, c.Lower + 1);
                int x = c.Side == "West" ? core.xMin - 1 : core.xMax;
                c.LowerPort = new Vector2Int(x, core.yMin + c.PortOffset);
                c.UpperPort = c.LowerPort + (c.Side == "West" ? Vector2Int.right : Vector2Int.left);
                Pair(map, c.LowerPort, c.UpperPort - c.LowerPort);
                map.SpawnCell = c.LowerPort - (c.UpperPort - c.LowerPort) * 2 + Vector2Int.down;
                map.ExitCell = c.UpperPort + Vector2Int.up * 2;
            }
            map.GenerationSeed = c.Seed;
            Validate(map);
            return map;
        }

        static MapData Floor(int width, int height, int lower)
        {
            var map = new MapData(width, height, Vector2Int.zero);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2Int(x, y);
                if (map.IsBorder(p, 1)) map.SetTerrain(p, MapTerrainType.Mountain);
                else map.SetElevation(p, lower);
            }
            return map;
        }

        static void Raise(MapData map, RectInt rect, int level)
        {
            foreach (Vector2Int p in rect.allPositionsWithin) map.SetElevation(p, level);
        }
        static void Pair(MapData map, Vector2Int low, Vector2Int up)
        {
            for (int lane = 0; lane < 2; lane++)
                map.AddStair(low + Vector2Int.up * lane, low + up + Vector2Int.up * lane,
                    MapStairVisualComposition.UpperAndLower,
                    up.x > 0 ? MapStairVisualSide.LeftOnly : MapStairVisualSide.RightOnly);
        }

        public static void Validate(MapData map)
        {
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.GetElevation(map.Origin + new Vector2Int(x, y)) > 4) throw new NotSupportedException("UnsupportedGrassElevation");
            var occupied = new HashSet<Vector2Int>();
            foreach (var s in map.Stairs)
            {
                Vector2Int dir = s.UpperCell - s.LowerCell;
                if (Math.Abs(dir.x) != 1 || dir.y != 0) throw new InvalidOperationException("UnsupportedSideDirection");
                if (!map.IsWalkable(s.LowerCell) || !map.IsWalkable(s.UpperCell) ||
                    map.GetElevation(s.UpperCell) != map.GetElevation(s.LowerCell) + 1) throw new InvalidOperationException("InvalidPortHeightOrTerrain");
                bool above = map.IsStairTransition(s.LowerCell + Vector2Int.up, s.UpperCell + Vector2Int.up);
                bool below = map.IsStairTransition(s.LowerCell + Vector2Int.down, s.UpperCell + Vector2Int.down);
                if (above == below) throw new InvalidOperationException("StairMustHaveExactlyTwoAtomicLanes");
                if (!map.CanTraverse(s.LowerCell - dir, s.LowerCell) || !map.CanTraverse(s.UpperCell, s.UpperCell + dir))
                    throw new InvalidOperationException("BlockedLanding");
                if (!occupied.Add(s.LowerCell) || !occupied.Add(s.UpperCell)) throw new InvalidOperationException("OverlappingPassages");
                if (map.CanTraverse(s.LowerCell + (above ? Vector2Int.down : Vector2Int.up),
                    s.UpperCell + (above ? Vector2Int.down : Vector2Int.up))) throw new InvalidOperationException("SideCliffBypass");
            }
            if (map.Stairs.Count == 0) throw new InvalidOperationException("MissingStair");
        }

        public static string Digest(MapData map)
        {
            using(var bytes = new System.IO.MemoryStream())
            using(var writer = new System.IO.BinaryWriter(bytes))
            {
                writer.Write(MapVNextAssets.Identity);writer.Write(map.GenerationSeed);writer.Write(map.Width);writer.Write(map.Height);writer.Write(map.Origin.x);writer.Write(map.Origin.y);
                for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
                {var c=map.Origin+new Vector2Int(x,y);writer.Write((int)map.GetCell(c).terrainType);writer.Write(map.GetElevation(c));}
                foreach(var s in map.Stairs){writer.Write(s.LowerCell.x);writer.Write(s.LowerCell.y);writer.Write(s.UpperCell.x);writer.Write(s.UpperCell.y);}
                writer.Flush();
                using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static List<string> NegativeResults(MapVNextAssets assets)
        {
            var results = new List<string>();
            void Reject(string name, Action action)
            {
                try { action(); } catch (ArgumentException) { results.Add(name + ":REJECTED"); return; }
                catch (InvalidOperationException) { results.Add(name + ":REJECTED"); return; }
                catch (NotSupportedException) { results.Add(name + ":UNSUPPORTED"); return; }
                throw new Exception("NegativeAccepted:" + name);
            }
            MapData Fresh() => Build(Cases()[0]);
            Reject("SameHeightFakeStair", () => { var m=Floor(12,12,0);m.AddStair(new Vector2Int(4,4),new Vector2Int(5,4)); });
            Reject("TwoGradeJump", () => { var m=Floor(12,12,0);m.SetElevation(new Vector2Int(5,4),2);m.AddStair(new Vector2Int(4,4),new Vector2Int(5,4)); });
            Reject("BlockedUpperMouth", () => {var m=Fresh();m.SetTerrain(new Vector2Int(8,7),MapTerrainType.Mountain);Validate(m);});
            Reject("BlockedLowerMouth", () => {var m=Fresh();m.SetTerrain(new Vector2Int(7,7),MapTerrainType.Mountain);Validate(m);});
            Reject("WaterEntry", () => {var m=Fresh();m.SetTerrain(new Vector2Int(7,7),MapTerrainType.DeepWater);Validate(m);});
            Reject("BlockedLanding", () => {var m=Fresh();m.SetTerrain(new Vector2Int(6,7),MapTerrainType.Mountain);Validate(m);});
            Reject("OneLaneOnly", () => {var m=Floor(12,12,0);m.SetElevation(new Vector2Int(5,4),1);m.AddStair(new Vector2Int(4,4),new Vector2Int(5,4));Validate(m);});
            Reject("ThirdLaneOverlap", () => {var m=Fresh();m.AddStair(new Vector2Int(7,9),new Vector2Int(8,9));Validate(m);});
            Reject("SideCliffBypass", () => {var m=Fresh();m.SetElevation(new Vector2Int(7,6),1);Validate(m);});
            Reject("WrongColorBinding", () => MapVNextAssets.ValidateFamilies(new[]{5,3,1,2,4}));
            Reject("SixthGrassLevel", () => assets.Native(5,13));
            Reject("DiagonalStair", () => {var m=Floor(12,12,0);m.SetElevation(new Vector2Int(5,5),1);m.AddStair(new Vector2Int(4,4),new Vector2Int(5,5));});
            Reject("OutOfBoundsPort", () => {var m=Fresh();m.AddStair(new Vector2Int(-1,4),new Vector2Int(0,4));});
            return results;
        }
    }
}
