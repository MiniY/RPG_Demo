#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure shape/rectangle intersections for a certified straight translation, never a motion proof.</summary>
internal static class EcologyProtectionGeometryExhaustiveOracle
{
    internal static bool Intervals(Collider2D shape, Vector2 bodyStart, Vector2 bodyEnd,
        Rect[] regions, List<Vector2> intervals)
    {
        intervals.Clear();
        // Rigidbody2D.position can already be authoritative while Transform still shows the
        // preceding pose inside a receiver/physics callback. Native bounds use the actual shape.
        Vector2 center = (Vector2)shape.bounds.center +
                         bodyStart - shape.attachedRigidbody.position;
        Vector2 delta = bodyEnd - bodyStart;
        Vector3 scale = shape.transform.lossyScale;
        CircleCollider2D circle = shape as CircleCollider2D;
        BoxCollider2D box = shape as BoxCollider2D;
        if (circle != null && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) < 0.00001f)
        {
            float radius = circle.radius * Mathf.Abs(scale.x);
            foreach (Rect region in regions)
            {
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
        }
        else if (box != null && box.edgeRadius == 0f &&
                 Mathf.Abs(Mathf.Sin(shape.transform.eulerAngles.z * Mathf.Deg2Rad)) < 0.00001f)
        {
            Vector2 half = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) * 0.5f;
            foreach (Rect region in regions)
                AddRect(center, delta, Rect.MinMaxRect(region.xMin - half.x, region.yMin - half.y,
                    region.xMax + half.x, region.yMax + half.y), intervals);
        }
        else return false;

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
        return true;
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

#endif
