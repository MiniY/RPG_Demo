using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owned potential solid configuration at a planned pose; independent of live enabled/death state.</summary>
public sealed class MapPlannedSolidGeometry
{
    internal sealed class Shape
    {
        internal readonly Vector2[] Points;
        internal readonly Vector2 Center;
        internal readonly float Radius;
        internal bool IsCircle => Points == null;
        internal Shape(Vector2[] points) { Points = (Vector2[])points.Clone(); }
        internal Shape(Vector2 center, float radius) { Center = center; Radius = radius; }
    }
    private readonly Shape[] shapes;
    private MapPlannedSolidGeometry(string logicalId, Shape[] shapes) { LogicalId = logicalId; this.shapes = shapes; }
    public string LogicalId { get; }
    public int ShapeCount => shapes.Length;
    internal Shape ShapeAt(int index) => shapes[index];

    public static MapPlannedSolidGeometry Capture(string logicalId, Transform sourceRoot,
        Vector2 plannedBodyPosition, Quaternion plannedBodyRotation)
        => CaptureCore(logicalId, sourceRoot, plannedBodyPosition, plannedBodyRotation, true);

    public static MapPlannedSolidGeometry CaptureLive(string logicalId, Transform sourceRoot)
        => CaptureCore(logicalId, sourceRoot, sourceRoot != null ? (Vector2)sourceRoot.position : default,
            sourceRoot != null ? sourceRoot.rotation : Quaternion.identity, false);

    private static MapPlannedSolidGeometry CaptureCore(string logicalId, Transform sourceRoot,
        Vector2 plannedBodyPosition, Quaternion plannedBodyRotation, bool potential)
    {
        if (string.IsNullOrWhiteSpace(logicalId) || sourceRoot == null || !Finite(plannedBodyPosition) ||
            Mathf.Abs(plannedBodyRotation.x) > .00001f || Mathf.Abs(plannedBodyRotation.y) > .00001f)
            throw Invalid("InvalidPlannedSolidPose");
        Vector3 scale = sourceRoot.lossyScale;
        Vector3 x = sourceRoot.localToWorldMatrix.MultiplyVector(Vector3.right);
        Vector3 y = sourceRoot.localToWorldMatrix.MultiplyVector(Vector3.up);
        if (!Finite(new Vector2(scale.x, scale.y)) || Mathf.Abs(scale.x * scale.y) < .000001f ||
            Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .00001f)
            throw Invalid("UnsupportedPlannedSolidShear");
        Matrix4x4 plannedRoot = Matrix4x4.TRS(plannedBodyPosition, plannedBodyRotation, scale);
        var result = new List<Shape>();
        // Planning captures potential solids even while hidden or pooled.
        // The explicit live capture derives only currently active solids.
        foreach (Collider2D collider in sourceRoot.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.isTrigger) continue;
            if (!potential && (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated)) continue;
            if (collider.usedByComposite || collider.usedByEffector) throw Invalid("UnsupportedPlannedSolidComposition");
            Matrix4x4 relative = Matrix4x4.identity;
            for (Transform part = collider.transform; part != sourceRoot; part = part.parent)
            {
                if (part == null) throw Invalid("PlannedSolidHierarchyMismatch");
                relative = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale) * relative;
            }
            Matrix4x4 pose = plannedRoot * relative;
            if (collider is BoxCollider2D box && box.edgeRadius == 0f)
            {
                Vector2 half = box.size * .5f;
                if (!Finite(half) || half.x <= 0f || half.y <= 0f) throw Invalid("InvalidPlannedBox");
                AddPolygon(result, pose, box.offset, new[] { new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y),
                    new Vector2(half.x, half.y), new Vector2(-half.x, half.y) });
            }
            else if (collider is CircleCollider2D circle)
            {
                Vector3 axisX = pose.MultiplyVector(Vector3.right), axisY = pose.MultiplyVector(Vector3.up);
                float sx = axisX.magnitude, sy = axisY.magnitude;
                if (Mathf.Abs(sx - sy) > .00001f || Mathf.Abs(Vector3.Dot(axisX.normalized, axisY.normalized)) > .00001f)
                    throw Invalid("UnsupportedPlannedCircleScale");
                float radius = circle.radius * sx;
                Vector2 center = pose.MultiplyPoint3x4(circle.offset);
                if (!Finite(center) || !Finite(new Vector2(radius, radius)) || radius <= 0f) throw Invalid("InvalidPlannedCircle");
                result.Add(new Shape(center, radius));
            }
            else if (collider is PolygonCollider2D polygon)
            {
                for (int path = 0; path < polygon.pathCount; path++) AddPolygon(result, pose, polygon.offset, polygon.GetPath(path));
            }
            else throw Invalid("UnsupportedPlannedSolidShape");
        }
        return new MapPlannedSolidGeometry(logicalId, result.ToArray());
    }
    private static void AddPolygon(List<Shape> result, Matrix4x4 pose, Vector2 offset, Vector2[] points)
    {
        if (points.Length < 3) throw Invalid("InvalidPlannedPolygon");
        var world = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            world[i] = pose.MultiplyPoint3x4(points[i] + offset);
            if (!Finite(world[i])) throw Invalid("NonFinitePlannedPolygon");
        }
        result.Add(new Shape(world));
    }
    private static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    private static MapPlanningException Invalid(string code) => new MapPlanningException(code, "Planned solid configuration could not be certified: " + code, false);
}
