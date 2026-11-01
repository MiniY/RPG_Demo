using System.Collections;
using MapVNext;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

public sealed class MapVNextPlayTests
{
    [UnityTest] public IEnumerator BaseEightConnections_UseFormalPlayerInputPhysicsBothWaysAndBlockClosedCliffs()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextStairs.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var demo=Object.FindObjectOfType<MapVNextDemo>();Assert.NotNull(demo);
        Assert.IsTrue(MapVNextDemo.IsolationReady);
        Assert.AreEqual(.3f,demo.Foot.radius,.0001f);Assert.IsFalse(demo.Foot.isTrigger);
        Assert.IsTrue(demo.Player.GetComponent<CapsuleCollider2D>().isTrigger);
        Assert.IsFalse(System.Array.Exists(demo.Overlay.GetComponents<MonoBehaviour>(),m=>m.GetType().Name=="TowerRunUiController"));
        for(int i=0;i<8;i++)
        {
            demo.Load(i);yield return null;yield return demo.RoundTrip(true,false);
            var face=demo.Terrain.WaterBaseTilemap.transform.parent.Find("GeneratedElevation"+(demo.Current.Lower+1)+"Face").GetComponent<UnityEngine.Tilemaps.Tilemap>();
            Assert.That(face.GetTile<UnityEngine.Tilemaps.Tile>(new Vector3Int(10,4,0)),Is.EqualTo(demo.Assets.Native(demo.Current.Lower+1,35)),"The visible south face must use native rock, never a grass cap");
            Assert.AreEqual(i+1,demo.Evidence.Count);Assert.IsTrue(demo.Evidence[i].uphill);Assert.IsTrue(demo.Evidence[i].downhill);Assert.IsTrue(demo.Evidence[i].cliffBlocked);
        }
        System.IO.File.WriteAllLines("Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix/playmode-movement.csv",demo.Trace);
        System.IO.File.WriteAllText("Artifacts/MapAcceptance/MapVNextStairs-20261028-r3-ReviewFix/playmode-base.json",JsonUtility.ToJson(new BaseRows{cases=demo.Evidence.ToArray()},true));
    }
    [System.Serializable] class BaseRows {public MapVNextDemo.CaseEvidence[] cases;}

    [UnityTest] public IEnumerator RenderedHeightColor_IsIndependentOfNearbyDistantOrAbsentWater()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MapVNext/MapVNextStairs.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        var demo=Object.FindObjectOfType<MapVNextDemo>();
        for(int h=0;h<=4;h++)
        {
            demo.Load(h==0?0:h-1);yield return null;
            var p=h==0?new Vector2Int(4,12):new Vector2Int(10,9);
            Assert.AreEqual(h,demo.Map.GetElevation(p));
            var tm=h==0?demo.Terrain.GrassOverlayTilemap:demo.Terrain.WaterBaseTilemap.transform.parent.Find("GeneratedElevation"+h+"Top").GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if(h>0)Assert.AreEqual(demo.Assets.Native(h,13).sprite.rect,tm.GetSprite(MapVNextProjection.V(p)).rect);
            foreach(var water in new[]{p+Vector2Int.up,new Vector2Int(20,13)})
            {
                // A render-only color probe, not a claim that arbitrary multi-grade water drops are playable.
                int originalHeight=demo.Map.GetElevation(water);
                demo.Map.SetElevation(water,0);
                demo.Map.SetTerrain(water,MapTerrainType.DeepWater);
                MapVNextProjection.Render(demo.Map,demo.Terrain,demo.Assets.SourceSettings,demo.Assets);
                Assert.AreEqual(demo.Assets.Native(h,13).sprite.texture,tm.GetSprite(MapVNextProjection.V(p)).texture);
                demo.Map.SetTerrain(water,MapTerrainType.Grass);
                demo.Map.SetElevation(water,originalHeight);
            }
            MapVNextProjection.Render(demo.Map,demo.Terrain,demo.Assets.SourceSettings,demo.Assets);
            Assert.AreEqual(demo.Assets.Native(h,13).sprite.texture,tm.GetSprite(MapVNextProjection.V(p)).texture);
        }
    }
}
