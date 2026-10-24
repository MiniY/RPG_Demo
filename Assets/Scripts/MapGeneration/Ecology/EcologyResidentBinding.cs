using System;
using System.Linq;
using UnityEngine;

public enum EcologyMovementStatus { Idle, Moving, Arrived, Blocked, NoPath, Retired }

/// <summary>Generation/home metadata prepared before a resident's first activation.</summary>
[DisallowMultipleComponent]
public sealed class EcologyResidentBinding : MonoBehaviour, IPooledLegacyActivation
{
    private Func<bool> currentScope, gameplayReady;
    private bool retired;
    private Collider2D movementFoot;
    private EcologyEcotypeDefinition boundType;
    private string acceptedLayoutSignature, acceptedGeometryIdentity;
    private long staticBlockerVersion;
    private Action navigationSynchronizer;
    internal void AttachNavigationSynchronizer(Action callback) { navigationSynchronizer = callback; }
    internal void DetachNavigationSynchronizer(Action callback) { if (navigationSynchronizer == callback) navigationSynchronizer = null; }
    private uint patrolSequence, patrolSeed;
    private readonly int[] recentPatrol = { -1, -1, -1, -1 };
    private int recentIndex;
    public Guid GenerationId { get; private set; }
    public string ResidentId { get; private set; }
    public string EcotypeId { get; private set; }
    public int HomeRegionId { get; private set; }
    public bool IsBound => ResidentId != null;
    public bool ScopeFaulted { get; private set; }
    public string NavigationFailure { get; private set; }
    public bool CanAct
    {
        get
        {
            if (!IsBound || !isActiveAndEnabled || retired || ScopeFaulted) return false;
            try { return currentScope() && gameplayReady(); }
            catch { ScopeFaulted = true; return false; }
        }
    }
    public EcologyHomeNavigation Home { get; private set; }
    public EcologyExcursionState Excursion { get; private set; }
    public void RebindNavigation(EcologyHomeNavigationCache cache)
    {
        if (!IsBound || retired || !isActiveAndEnabled || cache == null || cache.Layout.GenerationId != GenerationId ||
            cache.Layout.Signature != acceptedLayoutSignature || cache.AcceptedGeometryIdentity != acceptedGeometryIdentity ||
            cache.StaticBlockerVersion < staticBlockerVersion || boundType == null || movementFoot == null ||
            currentScope == null || !currentScope() || ScopeFaulted && NavigationFailure == null)
            throw new ArgumentException("A resident cannot refresh navigation from a retired, stale or foreign layout.");
        MapEcologyResident resident = cache.Layout.Residents.FirstOrDefault(r => r.Id == ResidentId && r.TypeId == EcotypeId && r.HomeRegionId == HomeRegionId);
        if (resident == null || boundType.ResolveMovementFoot(gameObject) != movementFoot)
            throw new ArgumentException("The live resident identity or original foot changed.");
        EcologyHomeNavigation next = cache.Get(resident, boundType);
        if (cache.Geometry.BuildNavigationGraph(movementFoot, 0f).FootprintIdentity != next.HomeGraph.FootprintIdentity)
            throw new InvalidOperationException("The live foot cannot adopt another footprint's navigation.");
        Home = next; staticBlockerVersion = cache.StaticBlockerVersion;
        if (NavigationFailure != null) { NavigationFailure = null; ScopeFaulted = false; }
    }
    private bool pursuitMovement;
    public bool BeginPursuit()
    {
        navigationSynchronizer?.Invoke();
        if (!CanAct || Excursion == null || !Excursion.MayChase) return false;
        pursuitMovement = true; return true;
    }
    public void RequestReturn() { if (Excursion != null) Excursion.RequestReturn(); pursuitMovement = true; }
    public bool TryGetHomeDistance(out int distance, out Vector2 waypoint)
    {
        navigationSynchronizer?.Invoke();
        distance = -1; waypoint = transform.position;
        return CanAct && movementFoot != null && movementFoot.attachedRigidbody != null && Home != null &&
            Home.TryMeasureHomeDistance(movementFoot, movementFoot.attachedRigidbody.position, out distance, out waypoint);
    }
    public bool CanPursuePosition(Vector2 position, Vector2 target)
    {
        navigationSynchronizer?.Invoke();
        return CanAct && Excursion != null && Excursion.MayChase && movementFoot != null &&
            Home.TryPursuitWaypoint(movementFoot, position, target, Excursion.MaximumHomeSteps, out _);
    }
    public bool TryMovementWaypoint(Vector2 position, Vector2 destination, out Vector2 waypoint)
    {
        navigationSynchronizer?.Invoke();
        waypoint = position;
        if (!CanAct || Home == null || movementFoot == null || Excursion == null) return false;
        if (Excursion.ReturnLatched) return Home.TryMeasureHomeDistance(movementFoot, position, out _, out waypoint);
        if (pursuitMovement) return Home.TryPursuitWaypoint(movementFoot, position, destination, Excursion.MaximumHomeSteps, out waypoint);
        return TryPatrolWaypoint(position, destination, out waypoint);
    }
    public void FailNavigation(string reason)
    {
        if (ScopeFaulted) return;
        NavigationFailure = reason; ScopeFaulted = true;
        Debug.LogWarning("ECOLOGY_NAVIGATION_FAILED resident=" + ResidentId + " home=" + HomeRegionId + " reason=" + reason, this);
    }
    internal bool TryClipTranslation(Collider2D foot, Vector2 from, Vector2 to, out Vector2 clipped)
    {
        navigationSynchronizer?.Invoke();
        clipped = from;
        if (!CanAct || Home == null || Excursion == null) return false;
        if (!pursuitMovement && !Excursion.HasPermit) return Home.TryClipHomeTranslation(foot, from, to, out clipped);
        int limit = Excursion.MaximumHomeSteps;
        if (Excursion.ReturnLatched && Home.TryMeasureHomeDistance(foot, from, out int distance, out _)) limit = Math.Max(limit, distance);
        return Home.TryClipExcursionTranslation(foot, from, to, limit, out clipped);
    }
    internal void ObserveActualMotion(Collider2D foot, Vector2 start, Vector2 end, bool certifiedStraight, bool stopped, double scaledWorldNow, double stepDuration)
    {
        navigationSynchronizer?.Invoke();
        if (!CanAct || Excursion == null || Home == null) return;
        bool completingEpisode = Excursion.HasPermit || Excursion.ReturnLatched;
        if (certifiedStraight && Home.TryObservedHomeExit(foot, start, end, out float fraction, out Vector2 witness))
        {
            Home.TryMeasureHomeDistance(foot, witness, out int witnessDistance, out _);
            Excursion.ObserveActualPose(false, false, witnessDistance, scaledWorldNow - stepDuration + fraction * stepDuration);
        }
        bool completeHome = Home.ContainsFullFoot(foot, end);
        Home.TryMeasureHomeDistance(foot, end, out int homeDistance, out _);
        Excursion.ObserveActualPose(completeHome, stopped, homeDistance, scaledWorldNow);
        if (completeHome && stopped && completingEpisode && !Excursion.HasPermit) pursuitMovement = false;
    }
    internal void MarkObservationFault() { ScopeFaulted = true; }
    public void Prepare(EcologyHomeNavigationCache cache, string residentId, EcologyEcotypeDefinition type,
        Func<bool> isCurrentScope, Func<bool> isGameplayReady, int maximumHomeSteps = 6, double maximumOutsideSeconds = 16d)
    {
        if (gameObject.activeInHierarchy || cache == null || type == null || isCurrentScope == null || isGameplayReady == null)
            throw new ArgumentException("Resident preparation requires an inactive actor, exact cache and paired scope predicates.");
        MapEcologyResident resident = cache.Layout.Residents.FirstOrDefault(r => r.Id == residentId);
        if (resident == null || resident.TypeId != type.TypeId) throw new ArgumentException("Unknown resident/ecotype identity.");
        Collider2D actual = type.ResolveMovementFoot(gameObject);
        EcologyHomeNavigation navigation = cache.Get(resident, type);
        if (cache.Geometry.BuildNavigationGraph(actual, 0f).FootprintIdentity != navigation.HomeGraph.FootprintIdentity)
            throw new InvalidOperationException("Prepared instance footprint differs from its retained ecotype.");
        GenerationId = cache.Layout.GenerationId; ResidentId = resident.Id; EcotypeId = resident.TypeId; HomeRegionId = resident.HomeRegionId;
        Home = navigation; currentScope = isCurrentScope; gameplayReady = isGameplayReady; ScopeFaulted = false; retired = false;
        movementFoot = actual; patrolSequence = 0; patrolSeed = 2166136261u; recentIndex = 0;
        navigationSynchronizer = null;
        boundType = type; acceptedLayoutSignature = cache.Layout.Signature; acceptedGeometryIdentity = cache.AcceptedGeometryIdentity;
        staticBlockerVersion = cache.StaticBlockerVersion;
        Excursion = new EcologyExcursionState(maximumHomeSteps, maximumOutsideSeconds);
        NavigationFailure = null;
        pursuitMovement = false;
        foreach (char c in resident.Id) patrolSeed = unchecked((patrolSeed ^ c) * 16777619u);
        for (int i = 0; i < recentPatrol.Length; i++) recentPatrol[i] = -1;
    }
    public bool TrySelectPatrolDestination(Vector2 position, out Vector2 destination)
    {
        navigationSynchronizer?.Invoke();
        destination = position;
        if (!CanAct || movementFoot == null || Home == null || Home.HomeNodes.Count == 0) return false;
        uint sequence = unchecked(++patrolSequence);
        int start = (int)(unchecked(patrolSeed + sequence * 2654435769u) % (uint)Home.HomeNodes.Count);
        for (int attempt = 0; attempt < Math.Min(32, Home.HomeNodes.Count); attempt++)
        {
            int node = Home.HomeNodes[(start + attempt) % Home.HomeNodes.Count];
            Vector2 candidate = Home.NodeWorldPosition(node);
            if ((candidate - position).sqrMagnitude <= .01f || Array.IndexOf(recentPatrol, node) >= 0) continue;
            if (!Home.TryPatrolWaypoint(movementFoot, position, node, out _)) continue;
            recentPatrol[recentIndex] = node; recentIndex = (recentIndex + 1) % recentPatrol.Length;
            destination = candidate; return true;
        }
        return false;
    }
    public bool TryPatrolWaypoint(Vector2 position, Vector2 destination, out Vector2 waypoint)
    {
        navigationSynchronizer?.Invoke();
        waypoint = position;
        return CanAct && movementFoot != null && Home != null && Home.TryHomeNode(destination, out int node) &&
            Home.TryPatrolWaypoint(movementFoot, position, node, out waypoint);
    }
    private void OnDisable() { retired = true; }
    public void PrepareForLegacyActivation()
    {
        if (gameObject.activeInHierarchy && IsBound) throw new InvalidOperationException("An active resident cannot silently become legacy.");
        GenerationId = Guid.Empty; ResidentId = EcotypeId = null; HomeRegionId = 0; Home = null;
        currentScope = gameplayReady = null; retired = true; ScopeFaulted = false;
        movementFoot = null;
        navigationSynchronizer = null;
        boundType = null; acceptedLayoutSignature = acceptedGeometryIdentity = null; staticBlockerVersion = 0;
        Excursion = null; pursuitMovement = false;
        NavigationFailure = null;
    }
}
