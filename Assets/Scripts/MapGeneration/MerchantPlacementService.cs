using System;
using UnityEngine;

/// <summary>Narrow seam implemented by the existing main Merchant gameplay component.</summary>
public interface IMerchantPlacementTarget
{
    Transform PlacementTransform { get; }
    Transform BoundPlayer { get; }
    bool TryBindCanonicalPlayer(Transform canonicalPlayer, out string reason);
}

/// <summary>Owns Required Merchant planning reservations and post-commit materialization.</summary>
[DisallowMultipleComponent]
public sealed class MerchantPlacementService : MonoBehaviour
{
    public const string MainMerchantLogicalObjectId = "merchant.main";
    public const string MerchantRole = "Merchant";
    public const string InteractionApproachLogicalObjectId = "merchant.main.interaction";
    public const string InteractionApproachRole = "MerchantInteractionApproach";

    [SerializeField] private MonoBehaviour merchantTarget;
    [SerializeField] private MerchantPlacementProfile profile = new MerchantPlacementProfile();

    private readonly MerchantPlacementPolicy policy = new MerchantPlacementPolicy();

    public MerchantPlacementProfile Profile => profile;
    public MonoBehaviour MerchantTarget => merchantTarget;
    public Guid? ReadyGenerationId { get; private set; }
    public int MaterializationCount { get; private set; }
    public Vector2Int PlacementCell { get; private set; }
    public Vector2Int InteractionApproachCell { get; private set; }
    public int ShortestPathSteps { get; private set; }

    private Guid? plannedGenerationId;

    public MerchantPlacementResult Plan(
        MapGenerationAttempt attempt,
        MapData map,
        MapPlacementPlan plan)
    {
        if (attempt == null) throw new ArgumentNullException(nameof(attempt));
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.GenerationId != attempt.GenerationId)
            throw new MapPlanningException(
                "StaleMerchantPlanning",
                $"Merchant planning rejected GenerationId={plan.GenerationId:D}; expected {attempt.GenerationId:D}.",
                false);
        if (!TryGetTarget(out _, out string targetReason))
            throw new MapPlanningException(
                "MerchantTargetUnavailable",
                $"Required Merchant planning failed. Role={MerchantRole}; " +
                $"LogicalObjectId={MainMerchantLogicalObjectId}; GenerationId={attempt.GenerationId:D}; " +
                $"Reason={targetReason}",
                false);

        MerchantSemanticPlacementRequest request = new MerchantSemanticPlacementRequest(
            new MapPlacementRequest(
                MainMerchantLogicalObjectId,
                MerchantRole,
                new[] { Vector2Int.zero },
                true,
                true),
            profile);
        MerchantPlacementResult result = policy.SelectRequired(
            attempt.AttemptSeed,
            map,
            plan,
            request);

        AddReservation(
            plan,
            new MapPlacementReservation(
                MainMerchantLogicalObjectId,
                MerchantRole,
                result.PlacementCell,
                result.FootprintCells,
                MapPlacementOwnership.SceneBound));
        AddReservation(
            plan,
            new MapPlacementReservation(
                InteractionApproachLogicalObjectId,
                InteractionApproachRole,
                result.InteractionApproachCell,
                new[] { result.InteractionApproachCell },
                MapPlacementOwnership.GenerationScoped));
        plannedGenerationId = attempt.GenerationId;
        ShortestPathSteps = result.ShortestPathSteps;
        return result;
    }

    public bool TryMaterialize(
        MapRuntimeContext context,
        Guid generationId,
        MapCoordinateBoundary coordinates,
        CanonicalPlayerProvider playerProvider,
        Transform configuredPlayer,
        out string reason)
    {
        reason = string.Empty;
        if (context == null)
            return Fail(null, generationId, MapFailureCategory.Configuration,
                "MissingRuntimeContext", "Merchant materialization requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Merchant materialization is owned by RandomGenerated mode.", out reason);
        if (!context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActivePlacementPlan == null || context.ActiveRegistry == null)
        {
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleMerchantMaterialization",
                "Merchant materialization rejected a stale or incomplete Generation.", out reason);
        }
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalMerchantMaterializationPhase",
                $"Merchant materialization requires Materializing; current phase is {context.Phase}.", out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "DuplicateMerchantMaterialization",
                "Merchant materialization rejected a duplicate Current Generation operation.", out reason);
        if (plannedGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleMerchantPlacementResult",
                "Merchant materialization rejected a stale semantic placement result.", out reason);
        if (coordinates == null || playerProvider == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingMerchantDependency",
                "Merchant materialization requires coordinates and the Canonical Player provider.", out reason);
        if (!TryGetTarget(out IMerchantPlacementTarget target, out string targetReason))
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MerchantTargetUnavailable", targetReason, out reason);
        if (!playerProvider.TryResolve(
                configuredPlayer,
                out CanonicalPlayerBinding canonicalPlayer,
                out string playerReason))
        {
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "CanonicalPlayerUnavailable", playerReason, out reason);
        }
        if (!TryFindReservation(
                context.ActivePlacementPlan,
                MainMerchantLogicalObjectId,
                MerchantRole,
                out MapPlacementReservation merchantReservation) ||
            !TryFindReservation(
                context.ActivePlacementPlan,
                InteractionApproachLogicalObjectId,
                InteractionApproachRole,
                out MapPlacementReservation approachReservation))
        {
            return Fail(context, generationId, MapFailureCategory.Registry,
                "MerchantReservationMissing",
                "Committed placement plan is missing the Required Merchant or interaction reservation.", out reason);
        }

        Vector3 destination;
        try
        {
            destination = coordinates.CellToWorld(
                context.ActiveMap,
                merchantReservation.AnchorCell);
        }
        catch (Exception exception)
        {
            return Fail(context, generationId, MapFailureCategory.Coordinate,
                "MerchantCoordinateConversionFailed", exception.Message, out reason);
        }
        if (!target.TryBindCanonicalPlayer(canonicalPlayer.Player, out string bindingReason) ||
            target.BoundPlayer != canonicalPlayer.Player)
        {
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MerchantCanonicalPlayerBindingFailed",
                string.IsNullOrWhiteSpace(bindingReason)
                    ? "Merchant did not retain the Canonical Player binding."
                    : bindingReason,
                out reason);
        }

        destination.z = target.PlacementTransform.position.z;
        target.PlacementTransform.position = destination;
        Physics2D.SyncTransforms();
        if (!context.ActiveRegistry.TryBindInstance(
                generationId,
                MainMerchantLogicalObjectId,
                MerchantRole,
                merchantTarget,
                out string registryReason))
        {
            return Fail(context, generationId, MapFailureCategory.Registry,
                "MerchantRegistryBindingFailed", registryReason, out reason);
        }

        ReadyGenerationId = generationId;
        PlacementCell = merchantReservation.AnchorCell;
        InteractionApproachCell = approachReservation.AnchorCell;
        MaterializationCount++;
        return true;
    }

    private bool TryGetTarget(out IMerchantPlacementTarget target, out string reason)
    {
        target = merchantTarget as IMerchantPlacementTarget;
        if (merchantTarget == null)
        {
            reason = "Required Merchant target reference is missing.";
            return false;
        }
        if (target == null)
        {
            reason = $"Merchant target {merchantTarget.name} does not implement {nameof(IMerchantPlacementTarget)}.";
            return false;
        }
        if (target.PlacementTransform == null)
        {
            reason = "Required Merchant target has no placement Transform.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static void AddReservation(
        MapPlacementPlan plan,
        MapPlacementReservation reservation)
    {
        if (!plan.TryAdd(reservation, out string reason))
            throw new MapPlanningException(
                "MerchantReservationConflict",
                $"Role={reservation.Role}; LogicalObjectId={reservation.LogicalObjectId}; " +
                $"GenerationId={plan.GenerationId:D}; Reason={reason}",
                false);
    }

    private static bool TryFindReservation(
        MapPlacementPlan plan,
        string logicalObjectId,
        string role,
        out MapPlacementReservation result)
    {
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (string.Equals(reservation.LogicalObjectId, logicalObjectId, StringComparison.Ordinal) &&
                string.Equals(reservation.Role, role, StringComparison.Ordinal))
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
