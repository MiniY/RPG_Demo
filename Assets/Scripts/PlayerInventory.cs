using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存一种奖励在背包中的数量。
/// </summary>
[Serializable]
public class InventoryItemStack
{
    /// <summary>
    /// 奖励的静态数据，例如名称、图标、说明。
    /// </summary>
    public RewardSO reward; // 奖励数据。

    /// <summary>
    /// 玩家当前拥有这种奖励的数量。
    /// </summary>
    public int amount; // 奖励数量。

    /// <summary>
    /// 创建一个新的奖励数量记录。
    /// </summary>
    /// <param name="rewardData">要记录的奖励数据。</param>
    /// <param name="rewardAmount">要记录的奖励数量。</param>
    public InventoryItemStack(RewardSO rewardData, int rewardAmount)
    {
        reward = rewardData;
        amount = rewardAmount;
    }
}

/// <summary>
/// 管理玩家背包中的奖励数量，不负责背包界面显示。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    /// <summary>
    /// 背包内容发生变化时通知界面刷新。
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// 玩家当前拥有的奖励列表，保留在 Inspector 中方便调试查看。
    /// </summary>
    [SerializeField] private List<InventoryItemStack> itemStacks = new List<InventoryItemStack>(); // 背包物品列表。

    /// <summary>
    /// 提供给 UI 读取的背包内容，只允许读取，不允许外部直接修改。
    /// </summary>
    public IReadOnlyList<InventoryItemStack> ItemStacks => itemStacks;

    /// <summary>
    /// 脚本启用时开始监听奖励拾取事件。
    /// </summary>
    private void OnEnable()
    {
        RewardPickup.OnRewardPickedUp += HandleRewardPickedUp;
    }

    /// <summary>
    /// 脚本禁用时停止监听奖励拾取事件，避免对象销毁后还收到通知。
    /// </summary>
    private void OnDisable()
    {
        RewardPickup.OnRewardPickedUp -= HandleRewardPickedUp;
    }

    /// <summary>
    /// 处理地面奖励被拾取后的背包数量增加。
    /// </summary>
    /// <param name="reward">被拾取的奖励数据。</param>
    /// <param name="amount">被拾取的奖励数量。</param>
    private void HandleRewardPickedUp(RewardSO reward, int amount)
    {
        AddItem(reward, amount);
    }

    /// <summary>
    /// 往背包中增加指定数量的奖励。
    /// </summary>
    /// <param name="reward">要增加的奖励数据。</param>
    /// <param name="amount">要增加的奖励数量。</param>
    public void AddItem(RewardSO reward, int amount)
    {
        if (reward == null || amount <= 0)
            return;

        InventoryItemStack existingStack = FindStack(reward); // 已经存在的奖励记录。

        if (existingStack != null)
        {
            existingStack.amount += amount;
        }
        else
        {
            InventoryItemStack newStack = new InventoryItemStack(reward, amount); // 新的奖励记录。
            itemStacks.Add(newStack);
        }

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 从背包中扣除指定数量的奖励，数量不足时不会扣除。
    /// </summary>
    /// <param name="reward">要扣除的奖励数据。</param>
    /// <param name="amount">要扣除的奖励数量。</param>
    /// <returns>扣除成功返回 true，数量不足或参数无效返回 false。</returns>
    public bool RemoveItem(RewardSO reward, int amount)
    {
        if (reward == null || amount <= 0)
            return false;

        InventoryItemStack existingStack = FindStack(reward); // 要扣除的奖励记录。

        if (existingStack == null || existingStack.amount < amount)
            return false;

        existingStack.amount -= amount;

        if (existingStack.amount <= 0)
            itemStacks.Remove(existingStack);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 判断背包中是否拥有足够数量的某种奖励。
    /// </summary>
    /// <param name="reward">要检查的奖励数据。</param>
    /// <param name="amount">需要的奖励数量。</param>
    /// <returns>数量足够返回 true，否则返回 false。</returns>
    public bool HasItem(RewardSO reward, int amount)
    {
        if (reward == null || amount <= 0)
            return false;

        return GetItemAmount(reward) >= amount;
    }

    /// <summary>
    /// 获取背包中某种奖励的当前数量。
    /// </summary>
    /// <param name="reward">要查询的奖励数据。</param>
    /// <returns>当前拥有数量，没有该奖励时返回 0。</returns>
    public int GetItemAmount(RewardSO reward)
    {
        InventoryItemStack existingStack = FindStack(reward); // 要查询的奖励记录。
        return existingStack == null ? 0 : existingStack.amount;
    }

    /// <summary>
    /// 在背包列表中查找指定奖励的数量记录。
    /// </summary>
    /// <param name="reward">要查找的奖励数据。</param>
    /// <returns>找到时返回奖励数量记录，找不到时返回 null。</returns>
    private InventoryItemStack FindStack(RewardSO reward)
    {
        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack currentStack = itemStacks[i]; // 当前正在检查的奖励记录。

            if (currentStack.reward == reward)
                return currentStack;
        }

        return null;
    }
}
