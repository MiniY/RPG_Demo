using System;
using System.Collections.Generic;
using UnityEngine;

public enum EcologyMotionKind { Normal, Hurt }

/// <summary>A trace stage is not a claim that a requested segment actually happened in physics.</summary>
public sealed class EcologyMotionTrace
{
    public long Step { get; }
    public string Phase { get; }
    public EcologyMotionKind Kind { get; }
    public Vector2 Position { get; }
    public long ResidentId { get; }
    public long GenerationId { get; }
    public long Lease { get; }
    public int SourceColliderId { get; }
    public int ContactColliderId { get; }
    public Vector2 ContactNormal { get; }
    public float ContactSeparation { get; }

    internal EcologyMotionTrace(long step, string phase, EcologyMotionKind kind, Vector2 position,
        EcologyCombatBinding binding, Collider2D solid, int contactColliderId = 0,
        Vector2 contactNormal = default, float contactSeparation = 0f)
    {
        Step = step;
        Phase = phase;
        Kind = kind;
        Position = position;
        ResidentId = binding != null ? binding.ResidentId : 0;
        GenerationId = binding != null ? binding.GenerationId : 0;
        Lease = binding != null ? binding.Lease : 0;
        SourceColliderId = solid != null ? solid.GetInstanceID() : 0;
        ContactColliderId = contactColliderId;
        ContactNormal = contactNormal;
        ContactSeparation = contactSeparation;
    }
}

/// <summary>Opt-in per-body intent arbitration. Continuous trajectory certification is a later slice.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EcologyMotionBody : MonoBehaviour, IPooledLegacyActivation
{
    private Rigidbody2D body;
    private Collider2D solid;
    private EcologyCombatBinding binding;
    private EcologyDamageBarrier barrier;
    private long boundLease;
    private ContactFilter2D filter;
    private RaycastHit2D[] hits = new RaycastHit2D[16];
    private float skin;
    private long intentStep = -1;
    private bool hasNormal;
    private bool hasHurt;
    private Vector2 normalTarget;
    private Vector2 hurtTarget;
    private long submittedStep = -1;
    private long observedStep = -1;
    private EcologyMotionKind submittedKind;
    private Vector2 previousPosition;
    private float previousRotation;
    private Vector2 submittedStart;
    private Vector2 submittedTarget;
    private float submittedRotation;
    private bool contactDuringStep;
    private long protectionWitnessStep = -1;
    private readonly List<Vector2> protectionIntervals = new List<Vector2>();
    private bool awaitingPreparedActivation;
    private bool legacyReleased;
    private EcologyResidentBinding resident;

    public event Action<EcologyMotionTrace> OnTrace;
    public void ConfigurePrepared(Collider2D movementSolid, int blockingLayers, float collisionSkin)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Prepared motion requires an inactive pool lease.");
        ConfigureCore(movementSolid, blockingLayers, collisionSkin, true);
    }

    public void CompletePreparedConfiguration()
    {
        if (!awaitingPreparedActivation || !isActiveAndEnabled || body == null || solid == null || !solid.enabled ||
            solid.attachedRigidbody != body || solid.shapeCount < 1 || binding == null || !binding.IsLive ||
            binding.Lease != boundLease || binding.Barrier != barrier)
            throw new InvalidOperationException("Prepared motion needs its active matching lease and actual native solid.");
        long previousFault = binding.MotionFaultRevision;
        awaitingPreparedActivation = false;
        try
        {
            barrier.RegisterMotionBody(this);
            RefreshProtectionBaseline();
            if (binding.MotionFaultRevision != previousFault) throw new InvalidOperationException("Prepared native protection baseline failed.");
        }
        catch
        {
            barrier.UnregisterMotionBody(this); awaitingPreparedActivation = true; throw;
        }
    }

    public bool IsReady => !legacyReleased && !awaitingPreparedActivation && isActiveAndEnabled && binding != null && binding.IsLive &&
                           binding.Lease == boundLease && binding.Barrier == barrier &&
                           body != null && solid != null && solid.enabled && !solid.isTrigger;
    public bool OwnsMotionRoute => !legacyReleased;

    public void Configure(Collider2D movementSolid, int blockingLayers, float collisionSkin)
        => ConfigureCore(movementSolid, blockingLayers, collisionSkin, false);

    private void ConfigureCore(Collider2D movementSolid, int blockingLayers, float collisionSkin, bool prepared)
    {
        Rigidbody2D currentBody = GetComponent<Rigidbody2D>();
        EcologyCombatBinding currentBinding = GetComponent<EcologyCombatBinding>();
        Rigidbody2D solidBody = movementSolid == null ? null : prepared
            ? movementSolid.GetComponentInParent<Rigidbody2D>(true) : movementSolid.attachedRigidbody;
        if (currentBody == null || movementSolid == null || movementSolid.isTrigger || solidBody != currentBody ||
            currentBinding == null || currentBinding.Barrier == null || !currentBinding.Barrier.IsScopeOpen ||
            !prepared && !currentBinding.IsLive)
            throw new ArgumentException("Motion requires the explicit solid shape and its matching opt-in lease.");
        if (barrier != null)
            barrier.UnregisterMotionBody(this);
        body = currentBody;
        solid = movementSolid;
        binding = currentBinding;
        resident = GetComponent<EcologyResidentBinding>();
        boundLease = binding.Lease;
        barrier = binding.Barrier;
        skin = Mathf.Max(0f, collisionSkin);
        filter = new ContactFilter2D();
        filter.SetLayerMask(blockingLayers);
        filter.useTriggers = false;
        intentStep = submittedStep = observedStep = -1;
        hasNormal = hasHurt = false;
        contactDuringStep = false;
        protectionWitnessStep = -1;
        awaitingPreparedActivation = prepared;
        legacyReleased = false;
        previousPosition = body.position;
        previousRotation = body.rotation;
        if (!prepared)
        {
            barrier.RegisterMotionBody(this);
            RefreshProtectionBaseline();
        }
    }

    public bool Request(Vector2 target, EcologyMotionKind kind)
    {
        if (!IsReady || !Time.inFixedTimeStep || barrier.CurrentStep <= 0)
            return false;
        long step = barrier.CurrentStep;
        if (submittedStep == step)
        {
            Trace(step, "LateIntentRejected", kind, target);
            return false;
        }
        if (intentStep != step)
        {
            intentStep = step;
            hasNormal = hasHurt = false;
        }
        if (kind == EcologyMotionKind.Hurt)
        {
            hasHurt = true;
            hurtTarget = target;
        }
        else
        {
            hasNormal = true;
            normalTarget = target;
        }
        Trace(step, "Requested", kind, target);
        return true;
    }

    // Only an explicit inactive legacy pool handoff releases this route.
    // Ordinary unready/faulted adapters continue to own it and fail closed.
    public static bool TryRequest(Transform actor, Vector2 target, EcologyMotionKind kind)
    {
        EcologyMotionBody motion = actor != null ? actor.GetComponent<EcologyMotionBody>() : null;
        if (motion == null || motion.legacyReleased)
            return false;
        motion.Request(target, kind);
        return true;
    }

    public void PrepareForLegacyActivation()
    {
        if (gameObject.activeInHierarchy && (binding != null || barrier != null))
            throw new InvalidOperationException("An active motion lease cannot silently become legacy.");
        if (barrier != null) barrier.UnregisterMotionBody(this);
        barrier = null; binding = null; solid = null; legacyReleased = true; awaitingPreparedActivation = true;
        intentStep = submittedStep = observedStep = -1; hasNormal = hasHurt = false;
    }

    public void ReleaseRetiredScopeForLegacy(EcologyDamageBarrier expected)
    {
        // Configure can reject the solid before retaining the combat scope. The matching opt-in binding still proves ownership.
        var pendingBinding = GetComponent<EcologyCombatBinding>();
        bool unconfiguredOwnLease = barrier == null && binding == null && pendingBinding != null && pendingBinding.Barrier == expected;
        if (expected == null || expected.IsScopeOpen || barrier != expected && !unconfiguredOwnLease)
            throw new InvalidOperationException("Only the exact retired motion scope can restore a persistent legacy player.");
        if (barrier != null) barrier.UnregisterMotionBody(this);
        barrier = null; binding = null; solid = null;
        legacyReleased = true; awaitingPreparedActivation = true;
        intentStep = submittedStep = observedStep = -1; hasNormal = hasHurt = false;
    }

    internal void Commit(long step)
    {
        if (!IsReady || intentStep != step || submittedStep == step || (!hasNormal && !hasHurt))
            return;
        if (observedStep == step - 1) VerifyClosedPose(step - 1);
        EcologyMotionKind kind = hasHurt ? EcologyMotionKind.Hurt : EcologyMotionKind.Normal;
        Vector2 target = hasHurt ? hurtTarget : normalTarget;
        Trace(step, "Selected", kind, target);
        Vector2 displacement = target - body.position;
        float requestedDistance = displacement.magnitude;
        float allowedDistance = requestedDistance;
        if (requestedDistance > 0.00001f)
        {
            Vector2 direction = displacement / requestedDistance;
            int count;
            while (true)
            {
                count = solid.Cast(direction, filter, hits, requestedDistance + skin, true);
                if (count < hits.Length || hits.Length >= 256)
                    break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            if (count >= hits.Length)
            {
                binding.RejectMotionHistory();
                Trace(step, "CastCapacityRejected", kind, body.position);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || (hit.distance <= 0.0001f && Vector2.Dot(hit.normal, direction) > 0f))
                    continue;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - skin));
            }
            target = body.position + direction * allowedDistance;
        }
        Trace(step, "CastAllowed", kind, target);
        if (resident != null && resident.IsBound)
        {
            if (!resident.CanAct)
            {
                target = body.position;
                Trace(step, "ResidentNotReadyStopped", kind, target);
            }
            else if (!resident.TryClipTranslation(solid, body.position, target, out target))
            {
                binding.RejectMotionHistory();
                Trace(step, "ResidentDomainFault", kind, body.position);
                return;
            }
            else Trace(step, "ResidentIntentLimited", kind, target);
        }
        submittedStep = step;
        submittedKind = kind;
        submittedStart = body.position;
        submittedTarget = target;
        submittedRotation = body.rotation;
        contactDuringStep = body.IsTouching(filter);
        body.MovePosition(target);
        Trace(step, "Submitted", kind, target);
    }

    internal void Observe(long completedStep)
    {
        if (!IsReady)
            return;
        if (observedStep == completedStep)
        {
            VerifyClosedPose(completedStep);
            return;
        }
        long precedingObservation = observedStep;
        observedStep = completedStep;
        Trace(completedStep, "PhysicsObserved", submittedKind, body.position);
        Vector2 start = submittedStep == completedStep ? submittedStart : previousPosition;
        bool straight = submittedStep == completedStep
            ? (body.position - submittedTarget).sqrMagnitude < 0.00000001f &&
              Mathf.Abs(Mathf.DeltaAngle(body.rotation, submittedRotation)) < 0.0001f && !contactDuringStep
            : (body.position - previousPosition).sqrMagnitude < 0.00000001f && !contactDuringStep;
        bool rotationStable = Mathf.Abs(Mathf.DeltaAngle(body.rotation,
            submittedStep == completedStep ? submittedRotation : previousRotation)) < .0001f;
        if (precedingObservation >= 0 && completedStep != precedingObservation + 1 || !rotationStable)
        {
            binding.RejectMotionHistory();
            Trace(completedStep, "MotionObservationGapOrRotation", submittedKind, body.position);
        }
        else if (barrier.Protection.Length == 0)
        {
            // The empty set cannot be crossed by any trajectory, including contact correction.
            PublishProtection(completedStep, false);
            Trace(completedStep, "EmptyProtectionCertified", submittedKind, body.position);
        }
        else if (straight && EcologyProtectionGeometry.Intervals(solid, start, body.position,
                barrier.Protection, protectionIntervals))
        {
            foreach (Vector2 interval in protectionIntervals)
            {
                PublishProtection(completedStep, true);
                if (interval.y < 1f) PublishProtection(completedStep, false);
            }
            if (protectionIntervals.Count == 0) PublishProtection(completedStep, false);
            Trace(completedStep, "StraightHistoryCertified", submittedKind, body.position);
        }
        else
        {
            bool valid = TryObserveActualProtectionPose(completedStep, out bool inside);
            if (!contactDuringStep)
            {
                binding.RejectMotionHistory();
                Trace(completedStep, "MotionHistoryUnknown", submittedKind, body.position);
            }
            if (valid && (inside || protectionWitnessStep == completedStep))
                Trace(completedStep, "ProtectionWitnessCertified", submittedKind, body.position);
            else if (valid && contactDuringStep)
                // Explicitly approved B1 semantics: observed outside is not continuous-path proof.
                // Do not manufacture an entry or reject damage solely for unobserved solver interiors.
                Trace(completedStep, "ContactObservedOutsideB1", submittedKind, body.position);
            else if (contactDuringStep)
            {
                binding.RejectMotionHistory();
                Trace(completedStep, "MotionHistoryUnknown", submittedKind, body.position);
            }
        }
        if (resident != null && resident.IsBound && resident.CanAct)
        {
            try
            {
                // A clipped or repeated Hurt intent can hold a native zero-velocity
                // apex while its original return leg is still pending.
                bool stopped = submittedStep != completedStep && (body.position - start).sqrMagnitude <= .00000001f &&
                    body.velocity.sqrMagnitude <= .00000001f;
                resident.ObserveActualMotion(solid, start, body.position, straight, stopped, Time.timeAsDouble, Time.fixedDeltaTime);
                if (resident.Excursion != null && resident.Excursion.NavigationFault)
                {
                    resident.MarkObservationFault();
                    binding.RejectMotionHistory(); Trace(completedStep, "ResidentNavigationFault", submittedKind, body.position);
                }
            }
            catch (Exception error)
            {
                resident.MarkObservationFault();
                binding.RejectMotionHistory(); Trace(completedStep, "ResidentObservationFault:" + error.GetType().Name + ":" + error.Message, submittedKind, body.position);
            }
        }
        previousPosition = body.position;
        previousRotation = body.rotation;
        contactDuringStep = false;
    }

    private void VerifyClosedPose(long step)
    {
        if ((body.position - previousPosition).sqrMagnitude <= 0.00000001f &&
            Mathf.Abs(Mathf.DeltaAngle(body.rotation, previousRotation)) < .0001f) return;
        // A pose changed after this physics result was closed. It is not a second simulation
        // segment and must not be silently folded into the next normal intent's starting pose.
        binding.RejectMotionHistory();
        Trace(step, "UnobservedPositionWrite", submittedKind, body.position);
        previousPosition = body.position;
        previousRotation = body.rotation;
    }

    internal void RefreshProtectionBaseline()
    {
        if (!IsReady) return;
        if (EcologyProtectionGeometry.Intervals(solid, body.position, body.position,
            barrier.Protection, protectionIntervals))
            PublishProtection(barrier.CurrentStep, protectionIntervals.Count > 0);
        else binding.RejectMotionHistory();
        previousPosition = body.position;
        previousRotation = body.rotation;
    }

    private void PublishProtection(long step, bool inside)
    {
        if (binding.ProtectionNow == inside) return;
        binding.ObserveProtection(inside);
        Trace(step, inside ? "ProtectionEntered" : "ProtectionExited", submittedKind, body.position);
    }

    private bool TryObserveActualProtectionPose(long step, out bool inside)
    {
        inside = false;
        if (!EcologyProtectionGeometry.Intervals(solid, body.position, body.position,
                barrier.Protection, protectionIntervals)) return false;
        inside = protectionIntervals.Count > 0;
        if (inside) protectionWitnessStep = step;
        PublishProtection(step, inside);
        return true;
    }

    internal void RefreshActualProtection()
    {
        if (!IsReady)
        {
            if (binding != null && binding.IsLive && binding.Lease == boundLease)
            {
                binding.RejectMotionHistory();
                Trace(barrier.CurrentStep, "MotionObserverUnavailable", submittedKind, body != null ? body.position : default);
            }
            return;
        }
        if (!TryObserveActualProtectionPose(barrier.CurrentStep, out _))
        {
            binding.RejectMotionHistory();
            Trace(barrier.CurrentStep, "ProtectionQueryFailed", submittedKind, body.position);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision) { ObserveContact(collision); }
    private void OnCollisionStay2D(Collision2D collision) { ObserveContact(collision); }

    private void ObserveContact(Collision2D collision)
    {
        if (!IsReady) return;
        contactDuringStep = true;
        if (barrier == null || body == null) return;
        RefreshActualProtection();
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            OnTrace?.Invoke(new EcologyMotionTrace(barrier.CurrentStep, "ContactCallbackObserved",
                submittedKind, body.position, binding, solid,
                collision.collider != null ? collision.collider.GetInstanceID() : 0,
                contact.normal, contact.separation));
        }
    }

    private void Trace(long step, string phase, EcologyMotionKind kind, Vector2 position)
    {
        OnTrace?.Invoke(new EcologyMotionTrace(step, phase, kind, position, binding, solid));
    }

    private void OnDisable()
    {
        if (barrier != null)
            barrier.UnregisterMotionBody(this);
        intentStep = -1;
        hasNormal = hasHurt = false;
    }
}
