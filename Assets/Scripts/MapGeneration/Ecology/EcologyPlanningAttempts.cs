using System;
using System.Collections.Generic;

public sealed class EcologyPlanningAttemptResult
{
    internal EcologyPlanningAttemptResult(MapPlacementPlan plan, int accepted, int used, List<string> failures)
    { Plan = plan; AcceptedAttempt = accepted; AttemptsUsed = used; Failures = failures.AsReadOnly(); }
    public MapPlacementPlan Plan { get; }
    public int AcceptedAttempt { get; }
    public int AttemptsUsed { get; }
    public IReadOnlyList<string> Failures { get; }
    public bool Succeeded => Plan != null;
    public MapCandidateCollisionGeometry StaticGeometry { get; private set; }
    public MapCandidateCollisionGeometry TerrainGeometry { get; private set; }
    public MapCandidateCollisionGeometry InitialPlacementGeometry { get; private set; }
    public int ExitApproachNode { get; private set; } = -1;
    internal EcologyPlanningAttemptResult WithGeometry(MapCandidateCollisionGeometry statics, MapCandidateCollisionGeometry initial, int exitApproach = -1,
        MapCandidateCollisionGeometry terrain = null)
    {
        var result = new EcologyPlanningAttemptResult(Plan, AcceptedAttempt, AttemptsUsed, new List<string>(Failures));
        result.StaticGeometry = statics; result.TerrainGeometry = terrain;
        result.InitialPlacementGeometry = initial; result.ExitApproachNode = exitApproach; return result;
    }
}

/// <summary>Owns the finite ecology search on one unchanged pending terrain, including exact restore.</summary>
public static class EcologyPlanningAttempts
{
    public const int MaximumAttempts = 8;
    private static readonly HashSet<string> CandidateFailures = new HashSet<string>(StringComparer.Ordinal)
    {
        "NoConnectedTerritory", "InsufficientSeparatedSeeds", "InsufficientConstructiveCapacity",
        "BossPlacementConflict", "JointPopulationCapacityUnavailable", "InsufficientGroupBudget",
        "SpawnFootprintBlocked", "NoQualifiedMerchant", "InsufficientSafeSpace", "ProtectedConnection",
        "CriticalRouteBlocked", "JointSolidOverlap", "PlantRequiredMinimumUnavailable",
        "EcologyRequiredMinimumUnavailable", "AnimalRequiredMinimumUnavailable"
    };
    public static EcologyPlanningAttemptResult Run(MapData map, MapCandidateCollisionGeometry fixedGeometry,
        Func<int, MapPlacementPlan> buildAndValidate, int? recordedAttempt = null)
    {
        if (map == null || fixedGeometry == null || buildAndValidate == null ||
            !fixedGeometry.MatchesMapData(map) || recordedAttempt.HasValue &&
            (recordedAttempt.Value < 0 || recordedAttempt.Value >= MaximumAttempts))
            throw Failure("InvalidEcologyAttemptInput");
        int generationSeed = map.GenerationSeed;
        int first = recordedAttempt ?? 0, end = recordedAttempt.HasValue ? first + 1 : MaximumAttempts;
        var failures = new List<string>();
        for (int attempt = first; attempt < end; attempt++)
        {
            MapPlacementPlan candidate = null;
            try { candidate = buildAndValidate(attempt); }
            catch (MapPlanningException exception)
            {
                // Only explicit candidate-quality failures spend another ecology
                // attempt. Configuration, identity and unexpected faults escape.
                if (!CandidateFailures.Contains(exception.Code)) throw;
                failures.Add("e=" + attempt + ":" + exception.Code + ": " + exception.Message);
            }
            finally
            {
                if (generationSeed != map.GenerationSeed || !fixedGeometry.MatchesMapData(map))
                    throw Failure("EcologyTerrainInputMutated");
            }
            if (candidate == null)
            {
                if (failures.Count != attempt - first + 1) throw Failure("MissingEcologyCandidate");
                continue;
            }
            MapEcologyLayout layout = candidate.Ecology;
            if (candidate.GenerationId != fixedGeometry.GenerationId || layout == null ||
                layout.GenerationId != fixedGeometry.GenerationId || layout.AcceptedAttempt != attempt ||
                layout.Width != map.Width || layout.Height != map.Height || layout.Origin != map.Origin)
                throw Failure("UnvalidatedOrStaleEcologyCandidate");
            return new EcologyPlanningAttemptResult(candidate, attempt, attempt - first + 1, failures);
        }
        return new EcologyPlanningAttemptResult(null, -1, end - first, failures);
    }
    private static MapPlanningException Failure(string code) => new MapPlanningException(code,
        "Ecology attempt owner rejected input/result: " + code, false);
}
