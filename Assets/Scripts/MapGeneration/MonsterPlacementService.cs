using System;
using UnityEngine;

/// <summary>Narrow placement seam implemented by the existing main Monster AI component.</summary>
public interface IMonsterPlacementTarget
{
    Transform PlacementTransform { get; }
    GameObject ReinitializationTarget { get; }
}

/// <summary>Owns the current Required Monster planning reservation and post-commit relocation.</summary>
[DisallowMultipleComponent]
public sealed class MonsterPlacementService : MonoBehaviour
{
    public const string MainMonsterLogicalObjectId = "monster.torch-blue.main";
    public const string MonsterRole = "Monster";

    [SerializeField] private MonoBehaviour monsterTarget;
    [SerializeField] private MonsterPlacementProfile profile = new MonsterPlacementProfile();

    private readonly MonsterPlacementPolicy policy = new MonsterPlacementPolicy();
    private Guid? plannedGenerationId;

    public MonsterPlacementProfile Profile => profile;
    public MonoBehaviour MonsterTarget => monsterTarget;
    public GameObject ReinitializationTarget =>
        monsterTarget is IMonsterPlacementTarget target ? target.ReinitializationTarget : null;
    public Guid? ReadyGenerationId { get; private set; }
    public int MaterializationCount { get; private set; }
    public Vector2Int PlacementCell { get; private set; }
    public int ShortestPathSteps { get; private set; }

    public MonsterPlacementResult Plan(
        MapGenerationAttempt attempt,
        MapData map,
        MapPlacementPlan plan)
    {
        if (attempt == null) throw new ArgumentNullException(nameof(attempt));
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.GenerationId != attempt.GenerationId)
        {
            throw new MapPlanningException(
                "StaleMonsterPlanning",
                $"Monster planning rejected GenerationId={plan.GenerationId:D}; expected {attempt.GenerationId:D}.",
                false);
        }
        if (!TryGetTarget(out _, out string targetReason))
        {
            throw new MapPlanningException(
                "MonsterTargetUnavailable",
                $"Required Monster planning failed. Role={MonsterRole}; " +
                $"LogicalObjectId={MainMonsterLogicalObjectId}; GenerationId={attempt.GenerationId:D}; " +
                $"Reason={targetReason}",
                false);
        }

        MonsterSemanticPlacementRequest request = new MonsterSemanticPlacementRequest(
            new MapPlacementRequest(
                MainMonsterLogicalObjectId,
                MonsterRole,
                new[] { Vector2Int.zero },
                true,
                true),
            profile);
        MonsterPlacementResult result = policy.SelectRequired(
            attempt.AttemptSeed,
            map,
            plan,
            request);
        MapPlacementReservation reservation = new MapPlacementReservation(
            MainMonsterLogicalObjectId,
            MonsterRole,
            result.PlacementCell,
            result.FootprintCells,
            MapPlacementOwnership.SceneBound);
        if (!plan.TryAdd(reservation, out string reason))
        {
            throw new MapPlanningException(
                "MonsterReservationConflict",
                $"Role={MonsterRole}; LogicalObjectId={MainMonsterLogicalObjectId}; " +
                $"GenerationId={plan.GenerationId:D}; Reason={reason}",
                false);
        }

        plannedGenerationId = attempt.GenerationId;
        ShortestPathSteps = result.ShortestPathSteps;
        return result;
    }

    public bool TryMaterialize(
        MapRuntimeContext context,
        Guid generationId,
        MapCoordinateBoundary coordinates,
        out string reason)
    {
        reason = string.Empty;
        if (context == null)
            return Fail(null, generationId, MapFailureCategory.Configuration,
                "MissingRuntimeContext", "Monster materialization requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Monster materialization is owned by RandomGenerated mode.", out reason);
        if (!context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActivePlacementPlan == null || context.ActiveRegistry == null)
        {
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleMonsterMaterialization",
                "Monster materialization rejected a stale or incomplete Generation.", out reason);
        }
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalMonsterMaterializationPhase",
                $"Monster materialization requires Materializing; current phase is {context.Phase}.", out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "DuplicateMonsterMaterialization",
                "Monster materialization rejected a duplicate Current Generation operation.", out reason);
        if (plannedGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleMonsterPlacementResult",
                "Monster materialization rejected a stale semantic placement result.", out reason);
        if (coordinates == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingMonsterCoordinateBoundary",
                "Monster materialization requires the shared coordinate boundary.", out reason);
        if (!TryGetTarget(out IMonsterPlacementTarget target, out string targetReason))
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MonsterTargetUnavailable", targetReason, out reason);
        if (!TryFindReservation(
                context.ActivePlacementPlan,
                out MapPlacementReservation reservation))
        {
            return Fail(context, generationId, MapFailureCategory.Registry,
                "MonsterReservationMissing",
                "Committed placement plan is missing the Required Monster reservation.", out reason);
        }

        Vector3 destination;
        try
        {
            destination = coordinates.CellToWorld(context.ActiveMap, reservation.AnchorCell);
        }
        catch (Exception exception)
        {
            return Fail(context, generationId, MapFailureCategory.Coordinate,
                "MonsterCoordinateConversionFailed", exception.Message, out reason);
        }

        Transform placementTransform = target.PlacementTransform;
        destination.z = placementTransform.position.z;
        Rigidbody2D body = placementTransform.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = destination;
        }
        placementTransform.position = destination;

        GameObject reinitializationTarget = target.ReinitializationTarget;
        if (!reinitializationTarget.activeSelf)
            reinitializationTarget.SetActive(true);
        if (!reinitializationTarget.activeInHierarchy)
        {
            return Fail(context, generationId, MapFailureCategory.Generation,
                "InactiveMonsterTarget",
                "The Required Monster must be active after materialization.", out reason);
        }
        Physics2D.SyncTransforms();

        if (!context.ActiveRegistry.TryBindInstance(
                generationId,
                MainMonsterLogicalObjectId,
                MonsterRole,
                monsterTarget,
                out string registryReason))
        {
            return Fail(context, generationId, MapFailureCategory.Registry,
                "MonsterRegistryBindingFailed", registryReason, out reason);
        }

        ReadyGenerationId = generationId;
        PlacementCell = reservation.AnchorCell;
        MaterializationCount++;
        return true;
    }

    private bool TryGetTarget(out IMonsterPlacementTarget target, out string reason)
    {
        target = monsterTarget as IMonsterPlacementTarget;
        if (monsterTarget == null)
        {
            reason = "Required Monster target reference is missing.";
            return false;
        }
        if (target == null)
        {
            reason = $"Monster target {monsterTarget.name} does not implement {nameof(IMonsterPlacementTarget)}.";
            return false;
        }
        if (target.PlacementTransform == null || target.ReinitializationTarget == null)
        {
            reason = "Required Monster target has no placement/reinitialization target.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool TryFindReservation(
        MapPlacementPlan plan,
        out MapPlacementReservation result)
    {
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (string.Equals(
                    reservation.LogicalObjectId,
                    MainMonsterLogicalObjectId,
                    StringComparison.Ordinal) &&
                string.Equals(reservation.Role, MonsterRole, StringComparison.Ordinal))
            {
                result = reservation;
                return true;
            }
        }
        result = null;
        return false;
    }

    private static bool Fail(
        MapRuntimeContext context,
        Guid generationId,
        MapFailureCategory category,
        string code,
        string message,
        out string reason)
    {
        reason = message;
        if (context != null)
        {
            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode,
                generationId,
                context.Phase,
                category,
                code,
                message));
        }
        return false;
    }
}
