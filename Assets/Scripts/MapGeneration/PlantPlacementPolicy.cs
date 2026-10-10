using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum PlantTerrainMask
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

/// <summary>Explicit, tunable gameplay profile for the current scene-bound Plant population.</summary>
[Serializable]
public sealed class PlantPlacementProfile
{
    public const int CurrentDefaultMinPathSteps = 0;
    public const int CurrentDefaultMaxPathSteps = int.MaxValue;
    public const int CurrentDefaultMinimumSpacingSteps = 1;
    public const int CurrentDefaultRequiredMinimum = 0;
    public const int CurrentDefaultTargetCount = 17;
    public const int CurrentDefaultMaximum = 17;
    public const PlantTerrainMask CurrentDefaultAllowedTerrains =
        PlantTerrainMask.Grass |
        PlantTerrainMask.Path |
        PlantTerrainMask.Forest |
        PlantTerrainMask.Sand;

    [SerializeField, Min(0)] private int minPathSteps = CurrentDefaultMinPathSteps;
    [SerializeField, Min(0)] private int maxPathSteps = CurrentDefaultMaxPathSteps;
    [SerializeField, Min(1)] private int minimumSpacingSteps = CurrentDefaultMinimumSpacingSteps;
    [SerializeField] private PlantTerrainMask allowedTerrains = CurrentDefaultAllowedTerrains;
    [SerializeField, Min(0)] private int requiredMinimum = CurrentDefaultRequiredMinimum;
    [SerializeField, Min(1)] private int targetCount = CurrentDefaultTargetCount;
    [SerializeField, Min(1)] private int maximum = CurrentDefaultMaximum;

    public PlantPlacementProfile() { }

    public PlantPlacementProfile(
        int minPathSteps,
        int maxPathSteps,
        int minimumSpacingSteps,
        PlantTerrainMask allowedTerrains,
        int requiredMinimum = CurrentDefaultRequiredMinimum,
        int targetCount = CurrentDefaultTargetCount,
        int maximum = CurrentDefaultMaximum)
    {
        this.minPathSteps = minPathSteps;
        this.maxPathSteps = maxPathSteps;
        this.minimumSpacingSteps = minimumSpacingSteps;
        this.allowedTerrains = allowedTerrains;
        this.requiredMinimum = requiredMinimum;
        this.targetCount = targetCount;
        this.maximum = maximum;
        Validate();
    }

    public bool Required => requiredMinimum > 0;
    public int MinPathSteps => minPathSteps;
    public int MaxPathSteps => maxPathSteps;
    public int MinimumSpacingSteps => minimumSpacingSteps;
    public PlantTerrainMask AllowedTerrains => allowedTerrains;
    public int RequiredMinimum => requiredMinimum;
    public int TargetCount => targetCount;
    public int Maximum => maximum;

    public bool AllowsTerrain(MapTerrainType terrainType)
    {
        PlantTerrainMask terrain = ToMask(terrainType);
        return terrain != PlantTerrainMask.None && (allowedTerrains & terrain) != 0;
    }

    public void Validate()
    {
        if (minPathSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(minPathSteps));
        if (maxPathSteps < minPathSteps)
            throw new ArgumentException("Plant max path steps must be greater than or equal to min path steps.");
        if (minimumSpacingSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumSpacingSteps));
        if (allowedTerrains == PlantTerrainMask.None)
            throw new ArgumentException("Plant allowed terrain must not be empty.");
        if (requiredMinimum != 0 || targetCount != 17 || maximum != 17)
        {
            throw new ArgumentException(
                "The current Stage 5D Plant profile is Optional and targets exactly 17 scene-bound Plants.");
        }
    }

    private static PlantTerrainMask ToMask(MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.Grass: return PlantTerrainMask.Grass;
            case MapTerrainType.DeepWater: return PlantTerrainMask.DeepWater;
            case MapTerrainType.Path: return PlantTerrainMask.Path;
            case MapTerrainType.ShallowWater: return PlantTerrainMask.ShallowWater;
            case MapTerrainType.Forest: return PlantTerrainMask.Forest;
            case MapTerrainType.Mountain: return PlantTerrainMask.Mountain;
            case MapTerrainType.Sand: return PlantTerrainMask.Sand;
            default: return PlantTerrainMask.None;
        }
    }
}

public sealed class PlantSemanticPlacementRequest
{
    public PlantSemanticPlacementRequest(
        MapPlacementRequest placement,
        PlantPlacementProfile profile)
    {
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Profile.Validate();
    }

    public MapPlacementRequest Placement { get; }
    public PlantPlacementProfile Profile { get; }
}

public sealed class PlantPlacementResult
{
    internal PlantPlacementResult(
        Vector2Int placementCell,
        int shortestPathSteps,
        IReadOnlyList<Vector2Int> footprintCells)
    {
        PlacementCell = placementCell;
        ShortestPathSteps = shortestPathSteps;
        FootprintCells = footprintCells;
    }

    public Vector2Int PlacementCell { get; }
    public int ShortestPathSteps { get; }
    public IReadOnlyList<Vector2Int> FootprintCells { get; }
}

/// <summary>Selects one deterministic legal Plant cell without owning population lifecycle.</summary>
public sealed class PlantPlacementPolicy
{
    private sealed class ScoredCandidate
    {
        public ScoredCandidate(PlantPlacementResult result, uint score)
        {
            Result = result;
            Score = score;
        }

        public PlantPlacementResult Result { get; }
        public uint Score { get; }
    }

    public PlantPlacementResult SelectOptional(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        PlantSemanticPlacementRequest request)
    {
        ValidateArguments(map, plan, request);
        Dictionary<Vector2Int, int> spawnDistances = BuildSpawnDistances(map, plan);
        List<ScoredCandidate> candidates = new List<ScoredCandidate>();

        for (int x = map.Origin.x; x < map.Origin.x + map.Width; x++)
        {
            for (int y = map.Origin.y; y < map.Origin.y + map.Height; y++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (!TryEvaluateCandidate(
                        map,
                        plan,
                        request,
                        spawnDistances,
                        candidate,
                        out PlantPlacementResult result,
                        out _,
                        false))
                {
                    continue;
                }

                uint score = MapSemanticPlacementPrimitives.StableScore(
                    mapSeed,
                    request.Placement.Role,
                    request.Placement.LogicalObjectId,
                    candidate);
                candidates.Add(new ScoredCandidate(result, score));
            }
        }

        candidates.Sort((left, right) =>
        {
            int scoreComparison = left.Score.CompareTo(right.Score);
            return scoreComparison != 0
                ? scoreComparison
                : MapSemanticPlacementPrimitives.CompareCells(
                    left.Result.PlacementCell,
                    right.Result.PlacementCell);
        });
        foreach (ScoredCandidate candidate in candidates)
        {
            if (PreservesCriticalPath(map, plan, candidate.Result.FootprintCells))
                return candidate.Result;
        }

        return null;
    }

    public bool TryEvaluateCandidate(
        MapData map,
        MapPlacementPlan plan,
        PlantSemanticPlacementRequest request,
        Vector2Int candidate,
        out PlantPlacementResult result,
        out string failureCode)
    {
        ValidateArguments(map, plan, request);
        return TryEvaluateCandidate(
            map,
            plan,
            request,
            BuildSpawnDistances(map, plan),
            candidate,
            out result,
            out failureCode,
            true);
    }

    private static bool TryEvaluateCandidate(
        MapData map,
        MapPlacementPlan plan,
        PlantSemanticPlacementRequest request,
        Dictionary<Vector2Int, int> spawnDistances,
        Vector2Int candidate,
        out PlantPlacementResult result,
        out string failureCode,
        bool validateCriticalPath)
    {
        result = null;
        failureCode = string.Empty;
        List<Vector2Int> footprint = MapSemanticPlacementPrimitives.BuildFootprint(
            candidate,
            request.Placement.FootprintOffsets);

        foreach (Vector2Int cell in footprint)
        {
            if (!map.IsInside(cell))
                return Fail("Bounds", out failureCode);
            if (!request.Profile.AllowsTerrain(map.GetCell(cell).terrainType))
                return Fail("AllowedTerrain", out failureCode);
            if (!map.IsWalkable(cell))
                return Fail("Walkability", out failureCode);
            if (plan.IsOccupied(cell))
                return Fail("Occupancy", out failureCode);
            if (!spawnDistances.ContainsKey(cell))
                return Fail("SpawnReachability", out failureCode);
        }

        if (!spawnDistances.TryGetValue(candidate, out int pathSteps))
            return Fail("SpawnReachability", out failureCode);
        if (pathSteps < request.Profile.MinPathSteps ||
            pathSteps > request.Profile.MaxPathSteps)
        {
            return Fail("SpawnPathDistance", out failureCode);
        }

        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (!string.Equals(
                    reservation.Role,
                    PlantPlacementService.PlantRole,
                    StringComparison.Ordinal))
            {
                continue;
            }

            int separation = Mathf.Abs(candidate.x - reservation.AnchorCell.x) +
                             Mathf.Abs(candidate.y - reservation.AnchorCell.y);
            if (separation < request.Profile.MinimumSpacingSteps)
                return Fail("PlantSpacing", out failureCode);
        }

        if (validateCriticalPath && !PreservesCriticalPath(map, plan, footprint))
            return Fail("CriticalPath", out failureCode);

        result = new PlantPlacementResult(candidate, pathSteps, footprint.AsReadOnly());
        return true;
    }

    private static bool PreservesCriticalPath(
        MapData map,
        MapPlacementPlan plan,
        IReadOnlyList<Vector2Int> footprint)
    {
        HashSet<Vector2Int> navigationBlockers = CollectNavigationBlockers(plan);
        foreach (Vector2Int cell in footprint)
            navigationBlockers.Add(cell);
        Dictionary<Vector2Int, int> distancesWithPlant =
            MapSemanticPlacementPrimitives.BuildWalkableDistances(
                map,
                map.SpawnCell,
                navigationBlockers);
        return distancesWithPlant.ContainsKey(map.ExitCell);
    }

    private static Dictionary<Vector2Int, int> BuildSpawnDistances(
        MapData map,
        MapPlacementPlan plan)
    {
        return MapSemanticPlacementPrimitives.BuildWalkableDistances(
            map,
            map.SpawnCell,
            CollectNavigationBlockers(plan));
    }

    private static HashSet<Vector2Int> CollectNavigationBlockers(MapPlacementPlan plan)
    {
        return MapSemanticPlacementPrimitives.CollectNavigationBlockers(
            plan,
            "CollisionDecoration",
            MerchantPlacementService.MerchantRole,
            PlantPlacementService.PlantRole);
    }

    private static void ValidateArguments(
        MapData map,
        MapPlacementPlan plan,
        PlantSemanticPlacementRequest request)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Placement.Required)
            throw new ArgumentException("The current Plant semantic request must be Optional.", nameof(request));
        request.Profile.Validate();
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }
}
