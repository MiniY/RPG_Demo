using System;
using System.Linq;
using MapVNext;
using NUnit.Framework;
using UnityEngine;

public sealed class MapVNextP2ContractTests
{
    static MapData Fresh()
    {
        var result=MapVNextRandomLayout.Generate(21193);
        Assert.AreEqual("SUCCESS",result.Summary.status, string.Join(";",result.Summary.attempts.Select(a=>a.reason)));
        return result.Map;
    }
    [TestCase(21193)] [TestCase(42)] [TestCase(-2026)]
    public void PilotInputs_CreateConnectedNonRectangularPlatformsWithRealHeight(int seed)
    {
        var result=MapVNextRandomLayout.Generate(seed);var summary=result.Summary;
        Assert.AreEqual("SUCCESS",summary.status,string.Join(";",summary.attempts.Select(a=>a.reason)));
        Assert.AreEqual(summary.walkableCells,summary.reachableCells);
        Assert.GreaterOrEqual(summary.roots,2);Assert.LessOrEqual(summary.roots,4);
        Assert.IsTrue(summary.platforms.Any(p=>p.turns>4&&p.footprintArea<p.width*p.height));
        Assert.AreEqual(summary.platforms.Length,summary.stairs.Length);
        Assert.IsTrue(summary.stairs.All(s=>s.upperHeight==s.lowerHeight+1));
        Assert.That(summary.maxElevation,Is.InRange(1,4));
        Assert.AreEqual(summary.semanticHash,MapVNextRandomLayout.SemanticHash(result.Map));
    }
    [Test] public void SemanticHash_ExcludesSeedLabelAndGenerationClock()
    {
        var map=Fresh();string before=MapVNextRandomLayout.SemanticHash(map);map.GenerationSeed=-12345;
        Assert.AreEqual(before,MapVNextRandomLayout.SemanticHash(map));
        map.SetElevation(new Vector2Int(3,3),1);
        Assert.AreNotEqual(before,MapVNextRandomLayout.SemanticHash(map));
    }
    [Test] public void SameInput_ProducesSameSemanticMapAndAttempts()
    {
        var a=MapVNextRandomLayout.Generate(21193);var b=MapVNextRandomLayout.Generate(21193);
        Assert.AreEqual(a.Summary.semanticHash,b.Summary.semanticHash);
        Assert.AreEqual(a.Summary.attempt,b.Summary.attempt);
        CollectionAssert.AreEqual(a.Summary.attempts.Select(x=>x.reason),b.Summary.attempts.Select(x=>x.reason));
    }
    [Test] public void SignedSeedAndSeparatedPlanningStreams_DoNotUseAbsoluteSeed()
    {
        Assert.AreNotEqual(MapVNextRandomLayout.Mix(1,0,1),MapVNextRandomLayout.Mix(-1,0,1));
        Assert.AreNotEqual(MapVNextRandomLayout.Mix(1,0,1),MapVNextRandomLayout.Mix(1,0,2));
    }
    [Test] public void NoLegalMouth_IsDetectedBeforeProjection()
    {
        var map=new MapData(20,20,Vector2Int.zero);
        foreach(var p in new RectInt(8,8,6,3).allPositionsWithin)map.SetElevation(p,1);
        Assert.IsFalse(MapVNextRandomLayout.HasLegalPort(map,1));
    }
    [Test] public void BlockedUpperMouth_IsRejectedAtomically()
    {
        var map=Fresh();map.SetTerrain(map.Stairs[0].UpperCell,MapTerrainType.Mountain);
        Assert.Throws<InvalidOperationException>(()=>MapVNextRandomLayout.Validate(map,new MapVNextLayoutConfig()));
    }
    [Test] public void BlockedLowerMouth_IsRejectedAtomically()
    {
        var map=Fresh();map.SetTerrain(map.Stairs[0].LowerCell,MapTerrainType.Mountain);
        Assert.Throws<InvalidOperationException>(()=>MapVNextRandomLayout.Validate(map,new MapVNextLayoutConfig()));
    }
    [Test] public void TwoGradeStair_IsRejectedByExistingMapData()
    {
        var map=new MapData(12,12,Vector2Int.zero);map.SetElevation(new Vector2Int(6,5),2);
        Assert.Throws<ArgumentException>(()=>map.AddStair(new Vector2Int(5,5),new Vector2Int(6,5)));
    }
    [Test] public void OverlappingThirdLane_IsRejected()
    {
        var map=Fresh();var stair=map.Stairs[0];
        map.AddStair(stair.LowerCell+Vector2Int.up*2,stair.UpperCell+Vector2Int.up*2);
        Assert.Throws<InvalidOperationException>(()=>MapVNextRandomLayout.Validate(map,new MapVNextLayoutConfig()));
    }
    [Test] public void ClosedCliffBypass_IsRejected()
    {
        var map=Fresh();var stair=map.Stairs[0];map.SetElevation(stair.LowerCell+Vector2Int.down,map.GetElevation(stair.UpperCell));
        Assert.Throws<InvalidOperationException>(()=>MapVNextRandomLayout.Validate(map,new MapVNextLayoutConfig()));
    }
    [Test] public void SixthHeight_IsExplicitlyUnsupported()
    {Assert.Throws<NotSupportedException>(()=>MapVNextRandomLayout.Generate(1,new MapVNextLayoutConfig{maxElevation=5}));}
    [Test] public void InvalidSize_IsExplicitlyRejectedInsteadOfFallingBackToFixture()
    {Assert.Throws<ArgumentException>(()=>MapVNextRandomLayout.Generate(1,new MapVNextLayoutConfig{width=24,height=18}));}
}
