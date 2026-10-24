using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapEcologyPlacementResult
{
    internal MapEcologyPlacementResult(string logicalId, string role, string typeId,
        GameObject prefab, Vector2Int cell, Vector2Int regionAnchor, int pathSteps)
    {
        LogicalId = logicalId;
        Role = role;
        TypeId = typeId;
        Prefab = prefab;
        Cell = cell;
        RegionAnchor = regionAnchor;
        PathSteps = pathSteps;
    }

    public string LogicalId { get; }
    public string Role { get; }
    public string TypeId { get; }
    public GameObject Prefab { get; }
    public Vector2Int Cell { get; }
    public Vector2Int RegionAnchor { get; }
    public int PathSteps { get; }
}

/// <summary>Finite, deterministic clustered placement within shared reachable terrain regions.</summary>
public sealed class MapEcologyPlacementPolicy
{
    private sealed class Cluster
    {
        public Cluster(MapPopulationRegion region, Vector2Int center)
        {
            Region = region;
            Center = center;
        }
        public MapPopulationRegion Region { get; }
        public Vector2Int Center { get; }
    }

    private readonly int seed;
    private readonly MapData map;
    private readonly MapPopulationRegionQuery regions;
    private readonly string role;
    private readonly MapPopulationTuning tuning;
    private readonly bool weightedFromFirstSlot;
    private readonly List<MapPopulationSpecies> eligible = new List<MapPopulationSpecies>();
    private readonly Dictionary<string, Cluster> clusters =
        new Dictionary<string, Cluster>(StringComparer.Ordinal);

    public MapEcologyPlacementPolicy(int seed, MapData map,
        MapPopulationRegionQuery regions, MapPlacementPlan initialPlan,
        string role, MapPopulationTuning tuning,
        IReadOnlyList<MapPopulationSpecies> species, bool weightedFromFirstSlot = false)
    {
        this.seed = seed;
        this.map = map ?? throw new ArgumentNullException(nameof(map));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
        this.role = role ?? throw new ArgumentNullException(nameof(role));
        this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        this.weightedFromFirstSlot = weightedFromFirstSlot;
        if (initialPlan == null) throw new ArgumentNullException(nameof(initialPlan));
        if (species == null) throw new ArgumentNullException(nameof(species));
        tuning.Validate();

        foreach (MapPopulationSpecies entry in species)
        {
            Cluster cluster = SelectCluster(entry, initialPlan);
            if (cluster == null)
                continue;
            eligible.Add(entry);
            clusters.Add(entry.TypeId, cluster);
        }
    }

    public int EligibleSpeciesCount => eligible.Count;

    public MapPopulationSpecies SelectSpecies(int slot)
    {
        if (eligible.Count == 0)
            return null;
        // Legacy recipes keep their exact deterministic prefix. A versioned roster larger
        // than the population cap must give every weighted species a chance from slot zero.
        if (!weightedFromFirstSlot && slot < eligible.Count)
            return eligible[slot];

        int totalWeight = 0;
        foreach (MapPopulationSpecies entry in eligible)
            totalWeight += entry.Weight;
        uint score = MapSemanticPlacementPrimitives.StableScore(
            seed, role, "weighted-species", slot.ToString("D3"), Vector2Int.zero);
        int draw = (int)(score % (uint)totalWeight);
        foreach (MapPopulationSpecies entry in eligible)
        {
            draw -= entry.Weight;
            if (draw < 0)
                return entry;
        }
        return eligible[eligible.Count - 1];
    }

    public MapEcologyPlacementResult SelectOptional(
        MapPlacementPlan plan, MapPopulationSpecies species, string logicalId)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (species == null || !clusters.TryGetValue(species.TypeId, out Cluster cluster))
            return null;

        Vector2Int selected = default;
        int selectedPath = 0;
        ulong bestScore = ulong.MaxValue;
        bool found = false;
        HashSet<Vector2Int> blockers = CollectNavigationBlockers(plan);
        Dictionary<Vector2Int, int> liveDistances =
            MapSemanticPlacementPrimitives.BuildWalkableDistances(map, map.SpawnCell, blockers);

        foreach (Vector2Int candidate in cluster.Region.Cells)
        {
            int clusterDistance = Chebyshev(candidate, cluster.Center);
            if (clusterDistance > tuning.ClusterRadius ||
                !IsLegal(plan, liveDistances, candidate, out int pathSteps))
                continue;

            if (role == DestructiblePlacementService.DestructibleRole &&
                !PreservesCriticalTargets(plan, blockers, candidate))
                continue;

            uint jitter = MapSemanticPlacementPrimitives.StableScore(
                seed, role, species.TypeId, logicalId, candidate);
            ulong score = ((ulong)(uint)clusterDistance << 32) | jitter;
            if (!found || score < bestScore ||
                (score == bestScore &&
                 MapSemanticPlacementPrimitives.CompareCells(candidate, selected) < 0))
            {
                selected = candidate;
                selectedPath = pathSteps;
                bestScore = score;
                found = true;
            }
        }
        if (!found)
            return null;

        uint variantScore = MapSemanticPlacementPrimitives.StableScore(
            seed, role, species.TypeId, logicalId, selected);
        GameObject prefab = species.Variants[(int)(variantScore % (uint)species.Variants.Count)];
        return new MapEcologyPlacementResult(
            logicalId, role, species.TypeId, prefab, selected,
            cluster.Region.Anchor, selectedPath);
    }

    private Cluster SelectCluster(MapPopulationSpecies species, MapPlacementPlan plan)
    {
        HashSet<Vector2Int> blockers = CollectNavigationBlockers(plan);
        Dictionary<Vector2Int, int> liveDistances =
            MapSemanticPlacementPrimitives.BuildWalkableDistances(map, map.SpawnCell, blockers);
        Cluster best = null;
        ulong bestRank = ulong.MaxValue;
        foreach (MapPopulationRegion region in regions.Regions)
        {
            if (region.Area < 9 || !species.Allows(region.Terrain))
                continue;
            Vector2Int center = default;
            uint centerScore = uint.MaxValue;
            bool foundCenter = false;
            foreach (Vector2Int cell in region.Cells)
            {
                if (!IsLegal(plan, liveDistances, cell, out _))
                    continue;
                uint cellScore = MapSemanticPlacementPrimitives.StableScore(
                    seed, role, species.TypeId, "cluster", cell);
                if (!foundCenter || cellScore < centerScore ||
                    (cellScore == centerScore &&
                     MapSemanticPlacementPrimitives.CompareCells(cell, center) < 0))
                {
                    center = cell;
                    centerScore = cellScore;
                    foundCenter = true;
                }
            }
            if (!foundCenter)
                continue;
            uint score = MapSemanticPlacementPrimitives.StableScore(
                seed, role, species.TypeId, "region", region.Anchor);
            ulong rank = ((ulong)score << 10) / (uint)Mathf.Min(region.Area, 1024);
            if (best == null || rank < bestRank ||
                (rank == bestRank &&
                 MapSemanticPlacementPrimitives.CompareCells(region.Anchor, best.Region.Anchor) < 0))
            {
                best = new Cluster(region, center);
                bestRank = rank;
            }
        }
        return best;
    }

    private bool IsLegal(MapPlacementPlan plan,
        IReadOnlyDictionary<Vector2Int, int> liveDistances,
        Vector2Int cell, out int pathSteps)
    {
        if (!liveDistances.TryGetValue(cell, out pathSteps) ||
            pathSteps < tuning.MinPathSteps || pathSteps > tuning.MaxPathSteps ||
            plan.IsOccupied(cell) || Chebyshev(cell, map.ExitCell) <= tuning.ExitSafetyRadius)
            return false;

        // All current sprites fit inside this one-cell clearance margin.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            Vector2Int neighbor = cell + new Vector2Int(dx, dy);
            if (!map.IsInside(neighbor) || !map.IsWalkable(neighbor) ||
                map.GetElevation(neighbor) != map.GetElevation(cell))
                return false;
            if (plan.IsOccupied(neighbor) &&
                !IsOtherPopulationReservation(plan, neighbor))
                return false;
        }

        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            bool sameRole = reservation.Role == role;
            bool otherPopulation = IsPopulationRole(reservation.Role);
            if (!sameRole && !otherPopulation)
                continue;
            int distance = Mathf.Abs(cell.x - reservation.AnchorCell.x) +
                           Mathf.Abs(cell.y - reservation.AnchorCell.y);
            int minimum = sameRole ? tuning.MinimumSpacingSteps : tuning.CrossRoleSpacingSteps;
            if (distance < minimum)
                return false;
        }
        return true;
    }

    private static bool IsOtherPopulationReservation(MapPlacementPlan plan, Vector2Int cell)
    {
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (!IsPopulationRole(reservation.Role))
                continue;
            foreach (Vector2Int occupied in reservation.OccupiedCells)
                if (occupied == cell)
                    return true;
        }
        return false;
    }

    private bool PreservesCriticalTargets(MapPlacementPlan plan,
        HashSet<Vector2Int> blockers, Vector2Int candidate)
    {
        HashSet<Vector2Int> withCandidate = new HashSet<Vector2Int>(blockers) { candidate };
        Dictionary<Vector2Int, int> distances =
            MapSemanticPlacementPrimitives.BuildWalkableDistances(map, map.SpawnCell, withCandidate);
        if (!distances.ContainsKey(map.ExitCell))
            return false;
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (reservation.Role == MonsterPlacementService.MonsterRole &&
                !distances.ContainsKey(reservation.AnchorCell))
                return false;
        }
        return true;
    }

    private static HashSet<Vector2Int> CollectNavigationBlockers(MapPlacementPlan plan) =>
        MapSemanticPlacementPrimitives.CollectNavigationBlockers(
            plan,
            "CollisionDecoration",
            MerchantPlacementService.MerchantRole,
            PlantPlacementService.PlantRole,
            DestructiblePlacementService.DestructibleRole);

    private static bool IsPopulationRole(string role) =>
        role == MonsterPlacementService.MonsterRole ||
        role == DestructiblePlacementService.DestructibleRole;

    private static int Chebyshev(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
