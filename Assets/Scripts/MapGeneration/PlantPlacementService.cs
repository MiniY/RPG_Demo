using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Narrow placement and transient-state seam implemented by the existing Plant component.</summary>
public interface IPlantPlacementTarget
{
    Transform PlacementTransform { get; }
    GameObject ReinitializationTarget { get; }
    void SetPlacementAvailable(bool available);
}

[Serializable]
public sealed class PlantPlacementBinding
{
    [SerializeField] private string logicalObjectId;
    [SerializeField] private Transform plantTransform;

    public PlantPlacementBinding(string logicalObjectId, Transform plantTransform)
    {
        this.logicalObjectId = logicalObjectId;
        this.plantTransform = plantTransform;
    }

    public string LogicalObjectId => logicalObjectId;
    public Transform PlantTransform => plantTransform;
}

/// <summary>Owns Optional Plant planning and exact post-commit scene-bound relocation.</summary>
[DisallowMultipleComponent]
public sealed class PlantPlacementService : MonoBehaviour
{
    public const string PlantRole = "Plant";
    public const string MushroomBigLogicalIdPrefix = "plant.mushroom-big.";
    public const string MushroomMidSizeLogicalIdPrefix = "plant.mushroom-midsize.";
    public const string MushroomSmallLogicalIdPrefix = "plant.mushroom-small.";
    public const string SweetPotatoBigLogicalIdPrefix = "plant.sweet-potato-big.";
    public const string SweetPotatoSmallLogicalIdPrefix = "plant.sweet-potato-small.";

    private static readonly string[] currentLogicalObjectIds =
    {
        MushroomBigLogicalIdPrefix + "1",
        MushroomBigLogicalIdPrefix + "2",
        MushroomBigLogicalIdPrefix + "3",
        MushroomBigLogicalIdPrefix + "4",
        MushroomMidSizeLogicalIdPrefix + "1",
        MushroomMidSizeLogicalIdPrefix + "2",
        MushroomMidSizeLogicalIdPrefix + "3",
        MushroomMidSizeLogicalIdPrefix + "4",
        MushroomSmallLogicalIdPrefix + "1",
        MushroomSmallLogicalIdPrefix + "2",
        MushroomSmallLogicalIdPrefix + "3",
        MushroomSmallLogicalIdPrefix + "4",
        SweetPotatoBigLogicalIdPrefix + "1",
        SweetPotatoBigLogicalIdPrefix + "2",
        SweetPotatoBigLogicalIdPrefix + "3",
        SweetPotatoSmallLogicalIdPrefix + "1",
        SweetPotatoSmallLogicalIdPrefix + "2"
    };

    [SerializeField] private PlantPlacementBinding[] bindings =
        Array.Empty<PlantPlacementBinding>();
    [SerializeField] private PlantPlacementProfile profile =
        new PlantPlacementProfile();

    private readonly PlantPlacementPolicy policy = new PlantPlacementPolicy();
    private readonly Dictionary<string, PlantPlacementResult> plannedPlacements =
        new Dictionary<string, PlantPlacementResult>(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2Int> materializedCells =
        new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
    private readonly List<GameObject> reinitializationTargets = new List<GameObject>();
    private Guid? plannedGenerationId;

    public static string[] CurrentLogicalObjectIds =>
        (string[])currentLogicalObjectIds.Clone();
    public PlantPlacementProfile Profile => profile;
    public IReadOnlyList<PlantPlacementBinding> Bindings => bindings;
    public IReadOnlyList<GameObject> ReinitializationTargets => reinitializationTargets.AsReadOnly();
    public Guid? ReadyGenerationId { get; private set; }
    public int MaterializationCount { get; private set; }
    public int PlannedCount => plannedPlacements.Count;
    public int MaterializedCount => materializedCells.Count;

    public IReadOnlyList<PlantPlacementResult> Plan(
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
                "StalePlantPlanning",
                $"Plant planning rejected GenerationId={plan.GenerationId:D}; expected {attempt.GenerationId:D}.",
                false);
        }

        profile.Validate();
        List<PlantPlacementBinding> orderedBindings = ValidateAndOrderBindings(attempt.GenerationId);
        plannedPlacements.Clear();
        List<PlantPlacementResult> results = new List<PlantPlacementResult>();
        foreach (PlantPlacementBinding binding in orderedBindings)
        {
            PlantSemanticPlacementRequest request = new PlantSemanticPlacementRequest(
                new MapPlacementRequest(
                    binding.LogicalObjectId,
                    PlantRole,
                    new[] { Vector2Int.zero },
                    false,
                    true),
                profile);
            PlantPlacementResult result = policy.SelectOptional(
                attempt.AttemptSeed,
                map,
                plan,
                request);
            if (result == null)
                continue;

            MapPlacementReservation reservation = new MapPlacementReservation(
                binding.LogicalObjectId,
                PlantRole,
                result.PlacementCell,
                result.FootprintCells,
                MapPlacementOwnership.SceneBound);
            if (!plan.TryAdd(reservation, out string reason))
            {
                throw new MapPlanningException(
                    "PlantReservationConflict",
                    $"Role={PlantRole}; LogicalObjectId={binding.LogicalObjectId}; " +
                    $"GenerationId={plan.GenerationId:D}; Reason={reason}",
                    false);
            }
            plannedPlacements.Add(binding.LogicalObjectId, result);
            results.Add(result);
        }

        if (results.Count < profile.RequiredMinimum)
        {
            throw new MapPlanningException(
                "PlantRequiredMinimumUnavailable",
                $"Plant planning produced {results.Count}; RequiredMinimum={profile.RequiredMinimum}; " +
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
                "MissingRuntimeContext", "Plant materialization requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Plant materialization is owned by RandomGenerated mode.", out reason);
        if (!context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActivePlacementPlan == null || context.ActiveRegistry == null)
        {
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StalePlantMaterialization",
                "Plant materialization rejected a stale or incomplete Generation.", out reason);
        }
        if (context.Phase != MapLifecyclePhase.Materializing)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalPlantMaterializationPhase",
                $"Plant materialization requires Materializing; current phase is {context.Phase}.", out reason);
        if (ReadyGenerationId == generationId)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "DuplicatePlantMaterialization",
                "Plant materialization rejected a duplicate Current Generation operation.", out reason);
        if (plannedGenerationId != generationId)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StalePlantPlacementResult",
                "Plant materialization rejected a stale semantic placement result.", out reason);
        if (coordinates == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingPlantCoordinateBoundary",
                "Plant materialization requires the shared coordinate boundary.", out reason);

        List<PlantPlacementBinding> orderedBindings;
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
                "PlantReservationCountMismatch",
                $"Committed Plant reservations={reservations.Count}; planned={plannedPlacements.Count}; " +
                $"RequiredMinimum={profile.RequiredMinimum}; Maximum={profile.Maximum}.", out reason);
        }

        materializedCells.Clear();
        reinitializationTargets.Clear();
        foreach (PlantPlacementBinding binding in orderedBindings)
        {
            IPlantPlacementTarget target = GetTarget(binding);
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
                    "PlantCoordinateConversionFailed", exception.Message, out reason);
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
                    PlantRole,
                    target as UnityEngine.Object,
                    out string registryReason))
            {
                return Fail(context, generationId, MapFailureCategory.Registry,
                    "PlantRegistryBindingFailed", registryReason, out reason);
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

    private List<PlantPlacementBinding> ValidateAndOrderBindings(Guid generationId)
    {
        if (bindings == null || bindings.Length != profile.TargetCount)
        {
            throw new MapPlanningException(
                "PlantBindingCountMismatch",
                $"Plant configuration requires exactly {profile.TargetCount} scene-bound bindings; " +
                $"found {bindings?.Length ?? 0}; GenerationId={generationId:D}.",
                false);
        }

        HashSet<string> expectedLogicalIds = new HashSet<string>(
            currentLogicalObjectIds,
            StringComparer.Ordinal);
        HashSet<Transform> transforms = new HashSet<Transform>();
        List<PlantPlacementBinding> ordered = new List<PlantPlacementBinding>(bindings.Length);
        foreach (PlantPlacementBinding binding in bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.LogicalObjectId) ||
                binding.PlantTransform == null)
            {
                throw new MapPlanningException(
                    "PlantBindingUnavailable",
                    $"Every Plant binding requires a stable Logical ID and scene Transform; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            if (!expectedLogicalIds.Remove(binding.LogicalObjectId) ||
                !transforms.Add(binding.PlantTransform))
            {
                throw new MapPlanningException(
                    "DuplicateOrUnknownPlantBinding",
                    $"Plant bindings require the exact stable Logical IDs and unique scene targets; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            if (binding.PlantTransform.GetComponent<IPlantPlacementTarget>() == null)
            {
                throw new MapPlanningException(
                    "PlantTargetContractMissing",
                    $"{binding.PlantTransform.name} does not implement {nameof(IPlantPlacementTarget)}; " +
                    $"GenerationId={generationId:D}.",
                    false);
            }
            ordered.Add(binding);
        }

        if (expectedLogicalIds.Count != 0)
        {
            throw new MapPlanningException(
                "PlantLogicalIdentityMismatch",
                $"Plant bindings are missing one or more current production Logical IDs; " +
                $"GenerationId={generationId:D}.",
                false);
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
            if (!string.Equals(reservation.Role, PlantRole, StringComparison.Ordinal))
                continue;
            reservations.Add(reservation.LogicalObjectId, reservation);
        }
        return reservations;
    }

    private static IPlantPlacementTarget GetTarget(PlantPlacementBinding binding) =>
        binding.PlantTransform.GetComponent<IPlantPlacementTarget>();

    private static void OmitTarget(IPlantPlacementTarget target)
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
