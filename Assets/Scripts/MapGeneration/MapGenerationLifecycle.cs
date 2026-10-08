using System;
using System.Collections.Generic;

public enum MapRetryPolicy { StrictSeed = 0, BoundedDeterministicRetry = 1 }
public enum MapAttemptOutcome { Succeeded = 0, RetryablePlanningRejection = 1, NonRetryableFailure = 2, PostCommitFailure = 3 }

public sealed class MapGenerationRequest
{
    public MapGenerationRequest(Guid requestId, int requestedSeed, string profileId = "default",
        MapRetryPolicy retryPolicy = MapRetryPolicy.StrictSeed, int maxAttempts = 1, int retryPolicyVersion = 1)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile ID cannot be empty.", nameof(profileId));
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        if (retryPolicyVersion < 1) throw new ArgumentOutOfRangeException(nameof(retryPolicyVersion));
        if (retryPolicy == MapRetryPolicy.StrictSeed && maxAttempts != 1)
            throw new ArgumentException("StrictSeed requires one attempt.", nameof(maxAttempts));
        RequestId = requestId; RequestedSeed = requestedSeed; ProfileId = profileId;
        RetryPolicy = retryPolicy; MaxAttempts = maxAttempts; RetryPolicyVersion = retryPolicyVersion;
    }
    public Guid RequestId { get; }
    public int RequestedSeed { get; }
    public string ProfileId { get; }
    public MapRetryPolicy RetryPolicy { get; }
    public int MaxAttempts { get; }
    public int RetryPolicyVersion { get; }
    public MapGenerationAttempt CreateAttempt(int attemptIndex, Guid generationId)
    {
        if (attemptIndex < 0 || attemptIndex >= MaxAttempts) throw new ArgumentOutOfRangeException(nameof(attemptIndex));
        return new MapGenerationAttempt(RequestId, attemptIndex, MapAttemptSeedDeriver.Derive(this, attemptIndex), generationId);
    }
}

public sealed class MapGenerationAttempt
{
    internal MapGenerationAttempt(Guid requestId, int attemptIndex, int attemptSeed, Guid generationId)
    {
        if (generationId == Guid.Empty) throw new ArgumentException("Generation ID cannot be empty.", nameof(generationId));
        RequestId = requestId; AttemptIndex = attemptIndex; AttemptSeed = attemptSeed; GenerationId = generationId;
    }
    public Guid RequestId { get; }
    public int AttemptIndex { get; }
    public int AttemptSeed { get; }
    public Guid GenerationId { get; }
}

public sealed class MapGenerationAttemptDiagnostic
{
    public MapGenerationAttemptDiagnostic(MapGenerationAttempt attempt, MapAttemptOutcome outcome,
        MapLifecyclePhase phase, MapFailureCategory failureCategory, string code, string reason)
    {
        Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt)); Outcome = outcome;
        Phase = phase; FailureCategory = failureCategory; Code = code ?? string.Empty; Reason = reason ?? string.Empty;
    }
    public MapGenerationAttempt Attempt { get; }
    public MapAttemptOutcome Outcome { get; }
    public MapLifecyclePhase Phase { get; }
    public MapFailureCategory FailureCategory { get; }
    public string Code { get; }
    public string Reason { get; }
    public bool IsRetryable => Outcome == MapAttemptOutcome.RetryablePlanningRejection;
}

public static class MapAttemptSeedDeriver
{
    public static int Derive(MapGenerationRequest request, int attemptIndex)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (attemptIndex < 0) throw new ArgumentOutOfRangeException(nameof(attemptIndex));
        if (attemptIndex == 0) return request.RequestedSeed;
        unchecked
        {
            uint hash = 2166136261;
            Mix(ref hash, request.RequestedSeed); Mix(ref hash, request.RetryPolicyVersion); Mix(ref hash, attemptIndex);
            foreach (char character in request.ProfileId) Mix(ref hash, character);
            return (int)hash;
        }
    }
    private static void Mix(ref uint hash, int value) { hash ^= (uint)value; hash *= 16777619; }
}

public static class MapLifecycleTransitions
{
    private static readonly Dictionary<MapLifecyclePhase, MapLifecyclePhase[]> Allowed =
        new Dictionary<MapLifecyclePhase, MapLifecyclePhase[]>
        {
            { MapLifecyclePhase.Initialized, new[] { MapLifecyclePhase.Preparing } },
            { MapLifecyclePhase.Materializing, new[] { MapLifecyclePhase.Preparing, MapLifecyclePhase.SpawningPlayer, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Ready, new[] { MapLifecyclePhase.Preparing, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Failed, new[] { MapLifecyclePhase.Preparing } },
            { MapLifecyclePhase.Preparing, new[] { MapLifecyclePhase.Generating, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Generating, new[] { MapLifecyclePhase.Planning, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Planning, new[] { MapLifecyclePhase.Planned, MapLifecyclePhase.Preparing, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Planned, new[] { MapLifecyclePhase.Committing, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Committing, new[] { MapLifecyclePhase.Committed, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Committed, new[] { MapLifecyclePhase.Projecting, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Projecting, new[] { MapLifecyclePhase.Materializing, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.SpawningPlayer, new[] { MapLifecyclePhase.Preparing, MapLifecyclePhase.Reinitializing, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.Reinitializing, new[] { MapLifecyclePhase.BindingCamera, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.BindingCamera, new[] { MapLifecyclePhase.Preparing, MapLifecyclePhase.ValidatingRuntime, MapLifecyclePhase.Failed } },
            { MapLifecyclePhase.ValidatingRuntime, new[] { MapLifecyclePhase.Ready, MapLifecyclePhase.Failed } }
        };
    public static bool IsLegal(MapLifecyclePhase current, MapLifecyclePhase next) =>
        Allowed.TryGetValue(current, out MapLifecyclePhase[] candidates) && Array.IndexOf(candidates, next) >= 0;
    internal static void Advance(MapRuntimeContext context, MapLifecyclePhase next)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (!IsLegal(context.Phase, next)) throw new InvalidOperationException($"Illegal lifecycle transition: {context.Phase} -> {next}.");
        context.SetPhase(next);
    }
}
