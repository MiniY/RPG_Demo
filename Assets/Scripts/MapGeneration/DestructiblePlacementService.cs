using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DestructiblePopulationPlan
{
    internal DestructiblePopulationPlan(
        int requestedTarget,
        IReadOnlyList<DestructiblePlacementResult> placements,
        string underfillReason)
    {
        RequestedTarget = requestedTarget;
        Placements = placements;
        UnderfillReason = underfillReason ?? string.Empty;
    }

    public int RequestedTarget { get; }
    public int ActualCount => Placements.Count;
    public int UnderfillCount => RequestedTarget - ActualCount;
    public string UnderfillReason { get; }
    public IReadOnlyList<DestructiblePlacementResult> Placements { get; }
}

/// <summary>Owns GenerationScoped destructible planning, materialization, and registry lifetime.</summary>
[DisallowMultipleComponent]
public sealed class DestructiblePlacementService : MonoBehaviour
{
    public const string DestructibleRole = "Destructible";
    public const string CurrentTypeId = "mushroom-small";
    public const string LogicalIdPrefix = "destructible.mushroom-small.";

    private sealed class ActiveDestructible
    {
        public ActiveDestructible(
            Guid generationId,
            string logicalObjectId,
            IDestructiblePlacementTarget target,
            GenerationScopedDestructibleLifetime lifetime,
            Action defeatedHandler)
        {
            GenerationId = generationId;
            LogicalObjectId = logicalObjectId;
            Target = target;
            Lifetime = lifetime;
            DefeatedHandler = defeatedHandler;
        }

        public Guid GenerationId { get; }
        public string LogicalObjectId { get; }
        public IDestructiblePlacementTarget Target { get; }
        public GenerationScopedDestructibleLifetime Lifetime { get; }
        public Action DefeatedHandler { get; }
    }

    [SerializeField] private GameObject destructiblePrefab;
    [SerializeField] private DestructiblePlacementProfile profile =
        new DestructiblePlacementProfile();

    private readonly DestructiblePlacementPolicy policy =
        new DestructiblePlacementPolicy();
    private readonly Dictionary<string, DestructiblePlacementResult> plannedPlacements =
        new Dictionary<string, DestructiblePlacementResult>(StringComparer.Ordinal);
    private readonly Dictionary<string, ActiveDestructible> activeInstances =
        new Dictionary<string, ActiveDestructible>(StringComparer.Ordinal);
    private MapRuntimeContext activeContext;
    private Guid? plannedGenerationId;

    public DestructiblePlacementProfile Profile => profile;
    public GameObject DestructiblePrefab => destructiblePrefab;
    public DestructiblePopulationPlan LastPopulationPlan { get; private set; }
    public Guid? ReadyGenerationId { get; private set; }
    public int MaterializationCount { get; private set; }
    public int ActiveCount => activeInstances.Count;
    public string LastLifecycleRejection { get; private set; } = string.Empty;

    public DestructiblePopulationPlan Plan(
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
                "StaleDestructiblePlanning",
                $"Destructible planning rejected GenerationId={plan.GenerationId:D}; " +
                $"expected {attempt.GenerationId:D}.",
                false);
        }

        ValidateConfiguration(attempt.GenerationId);
        plannedPlacements.Clear();
        List<DestructiblePlacementResult> placements =
            new List<DestructiblePlacementResult>();
        string underfillReason = string.Empty;
        for (int index = 1; index <= profile.TargetCount; index++)
        {
            string logicalObjectId = LogicalIdPrefix + index.ToString("D3");
            DestructibleSemanticPlacementRequest request =
                new DestructibleSemanticPlacementRequest(
                    new MapPlacementRequest(
                        logicalObjectId,
                        DestructibleRole,
                        new[] { Vector2Int.zero },
                        false,
                        true),
                    CurrentTypeId,
                    profile);
            DestructiblePlacementResult result = policy.SelectOptional(
                attempt.AttemptSeed,
                map,
                plan,
                request);
            if (result == null)
            {
                underfillReason = "EligibleCandidateExhausted";
                continue;
            }

            MapPlacementReservation reservation = new MapPlacementReservation(
                logicalObjectId,
                DestructibleRole,
                result.PlacementCell,
                result.FootprintCells,
                MapPlacementOwnership.GenerationScoped);
            if (!plan.TryAdd(reservation, out string reason))
            {
                throw new MapPlanningException(
                    "DestructibleReservationConflict",
                    $"Role={DestructibleRole}; Type={CurrentTypeId}; " +
                    $"LogicalObjectId={logicalObjectId}; " +
                    $"GenerationId={plan.GenerationId:D}; Reason={reason}",
                    false);
            }

            placements.Add(result);
            plannedPlacements.Add(logicalObjectId, result);
        }

        LastPopulationPlan = new DestructiblePopulationPlan(
            profile.TargetCount,
            placements.AsReadOnly(),
            underfillReason);
        if (placements.Count < profile.RequiredMinimum)
        {
            throw new MapPlanningException(
                "DestructibleRequiredMinimumUnavailable",
                $"Destructible planning produced {placements.Count}; " +
                $"RequiredMinimum={profile.RequiredMinimum}; " +
                $"TargetCount={profile.TargetCount}; " +
                $"UnderfillReason={underfillReason}; " +
                $"GenerationId={plan.GenerationId:D}.",
                true);
        }

        plannedGenerationId = attempt.GenerationId;
        return LastPopulationPlan;
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
                "MissingRuntimeContext",
                "Destructible materialization requires the runtime context.",
                out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode",
                "Destructible materialization is owned by RandomGenerated mode.",
                out reason);
        if (!context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActivePlacementPlan == null || context.ActiveRegistry == null)
        {
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleDestructibleMaterialization",
                "Destructible materialization rejected a stale or incomplete Generation.",
                out reason);
        }
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalDestructibleMaterializationPhase",
                $"Destructible materialization requires Materializing; " +
                $"current phase is {context.Phase}.",
                out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "DuplicateDestructibleMaterialization",
                "Destructible materialization rejected a duplicate Current Generation operation.",
                out reason);
        if (plannedGenerationId != generationId || LastPopulationPlan == null)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleDestructiblePlacementResult",
                "Destructible materialization rejected a stale population plan.",
                out reason);
        if (coordinates == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingDestructibleCoordinateBoundary",
                "Destructible materialization requires the shared coordinate boundary.",
                out reason);

        try
        {
            ValidateConfiguration(generationId);
        }
        catch (MapPlanningException exception)
        {
            return Fail(context, generationId, MapFailureCategory.Configuration,
                exception.Code, exception.Message, out reason);
        }

        Dictionary<string, MapPlacementReservation> reservations =
            CollectCommittedReservations(context.ActivePlacementPlan);
        if (reservations.Count != plannedPlacements.Count ||
            reservations.Count < profile.RequiredMinimum ||
            reservations.Count > profile.Maximum)
        {
            return Fail(context, generationId, MapFailureCategory.Registry,
                "DestructibleReservationCountMismatch",
                $"Committed Destructible reservations={reservations.Count}; " +
                $"planned={plannedPlacements.Count}; " +
                $"RequiredMinimum={profile.RequiredMinimum}; Maximum={profile.Maximum}.",
                out reason);
        }

        RetireActiveInstances();
        activeContext = context;
        foreach (DestructiblePlacementResult placement in LastPopulationPlan.Placements)
        {
            MapPlacementReservation reservation = reservations[placement.LogicalObjectId];
            Vector3 destination;
            try
            {
                destination = coordinates.CellToWorld(
                    context.ActiveMap,
                    reservation.AnchorCell);
            }
            catch (Exception exception)
            {
                RetireActiveInstances();
                return Fail(context, generationId, MapFailureCategory.Coordinate,
                    "DestructibleCoordinateConversionFailed",
                    exception.Message,
                    out reason);
            }

            IDestructiblePlacementTarget poolSource =
                destructiblePrefab.GetComponent<IDestructiblePlacementTarget>();
            GameObject instanceObject = poolSource.Spawn(
                destination,
                Quaternion.identity);
            IDestructiblePlacementTarget target = instanceObject != null
                ? instanceObject.GetComponent<IDestructiblePlacementTarget>()
                : null;
            if (target == null || target.PlacementInstance == null)
            {
                if (instanceObject != null)
                    poolSource.Return(instanceObject);
                RetireActiveInstances();
                return Fail(context, generationId, MapFailureCategory.Configuration,
                    "DestructibleDamageableMissing",
                    "The configured Destructible prefab must provide the " +
                    "IDestructiblePlacementTarget bridge over BaseDamageable.",
                    out reason);
            }

            string logicalObjectId = placement.LogicalObjectId;
            GenerationScopedDestructibleLifetime lifetime =
                instanceObject.GetComponent<GenerationScopedDestructibleLifetime>();
            if (lifetime == null)
                lifetime = instanceObject.AddComponent<GenerationScopedDestructibleLifetime>();
            Action defeatedHandler = () =>
                TryRelease(
                    generationId,
                    logicalObjectId,
                    target.PlacementInstance,
                    out _);
            target.SubscribeDefeated(defeatedHandler);
            lifetime.Arm(() =>
                TryRelease(
                    generationId,
                    logicalObjectId,
                    target.PlacementInstance,
                    out _));

            if (!context.ActiveRegistry.TryBindInstance(
                    generationId,
                    logicalObjectId,
                    DestructibleRole,
                    target.PlacementInstance,
                    out string registryReason))
            {
                target.UnsubscribeDefeated(defeatedHandler);
                lifetime.Disarm();
                poolSource.Return(instanceObject);
                RetireActiveInstances();
                return Fail(context, generationId, MapFailureCategory.Registry,
                    "DestructibleRegistryBindingFailed",
                    registryReason,
                    out reason);
            }

            activeInstances.Add(logicalObjectId, new ActiveDestructible(
                generationId,
                logicalObjectId,
                target,
                lifetime,
                defeatedHandler));
        }

        Physics2D.SyncTransforms();
        ReadyGenerationId = generationId;
        MaterializationCount++;
        return true;
    }

    public bool TryRelease(
        Guid generationId,
        string logicalObjectId,
        UnityEngine.Object instance,
        out string reason)
    {
        reason = string.Empty;
        if (activeContext == null ||
            !activeContext.IsCurrentActive(generationId) ||
            activeContext.ActiveRegistry == null)
        {
            return RejectLifecycle("StaleGeneration", out reason);
        }
        if (!activeInstances.TryGetValue(
                logicalObjectId,
                out ActiveDestructible active))
        {
            return RejectLifecycle("DuplicateOrUnknownLogicalObject", out reason);
        }
        if (active.GenerationId != generationId || active.Target.PlacementInstance != instance)
            return RejectLifecycle("GenerationOrInstanceMismatch", out reason);
        if (!activeContext.ActiveRegistry.Unregister(generationId, logicalObjectId))
            return RejectLifecycle("RegistryReleaseRejected", out reason);

        active.Target.UnsubscribeDefeated(active.DefeatedHandler);
        active.Lifetime.Disarm();
        activeInstances.Remove(logicalObjectId);
        LastLifecycleRejection = string.Empty;
        return true;
    }

    public bool TryGetActiveInstance(
        string logicalObjectId,
        out UnityEngine.Object instance)
    {
        if (activeInstances.TryGetValue(
                logicalObjectId,
                out ActiveDestructible active))
        {
            instance = active.Target.PlacementInstance;
            return true;
        }

        instance = null;
        return false;
    }

    private void RetireActiveInstances()
    {
        foreach (ActiveDestructible active in activeInstances.Values)
        {
            active.Target.UnsubscribeDefeated(active.DefeatedHandler);
            active.Lifetime.Disarm();
            active.Target.Return(active.Target.PlacementGameObject);
        }
        activeInstances.Clear();
        activeContext = null;
    }

    private void ValidateConfiguration(Guid generationId)
    {
        profile.Validate();
        if (destructiblePrefab == null)
        {
            throw new MapPlanningException(
                "DestructiblePrefabUnavailable",
                $"Stage 6 requires the explicit {CurrentTypeId} prefab; " +
                $"GenerationId={generationId:D}.",
                false);
        }
        IDestructiblePlacementTarget target =
            destructiblePrefab.GetComponent<IDestructiblePlacementTarget>();
        if (target == null || target.PlacementInstance == null)
        {
            throw new MapPlanningException(
                "DestructibleDamageableMissing",
                $"The {CurrentTypeId} prefab must provide the " +
                "IDestructiblePlacementTarget bridge over BaseDamageable; " +
                $"GenerationId={generationId:D}.",
                false);
        }
    }

    private static Dictionary<string, MapPlacementReservation>
        CollectCommittedReservations(MapPlacementPlan plan)
    {
        Dictionary<string, MapPlacementReservation> reservations =
            new Dictionary<string, MapPlacementReservation>(StringComparer.Ordinal);
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (string.Equals(
                    reservation.Role,
                    DestructibleRole,
                    StringComparison.Ordinal))
            {
                reservations.Add(reservation.LogicalObjectId, reservation);
            }
        }
        return reservations;
    }

    private bool RejectLifecycle(string code, out string reason)
    {
        LastLifecycleRejection = code;
        reason = code;
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
