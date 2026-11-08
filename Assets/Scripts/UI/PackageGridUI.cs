using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据玩家背包数据生成和刷新背包物品格子。
/// </summary>
public class PackageGridUI : MonoBehaviour
{
    /// <summary>
    /// 提供背包物品数据的玩家背包系统。
    /// </summary>
    [SerializeField] private PlayerInventory playerInventory; // 玩家背包系统。

    /// <summary>
    /// 用来生成背包格子的预制体。
    /// </summary>
    [SerializeField] private PackageSlotUI slotPrefab; // 背包格子预制体。

    /// <summary>
    /// 承载所有背包格子的父物体。
    /// </summary>
    [SerializeField] private Transform contentRoot; // 背包内容根物体。

    /// <summary>
    /// 用来显示奖励名称和说明的提示框。
    /// </summary>
    [SerializeField] private PackageRewardTooltipUI rewardTooltip; // 奖励说明提示框。

    /// <summary>
    /// 当前已经生成出来的背包格子列表。
    /// </summary>
    private readonly List<PackageSlotUI> generatedSlots = new List<PackageSlotUI>(); // 已生成格子列表。

    /// <summary>
    /// 当前被选中的背包格子。
    /// </summary>
    private PackageSlotUI currentSelectedSlot; // 当前选中格子。
    private PackageSlotUI currentTooltipSlot;
    private PlayerInventory subscribedInventory;
    private readonly Dictionary<string, PackageSlotUI> slotsByRewardId =
        new Dictionary<string, PackageSlotUI>(StringComparer.Ordinal);

    /// <summary>
    /// 脚本加载时补全没有手动绑定的引用。
    /// </summary>
    private void Awake()
    {
        if (contentRoot == null)
            contentRoot = transform;

        ResolveRewardTooltip();
    }

    /// <summary>
    /// 界面启用时监听背包变化，并立即刷新一次界面。
    /// </summary>
    private void OnEnable()
    {
        UnsubscribeInventory();
        ResolvePlayerInventory();
        ResolveRewardTooltip();

        if (playerInventory != null)
        {
            playerInventory.EnsureInitializedForRuntime();
            subscribedInventory = playerInventory;
            subscribedInventory.OnInventoryChanged += Refresh;
        }

        Refresh();
    }

    /// <summary>
    /// 界面禁用时停止监听背包变化，避免重复刷新。
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeInventory();
        ClearSlots();
    }

    private void OnDestroy()
    {
        UnsubscribeInventory();
        ClearSlots();
    }

    private void UnsubscribeInventory()
    {
        if (subscribedInventory != null)
            subscribedInventory.OnInventoryChanged -= Refresh;
        subscribedInventory = null;
    }

    /// <summary>
    /// 根据权威背包数据同步格子，复用具有唯一稳定身份的已有视图。
    /// </summary>
    public void Refresh()
    {
        if (!isActiveAndEnabled || playerInventory == null || slotPrefab == null || contentRoot == null)
        {
            ClearSlots();
            return;
        }

        // Unity 已销毁的对象仍可能保留托管引用；外部重建格子时也清理展示关联。
        if (!ReferenceEquals(currentSelectedSlot, null) && currentSelectedSlot == null)
        {
            currentSelectedSlot = null;
            HideTooltip();
        }

        IReadOnlyList<InventoryItemStack> itemStacks = playerInventory.ItemStacks; // 当前背包数据。
        var identityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack stack = itemStacks[i];
            if (stack == null || stack.Reward == null || stack.Amount <= 0 ||
                string.IsNullOrWhiteSpace(stack.Reward.RewardId))
                continue;
            string id = stack.Reward.RewardId;
            identityCounts.TryGetValue(id, out int count);
            identityCounts[id] = count + 1;
        }

        var nextSlots = new List<PackageSlotUI>();
        var nextByRewardId = new Dictionary<string, PackageSlotUI>(StringComparer.Ordinal);
        var retained = new HashSet<PackageSlotUI>();

        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack itemStack = itemStacks[i]; // 当前要显示的物品堆叠。

            if (itemStack == null || itemStack.Reward == null || itemStack.Amount <= 0)
                continue;

            string id = itemStack.Reward.RewardId;
            bool stableIdentity = !string.IsNullOrWhiteSpace(id) && identityCounts[id] == 1;
            PackageSlotUI slot = null;
            if (stableIdentity && slotsByRewardId.TryGetValue(id, out PackageSlotUI existing) &&
                existing != null && existing.CurrentReward != null &&
                string.Equals(existing.CurrentReward.RewardId, id, StringComparison.Ordinal))
                slot = existing;

            // 空/重复 ID 不合并，也不修复业务 ID；异常行仅使用本次刷新的临时视图。
            if (slot == null)
            {
                slot = Instantiate(slotPrefab, contentRoot);
                slot.OnSlotSelected += HandleSlotSelected;
                slot.OnDropRequested += HandleDropRequested;
                slot.OnTooltipRequested += HandleTooltipRequested;
                slot.OnTooltipHidden += HandleTooltipHidden;
            }
            if (slot.CurrentReward != itemStack.Reward || slot.CurrentAmount != itemStack.Amount)
            {
                slot.SetData(itemStack.Reward, itemStack.Amount);
                slot.SetSelected(slot == currentSelectedSlot);
            }
            slot.transform.SetSiblingIndex(nextSlots.Count);
            nextSlots.Add(slot);
            retained.Add(slot);
            if (stableIdentity)
                nextByRewardId.Add(id, slot);
        }

        for (int i = 0; i < generatedSlots.Count; i++)
            if (generatedSlots[i] != null && !retained.Contains(generatedSlots[i]))
                ReleaseSlot(generatedSlots[i]);

        generatedSlots.Clear();
        generatedSlots.AddRange(nextSlots);
        slotsByRewardId.Clear();
        foreach (KeyValuePair<string, PackageSlotUI> pair in nextByRewardId)
            slotsByRewardId.Add(pair.Key, pair.Value);

        if (currentTooltipSlot != null && retained.Contains(currentTooltipSlot) && rewardTooltip != null)
            rewardTooltip.Show(currentTooltipSlot.CurrentReward, currentTooltipSlot.GetComponent<RectTransform>());
        else
            HideTooltip();
    }

    /// <summary>
    /// 处理某个格子被点击选中后的互斥选中逻辑。
    /// </summary>
    /// <param name="selectedSlot">刚刚被选中的背包格子。</param>
    private void HandleSlotSelected(PackageSlotUI selectedSlot)
    {
        if (!isActiveAndEnabled || selectedSlot == null || !generatedSlots.Contains(selectedSlot))
            return;
        if (currentSelectedSlot != null && currentSelectedSlot != selectedSlot)
            currentSelectedSlot.SetSelected(false);

        currentSelectedSlot = selectedSlot;
        currentSelectedSlot.SetSelected(true);
    }

    /// <summary>
    /// 处理格子请求显示奖励说明。
    /// </summary>
    /// <param name="slot">请求显示说明的背包格子。</param>
    private void HandleTooltipRequested(PackageSlotUI slot)
    {
        if (!isActiveAndEnabled || slot == null || rewardTooltip == null || !generatedSlots.Contains(slot))
            return;

        currentTooltipSlot = slot;
        RectTransform slotRect = slot.GetComponent<RectTransform>(); // 当前格子的矩形变换。
        rewardTooltip.Show(slot.CurrentReward, slotRect);
    }

    /// <summary>
    /// 处理格子请求隐藏奖励说明。
    /// </summary>
    /// <param name="slot">请求隐藏说明的背包格子。</param>
    private void HandleTooltipHidden(PackageSlotUI slot)
    {
        if (slot == currentTooltipSlot)
            HideTooltip();
    }

    public bool BindInventory(PlayerInventory inventory)
    {
        if (playerInventory != null && playerInventory != inventory && playerInventory.gameObject.scene.IsValid())
        { Debug.LogWarning("背包视图与当前玩家引用冲突，未覆盖原绑定。", this); return false; }
        UnsubscribeInventory();
        playerInventory = inventory;
        if (isActiveAndEnabled && playerInventory != null)
        {
            subscribedInventory = playerInventory;
            subscribedInventory.OnInventoryChanged += Refresh;
            Refresh();
        }
        return true;
    }

    private void HandleDropRequested(PackageSlotUI slot)
    {
        if (!isActiveAndEnabled || slot == null || !generatedSlots.Contains(slot) || playerInventory == null) return;
        PackageUIPage page = GetComponentInParent<PackageUIPage>();
        UIRoot root = page != null ? page.OwningRoot : null;
        if (root == null || !root.CanUseWorkspace(UIRoutes.Package.Id) ||
            !root.Manager.TryFind(UIRoutes.Package, root.Scope, out _)) return;
        if (!playerInventory.TryDropOne(slot.CurrentReward, out string message))
            Debug.LogWarning(message, this);
    }

    public void SuspendHover() => HideTooltip();

    private void HideTooltip()
    {
        currentTooltipSlot = null;
        if (rewardTooltip != null)
            rewardTooltip.Hide();
    }

    private void ReleaseSlot(PackageSlotUI slot)
    {
        if (slot == currentSelectedSlot)
        {
            slot.SetSelected(false);
            currentSelectedSlot = null;
            HideTooltip();
        }
        else if (slot == currentTooltipSlot)
            HideTooltip();
        slot.OnSlotSelected -= HandleSlotSelected;
        slot.OnDropRequested -= HandleDropRequested;
        slot.OnTooltipRequested -= HandleTooltipRequested;
        slot.OnTooltipHidden -= HandleTooltipHidden;
        // 停用旧行后再延迟销毁，避免旧格子继续布局或触发 Hover。
        slot.gameObject.SetActive(false);
        Destroy(slot.gameObject);
    }

    /// <summary>
    /// 清除旧格子和旧的选中记录。
    /// </summary>
    private void ClearSlots()
    {
        for (int i = generatedSlots.Count - 1; i >= 0; i--)
        {
            PackageSlotUI slot = generatedSlots[i]; // 当前要清理的背包格子。

            if (slot == null)
                continue;

            ReleaseSlot(slot);
        }

        generatedSlots.Clear();
        slotsByRewardId.Clear();
        currentSelectedSlot = null;
        HideTooltip();
    }

    /// <summary>
    /// 自动寻找场景中的玩家背包系统，减少手动拖拽引用的步骤。
    /// </summary>
    private void ResolvePlayerInventory()
    {
        if (playerInventory != null && playerInventory.gameObject.scene.IsValid())
            return;

        playerInventory = GameSession.Instance != null ? GameSession.Instance.Inventory : null;
    }

    /// <summary>
    /// 自动寻找背包奖励说明提示框。
    /// </summary>
    private void ResolveRewardTooltip()
    {
        if (rewardTooltip != null)
            return;

#pragma warning disable CS0618
        rewardTooltip = FindObjectOfType<PackageRewardTooltipUI>();
#pragma warning restore CS0618
    }
}
