using System;
using UnityEngine;

// Test-assembly-only fault at the current production finalizer boundary.
public sealed class ConsumablesFailReadyFinalizer : MonoBehaviour, IMapRuntimeReadyFinalizer
{
    public bool TryFinalizeBeforeReady(MapRuntimeContext context, Guid generationId, out string reason)
    {
        reason = "CONSUMABLES_POST_COMMIT_INJECTED";
        return false;
    }
}
