using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MapVNextI1RewardTests
{
    [Test] public void AllSevenOriginalRewardPrefabsHaveCompletePreactivationGroundWithoutAssetEdits()
    {
        string[] paths=Directory.GetFiles("Assets/Prefabs/Rewards","*.prefab").OrderBy(p=>p,StringComparer.Ordinal).ToArray();Assert.That(paths.Length,Is.EqualTo(7));
        var runtime=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab"));
        var settings=ScriptableObject.CreateInstance<MapGenerationSettings>();var staging=new GameObject("InactiveRewardAudit");staging.SetActive(false);int implicitCount=0;
        try
        {
            var terrain=runtime.GetComponentInChildren<MapTilemapRenderer>(true);var decorations=runtime.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
            var map=new MapData(4,4,Vector2Int.zero);var geometry=MapCandidateCollisionGeometry.Capture(Guid.NewGuid(),map,new MapSimpleDecorationData(),settings,terrain,decorations);
            Vector2 centre=terrain.Coordinates.CellToWorld(new Vector2Int(1,1));
            foreach(string path in paths)
            {
                byte[] original=File.ReadAllBytes(path);var source=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);bool implicitCircle=MapSpatialRewardPlacement.UsesImplicitPickupCircle(source.transform);
                Assert.That(geometry.IsOriginalPickupPoseClear(source.transform,centre),Is.True,path);
                if(implicitCircle)
                {implicitCount++;Vector2 edge=terrain.Coordinates.CellToWorld(new Vector2Int(0,1))-Vector3.right*.25f;Assert.That(geometry.IsOriginalPickupPoseClear(source.transform,edge),Is.False,"Whole original .5 circle reaches outside floor");}
                var actor=UnityEngine.Object.Instantiate(source,staging.transform,false);actor.SetActive(false);actor.transform.SetParent(null,false);actor.transform.position=centre;
                try
                {
                    MapSpatialRewardPlacement.PrepareOriginalPickupCollider(actor);Assert.That(actor.activeSelf,Is.False);Assert.That(actor.GetComponent<Collider2D>().isTrigger,Is.True);
                    if(implicitCircle) {var circle=actor.GetComponent<CircleCollider2D>();Assert.That(circle.radius,Is.EqualTo(.5f));Assert.That(circle.offset,Is.EqualTo(Vector2.zero));}
                    Assert.That(geometry.IsPotentialPoseClear(actor.transform,centre),Is.True);
                    Assert.That(File.ReadAllBytes(path),Is.EqualTo(original));
                }
                finally {UnityEngine.Object.DestroyImmediate(actor);}
            }
            Assert.That(implicitCount,Is.GreaterThan(0),"Original collider-less rewards must be covered");
        }
        finally {UnityEngine.Object.DestroyImmediate(staging);UnityEngine.Object.DestroyImmediate(settings);UnityEngine.Object.DestroyImmediate(runtime);}
    }
}
