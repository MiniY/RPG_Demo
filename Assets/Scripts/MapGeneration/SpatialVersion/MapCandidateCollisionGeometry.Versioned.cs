using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    private MapNavigationLink[] QualifySpatialLinks(Collider2D foot, bool[] legal)
    {
        if (SpatialContext == null) return Array.Empty<MapNavigationLink>();
        var footprint = CaptureFootprint(foot, 0f); var result = new List<MapNavigationLink>();
        foreach (var link in SpatialContext.NavigationLinks)
        {
            if (link.From < 0 || link.To < 0 || link.From >= legal.Length || link.To >= legal.Length || !legal[link.From] || !legal[link.To]) continue;
            var points = link.Points.ToArray();
            for (int i = 1; i < points.Length - 1; i++) points[i] -= footprint.Offset;
            bool valid = true;
            for (int i = 1; valid && i < points.Length; i++) valid = Clear(footprint, points[i - 1], points[i]);
            if (valid) result.Add(new MapNavigationLink(link.From, link.To, points));
        }
        return result.ToArray();
    }
    private bool IsSpatialLinkInSurface(Collider2D foot, MapNavigationLink link, bool[] surface, bool[] protection)
    {
        var domain = new EcologyMotionDomain(this, surface, protection);
        for (int i = 1; i < link.Points.Count; i++)
            if (!domain.TryClip(foot, link.Points[i - 1], link.Points[i], out Vector2 end) || !end.Equals(link.Points[i])) return false;
        return true;
    }
    public void ValidateVersionedStairAccess(MapCandidateCollisionGeometry statics, Collider2D foot, Vector2Int spawn)
    {
        if (SpatialContext == null || !ReferenceEquals(SpatialContext, statics.SpatialContext)) throw Invalid("MixedStairGeometryContext");
        var graph = statics.BuildNavigationGraph(foot, 0f);
        var reachable = graph.BuildReturnField(new[] { (spawn.y - Origin.y) * Width + spawn.x - Origin.x });
        foreach (var link in SpatialContext.NavigationLinks)
            if (graph.FindLink(link.From, link.To) == null || reachable.Distances[link.From] < 0 || reachable.Distances[link.To] < 0)
            {
                var before=BuildNavigationGraph(foot,0f);var from=before.BuildReturnField(new[]{(spawn.y-Origin.y)*Width+spawn.x-Origin.x});
                // This is the same candidate-quality rejection as retained declared stairs.
                // The original ecology owner spends its unchanged finite budget; no plan is published.
                throw new MapPlanningException("CriticalRouteBlocked", "Seed="+SpatialContext.Map.GenerationSeed+"; versioned stair="+link.From+"->"+link.To+
                    "; terrainDecor="+(before.FindLink(link.From,link.To)!=null)+","+from.Distances[link.From]+","+from.Distances[link.To]+
                    "; roleStatics="+(graph.FindLink(link.From,link.To)!=null)+","+reachable.Distances[link.From]+","+reachable.Distances[link.To],false);
            }
    }
    public bool IsWorldGroundPolygonClear(Vector2[] points)
    {
        ValidateGroundPolygon(points);
        if (points.Any(p => !Finite(p) || p.x < floorBounds.xMin || p.x > floorBounds.xMax || p.y < floorBounds.yMin || p.y > floorBounds.yMax)) return false;
        Vector2 min=points[0],max=points[0];foreach(Vector2 point in points) {min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
        foreach (Polygon other in polygons)
        {
            if(max.x<=other.Bounds.xMin || min.x>=other.Bounds.xMax || max.y<=other.Bounds.yMin || min.y>=other.Bounds.yMax) continue;
            if (other.IsCircle)
            {
                if (Contains(points, other.Center)) return false;
                for (int i=0;i<points.Length;i++)
                    if (SegmentDistanceSquared(other.Center,other.Center,points[i],points[(i+1)%points.Length]) < (double)other.ContactRadius*other.ContactRadius) return false;
            }
            else if (GroundInteriorsOverlapValidated(points, other.Points)) return false;
        }
        return true;
    }
    private static bool IsDeclaredGroundOnly(Transform actor) => actor != null && actor.GetComponentsInChildren<Collider2D>(true).Length == 0 && actor.GetComponent<MapGroundFootprint>() != null;
    private static Vector2[] DeclaredWorldGround(Transform actor, Vector2 position)
    {
        Matrix4x4 pose=Matrix4x4.TRS(position,actor.rotation,actor.lossyScale);
        return actor.GetComponent<MapGroundFootprint>().Vertices.Select(p => (Vector2)pose.MultiplyPoint3x4(p)).ToArray();
    }
    public bool IsPotentialPoseClear(Transform actor, Vector2 position) => IsDeclaredGroundOnly(actor) ? IsWorldGroundPolygonClear(DeclaredWorldGround(actor, position)) : PotentialPolygons(actor, position).All(IsPolygonClear);
    public bool IsPlannedTranslationClear(Transform actor, Vector2 from, Vector2 to)
    {
        if (IsDeclaredGroundOnly(actor)) return IsWorldGroundPolygonClear(ConvexHull(DeclaredWorldGround(actor, from).Concat(DeclaredWorldGround(actor, to))));
        var first = PotentialPolygons(actor, from); var last = PotentialPolygons(actor, to);
        if (first.Count != last.Count) return false;
        for (int i = 0; i < first.Count; i++)
        {
            Polygon a = first[i], b = last[i];
            if (a.IsCircle)
            {
                var foot = new Footprint(true, a.Center - from, Vector2.one * a.ContactRadius, a.ContactRadius);
                if (!Clear(foot, from, to)) return false;
            }
            else
            {
                Vector2[] hull = ConvexHull(a.Points.Concat(b.Points));
                if (!IsPolygonClear(PlannedPolygon(new MapPlannedSolidGeometry.Shape(hull)))) return false;
            }
        }
        return true;
    }
    private bool IsPolygonClear(Polygon query)
    {
        float rim = query.IsCircle ? 0 : query.ContactRadius;
        if (query.Bounds.xMin - rim < floorBounds.xMin || query.Bounds.xMax + rim > floorBounds.xMax || query.Bounds.yMin - rim < floorBounds.yMin || query.Bounds.yMax + rim > floorBounds.yMax) return false;
        foreach (Polygon other in polygons) if (PlannedOverlap(query, other)) return false;
        return true;
    }
    private List<Polygon> PotentialPolygons(Transform actor, Vector2 position)
    {
        if (actor == null) throw Invalid("MissingGroundOccupant");
        var potential = MapPlannedSolidGeometry.Capture("ground-occupant", actor, position, actor.rotation);
        var result = new List<Polygon>(); for (int i = 0; i < potential.ShapeCount; i++) result.Add(PlannedPolygon(potential.ShapeAt(i)));
        if (result.Count > 0) return result;
        Matrix4x4 rootPose = Matrix4x4.TRS(position, actor.rotation, actor.lossyScale);
        foreach (var collider in actor.GetComponentsInChildren<Collider2D>(true))
        {
            if (!collider.isTrigger) continue;
            Matrix4x4 pose = rootPose * actor.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            if (collider is CircleCollider2D circle)
            {
                float sx = pose.MultiplyVector(Vector3.right).magnitude, sy = pose.MultiplyVector(Vector3.up).magnitude;
                if (Math.Abs(sx - sy) > .00001f) throw Invalid("NonUniformTriggerGroundCircle");
                result.Add(PlannedPolygon(new MapPlannedSolidGeometry.Shape(pose.MultiplyPoint3x4(circle.offset), circle.radius * sx)));
            }
            else
            {
                Vector2[] vertices;
                if (collider is BoxCollider2D box)
                { Vector2 h = box.size / 2; vertices = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), h, new Vector2(-h.x, h.y) }; }
                else if (collider is PolygonCollider2D poly && poly.pathCount == 1) vertices = poly.GetPath(0);
                else throw Invalid("UnsupportedTriggerGroundFootprint");
                result.Add(PlannedPolygon(new MapPlannedSolidGeometry.Shape(vertices.Select(p => (Vector2)pose.MultiplyPoint3x4(p + collider.offset)).ToArray())));
            }
        }
        if (result.Count == 0)
        {
            var declaration = actor.GetComponent<MapGroundFootprint>();
            if (declaration == null) throw Invalid("ExplicitGroundFootprintRequired");
            result.Add(PlannedPolygon(new MapPlannedSolidGeometry.Shape(declaration.Vertices.Select(p => (Vector2)rootPose.MultiplyPoint3x4(p)).ToArray())));
        }
        return result;
    }
    private static Vector2[] ConvexHull(IEnumerable<Vector2> source)
    {
        var points = source.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray(); if (points.Length < 3) throw Invalid("DegenerateGroundHull");
        float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        var hull = new List<Vector2>();
        foreach (var p in points) { while (hull.Count > 1 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        int lower = hull.Count;
        for (int i = points.Length - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        hull.RemoveAt(hull.Count - 1); return hull.ToArray();
    }
    internal bool TryVersionedWaypoint(Collider2D foot, Vector2 position, EcologyNavigationGraph graph, EcologyReturnField field, EcologyMotionDomain domain, out Vector2 waypoint, out int distance)
    {
        waypoint = position; distance = -1; if (SpatialContext == null || graph.GenerationId != GenerationId || field.GenerationId != GenerationId) return false;
        int best = int.MaxValue; float bestLength = float.PositiveInfinity; Vector2 chosenWaypoint = position;
        void Candidate(Vector2 next, int cost, float remaining)
        {
            if (cost < 0 || cost > best || cost == best && remaining >= bestLength || !IsTranslationClear(foot, position, next, 0) ||
                domain == null || !domain.TryClip(foot, position, next, out Vector2 clipped) || !clipped.Equals(next)) return;
            best = cost; bestLength = remaining; chosenWaypoint = next;
        }
        if (TryWorldNode(position, out int node))
        {
            for (int d = -1; d < graph.DirectionCount(node); d++)
            {
                int n = d < 0 ? node : graph.Neighbor(node, d); if (n < 0 || !graph.IsLegal(n) || field.Distances[n] < 0) continue;
                Vector2 centre = centers[n];
                if (!IsLocalTerrainConnector(position, n)) continue;
                if (Vector2.Distance(position, centre) < .035f && field.Next[n] >= 0)
                {
                    int next = field.Next[n]; var link = graph.FindLink(n, next);
                    Candidate(link != null ? link.Points[1] : centers[next], field.Distances[n], (centre - position).magnitude); 
                }
                else Candidate(centre, checked(field.Distances[n] + (Vector2.Distance(position, centre) < .035f ? 0 : 1)), (centre - position).magnitude);
            }
        }
        foreach (var link in graph.ContinuousLinks)
        {
            if (field.Distances[link.To] < 0 || field.Distances[link.From] <= field.Distances[link.To]) continue;
            for (int i = 1; i < link.Points.Count; i++)
            {
                Vector2 a = link.Points[i - 1], b = link.Points[i], delta = b - a;
                float t = delta.sqrMagnitude == 0 ? 1 : Mathf.Clamp01(Vector2.Dot(position - a, delta) / delta.sqrMagnitude);
                if (Vector2.Distance(position, a + delta * t) > .16f) continue;
                int target = t > .98f && i + 1 < link.Points.Count ? i + 1 : i;
                float remaining = Vector2.Distance(position, link.Points[target]);
                for (int j = target + 1; j < link.Points.Count; j++) remaining += Vector2.Distance(link.Points[j - 1], link.Points[j]);
                Candidate(link.Points[target], checked(field.Distances[link.To] + graph.MoveCost(link.From, link.To)), remaining);
            }
        }
        distance = best == int.MaxValue ? -1 : best; waypoint = chosenWaypoint; return distance >= 0;
    }
    internal bool CertifyVersionedRange(Collider2D foot, Vector2 from, Vector2 to, EcologyNavigationGraph graph, EcologyReturnField field, EcologyMotionDomain domain, int limit)
    {
        if (!IsTranslationClear(foot, from, to, 0)) return false;
        // Submitted movement is short; certify a bounded partition as well as the exact physical sweep.
        int steps = Math.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .025f)); if (steps > 256) return false;
        for (int i = 0; i <= steps; i++)
            if (!TryVersionedWaypoint(foot, Vector2.Lerp(from, to, (float)i / steps), graph, field, domain, out _, out int distance) || distance > limit) return false;
        return true;
    }
}

