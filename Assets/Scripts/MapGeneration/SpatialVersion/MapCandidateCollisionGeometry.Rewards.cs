using System;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    // Predict the same native root circle that RewardPickup.Awake creates.
    public bool IsOriginalPickupPoseClear(Transform source, Vector2 position)
    {
        if (!MapSpatialRewardPlacement.UsesImplicitPickupCircle(source)) return IsPotentialPoseClear(source, position);
        return IsPolygonClear(OriginalImplicitPickupPolygon(source, position));
    }

    /// <summary>Conservative bounds of the same potential pickup geometry at the origin.</summary>
    public Rect GetOriginalPickupBounds(Transform source)
    {
        var shapes = MapSpatialRewardPlacement.UsesImplicitPickupCircle(source)
            ? new System.Collections.Generic.List<Polygon> { OriginalImplicitPickupPolygon(source, Vector2.zero) }
            : PotentialPolygons(source, Vector2.zero);
        if (shapes.Count == 0) throw new InvalidOperationException("MissingOriginalPickupGeometry");
        Rect bounds = default;
        for (int i = 0; i < shapes.Count; i++)
        {
            Polygon shape = shapes[i];
            float rim = shape.IsCircle ? 0f : shape.ContactRadius;
            Rect current = Rect.MinMaxRect(shape.Bounds.xMin - rim, shape.Bounds.yMin - rim,
                shape.Bounds.xMax + rim, shape.Bounds.yMax + rim);
            bounds = i == 0 ? current : Rect.MinMaxRect(Mathf.Min(bounds.xMin, current.xMin), Mathf.Min(bounds.yMin, current.yMin),
                Mathf.Max(bounds.xMax, current.xMax), Mathf.Max(bounds.yMax, current.yMax));
        }
        return bounds;
    }

    private Polygon OriginalImplicitPickupPolygon(Transform source, Vector2 position)
    {
        Vector3 x = source.localToWorldMatrix.MultiplyVector(Vector3.right), y = source.localToWorldMatrix.MultiplyVector(Vector3.up);
        float sx = x.magnitude, sy = y.magnitude;
        if (sx <= 0 || Mathf.Abs(sx - sy) > .00001f || Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .00001f)
            throw new InvalidOperationException("UnsupportedImplicitPickupCircleScale");
        return PlannedPolygon(new MapPlannedSolidGeometry.Shape(position, .5f * sx));
    }
}
