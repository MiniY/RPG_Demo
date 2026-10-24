using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure shape/rectangle intersections for a certified straight translation, never a motion proof.</summary>
internal static class EcologyProtectionGeometry
{
    internal readonly struct Sweep
    {
        internal readonly Vector2 Center, Delta, Half, Min, Max;
        internal readonly bool Circle;
        internal readonly float Radius;
        internal Sweep(Vector2 center, Vector2 delta, Vector2 half, bool circle, float radius)
        {
            Center = center; Delta = delta; Half = half; Circle = circle; Radius = radius;
            Vector2 end = center + delta;
            float magnitude = Mathf.Max(Mathf.Max(Mathf.Abs(center.x), Mathf.Abs(center.y)), Mathf.Max(Mathf.Abs(end.x), Mathf.Abs(end.y)));
            Vector2 guard = Vector2.one * (.0001f * (1f + magnitude));
            Min = Vector2.Min(center, end) - half - guard;
            Max = Vector2.Max(center, end) + half + guard;
        }
    }

    internal static bool TryPrepare(Collider2D shape, Vector2 bodyStart, Vector2 bodyEnd, out Sweep sweep)
    {
        sweep = default;
        // Read the authoritative native shape once for this query, never cache it across poses.
        Vector2 center = (Vector2)shape.bounds.center + bodyStart - shape.attachedRigidbody.position;
        Vector2 delta = bodyEnd - bodyStart;
        Vector3 scale = shape.transform.lossyScale;
        if (shape is CircleCollider2D circle && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) < .00001f)
        {
            float radius = circle.radius * Mathf.Abs(scale.x);
            sweep = new Sweep(center, delta, Vector2.one * radius, true, radius); return true;
        }
        if (shape is BoxCollider2D box && box.edgeRadius == 0f &&
            Mathf.Abs(Mathf.Sin(shape.transform.eulerAngles.z * Mathf.Deg2Rad)) < .00001f)
        {
            Vector2 half = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) * .5f;
            sweep = new Sweep(center, delta, half, false, 0f); return true;
        }
        return false;
    }

    internal static bool Intervals(Collider2D shape, Vector2 bodyStart, Vector2 bodyEnd,
        Rect[] regions, List<Vector2> intervals)
    {
        intervals.Clear();
        // Rigidbody2D.position can already be authoritative while Transform still shows the
        // preceding pose inside a receiver/physics callback. Native bounds use the actual shape.
        if (!TryPrepare(shape, bodyStart, bodyEnd, out Sweep sweep)) return false;
        foreach (Rect region in regions) Append(sweep, region, intervals);
        Merge(intervals); return true;
    }

    internal static void PreparedIntervals(Sweep sweep, Rect region, List<Vector2> intervals)
    {
        intervals.Clear(); Append(sweep, region, intervals); Merge(intervals);
    }

    private static void Append(Sweep sweep, Rect region, List<Vector2> intervals)
    {
        Vector2 center = sweep.Center, delta = sweep.Delta, half = sweep.Half;
        // Conservative rejection only. The guard admits extra candidates to the unchanged
        // narrow phase; it never expands the actual protection or changes a returned interval.
        if (sweep.Max.x < region.xMin || sweep.Min.x > region.xMax ||
            sweep.Max.y < region.yMin || sweep.Min.y > region.yMax) return;
        if (sweep.Circle)
        {
                float radius = sweep.Radius;
                // A rounded rectangle is two strips plus four discs. An expanded AABB alone
                // would incorrectly report entry near each rounded corner.
                AddRect(center, delta, Rect.MinMaxRect(region.xMin - radius, region.yMin,
                    region.xMax + radius, region.yMax), intervals);
                AddRect(center, delta, Rect.MinMaxRect(region.xMin, region.yMin - radius,
                    region.xMax, region.yMax + radius), intervals);
                AddCircle(center, delta, new Vector2(region.xMin, region.yMin), radius, intervals);
                AddCircle(center, delta, new Vector2(region.xMin, region.yMax), radius, intervals);
                AddCircle(center, delta, new Vector2(region.xMax, region.yMin), radius, intervals);
                AddCircle(center, delta, new Vector2(region.xMax, region.yMax), radius, intervals);
        }
        else
        {
                AddRect(center, delta, Rect.MinMaxRect(region.xMin - half.x, region.yMin - half.y,
                    region.xMax + half.x, region.yMax + half.y), intervals);
        }
    }
    private static void Merge(List<Vector2> intervals)
    {
        intervals.Sort((left, right) => left.x.CompareTo(right.x));
        for (int i = 1; i < intervals.Count;)
        {
            Vector2 previous = intervals[i - 1];
            Vector2 current = intervals[i];
            if (current.x <= previous.y)
            {
                intervals[i - 1] = new Vector2(previous.x, Mathf.Max(previous.y, current.y));
                intervals.RemoveAt(i);
            }
            else i++;
        }
    }

    private static void AddRect(Vector2 origin, Vector2 delta, Rect rect, List<Vector2> result)
    {
        float enter = 0f, exit = 1f;
        if (Slab(origin.x, delta.x, rect.xMin, rect.xMax, ref enter, ref exit) &&
            Slab(origin.y, delta.y, rect.yMin, rect.yMax, ref enter, ref exit))
            result.Add(new Vector2(enter, exit));
    }

    private static bool Slab(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.0000001f) return origin >= min && origin <= max;
        float first = (min - origin) / delta, last = (max - origin) / delta;
        if (first > last) { float swap = first; first = last; last = swap; }
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, last);
        return enter <= exit;
    }

    private static void AddCircle(Vector2 origin, Vector2 delta, Vector2 center, float radius, List<Vector2> result)
    {
        Vector2 offset = origin - center;
        double a = Vector2.Dot(delta, delta);
        double c = Vector2.Dot(offset, offset) - (double)radius * radius;
        if (a < 1e-14) { if (c <= 0) result.Add(new Vector2(0f, 1f)); return; }
        double b = 2d * Vector2.Dot(offset, delta);
        double discriminant = b * b - 4d * a * c;
        if (discriminant < 0d) return;
        double root = Math.Sqrt(discriminant);
        float enter = Mathf.Max(0f, (float)((-b - root) / (2d * a)));
        float exit = Mathf.Min(1f, (float)((-b + root) / (2d * a)));
        if (enter <= exit) result.Add(new Vector2(enter, exit));
    }
}
