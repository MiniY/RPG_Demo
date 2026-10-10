using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Reinitializes explicitly registered map-dependent components after final placement.</summary>
[DisallowMultipleComponent]
public sealed class MapDependentReinitializationService : MonoBehaviour
{
    public Guid? ReadyGenerationId { get; private set; }
    public int ReinitializationCount { get; private set; }
    public int LastReinitializedObjectCount { get; private set; }

    public bool TryReinitialize(
        MapRuntimeContext context,
        Guid generationId,
        IReadOnlyList<GameObject> restartTargets,
        out string reason)
    {
        reason = string.Empty;
        if (context == null)
            return Fail(null, generationId, MapFailureCategory.Configuration,
                "MissingRuntimeContext", "Map-dependent reinitialization requires the runtime context.", out reason);
        if (context.Mode != MapRuntimeMode.RandomGenerated)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "WrongRuntimeMode", "Map-dependent reinitialization only runs in RandomGenerated mode.", out reason);
        if (context.ActiveGenerationId != generationId || context.ActiveMap == null)
            return Fail(context, generationId, MapFailureCategory.StaleResult,
                "StaleReinitialization", "Map-dependent reinitialization rejected a stale Generation ID.", out reason);
        if (context.Phase != MapLifecyclePhase.SpawningPlayer)
            return Fail(context, generationId, MapFailureCategory.Lifecycle,
                "IllegalReinitializationPhase",
                $"Map-dependent reinitialization requires SpawningPlayer; current phase is {context.Phase}.", out reason);

        MapLifecycleTransitions.Advance(context, MapLifecyclePhase.Reinitializing);
        if (restartTargets == null)
            return Fail(context, generationId, MapFailureCategory.Configuration,
                "MissingReinitializationTargets", "The explicit reinitialization target list is missing.", out reason);

        HashSet<GameObject> visited = new HashSet<GameObject>();
        int reinitializedCount = 0;
        foreach (GameObject target in restartTargets)
        {
            if (target == null)
                return Fail(context, generationId, MapFailureCategory.Configuration,
                    "MissingReinitializationTarget", "A registered map-dependent reinitialization target is missing.", out reason);
            if (!visited.Add(target))
                return Fail(context, generationId, MapFailureCategory.Configuration,
                    "DuplicateReinitializationTarget", $"{target.name} is registered for reinitialization more than once.", out reason);
            if (!target.activeSelf || !target.activeInHierarchy)
                return Fail(context, generationId, MapFailureCategory.Generation,
                    "InactiveReinitializationTarget", $"{target.name} must be active for map-dependent reinitialization.", out reason);

            int componentCount = 0;
            foreach (MonoBehaviour component in target.GetComponents<MonoBehaviour>())
            {
                if (!(component is IMapDependentReinitializable reinitializable))
                    continue;

                try
                {
                    reinitializable.ReinitializeForMap();
                }
                catch (Exception exception)
                {
                    return Fail(context, generationId, MapFailureCategory.Generation,
                        "MapDependentReinitializationFailed", exception.Message, out reason);
                }
                componentCount++;
            }
            if (componentCount == 0)
                return Fail(context, generationId, MapFailureCategory.Configuration,
                    "MissingReinitializationContract",
                    $"{target.name} has no IMapDependentReinitializable component.", out reason);
            reinitializedCount++;
        }

        ReadyGenerationId = generationId;
        LastReinitializedObjectCount = reinitializedCount;
        ReinitializationCount++;
        return true;
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
                context.Mode, generationId, context.Phase, category, code, message));
        }
        return false;
    }
}

public interface IMapDependentReinitializable
{
    void ReinitializeForMap();
}
