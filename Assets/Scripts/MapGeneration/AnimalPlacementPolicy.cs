using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum AnimalTerrainMask
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

/// <summary>Explicit, tunable gameplay profile for the current ambient Animal population.</summary>
[Serializable]
public sealed class AnimalPlacementProfile
{
    public const int CurrentDefaultMinPathSteps = 0;
    public const int CurrentDefaultMaxPathSteps = int.MaxValue;
    public const int CurrentDefaultMinimumSpacingSteps = 1;
    public const int CurrentDefaultRequiredMinimum = 0;
    public const int CurrentDefaultTargetCount = 4;
    public const int CurrentDefaultMaximum = 4;
    public const AnimalTerrainMask CurrentDefaultAllowedTerrains =
        AnimalTerrainMask.Grass |
        AnimalTerrainMask.Path |
        AnimalTerrainMask.Forest |
        AnimalTerrainMask.Sand;

    [SerializeField, Min(0)] private int minPathSteps = CurrentDefaultMinPathSteps;
    [SerializeField, Min(0)] private int maxPathSteps = CurrentDefaultMaxPathSteps;
    [SerializeField, Min(1)] private int minimumSpacingSteps = CurrentDefaultMinimumSpacingSteps;
    [SerializeField] private AnimalTerrainMask allowedTerrains = CurrentDefaultAllowedTerrains;
    [SerializeField, Min(0)] private int requiredMinimum = CurrentDefaultRequiredMinimum;
    [SerializeField, Min(1)] private int targetCount = CurrentDefaultTargetCount;
    [SerializeField, Min(1)] private int maximum = CurrentDefaultMaximum;

    public AnimalPlacementProfile() { }

    public AnimalPlacementProfile(
        int minPathSteps,
        int maxPathSteps,
        int minimumSpacingSteps,
        AnimalTerrainMask allowedTerrains,
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
    public AnimalTerrainMask AllowedTerrains => allowedTerrains;
    public int RequiredMinimum => requiredMinimum;
    public int TargetCount => targetCount;
    public int Maximum => maximum;

    public bool AllowsTerrain(MapTerrainType terrainType)
    {
        AnimalTerrainMask terrain = ToMask(terrainType);
        return terrain != AnimalTerrainMask.None && (allowedTerrains & terrain) != 0;
    }

    public void Validate()
    {
        if (minPathSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(minPathSteps));
        if (maxPathSteps < minPathSteps)
            throw new ArgumentException("Animal max path steps must be greater than or equal to min path steps.");
        if (minimumSpacingSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumSpacingSteps));
        if (allowedTerrains == AnimalTerrainMask.None)
            throw new ArgumentException("Animal allowed terrain must not be empty.");
        if (requiredMinimum != 0 || targetCount != 4 || maximum != 4)
        {
            throw new ArgumentException(
                "The current Stage 5C Animal profile is Optional and targets exactly four scene-bound Animals.");
        }
    }

    private static AnimalTerrainMask ToMask(MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.Grass: return AnimalTerrainMask.Grass;
            case MapTerrainType.DeepWater: return AnimalTerrainMask.DeepWater;
            case MapTerrainType.Path: return AnimalTerrainMask.Path;
            case MapTerrainType.ShallowWater: return AnimalTerrainMask.ShallowWater;
            case MapTerrainType.Forest: return AnimalTerrainMask.Forest;
            case MapTerrainType.Mountain: return AnimalTerrainMask.Mountain;
            case MapTerrainType.Sand: return AnimalTerrainMask.Sand;
            default: return AnimalTerrainMask.None;
        }
    }
}

public sealed class AnimalSemanticPlacementRequest
{
    public AnimalSemanticPlacementRequest(
        MapPlacementRequest placement,
        AnimalPlacementProfile profile)
    {
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Profile.Validate();
    }

    public MapPlacementRequest Placement { get; }
    public AnimalPlacementProfile Profile { get; }
}

public sealed class AnimalPlacementResult
{
    internal AnimalPlacementResult(
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

/// <summary>Selects one deterministic legal Animal cell without owning population lifecycle.</summary>
public sealed class AnimalPlacementPolicy
{
    public AnimalPlacementResult SelectOptional(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        AnimalSemanticPlacementRequest request)
    {
        ValidateArguments(map, plan, request);
        Dictionary<Vector2Int, int> spawnDistances = BuildSpawnDistances(map, plan);
        AnimalPlacementResult selected = null;
        uint selectedScore = uint.MaxValue;

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
                        out AnimalPlacementResult result,
                        out _))
                {
                    continue;
                }

                uint score = MapSemanticPlacementPrimitives.StableScore(
                    mapSeed,
                    request.Placement.Role,
                    request.Placement.LogicalObjectId,
                    candidate);
                if (selected == null || score < selectedScore ||
                    (score == selectedScore &&
                     MapSemanticPlacementPrimitives.CompareCells(candidate, selected.PlacementCell) < 0))
                {
                    selected = result;
                    selectedScore = score;
                }
            }
        }

        return selected;
    }

    public bool TryEvaluateCandidate(
        MapData map,
        MapPlacementPlan plan,
        AnimalSemanticPlacementRequest request,
        Vector2Int candidate,
        out AnimalPlacementResult result,
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
            out failureCode);
    }

    private static bool TryEvaluateCandidate(
        MapData map,
        MapPlacementPlan plan,
        AnimalSemanticPlacementRequest request,
        Dictionary<Vector2Int, int> spawnDistances,
        Vector2Int candidate,
        out AnimalPlacementResult result,
        out string failureCode)
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
                    AnimalPlacementService.AnimalRole,
                    StringComparison.Ordinal))
            {
                continue;
            }

            int separation = Mathf.Abs(candidate.x - reservation.AnchorCell.x) +
                             Mathf.Abs(candidate.y - reservation.AnchorCell.y);
            if (separation < request.Profile.MinimumSpacingSteps)
                return Fail("AnimalSpacing", out failureCode);
        }

        result = new AnimalPlacementResult(
            candidate,
            pathSteps,
            footprint.AsReadOnly());
        return true;
    }

    private static Dictionary<Vector2Int, int> BuildSpawnDistances(
        MapData map,
        MapPlacementPlan plan)
    {
        return MapSemanticPlacementPrimitives.BuildWalkableDistances(
            map,
            map.SpawnCell,
            MapSemanticPlacementPrimitives.CollectNavigationBlockers(
                plan,
                "CollisionDecoration",
                MerchantPlacementService.MerchantRole));
    }

    private static void ValidateArguments(
        MapData map,
        MapPlacementPlan plan,
        AnimalSemanticPlacementRequest request)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Placement.Required)
            throw new ArgumentException("The current Animal semantic request must be Optional.", nameof(request));
        request.Profile.Validate();
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }
}
