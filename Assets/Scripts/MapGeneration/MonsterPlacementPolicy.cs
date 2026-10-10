using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum MonsterTerrainMask
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

/// <summary>Explicit gameplay-balance configuration for the current Required Monster role.</summary>
[Serializable]
public sealed class MonsterPlacementProfile
{
    public const int CurrentDefaultMinPathSteps = 10;
    public const int CurrentDefaultMaxPathSteps = 28;
    public const int CurrentDefaultRequiredMinimum = 1;
    public const int CurrentDefaultTargetCount = 1;
    public const int CurrentDefaultMaximum = 1;
    public const MonsterTerrainMask CurrentDefaultAllowedTerrains =
        MonsterTerrainMask.Grass |
        MonsterTerrainMask.Path |
        MonsterTerrainMask.Forest |
        MonsterTerrainMask.Sand;

    [SerializeField, Min(0)] private int minPathSteps = CurrentDefaultMinPathSteps;
    [SerializeField, Min(0)] private int maxPathSteps = CurrentDefaultMaxPathSteps;
    [SerializeField] private MonsterTerrainMask allowedTerrains = CurrentDefaultAllowedTerrains;
    [SerializeField, Min(1)] private int requiredMinimum = CurrentDefaultRequiredMinimum;
    [SerializeField, Min(1)] private int targetCount = CurrentDefaultTargetCount;
    [SerializeField, Min(1)] private int maximum = CurrentDefaultMaximum;

    public MonsterPlacementProfile() { }

    public MonsterPlacementProfile(
        int minPathSteps,
        int maxPathSteps,
        MonsterTerrainMask allowedTerrains,
        int requiredMinimum = CurrentDefaultRequiredMinimum,
        int targetCount = CurrentDefaultTargetCount,
        int maximum = CurrentDefaultMaximum)
    {
        this.minPathSteps = minPathSteps;
        this.maxPathSteps = maxPathSteps;
        this.allowedTerrains = allowedTerrains;
        this.requiredMinimum = requiredMinimum;
        this.targetCount = targetCount;
        this.maximum = maximum;
        Validate();
    }

    public bool Required => true;
    public int MinPathSteps => minPathSteps;
    public int MaxPathSteps => maxPathSteps;
    public MonsterTerrainMask AllowedTerrains => allowedTerrains;
    public int RequiredMinimum => requiredMinimum;
    public int TargetCount => targetCount;
    public int Maximum => maximum;

    public bool AllowsTerrain(MapTerrainType terrainType)
    {
        MonsterTerrainMask terrain = ToMask(terrainType);
        return terrain != MonsterTerrainMask.None &&
               (allowedTerrains & terrain) != 0;
    }

    public void Validate()
    {
        if (minPathSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(minPathSteps));
        if (maxPathSteps < minPathSteps)
            throw new ArgumentException("Monster max path steps must be greater than or equal to min path steps.");
        if (allowedTerrains == MonsterTerrainMask.None)
            throw new ArgumentException("Monster allowed terrain must not be empty.");
        if (requiredMinimum != 1 || targetCount != 1 || maximum != 1)
        {
            throw new ArgumentException(
                "The current Stage 5B Monster profile requires exactly one scene-bound Monster.");
        }
    }

    private static MonsterTerrainMask ToMask(MapTerrainType terrainType)
    {
        switch (terrainType)
        {
            case MapTerrainType.Grass: return MonsterTerrainMask.Grass;
            case MapTerrainType.DeepWater: return MonsterTerrainMask.DeepWater;
            case MapTerrainType.Path: return MonsterTerrainMask.Path;
            case MapTerrainType.ShallowWater: return MonsterTerrainMask.ShallowWater;
            case MapTerrainType.Forest: return MonsterTerrainMask.Forest;
            case MapTerrainType.Mountain: return MonsterTerrainMask.Mountain;
            case MapTerrainType.Sand: return MonsterTerrainMask.Sand;
            default: return MonsterTerrainMask.None;
        }
    }
}

public sealed class MonsterSemanticPlacementRequest
{
    public MonsterSemanticPlacementRequest(
        MapPlacementRequest placement,
        MonsterPlacementProfile profile)
    {
        Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Profile.Validate();
    }

    public MapPlacementRequest Placement { get; }
    public MonsterPlacementProfile Profile { get; }
}

public sealed class MonsterPlacementResult
{
    internal MonsterPlacementResult(
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

/// <summary>Selects one deterministic legal Monster cell without owning AI or materialization.</summary>
public sealed class MonsterPlacementPolicy
{
    public MonsterPlacementResult SelectRequired(
        int mapSeed,
        MapData map,
        MapPlacementPlan plan,
        MonsterSemanticPlacementRequest request)
    {
        ValidateArguments(map, plan, request);
        Dictionary<Vector2Int, int> spawnDistances = BuildSpawnDistances(map, plan);
        MonsterPlacementResult selected = null;
        uint selectedScore = uint.MaxValue;
        Dictionary<string, int> rejections = new Dictionary<string, int>(StringComparer.Ordinal);

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
                        out MonsterPlacementResult result,
                        out string failureCode))
                {
                    rejections[failureCode] = rejections.TryGetValue(failureCode, out int count)
                        ? count + 1
                        : 1;
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

        if (selected != null)
            return selected;

        throw new MapPlanningException(
            "RequiredMonsterPlacementUnavailable",
            $"Required Monster placement failed. Role={request.Placement.Role}; " +
            $"LogicalObjectId={request.Placement.LogicalObjectId}; GenerationId={plan.GenerationId:D}; " +
            $"RequiredMinimum={request.Profile.RequiredMinimum}; " +
            $"FailedConstraints={MapSemanticPlacementPrimitives.FormatRejections(rejections)}.",
            false);
    }

    public bool TryEvaluateCandidate(
        MapData map,
        MapPlacementPlan plan,
        MonsterSemanticPlacementRequest request,
        Vector2Int candidate,
        out MonsterPlacementResult result,
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
        MonsterSemanticPlacementRequest request,
        Dictionary<Vector2Int, int> spawnDistances,
        Vector2Int candidate,
        out MonsterPlacementResult result,
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

        result = new MonsterPlacementResult(
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
        MonsterSemanticPlacementRequest request)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.Placement.Required)
            throw new ArgumentException("The current Monster semantic request must be Required.", nameof(request));
        request.Profile.Validate();
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }
}
