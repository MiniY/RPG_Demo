using System;
using System.Collections.Generic;
using UnityEngine;

public enum EcologyAttackStatus
{
    Windup,
    Pending,
    Consumed,
    ProtectionCancelled,
    IdentityCancelled,
    EligibilityCancelled,
    WindupCancelled,
    FaultRejected
}

/// <summary>One immutable pair of identities, plus the lifecycle of this individual attack.</summary>
public sealed class EcologyAttackRequest
{
    internal readonly EcologyDamageBarrier Owner;
    internal readonly EcologyCombatBinding Attacker;
    internal readonly EcologyCombatBinding Target;
    internal readonly long AttackerLease;
    internal readonly long TargetLease;
    internal readonly long AttackerProtectionRevision;
    internal readonly long TargetProtectionRevision;
    internal readonly long AttackerMotionFaultRevision;
    internal readonly long TargetMotionFaultRevision;
    internal readonly long OwnerScopeRevision;
    internal Func<bool> Eligibility;
    internal Func<bool> Receiver;

    public long AttackId { get; }
    public long GenerationId { get; }
    public long AttackerId { get; }
    public long TargetId { get; }
    public long RequiredCompletedStep { get; internal set; }
    public float StartedAtRealTime { get; }
    public float DueAtRealTime { get; internal set; }
    public float ConsumedAtRealTime { get; internal set; }
    public EcologyAttackStatus Status { get; internal set; }
    public bool DamageApplied { get; internal set; }

    internal EcologyAttackRequest(EcologyDamageBarrier owner, long attackId,
        EcologyCombatBinding attacker, EcologyCombatBinding target)
    {
        Owner = owner;
        OwnerScopeRevision = owner.ScopeRevision;
        AttackId = attackId;
        Attacker = attacker;
        Target = target;
        GenerationId = attacker.GenerationId;
        AttackerId = attacker.ResidentId;
        TargetId = target.ResidentId;
        AttackerLease = attacker.Lease;
        TargetLease = target.Lease;
        AttackerProtectionRevision = attacker.ProtectionRevision;
        TargetProtectionRevision = target.ProtectionRevision;
        AttackerMotionFaultRevision = attacker.MotionFaultRevision;
        TargetMotionFaultRevision = target.MotionFaultRevision;
        StartedAtRealTime = Time.realtimeSinceStartup;
        Status = EcologyAttackStatus.Windup;
    }

    public bool Queue(Func<bool> eligibility, Func<bool> receiver)
    {
        return Owner != null && Owner.Queue(this, eligibility, receiver);
    }

    public bool MatchesParticipants(Transform attacker, Transform target)
    {
        return Attacker != null && Target != null && Attacker.transform == attacker && Target.transform == target;
    }

    // Cancelling a newer windup never invalidates an older request already owned by the queue.
    public void CancelWindup()
    {
        if (Status == EcologyAttackStatus.Windup)
            Status = EcologyAttackStatus.WindupCancelled;
    }
}

/// <summary>
/// Early fixed-step queue. P0.V first slice: identity and deferred receiver dispatch only.
/// Continuous motion/protection certification is deliberately not claimed by this component.
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class EcologyDamageBarrier : MonoBehaviour
{
    private readonly List<EcologyAttackRequest> pending = new List<EcologyAttackRequest>();
    private long nextAttackId;
    private long begunStep;
    private readonly List<EcologyMotionBody> motionBodies = new List<EcologyMotionBody>();
    private Rect[] protection = Array.Empty<Rect>();
    private Func<bool> scopeIsValid;
    private Action<Action> unsubscribeScope;
    private bool scopeRetired;
    internal long ScopeRevision { get; private set; }
    public bool IsScopeOpen => isActiveAndEnabled && !scopeRetired;
    internal Rect[] Protection => protection;

    // The existing session/map owner supplies its state event. No legacy profile auto-registers.
    public void BindLifetime(Func<bool> isCurrentScope, Action<Action> subscribe, Action<Action> unsubscribe)
    {
        if (isCurrentScope == null || subscribe == null || unsubscribe == null)
            throw new ArgumentNullException("A scope needs its owner predicate and paired event registration.");
        scopeRetired = true;
        ScopeRevision = checked(ScopeRevision + 1);
        RetirePending(EcologyAttackStatus.IdentityCancelled);
        ReleaseLifetimeSubscription();
        scopeIsValid = isCurrentScope;
        unsubscribeScope = unsubscribe;
        scopeRetired = false;
        try { subscribe(RefreshLifetime); }
        catch { scopeRetired = true; ReleaseLifetimeSubscription(); throw; }
        RefreshLifetime();
    }

    private void RefreshLifetime()
    {
        if (scopeRetired || scopeIsValid == null) return;
        bool valid;
        try { valid = scopeIsValid(); }
        catch (Exception exception)
        {
            scopeRetired = true;
            ScopeRevision = checked(ScopeRevision + 1);
            RetirePending(EcologyAttackStatus.FaultRejected);
            Debug.LogException(exception, this);
            return;
        }
        if (valid) return;
        scopeRetired = true;
        ScopeRevision = checked(ScopeRevision + 1);
        RetirePending(EcologyAttackStatus.IdentityCancelled);
    }

    private void ReleaseLifetimeSubscription()
    {
        Action<Action> release = unsubscribeScope;
        unsubscribeScope = null;
        scopeIsValid = null;
        release?.Invoke(RefreshLifetime);
    }

    public void ConfigureProtection(Rect[] regions)
    {
        if (regions == null) throw new ArgumentNullException(nameof(regions));
        foreach (Rect region in regions)
            if (region.width <= 0f || region.height <= 0f ||
                float.IsNaN(region.x) || float.IsNaN(region.y) ||
                float.IsInfinity(region.xMin) || float.IsInfinity(region.xMax) ||
                float.IsInfinity(region.yMin) || float.IsInfinity(region.yMax))
                throw new ArgumentException("Protection rectangles must be finite and nonempty.");
        protection = (Rect[])regions.Clone();
        foreach (EcologyMotionBody motion in motionBodies)
            if (motion != null) motion.RefreshProtectionBaseline();
    }

    public long CompletedStep { get; private set; }
    public long CurrentStep => begunStep;
    public int PendingCount => pending.Count;
    public event Action<EcologyAttackRequest, string> OnTrace;

    public EcologyAttackRequest Prepare(EcologyCombatBinding attacker, EcologyCombatBinding target)
    {
        RefreshLifetime();
        SynchronizeExternalCallback();
        if (attacker == null || target == null || attacker.Barrier != this || target.Barrier != this)
            throw new ArgumentException("Both attack participants must belong to this barrier.");
        var request = new EcologyAttackRequest(this, checked(++nextAttackId), attacker, target);
        ValidateIdentityAndProtection(request);
        Trace(request, "Started");
        return request;
    }

    internal bool Queue(EcologyAttackRequest request, Func<bool> eligibility, Func<bool> receiver)
    {
        RefreshLifetime();
        SynchronizeExternalCallback();
        if (request.Owner != this || request.Status != EcologyAttackStatus.Windup)
            return false;
        if (!IsScopeOpen)
        {
            request.Status = EcologyAttackStatus.IdentityCancelled;
            Trace(request, "ScopeRetired");
            return false;
        }
        request.DueAtRealTime = Time.realtimeSinceStartup;
        if (eligibility == null || receiver == null)
            throw new ArgumentNullException("Deferred damage requires eligibility and the original receiver.");
        if (!ValidateIdentityAndProtection(request))
            return false;
        if (!eligibility())
        {
            request.Status = EcologyAttackStatus.EligibilityCancelled;
            Trace(request, "NominalDueRejected");
            return false;
        }

        request.Eligibility = eligibility;
        request.Receiver = receiver;
        // Production due callbacks run after automatic Physics(n), not inside the next FixedUpdate.
        request.RequiredCompletedStep = checked(begunStep + 1);
        request.Status = EcologyAttackStatus.Pending;
        pending.Add(request);
        Trace(request, "Enqueued");
        return true;
    }

    private void FixedUpdate()
    {
        RefreshLifetime();
        if (!IsScopeOpen) return;
        for (int i = motionBodies.Count - 1; i >= 0; i--)
            if (motionBodies[i] != null)
                motionBodies[i].Observe(begunStep);
        CompletedStep = begunStep;
        begunStep = checked(begunStep + 1);

        // Freeze this batch before receiver callbacks can enqueue another attack or retire identities.
        EcologyAttackRequest[] batch = pending.ToArray();
        foreach (EcologyAttackRequest request in batch)
        {
            if (!pending.Contains(request))
                continue;
            if (!ValidateIdentityAndProtection(request))
            {
                pending.Remove(request);
                request.Eligibility = null;
                request.Receiver = null;
                continue;
            }
            if (CompletedStep < request.RequiredCompletedStep)
                continue;
            pending.Remove(request);
            if (!request.Eligibility())
            {
                request.Status = EcologyAttackStatus.EligibilityCancelled;
                Trace(request, "ConsumeRejected");
                request.Eligibility = null;
                request.Receiver = null;
                continue;
            }

            // Terminal before invoking user code: an OnDamaged subscriber cannot consume this twice.
            request.Status = EcologyAttackStatus.Consumed;
            request.ConsumedAtRealTime = Time.realtimeSinceStartup;
            try
            {
                request.DamageApplied = request.Receiver();
            }
            catch (Exception exception)
            {
                request.Status = EcologyAttackStatus.FaultRejected;
                Debug.LogException(exception, this);
            }
            Trace(request, "Consumed");
            request.Eligibility = null;
            request.Receiver = null;
        }
    }

    private bool ValidateIdentityAndProtection(EcologyAttackRequest request)
    {
        if (!IsScopeOpen || request.OwnerScopeRevision != ScopeRevision ||
            !Matches(request.Attacker, request.GenerationId, request.AttackerId, request.AttackerLease) ||
            !Matches(request.Target, request.GenerationId, request.TargetId, request.TargetLease))
        {
            request.Status = EcologyAttackStatus.IdentityCancelled;
            Trace(request, "IdentityCancelled");
            return false;
        }
        request.Attacker.GetComponent<EcologyMotionBody>()?.RefreshActualProtection();
        request.Target.GetComponent<EcologyMotionBody>()?.RefreshActualProtection();
        if (request.Attacker.ProtectionNow || request.Target.ProtectionNow ||
            request.Attacker.ProtectionRevision != request.AttackerProtectionRevision ||
            request.Target.ProtectionRevision != request.TargetProtectionRevision)
        {
            request.Status = EcologyAttackStatus.ProtectionCancelled;
            Trace(request, "ProtectionCancelled");
            return false;
        }
        if (request.Attacker.MotionFaultRevision != request.AttackerMotionFaultRevision ||
            request.Target.MotionFaultRevision != request.TargetMotionFaultRevision)
        {
            request.Status = EcologyAttackStatus.FaultRejected;
            Trace(request, "MotionHistoryUnknown");
            return false;
        }
        return true;
    }

    private bool Matches(EcologyCombatBinding binding, long generation, long resident, long lease)
    {
        return binding != null && binding.Barrier == this && binding.IsLive &&
               binding.GenerationId == generation && binding.ResidentId == resident && binding.Lease == lease;
    }

    private void OnDisable()
    {
        scopeRetired = true;
        ScopeRevision = checked(ScopeRevision + 1);
        RetirePending(EcologyAttackStatus.IdentityCancelled);
        ReleaseLifetimeSubscription();
    }

    private void RetirePending(EcologyAttackStatus reason)
    {
        foreach (EcologyAttackRequest request in pending.ToArray())
        {
            pending.Remove(request);
            request.Status = reason;
            request.Eligibility = null;
            request.Receiver = null;
            Trace(request, "ScopeRetired");
        }
    }

    private void Trace(EcologyAttackRequest request, string phase)
    {
        OnTrace?.Invoke(request, phase);
    }

    internal void RegisterMotionBody(EcologyMotionBody body)
    {
        if (!motionBodies.Contains(body))
            motionBodies.Add(body);
        EcologyMotionCommitter committer = GetComponent<EcologyMotionCommitter>();
        if (committer == null)
            committer = gameObject.AddComponent<EcologyMotionCommitter>();
        committer.Bind(this);
    }

    internal void UnregisterMotionBody(EcologyMotionBody body) { motionBodies.Remove(body); }

    internal void CommitMotionBodies()
    {
        RefreshLifetime();
        if (!IsScopeOpen) return;
        for (int i = motionBodies.Count - 1; i >= 0; i--)
            if (motionBodies[i] != null)
                motionBodies[i].Commit(begunStep);
    }

    private void SynchronizeExternalCallback()
    {
        // Render callbacks follow automatic physics. A producer inside FixedUpdate must not
        // close the step whose simulation has not happened yet.
        if (Time.inFixedTimeStep) return;
        foreach (EcologyMotionBody motion in motionBodies)
            if (motion != null) motion.Observe(begunStep);
    }
}
