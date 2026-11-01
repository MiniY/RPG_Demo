using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MapVNextI1GroundTests
{
    private static Vector2[] Box(float x0,float y0,float x1,float y1) => new[] {new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)};
    [Test] public void CompleteGroundCanShareEdgeButAnyPositiveAreaIsForbidden()
    {
        var ground=Box(0,0,1,1);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(1,0,2,1)),Is.False);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(1,1,2,2)),Is.False);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(.99999f,0,2,1)),Is.True);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(.2f,.2f,.3f,.3f)),Is.True);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(-1,-1,2,2)),Is.True);
    }
    [Test] public void PublicGroundEntryRejectsDegenerateAndConcaveDeclarationsEvenWithNoObstacles()
    {
        GameObject runtime=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab"));
        var settings=ScriptableObject.CreateInstance<MapGenerationSettings>();
        try
        {
            var terrain=runtime.GetComponentInChildren<MapTilemapRenderer>(true);
            var decorations=runtime.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
            var map=new MapData(4,4,Vector2Int.zero);
            var geometry=MapCandidateCollisionGeometry.Capture(Guid.NewGuid(),map,new MapSimpleDecorationData(),settings,terrain,decorations);
            Vector2 centre=terrain.Coordinates.CellToWorld(new Vector2Int(1,1));
            Assert.Throws<ArgumentException>(()=>geometry.IsWorldGroundPolygonClear(new[] {centre,centre,centre}));
            var concave=new[] {new Vector2(0,0),new Vector2(1,0),new Vector2(.25f,.25f),new Vector2(1,1),new Vector2(0,1)}.Select(p=>centre+p*.1f).ToArray();
            Assert.Throws<ArgumentException>(()=>geometry.IsWorldGroundPolygonClear(concave));
            Assert.That(geometry.IsWorldGroundPolygonClear(Box(centre.x-.1f,centre.y-.1f,centre.x+.1f,centre.y+.1f)),Is.True);
        }
        finally {UnityEngine.Object.DestroyImmediate(runtime);UnityEngine.Object.DestroyImmediate(settings);}
    }
    [Test] public void SubFloatIntersectionPenetrationMustRemainBlocked()
    {
        var ground=Box(1,1,2,2);
        var obstacle=new[] {new Vector2(0,1.9999998807907104f),new Vector2(4,2.000000238418579f),new Vector2(0,3)};
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,obstacle),Is.True,"Double working points must preserve the real positive-area sliver.");
    }
    [Test] public void TransformedAndReversedGroundKeepsInteriorOverlap()
    {
        Func<Vector2,Vector2> transform=p => new Vector2(7+2*p.x-p.y,-4+p.x+2*p.y);
        var ground=Box(0,0,1,1).Select(transform).Reverse().ToArray();
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(1,0,2,1).Select(transform).ToArray()),Is.False);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(.999f,0,2,1).Select(transform).ToArray()),Is.True);
        Assert.That(MapCandidateCollisionGeometry.GroundInteriorsOverlap(ground,Box(1.1f,0,2,1).Select(transform).ToArray()),Is.False);
    }
}
