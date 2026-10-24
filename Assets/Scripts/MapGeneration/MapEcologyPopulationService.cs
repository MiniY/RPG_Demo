using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapEcologyPopulationPlan
{
    internal MapEcologyPopulationPlan(int requestedMonsters, int requestedDestructibles,
        IReadOnlyList<MapEcologyPlacementResult> placements)
    {
        RequestedMonsters = requestedMonsters;
        RequestedDestructibles = requestedDestructibles;
        Placements = placements;
    }

    public int RequestedMonsters { get; }
    public int RequestedDestructibles { get; }
    public IReadOnlyList<MapEcologyPlacementResult> Placements { get; }
    public int ActualMonsters => CountRole(MonsterPlacementService.MonsterRole);
    public int ActualDestructibles => CountRole(DestructiblePlacementService.DestructibleRole);

    private int CountRole(string role)
    {
        int count = 0;
        foreach (MapEcologyPlacementResult placement in Placements)
            if (placement.Role == role) count++;
        return count;
    }
}

/// <summary>Owns ecology-v1 Monster and Destructible instances within the existing map plan/registry.</summary>
[DisallowMultipleComponent]
public sealed class MapEcologyPopulationService : MonoBehaviour
{
    private sealed class ActiveInstance
    {
        public ActiveInstance(Guid generationId, string logicalId,
            IGenerationScopedPopulationTarget target,
            GenerationScopedPopulationLifetime lifetime, Action defeated)
        {
            GenerationId = generationId;
            LogicalId = logicalId;
            Target = target;
            Lifetime = lifetime;
            Defeated = defeated;
        }
        public Guid GenerationId { get; }
        public string LogicalId { get; }
        public IGenerationScopedPopulationTarget Target { get; }
        public GenerationScopedPopulationLifetime Lifetime { get; }
        public Action Defeated { get; }
    }

    [SerializeField] private MapEcologyProfile profile;

    private readonly List<MapEcologyPlacementResult> planned =
        new List<MapEcologyPlacementResult>();
    private readonly Dictionary<string, ActiveInstance> active =
        new Dictionary<string, ActiveInstance>(StringComparer.Ordinal);
    private readonly HashSet<GameObject> defeatedAwaitingPool = new HashSet<GameObject>();
    private readonly List<GameObject> monsterReinitializationTargets = new List<GameObject>();
    private MapRuntimeContext activeContext;
    private Guid? plannedGenerationId;
    private EcologyPopulationActivation zonedActivation;

    public MapEcologyProfile Profile => profile;
    public MapEcologyPopulationPlan LastPlan { get; private set; }
    public Guid? ReadyGenerationId { get; private set; }
    public int ActiveCount => active.Count;
    public IReadOnlyList<GameObject> MonsterReinitializationTargets =>
        monsterReinitializationTargets.AsReadOnly();

    public void ConfigureZonedActivation(EcologyPlanningAttemptResult accepted, EcologyEcotypeDefinition[] types,
        EcologyDamageBarrier barrier, long combatGenerationId, Func<bool> currentScope, Func<bool> gameplayReady)
    {
        if (accepted == null || accepted.Plan?.GenerationId != plannedGenerationId)
            throw new ArgumentException("Activation must use the current accepted original population plan.");
        zonedActivation = new EcologyPopulationActivation(accepted, types, barrier, combatGenerationId, currentScope, gameplayReady);
    }

    public MapEcologyPopulationPlan Plan(MapGenerationAttempt attempt,
        MapData map, MapPlacementPlan plan, string profileId = MapEcologyProfile.ProfileId)
    {
        if (attempt == null) throw new ArgumentNullException(nameof(attempt));
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.GenerationId != attempt.GenerationId)
            throw new MapPlanningException("StaleEcologyPlanning", "Generation ID mismatch.", false);
        if (profile == null)
            throw new MapPlanningException("EcologyProfileMissing", "Ecology profile is not assigned.", false);
        try { profile.Validate(); }
        catch (Exception exception)
        {
            throw new MapPlanningException("EcologyProfileInvalid", exception.Message, false);
        }

        planned.Clear();
        MapPopulationRegionQuery regions = MapPopulationRegionQuery.Build(map, plan);
        int requestedMonsters = PlanRole(
            attempt.AttemptSeed, map, plan, regions,
            MonsterPlacementService.MonsterRole, profile.Monster,
            profile.MonsterSpeciesFor(profileId),
            string.Equals(profileId, MapEcologyProfile.XiaoSuProfileId, StringComparison.Ordinal));
        int requestedDestructibles = PlanRole(
            attempt.AttemptSeed, map, plan, regions,
            DestructiblePlacementService.DestructibleRole, profile.Destructible,
            profile.DestructibleSpecies);

        LastPlan = new MapEcologyPopulationPlan(
            requestedMonsters, requestedDestructibles, planned.AsReadOnly());
        plannedGenerationId = attempt.GenerationId;
        return LastPlan;
    }

    public MapEcologyPopulationPlan PlanDestructibles(MapGenerationAttempt attempt,
        MapData map, MapPlacementPlan plan, int placementSeed, MapEcologyProfile retainedProfile = null)
    {
        if (attempt == null || map == null || plan == null || plan.GenerationId != attempt.GenerationId)
            throw new MapPlanningException("StaleEcologyPlanning", "Current pending generation required.", false);
        MapEcologyProfile selected = retainedProfile != null ? retainedProfile : profile;
        if (selected == null) throw new MapPlanningException("EcologyProfileMissing", "Ecology profile is not assigned.", false);
        try { selected.Validate(); }
        catch (Exception exception) { throw new MapPlanningException("EcologyProfileInvalid", exception.Message, false); }
        planned.Clear();
        int requested = PlanRole(placementSeed, map, plan, MapPopulationRegionQuery.Build(map, plan),
            DestructiblePlacementService.DestructibleRole, selected.Destructible, selected.DestructibleSpecies);
        LastPlan = new MapEcologyPopulationPlan(0, requested, planned.AsReadOnly());
        plannedGenerationId = attempt.GenerationId;
        return LastPlan;
    }

    public void AcceptZonedResidents(MapPlacementPlan plan, EcologyEcotypeDefinition[] types)
    {
        if (plan == null || plan.Ecology == null || plannedGenerationId != plan.GenerationId || LastPlan == null)
            throw new MapPlanningException("StaleEcologyPlanning", "Frozen accepted layout and matching original role plan required.", false);
        var catalog = new Dictionary<string, EcologyEcotypeDefinition>(StringComparer.Ordinal);
        foreach (EcologyEcotypeDefinition type in types) catalog.Add(type.TypeId, type);
        foreach (MapEcologyPlacementResult existing in planned)
            if (existing.Role == MonsterPlacementService.MonsterRole)
                throw new MapPlanningException("DuplicateZonedPopulation", "Zoned residents can only attach once.", false);
        foreach (MapEcologyResident resident in plan.Ecology.Residents)
        {
            if (!catalog.TryGetValue(resident.TypeId, out EcologyEcotypeDefinition type) ||
                !plan.Ecology.TryGetRegion(resident.HomeRegionId, out MapEcologyRegion region))
                throw new MapPlanningException("UnknownContent", "Accepted resident requires its exact retained prefab and home.", false);
            planned.Add(new MapEcologyPlacementResult(resident.Id, MonsterPlacementService.MonsterRole, resident.TypeId,
                type.Prefab, plan.Ecology.CellAt(resident.InitialNode), plan.Ecology.CellAt(region.Nodes[0]), 0));
        }
        LastPlan = new MapEcologyPopulationPlan(plan.Ecology.Residents.Count, LastPlan.RequestedDestructibles, planned.AsReadOnly());
    }

    private int PlanRole(int seed, MapData map, MapPlacementPlan plan,
        MapPopulationRegionQuery regions, string role, MapPopulationTuning tuning,
        IReadOnlyList<MapPopulationSpecies> species, bool weightedFromFirstSlot = false)
    {
        int requested = tuning.TargetForArea(regions.ReachableCount);
        int before = planned.Count;
        MapEcologyPlacementPolicy policy = new MapEcologyPlacementPolicy(
            seed, map, regions, plan, role, tuning, species, weightedFromFirstSlot);
        // Failed candidate slots get a finite chance to select another species/cluster.
        // The successful population still never exceeds the area-based target.
        int attemptLimit = requested + policy.EligibleSpeciesCount * 2;
        for (int index = 0; index < attemptLimit && planned.Count - before < requested; index++)
        {
            MapPopulationSpecies selected = policy.SelectSpecies(index);
            if (selected == null)
                break;
            string logicalId = "ecology." + role.ToLowerInvariant() + "." +
                               selected.TypeId + "." + (index + 1).ToString("D3");
            MapEcologyPlacementResult result = policy.SelectOptional(plan, selected, logicalId);
            if (result == null)
                continue;

            MapPlacementReservation reservation = new MapPlacementReservation(
                logicalId, role, result.Cell, new[] { result.Cell },
                MapPlacementOwnership.GenerationScoped);
            if (!plan.TryAdd(reservation, out string reason))
                throw new MapPlanningException("EcologyReservationConflict", reason, false);
            planned.Add(result);
        }

        int actual = planned.Count - before;
        if (actual < tuning.RequiredMinimum)
        {
            throw new MapPlanningException(
                "EcologyRequiredMinimumUnavailable",
                $"Role={role}; requested={requested}; actual={actual}; " +
                $"required={tuning.RequiredMinimum}; eligibleSpecies={policy.EligibleSpeciesCount}; " +
                $"reachableCells={regions.ReachableCount}; GenerationId={plan.GenerationId:D}.",
                true);
        }
        if (actual < requested)
            Debug.LogWarning($"Ecology {role} underfill: requested={requested}, actual={actual}, " +
                             $"GenerationId={plan.GenerationId:D}.", this);
        return requested;
    }

    public bool TryMaterialize(MapRuntimeContext context, Guid generationId,
        MapCoordinateBoundary coordinates, out string reason)
    {
        reason = string.Empty;
        if (context == null || context.Mode != MapRuntimeMode.RandomGenerated ||
            !context.IsCurrentActive(generationId) || context.ActiveMap == null ||
            context.ActiveRegistry == null || context.ActivePlacementPlan == null ||
            context.Phase != MapLifecyclePhase.Materializing ||
            plannedGenerationId != generationId || LastPlan == null ||
            ReadyGenerationId == generationId || coordinates == null)
        {
            return Fail(context, generationId, "EcologyMaterializationPrecondition",
                "Ecology materialization requires the current committed plan and coordinate boundary.",
                out reason);
        }

        EcologyPopulationActivation activation = zonedActivation;
        if (context.ActivePlacementPlan.Ecology != null && (activation == null || activation.GenerationId != generationId))
            return Fail(context, generationId, "ZonedPreparationMissing", "Zoned residents require the accepted inactive preparation transaction.", out reason);
        RetireActiveInstances();
        activeContext = context;
        zonedActivation = activation;
        if (context.ActivePlacementPlan.Ecology != null)
            return TryMaterializeZoned(context, generationId, coordinates, activation, out reason);
        foreach (MapEcologyPlacementResult placement in planned)
        {
            if (!context.ActiveRegistry.TryGet(placement.LogicalId, out MapObjectRegistryEntry entry) ||
                entry.Role != placement.Role ||
                entry.Ownership != MapPlacementOwnership.GenerationScoped ||
                entry.InitialCell != placement.Cell)
            {
                RetireActiveInstances();
                return Fail(context, generationId, "EcologyRegistryMismatch",
                    "Committed population reservation differs from the planned entry.", out reason);
            }

            IGenerationScopedPopulationTarget source =
                placement.Prefab.GetComponent<IGenerationScopedPopulationTarget>();
            if (source == null || source.PlacementInstance == null)
            {
                RetireActiveInstances();
                return Fail(context, generationId, "EcologyPrefabBridgeMissing",
                    placement.Prefab.name + " lacks a population bridge.", out reason);
            }

            Vector3 world;
            try { world = coordinates.CellToWorld(context.ActiveMap, placement.Cell); }
            catch (Exception exception)
            {
                RetireActiveInstances();
                return Fail(context, generationId, "EcologyCoordinateFailure",
                    exception.Message, out reason);
            }
            GameObject instance = source.Spawn(world, Quaternion.identity);
            IGenerationScopedPopulationTarget target = instance == null
                ? null : instance.GetComponent<IGenerationScopedPopulationTarget>();
            if (target == null || target.PlacementInstance == null ||
                !instance.activeInHierarchy)
            {
                if (instance != null) source.Return(instance);
                RetireActiveInstances();
                return Fail(context, generationId, "EcologySpawnFailure",
                    placement.LogicalId + " could not spawn an active Damageable.", out reason);
            }

            GenerationScopedPopulationLifetime lifetime =
                instance.GetComponent<GenerationScopedPopulationLifetime>();
            if (lifetime == null)
                lifetime = instance.AddComponent<GenerationScopedPopulationLifetime>();
            string logicalId = placement.LogicalId;
            Action defeated = () => Release(generationId, logicalId,
                target.PlacementInstance, true, out _);
            target.SubscribeDefeated(defeated);
            lifetime.Arm(() => TryRelease(generationId, logicalId,
                target.PlacementInstance, out _));

            if (!context.ActiveRegistry.TryBindInstance(
                    generationId, logicalId, placement.Role,
                    target.PlacementInstance, out string registryReason))
            {
                target.UnsubscribeDefeated(defeated);
                lifetime.Disarm();
                source.Return(instance);
                RetireActiveInstances();
                return Fail(context, generationId, "EcologyRegistryBindFailure",
                    registryReason, out reason);
            }

            active.Add(logicalId, new ActiveInstance(
                generationId, logicalId, target, lifetime, defeated));
            if (placement.Role == MonsterPlacementService.MonsterRole)
                monsterReinitializationTargets.Add(target.ReinitializationTarget);
        }

        Physics2D.SyncTransforms();
        ReadyGenerationId = generationId;
        return true;
    }

    public bool TryRelease(Guid generationId, string logicalId,
        UnityEngine.Object instance, out string reason) =>
        Release(generationId, logicalId, instance, false, out reason);

    private bool TryMaterializeZoned(MapRuntimeContext context, Guid generationId, MapCoordinateBoundary coordinates,
        EcologyPopulationActivation activation, out string reason)
    {
        reason = string.Empty;
        string preparingId = null; GameObject spawned = null;
        try
        {
            foreach (MapEcologyPlacementResult placement in planned)
            {
                preparingId = placement.LogicalId; spawned = null;
                if (!context.ActiveRegistry.TryGet(placement.LogicalId, out MapObjectRegistryEntry entry) ||
                    entry.Role != placement.Role || entry.Ownership != MapPlacementOwnership.GenerationScoped || entry.InitialCell != placement.Cell)
                    throw new InvalidOperationException("Committed zoned reservation differs from its original role plan.");
                var source = placement.Prefab.GetComponent<IGenerationScopedPreparedPopulationTarget>();
                if (source == null) throw new InvalidOperationException("Original source lacks prepared pooling: " + placement.LogicalId);
                spawned = source.SpawnPrepared(coordinates.CellToWorld(context.ActiveMap, placement.Cell), Quaternion.identity, actor =>
                {
                    var target = actor.GetComponent<IGenerationScopedPopulationTarget>();
                    if (target == null || target.PlacementInstance == null) throw new InvalidOperationException("Prepared actor bridge missing.");
                    if (placement.Role == MonsterPlacementService.MonsterRole) activation.Prepare(placement.LogicalId, actor);
                    var lifetime = actor.GetComponent<GenerationScopedPopulationLifetime>() ?? actor.AddComponent<GenerationScopedPopulationLifetime>();
                    Action defeated = () => Release(generationId, placement.LogicalId, target.PlacementInstance, true, out _);
                    if (!context.ActiveRegistry.TryBindInstance(generationId, placement.LogicalId, placement.Role, target.PlacementInstance, out string failure))
                        throw new InvalidOperationException(failure);
                    // Register cleanup before subscribing: even an inactive preparation failure has an explicit rollback owner.
                    active.Add(placement.LogicalId, new ActiveInstance(generationId, placement.LogicalId, target, lifetime, defeated));
                    target.SubscribeDefeated(defeated);
                    lifetime.Arm(() => TryRelease(generationId, placement.LogicalId, target.PlacementInstance, out _));
                    if (placement.Role == MonsterPlacementService.MonsterRole) monsterReinitializationTargets.Add(target.ReinitializationTarget);
                });
                if (spawned == null || !spawned.activeInHierarchy || !active.ContainsKey(placement.LogicalId))
                    throw new InvalidOperationException("Prepared actor lost its registered activation lease.");
                Physics2D.SyncTransforms();
                if (placement.Role == MonsterPlacementService.MonsterRole) activation.Complete(spawned);
            }
            ReadyGenerationId = generationId; return true;
        }
        catch (Exception error)
        {
            // SpawnPrepared already returns a failed preparation to its pool. Do not enqueue it twice.
            DetachPrepared(preparingId);
            if (spawned != null) spawned.GetComponent<IGenerationScopedPopulationTarget>()?.Return(spawned);
            foreach (string id in new List<string>(active.Keys))
            {
                GameObject actor = active[id].Target.PlacementGameObject;
                DetachPrepared(id); actor.GetComponent<IGenerationScopedPopulationTarget>()?.Return(actor);
            }
            ReadyGenerationId = null;
            return Fail(context, generationId, "ZonedPreparedActivationFailed", error.Message, out reason);
        }
    }
    private void DetachPrepared(string logicalId)
    {
        if (logicalId == null || !active.TryGetValue(logicalId, out ActiveInstance entry)) return;
        entry.Target.UnsubscribeDefeated(entry.Defeated); entry.Lifetime.Disarm();
        activeContext?.ActiveRegistry?.Unregister(entry.GenerationId, logicalId);
        active.Remove(logicalId); monsterReinitializationTargets.Remove(entry.Target.ReinitializationTarget);
    }

    private bool Release(Guid generationId, string logicalId,
        UnityEngine.Object instance, bool defeated, out string reason)
    {
        reason = string.Empty;
        if (activeContext == null || !activeContext.IsCurrentActive(generationId) ||
            activeContext.ActiveRegistry == null ||
            !active.TryGetValue(logicalId, out ActiveInstance entry) ||
            entry.GenerationId != generationId || entry.Target.PlacementInstance != instance)
        {
            reason = "Stale or duplicate population release.";
            return false;
        }
        if (!activeContext.ActiveRegistry.Unregister(generationId, logicalId))
        {
            reason = "Registry rejected population release.";
            return false;
        }
        entry.Target.UnsubscribeDefeated(entry.Defeated);
        if (defeated)
        {
            GameObject actor = entry.Target.PlacementGameObject;
            defeatedAwaitingPool.Add(actor);
            entry.Lifetime.Arm(() => defeatedAwaitingPool.Remove(actor));
        }
        else
            entry.Lifetime.Disarm();
        active.Remove(logicalId);
        monsterReinitializationTargets.Remove(entry.Target.ReinitializationTarget);
        return true;
    }

    public bool TryGetActiveInstance(string logicalId, out UnityEngine.Object instance)
    {
        if (active.TryGetValue(logicalId, out ActiveInstance entry))
        {
            instance = entry.Target.PlacementInstance;
            return true;
        }
        instance = null;
        return false;
    }

    public void RetireForLegacyProfile() { RetireActiveInstances(); zonedActivation = null; }

    private void OnDisable() => RetireActiveInstances(false);

    private void RetireActiveInstances(bool returnToPool = true)
    {
        foreach (ActiveInstance entry in active.Values)
        {
            entry.Target.UnsubscribeDefeated(entry.Defeated);
            entry.Lifetime.Disarm();
            if (returnToPool)
                entry.Target.Return(entry.Target.PlacementGameObject);
            else if (entry.Target.PlacementGameObject != null)
                entry.Target.PlacementGameObject.SetActive(false);
        }
        active.Clear();
        foreach (GameObject actor in new List<GameObject>(defeatedAwaitingPool))
            if (actor != null)
            {
                if (returnToPool)
                    actor.GetComponent<IGenerationScopedPopulationTarget>()?.Return(actor);
                else
                    actor.SetActive(false);
            }
        defeatedAwaitingPool.Clear();
        monsterReinitializationTargets.Clear();
        activeContext = null;
        ReadyGenerationId = null;
    }

    private static bool Fail(MapRuntimeContext context, Guid generationId,
        string code, string message, out string reason)
    {
        reason = message;
        if (context != null)
            context.RecordFailure(new MapFailureDiagnostic(
                context.Mode, generationId, context.Phase,
                MapFailureCategory.Generation, code, message));
        return false;
    }
}
