using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Preparation of residents borrowed from the original pool, before any activation.</summary>
public sealed class EcologyPopulationActivation
{
    private readonly Dictionary<string, EcologyEcotypeDefinition> types;
    private readonly Dictionary<string, long> combatIds;
    private readonly EcologyDamageBarrier barrier;
    private readonly long combatGenerationId;
    private readonly Func<bool> currentScope, gameplayReady;
    public EcologyPopulationActivation(EcologyPlanningAttemptResult accepted, EcologyEcotypeDefinition[] types,
        EcologyDamageBarrier barrier, long combatGenerationId, Func<bool> currentScope, Func<bool> gameplayReady)
    {
        if (accepted == null || !accepted.Succeeded || accepted.Plan.Ecology == null || types == null ||
            barrier == null || combatGenerationId <= 0 || currentScope == null || gameplayReady == null)
            throw new ArgumentException("An accepted layout, retained ecotypes and explicit combat scope are required.");
        Cache = new EcologyHomeNavigationCache(accepted.Plan.Ecology, accepted.StaticGeometry);
        this.types = types.ToDictionary(t => t.TypeId, StringComparer.Ordinal);
        combatIds = new Dictionary<string, long>(StringComparer.Ordinal);
        long identity = 1; // Identity 1 belongs to the canonical player in this barrier.
        foreach (MapEcologyResident resident in accepted.Plan.Ecology.Residents.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            Cache.Get(resident, this.types[resident.TypeId]); combatIds.Add(resident.Id, checked(++identity));
        }
        this.barrier = barrier; this.combatGenerationId = combatGenerationId;
        this.currentScope = currentScope; this.gameplayReady = gameplayReady;
    }
    public EcologyHomeNavigationCache Cache { get; }
    public Guid GenerationId => Cache.Layout.GenerationId;
    public void Prepare(string residentId, GameObject actor)
    {
        if (actor == null || actor.activeInHierarchy || !currentScope()) throw new InvalidOperationException("Stale or active preparation lease.");
        MapEcologyResident resident = Cache.Layout.Residents.Single(r => r.Id == residentId);
        EcologyEcotypeDefinition type = types[resident.TypeId];
        IEcologyResidentRuntime runtime = actor.GetComponent<IEcologyResidentRuntime>();
        if (runtime == null) throw new InvalidOperationException("Resident lacks its original runtime health/motion bridge.");
        var home = actor.GetComponent<EcologyResidentBinding>() ?? actor.AddComponent<EcologyResidentBinding>();
        var combat = actor.GetComponent<EcologyCombatBinding>() ?? actor.AddComponent<EcologyCombatBinding>();
        var motion = actor.GetComponent<EcologyMotionBody>() ?? actor.AddComponent<EcologyMotionBody>();
        home.Prepare(Cache, resident.Id, type, currentScope, gameplayReady);
        combat.Bind(barrier, combatGenerationId, combatIds[resident.Id], () => currentScope() && runtime.IsAlive);
        motion.ConfigurePrepared(type.ResolveMovementFoot(actor), runtime.MovementBlockingLayers, runtime.MovementCollisionSkin);
    }
    public void Complete(GameObject actor)
    {
        if (actor == null || !currentScope()) throw new InvalidOperationException("Stale activated resident.");
        actor.GetComponent<EcologyMotionBody>().CompletePreparedConfiguration();
    }
}
