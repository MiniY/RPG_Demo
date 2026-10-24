using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in joint planning on the original pending map and original role owners.</summary>
public static class EcologyJointPlacementPlanner
{
    public static EcologyPlanningAttemptResult Build(MapGenerationAttempt terrainAttempt, MapData map,
        MapSimpleDecorationData decorations, MapGenerationSettings settings, MapTilemapRenderer terrain,
        MapSimpleDecorationRenderer decoration, Collider2D playerFoot, MerchantPlacementService merchant,
        PlantPlacementService plants, MapEcologyPopulationService population, AnimalPlacementService animals,
        EcologyTerritoryRecipe recipe, EcologyEcotypeDefinition[] types, float exitInteractionRadius, int? recordedAttempt = null,
        MapEcologyProfile retainedPopulation = null)
    {
        if (terrainAttempt == null || merchant == null || plants == null || population == null || animals == null ||
            recipe == null || types == null || float.IsNaN(exitInteractionRadius) || float.IsInfinity(exitInteractionRadius) || exitInteractionRadius <= 0f)
            throw Failure("InvalidRecipe", "Original role owners, retained content and exact exit interaction radius are required.");
        MapCandidateCollisionGeometry basis = MapCandidateCollisionGeometry.Capture(terrainAttempt.GenerationId,
            map, decorations, settings, terrain, decoration);
        MapRemediationSurfacePlan surface = settings.remediationProjectedSurfaces ?
            MapSurfaceV4Policy.Build(map, terrain.TerrainV2Palette, settings) : null;
        MapCandidateCollisionGeometry acceptedStatic = null, acceptedInitial = null;
        int acceptedExitApproach = -1;
        EcologyPlanningAttemptResult result = EcologyPlanningAttempts.Run(map, basis, e =>
        {
            string domain = recipe.Identity + ":terrain=" + terrainAttempt.AttemptIndex.ToString(CultureInfo.InvariantCulture) +
                ":ecology=" + e.ToString(CultureInfo.InvariantCulture);
            int placementSeed = unchecked((int)MapSemanticPlacementPrimitives.StableScore(terrainAttempt.AttemptSeed,
                "eco.role-placement.v1", domain, Vector2Int.zero));
            MapPlacementPlan plan = MapStaticPlacementPlanner.Build(terrainAttempt.GenerationId, map, decorations, settings);
            EcologySafetyRegions preliminary = SelectSafety(basis, map, plan, playerFoot, merchant.Profile, placementSeed, domain);
            Vector2Int merchantCell = Cell(map, preliminary.MerchantNode);
            MerchantPlacementResult selected = merchant.PlanAtCandidate(terrainAttempt, map, plan, placementSeed, merchantCell);
            plants.Plan(terrainAttempt, map, plan, true, placementSeed, surface != null ?
                (Func<Vector2Int, bool>)(cell => surface.SupportsRoot(map, cell, map.GetElevation(cell))) : null);
            population.PlanDestructibles(terrainAttempt, map, plan, placementSeed, retainedPopulation);
            var sources = new Dictionary<string, Transform>(StringComparer.Ordinal)
            { { MerchantPlacementService.MainMerchantLogicalObjectId, ((IMerchantPlacementTarget)merchant.MerchantTarget).PlacementTransform } };
            foreach (PlantPlacementBinding binding in plants.Bindings) sources.Add(binding.LogicalObjectId, binding.PlantTransform);
            foreach (MapEcologyPlacementResult placed in population.LastPlan.Placements) sources.Add(placed.LogicalId, placed.Prefab.transform);
            MapPlannedSolidGeometry[] staticSolids = CaptureRoleSolids(plan, sources, terrain.Coordinates);
            MapCandidateCollisionGeometry statics = ValidateSolids(basis, staticSolids);
            if (settings is MapSurfaceV4Settings) ValidateDeclaredStairAccess(map, basis, statics, playerFoot);
            int approach = Node(map, selected.InteractionApproachCell);
            ulong[] ranks = Ranks(map, placementSeed, domain);
            if (!statics.TryBuildSafetyRegions(playerFoot, Node(map, map.SpawnCell), new[] { Node(map, merchantCell) },
                new[] { approach }, 10, 3, 1, ranks, out EcologySafetyRegions safety, out string safetyFailure))
                throw Failure("InsufficientSafeSpace", safetyFailure);
            var animalSources = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (AnimalPlacementBinding binding in animals.Bindings) animalSources.Add(binding.LogicalObjectId, binding.AnimalTransform);
            MapCandidateCollisionGeometry animalCandidateGeometry = statics;
            string qualifiedAnimal = null;
            bool QualifyAnimal(AnimalPlacementBinding binding, Vector2Int cell)
            {
                // Build once per animal after earlier reservations have committed.
                // The old cell-only policy can otherwise select a foot inside a face.
                if (qualifiedAnimal != binding.LogicalObjectId)
                {
                    animalCandidateGeometry = ValidateSolids(statics, CaptureRoleSolids(plan, animalSources, terrain.Coordinates));
                    qualifiedAnimal = binding.LogicalObjectId;
                }
                var solid = MapPlannedSolidGeometry.Capture(binding.LogicalObjectId, binding.AnimalTransform,
                    terrain.Coordinates.CellToWorld(cell), binding.AnimalTransform.rotation);
                return animalCandidateGeometry.IsPlannedSolidClear(solid);
            }
            animals.Plan(terrainAttempt, map, plan, placementSeed, settings is MapSurfaceV4Settings ?
                (Func<AnimalPlacementBinding, Vector2Int, bool>)QualifyAnimal : null);
            MapPlannedSolidGeometry[] animalSolids = CaptureRoleSolids(plan, animalSources, terrain.Coordinates);
            MapCandidateCollisionGeometry initial = ValidateSolids(statics, animalSolids);
            EcologyTerritoryDraft draft = EcologyTerritoryPlanner.Build(terrainAttempt.AttemptSeed, e, map, statics,
                playerFoot, safety, plan, recipe, types, terrainAttempt.AttemptIndex, initial);
            int exitApproach = ValidateRoutes(map, statics, playerFoot, safety, draft.Layout, terrain.Coordinates, exitInteractionRadius);
            foreach (MapEcologyResident resident in draft.Layout.Residents)
            {
                EcologyEcotypeDefinition type = types.Single(t => t.TypeId == resident.TypeId);
                Vector2 position = terrain.Coordinates.CellToWorld(draft.Layout.CellAt(resident.InitialNode));
                if (!initial.IsTranslationClear(type.MovementFoot, position, position, 0f))
                    throw Failure("JointPopulationCapacityUnavailable", resident.Id);
            }
            if (!draft.Plan.TryAttachEcology(draft.Layout, out string freezeFailure))
                throw Failure("InvalidEcologyPublication", freezeFailure);
            acceptedStatic = statics; acceptedInitial = initial; acceptedExitApproach = exitApproach;
            return draft.Plan;
        }, recordedAttempt);
        if (result.Succeeded) population.AcceptZonedResidents(result.Plan, types);
        return result.WithGeometry(acceptedStatic, acceptedInitial, acceptedExitApproach, basis);
    }

    private static EcologySafetyRegions SelectSafety(MapCandidateCollisionGeometry geometry, MapData map, MapPlacementPlan plan,
        Collider2D playerFoot, MerchantPlacementProfile profile, int seed, string domain)
    {
        EcologyNavigationGraph graph = geometry.BuildNavigationGraph(playerFoot, 0f);
        int spawn = Node(map, map.SpawnCell);
        EcologyReturnField fromSpawn = graph.BuildReturnField(new[] { spawn });
        var merchantNodes = new List<int>(); var approachNodes = new List<int>();
        var request = new MerchantSemanticPlacementRequest(new MapPlacementRequest(MerchantPlacementService.MainMerchantLogicalObjectId,
            MerchantPlacementService.MerchantRole, new[] { Vector2Int.zero }, true, true), profile);
        var policy = new MerchantPlacementPolicy();
        for (int n = 0; n < graph.NodeCount; n++)
        {
            if (fromSpawn.Distances[n] < 0 || fromSpawn.Distances[n] > 10) continue;
            if (!policy.TryEvaluateCandidate(seed, map, plan, request, Cell(map, n), out MerchantPlacementResult qualified, out _)) continue;
            int approach = Node(map, qualified.InteractionApproachCell);
            if (!graph.IsLegal(approach)) continue;
            merchantNodes.Add(n); approachNodes.Add(approach);
        }
        if (!geometry.TryBuildSafetyRegions(playerFoot, spawn, merchantNodes.ToArray(), approachNodes.ToArray(), 10, 3, 1,
            Ranks(map, seed, domain), out EcologySafetyRegions safety, out string failure))
            throw Failure("InsufficientSafeSpace", failure);
        return safety;
    }

    private static MapPlannedSolidGeometry[] CaptureRoleSolids(MapPlacementPlan plan,
        Dictionary<string, Transform> sources, MapCoordinateBoundary coordinates)
    {
        var result = new List<MapPlannedSolidGeometry>();
        foreach (MapPlacementReservation reservation in plan.Reservations.OrderBy(r => r.LogicalObjectId, StringComparer.Ordinal))
        {
            if (!sources.TryGetValue(reservation.LogicalObjectId, out Transform source)) continue;
            Quaternion rotation = reservation.Ownership == MapPlacementOwnership.GenerationScoped ? Quaternion.identity : source.rotation;
            MapPlannedSolidGeometry solid = MapPlannedSolidGeometry.Capture(reservation.LogicalObjectId, source,
                coordinates.CellToWorld(reservation.AnchorCell), rotation);
            if (solid.ShapeCount > 0) result.Add(solid);
        }
        return result.ToArray();
    }

    private static MapCandidateCollisionGeometry ValidateSolids(MapCandidateCollisionGeometry basis, MapPlannedSolidGeometry[] solids)
    {
        MapCandidateCollisionGeometry current = basis;
        foreach (MapPlannedSolidGeometry solid in solids)
        {
            if (!current.IsPlannedSolidClear(solid)) throw Failure("JointSolidOverlap", solid.LogicalId);
            current = current.WithPlannedSolids(new[] { solid });
        }
        return current;
    }

    private static void ValidateDeclaredStairAccess(MapData map, MapCandidateCollisionGeometry basis, MapCandidateCollisionGeometry geometry, Collider2D playerFoot)
    {
        EcologyNavigationGraph graph=geometry.BuildNavigationGraph(playerFoot,0f);
        var reachable=graph.BuildReturnField(new[]{Node(map,map.SpawnCell)});
        foreach(var stair in map.Stairs)
        {
            int lower=Node(map,stair.LowerCell),upper=Node(map,stair.UpperCell);
            if(!graph.IsLegal(lower) || !graph.IsLegal(upper) || reachable.Distances[lower]<0 || reachable.Distances[upper]<0 ||
                !graph.CanMove(lower,upper) || !graph.CanMove(upper,lower))
            {
                var before=basis.BuildNavigationGraph(playerFoot,0f);var from=before.BuildReturnField(new[]{Node(map,map.SpawnCell)});
                throw Failure("CriticalRouteBlocked","Seed="+map.GenerationSeed+"; Declared stair access="+stair.LowerCell+"->"+stair.UpperCell+
                    "; terrainDecor="+before.IsLegal(lower)+","+before.IsLegal(upper)+","+from.Distances[lower]+","+from.Distances[upper]+","+before.CanMove(lower,upper)+","+before.CanMove(upper,lower)+
                    "; roleStatics="+graph.IsLegal(lower)+","+graph.IsLegal(upper)+","+reachable.Distances[lower]+","+reachable.Distances[upper]+","+graph.CanMove(lower,upper)+","+graph.CanMove(upper,lower));
            }
        }
    }

    private static int ValidateRoutes(MapData map, MapCandidateCollisionGeometry geometry,
        Collider2D playerFoot, EcologySafetyRegions safety, MapEcologyLayout layout,
        MapCoordinateBoundary coordinates, float exitInteractionRadius)
    {
        EcologyNavigationGraph graph = geometry.BuildNavigationGraph(playerFoot, 0f);
        EcologyReturnField reachable = graph.BuildReturnField(new[] { Node(map, map.SpawnCell) });
        foreach (int node in new[] { safety.MerchantApproachNode, safety.RestNode })
            if (!graph.IsLegal(node) || reachable.Distances[node] < 0) throw Failure("CriticalRouteBlocked", "Required player destination=" + node);
        foreach (MapEcologyResident resident in layout.Residents)
            if (!graph.IsLegal(resident.InitialNode) || reachable.Distances[resident.InitialNode] < 0)
                throw Failure("CriticalRouteBlocked", "Resident approach=" + resident.Id);
        // The original floor owner uses body-to-portal distance, not occupancy
        // of ExitCell. Requiring its exact centre would reject valid old exits.
        Vector2 exit = coordinates.CellToWorld(map.ExitCell);
        for (int node = 0; node < graph.NodeCount; node++)
            if (graph.IsLegal(node) && reachable.Distances[node] >= 0 &&
                Vector2.Distance(coordinates.CellToWorld(Cell(map, node)), exit) <= exitInteractionRadius) return node;
        throw Failure("CriticalRouteBlocked", "No reachable whole-foot stop within the original exit interaction radius.");
    }

    private static ulong[] Ranks(MapData map, int seed, string domain)
    {
        var result = new ulong[checked(map.Width * map.Height)];
        for (int n = 0; n < result.Length; n++) result[n] = MapSemanticPlacementPrimitives.StableScore(seed,
            "eco.safety-placement.v1", domain, Cell(map, n) - map.Origin);
        return result;
    }
    private static Vector2Int Cell(MapData map, int node) => map.Origin + new Vector2Int(node % map.Width, node / map.Width);
    private static int Node(MapData map, Vector2Int cell) => checked((cell.y - map.Origin.y) * map.Width + cell.x - map.Origin.x);
    private static MapPlanningException Failure(string code, string detail) => new MapPlanningException(code,
        "Joint ecology planning: " + code + "; " + detail, false);
}
