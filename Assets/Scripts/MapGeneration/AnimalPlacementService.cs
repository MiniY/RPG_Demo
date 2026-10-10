using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Narrow placement and transient-state seam implemented by the existing Animal component.</summary>
public interface IAnimalPlacementTarget
{
    Transform PlacementTransform { get; }
    GameObject ReinitializationTarget { get; }
    void SetPlacementAvailable(bool available);
}

[Serializable]
public sealed class AnimalPlacementBinding
{
    [SerializeField] private string logicalObjectId;
    [SerializeField] private Transform animalTransform;

    public AnimalPlacementBinding(string logicalObjectId, Transform animalTransform)
    {
        this.logicalObjectId = logicalObjectId;
        this.animalTransform = animalTransform;
    }

    public string LogicalObjectId => logicalObjectId;
    public Transform AnimalTransform => animalTransform;
}

/// <summary>Owns Optional Animal planning and exact post-commit scene-bound relocation.</summary>
[DisallowMultipleComponent]
public sealed class AnimalPlacementService : MonoBehaviour
{
    public const string AnimalRole = "Animal";
    public const string HappySheepLogicalIdPrefix = "animal.happy-sheep.";

    [SerializeField] private AnimalPlacementBinding[] bindings =
        Array.Empty<AnimalPlacementBinding>();
    [SerializeField] private AnimalPlacementProfile profile =
        new AnimalPlacementProfile();

    private readonly AnimalPlacementPolicy policy = new AnimalPlacementPolicy();
    private readonly Dictionary<string, AnimalPlacementResult> plannedPlacements =
        new Dictionary<string, AnimalPlacementResult>(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2Int> materializedCells =
        new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
    private readonly List<GameObject> reinitializationTargets = new List<GameObject>();
    private Guid? plannedGenerationId;

    public AnimalPlacementProfile Profile => profile;
    public IReadOnlyList<AnimalPlacementBinding> Bindings => bindings;
    public IReadOnlyList<GameObject> ReinitializationTargets => reinitializationTargets.AsReadOnly();
    public Guid? ReadyGenerationId { get; private set; }
    public int MaterializationCount { get; private set; }
    public int PlannedCount => plannedPlacements.Count;
    public int MaterializedCount => materializedCells.Count;

    public IReadOnlyList<AnimalPlacementResult> Plan(
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
                "StaleAnimalPlanning",
                $"Animal planning rejected GenerationId={plan.GenerationId:D}; expected {attempt.GenerationId:D}.",
                false);
        }

        profile.Validate();
        List<AnimalPlacementBinding> orderedBindings = ValidateAndOrderBindings(
            attempt.GenerationId);
        plannedPlacements.Clear();
        List<AnimalPlacementResult> results = new List<AnimalPlacementResult>();
        foreach (AnimalPlacementBinding binding in orderedBindings)
        {
            AnimalSemanticPlacementRequest request = new AnimalSemanticPlacementRequest(
                new MapPlacementRequest(
                    binding.LogicalObjectId,
                    AnimalRole,
                    new[] { Vector2Int.zero },
                    false,
                    true),
                profile);
            AnimalPlacementResult result = policy.SelectOptional(
                attempt.AttemptSeed,
                map,
                plan,
                request);
            if (result == null)
                continue;

            MapPlacementReservation reservation = new MapPlacementReservation(
                binding.LogicalObjectId,
                AnimalRole,
                result.PlacementCell,
                result.FootprintCells,
                MapPlacementOwnership.SceneBound);
            if (!plan.TryAdd(reservation, out string reason))
            {
                throw new MapPlanningException(
                    "AnimalReservationConflict",
                    $"Role={AnimalRole}; LogicalObjectId={binding.LogicalObjectId}; " +
                    $"GenerationId={plan.GenerationId:D}; Reason={reason}",
                    false);
            }
            plannedPlacements.Add(binding.LogicalObjectId, result);
            results.Add(result);
        }

        if (results.Count < profile.RequiredMinimum)
        {
            throw new MapPlanningException(
                "AnimalRequiredMinimumUnavailable",
                $"Animal planning produced {results.Count}; RequiredMinimum={profile.RequiredMinimum}; " +
                $"GenerationId={plan.GenerationId:D}.",
                false);
        }

        plannedGenerationId = attempt.GenerationId;
        return results.AsReadOnly();
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
                "MissingRuntimeContext", "Animal materialization requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Animal materialization is owned by RandomGenerated mode.", out reason);
        if (!context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActivePlacementPlan == null || context.ActiveRegistry == null)
        {
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleAnimalMaterialization",
                "Animal materialization rejected a stale or incomplete Generation.", out reason);
        }
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalAnimalMaterializationPhase",
                $"Animal materialization requires Materializing; current phase is {context.Phase}.", out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "DuplicateAnimalMaterialization",
                "Animal materialization rejected a duplicate Current Generation operation.", out reason);
        if (plannedGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleAnimalPlacementResult",
                "Animal materialization rejected a stale semantic placement result.", out reason);
        if (coordinates == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingAnimalCoordinateBoundary",
                "Animal materialization requires the shared coordinate boundary.", out reason);

        List<AnimalPlacementBinding> orderedBindings;
        try
        {
            orderedBindings = ValidateAndOrderBindings(generationId);
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
                "AnimalReservationCountMismatch",
                $"Committed Animal reservations={reservations.Count}; planned={plannedPlacements.Count}; " +
                $"RequiredMinimum={profile.RequiredMinimum}; Maximum={profile.Maximum}.", out reason);
        }

        materializedCells.Clear();
        reinitializationTargets.Clear();
        foreach (AnimalPlacementBinding binding in orderedBindings)
        {
            IAnimalPlacementTarget target = GetTarget(binding);
            if (!reservations.TryGetValue(binding.LogicalObjectId, out MapPlacementReservation reservation))
            {
                OmitTarget(target);
                continue;
            }

            Vector3 destination;
            try
            {
                destination = coordinates.CellToWorld(context.ActiveMap, reservation.AnchorCell);
            }
            catch (Exception exception)
            {
                return Fail(context, generationId, MapFailureCategory.Coordinate,
                    "AnimalCoordinateConversionFailed", exception.Message, out reason);
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
            target.SetPlacementAvailable(true);

            if (!context.ActiveRegistry.TryBindInstance(
                    generationId,
                    binding.LogicalObjectId,
                    AnimalRole,
                    target as UnityEngine.Object,
                    out string registryReason))
            {
                return Fail(context, generationId, MapFailureCategory.Registry,
                    "AnimalRegistryBindingFailed", registryReason, out reason);
            }

            materializedCells.Add(binding.LogicalObjectId, reservation.AnchorCell);
            if (target.ReinitializationTarget.activeInHierarchy)
                reinitializationTargets.Add(target.ReinitializationTarget);
        }

        Physics2D.SyncTransforms();
        ReadyGenerationId = generationId;
        MaterializationCount++;
        return true;
    }

    public bool TryGetMaterializedCell(string logicalObjectId, out Vector2Int cell) =>
        materializedCells.TryGetValue(logicalObjectId, out cell);

    private List<AnimalPlacementBinding> ValidateAndOrderBindings(Guid generationId)
    {
        if (bindings == null || bindings.Length != profile.TargetCount)
        {
            throw new MapPlanningException(
                "AnimalBindingCountMismatch",
                $"Animal configuration requires exactly {profile.TargetCount} scene-bound bindings; " +
                $"found {bindings?.Length ?? 0}; GenerationId={generationId:D}.",
                false);
        }

        HashSet<string> logicalIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<Transform> transforms = new HashSet<Transform>();
        List<AnimalPlacementBinding> ordered = new List<AnimalPlacementBinding>(bindings.Length);
        foreach (AnimalPlacementBinding binding in bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.LogicalObjectId) ||
                binding.AnimalTransform == null)
            {
                throw new MapPlanningException(
                    "AnimalBindingUnavailable",
                    $"Every Animal binding requires a stable Logical ID and scene Transform; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            if (!logicalIds.Add(binding.LogicalObjectId) ||
                !transforms.Add(binding.AnimalTransform))
            {
                throw new MapPlanningException(
                    "DuplicateAnimalBinding",
                    $"Animal bindings require unique Logical IDs and scene targets; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            if (binding.AnimalTransform.GetComponent<IAnimalPlacementTarget>() == null)
            {
                throw new MapPlanningException(
                    "AnimalTargetContractMissing",
                    $"{binding.AnimalTransform.name} does not implement {nameof(IAnimalPlacementTarget)}; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            ordered.Add(binding);
        }

        ordered.Sort((left, right) => string.Compare(
            left.LogicalObjectId,
            right.LogicalObjectId,
            StringComparison.Ordinal));
        return ordered;
    }

    private static Dictionary<string, MapPlacementReservation> CollectCommittedReservations(
        MapPlacementPlan plan)
    {
        Dictionary<string, MapPlacementReservation> reservations =
            new Dictionary<string, MapPlacementReservation>(StringComparer.Ordinal);
        foreach (MapPlacementReservation reservation in plan.Reservations)
        {
            if (!string.Equals(reservation.Role, AnimalRole, StringComparison.Ordinal))
                continue;
            reservations.Add(reservation.LogicalObjectId, reservation);
        }
        return reservations;
    }

    private static IAnimalPlacementTarget GetTarget(AnimalPlacementBinding binding) =>
        binding.AnimalTransform.GetComponent<IAnimalPlacementTarget>();

    private void OmitTarget(IAnimalPlacementTarget target)
    {
        GameObject targetObject = target.ReinitializationTarget;
        target.SetPlacementAvailable(false);
        if (targetObject.activeInHierarchy &&
            target is IMapDependentReinitializable reinitializable)
        {
            reinitializable.ReinitializeForMap();
        }
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
