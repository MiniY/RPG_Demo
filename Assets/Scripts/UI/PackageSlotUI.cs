using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 控制单个背包物品格子的显示、点击和选中状态。
/// </summary>
public class PackageSlotUI : MonoBehaviour, IPointerClickHandler
{
    /// <summary>
    /// 当前格子被点击选中时发出的通知。
    /// </summary>
    public event Action<PackageSlotUI> OnSlotSelected;

    /// <summary>
    /// 用来显示奖励图标的图片组件。
    /// </summary>
    [SerializeField] private Image itemIcon; // 物品图标图片。

    /// <summary>
    /// 用来显示奖励数量的文本组件。
    /// </summary>
    [SerializeField] private TMP_Text amountText; // 物品数量文本。

    /// <summary>
    /// 表示当前格子被选中的视觉对象。
    /// </summary>
    [SerializeField] private GameObject selectedObject; // 选中状态对象。

    /// <summary>
    /// 当前格子显示的奖励数据。
    /// </summary>
    private RewardSO currentReward; // 当前奖励数据。

    /// <summary>
    /// 当前格子显示的奖励数量。
    /// </summary>
    private int currentAmount; // 当前奖励数量。

    /// <summary>
    /// 当前格子显示的奖励数据，只允许外部读取。
    /// </summary>
    public RewardSO CurrentReward => currentReward;

    /// <summary>
    /// 当前格子显示的奖励数量，只允许外部读取。
    /// </summary>
    public int CurrentAmount => currentAmount;

    /// <summary>
    /// 脚本第一次加载时，默认关闭选中状态。
    /// </summary>
    private void Awake()
    {
        SetSelected(false);
    }

    /// <summary>
    /// 在编辑器中添加脚本时，自动寻找常用子物体并绑定引用。
    /// </summary>
    private void Reset()
    {
        Transform itemIconTransform = transform.Find("ItemIcon"); // 物品图标子物体。
        Transform amountTextTransform = transform.Find("AmountText"); // 数量文本子物体。
        Transform selectedTransform = transform.Find("Selected"); // 选中状态子物体。

        if (itemIconTransform != null)
            itemIcon = itemIconTransform.GetComponent<Image>();

        if (amountTextTransform != null)
            amountText = amountTextTransform.GetComponent<TMP_Text>();

        if (selectedTransform != null)
            selectedObject = selectedTransform.gameObject;
    }

    /// <summary>
    /// 设置当前格子要显示的奖励数据和数量。
    /// </summary>
    /// <param name="reward">要显示的奖励数据。</param>
    /// <param name="amount">要显示的奖励数量。</param>
    public void SetData(RewardSO reward, int amount)
    {
        currentReward = reward;
        currentAmount = Mathf.Max(0, amount);

        RefreshIcon();
        RefreshAmountText();
        SetSelected(false);
    }

    /// <summary>
    /// 设置当前格子的选中视觉状态。
    /// </summary>
    /// <param name="isSelected">是否显示选中状态。</param>
    public void SetSelected(bool isSelected)
    {
        if (selectedObject != null)
            selectedObject.SetActive(isSelected);
    }

    /// <summary>
    /// 鼠标点击当前格子时，显示选中状态并通知外部。
    /// </summary>
    /// <param name="eventData">鼠标点击事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentReward == null)
            return;

        SetSelected(true);
        OnSlotSelected?.Invoke(this);
    }

    /// <summary>
    /// 根据当前奖励数据刷新物品图标。
    /// </summary>
    private void RefreshIcon()
    {
        if (itemIcon == null)
            return;

        Sprite iconSprite = currentReward == null ? null : currentReward.itemIcon; // 当前要显示的图标。

        itemIcon.sprite = iconSprite;
        itemIcon.enabled = iconSprite != null;
    }

    /// <summary>
    /// 根据当前奖励数量刷新数量文本。
    /// </summary>
    private void RefreshAmountText()
    {
        if (amountText == null)
            return;

        amountText.gameObject.SetActive(currentReward != null);
        amountText.text = "x" + currentAmount;
    }
}
