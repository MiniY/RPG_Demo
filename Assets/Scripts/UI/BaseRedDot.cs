using UnityEngine;

/// <summary>
/// 提供通用红点显示和隐藏能力，不决定红点出现条件。
/// </summary>
public class BaseRedDot : MonoBehaviour
{
    /// <summary>
    /// 用来显示提示红点的界面对象。
    /// </summary>
    [SerializeField] private GameObject redDotObject; // 红点对象。

    /// <summary>
    /// 当前红点是否正在显示。
    /// </summary>
    public bool IsRedDotVisible => redDotObject != null && redDotObject.activeSelf;

    /// <summary>
    /// 初始化时先隐藏红点，避免游戏开始时误显示。
    /// </summary>
    protected virtual void Awake()
    {
        SetRedDotVisible(false);
    }

    /// <summary>
    /// 设置红点是否显示。
    /// </summary>
    /// <param name="isVisible">是否显示红点。</param>
    protected void SetRedDotVisible(bool isVisible)
    {
        if (redDotObject != null)
            redDotObject.SetActive(isVisible);
    }
}