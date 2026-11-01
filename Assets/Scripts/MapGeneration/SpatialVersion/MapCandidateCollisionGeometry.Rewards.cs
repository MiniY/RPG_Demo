using System;
using UnityEngine;

public sealed partial class MapCandidateCollisionGeometry
{
    // The frozen original RewardPickup.Awake adds a root CircleCollider2D only when
    // no root collider exists. Its native default is .5, zero offset; it becomes a
    // trigger before the first enable. Predict that complete shape before creation.
    public bool IsOriginalPickupPoseClear(Transform source, Vector2 position)
    {
        if(!MapSpatialRewardPlacement.UsesImplicitPickupCircle(source))return IsPotentialPoseClear(source,position);
        Vector3 x=source.localToWorldMatrix.MultiplyVector(Vector3.right),y=source.localToWorldMatrix.MultiplyVector(Vector3.up);
        float sx=x.magnitude,sy=y.magnitude;
        if(sx<=0 || Mathf.Abs(sx-sy)>.00001f || Mathf.Abs(Vector3.Dot(x.normalized,y.normalized))>.00001f)
            throw new InvalidOperationException("UnsupportedImplicitPickupCircleScale");
        return IsPolygonClear(PlannedPolygon(new MapPlannedSolidGeometry.Shape(position,.5f*sx)));
    }
}
