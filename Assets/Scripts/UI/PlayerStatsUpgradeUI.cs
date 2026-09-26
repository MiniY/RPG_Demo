using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理 CharacterPanelUI（角色展示面板）中的玩家属性加点和减点界面。
/// </summary>
public class PlayerStatsUpgradeUI : MonoBehaviour
{
    [Header("属性文本")]

    /// <summary>
    /// 生命值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text healthNumber;

    /// <summary>
    /// 魔法值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text manaNumber;

    /// <summary>
    /// 体力值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text staminaNumber;

    [Header("生命值按钮")]

    /// <summary>
    /// 增加最大生命值按钮。
    /// </summary>
    [SerializeField] private Button healthAddButton;

    /// <summary>
    /// 减少最大生命值按钮。
    /// </summary>
    [SerializeField] private Button healthSubtractButton;

    [Header("魔法值按钮")]

    /// <summary>
    /// 增加最大魔法值按钮。
    /// </summary>
    [SerializeField] private Button manaAddButton;

    /// <summary>
    /// 减少最大魔法值按钮。
    /// </summary>
    [SerializeField] private Button manaSubtractButton;

    [Header("体力值按钮")]

    /// <summary>
    /// 增加最大体力值按钮。
    /// </summary>
    [SerializeField] private Button staminaAddButton;

    /// <summary>
    /// 减少最大体力值按钮。
    /// </summary>
    [SerializeField] private Button staminaSubtractButton;

    [Header("提示")]

    /// <summary>
    /// 操作失败时显示原因的文本。
    /// </summary>
    [SerializeField] private TMP_Text warningText;

    /// <summary>
    /// 控制警告文本整体显示状态的根对象。
    /// </summary>
    private GameObject warningRoot;

    /// <summary>
    /// 玩家属性运行时数据。
    /// </summary>
    private PlayerStatsRuntime playerStatsRuntime;

    /// <summary>
    /// 玩家成长服务。
    /// </summary>
    private PlayerProgressionService progressionService;

    /// <summary>
    /// 玩家背包系统，用于读取金币数量。
    /// </summary>
    private PlayerInventory playerInventory;

    /// <summary>
    /// 当前按钮和数据事件是否已经绑定。
    /// </summary>
    private bool isBound;

    /// <summary>
    /// 脚本启用时绑定数据事件和按钮事件。
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
        SubscribeDataEvents();
        RefreshView();
    }

    /// <summary>
    /// 脚本禁用时解除数据事件和按钮事件。
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeDataEvents();
        UnbindEvents();
    }

    /// <summary>
    /// 自动补全场景引用，减少手动拖拽。
    /// </summary>
    private void ResolveReferences()
    {
        healthNumber = healthNumber != null ? healthNumber : FindChildComponent<TMP_Text>("HealthNumber");
        manaNumber = manaNumber != null ? manaNumber : FindChildComponent<TMP_Text>("ManaNumber");
        staminaNumber = staminaNumber != null ? staminaNumber : FindChildComponent<TMP_Text>("StaminaNumber");

        healthAddButton = healthAddButton != null ? healthAddButton : FindChildComponent<Button>("HealthAddButton");
        healthSubtractButton = healthSubtractButton != null ? healthSubtractButton : FindChildComponent<Button>("HealthSubtractButton");
        manaAddButton = manaAddButton != null ? manaAddButton : FindChildComponent<Button>("ManaAddButton");
        manaSubtractButton = manaSubtractButton != null ? manaSubtractButton : FindChildComponent<Button>("ManaSubtractButton");
        staminaAddButton = staminaAddButton != null ? staminaAddButton : FindChildComponent<Button>("StaminaAddButton");
        staminaSubtractButton = staminaSubtractButton != null ? staminaSubtractButton : FindChildComponent<Button>("StaminaSubtractButton");

        if (warningText == null)
        {
            warningRoot = FindNamedObject("WarningText");

            if (warningRoot != null)
                warningText = warningRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (warningRoot == null && warningText != null)
        {
            warningRoot = warningText.transform.parent != null &&
                          warningText.transform.parent.name == "WarningText"
                ? warningText.transform.parent.gameObject
                : warningText.gameObject;
        }

        if (GameSession.Instance != null)
        {
            playerStatsRuntime = GameSession.Instance.PlayerStats;
            progressionService = GameSession.Instance.ProgressionService;
        }

        if (playerInventory == null)
        {
#pragma warning disable CS0618
            playerInventory = FindObjectOfType<PlayerInventory>();
#pragma warning restore CS0618
        }
    }

    /// <summary>
    /// 绑定六个属性加减按钮。
    /// </summary>
    private void BindEvents()
    {
        if (isBound)
            return;

        if (healthAddButton != null)
            healthAddButton.onClick.AddListener(IncreaseHealth);
        if (healthSubtractButton != null)
            healthSubtractButton.onClick.AddListener(DecreaseHealth);
        if (manaAddButton != null)
            manaAddButton.onClick.AddListener(IncreaseMana);
        if (manaSubtractButton != null)
            manaSubtractButton.onClick.AddListener(DecreaseMana);
        if (staminaAddButton != null)
            staminaAddButton.onClick.AddListener(IncreaseStamina);
        if (staminaSubtractButton != null)
            staminaSubtractButton.onClick.AddListener(DecreaseStamina);

        isBound = true;
    }

    /// <summary>
    /// 解除六个属性加减按钮的监听。
    /// </summary>
    private void UnbindEvents()
    {
        if (!isBound)
            return;

        if (healthAddButton != null)
            healthAddButton.onClick.RemoveListener(IncreaseHealth);
        if (healthSubtractButton != null)
            healthSubtractButton.onClick.RemoveListener(DecreaseHealth);
        if (manaAddButton != null)
            manaAddButton.onClick.RemoveListener(IncreaseMana);
        if (manaSubtractButton != null)
            manaSubtractButton.onClick.RemoveListener(DecreaseMana);
        if (staminaAddButton != null)
            staminaAddButton.onClick.RemoveListener(IncreaseStamina);
        if (staminaSubtractButton != null)
            staminaSubtractButton.onClick.RemoveListener(DecreaseStamina);

        isBound = false;
    }

    /// <summary>
    /// 监听统一玩家属性和背包数据变化。
    /// </summary>
    private void SubscribeDataEvents()
    {
        if (playerStatsRuntime != null)
            playerStatsRuntime.OnStatsChanged += RefreshView;

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += RefreshView;
    }

    /// <summary>
    /// 解除统一玩家属性和背包数据变化监听。
    /// </summary>
    private void UnsubscribeDataEvents()
    {
        if (playerStatsRuntime != null)
            playerStatsRuntime.OnStatsChanged -= RefreshView;

        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshView;
    }

    /// <summary>
    /// 增加一点最大生命值。
    /// </summary>
    private void IncreaseHealth()
    {
        TryIncrease(PlayerStatType.Health);
    }

    /// <summary>
    /// 减少一点最大生命值。
    /// </summary>
    private void DecreaseHealth()
    {
        TryDecrease(PlayerStatType.Health);
    }

    /// <summary>
    /// 增加一点最大魔法值。
    /// </summary>
    private void IncreaseMana()
    {
        TryIncrease(PlayerStatType.Mana);
    }

    /// <summary>
    /// 减少一点最大魔法值。
    /// </summary>
    private void DecreaseMana()
    {
        TryDecrease(PlayerStatType.Mana);
    }

    /// <summary>
    /// 增加一点最大体力值。
    /// </summary>
    private void IncreaseStamina()
    {
        TryIncrease(PlayerStatType.Stamina);
    }

    /// <summary>
    /// 减少一点最大体力值。
    /// </summary>
    private void DecreaseStamina()
    {
        TryDecrease(PlayerStatType.Stamina);
    }

    /// <summary>
    /// 请求成长服务增加一个属性点。
    /// </summary>
    /// <param name="statType">要增加的属性类型。</param>
    private void TryIncrease(PlayerStatType statType)
    {
        ResolveReferences();

        string failureMessage = string.Empty;
        bool success = progressionService != null &&
                       progressionService.TryIncrease(statType, out failureMessage);

        if (!success)
        {
            ShowWarning(failureMessage);
            return;
        }

        ClearWarning();
    }

    /// <summary>
    /// 请求成长服务减少一个属性点。
    /// </summary>
    /// <param name="statType">要减少的属性类型。</param>
    private void TryDecrease(PlayerStatType statType)
    {
        ResolveReferences();

        string failureMessage = string.Empty;
        bool success = progressionService != null &&
                       progressionService.TryDecrease(statType, out failureMessage);

        if (!success)
        {
            ShowWarning(failureMessage);
            return;
        }

        ClearWarning();
    }

    /// <summary>
    /// 刷新三个属性文本和六个按钮的可交互状态。
    /// </summary>
    public void RefreshView()
    {
        ResolveReferences();

        if (playerStatsRuntime == null)
            return;

        playerStatsRuntime.EnsureInitializedForRuntime();
        playerInventory?.EnsureInitializedForRuntime();

        SetStatText(healthNumber, PlayerStatType.Health);
        SetStatText(manaNumber, PlayerStatType.Mana);
        SetStatText(staminaNumber, PlayerStatType.Stamina);

        int goldAmount = GetGoldAmount();
        int goldCost = progressionService != null ? progressionService.GoldCostPerPoint : 10;

        SetButtonState(healthAddButton, goldAmount >= goldCost &&
                                        playerStatsRuntime.GetMaximum(PlayerStatType.Health) < PlayerStatsRuntime.MaximumMaximumValue);
        SetButtonState(manaAddButton, goldAmount >= goldCost &&
                                      playerStatsRuntime.GetMaximum(PlayerStatType.Mana) < PlayerStatsRuntime.MaximumMaximumValue);
        SetButtonState(staminaAddButton, goldAmount >= goldCost &&
                                          playerStatsRuntime.GetMaximum(PlayerStatType.Stamina) < PlayerStatsRuntime.MaximumMaximumValue);

        SetButtonState(healthSubtractButton, playerStatsRuntime.GetMaximum(PlayerStatType.Health) > PlayerStatsRuntime.MinimumMaximumValue);
        SetButtonState(manaSubtractButton, playerStatsRuntime.GetMaximum(PlayerStatType.Mana) > PlayerStatsRuntime.MinimumMaximumValue);
        SetButtonState(staminaSubtractButton, playerStatsRuntime.GetMaximum(PlayerStatType.Stamina) > PlayerStatsRuntime.MinimumMaximumValue);
    }

    /// <summary>
    /// 把一个属性转换成“当前值 / 最大值”格式。
    /// </summary>
    /// <param name="text">要更新的文本。</param>
    /// <param name="statType">属性类型。</param>
    private void SetStatText(TMP_Text text, PlayerStatType statType)
    {
        if (text == null)
            return;

        int current = Mathf.FloorToInt(playerStatsRuntime.GetCurrent(statType) + 0.0001f);
        int maximum = playerStatsRuntime.GetMaximum(statType);
        text.text = current + " / " + maximum;
        text.enableAutoSizing = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>
    /// 根据条件设置按钮是否可点击。
    /// </summary>
    /// <param name="button">要设置的按钮。</param>
    /// <param name="isInteractable">是否可交互。</param>
    private void SetButtonState(Button button, bool isInteractable)
    {
        if (button != null)
            button.interactable = isInteractable;
    }

    /// <summary>
    /// 获取玩家当前金币数量。
    /// </summary>
    /// <returns>金币数量。</returns>
    private int GetGoldAmount()
    {
        if (playerInventory == null || playerInventory.GoldReward == null)
            return 0;

        long amount = playerInventory.GetItemAmountLong(playerInventory.GoldReward);
        return amount <= 0L ? 0 : amount >= int.MaxValue ? int.MaxValue : (int)amount;
    }

    /// <summary>
    /// 显示一次属性操作失败提示。
    /// </summary>
    /// <param name="message">失败原因。</param>
    private void ShowWarning(string message)
    {
        if (warningText == null)
            return;

        warningText.text = string.IsNullOrWhiteSpace(message) ? "操作失败。" : message;

        if (warningRoot != null)
            warningRoot.SetActive(true);
        else
            warningText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏属性操作失败提示。
    /// </summary>
    private void ClearWarning()
    {
        if (warningRoot != null)
            warningRoot.SetActive(false);
        else if (warningText != null)
            warningText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 按名称查找当前角色展示面板下的子组件。
    /// </summary>
    /// <typeparam name="T">要查找的组件类型。</typeparam>
    /// <param name="objectName">子对象名称。</param>
    /// <returns>找到的组件，找不到时返回 null。</returns>
    private T FindChildComponent<T>(string objectName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != objectName)
                continue;

            T component = children[i].GetComponent<T>();

            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// 按名称查找当前场景中的对象。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <returns>找到的对象，找不到时返回 null。</returns>
    private static GameObject FindNamedObject(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];

            if (candidate != null && candidate.scene.IsValid() && candidate.name == objectName)
                return candidate;
        }

        return null;
    }
}
