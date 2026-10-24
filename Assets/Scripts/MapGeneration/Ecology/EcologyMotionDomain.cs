using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Clips proposed translations against a frozen surface. This never certifies actual physics history.</summary>
internal sealed class EcologyMotionDomain
{
    private readonly Rect bounds;
    private readonly Rect[] excluded, protection;
    private readonly List<Vector2> intervals = new List<Vector2>();

    internal EcologyMotionDomain(MapCandidateCollisionGeometry geometry, bool[] allowed, bool[] protectedSurface)
    {
        bounds = geometry.WorldSurfaceBounds;
        excluded = Rectangles(geometry, allowed, false);
        protection = Rectangles(geometry, protectedSurface, true);
    }

    private static Rect[] Rectangles(MapCandidateCollisionGeometry geometry, bool[] mask, bool selected)
    {
        var result = new List<Rect>();
        for (int y = 0; y < geometry.Height; y++)
        for (int x = 0; x < geometry.Width;)
        {
            if (mask[y * geometry.Width + x] != selected) { x++; continue; }
            int first = x++;
            while (x < geometry.Width && mask[y * geometry.Width + x] == selected) x++;
            Rect left = geometry.CellSurfaceRect(y * geometry.Width + first);
            Rect right = geometry.CellSurfaceRect(y * geometry.Width + x - 1);
            result.Add(Rect.MinMaxRect(left.xMin, left.yMin, right.xMax, right.yMax));
        }
        return result.ToArray();
    }

    internal bool TryClip(Collider2D foot, Vector2 start, Vector2 target, out Vector2 clipped)
    {
        clipped = start;
        if (foot == null || foot.attachedRigidbody == null || !Finite(start) || !Finite(target) ||
            !EcologyProtectionGeometry.TryPrepare(foot, start, target, out var sweep)) return false;
        Vector2 center = sweep.Center;
        Vector2 half = foot.bounds.extents, delta = target - start;
        float limit = 1f;
        if (!LimitAxis(center.x, half.x, delta.x, bounds.xMin, bounds.xMax, ref limit) ||
            !LimitAxis(center.y, half.y, delta.y, bounds.yMin, bounds.yMax, ref limit)) return false;
        if (!LimitRegions(foot, sweep, center, half, excluded, false, ref limit) ||
            !LimitRegions(foot, sweep, center, half, protection, true, ref limit)) return false;
        // A tiny world-space retreat keeps rounding from submitting a forbidden boundary contact.
        if (limit < 1f && delta.sqrMagnitude > 0f) limit = Mathf.Max(0f, limit - .00001f / delta.magnitude);
        clipped = limit >= 1f ? target : start + delta * limit;
        return true;
    }

    internal bool TryFirstExcludedWitness(Collider2D foot, Vector2 start, Vector2 end, out float fraction, out Vector2 witness)
    {
        fraction = float.PositiveInfinity; witness = start;
        if (!EcologyProtectionGeometry.TryPrepare(foot, start, end, out var sweep)) return false;
        Vector2 center = sweep.Center;
        Vector2 delta = end - start, half = foot.bounds.extents;
        foreach (Rect region in excluded)
        {
            EcologyProtectionGeometry.PreparedIntervals(sweep, region, intervals);
            foreach (Vector2 interval in intervals)
            {
                float middle = (interval.x + interval.y) * .5f;
                if (interval.x >= fraction || !PositiveOverlap(foot, center + delta * middle, half, region)) continue;
                fraction = interval.x; witness = start + delta * middle;
            }
        }
        return fraction < float.PositiveInfinity;
    }

    private static bool LimitAxis(float center, float half, float delta, float min, float max, ref float limit)
    {
        if (center - half < min || center + half > max) return false;
        if (delta > 0f) limit = Mathf.Min(limit, (max - center - half) / delta);
        else if (delta < 0f) limit = Mathf.Min(limit, (min - center + half) / delta);
        return true;
    }

    private bool LimitRegions(Collider2D foot, EcologyProtectionGeometry.Sweep sweep, Vector2 center, Vector2 half,
        Rect[] regions, bool closedContact, ref float limit)
    {
        Vector2 delta = sweep.Delta;
        foreach (Rect region in regions)
        {
            EcologyProtectionGeometry.PreparedIntervals(sweep, region, intervals);
            foreach (Vector2 interval in intervals)
            {
                if (!closedContact && !PositiveOverlap(foot, center + delta * ((interval.x + interval.y) * .5f), half, region)) continue;
                if (interval.x == 0f && (closedContact || PositiveOverlap(foot, center, half, region))) return false;
                limit = Mathf.Min(limit, interval.x);
            }
        }
        return true;
    }

    private static bool PositiveOverlap(Collider2D foot, Vector2 center, Vector2 half, Rect rect)
    {
        if (foot is CircleCollider2D)
        {
            double dx = Math.Max(rect.xMin - center.x, Math.Max(0d, center.x - rect.xMax));
            double dy = Math.Max(rect.yMin - center.y, Math.Max(0d, center.y - rect.yMax));
            return dx * dx + dy * dy < (double)half.x * half.x;
        }
        return center.x + half.x > rect.xMin && center.x - half.x < rect.xMax &&
            center.y + half.y > rect.yMin && center.y - half.y < rect.yMax;
    }
    private static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y);
}
