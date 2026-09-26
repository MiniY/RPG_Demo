using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店模式标签按钮 UI，负责根据 Toggle（切换控件）的选中状态改变按钮大小和文字颜色。
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ShopModeTabButtonUI : MonoBehaviour
{
    /// <summary>
    /// 当前标签按钮使用的 Toggle（切换控件）。
    /// </summary>
    [SerializeField] private Toggle toggle; // 切换控件。

    /// <summary>
    /// 需要缩放的目标 RectTransform（矩形变换），通常是按钮自身。
    /// </summary>
    [SerializeField] private RectTransform targetTransform; // 缩放目标。

    /// <summary>
    /// 需要变色的 TMP_Text（TextMeshPro 文本）。
    /// </summary>
    [SerializeField] private TMP_Text labelText; // 标签文本。

    /// <summary>
    /// 未选中时的按钮缩放。
    /// </summary>
    [SerializeField] private Vector3 normalScale = Vector3.one; // 普通缩放。

    /// <summary>
    /// 选中时的按钮缩放。
    /// </summary>
    [SerializeField] private Vector3 selectedScale = new Vector3(1.08f, 1.08f, 1f); // 选中缩放。

    /// <summary>
    /// 未选中时的文字颜色。
    /// </summary>
    [SerializeField] private Color normalTextColor = Color.white; // 普通文字颜色。

    /// <summary>
    /// 选中时的文字颜色。
    /// </summary>
    [SerializeField] private Color selectedTextColor = new Color(1f, 0.84f, 0.32f); // 选中文字颜色。

    /// <summary>
    /// 视觉过渡速度，数值越大变化越快。
    /// </summary>
    [SerializeField] private float animationSpeed = 12f; // 动画速度。

    /// <summary>
    /// 当前目标缩放。
    /// </summary>
    private Vector3 targetScale; // 目标缩放。

    /// <summary>
    /// 当前目标文字颜色。
    /// </summary>
    private Color targetTextColor; // 目标文字颜色。

    /// <summary>
    /// 初始化组件引用。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        RefreshImmediate();
    }

    /// <summary>
    /// 启用时监听 Toggle（切换控件）的状态变化。
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();

        if (toggle != null)
            toggle.onValueChanged.AddListener(HandleToggleValueChanged);

        RefreshImmediate();
    }

    /// <summary>
    /// 禁用时取消监听 Toggle（切换控件）的状态变化。
    /// </summary>
    private void OnDisable()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(HandleToggleValueChanged);
    }

    /// <summary>
    /// 每帧平滑更新按钮大小和文字颜色。
    /// </summary>
    private void Update()
    {
        float lerpAmount = Time.unscaledDeltaTime * animationSpeed; // 本帧插值比例。

        if (targetTransform != null)
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, targetScale, lerpAmount);

        if (labelText != null)
            labelText.color = Color.Lerp(labelText.color, targetTextColor, lerpAmount);
    }

    /// <summary>
    /// 立即刷新视觉状态，不播放过渡动画。
    /// </summary>
    public void RefreshImmediate()
    {
        bool isSelected = toggle != null && toggle.isOn; // 当前是否选中。

        SetVisualState(isSelected);

        if (targetTransform != null)
            targetTransform.localScale = targetScale;

        if (labelText != null)
            labelText.color = targetTextColor;
    }

    /// <summary>
    /// 处理 Toggle（切换控件）状态变化。
    /// </summary>
    /// <param name="isSelected">当前标签是否被选中。</param>
    private void HandleToggleValueChanged(bool isSelected)
    {
        SetVisualState(isSelected);
    }

    /// <summary>
    /// 设置当前标签按钮的目标视觉状态。
    /// </summary>
    /// <param name="isSelected">当前标签是否被选中。</param>
    private void SetVisualState(bool isSelected)
    {
        targetScale = isSelected ? selectedScale : normalScale;
        targetTextColor = isSelected ? selectedTextColor : normalTextColor;
    }

    /// <summary>
    /// 自动补全脚本需要的组件引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (targetTransform == null)
            targetTransform = transform as RectTransform;

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>();
    }

    /// <summary>
    /// 添加脚本时自动绑定基础引用。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
        RefreshImmediate();
    }
}
