using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in attack strategy for the remediation recipe; the existing monster FSM owns execution.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAttackController))]
public sealed class BarrelExplosionAttack : MonoBehaviour
{
    public const double FuseSeconds = 3d;
    [SerializeField, Min(.01f)] private float triggerRadius = 2f;
    [SerializeField, Min(.01f)] private float explosionRadius = 1.5f;
    [SerializeField, Min(0f)] private float explosionDamage = 10f;

    private enum Phase { Idle, Fuse, Pending, Settled, Cancelled }
    private Phase phase;
    private BaseMonster monster;
    private MonsterMovementController movement;
    private MonsterAttackController attack;
    private Collider2D foot;
    private double armedAt;
    private readonly List<Victim> victims = new List<Victim>();
    private sealed class Victim
    {
        public PlayerDamageController Player;
        public EcologyAttackRequest Request;
    }

    public float TriggerRadius => triggerRadius;
    public float ExplosionRadius => explosionRadius;
    public bool IsLatched => phase == Phase.Fuse || phase == Phase.Pending;
    public int ExplosionCount { get; private set; }
    public int DamageCount { get; private set; }
    public double ArmedAt => armedAt;
    public event Action<string, double> OnTrace;

    private void Awake()
    {
        monster = GetComponent<BaseMonster>();
        movement = GetComponent<MonsterMovementController>();
        attack = GetComponent<MonsterAttackController>();
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        foreach (Collider2D shape in GetComponentsInChildren<Collider2D>(true))
            if (!shape.isTrigger && shape.attachedRigidbody == body)
            {
                if (foot != null) throw new InvalidOperationException("Barrel requires one original solid movement foot.");
                foot = shape;
            }
    }

    private void OnEnable()
    {
        phase = Phase.Idle; victims.Clear(); ExplosionCount = DamageCount = 0; armedAt = 0d;
    }

    private void OnDisable() { Cancel(false); }

    public bool CanTrigger(Transform target)
    {
        PlayerDamageController player = target != null ? target.GetComponentInParent<PlayerDamageController>() : null;
        EcologyCombatBinding source = GetComponent<EcologyCombatBinding>();
        EcologyCombatBinding receiver = player != null ? player.GetComponent<EcologyCombatBinding>() : null;
        return isActiveAndEnabled && phase == Phase.Idle && source != null && source.IsLive &&
            receiver != null && receiver.IsLive && source.Barrier == receiver.Barrier &&
            InRadius(player, triggerRadius) && CanFinishAndReturn(FuseSeconds);
    }

    public bool TryBegin(Transform target)
    {
        if (!CanTrigger(target)) return false;
        victims.Clear();
        foreach (PlayerDamageController player in FindObjectsOfType<PlayerDamageController>())
            if (EcologyCombatBinding.TryPrepare(transform, player.transform, out EcologyAttackRequest request))
                victims.Add(new Victim { Player = player, Request = request });
        if (victims.Count == 0) return false;
        phase = Phase.Fuse; armedAt = Time.timeAsDouble;
        Vector2 direction = attack.GetDirectionToTarget(target);
        if (direction.sqrMagnitude < .0001f) direction = movement.FacingDirection;
        movement.FaceDirection(direction); movement.Stop();
        attack.BeginAttack(direction);
        Trace("Armed");
        return true;
    }

    // Called only by the existing Attack state. No second Update/FSM or animation-event damage clock.
    public void TickAttack()
    {
        if (!IsLatched) return;
        movement.Stop();
        double remaining = Math.Max(0d, armedAt + FuseSeconds - Time.timeAsDouble);
        if (monster == null || monster.IsDefeated || !CanFinishAndReturn(remaining))
        {
            Cancel(true); return;
        }
        if (phase == Phase.Fuse && remaining <= 0d)
        {
            phase = Phase.Pending; ExplosionCount++;
            Trace("NominalExplosionDue");
            foreach (Victim victim in victims)
            {
                Victim captured = victim;
                victim.Request.Queue(() => IsLatched && !monster.IsDefeated &&
                    CanFinishAndReturn(0d) && InRadius(captured.Player, explosionRadius) &&
                    HasStaticLineOfSight(captured.Player), () =>
                    {
                        bool applied = captured.Player != null && captured.Player.TakeDamage(explosionDamage, transform);
                        if (applied) { DamageCount++; Trace("DamageApplied"); }
                        return applied;
                    });
            }
        }
        if (phase != Phase.Pending) return;
        foreach (Victim victim in victims)
            if (victim.Request.Status == EcologyAttackStatus.Pending) return;
        // The original death owner alone creates rewards, unregisters the target and returns the lease.
        phase = Phase.Settled; Trace("SelfDefeat");
        monster.TakeDamage(monster.CurrentHealth, transform);
    }

    public bool CanFinishAndReturn(double remainingFuse)
    {
        EcologyResidentBinding resident = GetComponent<EcologyResidentBinding>();
        if (resident == null || !resident.IsBound) return true;
        if (!resident.CanAct || resident.Excursion == null || resident.Excursion.ReturnLatched ||
            !resident.TryGetHomeDistance(out int steps, out _) || steps > resident.Excursion.MaximumHomeSteps) return false;
        if (steps == 0) return true;
        if (!resident.Excursion.HasPermit || movement.MoveSpeed <= 0f) return false;
        EcologyHomeNavigation home = resident.Home;
        int width = home.MovementGraph.Width;
        float horizontal = width > 1 ? Vector2.Distance(home.NodeWorldPosition(0), home.NodeWorldPosition(1)) : 0f;
        float vertical = home.MovementGraph.Height > 1 ? Vector2.Distance(home.NodeWorldPosition(0), home.NodeWorldPosition(width)) : 0f;
        double returnSeconds = (steps + 1d) * Math.Max(horizontal, vertical) / movement.MoveSpeed;
        double elapsed = Math.Max(resident.Excursion.Elapsed, Time.timeAsDouble - resident.Excursion.DepartedAt);
        return elapsed + remainingFuse + returnSeconds + 2d * Time.fixedDeltaTime < resident.Excursion.MaximumOutsideSeconds;
    }

    public void Cancel(bool resetVisual = true)
    {
        if (!IsLatched) return;
        phase = Phase.Cancelled;
        foreach (Victim victim in victims) victim.Request.CancelWindup();
        Trace("Cancelled");
        if (resetVisual && monster != null && !monster.IsDefeated)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isActiveAndEnabled) { animator.Rebind(); animator.Update(0f); }
        }
    }

    // A cancelled attack can be replaced only after the FSM has left Attack; old requests stay cancelled.
    public void LeaveAttack()
    {
        Cancel();
        if (phase == Phase.Cancelled) { phase = Phase.Idle; victims.Clear(); }
    }

    private bool InRadius(PlayerDamageController player, float radius)
    {
        Collider2D targetFoot = player != null ? player.GetComponent<PlayerColliderRoles>()?.MovementCollider : null;
        return monster != null && !monster.IsDefeated && foot != null && foot.enabled && targetFoot != null &&
            player.isActiveAndEnabled && !player.IsDefeated &&
            MapElevationRuntimeQuery.SameLevel(foot.bounds.center, targetFoot.bounds.center) &&
            ((Vector2)targetFoot.ClosestPoint(foot.bounds.center) - (Vector2)foot.bounds.center).sqrMagnitude <= radius * radius;
    }

    private bool HasStaticLineOfSight(PlayerDamageController player)
    {
        Collider2D targetFoot = player != null ? player.GetComponent<PlayerColliderRoles>()?.MovementCollider : null;
        if (foot == null || targetFoot == null) return false;
        Vector2 start = foot.bounds.center;
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(start, targetFoot.ClosestPoint(start), movement.MovementBlockingLayers))
        {
            Collider2D blocker = hit.collider;
            if (blocker == null || blocker.isTrigger || blocker.transform.IsChildOf(transform) ||
                blocker.transform.IsChildOf(player.transform) || blocker.GetComponentInParent<BaseMonster>() != null ||
                blocker.GetComponentInParent<PlayerDamageController>() != null) continue;
            if (blocker.attachedRigidbody != null && blocker.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;
            return false;
        }
        return true;
    }

    private void Trace(string label) { OnTrace?.Invoke(label, Time.timeAsDouble); }
}
