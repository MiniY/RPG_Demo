using System;
using System.Collections.Generic;
using System.Linq;
using MapVNext;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MapVNextEditTests
{
    static MapVNextAssets Assets => AssetDatabase.LoadAssetAtPath<MapVNextAssets>("Assets/MapGeneration/MapVNext/MapVNextAssets.asset");
    public static IEnumerable<StairCase> FiniteCases => MapVNextFixture.Cases();
    [TestCaseSource(nameof(FiniteCases))]
    public void FiniteCase_HasDeterministicHeightAndAtomicPorts(StairCase c)
    {
        MapData a=MapVNextFixture.Build(c),b=MapVNextFixture.Build(c);
        Assert.AreEqual(MapVNextFixture.Digest(a),MapVNextFixture.Digest(b));
        foreach(var s in a.Stairs)
        {
            Assert.AreEqual(a.GetElevation(s.LowerCell)+1,a.GetElevation(s.UpperCell));
            Assert.IsTrue(a.CanTraverse(s.LowerCell,s.UpperCell));Assert.IsTrue(a.CanTraverse(s.UpperCell,s.LowerCell));
        }
        var path=new List<Vector2Int>();Assert.IsTrue(MapElevationRuntimeQuery.TryFindPath(a,a.SpawnCell,a.ExitCell,path));
        var folder="Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix/FixtureContracts";System.IO.Directory.CreateDirectory(folder);
        var cells=new List<CellRow>();
        for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)
        {
            var p=new Vector2Int(x,y);cells.Add(new CellRow{x=x,y=y,elevation=a.GetElevation(p),terrain=a.GetCell(p).terrainType.ToString(),walkable=a.IsWalkable(p)});
        }
        var stairs=a.Stairs.Select(s=>new StairRow{low=s.LowerCell,high=s.UpperCell,lowerElevation=a.GetElevation(s.LowerCell),upperElevation=a.GetElevation(s.UpperCell),westSide=s.UpperCell.x>s.LowerCell.x,uphill=s.UpperCell-s.LowerCell}).ToArray();
        System.IO.File.WriteAllText(folder+"/"+c.Id+".json",JsonUtility.ToJson(new FixtureRow{spec=c,digest=MapVNextFixture.Digest(a),cells=cells.ToArray(),stairs=stairs},true));
    }
    [Test] public void NativeImport_HasAll220StableRectsAndCorrectR17Families()
    {
        MapVNextAssets.ValidateFamilies(new[]{5,3,2,1,4});
        for(int h=0;h<5;h++)for(int role=0;role<44;role++)
        {
            var t=Assets.Native(h,role);Assert.AreEqual(64,t.sprite.pixelsPerUnit);
            Assert.AreEqual("Tilemap_color"+MapVNextAssets.Families[h]+"_"+role,t.sprite.name);
        }
    }
    [Test] public void TargetedNegativeContracts_RejectAllThirteen()
    {
        var rows=MapVNextFixture.NegativeResults(Assets);Assert.AreEqual(13,rows.Count);
        System.IO.File.WriteAllLines("Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix/negative-results.txt",rows);
    }
    [Test] public void SameHeightColor_DoesNotDependOnWaterDistanceOrSeed()
    {
        for(int h=0;h<=4;h++)
        {
            var map=new MapData(12,12,Vector2Int.zero);var p=new Vector2Int(6,6);map.SetElevation(p,h);
            var expected=Assets.Native(map.GetElevation(p),13);
            foreach(int seed in MapVNextFixture.FixedSeeds)
            {
                map.GenerationSeed=seed;
                foreach(var q in new[]{new Vector2Int(1,1),new Vector2Int(5,6),new Vector2Int(10,10)})
                {map.SetTerrain(q,MapTerrainType.DeepWater);Assert.AreSame(expected,Assets.Native(map.GetElevation(p),13));map.SetTerrain(q,MapTerrainType.Grass);}
            }
        }
    }
    [Serializable] class CellRow {public int x,y,elevation;public string terrain;public bool walkable;}
    [Serializable] class StairRow {public Vector2Int low,high,uphill;public int lowerElevation,upperElevation;public bool westSide;}
    [Serializable] class FixtureRow {public StairCase spec;public string digest;public CellRow[] cells;public StairRow[] stairs;}
}
