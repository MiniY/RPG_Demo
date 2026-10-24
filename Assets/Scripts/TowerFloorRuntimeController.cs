using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerFloorExitState
{
    Unprepared,
    Locked,
    Available,
    Transitioning,
    Retired
}

/// <summary>
/// Owns the current generation's required Monster targets and the single world/minimap exit gate.
/// </summary>
[DisallowMultipleComponent]
public sealed class TowerFloorRuntimeController : MonoBehaviour, IMapRuntimeReadyFinalizer
{
    [SerializeField] private MapGenerationController mapController;
    [SerializeField] private MapMinimapController minimapController;
    [SerializeField] private GameObject exitPortalPrefab;
    [SerializeField, Min(0.1f)] private float interactionRadius = 1.5f;

    private readonly HashSet<BaseDamageable> targets = new HashSet<BaseDamageable>();
    private readonly HashSet<BaseDamageable> defeatedTargets = new HashSet<BaseDamageable>();
    private GameObject exitPortalInstance;
    private Guid currentGenerationId;
    private Transform player;
    private GameInput boundInput;

    public event Action<Guid> ExitUseRequested;
    public TowerFloorExitState State { get; private set; } = TowerFloorExitState.Unprepared;
    public Guid CurrentGenerationId => currentGenerationId;
    public int TargetCount => targets.Count;
    public int DefeatedTargetCount => defeatedTargets.Count;
    public float ExitInteractionRadius => interactionRadius;

    private void Awake()
    {
        ResolveReferences();
        EnsurePortalInstance();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindLifecycle();
        TryBindInput();
    }

    private void Update()
    {
        TryBindInput();
    }

    private void OnDisable()
    {
        UnbindInput();
        UnbindLifecycle();
        RetireCurrentGeneration();
    }

    public bool TryFinalizeBeforeReady(
        MapRuntimeContext context,
        Guid generationId,
        out string reason)
    {
        ResolveReferences();
        EnsurePortalInstance();
        RetireCurrentGeneration();

        if (context == null || context.ActiveGenerationId != generationId ||
            context.ActiveMap == null || context.ActiveRegistry == null ||
            context.ActiveRegistry.IsRetired)
        {
            reason = "楼层最终化需要当前有效的地图、Registry 和 GenerationId。";
            return false;
        }

        if (mapController == null || mapController.TilemapRenderer == null ||
            minimapController == null || exitPortalInstance == null)
        {
            reason = "楼层最终化缺少地图、小地图或黑洞出口引用。";
            return false;
        }

        foreach (MapObjectRegistryEntry entry in context.ActiveRegistry.Entries)
        {
            if (entry.GenerationId != generationId ||
                !string.Equals(entry.Role, MonsterPlacementService.MonsterRole, StringComparison.Ordinal))
            {
                continue;
            }

            Component component = entry.Instance as Component;
            BaseDamageable target = component == null ? null : component.GetComponent<BaseDamageable>();

            if (target == null || !target.gameObject.activeInHierarchy || !targets.Add(target))
            {
                reason = "当前楼层的目标 Monster 没有唯一、活动且可伤害的运行实例。";
                RetireCurrentGeneration();
                return false;
            }
        }

        if (targets.Count == 0)
        {
            reason = "战斗层没有合法的目标 Monster 集合。";
            RetireCurrentGeneration();
            return false;
        }

        foreach (BaseDamageable target in targets)
        {
            target.ResetForGenerationReuse();
            target.OnDefeated += HandleTargetDefeated;
        }

        currentGenerationId = generationId;
        defeatedTargets.Clear();
        player = ResolvePlayer();
        Vector3 exitPosition = mapController.TilemapRenderer.Coordinates.CellToWorld(
            context.ActiveMap,
            context.ActiveMap.ExitCell);
        exitPosition.z = exitPortalInstance.transform.position.z;
        exitPortalInstance.transform.position = exitPosition;
        exitPortalInstance.SetActive(false);

        if (!minimapController.TrySetExitAvailable(generationId, false))
        {
            reason = "小地图拒绝了当前 GenerationId 的出口锁定。";
            RetireCurrentGeneration();
            return false;
        }

        State = TowerFloorExitState.Unprepared;
        reason = string.Empty;
        return true;
    }

    private void HandleGameplayReady(Guid generationId)
    {
        if (generationId != currentGenerationId || State != TowerFloorExitState.Unprepared)
            return;

        State = TowerFloorExitState.Locked;
    }

    private void HandleTargetDefeated(BaseDamageable target)
    {
        if (State != TowerFloorExitState.Locked || target == null ||
            !targets.Contains(target) || !target.IsDefeated ||
            mapController == null ||
            !mapController.Orchestrator.IsCurrentActive(currentGenerationId) ||
            !defeatedTargets.Add(target))
        {
            return;
        }

        if (defeatedTargets.Count != targets.Count)
            return;

        if (!minimapController.TrySetExitAvailable(currentGenerationId, true))
            return;

        State = TowerFloorExitState.Available;
        exitPortalInstance.SetActive(true);
    }

    private void HandleInteractPressed()
    {
        if (State != TowerFloorExitState.Available ||
            TowerRunGateHub.IsGameplayBlocked ||
            exitPortalInstance == null || !exitPortalInstance.activeInHierarchy)
        {
            return;
        }

        if (player == null)
            player = ResolvePlayer();

        PlayerDamageController damage = player == null
            ? null
            : player.GetComponent<PlayerDamageController>();

        if (player == null || damage == null || damage.IsDefeated || damage.CurrentHealth <= 0f ||
            Vector2.Distance(player.position, exitPortalInstance.transform.position) > interactionRadius)
        {
            return;
        }

        Action<Guid> handler = ExitUseRequested;

        if (handler == null)
            return;

        handler.Invoke(currentGenerationId);
    }

    public bool TryAcceptExitTransition(Guid generationId)
    {
        if (generationId == Guid.Empty || generationId != currentGenerationId ||
            State != TowerFloorExitState.Available || mapController == null ||
            !mapController.Orchestrator.IsCurrentActive(generationId) ||
            defeatedTargets.Count != targets.Count || exitPortalInstance == null ||
            !exitPortalInstance.activeInHierarchy)
        {
            return false;
        }

        State = TowerFloorExitState.Transitioning;
        exitPortalInstance.SetActive(false);
        return true;
    }

    private void ResolveReferences()
    {
        if (mapController == null)
            mapController = GetComponentInChildren<MapGenerationController>(true);

        if (minimapController == null)
            minimapController = FindObjectOfType<MapMinimapController>();
    }

    private void EnsurePortalInstance()
    {
        if (exitPortalInstance != null || exitPortalPrefab == null)
            return;

        exitPortalInstance = Instantiate(exitPortalPrefab, transform);
        exitPortalInstance.name = "TowerFloorExitPortal";
        exitPortalInstance.SetActive(false);
    }

    private void BindLifecycle()
    {
        if (mapController != null)
            mapController.Orchestrator.GameplayReady += HandleGameplayReady;
    }

    private void UnbindLifecycle()
    {
        if (mapController != null)
            mapController.Orchestrator.GameplayReady -= HandleGameplayReady;
    }

    private void TryBindInput()
    {
        if (boundInput != null || GameInput.Instance == null)
            return;

        boundInput = GameInput.Instance;
        boundInput.OnInteractPressed += HandleInteractPressed;
    }

    private void UnbindInput()
    {
        if (boundInput == null)
            return;

        boundInput.OnInteractPressed -= HandleInteractPressed;
        boundInput = null;
    }

    private Transform ResolvePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject == null ? null : playerObject.transform;
    }

    private void RetireCurrentGeneration()
    {
        foreach (BaseDamageable target in targets)
        {
            if (target != null)
                target.OnDefeated -= HandleTargetDefeated;
        }

        targets.Clear();
        defeatedTargets.Clear();

        if (exitPortalInstance != null)
            exitPortalInstance.SetActive(false);

        if (State != TowerFloorExitState.Unprepared)
            State = TowerFloorExitState.Retired;

        currentGenerationId = Guid.Empty;
    }
}
