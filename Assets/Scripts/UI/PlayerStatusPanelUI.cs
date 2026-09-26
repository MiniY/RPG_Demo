using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 只负责显示玩家的生命值、魔法值和体力值，不负责修改属性数据。
/// </summary>
public class PlayerStatusPanelUI : MonoBehaviour
{
    [Header("状态条")]

    /// <summary>
    /// 生命值填充图片。
    /// </summary>
    [SerializeField] private Image healthFill;

    /// <summary>
    /// 魔法值填充图片。
    /// </summary>
    [SerializeField] private Image manaFill;

    /// <summary>
    /// 体力值填充图片。
    /// </summary>
    [SerializeField] private Image staminaFill;

    [Header("状态文本")]

    /// <summary>
    /// 生命值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text healthText;

    /// <summary>
    /// 魔法值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text manaText;

    /// <summary>
    /// 体力值数值文本。
    /// </summary>
    [SerializeField] private TMP_Text staminaText;

    /// <summary>
    /// 统一管理玩家属性的运行时数据。
    /// </summary>
    private PlayerStatsRuntime playerStatsRuntime;

    /// <summary>
    /// 脚本启用时查找界面引用并绑定属性事件。
    /// </summary>
    private void OnEnable()
    {
        ResolveViewReferences();

        if (GameSession.Instance != null)
            SetStatsRuntime(GameSession.Instance.PlayerStats);
    }

    /// <summary>
    /// 脚本禁用时解除属性事件监听。
    /// </summary>
    private void OnDisable()
    {
        UnbindStatsRuntime();
    }

    /// <summary>
    /// 绑定统一玩家属性数据。
    /// </summary>
    /// <param name="statsRuntime">玩家属性运行时数据。</param>
    public void SetStatsRuntime(PlayerStatsRuntime statsRuntime)
    {
        if (playerStatsRuntime == statsRuntime)
        {
            RefreshView();
            return;
        }

        UnbindStatsRuntime();
        playerStatsRuntime = statsRuntime;

        if (playerStatsRuntime != null)
        {
            playerStatsRuntime.OnStatsChanged += RefreshView;
            playerStatsRuntime.EnsureInitializedForRuntime();
        }

        RefreshView();
    }

    /// <summary>
    /// 解除当前属性数据事件监听。
    /// </summary>
    private void UnbindStatsRuntime()
    {
        if (playerStatsRuntime != null)
            playerStatsRuntime.OnStatsChanged -= RefreshView;

        playerStatsRuntime = null;
    }

    /// <summary>
    /// 自动查找 HUD（游戏主界面）中的状态条和文本。
    /// </summary>
    private void ResolveViewReferences()
    {
        healthFill = healthFill != null ? healthFill : FindChildComponent<Image>("HealthBar");
        manaFill = manaFill != null ? manaFill : FindChildComponent<Image>("ManaBar");
        staminaFill = staminaFill != null ? staminaFill : FindChildComponent<Image>("StaminaBar");

        healthText = healthText != null ? healthText : FindChildComponent<TMP_Text>("HealthNumber");
        manaText = manaText != null ? manaText : FindChildComponent<TMP_Text>("ManaNumber");
        staminaText = staminaText != null ? staminaText : FindChildComponent<TMP_Text>("StaminaNumber");

        ConfigureFill(healthFill);
        ConfigureFill(manaFill);
        ConfigureFill(staminaFill);
    }

    /// <summary>
    /// 把状态条设置为从左向右的水平填充图像。
    /// </summary>
    /// <param name="fillImage">要配置的填充图片。</param>
    private void ConfigureFill(Image fillImage)
    {
        if (fillImage == null)
            return;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    /// <summary>
    /// 根据统一属性数据刷新三个状态条和三个数值文本。
    /// </summary>
    public void RefreshView()
    {
        if (playerStatsRuntime == null)
            return;

        playerStatsRuntime.EnsureInitializedForRuntime();
        SetStatusView(healthFill, healthText, PlayerStatType.Health);
        SetStatusView(manaFill, manaText, PlayerStatType.Mana);
        SetStatusView(staminaFill, staminaText, PlayerStatType.Stamina);
    }

    /// <summary>
    /// 更新一个状态的填充比例和“当前值 / 最大值”文本。
    /// </summary>
    /// <param name="fillImage">状态条填充图片。</param>
    /// <param name="text">状态数值文本。</param>
    /// <param name="statType">属性类型。</param>
    private void SetStatusView(Image fillImage, TMP_Text text, PlayerStatType statType)
    {
        float current = playerStatsRuntime.GetCurrent(statType);
        int maximum = playerStatsRuntime.GetMaximum(statType);

        if (fillImage != null)
            fillImage.fillAmount = maximum <= 0 ? 0f : Mathf.Clamp01(current / maximum);

        if (text != null)
        {
            text.text = Mathf.FloorToInt(current + 0.0001f) + " / " + maximum;
            text.enableAutoSizing = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    /// <summary>
    /// 按名称查找当前 HUD 下的子组件。
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
}
