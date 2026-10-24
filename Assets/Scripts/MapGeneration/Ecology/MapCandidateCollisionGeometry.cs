using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Frozen candidate geometry from projection inputs; never samples the live Physics2D world.</summary>
public sealed partial class MapCandidateCollisionGeometry
{
    private sealed class Polygon
    {
        internal Vector2[] Points;
        internal Rect Bounds;
        internal float ContactRadius;
        internal bool IsCircle;
        internal Vector2 Center;
        internal int MinX, MinY, MaxX, MaxY;
    }
    private readonly bool[] walkable;
    private readonly byte[] terrainEdges;
    private readonly Vector2[] centers;
    private readonly Polygon[] polygons;
    private readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
    private readonly Rect floorBounds;
    private readonly float bucketSize;
    private readonly float polygonContactRadius;
    private readonly Dictionary<string, EcologyNavigationGraph> graphs = new Dictionary<string, EcologyNavigationGraph>(StringComparer.Ordinal);
    private MapCandidateCollisionGeometry(Guid generation, MapData map, MapTilemapRenderer terrain,
        List<Vector2[]> shapes, float[] sourceContactRadii)
    {
        GenerationId = generation; Width = map.Width; Height = map.Height; Origin = map.Origin;
        MapContentIdentity = ComputeMapContentIdentity(map);
        polygonContactRadius = Physics2D.defaultContactOffset;
        if (!Finite(polygonContactRadius) || polygonContactRadius < 0f) throw Invalid("InvalidPolygonContactRadius");
        int count = checked(Width * Height);
        walkable = new bool[count]; terrainEdges = new byte[count]; centers = new Vector2[count];
        Vector2 lower = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(Origin.x, Origin.y, 0));
        Vector2 upper = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(Origin.x + Width, Origin.y + Height, 0));
        floorBounds = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        Vector2 cellStep = terrain.Coordinates.CellToWorld(Origin + Vector2Int.right) - terrain.Coordinates.CellToWorld(Origin);
        Vector2 rowStep = terrain.Coordinates.CellToWorld(Origin + Vector2Int.up) - terrain.Coordinates.CellToWorld(Origin);
        bucketSize = Mathf.Min(cellStep.x, rowStep.y);
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        for (int node = 0; node < count; node++)
        {
            Vector2Int cell = Origin + new Vector2Int(node % Width, node / Width);
            centers[node] = terrain.Coordinates.CellToWorld(cell); walkable[node] = map.IsWalkable(cell);
            for (int d = 0; d < 4; d++) if (map.CanTraverse(cell, cell + directions[d])) terrainEdges[node] |= (byte)(1 << d);
        }
        polygons = new Polygon[shapes.Count];
        for (int i = 0; i < shapes.Count; i++)
        {
            Vector2[] points = (Vector2[])shapes[i].Clone();
            if (points.Length < 3) throw Invalid("UnknownCollisionOutline");
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 point in points)
            {
                if (!Finite(point)) throw Invalid("NonFiniteCollisionOutline");
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var polygon = new Polygon { Points = points, ContactRadius = sourceContactRadii[i], Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y),
                MinX = Bucket(min.x), MaxX = Bucket(max.x), MinY = Bucket(min.y), MaxY = Bucket(max.y) };
            polygons[i] = polygon;
            for (int y = polygon.MinY; y <= polygon.MaxY; y++)
            for (int x = polygon.MinX; x <= polygon.MaxX; x++)
            {
                long key = Key(x, y);
                if (!buckets.TryGetValue(key, out List<int> list)) { list = new List<int>(); buckets.Add(key, list); }
                list.Add(i);
            }
        }
        ProjectionIdentity = Hash(writer =>
        {
            writer.Write("candidate-collision-v3-native-contact-map-input"); writer.Write(MapContentIdentity); writer.Write(polygonContactRadius);
            writer.Write(Width); writer.Write(Height); writer.Write(Origin.x); writer.Write(Origin.y);
            writer.Write(floorBounds.xMin); writer.Write(floorBounds.yMin); writer.Write(floorBounds.xMax); writer.Write(floorBounds.yMax);
            for (int n = 0; n < count; n++)
            { writer.Write(walkable[n]); writer.Write(terrainEdges[n]); writer.Write(centers[n].x); writer.Write(centers[n].y); }
            writer.Write(polygons.Length);
            foreach (Polygon polygon in polygons)
            { writer.Write(polygon.ContactRadius); writer.Write(polygon.Points.Length); foreach (Vector2 point in polygon.Points) { writer.Write(point.x); writer.Write(point.y); } }
        });
    }
    public Guid GenerationId { get; }
    public Vector2Int Origin { get; }
    public int Width { get; }
    public int Height { get; }
    public string ProjectionIdentity { get; }
    private string physicalContentIdentity;
    // ProjectionIdentity retains the accepted construction/recipe history.
    // Live revision equality compares the canonical physical set instead.
    public string PhysicalContentIdentity => physicalContentIdentity ?? (physicalContentIdentity = Hash(writer =>
    {
        writer.Write("candidate-physical-content-v1"); writer.Write(MapContentIdentity);
        writer.Write(floorBounds.xMin); writer.Write(floorBounds.yMin); writer.Write(floorBounds.xMax); writer.Write(floorBounds.yMax);
        var shapes = new List<string>(polygons.Length);
        foreach (Polygon polygon in polygons)
            shapes.Add(Hash(shape =>
            {
                shape.Write(polygon.IsCircle); shape.Write(polygon.ContactRadius);
                if (polygon.IsCircle) { shape.Write(polygon.Center.x); shape.Write(polygon.Center.y); }
                else
                {
                    shape.Write(polygon.Points.Length);
                    foreach (Vector2 point in polygon.Points) { shape.Write(point.x); shape.Write(point.y); }
                }
            }));
        shapes.Sort(StringComparer.Ordinal); writer.Write(shapes.Count);
        foreach (string shape in shapes) writer.Write(shape);
    }));
    public string MapContentIdentity { get; }
    public bool IsPhysicallyEquivalentTo(MapCandidateCollisionGeometry other)
    {
        if (other == null || MapContentIdentity != other.MapContentIdentity || !floorBounds.Equals(other.floorBounds) || polygons.Length != other.polygons.Length) return false;
        if (PhysicalContentIdentity == other.PhysicalContentIdentity) return true;
        // Parent-local Transform round trips may move a world coordinate by one float ULP.
        // This is a bounded numerical comparison only; canonical layout/signature bytes stay exact.
        const float tolerance = .00001f;
        var remaining = new List<Polygon>(other.polygons);
        foreach (Polygon shape in polygons)
        {
            int match = -1;
            for (int i = 0; i < remaining.Count; i++)
            {
                Polygon candidate = remaining[i];
                if (ReferenceEquals(shape, candidate)) { match = i; break; }
                if (shape.IsCircle != candidate.IsCircle || Mathf.Abs(shape.ContactRadius - candidate.ContactRadius) > tolerance) continue;
                bool same = true;
                if (shape.IsCircle) same = SamePhysicalPoint(shape.Center, candidate.Center, tolerance);
                else
                {
                    same = shape.Points.Length == candidate.Points.Length;
                    for (int p = 0; same && p < shape.Points.Length; p++) same = SamePhysicalPoint(shape.Points[p], candidate.Points[p], tolerance);
                }
                if (same) { match = i; break; }
            }
            if (match < 0) return false;
            remaining.RemoveAt(match);
        }
        return true;
    }
    private static bool SamePhysicalPoint(Vector2 a, Vector2 b, float tolerance) =>
        Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;
    public int PolygonCount => polygons.Length;
    public Vector2 NodeWorldPosition(int node) => node >= 0 && node < centers.Length ? centers[node] : throw new ArgumentOutOfRangeException(nameof(node));
    public bool TryWorldNode(Vector2 position, out int node)
    {
        node = -1;
        if (!Finite(position)) return false;
        int x = Mathf.FloorToInt((position.x - floorBounds.xMin) / (floorBounds.width / Width));
        int y = Mathf.FloorToInt((position.y - floorBounds.yMin) / (floorBounds.height / Height));
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        node = y * Width + x; return true;
    }
    public bool MatchesMapData(MapData map) => map != null && map.Width == Width && map.Height == Height &&
        map.Origin == Origin && ComputeMapContentIdentity(map) == MapContentIdentity;

    private static string ComputeMapContentIdentity(MapData map) => Hash(writer =>
    {
        writer.Write("candidate-map-content-v1"); writer.Write(map.Width); writer.Write(map.Height);
        writer.Write(map.Origin.x); writer.Write(map.Origin.y); writer.Write(map.HasElevation);
        writer.Write(map.SpawnCell.x); writer.Write(map.SpawnCell.y); writer.Write(map.ExitCell.x); writer.Write(map.ExitCell.y);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Vector2Int cell = map.Origin + new Vector2Int(x, y);
            writer.Write((int)map.GetCell(cell).terrainType); writer.Write(map.GetElevation(cell));
        }
        writer.Write(map.Stairs.Count);
        foreach (MapStairConnection stair in map.Stairs)
        {
            writer.Write(stair.LowerCell.x); writer.Write(stair.LowerCell.y);
            writer.Write(stair.UpperCell.x); writer.Write(stair.UpperCell.y);
        }
    });

    public static MapCandidateCollisionGeometry Capture(Guid generationId, MapData map,
        MapSimpleDecorationData decorations, MapGenerationSettings settings,
        MapTilemapRenderer terrain, MapSimpleDecorationRenderer decoration)
    {
        if (generationId == Guid.Empty || map == null || decorations == null || settings == null || terrain == null || decoration == null)
            throw Invalid("MissingCandidateGeometryInput");
        ValidateGrid(terrain.WaterBaseTilemap); ValidateGrid(terrain.TerrainCollisionTilemap);
        ValidateGrid(decoration.DecorationCollisionTilemap);
        var shapes = new List<Vector2[]>();
        var cliffs = new List<Vector2[]>();
        Transform cliffSpace = terrain.TerrainCollisionTilemap.transform.parent;
        for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (!map.IsWalkable(cell)) AppendTile(settings.collisionMarkerTile, terrain.TerrainCollisionTilemap, cell, shapes);
            else
            {
                AppendCliffEdge(map, terrain.WaterBaseTilemap, cliffSpace, cell, cell + Vector2Int.right, true, cliffs);
                AppendCliffEdge(map, terrain.WaterBaseTilemap, cliffSpace, cell, cell + Vector2Int.up, false, cliffs);
            }
        }
        int terrainShapeCount = shapes.Count;
        foreach (Vector2[] path in cliffs)
        {
            var world = new Vector2[path.Length];
            for (int i = 0; i < path.Length; i++) world[i] = cliffSpace.TransformPoint(path[i]);
            shapes.Add(world);
        }
        if (settings.remediationProjectedSurfaces)
            shapes.AddRange(MapSurfaceV4Policy.Build(map, terrain.TerrainV2Palette, settings).WorldPolygons(terrain.WaterBaseTilemap));
        int cliffShapeEnd = shapes.Count;
        var decorationCells = new HashSet<Vector2Int>();
        foreach (MapSimpleDecorationPlacement placement in decorations.Placements)
        {
            MapSimpleDecorationVariant variant = settings.simpleDecorationPalette?.FindVariant(placement.VariantId);
            if (variant == null || variant.DecorationType != placement.DecorationType) throw Invalid("UnknownDecorationGeometry");
            foreach (Vector2Int offset in variant.CollisionOffsets)
            {
                Vector2Int cell = placement.AnchorCell + offset;
                if (map.IsInside(cell) && decorationCells.Add(cell))
                    AppendTile(settings.simpleDecorationCollisionMarkerTile, decoration.DecorationCollisionTilemap, cell, shapes);
            }
        }
        var radii = new float[shapes.Count];
        float terrainRadius = ProjectionContactRadius(terrain.TerrainCollisionTilemap);
        float decorationRadius = ProjectionContactRadius(decoration.DecorationCollisionTilemap);
        for (int i = 0; i < radii.Length; i++)
            radii[i] = i < terrainShapeCount ? terrainRadius : i < cliffShapeEnd ? Physics2D.defaultContactOffset : decorationRadius;
        return new MapCandidateCollisionGeometry(generationId, map, terrain, shapes, radii);
    }

    // The renderer calls this same formula, retaining its original loop/path order and local coordinates.
    public static void AppendCliffEdge(MapData map, Tilemap ground, Transform collisionSpace,
        Vector2Int from, Vector2Int to, bool vertical, List<Vector2[]> paths)
    {
        if (!map.IsInside(to) || !map.IsWalkable(to) || map.GetElevation(from) == map.GetElevation(to) || map.IsStairTransition(from, to)) return;
        Vector3 center = ground.GetCellCenterWorld(new Vector3Int(from.x, from.y, 0));
        Vector3 neighbor = ground.GetCellCenterWorld(new Vector3Int(to.x, to.y, 0));
        Vector3 midpoint = (center + neighbor) * 0.5f;
        Vector3 local = collisionSpace.InverseTransformPoint(midpoint);
        Vector3 size = ground.layoutGrid.cellSize;
        float halfLength = (vertical ? size.y : size.x) * 0.5f;
        float halfWidth = Mathf.Min(size.x, size.y) * 0.035f;
        float hx = vertical ? halfWidth : halfLength;
        float hy = vertical ? halfLength : halfWidth;
        paths.Add(new[] { new Vector2(local.x - hx, local.y - hy), new Vector2(local.x + hx, local.y - hy),
            new Vector2(local.x + hx, local.y + hy), new Vector2(local.x - hx, local.y + hy) });
    }

    public EcologyNavigationGraph BuildNavigationGraph(Collider2D solidFoot, float skin)
    {
        Footprint footprint = CaptureFootprint(solidFoot, skin);
        string identity = FootprintHash(footprint);
        if (graphs.TryGetValue(identity, out EcologyNavigationGraph cached)) return cached;
        var legal = new bool[walkable.Length]; var edges = new byte[walkable.Length];
        for (int node = 0; node < legal.Length; node++) legal[node] = walkable[node] && Clear(footprint, centers[node], centers[node]);
        for (int node = 0; node < legal.Length; node++)
        {
            if (!legal[node]) continue;
            for (int d = 0; d < 4; d++)
            {
                if ((terrainEdges[node] & (1 << d)) == 0) continue;
                int next = d == 0 ? node + 1 : d == 1 ? node + Width : d == 2 ? node - 1 : node - Width;
                if (next >= 0 && next < legal.Length && legal[next] && Clear(footprint, centers[node], centers[next])) edges[node] |= (byte)(1 << d);
            }
        }
        cached = new EcologyNavigationGraph(GenerationId, Origin, Width, Height, ProjectionIdentity, identity, legal, edges);
        graphs.Add(identity, cached); return cached;
    }
    public bool IsTranslationClear(Collider2D solidFoot, Vector2 bodyStart, Vector2 bodyEnd, float skin) =>
        Clear(CaptureFootprint(solidFoot, skin), bodyStart, bodyEnd);

    public EcologyConstructiveCapacity BuildCompatibleCapacity(Collider2D solidFoot,
        int[] memberNodes, int[] blockedPlacementNodes, ulong[] stableRanksByNode, int minimumSpacingSteps)
    {
        Footprint foot = CaptureFootprint(solidFoot, 0f);
        EcologyNavigationGraph graph = BuildNavigationGraph(solidFoot, 0f);
        return EcologyConstructiveCapacity.Build(graph, memberNodes, blockedPlacementNodes,
            stableRanksByNode, minimumSpacingSteps, (a, b) =>
            {
                Vector2 delta = centers[a] - centers[b];
                if (foot.Circle) return Dot(delta, delta) > 4d * foot.Radius * foot.Radius;
                double x = Math.Max(0d, Math.Abs(delta.x) - 2d * foot.Half.x);
                double y = Math.Max(0d, Math.Abs(delta.y) - 2d * foot.Half.y);
                return x * x + y * y > 4d * foot.Radius * foot.Radius;
            });
    }

    private readonly struct Footprint
    {
        internal Footprint(bool circle, Vector2 offset, Vector2 half, float radius)
        { Circle = circle; Offset = offset; Half = half; Radius = radius; }
        internal readonly bool Circle;
        internal readonly Vector2 Offset, Half;
        internal readonly float Radius;
    }
    private Footprint CaptureFootprint(Collider2D shape, float skin)
    {
        if (shape == null || shape.isTrigger || !shape.enabled ||
            !Finite(skin) || skin < 0f) throw Invalid("UnknownSolidFootprint");
        Rigidbody2D body = shape.attachedRigidbody != null ? shape.attachedRigidbody : shape.GetComponentInParent<Rigidbody2D>(true);
        if (body == null) throw Invalid("MissingFootprintBodyConfiguration");
        Vector3 scale = shape.transform.lossyScale;
        // A footprint is configuration, not a sample of its world pose. Subtracting
        // translated world points injects cancellation noise into its content hash.
        Matrix4x4 shapeToBody = Matrix4x4.identity;
        for (Transform part = shape.transform; part != body.transform; part = part.parent)
        {
            if (part == null) throw Invalid("FootprintBodyHierarchyMismatch");
            shapeToBody = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale) * shapeToBody;
        }
        Matrix4x4 bodyLinear = Matrix4x4.TRS(Vector3.zero, body.transform.rotation, body.transform.lossyScale);
        Vector2 offset = bodyLinear.MultiplyVector(shapeToBody.MultiplyPoint3x4(shape.offset));
        if (!Finite(offset) || !Finite(scale.x) || !Finite(scale.y)) throw Invalid("NonFiniteFootprint");
        if (shape is CircleCollider2D circle && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) < 0.00001f)
        {
            // Native polygon contacts include their configured contact radius.
            // A circle contributes only its own geometric radius; a box adds a
            // second polygon radius. This is clearance, not Cast hit distance.
            float radius = circle.radius * Mathf.Abs(scale.x) + skin;
            if (!(radius > 0f) || !Finite(radius)) throw Invalid("InvalidFootprintSize");
            return new Footprint(true, offset, Vector2.one * radius, radius);
        }
        if (shape is BoxCollider2D box && box.edgeRadius == 0f && Mathf.Abs(Mathf.Sin(shape.transform.eulerAngles.z * Mathf.Deg2Rad)) < 0.00001f)
        {
            Vector2 half = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) * 0.5f + Vector2.one * skin;
            if (!Finite(half) || half.x <= 0f || half.y <= 0f) throw Invalid("InvalidFootprintSize");
            return new Footprint(false, offset, half, polygonContactRadius);
        }
        throw Invalid("UnsupportedSolidFootprintGeometry");
    }
    private bool Clear(Footprint foot, Vector2 bodyStart, Vector2 bodyEnd)
    {
        if (!Finite(bodyStart) || !Finite(bodyEnd)) throw Invalid("NonFiniteGeometryQuery");
        Vector2 start = bodyStart + foot.Offset, end = bodyEnd + foot.Offset;
        Vector2 queryHalf = foot.Half + Vector2.one * (polygonContactRadius + (foot.Circle ? 0f : foot.Radius));
        Vector2 min = Vector2.Min(start, end) - queryHalf, max = Vector2.Max(start, end) + queryHalf;
        if (min.x < floorBounds.xMin || min.y < floorBounds.yMin || max.x > floorBounds.xMax || max.y > floorBounds.yMax) return false;
        int minX = Bucket(min.x), maxX = Bucket(max.x), minY = Bucket(min.y), maxY = Bucket(max.y);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!buckets.TryGetValue(Key(x, y), out List<int> candidates)) continue;
            foreach (int index in candidates)
            {
                Polygon polygon = polygons[index];
                // Visit each polygon in the first shared bucket, without per-query scratch allocation.
                if (x != Math.Max(minX, polygon.MinX) || y != Math.Max(minY, polygon.MinY) ||
                    max.x < polygon.Bounds.xMin || min.x > polygon.Bounds.xMax || max.y < polygon.Bounds.yMin || min.y > polygon.Bounds.yMax) continue;
                double contactRadius = foot.Radius + polygon.ContactRadius;
                if (polygon.IsCircle)
                {
                    if (foot.Circle ? SegmentDistanceSquared(start, end, polygon.Center, polygon.Center) <= contactRadius * contactRadius :
                        SweptBoxDistanceSquared(start, end, foot.Half, polygon.Center, polygon.Center) <= contactRadius * contactRadius) return false;
                    continue;
                }
                if (Contains(polygon.Points, start)) return false;
                for (int edge = 0; edge < polygon.Points.Length; edge++)
                {
                    Vector2 a = polygon.Points[edge], b = polygon.Points[(edge + 1) % polygon.Points.Length];
                    if (foot.Circle ? SegmentDistanceSquared(start, end, a, b) <= contactRadius * contactRadius :
                        SweptBoxDistanceSquared(start, end, foot.Half, a, b) <= contactRadius * contactRadius) return false;
                }
            }
        }
        return true;
    }

    private static bool SweptBoxTouchesSegment(Vector2 start, Vector2 end, Vector2 half, Vector2 a, Vector2 b)
    {
        Vector2 movement = end - start, edge = b - a;
        return AxisOverlaps(Vector2.right, start, end, half, a, b) && AxisOverlaps(Vector2.up, start, end, half, a, b) &&
            AxisOverlaps(new Vector2(-movement.y, movement.x), start, end, half, a, b) &&
            AxisOverlaps(new Vector2(-edge.y, edge.x), start, end, half, a, b);
    }
    private static double SweptBoxDistanceSquared(Vector2 start, Vector2 end, Vector2 half, Vector2 a, Vector2 b)
    {
        if (SweptBoxTouchesSegment(start, end, half, a, b)) return 0d;
        double best = double.PositiveInfinity;
        // These twelve segments contain every boundary edge of the convex
        // swept rectangle, including diagonal motion, without allocating a hull.
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 offset = BoxCorner(half, corner), next = BoxCorner(half, (corner + 1) % 4);
            best = Math.Min(best, SegmentDistanceSquared(start + offset, end + offset, a, b));
            best = Math.Min(best, SegmentDistanceSquared(start + offset, start + next, a, b));
            best = Math.Min(best, SegmentDistanceSquared(end + offset, end + next, a, b));
        }
        return best;
    }
    private static Vector2 BoxCorner(Vector2 half, int index) =>
        new Vector2(index == 1 || index == 2 ? half.x : -half.x, index >= 2 ? half.y : -half.y);
    private static bool AxisOverlaps(Vector2 axis, Vector2 start, Vector2 end, Vector2 half, Vector2 a, Vector2 b)
    {
        if (axis.sqrMagnitude < 1e-14f) return true;
        double s = Dot(axis, start), e = Dot(axis, end), p = Dot(axis, a), q = Dot(axis, b);
        double radius = Math.Abs(axis.x) * half.x + Math.Abs(axis.y) * half.y;
        return Math.Max(s, e) + radius >= Math.Min(p, q) && Math.Max(p, q) >= Math.Min(s, e) - radius;
    }
    private static bool Contains(Vector2[] points, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector2 a = points[j], b = points[i];
            if (PointSegmentDistanceSquared(point, a, b) < 1e-14) return true;
            if ((a.y > point.y) != (b.y > point.y) && point.x < (double)(b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
    private static double SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double abC = Cross(b - a, c - a), abD = Cross(b - a, d - a), cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
        if (((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) && ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0))) return 0;
        return Math.Min(Math.Min(PointSegmentDistanceSquared(a, c, d), PointSegmentDistanceSquared(b, c, d)),
            Math.Min(PointSegmentDistanceSquared(c, a, b), PointSegmentDistanceSquared(d, a, b)));
    }
    private static double PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a; double length = Dot(edge, edge);
        double t = length < 1e-20 ? 0d : Math.Max(0d, Math.Min(1d, Dot(point - a, edge) / length));
        double x = point.x - a.x - t * edge.x, y = point.y - a.y - t * edge.y;
        return x * x + y * y;
    }
    private static double Dot(Vector2 a, Vector2 b) => (double)a.x * b.x + (double)a.y * b.y;
    private static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;
    private int Bucket(float coordinate) => checked((int)Math.Floor(coordinate / bucketSize));
    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    private static string FootprintHash(Footprint foot) => Hash(w =>
    { w.Write("solid-foot-v1"); w.Write(foot.Circle); w.Write(foot.Offset.x); w.Write(foot.Offset.y); w.Write(foot.Half.x); w.Write(foot.Half.y); w.Write(foot.Radius); });
    private static string Hash(Action<BinaryWriter> write)
    {
        using (var bytes = new MemoryStream())
        {
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) write(writer);
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }
    }
    private static void ValidateGrid(Tilemap tilemap)
    {
        if (tilemap == null || tilemap.layoutGrid == null || tilemap.layoutGrid.cellLayout != GridLayout.CellLayout.Rectangle ||
            tilemap.orientationMatrix != Matrix4x4.identity || tilemap.tileAnchor != new Vector3(.5f, .5f, 0f)) throw Invalid("UnsupportedProjectionGrid");
        Vector3 origin = tilemap.CellToWorld(Vector3Int.zero);
        Vector3 x = tilemap.CellToWorld(Vector3Int.right) - origin, y = tilemap.CellToWorld(Vector3Int.up) - origin;
        if (x.x <= 0f || y.y <= 0f || Mathf.Abs(x.y) > .00001f || Mathf.Abs(y.x) > .00001f ||
            !Finite(x.x) || !Finite(y.y)) throw Invalid("UnsupportedProjectionGridTransform");
        TilemapCollider2D collider = tilemap.GetComponent<TilemapCollider2D>();
        CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
        if (collider != null && collider.extrusionFactor != 0f || composite != null && composite.edgeRadius != 0f)
            throw Invalid("UnsupportedProjectionColliderExpansion");
    }
    private static void AppendTile(TileBase source, Tilemap tilemap, Vector2Int cell, List<Vector2[]> shapes)
    {
        if (!(source is Tile tile) || tile.colliderType == Tile.ColliderType.None || tile.transform != Matrix4x4.identity)
            throw Invalid("UnknownCollisionTileGeometry");
        Vector3Int index = new Vector3Int(cell.x, cell.y, 0);
        if (tile.colliderType == Tile.ColliderType.Grid)
        {
            shapes.Add(new Vector2[] { tilemap.CellToWorld(index), tilemap.CellToWorld(index + Vector3Int.right),
                tilemap.CellToWorld(index + Vector3Int.right + Vector3Int.up), tilemap.CellToWorld(index + Vector3Int.up) });
            return;
        }
        if (tile.colliderType != Tile.ColliderType.Sprite || tile.sprite == null || tile.sprite.GetPhysicsShapeCount() == 0)
            throw Invalid("MissingImportedCollisionOutline");
        Vector3 center = tilemap.GetCellCenterWorld(index);
        var points = new List<Vector2>();
        for (int path = 0; path < tile.sprite.GetPhysicsShapeCount(); path++)
        {
            points.Clear(); tile.sprite.GetPhysicsShape(path, points);
            if (points.Count < 3) throw Invalid("InvalidImportedCollisionOutline");
            var world = new Vector2[points.Count];
            for (int p = 0; p < points.Count; p++) world[p] = center + tilemap.transform.TransformVector(points[p]);
            shapes.Add(world);
        }
    }
    private static float ProjectionContactRadius(Tilemap tilemap)
    {
        var source = tilemap.GetComponent<TilemapCollider2D>();
        var composite = tilemap.GetComponent<CompositeCollider2D>();
        if (source != null && source.usedByComposite)
        {
            if (composite == null) throw Invalid("MissingProjectionComposite");
            return composite.geometryType == CompositeCollider2D.GeometryType.Outlines ? 0f : Physics2D.defaultContactOffset;
        }
        return Physics2D.defaultContactOffset;
    }
    private static MapPlanningException Invalid(string code) => new MapPlanningException(code, "Candidate collision geometry could not be certified: " + code, false);
}
