using System;
using System.Collections;
using System.IO;
using MapVNext;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public sealed class MapVNextR3RegressionTests
{
    [UnityTest] public IEnumerator InterfaceAndRelativeLowerSeam_AllEightPairs_KeepTrueDownwardBorders()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextStairs.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;var demo=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
        for(int i=0;i<8;i++)
        {
            demo.Load(i);yield return null;var grid=demo.Terrain.WaterBaseTilemap.transform.parent;
            var high=grid.Find("GeneratedElevation"+(demo.Current.Lower+1)+"Top").GetComponent<Tilemap>();
            var low=demo.Current.Lower==0?demo.Terrain.GrassOverlayTilemap:grid.Find("GeneratedElevation"+demo.Current.Lower+"Top").GetComponent<Tilemap>();
            Vector2Int p=demo.Current.UpperPort,q=demo.Current.LowerPort+Vector2Int.up*3;
            Assert.AreEqual(demo.Current.Side=="West"?12:14,demo.Assets.Role(high.GetTile<Tile>(MapVNextProjection.V(p))),demo.Current.Id+": lower interface cap");
            Assert.AreEqual(13,demo.Assets.Role(high.GetTile<Tile>(MapVNextProjection.V(p+Vector2Int.up))),demo.Current.Id+": upper mouth");
            Assert.AreEqual(demo.Current.Lower==0?9:13,demo.Assets.Role(low.GetTile<Tile>(MapVNextProjection.V(q))),demo.Current.Id+": relative lower floor continues under higher neighbour");
            Assert.AreEqual(21,demo.Assets.Role(high.GetTile<Tile>(new Vector3Int(10,5,0))),"True south drop cap retained");
            Assert.AreEqual(5,demo.Assets.Role(high.GetTile<Tile>(new Vector3Int(10,10,0))),"True north boundary retained");
            if(demo.Current.Lower>0)
            {
                var water=new Vector2Int(3,3);demo.Map.SetElevation(water,0);demo.Map.SetTerrain(water,MapTerrainType.DeepWater);
                MapVNextProjection.Render(demo.Map,demo.Terrain,demo.Assets.SourceSettings,demo.Assets);
                Assert.AreEqual(21,demo.Assets.Role(low.GetTile<Tile>(new Vector3Int(3,4,0))),"True water-facing lower-tier boundary retained");
                demo.Map.SetTerrain(water,MapTerrainType.Grass);demo.Map.SetElevation(water,demo.Current.Lower);
            }
        }
    }
    [UnityTest] public IEnumerator Seed21193_ClimbsAllFourAndReturnsThroughSameJoinedMouths()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextStairs.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;var demo=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
        foreach(int index in new[]{60,62}){demo.Load(index);yield return null;yield return demo.SeedRoundTrip();}
        Assert.AreEqual(2,demo.Evidence.Count);Assert.IsTrue(demo.Evidence[0].uphill);Assert.IsTrue(demo.Evidence[0].downhill);Assert.IsTrue(demo.Evidence[1].uphill);Assert.IsTrue(demo.Evidence[1].downhill);
        File.WriteAllLines("Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix/seed21193-playmode.csv",demo.Trace);
    }
    [UnityTest] public IEnumerator AllTwelveSeedApproaches_UseActualFootClearanceWithoutChangingMapData()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextStairs.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;var demo=UnityEngine.Object.FindObjectOfType<MapVNextDemo>();
        for(int index=60;index<72;index++){demo.Load(index);yield return null;demo.ValidateSeedApproachRoutes();}
    }
}
