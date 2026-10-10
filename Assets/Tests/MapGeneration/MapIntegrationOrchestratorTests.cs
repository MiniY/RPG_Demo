using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class MapIntegrationOrchestratorTests
{
    [Serializable]
    private sealed class AttemptChainEvidence
    {
        public string scenario;
        public string requestId;
        public int requestedSeed;
        public string retryPolicy;
        public int maxAttempts;
        public int attemptId;
        public int attemptSeed;
        public string generationId;
        public string mode;
        public string phase;
        public string outcome;
        public string failureCode;
        public string finalResult;
        public string finalFailureCode;
    }

    [Test]
    public void StrictSeedCommitsOneAttemptAndDoesNotPublishGameplayReady()
    {
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationRequest request = new MapGenerationRequest(Guid.NewGuid(), 12345);
        int readyCount = 0;
        orchestrator.GameplayReady += _ => readyCount++;

        MapGenerationExecutionResult result = ExecuteSuccess(orchestrator, request);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.GenerationId, Is.EqualTo(context.ActiveGenerationId));
        Assert.That(context.PendingGenerationId, Is.Null);
        Assert.That(context.Phase, Is.EqualTo(MapLifecyclePhase.Materializing));
        Assert.That(context.AttemptChain, Has.Count.EqualTo(1));
        Assert.That(context.AttemptChain[0].Attempt.AttemptSeed, Is.EqualTo(12345));
        Assert.That(context.AttemptChain[0].Outcome, Is.EqualTo(MapAttemptOutcome.Succeeded));
        Assert.That(readyCount, Is.Zero);
    }

    [Test]
    public void BoundedRetryIsDeterministicAndUsesUniqueGenerationIds()
    {
        MapGenerationRequest firstRequest = new MapGenerationRequest(Guid.NewGuid(), -91, "acceptance",
            MapRetryPolicy.BoundedDeterministicRetry, 3, 2);
        MapRuntimeContext firstContext = InitializedContext();
        MapIntegrationOrchestrator first = new MapIntegrationOrchestrator(firstContext);
        int planningCalls = 0;

        MapGenerationExecutionResult firstResult = first.Execute(firstRequest,
            attempt => Pending(attempt),
            (attempt, pending) =>
            {
                planningCalls++;
                if (attempt.AttemptIndex < 2)
                    throw new MapPlanningException("CandidateExhaustion", "No legal candidate.", true);
                return Plan(attempt, pending.Map);
            },
            _ => { });

        Assert.That(firstResult.Succeeded, Is.True);
        Assert.That(planningCalls, Is.EqualTo(3));
        Assert.That(firstContext.AttemptChain, Has.Count.EqualTo(3));
        Assert.That(firstContext.AttemptChain[0].Outcome, Is.EqualTo(MapAttemptOutcome.RetryablePlanningRejection));
        Assert.That(firstContext.AttemptChain[1].Outcome, Is.EqualTo(MapAttemptOutcome.RetryablePlanningRejection));
        Assert.That(firstContext.AttemptChain[2].Outcome, Is.EqualTo(MapAttemptOutcome.Succeeded));
        HashSet<Guid> ids = new HashSet<Guid>();
        foreach (MapGenerationAttemptDiagnostic diagnostic in firstContext.AttemptChain)
            Assert.That(ids.Add(diagnostic.Attempt.GenerationId), Is.True);

        MapGenerationRequest replay = new MapGenerationRequest(Guid.NewGuid(), -91, "acceptance",
            MapRetryPolicy.BoundedDeterministicRetry, 3, 2);
        for (int index = 0; index < 3; index++)
            Assert.That(replay.CreateAttempt(index, Guid.NewGuid()).AttemptSeed,
                Is.EqualTo(firstContext.AttemptChain[index].Attempt.AttemptSeed));
    }

    [Test]
    public void RetryBudgetExhaustionKeepsPriorActiveGenerationIntact()
    {
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        ExecuteSuccess(orchestrator, new MapGenerationRequest(Guid.NewGuid(), 10));
        Guid activeId = context.ActiveGenerationId.Value;
        MapData activeMap = context.ActiveMap;
        MapObjectRegistry activeRegistry = context.ActiveRegistry;

        MapGenerationRequest request = new MapGenerationRequest(Guid.NewGuid(), 20, "production",
            MapRetryPolicy.BoundedDeterministicRetry, 2);
        MapGenerationExecutionResult result = orchestrator.Execute(request,
            Pending,
            (attempt, pending) => throw new MapPlanningException("InsufficientEligibleArea", "Too little capacity.", true),
            _ => Assert.Fail("A rejected pending generation must not project."));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure.Code, Is.EqualTo("RetryBudgetExhausted"));
        Assert.That(context.ActiveGenerationId, Is.EqualTo(activeId));
        Assert.That(context.ActiveMap, Is.SameAs(activeMap));
        Assert.That(context.ActiveRegistry, Is.SameAs(activeRegistry));
        Assert.That(activeRegistry.IsRetired, Is.False);
        Assert.That(context.PendingGenerationId, Is.Null);
    }

    [Test]
    public void CommitRetiresPreviousRegistryExactlyOnce()
    {
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        ExecuteSuccess(orchestrator, new MapGenerationRequest(Guid.NewGuid(), 1));
        MapObjectRegistry previous = context.ActiveRegistry;

        ExecuteSuccess(orchestrator, new MapGenerationRequest(Guid.NewGuid(), 2));

        Assert.That(previous.IsRetired, Is.True);
        Assert.That(previous.Retire(previous.GenerationId), Is.False);
        Assert.That(context.ActiveRegistry, Is.Not.SameAs(previous));
    }

    [Test]
    public void PostCommitFailureDoesNotRetryOrPublishReady()
    {
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationRequest request = new MapGenerationRequest(Guid.NewGuid(), 4, "production",
            MapRetryPolicy.BoundedDeterministicRetry, 3);
        int generationCalls = 0;
        int readyCount = 0;
        orchestrator.GameplayReady += _ => readyCount++;

        MapGenerationExecutionResult result = orchestrator.Execute(request,
            attempt => { generationCalls++; return Pending(attempt); },
            (attempt, pending) => Plan(attempt, pending.Map),
            _ => throw new InvalidOperationException("Projection broke."));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure.Code, Is.EqualTo("ProjectionFailed"));
        Assert.That(generationCalls, Is.EqualTo(1));
        Assert.That(context.ActiveGenerationId, Is.Not.Null);
        Assert.That(context.AttemptChain, Has.Count.EqualTo(1));
        Assert.That(context.AttemptChain[0].Outcome, Is.EqualTo(MapAttemptOutcome.PostCommitFailure));
        Assert.That(readyCount, Is.Zero);
    }

    [Test]
    public void StaleGenerationResultFailsExplicitly()
    {
        MapRuntimeContext context = InitializedContext();
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);
        MapGenerationExecutionResult result = orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), 5),
            attempt => new MapPendingGeneration(Guid.NewGuid(), CreateMap(), new MapSimpleDecorationData()),
            (attempt, pending) => Plan(attempt, pending.Map),
            _ => { });

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(MapFailureCategory.StaleResult));
        Assert.That(result.Failure.Code, Is.EqualTo("StaleGenerationResult"));
        Assert.That(context.PendingGenerationId, Is.Null);
        Assert.That(context.ActiveGenerationId, Is.Null);
    }

    [Test]
    public void IllegalPhaseTransitionIsRejected()
    {
        Assert.That(MapLifecycleTransitions.IsLegal(MapLifecyclePhase.Initialized, MapLifecyclePhase.Committing), Is.False);
        Assert.That(MapLifecycleTransitions.IsLegal(MapLifecyclePhase.Planning, MapLifecyclePhase.Planned), Is.True);
        MapRuntimeContext context = InitializedContext();
        SetContextPhase(context, MapLifecyclePhase.Committing);
        MapIntegrationOrchestrator orchestrator = new MapIntegrationOrchestrator(context);

        MapGenerationExecutionResult result = orchestrator.Execute(
            new MapGenerationRequest(Guid.NewGuid(), 6), Pending,
            (attempt, pending) => Plan(attempt, pending.Map), _ => { });

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(MapFailureCategory.Lifecycle));
        Assert.That(result.Failure.Code, Is.EqualTo("IllegalPhaseTransition"));
    }

    [Test]
    public void PlacementAndRegistryOccupancyAreConflictSafeAndIdempotent()
    {
        Guid generationId = Guid.NewGuid();
        MapPlacementPlan plan = new MapPlacementPlan(generationId);
        Assert.That(plan.TryAdd(Reservation("first", new Vector2Int(1, 1)), out _), Is.True);
        Assert.That(plan.TryAdd(Reservation("second", new Vector2Int(1, 1)), out string conflict), Is.False);
        Assert.That(conflict, Does.Contain("already reserved"));

        MapObjectRegistry registry = new MapObjectRegistry(generationId);
        Assert.That(registry.TryRegister(generationId, "plant.1", "Plant", null, new Vector2Int(2, 2),
            MapPlacementOwnership.GenerationScoped, new[] { new Vector2Int(2, 2) }, out _), Is.True);
        Assert.That(registry.TryRegister(generationId, "plant.1", "Plant", null, new Vector2Int(2, 2),
            MapPlacementOwnership.GenerationScoped, new[] { new Vector2Int(2, 2) }, out _), Is.False);
        Assert.That(registry.Unregister(generationId, "plant.1"), Is.True);
        Assert.That(registry.Unregister(generationId, "plant.1"), Is.False);
        Assert.That(registry.IsOccupied(new Vector2Int(2, 2)), Is.False);
        Assert.That(registry.Retire(generationId), Is.True);
        Assert.That(registry.Retire(generationId), Is.False);
    }

    [Test]
    public void Stage8AttemptChainsAreCompleteForSuccessExhaustionAndNonRetryableFailure()
    {
        MapGenerationRequest retryThenSuccessRequest = new MapGenerationRequest(
            Guid.NewGuid(),
            -91,
            "stage8-attempt-chain-v1",
            MapRetryPolicy.BoundedDeterministicRetry,
            4,
            2);
        MapRuntimeContext retryThenSuccessContext = InitializedContext();
        MapIntegrationOrchestrator retryThenSuccess =
            new MapIntegrationOrchestrator(retryThenSuccessContext);
        int successPlanningCalls = 0;
        MapGenerationExecutionResult succeeded = retryThenSuccess.Execute(
            retryThenSuccessRequest,
            Pending,
            (attempt, pending) =>
            {
                successPlanningCalls++;
                if (attempt.AttemptIndex < 2)
                {
                    throw new MapPlanningException(
                        "CandidateExhaustion",
                        "Stage 8 deterministic capacity rejection.",
                        true);
                }
                return Plan(attempt, pending.Map);
            },
            _ => { });

        Assert.That(succeeded.Succeeded, Is.True);
        Assert.That(successPlanningCalls, Is.EqualTo(3));
        Assert.That(retryThenSuccessContext.AttemptChain, Has.Count.EqualTo(3));
        Assert.That(retryThenSuccessContext.AttemptChain[0].IsRetryable, Is.True);
        Assert.That(retryThenSuccessContext.AttemptChain[1].IsRetryable, Is.True);
        Assert.That(retryThenSuccessContext.AttemptChain[2].Outcome,
            Is.EqualTo(MapAttemptOutcome.Succeeded));
        AssertUniqueAttemptGenerationIds(retryThenSuccessContext.AttemptChain);
        WriteAttemptChainEvidence(
            "RetryThenSuccess",
            retryThenSuccessRequest,
            retryThenSuccessContext,
            succeeded);

        MapGenerationRequest exhaustedRequest = new MapGenerationRequest(
            Guid.NewGuid(),
            711,
            "stage8-attempt-chain-v1",
            MapRetryPolicy.BoundedDeterministicRetry,
            3,
            2);
        MapRuntimeContext exhaustedContext = InitializedContext();
        MapIntegrationOrchestrator exhausted = new MapIntegrationOrchestrator(exhaustedContext);
        int exhaustionPlanningCalls = 0;
        MapGenerationExecutionResult exhaustedResult = exhausted.Execute(
            exhaustedRequest,
            Pending,
            (attempt, pending) =>
            {
                exhaustionPlanningCalls++;
                throw new MapPlanningException(
                    "InsufficientEligibleArea",
                    "Stage 8 deterministic budget exhaustion.",
                    true);
            },
            _ => Assert.Fail("An exhausted request must not project."));

        Assert.That(exhaustedResult.Succeeded, Is.False);
        Assert.That(exhaustedResult.Failure.Code, Is.EqualTo("RetryBudgetExhausted"));
        Assert.That(exhaustionPlanningCalls, Is.EqualTo(3));
        Assert.That(exhaustedContext.AttemptChain, Has.Count.EqualTo(3));
        Assert.That(exhaustedContext.ActiveGenerationId, Is.Null);
        Assert.That(exhaustedContext.PendingGenerationId, Is.Null);
        AssertUniqueAttemptGenerationIds(exhaustedContext.AttemptChain);
        WriteAttemptChainEvidence(
            "RetryBudgetExhausted",
            exhaustedRequest,
            exhaustedContext,
            exhaustedResult);

        MapGenerationRequest nonRetryableRequest = new MapGenerationRequest(
            Guid.NewGuid(),
            8128,
            "stage8-attempt-chain-v1",
            MapRetryPolicy.BoundedDeterministicRetry,
            4,
            2);
        MapRuntimeContext nonRetryableContext = InitializedContext();
        MapIntegrationOrchestrator nonRetryable =
            new MapIntegrationOrchestrator(nonRetryableContext);
        int nonRetryablePlanningCalls = 0;
        MapGenerationExecutionResult nonRetryableResult = nonRetryable.Execute(
            nonRetryableRequest,
            Pending,
            (attempt, pending) =>
            {
                nonRetryablePlanningCalls++;
                throw new MapPlanningException(
                    "MissingRequiredConfiguration",
                    "Stage 8 structural failures terminate immediately.",
                    false);
            },
            _ => Assert.Fail("A non-retryable request must not project."));

        Assert.That(nonRetryableResult.Succeeded, Is.False);
        Assert.That(nonRetryableResult.Failure.Code,
            Is.EqualTo("MissingRequiredConfiguration"));
        Assert.That(nonRetryablePlanningCalls, Is.EqualTo(1));
        Assert.That(nonRetryableContext.AttemptChain, Has.Count.EqualTo(1));
        Assert.That(nonRetryableContext.AttemptChain[0].Outcome,
            Is.EqualTo(MapAttemptOutcome.NonRetryableFailure));
        Assert.That(nonRetryableContext.ActiveGenerationId, Is.Null);
        Assert.That(nonRetryableContext.PendingGenerationId, Is.Null);
        WriteAttemptChainEvidence(
            "NonRetryableStopsImmediately",
            nonRetryableRequest,
            nonRetryableContext,
            nonRetryableResult);
    }

    private static void AssertUniqueAttemptGenerationIds(
        IReadOnlyList<MapGenerationAttemptDiagnostic> diagnostics)
    {
        HashSet<Guid> generationIds = new HashSet<Guid>();
        foreach (MapGenerationAttemptDiagnostic diagnostic in diagnostics)
            Assert.That(generationIds.Add(diagnostic.Attempt.GenerationId), Is.True);
    }

    private static void WriteAttemptChainEvidence(
        string scenario,
        MapGenerationRequest request,
        MapRuntimeContext context,
        MapGenerationExecutionResult result)
    {
        foreach (MapGenerationAttemptDiagnostic diagnostic in context.AttemptChain)
        {
            AttemptChainEvidence evidence = new AttemptChainEvidence
            {
                scenario = scenario,
                requestId = request.RequestId.ToString("D"),
                requestedSeed = request.RequestedSeed,
                retryPolicy = request.RetryPolicy.ToString(),
                maxAttempts = request.MaxAttempts,
                attemptId = diagnostic.Attempt.AttemptIndex,
                attemptSeed = diagnostic.Attempt.AttemptSeed,
                generationId = diagnostic.Attempt.GenerationId.ToString("D"),
                mode = context.Mode.ToString(),
                phase = diagnostic.Phase.ToString(),
                outcome = diagnostic.Outcome.ToString(),
                failureCode = diagnostic.Code,
                finalResult = result.Succeeded ? "PASS" : "FAIL",
                finalFailureCode = result.Failure?.Code ?? string.Empty
            };
            string line = "STAGE8_ATTEMPT_EVIDENCE|" + JsonUtility.ToJson(evidence);
            Debug.Log(line);
            TestContext.Progress.WriteLine(line);
        }
    }

    private static MapGenerationExecutionResult ExecuteSuccess(MapIntegrationOrchestrator orchestrator, MapGenerationRequest request)
    {
        return orchestrator.Execute(request, Pending,
            (attempt, pending) => Plan(attempt, pending.Map), _ => { });
    }

    private static MapPendingGeneration Pending(MapGenerationAttempt attempt) =>
        new MapPendingGeneration(attempt.GenerationId, CreateMap(), new MapSimpleDecorationData());

    private static MapPlacementPlan Plan(MapGenerationAttempt attempt, MapData map)
    {
        MapPlacementPlan plan = new MapPlacementPlan(attempt.GenerationId);
        Assert.That(plan.TryAdd(Reservation("spawn", map.SpawnCell), out _), Is.True);
        return plan;
    }

    private static MapPlacementReservation Reservation(string id, Vector2Int cell) =>
        new MapPlacementReservation(id, "Test", cell, new[] { cell }, MapPlacementOwnership.StaticConstraint);

    private static MapData CreateMap()
    {
        MapData map = new MapData(8, 8, new Vector2Int(10, -4));
        map.SpawnCell = new Vector2Int(12, -2);
        map.ExitCell = new Vector2Int(16, 2);
        return map;
    }

    private static MapRuntimeContext InitializedContext()
    {
        MapRuntimeContext context = new MapRuntimeContext(MapRuntimeMode.RandomGenerated);
        SetContextPhase(context, MapLifecyclePhase.Initialized);
        return context;
    }

    private static void SetContextPhase(MapRuntimeContext context, MapLifecyclePhase phase)
    {
        typeof(MapRuntimeContext).GetMethod("SetPhase",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(context, new object[] { phase });
    }
}
