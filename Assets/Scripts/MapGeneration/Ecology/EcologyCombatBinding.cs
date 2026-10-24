using System;
using UnityEngine;

/// <summary>Explicit opt-in identity for the P0.V combat experiment. Not added by legacy profiles.</summary>
[DisallowMultipleComponent]
public sealed class EcologyCombatBinding : MonoBehaviour, IPooledLegacyActivation
{
    private Func<bool> isAlive;

    public EcologyDamageBarrier Barrier { get; private set; }
    public long GenerationId { get; private set; }
    public long ResidentId { get; private set; }
    public long Lease { get; private set; }
    public long ProtectionRevision { get; private set; }
    public bool ProtectionNow { get; private set; }
    public long MotionFaultRevision { get; private set; }
    public bool IsLive => isActiveAndEnabled && Barrier != null && Barrier.IsScopeOpen && isAlive != null && isAlive();

    public void Bind(EcologyDamageBarrier barrier, long generationId, long residentId, Func<bool> alive)
    {
        if (barrier == null || alive == null || generationId <= 0 || residentId <= 0)
            throw new ArgumentException("Combat binding requires a barrier, positive identity and life predicate.");
        RetireLease();
        Barrier = barrier;
        GenerationId = generationId;
        ResidentId = residentId;
        isAlive = alive;
        ProtectionNow = false;
        ProtectionRevision = 0;
        MotionFaultRevision = 0;
    }

    // The eventual motion observer owns these events. A requested destination is never evidence of entry.
    public void ObserveProtection(bool protectedNow)
    {
        if (ProtectionNow == protectedNow)
            return;
        ProtectionNow = protectedNow;
        ProtectionRevision = checked(ProtectionRevision + 1);
    }

    public void RetireLease()
    {
        Lease = checked(Lease + 1);
        Barrier = null;
        isAlive = null;
    }

    public void PrepareForLegacyActivation()
    {
        if (gameObject.activeInHierarchy && Barrier != null) throw new InvalidOperationException("A bound active combat lease cannot become legacy.");
        RetireLease(); GenerationId = ResidentId = 0; ProtectionNow = false;
        ProtectionRevision = MotionFaultRevision = 0;
    }

    public void ReleaseRetiredScopeForLegacy(EcologyDamageBarrier expected)
    {
        if (expected == null || expected.IsScopeOpen || Barrier != null && Barrier != expected)
            throw new InvalidOperationException("Only the exact retired scope may release an active persistent player.");
        RetireLease(); GenerationId = ResidentId = 0; ProtectionNow = false;
        ProtectionRevision = MotionFaultRevision = 0;
    }

    internal void RejectMotionHistory()
    {
        MotionFaultRevision = checked(MotionFaultRevision + 1);
    }

    private void OnDisable()
    {
        RetireLease();
    }

    public static bool TryPrepare(Transform attacker, Transform target, out EcologyAttackRequest request)
    {
        request = null;
        EcologyCombatBinding source = attacker != null ? attacker.GetComponent<EcologyCombatBinding>() : null;
        EcologyCombatBinding victim = target != null ? target.GetComponent<EcologyCombatBinding>() : null;
        if (source == null || victim == null || source.Barrier == null || source.Barrier != victim.Barrier)
            return false;
        request = source.Barrier.Prepare(source, victim);
        return true;
    }
}
