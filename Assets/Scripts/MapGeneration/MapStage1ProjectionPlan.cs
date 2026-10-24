using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Audited native edge roles for the explicit Stage1 projection, never an asset loader.</summary>
public sealed class MapStage1CornerSources
{
    internal readonly Tile NorthWest;
    internal readonly Tile NorthEast;
    internal readonly Tile Side;

    public MapStage1CornerSources(Tile northWest, Tile northEast, Tile side)
    { NorthWest = northWest; NorthEast = northEast; Side = side; }

    internal void Validate(Texture2D atlas)
    {
        // The opt-in caller binds audited asset references; runtime validates their source roles.
        Check(NorthWest, new Rect(320, 256, 64, 64));
        Check(NorthEast, new Rect(448, 256, 64, 64));
        Check(Side, new Rect(384, 320, 64, 64));
        void Check(Tile tile, Rect rect)
        {
            MapStage1ProjectionPlan.ValidateNative(tile, atlas);
            if (tile.sprite.rect != rect)
                throw MapStage1ProjectionPlan.Error("Stage1CornerSourceRoleInvalid", "Native corner source Rect does not match its audited role.");
        }
    }
}

/// <summary>
/// One disposable, input-bound preparation for the explicit Stage1 renderer.
/// MapData remains authoritative. Unity resources are owned and released by its projector.
/// </summary>
public sealed class MapStage1ProjectionPlan : IDisposable
{
    public sealed class Fragment
    {
        internal Fragment(int level, Vector2Int cell, Vector2Int quadrant, Tile source, bool replacement)
        { Level = level; Cell = cell; Quadrant = quadrant; Source = source; Replacement = replacement; }
        public int Level { get; }
        public Vector2Int Cell { get; }
        public Vector2Int Quadrant { get; }
        public Tile Source { get; }
        public bool Replacement { get; }
        public Rect SourceRect => new Rect(Source.sprite.rect.x + Quadrant.x * 16,
            Source.sprite.rect.y + Quadrant.y * 16, 16, 16);
    }

    internal readonly MapElevationProjector Projector;
    internal readonly MapTilemapRenderer Renderer;
    internal readonly MapData Map;
    internal readonly MapGenerationSettings Settings;
    internal readonly MapTerrainV2Palette Palette;
    internal readonly MapTerrainV2Palette H2Palette;
    internal readonly MapStage1SouthStairProjectionRequest.Projection South;
    internal readonly MapStage1CornerSources Sources;
    internal readonly Dictionary<(int Level, Vector2Int Cell), Tile> Top = new Dictionary<(int, Vector2Int), Tile>();
    internal readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
    internal readonly List<Fragment> Pieces = new List<Fragment>();
    internal sealed class CoveragePlacement
    {
        internal CoveragePlacement(string role, int level, Vector2Int logical, Vector2Int visual, Tile tile, int order)
        { Role = role; Level = level; Logical = logical; Visual = visual; Tile = tile; Order = order; }
        internal readonly string Role;
        internal readonly int Level;
        internal readonly Vector2Int Logical, Visual;
        internal readonly Tile Tile;
        internal readonly int Order;
    }
    internal readonly List<CoveragePlacement> CoverageFaces = new List<CoveragePlacement>();
    internal readonly List<CoveragePlacement> CoverageStairs = new List<CoveragePlacement>();
    internal readonly Dictionary<Texture2D, Color32[]> NativePixels = new Dictionary<Texture2D, Color32[]>();
    private string nativeBinding;
    private readonly List<CoverageFootprint> coverageWrites = new List<CoverageFootprint>();
    private readonly Dictionary<Tile, Color32[]> canonicalCoveragePixels = new Dictionary<Tile, Color32[]>();

    private sealed class CoverageFootprint
    {
        internal string Role;
        internal int Level, Order, SortingLayer;
        internal Vector2Int Cell;
        internal Tile Tile;
        internal Matrix4x4 World;
        internal Vector2[] Vertices, UV;
        internal ushort[] Triangles;
        internal Color32[] Pixels;
        internal Rect Rect;
        internal Color LayerColor;
    }

    private sealed class CoverageProof
    {
        internal bool Resolved;
        internal string Reason, Evidence;
        internal CoverageProof(bool resolved, string reason, string evidence)
        { Resolved = resolved; Reason = reason; Evidence = evidence; }
    }

    internal void PrepareCoverageFootprints()
    {
        foreach (var pair in Top)
            Add("H" + pair.Key.Level + "Top", pair.Key.Level, pair.Key.Cell, pair.Value, pair.Key.Level * 10);
        foreach (var p in CoverageFaces.Concat(CoverageStairs)) Add(p.Role, p.Level, p.Visual, p.Tile, p.Order);

        void Add(string role, int level, Vector2Int cell, Tile tile, int order)
        {
            Sprite sprite = tile.sprite;
            var write = new CoverageFootprint { Role = role, Level = level, Cell = cell, Tile = tile, Order = order,
                World = Projector.CoverageWorld(role, cell), SortingLayer = Renderer.WaterBaseTilemap.GetComponent<TilemapRenderer>().sortingLayerID,
                Vertices = sprite.vertices, UV = sprite.uv, Triangles = sprite.triangles, Rect = sprite.rect,
                LayerColor = Projector.CoverageLayerColor(role), Pixels = CropCoveragePixels(tile, level, cell, false) };
            if (!canonicalCoveragePixels.ContainsKey(tile)) canonicalCoveragePixels.Add(tile, CropCoveragePixels(tile, level, cell, true));
            coverageWrites.Add(write);
        }
    }

    internal int CoverageLayerOrder(string role, int fallback)
    {
        int[] orders = coverageWrites.Where(w => w.Role == role).Select(w => w.Order).Distinct().ToArray();
        return orders.Length == 0 ? fallback : orders.Single();
    }

    private Color32[] CropCoveragePixels(Tile tile, int level, Vector2Int cell, bool canonical)
    {
        Sprite sprite = tile.sprite; var output = new Color32[4096];
        var fragments = canonical && Owned.Contains(tile) ? Pieces.Where(p => p.Level == level && p.Cell == cell).ToArray() : Array.Empty<Fragment>();
        if (fragments.Length > 0)
        {
            if (fragments.Length != 16 || fragments.Select(p => p.Quadrant).Distinct().Count() != 16)
                throw Error("Stage1ProjectionRegionMissing", "A native composition must contain each of its16 source regions once.");
            foreach (Fragment f in fragments)
            {
                Color32[] pixels = NativePixels[f.Source.sprite.texture]; Rect r = f.SourceRect;
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    output[(f.Quadrant.y * 16 + y) * 64 + f.Quadrant.x * 16 + x] = pixels[((int)r.y + y) * f.Source.sprite.texture.width + (int)r.x + x];
            }
            return output;
        }
        Color32[] whole = NativePixels.TryGetValue(sprite.texture, out Color32[] native) ? native : sprite.texture.GetPixels32();
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            output[y * 64 + x] = whole[((int)sprite.rect.y + y) * sprite.texture.width + (int)sprite.rect.x + x];
        return output;
    }

    private string CaptureNativeBinding(bool readCurrent)
    {
        var b = new StringBuilder();
        foreach (var entry in NativePixels.OrderBy(p => p.Key.GetInstanceID()))
        {
            if (entry.Key == null) throw Error("Stage1ProjectionStale", "A native source was destroyed.");
            Color32[] pixels = readCurrent ? MapElevationProjector.ReadStage1Pixels(entry.Key) : entry.Value;
            b.Append(entry.Key.GetInstanceID()).Append(PixelHash(pixels));
        }
        return Hash(Encoding.UTF8.GetBytes(b.ToString()));
    }

    private string CaptureCoverageBinding()
    {
        var b = new StringBuilder();
        foreach (var entry in Top.OrderBy(p => p.Key.Level).ThenBy(p => p.Key.Cell.y).ThenBy(p => p.Key.Cell.x))
        { b.Append(entry.Key.Level).Append(entry.Key.Cell); TileState(b, entry.Value); }
        foreach (Fragment f in Pieces)
        { b.Append(f.Level).Append(f.Cell).Append(f.Quadrant).Append(f.Replacement); TileState(b, f.Source); }
        foreach (CoverageFootprint c in coverageWrites)
        {
            b.Append(c.Role).Append(c.Level).Append(c.Cell).Append(c.Order).Append(c.SortingLayer).Append(c.World.ToString("R"))
                .Append(c.LayerColor.ToString("R")).Append(c.Rect.ToString("R")); TileState(b, c.Tile);
            foreach (Vector2 v in c.Vertices) b.Append(v.ToString("R"));
            foreach (Vector2 uv in c.UV) b.Append(uv.ToString("R"));
            foreach (ushort index in c.Triangles) b.Append(index).Append(',');
            b.Append(PixelHash(c.Pixels));
        }
        foreach (var p in canonicalCoveragePixels.OrderBy(p => p.Key.GetInstanceID())) b.Append(p.Key.GetInstanceID()).Append(PixelHash(p.Value));
        return Hash(Encoding.UTF8.GetBytes(b.ToString()));
    }

    private static string PixelHash(Color32[] pixels)
    {
        var bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        { bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a; }
        return Hash(bytes);
    }

    internal void ReleaseCoverage()
    { coverageWrites.Clear(); canonicalCoveragePixels.Clear(); NativePixels.Clear(); CoverageFaces.Clear(); CoverageStairs.Clear(); }

    private CoverageProof ProveCoverage(MapData map, TerrainTopology.InnerCorner corner, IReadOnlyList<CoverageFootprint> observed)
    {
        CoverageProof Fail(string reason, string detail) => new CoverageProof(false, reason, detail);
        bool joint = corner.Level == 2 && corner.DiagonalLevel == 1 && (corner.Direction == "SW" || corner.Direction == "SE");
        if (!joint && !SupportedHigherStencil(map, corner))
            return Fail("CoverageNeighbourhoodUnsupported", "No verified relative higher-occupancy structural family.");
        if (!Projector.CoverageGeometrySupported()) return Fail("CoverageGeometryUnsupported", "Only the audited axis-aligned unit XY/Point mapping is supported.");

        var window = new RectInt(corner.Cell.x - 1, corner.Cell.y - 1, 3, 3);
        if (joint)
        {
            if (!RealSouthPair(map, corner)) return Fail("CoverageRealStairPairMissing", "A real selected complete south pair is required.");
            var left = map.Stairs.Single(s => South.SelectsLeft(s));
            window = new RectInt(left.UpperCell.x - 1, left.LowerCell.y, 4, 4);
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                int diagonalX = corner.Direction == "SW" ? -1 : 1;
                int required = x == diagonalX && y == -1 ? 1 : 2;
                if (map.GetElevation(corner.Cell + new Vector2Int(x, y)) != required)
                    return Fail("CoverageNeighbourhoodUnsupported", "South joint has an unverified relative neighborhood.");
            }
        }
        else if (map.Stairs.Any(s => map.GetElevation(s.UpperCell) == 2) && !RealSouthPair(map, null))
            return Fail("CoverageRealStairPairMissing", "An unselected H2 stair cannot borrow the native south projection proof.");

        bool Intersects(CoverageFootprint c)
        {
            var center = Renderer.WaterBaseTilemap.GetCellCenterWorld(new Vector3Int(window.x, window.y, 0));
            float minX = center.x - .5f, minY = center.y - .5f;
            Vector3[] vertices = c.Vertices.Select(v => c.World.MultiplyPoint3x4(v)).ToArray();
            return vertices.Any() && vertices.Max(v => v.x) >= minX && vertices.Min(v => v.x) <= minX + window.width &&
                vertices.Max(v => v.y) >= minY && vertices.Min(v => v.y) <= minY + window.height;
        }
        var writes = observed.Where(Intersects).ToList();
        foreach (CoverageFootprint expected in coverageWrites.Where(Intersects))
        {
            var c = writes.SingleOrDefault(w => w.Role == expected.Role && w.Cell == expected.Cell);
            if (c == null) return Fail(expected.Role == "H1Top" ? "CoverageMissingRequiredBase" : expected.Role == "H2Face" ?
                "CoverageMissingRequiredFace" : "CoverageSourceRoleMismatch", "Missing " + expected.Role + " at " + expected.Cell);
            if (c.Tile != expected.Tile || c.Level != expected.Level || c.Rect != expected.Rect || c.LayerColor != Color.white)
                return Fail("CoverageSourceRoleMismatch", c.Role + " source/role/Rect/tint mismatch at " + c.Cell);
            int canonicalOrder = c.Role == "H1Top" ? 10 : c.Role == "H2Top" ? 20 : c.Role == "H2Face" ? 13 : 22;
            if (c.Order != canonicalOrder || c.SortingLayer != Renderer.WaterBaseTilemap.GetComponent<TilemapRenderer>().sortingLayerID)
                return Fail("CoverageLayerOrderMismatch", c.Role + " layer order mismatch at " + c.Cell);
            if (c.World != Projector.CoverageWorld(c.Role, c.Cell) || !c.Vertices.SequenceEqual(c.Tile.sprite.vertices) ||
                !c.UV.SequenceEqual(c.Tile.sprite.uv) || !c.Triangles.SequenceEqual(c.Tile.sprite.triangles))
                return Fail("CoverageFootprintMismatch", c.Role + " actual world/geometry/UV mismatch at " + c.Cell);
            if (!CoverageMeshValid(c)) return Fail("CoverageGeometryUnsupported", c.Role + " has an unsupported native mesh or UV mapping.");
            Color32[] canonical = canonicalCoveragePixels[expected.Tile];
            if (c.Pixels.Length != canonical.Length) return Fail("CoverageSourceRoleMismatch", "Wrong RGBA footprint length.");
            for (int i = 0; i < canonical.Length; i++)
                if (!canonical[i].Equals(c.Pixels[i])) return Fail(canonical[i].a > 0 && c.Pixels[i].a == 0 ?
                    "CoverageUnexplainedExposure" : "CoverageSourceRoleMismatch", c.Role + " native RGBA mismatch at " + c.Cell + " sample" + i);
        }
        // MapData supplies required roles independently of the observed/possibly incomplete command list.
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            var cell = new Vector2Int(x, y); int level = map.GetElevation(cell);
            if (level >= 1)
                for (int h = 1; h <= level; h++)
                {
                    var p = coverageWrites.SingleOrDefault(w => w.Role == "H" + h + "Top" && w.Cell == cell);
                    if (p == null && window.Contains(cell)) return Fail(h == 1 ? "CoverageMissingRequiredBase" : "CoverageSourceRoleMismatch", "Required map top missing at " + cell);
                    if (p != null && !Owned.Contains(p.Tile) && p.Tile != Projector.ExpectedCoverageTop(this, cell, h))
                        return Fail("CoverageSourceRoleMismatch", "Native top does not match its map boundary role at " + cell);
                }
            var south = cell + Vector2Int.down;
            if (level == 2 && map.IsInside(south) && map.GetElevation(south) < 2 && !map.IsStairTransition(cell, south) && window.Contains(south))
            {
                var p = writes.SingleOrDefault(w => w.Role == "H2Face" && w.Cell == south);
                if (p == null) return Fail("CoverageMissingRequiredFace", "Map requires a front face at " + south);
                if (p.Tile != Projector.ExpectedCoverageFace(this, cell)) return Fail("CoverageSourceRoleMismatch", "Wrong native front face role at " + south);
            }
        }
        if (South != null)
            foreach (var piece in South.Pieces)
                if (window.Contains(piece.Visual) && !writes.Any(w => w.Role == "H2Stairs" && w.Cell == piece.Visual && w.Tile == piece.Tile))
                    return Fail("CoverageSourceRoleMismatch", "Missing/wrong selected native tread at " + piece.Visual);

        int unsupported = 0, h0 = 0, sampled = 0, nativeExterior = 0;
        string exteriorFailure = null;
        for (int cy = window.yMin; cy < window.yMax; cy++) for (int cx = window.xMin; cx < window.xMax; cx++)
        {
            var cell = new Vector2Int(cx, cy);
            if (!map.IsInside(cell)) return Fail("CoverageNeighbourhoodUnsupported", "Coverage window leaves MapData.");
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                Vector3 world = Renderer.WaterBaseTilemap.transform.TransformPoint(Renderer.WaterBaseTilemap.CellToLocalInterpolated(
                    new Vector3(cx + (x + .5f) / 64, cy + (y + .5f) / 64, 0)));
                bool covered = writes.Any(c => CoverageSampleAt(c, world)); sampled++;
                if (!covered)
                {
                    if (map.GetElevation(cell) == 0) { h0++; continue; }
                    var basis = writes.SingleOrDefault(c => c.Role == "H1Top" && c.Cell == cell);
                    Color32[] native = basis != null ? canonicalCoveragePixels[basis.Tile] : null;
                    bool duty = writes.Any(c =>
                    {
                        if (c.Role == "H1Top") return false;
                        Vector2 p = c.World.inverse.MultiplyPoint3x4(world);
                        return p.x >= -.5f && p.x <= .5f && p.y >= -.5f && p.y <= .5f;
                    });
                    string failure = ProveNativeExterior(map, cell, x, y, native, duty);
                    if (failure == null) nativeExterior++;
                    else { unsupported++; if (exteriorFailure == null) exteriorFailure = failure; }
                }
            }
        }
        string detail = "window=" + window + "; writes=" + writes.Count + "; samples=" + sampled + "; H0-background=" + h0 +
            "; H1/H2-unproven=" + unsupported + "; native-exterior=" + nativeExterior + ";";
        if (unsupported > 0) return Fail("CoverageNativeBoundaryUnproven/" + exteriorFailure, detail);
        return new CoverageProof(true, joint ? "SouthJointCoverageVerified" : nativeExterior > 0 ?
            "HigherOccupancyNativeExteriorVerified" : "HigherOccupancyCoverageVerified", detail);
    }

    // Only called after the same Plan has proved source, native RGBA, geometry and required roles.
    // A null reason proves one native H1 boundary sample; it is never a whole-Tile exemption.
    private static string ProveNativeExterior(MapData map, Vector2Int cell, int x, int y,
        Color32[] native, bool structuralDuty)
    {
        if (map == null || !map.IsInside(cell)) return "ExteriorUnknownMapCell";
        if (map.GetElevation(cell) != 1) return "ExteriorHeightNotH1";
        if (native == null || native.Length != 4096) return "ExteriorNativeSourceUnbound";
        if (x < 0 || x >= 64 || y < 0 || y >= 64) return "ExteriorSampleOutOfRange";
        var neighbors = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        var h0 = neighbors.Select(d => map.IsInside(cell + d) && map.GetElevation(cell + d) == 0).ToArray();
        if (!h0.Any(v => v)) return "ExteriorNoRealH0Edge";
        int qx = x / 16, qy = y / 16;
        var edges = Enumerable.Range(0, 4).Where(i => h0[i] &&
            (i == 0 ? qy == 3 : i == 1 ? qx == 3 : i == 2 ? qy == 0 : qx == 0)).ToArray();
        if (edges.Length == 0) return "ExteriorWrongRegion";
        if (native[y * 64 + x].a != 0) return "ExteriorNativeOpaque";
        foreach (int edge in edges)
        {
            var queue = new Queue<int>(); var seen = new bool[256];
            queue.Enqueue(y * 64 + x); seen[(y % 16) * 16 + x % 16] = true;
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), px = p % 64, py = p / 64;
                if (edge == 0 ? py == 63 : edge == 1 ? px == 63 : edge == 2 ? py == 0 : px == 0)
                    return structuralDuty ? "ExteriorStructuralDuty" : null;
                foreach (Vector2Int d in neighbors)
                {
                    int nx = px + d.x, ny = py + d.y;
                    if (nx < qx * 16 || nx >= (qx + 1) * 16 || ny < qy * 16 || ny >= (qy + 1) * 16) continue;
                    int local = (ny % 16) * 16 + nx % 16;
                    if (!seen[local] && native[ny * 64 + nx].a == 0)
                    { seen[local] = true; queue.Enqueue(ny * 64 + nx); }
                }
            }
        }
        return "ExteriorNoFourConnectedPath";
    }

    private bool RealSouthPair(MapData map, TerrainTopology.InnerCorner? corner)
    {
        if (South == null || South.Level != 2) return false;
        var pair = map.Stairs.Where(s => map.GetElevation(s.UpperCell) == 2).OrderBy(s => s.LowerCell.x).ToArray();
        if (pair.Length != 2 || !South.SelectsLeft(pair[0]) || pair[1].LowerCell != pair[0].LowerCell + Vector2Int.right ||
            pair[1].VisualComposition != pair[0].VisualComposition || pair.Any(s => s.VisualSide != MapStairVisualSide.Both || s.VisualPlacement != MapStairVisualPlacement.Legacy) ||
            pair.Any(s => s.UpperCell != s.LowerCell + Vector2Int.up || map.GetElevation(s.LowerCell) != 1 ||
                !map.CanTraverse(s.LowerCell, s.UpperCell) || !map.CanTraverse(s.UpperCell, s.LowerCell) ||
                !map.CanTraverse(s.LowerCell + Vector2Int.down, s.LowerCell) || !map.CanTraverse(s.UpperCell, s.UpperCell + Vector2Int.up))) return false;
        if (corner.HasValue)
        {
            var c = corner.Value; int lane = c.Direction == "SW" ? 0 : 1;
            if (c.Cell != pair[lane].UpperCell + Vector2Int.up) return false;
        }
        return South.Pieces.Count == 4 && pair.All(s => new[] { s.LowerCell, s.UpperCell }.All(logical =>
            South.Pieces.Any(p => p.Logical == logical && p.Visual == logical + Vector2Int.up)));
    }

    private static bool SupportedHigherStencil(MapData map, TerrainTopology.InnerCorner corner)
    {
        if (corner.Level != 1 || corner.DiagonalLevel != 2) return false;
        int sx = corner.Direction.EndsWith("W", StringComparison.Ordinal) ? -1 : 1;
        bool north = corner.Direction.StartsWith("N", StringComparison.Ordinal); int sy = north ? 1 : -1;
        foreach (int outer in north ? new[] { 0 } : new[] { 0, 1, 2 })
        {
            bool matches = true;
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                int required = x == sx && y == sy ? 2 : !north && y == 1 && (outer == 2 || outer == 1 && x == -sx) ? 0 : 1;
                var cell = corner.Cell + new Vector2Int(x, y);
                if (!map.IsInside(cell) || map.GetElevation(cell) != required || !map.IsWalkable(cell)) matches = false;
            }
            if (matches) return true;
        }
        return false;
    }

    private bool CoverageMeshValid(CoverageFootprint c)
    {
        Sprite sprite = c.Tile.sprite;
        if (sprite.packed || sprite.pixelsPerUnit != 64 || sprite.pivot != new Vector2(32, 32) || c.Rect.size != new Vector2(64, 64) ||
            c.Tile.transform != Matrix4x4.identity || c.Tile.color != Color.white || c.Triangles.Length % 3 != 0 || c.Vertices.Length != c.UV.Length) return false;
        for (int i = 0; i < c.Vertices.Length; i++)
        {
            Vector2 v = c.Vertices[i];
            if (v.x < -.500001f || v.x > .500001f || v.y < -.500001f || v.y > .500001f) return false;
            var uv = new Vector2((v.x * 64 + 32 + c.Rect.x) / sprite.texture.width, (v.y * 64 + 32 + c.Rect.y) / sprite.texture.height);
            if ((uv - c.UV[i]).sqrMagnitude > 1e-10f) return false;
        }
        if (c.Triangles.Any(i => i >= c.Vertices.Length)) return false;
        if (Owned.Contains(c.Tile))
        {
            if (sprite.texture.width != 576 || sprite.texture.height != 384 || c.Vertices.Length != 64 || c.Triangles.Length != 96) return false;
            for (int iy = 0; iy < 4; iy++) for (int ix = 0; ix < 4; ix++)
            {
                int start = (iy * 4 + ix) * 4; int tri = (iy * 4 + ix) * 6;
                var v = new[] { new Vector2(ix*.25f-.5f,iy*.25f-.5f), new Vector2(ix*.25f-.5f,(iy+1)*.25f-.5f),
                    new Vector2((ix+1)*.25f-.5f,(iy+1)*.25f-.5f),new Vector2((ix+1)*.25f-.5f,iy*.25f-.5f) };
                if (!c.Vertices.Skip(start).Take(4).SequenceEqual(v) ||
                    !c.Triangles.Skip(tri).Take(6).Select(t => (int)t).SequenceEqual(new[] { start,start+1,start+2,start+2,start+3,start })) return false;
            }
        }
        return true;
    }

    private static bool CoverageSampleAt(CoverageFootprint c, Vector3 world)
    {
        Vector2 p = c.World.inverse.MultiplyPoint3x4(world);
        if (p.x < -.5f || p.x > .5f || p.y < -.5f || p.y > .5f) return false;
        for (int i = 0; i < c.Triangles.Length; i += 3)
        {
            int ia = c.Triangles[i], ib = c.Triangles[i+1], ic = c.Triangles[i+2];
            Vector2 a = c.Vertices[ia], u = c.Vertices[ib] - a, v = c.Vertices[ic] - a, q = p - a;
            float det = u.x * v.y - u.y * v.x; if (Mathf.Abs(det) < 1e-10f) continue;
            float s = (q.x*v.y-q.y*v.x)/det, t = (u.x*q.y-u.y*q.x)/det;
            if (s < 0 || t < 0 || s+t > 1) continue;
            Vector2 uv = c.UV[ia] + s*(c.UV[ib]-c.UV[ia]) + t*(c.UV[ic]-c.UV[ia]);
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x*c.Tile.sprite.texture.width-c.Rect.x),0,63);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y*c.Tile.sprite.texture.height-c.Rect.y),0,63);
            if (c.Pixels[y*64+x].a > 0) return true;
        }
        return false;
    }

    private readonly Func<string> layerBinding;
    private string inputBinding;
    private string resourceBinding;
    internal int State; // 0 prepared, 1 consumed/displayed, 2 released
    internal bool Foundation;

    internal MapStage1ProjectionPlan(MapElevationProjector projector, MapTilemapRenderer renderer,
        MapData map, MapGenerationSettings settings, MapTerrainV2Palette palette, MapTerrainV2Palette h2,
        MapStage1SouthStairProjectionRequest.Projection south, MapStage1CornerSources sources, Func<string> captureLayers)
    { Projector = projector; Renderer = renderer; Map = map; Settings = settings; Palette = palette;
      H2Palette = h2; South = south; Sources = sources; layerBinding = captureLayers; }

    public string Status => State == 0 ? "Prepared" : State == 1 ? "Consumed" : "Released";
    public IReadOnlyList<Fragment> Fragments => Pieces.AsReadOnly();
    public IReadOnlyList<UnityEngine.Object> RuntimeResources => Owned.ToArray();
    public int RuntimeTextureCount => Owned.OfType<Texture2D>().Count();
    public long TexturePixelBytes => Owned.OfType<Texture2D>().Sum(t => (long)t.width * t.height * 4);
    public TileBase GetTopTile(int level, Vector2Int cell) => Top.TryGetValue((level, cell), out Tile tile) ? tile : null;

    public sealed class CornerDecision
    {
        internal CornerDecision(TerrainTopology.InnerCorner corner, bool resolved, string reason, string evidence)
        { Cell = corner.Cell; Level = corner.Level; Direction = corner.Direction; DiagonalLevel = corner.DiagonalLevel;
          Resolved = resolved; Reason = reason; Evidence = evidence; }
        public Vector2Int Cell { get; }
        public int Level { get; }
        public string Direction { get; }
        public int DiagonalLevel { get; }
        public bool Resolved { get; }
        public string Reason { get; }
        public string Evidence { get; }
    }

    /// <summary>Only the bounded native NW/NE replacement contract is currently proved.
    /// Higher occupancy and stair joints keep their own rejection until coverage is proved.</summary>
    public IReadOnlyList<CornerDecision> EvaluateCorners(MapData map)
    {
        Validate(Renderer, map, Settings);
        var result = new List<CornerDecision>();
        foreach (TerrainTopology.InnerCorner corner in TerrainTopology.EnumerateV21InnerCorners(map))
        {
            int sx = corner.Direction.EndsWith("W", StringComparison.Ordinal) ? -1 : 1;
            bool northern = corner.Direction.StartsWith("N", StringComparison.Ordinal);
            Vector2Int north = corner.Cell + Vector2Int.up, side = corner.Cell + new Vector2Int(sx, 0);
            Tile edge = sx < 0 ? Sources.NorthWest : Sources.NorthEast;
            bool Has(Vector2Int cell, Vector2Int quadrant, Tile source) =>
                Top.TryGetValue((corner.Level, cell), out Tile tile) && tile != null &&
                Pieces.Any(p => p.Level == corner.Level && p.Cell == cell && p.Quadrant == quadrant &&
                    p.Replacement && ReferenceEquals(p.Source, source));
            bool foundation = corner.Level == 1 || (Foundation &&
                new[] { corner.Cell, north, side, corner.Cell + new Vector2Int(sx, 1) }
                    .All(c => Top.TryGetValue((1, c), out Tile tile) && tile != null));
            bool resolved = northern && corner.DiagonalLevel == corner.Level - 1 && foundation &&
                Has(north, new Vector2Int(sx < 0 ? 0 : 3, 0), edge) &&
                Has(side, new Vector2Int(sx < 0 ? 3 : 0, 3), Sources.Side);
            bool southJoint = !northern && South != null && map.Stairs.Any(s =>
                s.UpperCell == corner.Cell + Vector2Int.down && South.Pieces.Any(p => p.Logical == s.UpperCell) &&
                ((corner.Direction == "SW" && South.SelectsLeft(s)) || (corner.Direction == "SE" && !South.SelectsLeft(s))));
            string reason = resolved ? "NativeNorthReplacementsAndFoundation" :
                corner.DiagonalLevel > corner.Level ? "HigherOccupancyCoverageNotProven" :
                southJoint ? "SouthJointCoverageNotProven" : "DirectionHasNoVerifiedReplacement";
            string evidence = "3x3=";
            for (int y = 1; y >= -1; y--) for (int x = -1; x <= 1; x++)
                evidence += map.GetElevation(corner.Cell + new Vector2Int(x, y)) + ",";
            evidence += "; foundation=" + foundation + "; native edge=" + edge.sprite.name + " " + edge.sprite.rect +
                "; side=" + Sources.Side.sprite.name + " " + Sources.Side.sprite.rect + "; geometry=4x4";
            if (!resolved && (corner.DiagonalLevel > corner.Level || southJoint))
            {
                CoverageProof proof = ProveCoverage(map, corner, coverageWrites);
                resolved = proof.Resolved; reason = proof.Reason; evidence += "; " + proof.Evidence;
            }
            result.Add(new CornerDecision(corner, resolved, reason, evidence));
        }
        return result.AsReadOnly();
    }

    public void Dispose() { Projector.Cancel(this); }

    internal void Seal()
    { inputBinding = CaptureInputs(); resourceBinding = CaptureResources(); nativeBinding = CaptureNativeBinding(false); }

    internal void Validate(MapTilemapRenderer renderer, MapData map, MapGenerationSettings settings)
    {
        try
        {
            if (State != 0 || !Projector.IsPending(this))
                throw Error("Stage1ProjectionNotPrepared", "The preparation was consumed, cancelled or cleared.");
            if (!ReferenceEquals(Renderer, renderer) || !ReferenceEquals(Map, map) || !ReferenceEquals(Settings, settings))
                throw Error("Stage1ProjectionOwnerMismatch", "Prepared projection belongs to another renderer, map or settings.");
            if (inputBinding != CaptureInputs() || resourceBinding != CaptureResources() || nativeBinding != CaptureNativeBinding(true))
                throw Error("Stage1ProjectionStale", "Projection inputs or prepared resources changed after preparation.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal static MapPlanningException Error(string code, string text) => new MapPlanningException(code, text, false);

    internal static void ValidateNative(Tile tile, Texture2D atlas)
    {
        Sprite sprite = tile != null ? tile.sprite : null;
        if (sprite == null)
            throw Error("Stage1ProjectionSpriteMissing", "A required native Top Tile has no Sprite.");
        if (atlas == null || sprite.texture != atlas || atlas.width != 576 || atlas.height != 384 ||
            atlas.name != "Tilemap_color1" || sprite.packed || sprite.rect.size != new Vector2(64, 64) ||
            sprite.pixelsPerUnit != 64 || sprite.pivot != new Vector2(32, 32) ||
            tile.transform != Matrix4x4.identity || tile.color != Color.white || atlas.filterMode != FilterMode.Point)
            throw Error("Stage1ProjectionSourceUnsupported", "Only the bound, untransformed audited native color1 source roles are supported.");
    }

    private string CaptureInputs()
    {
        var b = new StringBuilder();
        b.Append(Map.Origin).Append(Map.Width).Append('/').Append(Map.Height).Append('/').Append(Map.GenerationSeed)
            .Append(Map.SpawnCell).Append(Map.ExitCell);
        for (int y = Map.Origin.y; y < Map.Origin.y + Map.Height; y++)
        for (int x = Map.Origin.x; x < Map.Origin.x + Map.Width; x++)
        { var c = new Vector2Int(x, y); b.Append((int)Map.GetCell(c).terrainType).Append(':').Append(Map.GetElevation(c)).Append(';'); }
        foreach (MapStairConnection s in Map.Stairs)
            b.Append(s.LowerCell).Append(s.UpperCell).Append((int)s.VisualComposition).Append((int)s.VisualSide).Append((int)s.VisualPlacement);
        AppendObject(b, Settings); AppendObject(b, Renderer); AppendObject(b, Palette); AppendObject(b, H2Palette);
        CapturePalette(Palette); CapturePalette(H2Palette);
        TileState(b, Sources.NorthWest); TileState(b, Sources.NorthEast); TileState(b, Sources.Side);
        if (South != null) foreach (var p in South.Pieces) { b.Append(p.Logical).Append(p.Visual); TileState(b, p.Tile); }
        foreach (CoveragePlacement p in CoverageFaces.Concat(CoverageStairs))
        { b.Append(p.Role).Append(p.Level).Append(p.Logical).Append(p.Visual).Append(p.Order); TileState(b, p.Tile); }
        Tilemap coordinates = Renderer.WaterBaseTilemap;
        b.Append(coordinates.GetInstanceID()).Append(coordinates.transform.localToWorldMatrix.ToString("R"))
            .Append(coordinates.tileAnchor.ToString("R")).Append(coordinates.orientationMatrix.ToString("R"));
        Grid grid = coordinates.GetComponentInParent<Grid>();
        if (grid != null) b.Append(grid.cellSize.ToString("R")).Append(grid.cellGap.ToString("R")).Append((int)grid.cellLayout).Append((int)grid.cellSwizzle);
        b.Append(layerBinding()).Append(CaptureCoverageBinding());
        TilemapRenderer coordinateRenderer = coordinates.GetComponent<TilemapRenderer>();
        b.Append(MaterialState(coordinateRenderer.sharedMaterial)).Append(coordinateRenderer.sortingLayerID)
            .Append(coordinateRenderer.sortingOrder).Append((int)coordinateRenderer.mode);
        return Hash(Encoding.UTF8.GetBytes(b.ToString()));

        void CapturePalette(MapTerrainV2Palette palette)
        {
            if (palette == null) { b.Append("missing palette"); return; }
            AppendObject(b, palette.GrassAndCliff);
            if (palette.GrassAndCliff != null)
            for (int mask = 0; mask < 16; mask++)
            {
                TileState(b, palette.GrassAndCliff.GetTile((TerrainBoundaryMask)mask) as Tile);
                TileState(b, palette.GrassAndCliff.GetSouthFaceTile((TerrainBoundaryMask)mask) as Tile);
            }
            TileState(b, palette.StairLowerLeft as Tile); TileState(b, palette.StairLowerRight as Tile);
            TileState(b, palette.StairUpperLeft as Tile); TileState(b, palette.StairUpperRight as Tile);
            TextureState(b, palette.ShadowTexture); TextureState(b, palette.FoamTexture);
        }
    }

    private string CaptureResources()
    {
        var b = new StringBuilder();
        foreach (UnityEngine.Object value in Owned)
        {
            if (value == null) throw Error("Stage1ProjectionStale", "A prepared resource was destroyed.");
            b.Append(value.GetInstanceID());
            if (value is Tile tile) TileState(b, tile);
            else if (value is Sprite sprite) SpriteState(b, sprite);
            else if (value is Texture2D texture)
            {
                TextureState(b, texture);
                Color32[] pixels = texture.GetPixels32(); var bytes = new byte[pixels.Length * 4];
                for (int i = 0; i < pixels.Length; i++)
                { bytes[4*i] = pixels[i].r; bytes[4*i+1] = pixels[i].g; bytes[4*i+2] = pixels[i].b; bytes[4*i+3] = pixels[i].a; }
                b.Append(Hash(bytes));
            }
        }
        return Hash(Encoding.UTF8.GetBytes(b.ToString()));
    }

    private static void AppendObject(StringBuilder b, UnityEngine.Object value)
    { b.Append(value != null ? value.GetInstanceID() : 0); if (value != null) b.Append(JsonUtility.ToJson(value)); }
    internal static string MaterialState(Material material)
    {
        if (material == null) return "missing material";
        var b = new StringBuilder().Append(material.GetInstanceID()).Append('/').Append(material.renderQueue);
        Shader shader = material.shader;
        b.Append(shader != null ? shader.GetInstanceID() : 0);
        foreach (string keyword in material.shaderKeywords.OrderBy(k => k)) b.Append(keyword).Append(';');
        if (shader != null)
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string name = shader.GetPropertyName(i); b.Append(name).Append('=');
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color: b.Append(material.GetColor(name).ToString("R")); break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector: b.Append(material.GetVector(name).ToString("R")); break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    Texture texture = material.GetTexture(name);
                    b.Append(texture != null ? texture.GetInstanceID() : 0).Append(material.GetTextureScale(name).ToString("R"))
                        .Append(material.GetTextureOffset(name).ToString("R")); break;
                default: b.Append(material.GetFloat(name).ToString("R")); break;
            }
        }
        return b.ToString();
    }
    private static void TileState(StringBuilder b, Tile tile)
    {
        b.Append(tile != null ? tile.GetInstanceID() : 0);
        if (tile == null) return;
        b.Append(tile.color.ToString("R")).Append(tile.transform.ToString("R")).Append((int)tile.colliderType);
        SpriteState(b, tile.sprite);
    }
    private static void TextureState(StringBuilder b, Texture2D texture)
    {
        b.Append(texture != null ? texture.GetInstanceID() : 0);
        if (texture != null) b.Append(texture.width).Append('/').Append(texture.height).Append((int)texture.format)
            .Append((int)texture.graphicsFormat).Append(texture.isReadable).Append(texture.mipmapCount)
            .Append((int)texture.filterMode).Append((int)texture.wrapMode);
    }
    private static void SpriteState(StringBuilder b, Sprite sprite)
    {
        b.Append(sprite != null ? sprite.GetInstanceID() : 0); if (sprite == null) return;
        TextureState(b, sprite.texture); b.Append(sprite.rect.ToString("R")).Append(sprite.pivot.ToString("R")).Append(sprite.pixelsPerUnit).Append(sprite.packed);
        foreach (Vector2 v in sprite.vertices) b.Append(v.ToString("R"));
        foreach (Vector2 uv in sprite.uv) b.Append(uv.ToString("R"));
        foreach (ushort t in sprite.triangles) b.Append(t).Append(',');
    }
    private static string Hash(byte[] bytes)
    { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
}
