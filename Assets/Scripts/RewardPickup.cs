using System;
using UnityEngine;

// 控制地面奖励的拾取逻辑，不负责显示动画。
public class RewardPickup : MonoBehaviour
{
    [SerializeField] private RewardSO reward; // 被拾取的奖励数据。
    [SerializeField, Min(1)] private int amount = 1; // 被拾取的奖励数量。

    private bool hasBeenPickedUp; // 是否已经被玩家拾取。
    private RewardVisualController rewardVisualController; // 奖励视觉控制器。

    public static event Action<RewardSO, int> OnRewardPickedUp; // 奖励被拾取时通知背包系统。

    // 奖励启用或从对象池取出时，重置拾取状态。
    private void OnEnable()
    {
        hasBeenPickedUp = false;
    }

    // 初始化奖励数据和数量。
    public void Initialize(RewardSO rewardData, int rewardAmount)
    {
        reward = rewardData;
        amount = Mathf.Max(1, rewardAmount);
    }

    // 初始化拾取碰撞体和视觉控制器引用。
    private void Awake()
    {
        Collider2D pickupCollider = GetComponent<Collider2D>();
        rewardVisualController = GetComponentInChildren<RewardVisualController>();

        if (pickupCollider == null)
            pickupCollider = gameObject.AddComponent<CircleCollider2D>();

        pickupCollider.isTrigger = true;
    }

    // 玩家进入拾取范围时收取奖励。
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp || !IsPlayer(other))
            return;

        hasBeenPickedUp = true;
        OnRewardPickedUp?.Invoke(reward, amount);

        if (rewardVisualController != null)
            rewardVisualController.MarkCollected();

        ObjectPoolManager.ReturnOrDeactivate(gameObject);
    }

    // 判断进入触发器的对象是否是玩家。
    private bool IsPlayer(Collider2D other)
    {
        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }
}
