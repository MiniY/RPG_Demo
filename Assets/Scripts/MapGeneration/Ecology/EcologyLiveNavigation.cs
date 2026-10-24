using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Derived shared navigation for accepted static roles; owns neither spawning nor the registry.</summary>
public sealed class EcologyLiveNavigation : IDisposable
{
    private readonly EcologyStaticCollisionRevision revision;
    private readonly Dictionary<string, Transform> sources;
    private readonly Dictionary<string, EcologyEcotypeDefinition> types;
    private readonly EcologyResidentBinding[] residents;
    private readonly Func<bool> currentScope;
    private readonly List<Collider2D> colliders = new List<Collider2D>();
    private readonly Dictionary<int, SolidState> states = new Dictionary<int, SolidState>();
    private readonly HashSet<int> observed = new HashSet<int>();
    private bool disposed, synchronizing;
    public long SynchronizeCalls { get; private set; }
    public double SynchronizeMilliseconds { get; private set; }
    public long Refreshes { get; private set; }
    public long PhysicalChanges { get; private set; }

    public EcologyLiveNavigation(EcologyPlanningAttemptResult accepted, IEnumerable<EcologyEcotypeDefinition> types,
        IDictionary<string, Transform> staticSources, IEnumerable<EcologyResidentBinding> residents, Func<bool> currentScope)
    {
        revision = new EcologyStaticCollisionRevision(accepted);
        this.types = (types ?? throw new ArgumentNullException(nameof(types))).ToDictionary(t => t.TypeId, StringComparer.Ordinal);
        sources = new Dictionary<string, Transform>(staticSources ?? throw new ArgumentNullException(nameof(staticSources)), StringComparer.Ordinal);
        this.residents = (residents ?? throw new ArgumentNullException(nameof(residents))).ToArray();
        this.currentScope = currentScope ?? throw new ArgumentNullException(nameof(currentScope));
        Cache = new EcologyHomeNavigationCache(accepted.Plan.Ecology, accepted.StaticGeometry);
        Refresh(true);
        foreach (EcologyResidentBinding resident in this.residents) resident.AttachNavigationSynchronizer(Synchronize);
    }
    public long Version => revision.Version;
    public EcologyHomeNavigationCache Cache { get; private set; }
    public void Synchronize()
    {
        if (disposed || synchronizing) return;
        long started = System.Diagnostics.Stopwatch.GetTimestamp(); SynchronizeCalls++;
        synchronizing = true;
        try { if (currentScope()) Refresh(false); }
        catch (Exception error)
        {
            foreach (EcologyResidentBinding resident in residents)
                if (IsCurrentResident(resident)) resident.FailNavigation("StaticNavigationRefresh:" + error.GetType().Name);
        }
        finally
        {
            synchronizing = false;
            SynchronizeMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        }
    }
    private bool IsCurrentResident(EcologyResidentBinding resident) => resident != null && resident.isActiveAndEnabled &&
        resident.IsBound && resident.GenerationId == revision.GenerationId;

    private void Refresh(bool initial)
    {
        bool changed = initial;
        observed.Clear();
        foreach (Transform source in sources.Values)
        {
            if (source == null) continue;
            colliders.Clear(); source.GetComponentsInChildren(true, colliders);
            foreach (Collider2D collider in colliders)
            {
                int id = collider.GetInstanceID(); observed.Add(id);
                if (!states.TryGetValue(id, out SolidState previous) || !previous.Matches(collider))
                { states[id] = new SolidState(collider); changed = true; }
            }
        }
        if (states.Count != observed.Count)
        {
            foreach (int removed in states.Keys.Where(id => !observed.Contains(id)).ToArray()) states.Remove(removed);
            changed = true;
        }
        if (!changed) return;
        Refreshes++;
        var solids = new List<MapPlannedSolidGeometry>();
        foreach (var source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
            if (source.Value != null) solids.Add(MapPlannedSolidGeometry.CaptureLive(source.Key, source.Value));
        bool physicalChange = revision.UpdateLiveSolids(solids);
        if (physicalChange) PhysicalChanges++;
        if (!initial && !physicalChange) return;
        EcologyHomeNavigationCache next = Cache.WithStaticRevision(revision);
        // Build every affected shared field before publishing any of them.
        foreach (EcologyResidentBinding resident in residents)
        {
            if (!IsCurrentResident(resident)) continue;
            MapEcologyResident accepted = next.Layout.Residents.Single(r => r.Id == resident.ResidentId && r.TypeId == resident.EcotypeId);
            next.Get(accepted, types[accepted.TypeId]);
        }
        foreach (EcologyResidentBinding resident in residents) if (IsCurrentResident(resident)) resident.RebindNavigation(next);
        Cache = next;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (EcologyResidentBinding resident in residents) if (resident != null) resident.DetachNavigationSynchronizer(Synchronize);
    }

    private sealed class SolidState
    {
        private readonly Matrix4x4 pose;
        private readonly bool enabled, trigger, composite, effector, simulated;
        private readonly Vector2 offset, size;
        private readonly float radius;
        private readonly Vector2[][] paths;
        internal SolidState(Collider2D collider)
        {
            pose = collider.transform.localToWorldMatrix;
            enabled = collider.enabled && collider.gameObject.activeInHierarchy;
            trigger = collider.isTrigger; composite = collider.usedByComposite; effector = collider.usedByEffector;
            simulated = collider.attachedRigidbody == null || collider.attachedRigidbody.simulated;
            offset = collider.offset;
            if (collider is BoxCollider2D box) { size = box.size; radius = box.edgeRadius; }
            if (collider is CircleCollider2D circle) radius = circle.radius;
            if (collider is PolygonCollider2D polygon)
            {
                paths = new Vector2[polygon.pathCount][];
                for (int i = 0; i < paths.Length; i++) paths[i] = polygon.GetPath(i);
            }
        }
        internal bool Matches(Collider2D collider)
        {
            if (!pose.Equals(collider.transform.localToWorldMatrix) || enabled != (collider.enabled && collider.gameObject.activeInHierarchy) ||
                trigger != collider.isTrigger || composite != collider.usedByComposite || effector != collider.usedByEffector ||
                simulated != (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated) || !offset.Equals(collider.offset)) return false;
            if (collider is BoxCollider2D box && (!size.Equals(box.size) || radius != box.edgeRadius)) return false;
            if (collider is CircleCollider2D circle && radius != circle.radius) return false;
            if (collider is PolygonCollider2D polygon)
            {
                if (paths == null || paths.Length != polygon.pathCount) return false;
                for (int i = 0; i < paths.Length; i++) if (!paths[i].SequenceEqual(polygon.GetPath(i))) return false;
            }
            return true;
        }
    }
}
