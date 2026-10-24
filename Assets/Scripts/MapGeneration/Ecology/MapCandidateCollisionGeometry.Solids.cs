using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    public bool IsPlannedSolidClear(MapPlannedSolidGeometry solid)
    {
        if (solid == null) throw new ArgumentNullException(nameof(solid));
        for (int i = 0; i < solid.ShapeCount; i++)
        {
            Polygon query = PlannedPolygon(solid.ShapeAt(i));
            float rim = query.IsCircle ? 0f : query.ContactRadius;
            Rect bounds = query.Bounds;
            if (bounds.xMin - rim < floorBounds.xMin || bounds.xMax + rim > floorBounds.xMax ||
                bounds.yMin - rim < floorBounds.yMin || bounds.yMax + rim > floorBounds.yMax) return false;
            int minX = Bucket(bounds.xMin - rim - polygonContactRadius), maxX = Bucket(bounds.xMax + rim + polygonContactRadius);
            int minY = Bucket(bounds.yMin - rim - polygonContactRadius), maxY = Bucket(bounds.yMax + rim + polygonContactRadius);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!buckets.TryGetValue(Key(x, y), out List<int> entries)) continue;
                foreach (int index in entries)
                {
                    Polygon other = polygons[index];
                    if (x != Math.Max(minX, other.MinX) || y != Math.Max(minY, other.MinY)) continue;
                    if (PlannedOverlap(query, other)) return false;
                }
            }
        }
        return true;
    }

    private static bool PlannedOverlap(Polygon a, Polygon b)
    {
        double radius = a.ContactRadius + b.ContactRadius, squared = radius * radius;
        if (a.IsCircle && b.IsCircle) return Dot(a.Center - b.Center, a.Center - b.Center) <= squared;
        if (a.IsCircle || b.IsCircle)
        {
            Polygon circle = a.IsCircle ? a : b, polygon = a.IsCircle ? b : a;
            if (Contains(polygon.Points, circle.Center)) return true;
            for (int i = 0; i < polygon.Points.Length; i++)
                if (SegmentDistanceSquared(circle.Center, circle.Center, polygon.Points[i], polygon.Points[(i + 1) % polygon.Points.Length]) <= squared) return true;
            return false;
        }
        if (Contains(a.Points, b.Points[0]) || Contains(b.Points, a.Points[0])) return true;
        for (int i = 0; i < a.Points.Length; i++)
        for (int j = 0; j < b.Points.Length; j++)
            if (SegmentDistanceSquared(a.Points[i], a.Points[(i + 1) % a.Points.Length],
                b.Points[j], b.Points[(j + 1) % b.Points.Length]) <= squared) return true;
        return false;
    }

    private Polygon PlannedPolygon(MapPlannedSolidGeometry.Shape shape)
    {
        Vector2 min, max;
        if (shape.IsCircle) { min = shape.Center - Vector2.one * shape.Radius; max = shape.Center + Vector2.one * shape.Radius; }
        else
        {
            min = max = shape.Points[0];
            foreach (Vector2 point in shape.Points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        }
        return new Polygon { Points = shape.IsCircle ? Array.Empty<Vector2>() : (Vector2[])shape.Points.Clone(),
            IsCircle = shape.IsCircle, Center = shape.Center, ContactRadius = shape.IsCircle ? shape.Radius : polygonContactRadius,
            Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y), MinX = Bucket(min.x), MaxX = Bucket(max.x), MinY = Bucket(min.y), MaxY = Bucket(max.y) };
    }

    public MapCandidateCollisionGeometry WithPlannedSolids(IEnumerable<MapPlannedSolidGeometry> solids)
    {
        if (solids == null) throw new ArgumentNullException(nameof(solids));
        var ordered = new List<MapPlannedSolidGeometry>(solids);
        ordered.Sort((a, b) => string.CompareOrdinal(a?.LogicalId, b?.LogicalId));
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i] == null || i > 0 && ordered[i - 1].LogicalId == ordered[i].LogicalId) throw Invalid("DuplicateOrMissingPlannedSolidIdentity");
        if (ordered.Count == 0) return this;
        return new MapCandidateCollisionGeometry(this, ordered);
    }
    private MapCandidateCollisionGeometry(MapCandidateCollisionGeometry original, List<MapPlannedSolidGeometry> additions)
    {
        GenerationId = original.GenerationId; Width = original.Width; Height = original.Height; Origin = original.Origin;
        MapContentIdentity = original.MapContentIdentity; floorBounds = original.floorBounds;
        polygonContactRadius = original.polygonContactRadius; bucketSize = original.bucketSize;
        walkable = original.walkable; terrainEdges = original.terrainEdges; centers = original.centers;
        var all = new List<Polygon>(original.polygons);
        foreach (MapPlannedSolidGeometry solid in additions)
        for (int i = 0; i < solid.ShapeCount; i++)
        {
            MapPlannedSolidGeometry.Shape shape = solid.ShapeAt(i);
            all.Add(PlannedPolygon(shape));
        }
        polygons = all.ToArray();
        for (int i = 0; i < polygons.Length; i++)
        {
            Polygon polygon = polygons[i];
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
            writer.Write("candidate-planned-solids-v1"); writer.Write(original.ProjectionIdentity); writer.Write(additions.Count);
            foreach (MapPlannedSolidGeometry solid in additions)
            {
                writer.Write(solid.LogicalId); writer.Write(solid.ShapeCount);
                for (int i = 0; i < solid.ShapeCount; i++)
                {
                    MapPlannedSolidGeometry.Shape shape = solid.ShapeAt(i); writer.Write(shape.IsCircle);
                    if (shape.IsCircle) { writer.Write(shape.Center.x); writer.Write(shape.Center.y); writer.Write(shape.Radius); }
                    else { writer.Write(shape.Points.Length); foreach (Vector2 point in shape.Points) { writer.Write(point.x); writer.Write(point.y); } }
                }
            }
        });
    }
}
