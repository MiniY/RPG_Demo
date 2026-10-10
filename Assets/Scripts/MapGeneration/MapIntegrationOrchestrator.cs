using System;

public sealed class MapPendingGeneration
{
    public MapPendingGeneration(Guid generationId, MapData map, MapSimpleDecorationData decorations)
    {
        if (generationId == Guid.Empty) throw new ArgumentException("Generation ID cannot be empty.", nameof(generationId));
        GenerationId = generationId;
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Decorations = decorations;
    }
    public Guid GenerationId { get; }
    public MapData Map { get; }
    public MapSimpleDecorationData Decorations { get; }
}

public sealed class MapGenerationExecutionResult
{
    internal MapGenerationExecutionResult(bool succeeded, Guid requestId, Guid? generationId,
        MapPendingGeneration committedGeneration, MapFailureDiagnostic failure)
    { Succeeded = succeeded; RequestId = requestId; GenerationId = generationId;
      CommittedGeneration = committedGeneration; Failure = failure; }
    public bool Succeeded { get; }
    public Guid RequestId { get; }
    public Guid? GenerationId { get; }
    public MapPendingGeneration CommittedGeneration { get; }
    public MapFailureDiagnostic Failure { get; }
}

public sealed class MapIntegrationOrchestrator
{
    private readonly MapRuntimeContext context;
    public MapIntegrationOrchestrator(MapRuntimeContext context)
    { this.context = context ?? throw new ArgumentNullException(nameof(context)); }

    public event Action<Guid> GameplayReady;
    public MapRuntimeContext Context => context;

    public MapGenerationExecutionResult Execute(MapGenerationRequest request,
        Func<MapGenerationAttempt, MapPendingGeneration> generate,
        Func<MapGenerationAttempt, MapPendingGeneration, MapPlacementPlan> plan,
        Action<MapPendingGeneration> project)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (generate == null) throw new ArgumentNullException(nameof(generate));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (project == null) throw new ArgumentNullException(nameof(project));
        context.BeginRequest(request);

        try { MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Preparing); }
        catch (Exception exception) { return Fail(request, null, MapLifecyclePhase.Preparing,
            MapFailureCategory.Lifecycle, "IllegalPhaseTransition", exception.Message, false); }

        for (int index = 0; index < request.MaxAttempts; index++)
        {
            MapGenerationAttempt attempt = request.CreateAttempt(index, Guid.NewGuid());
            context.BeginAttempt(attempt);
            try
            {
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Generating);
                MapPendingGeneration pending = generate(attempt);
                if (pending == null || pending.GenerationId != attempt.GenerationId)
                    return Fail(request, attempt, MapLifecyclePhase.Generating,
                        MapFailureCategory.StaleResult, "StaleGenerationResult",
                        "Generated result does not belong to the current pending attempt.", false);
                context.SetPendingMap(attempt.GenerationId, pending.Map);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Planning);
                MapPlacementPlan placementPlan = plan(attempt, pending);
                if (placementPlan == null || placementPlan.GenerationId != attempt.GenerationId)
                    return Fail(request, attempt, MapLifecyclePhase.Planning,
                        MapFailureCategory.StaleResult, "StalePlacementResult",
                        "Placement result does not belong to the current pending attempt.", false);
                MapPlacementValidator.Validate(pending.Map, placementPlan);
                context.SetPendingPlan(attempt.GenerationId, placementPlan);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Planned);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Committing);
                MapObjectRegistry previousRegistry = context.CommitPending(attempt.GenerationId);
                previousRegistry?.Retire(previousRegistry.GenerationId);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Committed);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Projecting);
                try { project(pending); }
                catch (Exception exception)
                {
                    return Fail(request, attempt, MapLifecyclePhase.Projecting,
                        MapFailureCategory.Generation, "ProjectionFailed", exception.Message, true);
                }
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Materializing);
                context.AddAttemptDiagnostic(new MapGenerationAttemptDiagnostic(attempt,
                    MapAttemptOutcome.Succeeded, MapLifecyclePhase.Materializing, MapFailureCategory.None,
                    "Committed", "Pending generation committed and projected."));
                return new MapGenerationExecutionResult(true, request.RequestId,
                    attempt.GenerationId, pending, null);
            }
            catch (MapPlanningException exception)
            {
                if (context.IsCurrentPending(attempt.GenerationId)) context.RejectPending(attempt.GenerationId);
                bool canRetry = exception.Retryable && request.RetryPolicy == MapRetryPolicy.BoundedDeterministicRetry && index + 1 < request.MaxAttempts;
                context.AddAttemptDiagnostic(new MapGenerationAttemptDiagnostic(attempt,
                    exception.Retryable ? MapAttemptOutcome.RetryablePlanningRejection : MapAttemptOutcome.NonRetryableFailure,
                    MapLifecyclePhase.Planning, MapFailureCategory.Planning, exception.Code, exception.Message));
                if (!canRetry)
                    return Fail(request, attempt, MapLifecyclePhase.Planning, MapFailureCategory.Planning,
                        exception.Retryable && index + 1 >= request.MaxAttempts ? "RetryBudgetExhausted" : exception.Code,
                        exception.Message, false, false);
                MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Preparing);
            }
            catch (Exception exception)
            {
                MapLifecyclePhase failedPhase = context.Phase;
                if (context.IsCurrentPending(attempt.GenerationId)) context.RejectPending(attempt.GenerationId);
                return Fail(request, attempt, failedPhase,
                    failedPhase == MapLifecyclePhase.Planning ? MapFailureCategory.Planning : MapFailureCategory.Generation,
                    "UnhandledGenerationFailure", exception.Message, false);
            }
        }
        return Fail(request, null, MapLifecyclePhase.Planning, MapFailureCategory.Planning,
            "RetryBudgetExhausted", "Generation attempt budget was exhausted.", false);
    }

    public bool IsCurrentPending(Guid generationId) => context.IsCurrentPending(generationId);
    public bool IsCurrentActive(Guid generationId) => context.IsCurrentActive(generationId);

    internal bool PublishGameplayReady(Guid generationId)
    {
        if (!context.IsCurrentActive(generationId) || context.Phase != MapLifecyclePhase.Ready || context.IsFailed) return false;
        GameplayReady?.Invoke(generationId); return true;
    }

    private MapGenerationExecutionResult Fail(MapGenerationRequest request, MapGenerationAttempt attempt,
        MapLifecyclePhase phase, MapFailureCategory category, string code, string reason,
        bool postCommit, bool addAttempt = true)
    {
        if (!postCommit && attempt != null && context.IsCurrentPending(attempt.GenerationId))
            context.RejectPending(attempt.GenerationId);
        if (attempt != null && addAttempt)
            context.AddAttemptDiagnostic(new MapGenerationAttemptDiagnostic(attempt,
                postCommit ? MapAttemptOutcome.PostCommitFailure : MapAttemptOutcome.NonRetryableFailure,
                phase, category, code, reason));
        MapFailureDiagnostic failure = new MapFailureDiagnostic(context.Mode, request.RequestId,
            attempt?.GenerationId, phase, category, code, reason);
        context.RecordFailure(failure);
        return new MapGenerationExecutionResult(false, request.RequestId, attempt?.GenerationId, null, failure);
    }
}
