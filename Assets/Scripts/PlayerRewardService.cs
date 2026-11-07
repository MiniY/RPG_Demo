using System;
using UnityEngine;

/// <summary>Coordinates owned inventory, current stats and ground leases; owns no duplicate state.</summary>
public sealed class PlayerRewardService
{
    private readonly PlayerInventory inventory;
    private readonly PlayerStatsRuntime stats;
    private readonly MapGenerationController mapController;
    private readonly PlayerAction playerAction;
    private readonly Func<bool> canInteract;
    private readonly Func<GameSaveData, bool> commitRuntimeData;
    private bool processing;

    internal PlayerInventory Inventory => inventory;

    internal PlayerRewardService(PlayerInventory inventory, PlayerStatsRuntime stats,
        MapGenerationController mapController, Func<bool> canInteract, Func<GameSaveData, bool> commitRuntimeData = null)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
        this.mapController = mapController;
        this.canInteract = canInteract ?? throw new ArgumentNullException(nameof(canInteract));
        this.commitRuntimeData = commitRuntimeData ?? GameSaveService.TryCommitRuntimeData;
        playerAction = inventory.GetComponent<PlayerAction>();
    }

    private bool IsReady => !processing && inventory != null && inventory.isActiveAndEnabled &&
        inventory.gameObject.scene.IsValid() && inventory.gameObject.scene.isLoaded &&
        inventory.IsInitialized && stats != null && stats.IsInitialized && canInteract();

    internal RewardPickupResult TrySettleConsumable(RewardSO reward, int amount)
    {
        if (amount != 1 || reward == null || !IsReady)
            return default;
        if (!reward.TryGetRecovery(out PlayerStatType stat, out float value))
        {
            string id = string.IsNullOrWhiteSpace(reward.RewardId) ? "<missing>" : reward.RewardId;
            string configuredAmount = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Debug.LogWarning($"[Consumables] Invalid recovery configuration: reward={id}; kind={reward.RecoveryKind}; amount={configuredAmount}.", reward);
            return default;
        }

        processing = true;
        PlayerInventorySnapshot inventoryBefore = inventory.CaptureSnapshot();
        PlayerStatsSnapshot statsBefore = stats.CaptureSnapshot();
        try
        {
            RewardPickupResult result;
            if (stats.GetCurrent(stat) < stats.GetMaximum(stat))
            {
                if (!stats.TryRestoreCurrent(stat, value, out float restored)) return default;
                result = RewardPickupResult.Recovered(stat, restored);
            }
            else
            {
                if (!inventory.TryAddItemWithoutPersistence(reward, 1)) return default;
                result = RewardPickupResult.IntoInventory(1);
            }

            if (commitRuntimeData(CaptureCurrentData())) return result;
            inventory.RestoreSnapshot(inventoryBefore, false);
            stats.RestoreSnapshot(statsBefore, false);
            return default;
        }
        catch (Exception error)
        {
            inventory.RestoreSnapshot(inventoryBefore, false);
            stats.RestoreSnapshot(statsBefore, false);
            Debug.LogError($"[Consumables] Settlement rejected; current unit restored: {error}", inventory);
            return default;
        }
        finally { processing = false; }
    }

    internal void NotifyRecovered()
    {
        if (stats != null) stats.NotifyCommittedStatsChanged();
    }

    public bool TryDropOne(RewardSO reward, out string message)
    {
        message = string.Empty;
        if (!IsReady) { message = "当前无法丢出物品。"; return false; }
        if (reward == null || !reward.TryGetRecovery(out _, out _) || reward.rewardPrefab == null)
        { message = "此物品不能丢出。"; return false; }
        if (inventory.GetItemAmountLong(reward) < 1)
        { message = "背包中没有这份物品。"; return false; }
        if (mapController == null || mapController.Orchestrator == null)
        { message = "当前地图尚未准备好。"; return false; }

        var context = mapController.Orchestrator.Context;
        if (context.IsFailed || context.Phase != MapLifecyclePhase.Ready || !context.ActiveGenerationId.HasValue)
        { message = "当前地图尚未准备好。"; return false; }
        Guid generation = context.ActiveGenerationId.Value;
        Vector2 direction = playerAction != null ? playerAction.FacingDirection : Vector2.down;
        Vector2 position;
        try
        {
            if (!MapSpatialRewardPlacement.TryFindForPlayer(context, generation, reward.rewardPrefab.transform,
                    inventory.transform, direction, out position))
            { message = "身旁没有可以安全丢放物品的位置。"; return false; }
        }
        catch (Exception error)
        {
            message = "物品碰撞形状无法确认，未丢出。";
            Debug.LogWarning($"[Consumables] Drop pose rejected: {error.Message}", inventory);
            return false;
        }

        processing = true;
        try
        {
            PlayerInventorySnapshot inventoryBefore = inventory.CaptureSnapshot();
            GameSaveData memoryBefore = GameSaveService.Clone(GameSaveService.GetOrCreateData());
            GameObject ground = null;
            GenerationScopedRewardLifetime preparedLifetime = null;
            bool committed = false;
            try
            {
                ground = ObjectPoolManager.SpawnPrepared(reward.rewardPrefab, position, Quaternion.identity, actor =>
                {
                    if (context.ActiveGenerationId != generation || context.IsFailed || context.Phase != MapLifecyclePhase.Ready)
                        throw new InvalidOperationException("Drop generation changed before preparation.");
                    MapSpatialRewardPlacement.PrepareOriginalPickupCollider(actor);
                    var pickup = actor.GetComponent<RewardPickup>() ?? actor.AddComponent<RewardPickup>();
                    pickup.InitializeOwnedUnits(reward, 1);
                    preparedLifetime = actor.GetComponent<GenerationScopedRewardLifetime>() ??
                        actor.AddComponent<GenerationScopedRewardLifetime>();
                    if (!inventory.TryRemoveItemWithoutPersistence(reward, 1) || !commitRuntimeData(CaptureCurrentData()))
                        throw new InvalidOperationException("Drop runtime commit rejected.");
                    committed = true;
                    preparedLifetime.Arm(mapController, () => ObjectPoolManager.ReturnOrDeactivate(actor));
                });
                if (ground == null || !ground.activeInHierarchy || context.ActiveGenerationId != generation || context.IsFailed)
                    throw new InvalidOperationException("Prepared drop did not retain its active generation.");
            }
            catch (Exception error)
            {
                if (preparedLifetime != null) preparedLifetime.Disarm();
                if (ground != null) ObjectPoolManager.ReturnOrDeactivate(ground);
                inventory.RestoreSnapshot(inventoryBefore, false);
                if (committed && !GameSaveService.TryCommitRuntimeData(memoryBefore))
                    throw new InvalidOperationException("Drop compensation could not restore its prior runtime data.", error);
                message = "丢出未完成，物品仍在背包。";
                Debug.LogWarning($"[Consumables] Drop rejected and compensated: {error.Message}", inventory);
                return false;
            }
            inventory.NotifyCommittedInventoryChanged();
            return true;
        }
        finally { processing = false; }
    }

    private GameSaveData CaptureCurrentData()
    {
        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData());
        inventory.WriteToSaveData(candidate);
        stats.WriteToSaveData(candidate);
        return candidate;
    }
}
