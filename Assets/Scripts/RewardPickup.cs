using System;
using UnityEngine;

// 控制地面奖励的拾取逻辑，不负责显示动画。
public class RewardPickup : MonoBehaviour
{
    [SerializeField] private RewardSO reward; // 被拾取的奖励数据。
    [SerializeField, Min(1)] private int amount = 1; // 被拾取的奖励数量。

    private bool hasBeenPickedUp; // 是否已经被玩家拾取。
    private RewardVisualController rewardVisualController; // 奖励视觉控制器。

    /// <summary>
    /// 背包系统确认奖励已经成功写入后发出的通知。
    /// </summary>
    public static event Action<RewardSO, int> OnRewardPickedUp; // 奖励成功拾取通知。

    /// <summary>
    /// 背包系统尝试接收奖励的验证事件；返回 false 时保留地面奖励。
    /// </summary>
    public static event Func<RewardSO, int, bool> OnRewardPickupRequested; // 奖励接收验证事件。

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
        int finalAmount = reward == null ? amount : reward.GetPickupAmount(amount); // 最终加入背包的数量。
        if (!TryAcceptReward(finalAmount))
        {
            hasBeenPickedUp = false;
            return;
        }

        OnRewardPickedUp?.Invoke(reward, finalAmount);

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

    /// <summary>
    /// 请求背包确认奖励已经成功接收。
    /// </summary>
    /// <param name="finalAmount">最终加入背包的数量。</param>
    /// <returns>至少一个背包系统确认成功时返回 true。</returns>
    private bool TryAcceptReward(int finalAmount)
    {
        if (OnRewardPickupRequested == null)
        {
            Debug.LogWarning("场景中没有可用的 PlayerInventory（玩家背包），地面奖励不会被回收。", this);
            return false;
        }

        Delegate[] handlers = OnRewardPickupRequested.GetInvocationList(); // 所有奖励接收者。

        for (int i = 0; i < handlers.Length; i++)
        {
            Func<RewardSO, int, bool> handler =
                handlers[i] as Func<RewardSO, int, bool>;

            if (handler == null || !handler.Invoke(reward, finalAmount))
                return false;
        }

        return true;
    }
}
