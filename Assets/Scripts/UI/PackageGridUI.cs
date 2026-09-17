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
        ResolvePlayerInventory();
        ResolveRewardTooltip();

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += Refresh;

        Refresh();
    }

    /// <summary>
    /// 界面禁用时停止监听背包变化，避免重复刷新。
    /// </summary>
    private void OnDisable()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= Refresh;

        if (rewardTooltip != null)
            rewardTooltip.Hide();
    }

    /// <summary>
    /// 根据当前背包数据重建所有背包格子。
    /// </summary>
    public void Refresh()
    {
        ClearSlots();

        if (playerInventory == null || slotPrefab == null || contentRoot == null)
            return;

        IReadOnlyList<InventoryItemStack> itemStacks = playerInventory.ItemStacks; // 当前背包数据。

        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack itemStack = itemStacks[i]; // 当前要显示的物品堆叠。

            if (itemStack == null || itemStack.reward == null || itemStack.amount <= 0)
                continue;

            PackageSlotUI slot = Instantiate(slotPrefab, contentRoot); // 新生成的背包格子。
            slot.SetData(itemStack.reward, itemStack.amount);
            slot.OnSlotSelected += HandleSlotSelected;
            slot.OnTooltipRequested += HandleTooltipRequested;
            slot.OnTooltipHidden += HandleTooltipHidden;
            generatedSlots.Add(slot);
        }
    }

    /// <summary>
    /// 处理某个格子被点击选中后的互斥选中逻辑。
    /// </summary>
    /// <param name="selectedSlot">刚刚被选中的背包格子。</param>
    private void HandleSlotSelected(PackageSlotUI selectedSlot)
    {
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
        if (slot == null || rewardTooltip == null)
            return;

        RectTransform slotRect = slot.GetComponent<RectTransform>(); // 当前格子的矩形变换。
        rewardTooltip.Show(slot.CurrentReward, slotRect);
    }

    /// <summary>
    /// 处理格子请求隐藏奖励说明。
    /// </summary>
    /// <param name="slot">请求隐藏说明的背包格子。</param>
    private void HandleTooltipHidden(PackageSlotUI slot)
    {
        if (rewardTooltip != null)
            rewardTooltip.Hide();
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

            slot.OnSlotSelected -= HandleSlotSelected;
            slot.OnTooltipRequested -= HandleTooltipRequested;
            slot.OnTooltipHidden -= HandleTooltipHidden;
            Destroy(slot.gameObject);
        }

        generatedSlots.Clear();
        currentSelectedSlot = null;

        if (rewardTooltip != null)
            rewardTooltip.Hide();
    }

    /// <summary>
    /// 自动寻找场景中的玩家背包系统，减少手动拖拽引用的步骤。
    /// </summary>
    private void ResolvePlayerInventory()
    {
        if (playerInventory != null)
            return;

#pragma warning disable CS0618
        playerInventory = FindObjectOfType<PlayerInventory>();
#pragma warning restore CS0618
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