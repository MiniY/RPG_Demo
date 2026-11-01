using System.Collections.Generic;
using System.Linq;
using MapVNext;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MapVNextR3EditTests
{
    [Test] public void RelativeLowerNeighbours_DoNotEndAtHigherPlatforms_ButEndAtRealDropsAndWater()
    {
        foreach(var c in MapVNextFixture.Cases().Take(60))
        {
            var map=MapVNextFixture.Build(c);
            foreach(var d in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
            {
                // Open-floor cells next to the rectangle on all four sides share a continuous foundation.
                Vector2Int p=d==Vector2Int.up?new Vector2Int(10,5+c.Height):d==Vector2Int.down?new Vector2Int(10,4):
                    d==Vector2Int.left?new Vector2Int(7,5+c.Height-1):new Vector2Int(8+c.Width,5+c.Height-1);
                Assert.AreEqual(c.Lower,map.GetElevation(p));
                if(c.Lower>0)Assert.AreEqual(13,MapVNextProjection.TopRole(map,p,c.Lower),c.Id+":"+p);
            }
            int upper=c.Lower+1;
            Assert.AreEqual(4,MapVNextProjection.TopRole(map,new Vector2Int(8,5+c.Height-1),upper));
            Assert.AreEqual(6,MapVNextProjection.TopRole(map,new Vector2Int(7+c.Width,5+c.Height-1),upper));
            Assert.AreEqual(20,MapVNextProjection.TopRole(map,new Vector2Int(8,5),upper));
            Assert.AreEqual(22,MapVNextProjection.TopRole(map,new Vector2Int(7+c.Width,5),upper));
            var q=new Vector2Int(3,3);map.SetElevation(q,0);map.SetTerrain(q,MapTerrainType.DeepWater);
            if(c.Lower>0)Assert.AreEqual(21,MapVNextProjection.TopRole(map,q+Vector2Int.up,c.Lower));
        }
    }
    [TestCase(false)] [TestCase(true)]
    public void DerivedSlope_UsesSameAtomicMapDataEdges_MirrorsEast_AndTranslatesOrigin(bool east)
    {
        var origin=new Vector2Int(-31,23);var map=new MapData(12,12,origin);
        for(int y=2;y<10;y++)for(int x=5;x<10;x++)map.SetElevation(origin+new Vector2Int(x,y),1);
        Vector2Int low=origin+new Vector2Int(east?10:4,4),dir=east?Vector2Int.left:Vector2Int.right;
        for(int i=0;i<2;i++)map.AddStair(low+Vector2Int.up*i,low+dir+Vector2Int.up*i,
            MapStairVisualComposition.UpperAndLower,east?MapStairVisualSide.RightOnly:MapStairVisualSide.LeftOnly);
        var go=new GameObject("R3 translated-grid test",typeof(Grid));
        var child=new GameObject("Ground",typeof(Tilemap));child.transform.SetParent(go.transform,false);go.transform.position=new Vector3(10,-6,0);
        try
        {
            var coordinates=new MapCoordinateBoundary(child.GetComponent<Tilemap>());
            var ramp=MapVNextStairGeometry.Derive(map,coordinates).Single();
            Assert.AreEqual(low,ramp.Lower);Assert.AreEqual(low+dir,ramp.Upper);
            Assert.AreEqual(3,ramp.Segments.Length);Assert.AreEqual(1,ramp.UpperElevation);Assert.AreEqual(0,ramp.LowerElevation);
            Assert.Greater(ramp.Exit.y-ramp.Entry.y,3.3f);Assert.AreEqual(east?-1:1,Mathf.Sign(ramp.Exit.x-ramp.Entry.x));
            Assert.IsTrue(map.CanTraverse(low,low+dir));Assert.IsFalse(map.CanTraverse(low,low+dir+Vector2Int.up),"Direct diagonal cell edges remain forbidden");
            Assert.AreEqual("SLOPE",ramp.Phase(ramp.Middle));
        }
        finally{Object.DestroyImmediate(go);}
    }
}
