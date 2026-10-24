using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>The existing session owner supplies real lifecycle and canonical-player access.</summary>
public interface IEcologyMapSession
{
    event Action StateChanged;
    bool IsEcologyPlaying { get; }
    bool IsEcologyPreparing { get; }
    float ExitInteractionRadius { get; }
    Collider2D PlayerMovementFoot { get; }
    void BindEcologyPlayer(EcologyDamageBarrier barrier, long generation, Func<bool> currentScope);
    void ReleaseRetiredEcologyPlayer(EcologyDamageBarrier barrier);
}

/// <summary>One committed-map adapter; derives rules from the original plan and registry.</summary>
public sealed class EcologyMapRuntime : IDisposable
{
    private static long nextCombatGeneration;
    private readonly MapRuntimeContext context;
    private readonly EcologyPlanningAttemptResult accepted;
    private readonly EcologyEcotypeDefinition[] types;
    private readonly IEcologyMapSession session;
    private readonly GameObject holder;
    private bool played, disposed;
    private EcologyLiveNavigation navigation;
    public EcologyMapRuntime(MapRuntimeContext context, EcologyPlanningAttemptResult accepted,
        EcologyEcotypeDefinition[] types, IEcologyMapSession session, Transform parent)
    {
        if (context == null || accepted == null || !accepted.Succeeded || context.ActiveGenerationId != accepted.Plan.GenerationId ||
            context.ActivePlacementPlan != accepted.Plan || types == null || session == null)
            throw new ArgumentException("Committed original plan and session owner are required.");
        this.context = context; this.accepted = accepted; this.types = types; this.session = session;
        CombatGeneration = checked(++nextCombatGeneration);
        holder = new GameObject("Ecology scope " + accepted.Plan.GenerationId.ToString("N")); holder.transform.SetParent(parent, false);
        Barrier = holder.AddComponent<EcologyDamageBarrier>();
        var protection = new List<Rect>();
        // Use the same grid boundary as candidate geometry, including scaled rectangular grids.
        MapCoordinateBoundary coordinates = parent.GetComponent<MapGenerationController>().TilemapRenderer.Coordinates;
        foreach (MapEcologyRegion region in accepted.Plan.Ecology.Regions.Where(r => r.IsSafety))
            foreach (int node in region.Nodes.Concat(region.BufferNodes).Distinct())
            {
                Vector2Int cell = accepted.Plan.Ecology.CellAt(node);
                var grid = coordinates.Tilemap;
                Vector3 a = grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0));
                Vector3 b = grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0));
                protection.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
        Barrier.ConfigureProtection(protection.ToArray());
        Barrier.BindLifetime(IsCurrent, h => session.StateChanged += h, h => session.StateChanged -= h);
    }
    public EcologyDamageBarrier Barrier { get; }
    public long CombatGeneration { get; }
    public long StaticVersion => navigation?.Version ?? 0;
    public bool IsCurrent()
    {
        if (disposed || context.IsFailed || context.ActiveGenerationId != accepted.Plan.GenerationId) return false;
        if (session.IsEcologyPlaying) played = true;
        else if (played || !session.IsEcologyPreparing) return false;
        return true;
    }
    public bool CanAct() => IsCurrent() && Barrier.IsScopeOpen && context.IsReady && session.IsEcologyPlaying;
    public void ConfigurePopulation(MapEcologyPopulationService population) => population.ConfigureZonedActivation(
        accepted, types, Barrier, CombatGeneration, () => IsCurrent() && Barrier.IsScopeOpen, CanAct);

    public void FinalizeBeforeReady()
    {
        if (!IsCurrent() || context.Phase != MapLifecyclePhase.ValidatingRuntime) throw new InvalidOperationException("Stale runtime finalization.");
        session.BindEcologyPlayer(Barrier, CombatGeneration, () => IsCurrent() && Barrier.IsScopeOpen);
        var sources = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var residents = new List<EcologyResidentBinding>();
        foreach (MapPlacementReservation reservation in accepted.Plan.Reservations)
        {
            bool solid = reservation.Role == MerchantPlacementService.MerchantRole || reservation.Role == PlantPlacementService.PlantRole ||
                reservation.Role == DestructiblePlacementService.DestructibleRole;
            bool resident = reservation.Role == MonsterPlacementService.MonsterRole;
            if (!solid && !resident) continue;
            if (!context.ActiveRegistry.TryGet(reservation.LogicalObjectId, out MapObjectRegistryEntry entry) ||
                !(entry.Instance is Component component) || !component.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Accepted role lacks its actual active original registry instance: " + reservation.LogicalObjectId);
            if (solid) sources.Add(reservation.LogicalObjectId, component.transform);
            if (resident)
            {
                var binding = component.GetComponent<EcologyResidentBinding>();
                var motion = component.GetComponent<EcologyMotionBody>();
                if (binding == null || binding.GenerationId != accepted.Plan.GenerationId || binding.ResidentId != reservation.LogicalObjectId ||
                    motion == null || !motion.IsReady || component.GetComponent<EcologyCombatBinding>().MotionFaultRevision != 0)
                    throw new InvalidOperationException("Resident native activation or exact identity failed.");
                residents.Add(binding);
            }
        }
        navigation = new EcologyLiveNavigation(accepted, types, sources, residents, () => IsCurrent() && Barrier.IsScopeOpen);
        if (navigation.Version != 0)
        {
            var potential = new List<MapPlannedSolidGeometry>();
            foreach (var source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                MapPlacementReservation reservation = accepted.Plan.Reservations.Single(r => r.LogicalObjectId == source.Key);
                MapPlannedSolidGeometry expected = MapPlannedSolidGeometry.Capture(source.Key, source.Value,
                    accepted.StaticGeometry.NodeWorldPosition(accepted.Plan.Ecology.NodeAt(reservation.AnchorCell)), source.Value.rotation);
                MapPlannedSolidGeometry live = MapPlannedSolidGeometry.CaptureLive(source.Key, source.Value);
                potential.Add(expected);
                string expectedId = accepted.TerrainGeometry.WithPlannedSolids(new[] { expected }).PhysicalContentIdentity;
                string liveId = accepted.TerrainGeometry.WithPlannedSolids(new[] { live }).PhysicalContentIdentity;
                if (expectedId != liveId)
                {
                    Vector2 planned = accepted.StaticGeometry.NodeWorldPosition(accepted.Plan.Ecology.NodeAt(reservation.AnchorCell));
                    Vector3 actual = source.Value.position;
                    Debug.LogWarning($"ECOLOGY_STATIC_FLOAT id={source.Key} plannedX={planned.x:R} plannedY={planned.y:R} actualX={actual.x:R} actualY={actual.y:R}");
                    for (int i = 0; i < expected.ShapeCount; i++)
                    {
                        var a = expected.ShapeAt(i); var b = live.ShapeAt(i);
                        if (a.Points != null && b.Points != null)
                            for (int j = 0; j < a.Points.Length; j++)
                                Debug.LogWarning($"ECOLOGY_STATIC_VERTEX id={source.Key} shape={i} point={j} ax={a.Points[j].x:R} ay={a.Points[j].y:R} bx={b.Points[j].x:R} by={b.Points[j].y:R}");
                    }
                }
                Debug.LogWarning($"ECOLOGY_STATIC_MISMATCH id={source.Key} source={source.Value.name} position={source.Value.position.ToString("F7")} " +
                    $"planned={accepted.StaticGeometry.NodeWorldPosition(accepted.Plan.Ecology.NodeAt(reservation.AnchorCell)).ToString("F7")} " +
                    $"potentialShapes={expected.ShapeCount} liveShapes={live.ShapeCount} equal={expectedId == liveId} expected={expectedId} live={liveId}");
            }
            Debug.LogWarning("ECOLOGY_STATIC_SET accepted=" + accepted.StaticGeometry.PhysicalContentIdentity +
                " currentPotential=" + accepted.TerrainGeometry.WithPlannedSolids(potential).PhysicalContentIdentity + " live=" + navigation.Cache.Geometry.PhysicalContentIdentity);
            throw new InvalidOperationException("Actual materialized static geometry differs from the accepted layout.");
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            navigation?.Dispose();
            if (Barrier != null) { Barrier.enabled = false; session.ReleaseRetiredEcologyPlayer(Barrier); }
        }
        finally
        {
            if (holder != null) UnityEngine.Object.Destroy(holder);
        }
    }
}
