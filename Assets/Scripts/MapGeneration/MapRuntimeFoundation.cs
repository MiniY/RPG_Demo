using System;
using System.Collections.Generic;

public enum MapRuntimeMode { RandomGenerated = 0, LegacyStatic = 1 }

public enum MapLifecyclePhase
{
    NotStarted = 0, ValidatingConfiguration = 1, Initialized = 2, Preparing = 3,
    Generating = 4, Planning = 5, Planned = 6, Committing = 7, Committed = 8,
    Projecting = 9, Materializing = 10, SpawningPlayer = 11, Reinitializing = 12,
    BindingCamera = 13, ValidatingRuntime = 14, Ready = 15, Failed = 16
}

public enum MapFailureCategory
{
    None = 0, Configuration = 1, ModeConflict = 2, Coordinate = 3,
    Lifecycle = 4, Generation = 5, Planning = 6, Registry = 7,
    StaleResult = 8, Unknown = 9
}

public readonly struct MapRuntimeAuthorityState
{
    public MapRuntimeAuthorityState(bool randomGeneratedActive, bool legacyStaticActive)
    {
        RandomGeneratedActive = randomGeneratedActive;
        LegacyStaticActive = legacyStaticActive;
    }
    public bool RandomGeneratedActive { get; }
    public bool LegacyStaticActive { get; }
}

public sealed class MapFailureDiagnostic
{
    public MapFailureDiagnostic(MapRuntimeMode mode, Guid? generationId,
        MapLifecyclePhase phase, MapFailureCategory category, string code, string reason)
        : this(mode, null, generationId, phase, category, code, reason) { }

    public MapFailureDiagnostic(MapRuntimeMode mode, Guid? requestId, Guid? generationId,
        MapLifecyclePhase phase, MapFailureCategory category, string code, string reason)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Failure code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Failure reason cannot be empty.", nameof(reason));
        Mode = mode; RequestId = requestId; GenerationId = generationId; Phase = phase;
        Category = category; Code = code; Reason = reason;
    }
    public MapRuntimeMode Mode { get; }
    public Guid? RequestId { get; }
    public Guid? GenerationId { get; }
    public MapLifecyclePhase Phase { get; }
    public MapFailureCategory Category { get; }
    public string Code { get; }
    public string Reason { get; }
}

public sealed class MapRuntimeDiagnosticSnapshot
{
    internal MapRuntimeDiagnosticSnapshot(MapRuntimeMode mode, Guid? activeGenerationId,
        Guid? pendingGenerationId, MapLifecyclePhase phase,
        MapRuntimeAuthorityState authorityState, MapFailureDiagnostic failure,
        MapGenerationRequest activeRequest, IReadOnlyList<MapGenerationAttemptDiagnostic> attemptChain,
        MapData activeMap, MapPlacementPlan activePlacementPlan, MapObjectRegistry activeRegistry)
    {
        Mode = mode; ActiveGenerationId = activeGenerationId; PendingGenerationId = pendingGenerationId;
        Phase = phase; AuthorityState = authorityState; Failure = failure; ActiveRequest = activeRequest;
        AttemptChain = attemptChain ?? Array.Empty<MapGenerationAttemptDiagnostic>();
        HasActiveMap = activeMap != null;
        PlacementReservationCount = activePlacementPlan?.ReservationCount ?? 0;
        RegistryEntryCount = activeRegistry?.Count ?? 0;
        IsReady = phase == MapLifecyclePhase.Ready && failure == null;
        IsFailed = phase == MapLifecyclePhase.Failed || failure != null;
        StateSummary = $"Mode={Mode}; ActiveGenerationId={Format(ActiveGenerationId)}; " +
            $"PendingGenerationId={Format(PendingGenerationId)}; Phase={Phase}; Ready={IsReady}; " +
            $"Failed={IsFailed}; HasActiveMap={HasActiveMap}; PlacementReservations={PlacementReservationCount}; " +
            $"RegistryEntries={RegistryEntryCount}; Attempts={AttemptChain.Count}; FailureCode={Failure?.Code ?? "none"}";
    }
    public MapRuntimeMode Mode { get; }
    public Guid? ActiveGenerationId { get; }
    public Guid? PendingGenerationId { get; }
    public MapLifecyclePhase Phase { get; }
    public MapRuntimeAuthorityState AuthorityState { get; }
    public bool IsReady { get; }
    public bool IsFailed { get; }
    public MapFailureDiagnostic Failure { get; }
    public MapGenerationRequest ActiveRequest { get; }
    public IReadOnlyList<MapGenerationAttemptDiagnostic> AttemptChain { get; }
    public bool HasActiveMap { get; }
    public int PlacementReservationCount { get; }
    public int RegistryEntryCount { get; }
    public string StateSummary { get; }
    private static string Format(Guid? value) => value?.ToString("D") ?? "none";
}

public sealed class MapRuntimeContext
{
    private readonly List<MapGenerationAttemptDiagnostic> attemptChain = new List<MapGenerationAttemptDiagnostic>();
    private Guid? activeGenerationId;
    private Guid? pendingGenerationId;
    private MapLifecyclePhase phase;
    private MapRuntimeAuthorityState authorityState;
    private MapFailureDiagnostic failure;
    private MapGenerationRequest activeRequest;
    private MapData activeMap;
    private MapData pendingMap;
    private MapPlacementPlan activePlacementPlan;
    private MapPlacementPlan pendingPlacementPlan;
    private MapObjectRegistry activeRegistry;

    public MapRuntimeContext(MapRuntimeMode mode) { Mode = mode; phase = MapLifecyclePhase.NotStarted; }
    public MapRuntimeMode Mode { get; }
    public Guid? ActiveGenerationId => activeGenerationId;
    public Guid? PendingGenerationId => pendingGenerationId;
    public MapLifecyclePhase Phase => phase;
    public bool IsReady => phase == MapLifecyclePhase.Ready && failure == null;
    public bool IsFailed => phase == MapLifecyclePhase.Failed || failure != null;
    public MapFailureDiagnostic Failure => failure;
    public MapData ActiveMap => activeMap;
    public MapData PendingMap => pendingMap;
    public MapPlacementPlan ActivePlacementPlan => activePlacementPlan;
    public MapPlacementPlan PendingPlacementPlan => pendingPlacementPlan;
    public MapObjectRegistry ActiveRegistry => activeRegistry;
    public IReadOnlyList<MapGenerationAttemptDiagnostic> AttemptChain => attemptChain.AsReadOnly();

    internal void SetPhase(MapLifecyclePhase nextPhase) => phase = nextPhase;
    internal void SetGenerationIdentifiers(Guid? active, Guid? pending) { activeGenerationId = active; pendingGenerationId = pending; }
    internal void RecordAuthorityState(MapRuntimeAuthorityState state) => authorityState = state;
    internal void BeginRequest(MapGenerationRequest request)
    {
        activeRequest = request ?? throw new ArgumentNullException(nameof(request));
        attemptChain.Clear(); failure = null;
    }
    internal void BeginAttempt(MapGenerationAttempt attempt)
    {
        if (attempt == null) throw new ArgumentNullException(nameof(attempt));
        pendingGenerationId = attempt.GenerationId; pendingMap = null; pendingPlacementPlan = null;
    }
    internal void SetPendingMap(Guid generationId, MapData map)
    {
        EnsurePending(generationId); pendingMap = map ?? throw new ArgumentNullException(nameof(map));
    }
    internal void SetPendingPlan(Guid generationId, MapPlacementPlan plan)
    {
        EnsurePending(generationId);
        if (plan == null || plan.GenerationId != generationId)
            throw new ArgumentException("Placement plan must belong to the pending generation.", nameof(plan));
        pendingPlacementPlan = plan;
    }
    internal MapObjectRegistry CommitPending(Guid generationId)
    {
        EnsurePending(generationId);
        if (pendingMap == null || pendingPlacementPlan == null)
            throw new InvalidOperationException("Pending map and placement plan must be complete before commit.");
        MapObjectRegistry previous = activeRegistry;
        activeGenerationId = generationId; activeMap = pendingMap; activePlacementPlan = pendingPlacementPlan;
        activeRegistry = new MapObjectRegistry(generationId);
        foreach (MapPlacementReservation reservation in pendingPlacementPlan.Reservations)
        {
            if (!activeRegistry.TryRegister(
                    generationId,
                    reservation.LogicalObjectId,
                    reservation.Role,
                    null,
                    reservation.AnchorCell,
                    reservation.Ownership,
                    reservation.OccupiedCells,
                    out string reason))
            {
                activeRegistry.Retire(generationId);
                activeRegistry = null;
                throw new InvalidOperationException($"Registry commit failed: {reason}");
            }
        }
        ClearPending(); return previous;
    }
    internal void RejectPending(Guid generationId) { EnsurePending(generationId); ClearPending(); }
    internal void AddAttemptDiagnostic(MapGenerationAttemptDiagnostic diagnostic)
    {
        attemptChain.Add(diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
    }
    internal void RecordFailure(MapFailureDiagnostic diagnostic)
    {
        failure = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)); phase = MapLifecyclePhase.Failed;
    }
    internal void ClearFailure() => failure = null;
    internal bool IsCurrentPending(Guid id) => pendingGenerationId == id;
    internal bool IsCurrentActive(Guid id) => activeGenerationId == id;
    public MapRuntimeDiagnosticSnapshot CreateDiagnosticSnapshot() => new MapRuntimeDiagnosticSnapshot(
        Mode, activeGenerationId, pendingGenerationId, phase, authorityState, failure,
        activeRequest, attemptChain.AsReadOnly(), activeMap, activePlacementPlan, activeRegistry);
    private void EnsurePending(Guid id)
    {
        if (pendingGenerationId != id) throw new InvalidOperationException($"Generation {id:D} is not pending.");
    }
    private void ClearPending() { pendingGenerationId = null; pendingMap = null; pendingPlacementPlan = null; }
}

public static class MapRuntimeModeValidator
{
    public static MapFailureDiagnostic Validate(MapRuntimeMode mode, MapRuntimeAuthorityState state)
    {
        switch (mode)
        {
            case MapRuntimeMode.RandomGenerated:
                if (state.LegacyStaticActive) return Failure(mode, MapFailureCategory.ModeConflict, "RandomModeLegacyAuthorityActive", "Legacy authority is active in RandomGenerated mode.");
                if (!state.RandomGeneratedActive) return Failure(mode, MapFailureCategory.Configuration, "RandomAuthorityInactive", "Random authority must be active in RandomGenerated mode.");
                return null;
            case MapRuntimeMode.LegacyStatic:
                if (state.RandomGeneratedActive) return Failure(mode, MapFailureCategory.ModeConflict, "LegacyModeRandomAuthorityActive", "Random authority is active in LegacyStatic mode.");
                if (!state.LegacyStaticActive) return Failure(mode, MapFailureCategory.Configuration, "LegacyAuthorityInactive", "Legacy authority must be active in LegacyStatic mode.");
                return null;
            default: return Failure(mode, MapFailureCategory.Configuration, "UnsupportedRuntimeMode", $"Unsupported runtime mode: {mode}.");
        }
    }
    private static MapFailureDiagnostic Failure(MapRuntimeMode mode, MapFailureCategory category, string code, string reason) =>
        new MapFailureDiagnostic(mode, null, MapLifecyclePhase.ValidatingConfiguration, category, code, reason);
}
