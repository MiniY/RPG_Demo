using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Retains the original rolled reward; immutable terrain failure is not retried every frame.</summary>
public sealed class MapSpatialPendingRewards : MonoBehaviour
{
    private sealed class Work
    {
        internal Func<bool> Publish;
        internal MapCandidateCollisionGeometry LastGeometry;
        internal int Attempts = 1;
    }
    private readonly Queue<Work> pending = new Queue<Work>();
    private MapRuntimeContext context; private Guid generation;
    public int PendingCount => pending.Count;
    public int RetiredUnclaimedCount { get; private set; }
    public int RevisionRetryCount { get; private set; }
    public void Enqueue(MapRuntimeContext owner, Guid expectedGeneration, Func<bool> tryPublish)
    {
        if (owner == null || owner.ActiveGenerationId != expectedGeneration || owner.ActiveSpatialContext == null || tryPublish == null)
            throw new InvalidOperationException("InvalidPendingRewardGeneration");
        if (pending.Count > 0 && (context != owner || generation != expectedGeneration)) Retire();
        context = owner; generation = expectedGeneration;
        pending.Enqueue(new Work { Publish = tryPublish, LastGeometry = owner.ActiveSpatialGeometry });
        Debug.LogWarning("VNext reward retained with original quantity; no legal bounded position. Suspended until spatial geometry changes, at most two revision retries.");
    }
    private void Update()
    {
        if (pending.Count == 0) return;
        // Same lifetime as existing unclaimed ground rewards. The retirement is counted explicitly.
        if (context == null || context.ActiveGenerationId != generation) { Retire(); return; }
        if (!context.IsReady) return;
        int budget = Math.Min(2, pending.Count);
        for (int i = 0; i < budget; i++)
        {
            Work work = pending.Dequeue();
            bool changed = !ReferenceEquals(work.LastGeometry, context.ActiveSpatialGeometry);
            if (changed && work.Attempts < 3)
            {
                work.LastGeometry = context.ActiveSpatialGeometry; work.Attempts++; RevisionRetryCount++;
                if (work.Publish()) continue;
            }
            pending.Enqueue(work);
        }
    }
    private void Retire() { RetiredUnclaimedCount += pending.Count; pending.Clear(); }
}
