using System;
using System.Collections;
using UnityEngine;

// 控制地面奖励的拾取与余量，不负责背包、属性或存档权威。
public class RewardPickup : MonoBehaviour
{
    [SerializeField] private RewardSO reward;
    [SerializeField, Min(1)] private int amount = 1;
    private bool amountIsOwnedUnits;
    private bool hasBeenPickedUp;
    private int lease;
    private RewardVisualController rewardVisualController;

    public static event Action<RewardSO, int> OnRewardPickedUp;
    public static event Action<RewardSO, RewardPickupResult> OnRewardProcessed;
    public static event Func<RewardSO, int, RewardPickupResult> OnRewardPickupRequested;

    public int RemainingAmount => amount;

    private void OnEnable()
    {
        lease++;
        hasBeenPickedUp = false;
    }

    private void OnDisable()
    {
        lease++;
        StopAllCoroutines();
        if (rewardVisualController != null) rewardVisualController.CancelPresentation();
        if (amount == 0 && gameObject.activeSelf) ObjectPoolManager.ReturnOrDeactivate(gameObject);
    }

    public void Initialize(RewardSO rewardData, int rewardAmount) => Initialize(rewardData, rewardAmount, false);
    public void InitializeOwnedUnits(RewardSO rewardData, int rewardAmount) => Initialize(rewardData, rewardAmount, true);

    private void Initialize(RewardSO rewardData, int rewardAmount, bool ownedUnits)
    {
        lease++;
        StopAllCoroutines();
        reward = rewardData;
        amount = Mathf.Max(1, rewardAmount);
        amountIsOwnedUnits = ownedUnits;
        hasBeenPickedUp = false;
        enabled = true;
        if (rewardVisualController != null) rewardVisualController.ResetForSpawn();
    }

    private void Awake()
    {
        Collider2D pickupCollider = GetComponent<Collider2D>();
        rewardVisualController = GetComponentInChildren<RewardVisualController>(true);
        if (pickupCollider == null) pickupCollider = gameObject.AddComponent<CircleCollider2D>();
        pickupCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp || !isActiveAndEnabled || other == null || !IsPlayer(other)) return;
        if (reward == null)
        {
            Debug.LogWarning("[Consumables] Pickup definition is missing; ground quantity retained.", this);
            return;
        }
        hasBeenPickedUp = true;
        StartCoroutine(Settle(other.GetComponentInParent<PlayerInventory>(), lease));
    }

    private IEnumerator Settle(PlayerInventory expectedInventory, int currentLease)
    {
        RewardSO definition = reward;
        int remaining = definition == null || amountIsOwnedUnits ? amount : definition.GetPickupAmount(amount);
        bool perUnit = definition != null && definition.IsRecoveryConsumable;
        bool inventoryPresentation = false;
        int unitsThisFrame = 0;
        while (remaining > 0 && IsCurrentLease(currentLease))
        {
            int requested = perUnit ? 1 : remaining;
            RewardPickupResult result = TryAcceptReward(definition, requested, expectedInventory, out PlayerInventory receiver);
            if (!result.Accepted) break;
            if (!IsCurrentLease(currentLease)) yield break;

            // No observers run between the business commit and this ground quantity update.
            remaining -= result.AcceptedAmount;
            amount = remaining;
            amountIsOwnedUnits = true;
            inventoryPresentation |= result.Kind == RewardSettlementKind.Inventory;
            receiver.NotifyCommittedGroundSettlement(result);
            NotifyCommittedOutcome(definition, result);
            if (!IsCurrentLease(currentLease)) yield break;
            if (!perUnit) break;
            if (++unitsThisFrame == 32 && remaining > 0)
            {
                unitsThisFrame = 0;
                yield return null;
            }
        }

        if (!IsCurrentLease(currentLease)) yield break;
        if (inventoryPresentation && rewardVisualController != null)
        {
            IEnumerator visual = rewardVisualController.PlayInventoryHop();
            float started = Time.realtimeSinceStartup;
            while (IsCurrentLease(currentLease) && Time.realtimeSinceStartup - started < .5f)
            {
                bool next;
                try { next = visual.MoveNext(); }
                catch (Exception error)
                {
                    Debug.LogError($"[Consumables] Committed pickup presentation failed: {error}", this);
                    break;
                }
                if (!next) break;
                yield return null;
            }
            if (IsCurrentLease(currentLease) && rewardVisualController != null) rewardVisualController.CancelPresentation();
        }
        if (!IsCurrentLease(currentLease)) yield break;
        if (remaining == 0) ObjectPoolManager.ReturnOrDeactivate(gameObject);
        else
        {
            if (inventoryPresentation && rewardVisualController != null) rewardVisualController.ResetForSpawn();
            hasBeenPickedUp = false;
        }
    }

    private bool IsCurrentLease(int value) => value == lease && isActiveAndEnabled;
    private bool IsPlayer(Collider2D other) => other.CompareTag("Player") || other.transform.root.CompareTag("Player");

    private void NotifyCommittedOutcome(RewardSO definition, RewardPickupResult result)
    {
        if (result.Kind == RewardSettlementKind.Inventory)
        {
            Action<RewardSO, int> observers = OnRewardPickedUp;
            if (observers != null)
                foreach (Action<RewardSO, int> observer in observers.GetInvocationList())
                    try { observer.Invoke(definition, result.AcceptedAmount); }
                    catch (Exception error) { Debug.LogError($"[ADR02] Pickup committed; observer {observer.Method.Name} failed: {error}", this); }
        }
        Action<RewardSO, RewardPickupResult> processed = OnRewardProcessed;
        if (processed != null)
            foreach (Action<RewardSO, RewardPickupResult> observer in processed.GetInvocationList())
                try { observer.Invoke(definition, result); }
                catch (Exception error) { Debug.LogError($"[Consumables] Settlement committed; observer {observer.Method.Name} failed: {error}", this); }
    }

    private RewardPickupResult TryAcceptReward(RewardSO definition, int requested, PlayerInventory expectedInventory, out PlayerInventory receiver)
    {
        receiver = null;
        Func<RewardSO, int, RewardPickupResult> request = OnRewardPickupRequested;
        Delegate[] handlers = request?.GetInvocationList();
        int receiverCount = handlers == null ? 0 : handlers.Length;
        if (receiverCount != 1)
        {
            Debug.LogWarning($"[ADR02] Pickup rejected before mutation: expected one receiver, found {receiverCount}; reward={definition?.RewardId}, amount={requested}.", this);
            return default;
        }
        var handler = (Func<RewardSO, int, RewardPickupResult>)handlers[0];
        PlayerInventory inventory = handler.Target as PlayerInventory;
        if (inventory == null || inventory != expectedInventory || !inventory.isActiveAndEnabled ||
            !inventory.gameObject.scene.IsValid() || !inventory.gameObject.scene.isLoaded ||
            !inventory.IsInitialized || !inventory.IsPickupRequestReceiver(handler))
        {
            Debug.LogWarning($"[ADR02] Pickup rejected before mutation: stale, unknown or mismatched receiver {handler.Method.DeclaringType?.Name}.{handler.Method.Name}.", this);
            return default;
        }
        receiver = inventory;
        return handler.Invoke(definition, requested);
    }
}
