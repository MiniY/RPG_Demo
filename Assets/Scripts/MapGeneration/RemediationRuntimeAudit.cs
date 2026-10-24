using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Read-only acceptance observation. Deliberately reads rendered layers instead of the qualification function.</summary>
public static class RemediationRuntimeAudit
{
    [Serializable] public sealed class Region
    { public int id, target, planned, active, registered, capacity, validArea, minimumGroup; public string type; }
    [Serializable] public sealed class Snapshot
    {
        public string profile, generation, layout, surfacePolicy;
        public int seed, floor, decorations, plants, faceTiles, nativeFacePaths, planned, active, registered;
        public List<Region> regions = new List<Region>();
        public List<string> violations = new List<string>();
    }
    [Serializable] public sealed class NativeGeometrySnapshot
    {
        public int testedPoses, clearPoses, blockedPoses, contactBoundaryPoses, stairSweeps, nativeColliders;
        public List<string> mismatches = new List<string>();
        public string scope = "Actual unchanged player circle at all walkable centres against native static projection. Contact boundary <=0.03 is recorded separately. No physics simulation or gameplay actor teleport.";
    }

    public static NativeGeometrySnapshot CompareNativeProjection(MapGenerationController controller, CircleCollider2D originalFoot)
    {
        var result = new NativeGeometrySnapshot(); MapData map = controller.LastGeneratedMap;
        var terrain = controller.TilemapRenderer;
        var basis = MapCandidateCollisionGeometry.Capture(Guid.NewGuid(), map, controller.LastGeneratedSimpleDecorations,
            controller.Settings, terrain, controller.SimpleDecorationRenderer);
        var obstacles = terrain.GroundTilemap.transform.parent.GetComponentsInChildren<Collider2D>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.shapeCount > 0 &&
                !(c is TilemapCollider2D tile && tile.usedByComposite)).ToArray();
        result.nativeColliders = obstacles.Length;
        var probeObject = new GameObject("Owned acceptance native geometry probe");
        var probeBody = probeObject.AddComponent<Rigidbody2D>();
        probeBody.bodyType = RigidbodyType2D.Kinematic; probeBody.gravityScale = 0f;
        var probe = probeObject.AddComponent<CircleCollider2D>();
        probe.radius = originalFoot.radius; probe.offset = originalFoot.offset;
        probeObject.transform.localScale = originalFoot.transform.lossyScale;
        Vector2 mapMinimum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x, map.Origin.y, 0));
        Vector2 mapMaximum = terrain.WaterBaseTilemap.CellToWorld(new Vector3Int(map.Origin.x + map.Width, map.Origin.y + map.Height, 0));
        try
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
            {
                var cell = new Vector2Int(x, y); if (!map.IsWalkable(cell)) continue;
                Vector2 world = terrain.Coordinates.CellToWorld(cell); probeObject.transform.position = world; Physics2D.SyncTransforms();
                Bounds footBounds = probe.bounds;
                bool expected = footBounds.min.x >= mapMinimum.x && footBounds.min.y >= mapMinimum.y &&
                    footBounds.max.x <= mapMaximum.x && footBounds.max.y <= mapMaximum.y;
                float minimumGap = float.PositiveInfinity;
                foreach (var obstacle in obstacles)
                {
                    ColliderDistance2D distance = probe.Distance(obstacle);
                    if (!distance.isValid) throw new InvalidOperationException("Native acceptance oracle has no valid shape distance.");
                    minimumGap = Mathf.Min(minimumGap, distance.distance);
                    if (distance.isOverlapped) expected = false;
                }
                result.testedPoses++;
                bool actual = basis.IsTranslationClear(probe, world, world, 0f);
                if (expected && minimumGap <= .03f) { result.contactBoundaryPoses++; continue; }
                if (expected) result.clearPoses++; else result.blockedPoses++;
                if (actual != expected) result.mismatches.Add("Cell=" + cell + " plannedClear=" + actual + " nativeClear=" + expected + " gap=" + minimumGap.ToString("R"));
            }
            foreach (var stair in map.Stairs)
            {
                Vector2 lower = terrain.Coordinates.CellToWorld(stair.LowerCell), upper = terrain.Coordinates.CellToWorld(stair.UpperCell);
                result.stairSweeps++;
                if (!basis.IsTranslationClear(probe, lower, upper, 0f)) result.mismatches.Add("Stair blocked=" + stair.LowerCell + "->" + stair.UpperCell);
            }
        }
        finally { probe.enabled = false; UnityEngine.Object.Destroy(probeObject); }
        return result;
    }

    public static Snapshot Capture(MapGenerationController controller, int floor)
    {
        var context = controller.Orchestrator.Context;
        if (!context.IsReady) throw new InvalidOperationException("Acceptance can observe only a committed Ready generation.");
        MapData map = context.ActiveMap; var plan = context.ActivePlacementPlan;
        var population = controller.GetComponentInParent<MapEcologyPopulationService>();
        var layout = plan.Ecology;
        var result = new Snapshot { seed = map.GenerationSeed, floor = floor, generation = context.ActiveGenerationId.ToString(),
            profile = controller.GetComponentInParent<MapRuntimeBootstrap>().DiagnosticSnapshot.ActiveRequest.ProfileId,
            layout = layout.Signature, surfacePolicy = controller.Settings is MapSurfaceV4Settings ? MapSurfaceV4Policy.Version : MapRemediationSurfacePolicy.Version,
            decorations = controller.LastGeneratedSimpleDecorations.Count };
        var actors = context.ActiveRegistry.Entries.Where(e => e.Role == MonsterPlacementService.MonsterRole).ToArray();
        result.planned = population.LastPlan.ActualMonsters;
        result.registered = actors.Count(e => e.Instance != null);
        result.active = population.LastPlan.Placements.Where(p => p.Role == MonsterPlacementService.MonsterRole)
            .Count(p => population.TryGetActiveInstance(p.LogicalId, out var actor) && ((Component)actor).gameObject.activeInHierarchy);
        foreach (var region in layout.Regions.Where(r => !r.IsSafety))
        {
            var residents = layout.Residents.Where(r => r.HomeRegionId == region.Id).ToArray();
            var row = new Region { id = region.Id, target = region.AssignedPopulation, capacity = region.ConstructiveCapacity,
                planned = residents.Length, type = region.TypeId, validArea = region.Nodes.Count, minimumGroup = 3 };
            row.registered = actors.Count(a => a.Instance != null && residents.Any(r => r.Id == a.LogicalObjectId));
            row.active = residents.Count(r => population.TryGetActiveInstance(r.Id, out var actor) && ((Component)actor).gameObject.activeInHierarchy);
            result.regions.Add(row);
            if (row.target < 3 || row.target > row.capacity || row.planned != row.target || row.active != row.target || row.registered != row.target)
                result.violations.Add("Population/region=" + row.id);
        }
        if (result.planned != result.active || result.planned != result.registered) result.violations.Add("Population/totals");
        var root = controller.TilemapRenderer.WaterBaseTilemap.transform.parent;
        var tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        var faces = tilemaps.Where(t => t.name.StartsWith("GeneratedElevation") && t.name.EndsWith("Face")).ToArray();
        var stairs = tilemaps.Where(t => t.name.StartsWith("GeneratedElevation") && t.name.EndsWith("Stairs")).ToArray();
        var foam = tilemaps.SingleOrDefault(t => t.name == "GeneratedWaterFoam");
        foreach (var face in faces) foreach (Vector3Int cell in face.cellBounds.allPositionsWithin) if (face.HasTile(cell)) result.faceTiles++;
        var native = root.GetComponentsInChildren<PolygonCollider2D>(true).SingleOrDefault(c => c.name == "GeneratedCliffFaces_R3");
        result.nativeFacePaths = native != null ? native.pathCount : 0;
        if (result.faceTiles > 0 && result.nativeFacePaths == 0) result.violations.Add("MissingNativeFaces");
        foreach (var placement in controller.LastGeneratedSimpleDecorations.Placements)
        {
            var variant = controller.Settings.simpleDecorationPalette.FindVariant(placement.VariantId);
            var supports = variant.TileParts.Where(p => p.RenderLayer == MapSimpleDecorationRenderLayer.Ground).Select(p => p.Offset)
                .Concat(variant.CollisionOffsets).Append(Vector2Int.zero).Distinct();
            foreach (var offset in supports) CheckRoot(placement.AnchorCell + offset, map.GetElevation(placement.AnchorCell), "Decoration/" + placement.VariantId);
        }
        foreach (var reservation in plan.Reservations.Where(r => r.Role == PlantPlacementService.PlantRole))
        { result.plants++; CheckRoot(reservation.AnchorCell, map.GetElevation(reservation.AnchorCell), "Plant/" + reservation.LogicalObjectId); }
        return result;

        void CheckRoot(Vector2Int cell, int level, string label)
        {
            var index = new Vector3Int(cell.x, cell.y, 0);
            if (!map.IsInside(cell) || !map.IsWalkable(cell) || map.GetElevation(cell) != level) Fail("UnsupportedGround");
            else if (faces.Any(t => t.HasTile(index)) || level == 0 && controller.TilemapRenderer.ElevationTilemap.HasTile(index)) Fail("ActualProjectedFace");
            else if (stairs.Any(t => t.HasTile(index)) || map.Stairs.Any(s => s.LowerCell == cell || s.UpperCell == cell)) Fail("ActualStair");
            else if (level == 0 && foam != null && foam.HasTile(index)) Fail("ActualVisibleFoam");
            void Fail(string reason) => result.violations.Add(label + "/" + reason + "@" + cell);
        }
    }
}
