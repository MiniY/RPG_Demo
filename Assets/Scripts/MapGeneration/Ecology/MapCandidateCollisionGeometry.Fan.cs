using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    // The union of every straight connector from a proposed segment to one
    // anchor is a triangle, expanded by the entire foot. Checking only its
    // three boundary edges would miss a blocker wholly inside that triangle.
    internal bool IsConnectorFanClear(Collider2D shape, Vector2 from, Vector2 to, Vector2 anchor,
        bool[] requiredSurface, bool[] forbiddenSurface)
    {
        Footprint foot = CaptureFootprint(shape, 0f);
        Vector2 a = from + foot.Offset, b = to + foot.Offset, c = anchor + foot.Offset;
        Vector2 half = foot.Circle ? Vector2.zero : foot.Half;
        Vector2 padding = foot.Half + Vector2.one * (polygonContactRadius + (foot.Circle ? 0f : foot.Radius));
        Vector2 min = Vector2.Min(a, Vector2.Min(b, c)) - padding;
        Vector2 max = Vector2.Max(a, Vector2.Max(b, c)) + padding;
        if (min.x < floorBounds.xMin || min.y < floorBounds.yMin || max.x > floorBounds.xMax || max.y > floorBounds.yMax) return false;
        int minX = Bucket(min.x), maxX = Bucket(max.x), minY = Bucket(min.y), maxY = Bucket(max.y);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!buckets.TryGetValue(Key(x, y), out List<int> candidates)) continue;
            foreach (int index in candidates)
            {
                Polygon obstacle = polygons[index];
                if (x != Math.Max(minX, obstacle.MinX) || y != Math.Max(minY, obstacle.MinY) ||
                    max.x < obstacle.Bounds.xMin || min.x > obstacle.Bounds.xMax || max.y < obstacle.Bounds.yMin || min.y > obstacle.Bounds.yMax) continue;
                double radius = foot.Radius + obstacle.ContactRadius;
                if (obstacle.IsCircle)
                {
                    if (FanSegmentDistanceSquared(a, b, c, half, obstacle.Center, obstacle.Center) <= radius * radius) return false;
                    continue;
                }
                if (Contains(obstacle.Points, a)) return false;
                for (int edge = 0; edge < obstacle.Points.Length; edge++)
                    if (FanSegmentDistanceSquared(a, b, c, half, obstacle.Points[edge],
                        obstacle.Points[(edge + 1) % obstacle.Points.Length]) <= radius * radius) return false;
            }
        }
        float width = floorBounds.width / Width, height = floorBounds.height / Height;
        minX = Mathf.Clamp(Mathf.FloorToInt((min.x - floorBounds.xMin) / width) - 1, 0, Width - 1);
        maxX = Mathf.Clamp(Mathf.FloorToInt((max.x - floorBounds.xMin) / width), 0, Width - 1);
        minY = Mathf.Clamp(Mathf.FloorToInt((min.y - floorBounds.yMin) / height) - 1, 0, Height - 1);
        maxY = Mathf.Clamp(Mathf.FloorToInt((max.y - floorBounds.yMin) / height), 0, Height - 1);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            int node = y * Width + x; Rect rect = SafetySurfaceRect(node);
            bool positive, closed;
            if (foot.Circle)
            {
                double squared = FanRectDistanceSquared(a, b, c, rect), radiusSquared = (double)foot.Radius * foot.Radius;
                positive = squared < radiusSquared; closed = squared <= radiusSquared;
            }
            else
            {
                positive = BoxFanRectOverlaps(a, b, c, half, rect, true);
                closed = BoxFanRectOverlaps(a, b, c, half, rect, false);
            }
            if (closed && forbiddenSurface[node] || positive && (!walkable[node] || !requiredSurface[node])) return false;
        }
        return true;
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        if (Math.Abs(Cross(b - a, c - a)) < 1e-14) return false;
        double first = Cross(b - a, point - a), second = Cross(c - b, point - b), third = Cross(a - c, point - c);
        return first >= 0d && second >= 0d && third >= 0d || first <= 0d && second <= 0d && third <= 0d;
    }
    private static double FanSegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 half, Vector2 p, Vector2 q)
    {
        if (InTriangle(p, a, b, c) || InTriangle(q, a, b, c)) return 0d;
        return Math.Min(SweptBoxDistanceSquared(a, b, half, p, q), Math.Min(
            SweptBoxDistanceSquared(b, c, half, p, q), SweptBoxDistanceSquared(c, a, half, p, q)));
    }
    private static double FanRectDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Rect rect)
    {
        if (rect.Contains(a) || rect.Contains(b) || rect.Contains(c)) return 0d;
        double best = double.PositiveInfinity;
        Vector2 half = rect.size * .5f, centre = rect.center;
        for (int edge = 0; edge < 4; edge++)
            best = Math.Min(best, FanSegmentDistanceSquared(a, b, c, Vector2.zero,
                centre + BoxCorner(half, edge), centre + BoxCorner(half, (edge + 1) % 4)));
        return best;
    }
    private static bool BoxFanRectOverlaps(Vector2 a, Vector2 b, Vector2 c, Vector2 half, Rect rect, bool strict)
    {
        return FanAxis(Vector2.right, a, b, c, half, rect, strict) && FanAxis(Vector2.up, a, b, c, half, rect, strict) &&
            FanAxis(new Vector2(a.y - b.y, b.x - a.x), a, b, c, half, rect, strict) &&
            FanAxis(new Vector2(b.y - c.y, c.x - b.x), a, b, c, half, rect, strict) &&
            FanAxis(new Vector2(c.y - a.y, a.x - c.x), a, b, c, half, rect, strict);
    }
    private static bool FanAxis(Vector2 axis, Vector2 a, Vector2 b, Vector2 c, Vector2 half, Rect rect, bool strict)
    {
        if (axis.sqrMagnitude < 1e-14f) return true;
        double radius = Math.Abs(axis.x) * half.x + Math.Abs(axis.y) * half.y;
        double low = Math.Min(Dot(axis, a), Math.Min(Dot(axis, b), Dot(axis, c))) - radius;
        double high = Math.Max(Dot(axis, a), Math.Max(Dot(axis, b), Dot(axis, c))) + radius;
        double otherRadius = Math.Abs(axis.x) * rect.width * .5d + Math.Abs(axis.y) * rect.height * .5d;
        double centre = Dot(axis, rect.center);
        return strict ? high > centre - otherRadius && centre + otherRadius > low : high >= centre - otherRadius && centre + otherRadius >= low;
    }
}
