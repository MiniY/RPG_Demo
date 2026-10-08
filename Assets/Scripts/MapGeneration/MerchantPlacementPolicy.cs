using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum MerchantTerrainMask
{
    None = 0,
    Grass = 1 << 0,
    DeepWater = 1 << 1,
    Path = 1 << 2,
    ShallowWater = 1 << 3,
    Forest = 1 << 4,
    Mountain = 1 << 5,
    Sand = 1 << 6
}

/// <summary>Explicit gameplay-balance configuration for the Required Merchant role.</summary>
[Serializable]
public sealed class MerchantPlacementProfile
{
    public const int CurrentDefaultMinPathSteps = 8;
    public const int CurrentDefaultMaxPathSteps = 24;
    public const MerchantTerrainMask CurrentDefaultAllowedTerrains =
        MerchantTerrainMask.Grass | MerchantTerrainMask.Path;

    [SerializeField, Min(0)] private int minPathSteps = CurrentDefaultMinPathSteps;
    [SerializeField, Min(0)] private int maxPathSteps = CurrentDefaultMaxPathSteps;
    [SerializeField] private MerchantTerrainMask allowedTerrains = CurrentDefaultAllowedTerrains;

    public MerchantPlacementProfile() { }

    public MerchantPlacementProfile(
        int minPathSteps,
        int maxPathSteps,
        MerchantTerrainMask allowedTerrains)
    {
        this.minPathSteps = minPathSteps;
        this.maxPathSteps = maxPathSteps;
        this.allowedTerrains = allowedTerrains;
        Validate();
    }

    public int MinPathSteps => minPathSteps;
    public int MaxPathSteps => maxPathSteps;
    public MerchantTerrainMask AllowedTerrains => allowedTerrains;

    public bool AllowsTerrain(MapTerrainType terrainType)
    {
        MerchantTerrainMask terrain = ToMask(terrainType);
        return terrain != MerchantTerrainMask.None &&
               (allowedTerrains & terrain) != 0;
    }

    public void Validate()
    {
        if (minPathSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(minPathSteps));
        if (maxPathSteps < minPathSteps)
            throw new ArgumentException("Merchant max path steps must be greater than or equal to min path steps.");
        if (allowedTerrains == MerchantTerrainMask.None)
            throw new ArgumentException("Merchant allowed terrain must not be empty.");
    }

    private static MerchantTerrainMask ToMask(MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.Grass: return MerchantTerrainMask.Grass;
            case MapTerrainType.DeepWater: return MerchantTerrainMask.DeepWater;
            case MapTerrainType.Path: return MerchantTerrainMask.Path;
            case MapTerrainType.ShallowWater: return MerchantTerrainMask.ShallowWater;
            case MapTerrainType.Forest: return MerchantTerrainMask.Forest;
            case MapTerrainType.Mountain: return MerchantTerrainMask.Mountain;
            case MapTerrainType.Sand: return MerchantTerrainMask.Sand;
            default: return MerchantTerrainMask.None;
        }
    }
}

/// <summary>Role-specific semantic request layered on the shared placement request.</summary>
public sealed class MerchantSemanticPlacementRequest
{
    public MerchantSemanticPlacementRequest(
        MapPlacementRequest placement,
        MerchantPlacementProfile profile)
    {
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Profile.Validate();
    }

    public MapPlacementRequest Placement { get; }
    public MerchantPlacementProfile Profile { get; }
}

public sealed class MerchantPlacementResult
{
    internal MerchantPlacementResult(
        Vector2Int placementCell,
        Vector2Int interactionApproachCell,
        int shortestPathSteps,
        IReadOnlyList<Vector2Int> footprintCells)
    {
        PlacementCell = placementCell;
        InteractionApproachCell = interactionApproachCell;
        ShortestPathSteps = shortestPathSteps;
        FootprintCells = footprintCells;
    }

    public Vector2Int PlacementCell { get; }
    public Vector2Int InteractionApproachCell { get; }
    public int ShortestPathSteps { get; }
    public IReadOnlyList<Vector2Int> FootprintCells { get; }
}

/// <summary>Selects a deterministic legal Merchant cell without owning orchestration or materialization.</summary>
public sealed class MerchantPlacementPolicy
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.left,
        Vector2Int.down
    };

    public MerchantPlacementResult SelectRequired(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        MerchantSemanticPlacementRequest request)
    {
        ValidateArguments(map, plan, request);
        Dictionary<Vector2Int, int> spawnDistances = BuildDistances(
            map,
            map.SpawnCell,
            CollectNavigationBlockers(plan));
        MerchantPlacementResult selected = null;
        uint selectedScore = uint.MaxValue;
        Dictionary<string, int> rejections = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (!TryEvaluateCandidate(
                        mapSeed,
                        map,
                        plan,
                        request,
                        spawnDistances,
                        candidate,
                        out MerchantPlacementResult result,
                        out string failureCode))
                {
                    rejections[failureCode] = rejections.TryGetValue(failureCode, out int count)
                        ? count + 1
                        : 1;
                    continue;
                }

                uint score = StableScore(
                    mapSeed,
                    request.Placement.Role,
                    request.Placement.LogicalObjectId,
                    candidate);
                if (selected == null || score < selectedScore ||
                    (score == selectedScore && CompareCells(candidate, selected.PlacementCell) < 0))
                {
                    selected = result;
                    selectedScore = score;
                }
            }
        }

        if (selected != null)
            return selected;

        string diagnostic = FormatRejections(rejections);
        throw new MapPlanningException(
            "RequiredMerchantPlacementUnavailable",
            $"Required Merchant placement failed. Role={request.Placement.Role}; " +
            $"LogicalObjectId={request.Placement.LogicalObjectId}; GenerationId={plan.GenerationId:D}; " +
            $"FailedConstraints={diagnostic}.",
            false);
    }

    public bool TryEvaluateCandidate(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        MerchantSemanticPlacementRequest request,
        Vector2Int candidate,
        out MerchantPlacementResult result,
        out string failureCode)
    {
        ValidateArguments(map, plan, request);
        Dictionary<Vector2Int, int> spawnDistances = BuildDistances(
            map,
            map.SpawnCell,
            CollectNavigationBlockers(plan));
        return TryEvaluateCandidate(
            mapSeed,
            map,
            plan,
            request,
            spawnDistances,
            candidate,
            out result,
            out failureCode);
    }

    private static bool TryEvaluateCandidate(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        MerchantSemanticPlacementRequest request,
        Dictionary<Vector2Int, int> spawnDistances,
        Vector2Int candidate,
        out MerchantPlacementResult result,
        out string failureCode)
    {
        result = null;
        failureCode = string.Empty;
        List<Vector2Int> footprint = BuildFootprint(candidate, request.Placement.FootprintOffsets);

        foreach (Vector2Int cell in footprint)
        {
            if (!map.IsInside(cell))
                return Fail("Bounds", out failureCode);
            if (!request.Profile.AllowsTerrain(map.GetCell(cell).terrainType))
                return Fail("AllowedTerrain", out failureCode);
            if (!map.IsWalkable(cell))
                return Fail("Walkability", out failureCode);
            if (plan.IsOccupied(cell))
                return Fail("StaticOccupancy", out failureCode);
            if (!spawnDistances.ContainsKey(cell))
                return Fail("SpawnReachability", out failureCode);
        }

        if (!spawnDistances.TryGetValue(candidate, out int pathSteps))
            return Fail("SpawnReachability", out failureCode);
        if (pathSteps < request.Profile.MinPathSteps ||
            pathSteps > request.Profile.MaxPathSteps)
            return Fail("SpawnPathDistance", out failureCode);

        HashSet<Vector2Int> navigationBlockers = CollectNavigationBlockers(plan);
        foreach (Vector2Int cell in footprint)
            navigationBlockers.Add(cell);
        Dictionary<Vector2Int, int> distancesWithMerchant = BuildDistances(
            map,
            map.SpawnCell,
            navigationBlockers);
        if (!distancesWithMerchant.ContainsKey(map.ExitCell))
            return Fail("CriticalPath", out failureCode);

        HashSet<Vector2Int> footprintSet = new HashSet<Vector2Int>(footprint);
        bool hasApproach = false;
        Vector2Int selectedApproach = default;
        uint selectedApproachScore = uint.MaxValue;
        foreach (Vector2Int footprintCell in footprint)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int approach = footprintCell + direction;
                if (footprintSet.Contains(approach) ||
                    !map.IsInside(approach) ||
                    !map.IsWalkable(approach) ||
                    plan.IsOccupied(approach) ||
                    !distancesWithMerchant.ContainsKey(approach))
                {
                    continue;
                }

                uint score = StableScore(
                    mapSeed,
                    request.Placement.Role,
                    request.Placement.LogicalObjectId + ".interaction",
                    approach);
                if (!hasApproach || score < selectedApproachScore ||
                    (score == selectedApproachScore && CompareCells(approach, selectedApproach) < 0))
                {
                    hasApproach = true;
                    selectedApproach = approach;
                    selectedApproachScore = score;
                }
            }
        }

        if (!hasApproach)
            return Fail("InteractionApproach", out failureCode);

        result = new MerchantPlacementResult(
            candidate,
            selectedApproach,
            pathSteps,
            footprint.AsReadOnly());
        return true;
    }

    private static void ValidateArguments(
        MapData map,
        MapPlacementPlan plan,
        MerchantSemanticPlacementRequest request)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.Placement.Required)
            throw new ArgumentException("The current Merchant semantic request must be Required.", nameof(request));
        request.Profile.Validate();
    }

    private static List<Vector2Int> BuildFootprint(
        Vector2Int anchor,
        IReadOnlyList<Vector2Int> offsets)
    {
        List<Vector2Int> cells = new List<Vector2Int>(offsets.Count);
        foreach (Vector2Int offset in offsets)
            cells.Add(anchor + offset);
        return cells;
    }

    private static HashSet<Vector2Int> CollectNavigationBlockers(MapPlacementPlan plan)
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (!string.Equals(reservation.Role, "CollisionDecoration", StringComparison.Ordinal))
                continue;
            foreach (Vector2Int cell in reservation.OccupiedCells)
                blocked.Add(cell);
        }
        return blocked;
    }

    private static Dictionary<Vector2Int, int> BuildDistances(
        MapData map,
        Vector2Int start,
        HashSet<Vector2Int> blocked)
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        if (!map.IsInside(start) || !map.IsWalkable(start) || blocked.Contains(start))
            return distances;

        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        distances.Add(start, 0);
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            int nextDistance = distances[current] + 1;
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (!map.IsInside(next) || !map.IsWalkable(next) ||
                    blocked.Contains(next) || distances.ContainsKey(next))
                {
                    continue;
                }
                distances.Add(next, nextDistance);
                pending.Enqueue(next);
            }
        }
        return distances;
    }

    private static uint StableScore(
        int mapSeed,
        string role,
        string logicalObjectId,
        Vector2Int cell)
    {
        unchecked
        {
            uint hash = 2166136261;
            Mix(ref hash, mapSeed);
            Mix(ref hash, role);
            Mix(ref hash, logicalObjectId);
            Mix(ref hash, cell.x);
            Mix(ref hash, cell.y);
            return hash;
        }
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619;
        }
    }

    private static void Mix(ref uint hash, string value)
    {
        foreach (char character in value)
            Mix(ref hash, character);
    }

    private static int CompareCells(Vector2Int left, Vector2Int right)
    {
        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }

    private static string FormatRejections(Dictionary<string, int> rejections)
    {
        if (rejections.Count == 0)
            return "NoCellsEvaluated";
        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> rejection in rejections)
            parts.Add($"{rejection.Key}:{rejection.Value}");
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }
}
