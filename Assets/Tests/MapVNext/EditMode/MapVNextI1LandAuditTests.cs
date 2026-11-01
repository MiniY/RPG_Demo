using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using MapVNext;

/// <summary>Diagnostic data from the real retained numerical generator. This does not claim production Ready.</summary>
public sealed class MapVNextI1LandAuditTests
{
    [Serializable] private sealed class Row
    {
        public int seed, walkable, eligible, largestEligibleSquare, necessaryNativePorts, portsWithEligibleUpperFootprint;
        public string layoutStatus, layoutFailure, baseDigest;
    }
    [Serializable] private sealed class Report { public string scope = "DIAGNOSTIC_ONLY_NOT_READY_OR_PHYSICS_PROOF"; public List<Row> rows = new List<Row>(); }
    [Test] public void InspectActualRetainedLandAndAllFourRepresentativeInputs()
    {
        var strategy = UnityEditor.AssetDatabase.LoadAssetAtPath<MapVNextFormalTerrain>("Assets/MapGeneration/MapVNext/I1/Terrain.asset");
        var original = UnityEditor.AssetDatabase.LoadAssetAtPath<MapGenerationSettings>("Assets/MapGeneration/MapVNext/I1/Settings.asset");
        string output = Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE"); var report = new Report();
        foreach (int seed in new[] { 42, 21193, 7727, 314159 })
        {
            var settings = UnityEngine.Object.Instantiate(original); settings.seed = seed;
            try
            {
                MapData map = strategy.BaseTerrain.Generate(settings, false);
                string before = MapVNextFixture.Digest(map); var row = new Row { seed = seed, baseDigest = before };
                var csv = new List<string> { "x,y,terrain,elevation,rootEligible" }; var squares = new int[map.Width, map.Height];
                bool Eligible(Vector2Int p) => map.IsInside(p) && !map.IsBorder(p, 2) && map.IsWalkable(p) && map.GetCell(p).terrainType != MapTerrainType.Path &&
                    Mathf.Max(Math.Abs(p.x-map.SpawnCell.x),Math.Abs(p.y-map.SpawnCell.y)) > settings.spawnProtectionRadius+2 &&
                    Mathf.Max(Math.Abs(p.x-map.ExitCell.x),Math.Abs(p.y-map.ExitCell.y)) > 5 &&
                    new[] { Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right }.All(d => map.IsInside(p+d) && map.IsWalkable(p+d));
                for (int y=0;y<map.Height;y++) for (int x=0;x<map.Width;x++)
                {
                    var p=map.Origin+new Vector2Int(x,y); bool eligible=Eligible(p);
                    if(map.IsWalkable(p)) row.walkable++; if(eligible) row.eligible++;
                    if(eligible) squares[x,y]=1+(x==0||y==0?0:Math.Min(squares[x-1,y],Math.Min(squares[x,y-1],squares[x-1,y-1])));
                    row.largestEligibleSquare=Math.Max(row.largestEligibleSquare,squares[x,y]);
                    csv.Add(x+","+y+","+map.GetCell(p).terrainType+","+map.GetElevation(p)+","+eligible);
                }
                for(int y=2;y<map.Height-4;y++) for(int x=3;x<map.Width-3;x++) foreach(var d in new[]{Vector2Int.left,Vector2Int.right})
                {
                    var low=map.Origin+new Vector2Int(x,y); bool clear=true, eligible=true;
                    for(int u=-3;u<=3;u++) for(int v=-2;v<=4;v++)
                    {
                        if(u>0&&v < -1) continue; var p=low+d*u+Vector2Int.up*v;
                        if(!map.IsInside(p)||!map.IsWalkable(p)) clear=false;
                        if(u>0&&!Eligible(p)) eligible=false;
                    }
                    if(clear) row.necessaryNativePorts++; if(clear&&eligible) row.portsWithEligibleUpperFootprint++;
                }
                try { var candidate=MapVNextRandomLayout.GenerateOnTerrain(map,seed,settings.spawnProtectionRadius);row.layoutStatus="LAYOUT_ONLY_ACCEPTED";File.WriteAllText(Path.Combine(output,"layout-"+seed+".txt"),MapVNextFixture.Digest(candidate)); }
                catch(MapPlanningException error) {row.layoutStatus="BOUNDED_LAYOUT_REJECTION";row.layoutFailure=error.Message;}
                Assert.That(MapVNextFixture.Digest(map),Is.EqualTo(before),"Diagnostics and rejected layouts must not mutate base terrain.");
                report.rows.Add(row);File.WriteAllLines(Path.Combine(output,"base-land-"+seed+".csv"),csv);
                File.WriteAllText(Path.Combine(output,"land-audit.json"),JsonUtility.ToJson(report,true));
            }
            finally {UnityEngine.Object.DestroyImmediate(settings);}
        }
        Assert.That(report.rows.Count,Is.EqualTo(4));
    }
    [Serializable] private sealed class StaticConflictReport
    {
        public string scope="DIAGNOSTIC_ONLY_RECONSTRUCTION_NOT_READY",buildId,generation,baseDigest,afterDigest,errorCode,errorMessage,existingOwner,incomingOwner,variantId;
        public int seed=2061438863,rawDecorations,filteredDecorations;
        public bool emptyPlanSucceeded,fullPlanSucceeded;
        public Vector2Int spawn,exit,conflictCell,anchor;
        public Vector2Int[] collisionCells;
        public List<string> reservations=new List<string>();
    }
    [Test] public void ActualFailedSeedStaticDeclarationIsRejectedAndVNextFiltersBeforePlanning()
    {
        var report=new StaticConflictReport {buildId=MapHostBuildStamp.BuildId,scope="REGRESSION_ORIGINAL_STRICT_NEGATIVE_AND_CURRENT_PREFILTERED_PLAN_NOT_READY"};
        string output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");Directory.CreateDirectory(output);
        var strategy=UnityEditor.AssetDatabase.LoadAssetAtPath<MapVNextFormalTerrain>("Assets/MapGeneration/MapVNext/I1/Terrain.asset");
        var settings=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<MapGenerationSettings>("Assets/MapGeneration/MapVNext/I1/Settings.asset"));
        var runtime=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/RandomMapRuntime.prefab"));
        MapData map=null;
        try
        {
            var attempt=new MapGenerationRequest(Guid.NewGuid(),report.seed).CreateAttempt(0,Guid.NewGuid());
            settings.seed=attempt.AttemptSeed;report.generation=attempt.GenerationId.ToString();
            map=strategy.Generate(settings,true);report.baseDigest=MapVNextFixture.Digest(map);report.spawn=map.SpawnCell;report.exit=map.ExitCell;
            var terrain=runtime.GetComponentInChildren<MapTilemapRenderer>(true);var decorations=runtime.GetComponentInChildren<MapSimpleDecorationRenderer>(true);
            var space=strategy.CreateSpatialContext(attempt.GenerationId,map,terrain,settings);
            var raw=strategy.GenerateDecorations(map,settings);report.rawDecorations=raw.Count;
            var filtered=MapSpatialDecorationFilter.Filter(space,raw,settings,terrain,decorations);report.filteredDecorations=filtered.Count;
            var plan=MapStaticPlacementPlanner.Build(attempt.GenerationId,map,new MapSimpleDecorationData(),settings);report.emptyPlanSucceeded=true;
            foreach(var reservation in plan.Reservations)report.reservations.Add(reservation.LogicalObjectId+":"+string.Join(";",reservation.OccupiedCells));
            // This declaration comes from the real retained generator, not a moved object.
            var illegal=raw.Placements.Single(p=>p.VariantId=="scattered-rock-03"&&p.AnchorCell==new Vector2Int(37,47));
            var illegalVariant=settings.simpleDecorationPalette.FindVariant(illegal.VariantId);
            var declaration=new MapSimpleDecorationData();
            typeof(MapSimpleDecorationData).GetMethod("Add",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(declaration,new object[]{illegal,illegalVariant});
            var negative=Assert.Throws<MapPlanningException>(()=>MapStaticPlacementPlanner.Build(attempt.GenerationId,map,declaration,settings));
            report.errorCode=negative.Code;report.errorMessage=negative.Message;report.conflictCell=illegal.AnchorCell;
            report.existingOwner=plan.Reservations.First(r=>r.OccupiedCells.Contains(illegal.AnchorCell)).LogicalObjectId;
            report.incomingOwner="isolated-retained-collision-declaration";report.variantId=illegal.VariantId;report.anchor=illegal.AnchorCell;
            report.collisionCells=illegalVariant.CollisionOffsets.Select(offset=>illegal.AnchorCell+offset).ToArray();
            Assert.That(report.errorCode,Is.EqualTo("StaticConstraintConflict"));
            Assert.That(report.errorMessage,Is.EqualTo("Cell (37, 47) is already reserved."));
            Assert.That(report.existingOwner,Is.EqualTo("map.stairs"));
            Assert.That(filtered.Placements.Any(p=>p.VariantId==illegal.VariantId&&p.AnchorCell==illegal.AnchorCell),Is.False);
            foreach(var placement in filtered.Placements)
                foreach(var offset in settings.simpleDecorationPalette.FindVariant(placement.VariantId).CollisionOffsets)
                    Assert.That(plan.IsOccupied(placement.AnchorCell+offset),Is.False,"No filtered collision declaration may reuse an original static reservation.");
            MapStaticPlacementPlanner.Build(attempt.GenerationId,map,filtered,settings);report.fullPlanSucceeded=true;
            Assert.That(raw.Count,Is.EqualTo(report.rawDecorations));
            Assert.That(MapVNextFixture.Digest(map),Is.EqualTo(report.baseDigest));
        }
        finally
        {
            if(map!=null)report.afterDigest=MapVNextFixture.Digest(map);
            File.WriteAllText(Path.Combine(output,"failed-seed-static-reservation-diagnostic.json"),JsonUtility.ToJson(report,true));
            UnityEngine.Object.DestroyImmediate(runtime);UnityEngine.Object.DestroyImmediate(settings);
        }
    }

}
