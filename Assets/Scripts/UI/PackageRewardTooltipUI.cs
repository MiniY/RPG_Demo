using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 显示背包奖励的名称和说明，并把提示框限制在指定背景范围内。
/// </summary>
public class PackageRewardTooltipUI : MonoBehaviour
{
    /// <summary>
    /// 提示框自身的矩形变换。
    /// </summary>
    [SerializeField] private RectTransform tooltipRoot; // 提示框根节点。

    /// <summary>
    /// 限制提示框不能超出的背景范围。
    /// </summary>
    [SerializeField] private RectTransform boundsRoot; // 边界背景范围。

    /// <summary>
    /// 奖励名称文本。
    /// </summary>
    [SerializeField] private TMP_Text rewardNameText; // 奖励名称文本。

    /// <summary>
    /// 奖励说明文本。
    /// </summary>
    [SerializeField] private TMP_Text descriptionText; // 奖励说明文本。

    /// <summary>
    /// 用来隐藏和显示提示框的透明度组件。
    /// </summary>
    [SerializeField] private CanvasGroup canvasGroup; // 透明度控制组件。

    /// <summary>
    /// 提示框和格子之间的水平距离。
    /// </summary>
    [SerializeField, Min(0f)] private float horizontalOffset = 14f; // 水平偏移。

    /// <summary>
    /// 提示框和格子之间的垂直距离。
    /// </summary>
    [SerializeField, Min(0f)] private float verticalOffset = 10f; // 垂直偏移。

    /// <summary>
    /// 提示框距离背景边缘的最小间距。
    /// </summary>
    [SerializeField, Min(0f)] private float edgePadding = 8f; // 边缘间距。

    /// <summary>
    /// 缓存格子的世界四角坐标。
    /// </summary>
    private readonly Vector3[] anchorWorldCorners = new Vector3[4]; // 格子世界四角。

    /// <summary>
    /// 缓存背景的本地四角坐标。
    /// </summary>
    private readonly Vector3[] boundsLocalCorners = new Vector3[4]; // 背景本地四角。

    /// <summary>
    /// 初始化提示框引用和隐藏状态。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    /// <summary>
    /// 在编辑器中添加脚本时自动绑定常见引用。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 显示指定奖励的说明，并根据格子位置自动选择合适位置。
    /// </summary>
    /// <param name="reward">要显示说明的奖励数据。</param>
    /// <param name="anchor">触发提示的背包格子。</param>
    public void Show(RewardSO reward, RectTransform anchor)
    {
        if (reward == null || anchor == null)
        {
            Hide();
            return;
        }

        ResolveReferences();

        if (tooltipRoot == null || boundsRoot == null)
            return;

        if (rewardNameText != null)
            rewardNameText.text = reward.rewardName;

        if (descriptionText != null)
            descriptionText.text = reward.description;

        SetVisible(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
        PlaceNearAnchor(anchor);
    }

    /// <summary>
    /// 隐藏奖励说明提示框。
    /// </summary>
    public void Hide()
    {
        ResolveReferences();
        SetVisible(false);
    }

    /// <summary>
    /// 自动补全没有手动绑定的引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (tooltipRoot == null)
            tooltipRoot = GetComponent<RectTransform>();

        if (boundsRoot == null && transform.parent is RectTransform parentRect)
            boundsRoot = parentRect;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 设置提示框显示状态。
    /// </summary>
    /// <param name="isVisible">是否显示提示框。</param>
    private void SetVisible(bool isVisible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 把提示框放在格子附近，并确保不会超出背景范围。
    /// </summary>
    /// <param name="anchor">触发提示的背包格子。</param>
    private void PlaceNearAnchor(RectTransform anchor)
    {
        tooltipRoot.pivot = new Vector2(0f, 1f);
        anchor.GetWorldCorners(anchorWorldCorners);
        boundsRoot.GetLocalCorners(boundsLocalCorners);

        Vector2 bottomLeft = boundsRoot.InverseTransformPoint(anchorWorldCorners[0]); // 格子左下角。
        Vector2 topLeft = boundsRoot.InverseTransformPoint(anchorWorldCorners[1]); // 格子左上角。
        Vector2 topRight = boundsRoot.InverseTransformPoint(anchorWorldCorners[2]); // 格子右上角。
        Vector2 bottomRight = boundsRoot.InverseTransformPoint(anchorWorldCorners[3]); // 格子右下角。
        Vector2 tooltipSize = tooltipRoot.rect.size; // 提示框尺寸。

        Vector2[] candidates = new Vector2[]
        {
            new Vector2(bottomRight.x + horizontalOffset, bottomRight.y - verticalOffset),
            new Vector2(topRight.x + horizontalOffset, topRight.y + verticalOffset + tooltipSize.y),
            new Vector2(bottomLeft.x - horizontalOffset - tooltipSize.x, bottomLeft.y - verticalOffset),
            new Vector2(topLeft.x - horizontalOffset - tooltipSize.x, topLeft.y + verticalOffset + tooltipSize.y)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!IsTooltipInsideBounds(candidates[i], tooltipSize))
                continue;

            ApplyLocalPosition(candidates[i]);
            return;
        }

        ApplyLocalPosition(ClampToBounds(candidates[0], tooltipSize));
    }

    /// <summary>
    /// 判断提示框是否完整位于背景范围内。
    /// </summary>
    /// <param name="topLeftPosition">提示框左上角位置。</param>
    /// <param name="tooltipSize">提示框尺寸。</param>
    /// <returns>完整位于背景范围内时返回 true。</returns>
    private bool IsTooltipInsideBounds(Vector2 topLeftPosition, Vector2 tooltipSize)
    {
        float minX = boundsLocalCorners[0].x + edgePadding;
        float minY = boundsLocalCorners[0].y + edgePadding;
        float maxX = boundsLocalCorners[2].x - edgePadding;
        float maxY = boundsLocalCorners[2].y - edgePadding;

        float left = topLeftPosition.x;
        float right = topLeftPosition.x + tooltipSize.x;
        float top = topLeftPosition.y;
        float bottom = topLeftPosition.y - tooltipSize.y;

        return left >= minX && right <= maxX && bottom >= minY && top <= maxY;
    }

    /// <summary>
    /// 把提示框位置夹紧到背景范围内。
    /// </summary>
    /// <param name="topLeftPosition">提示框左上角位置。</param>
    /// <param name="tooltipSize">提示框尺寸。</param>
    /// <returns>不会超出背景范围的位置。</returns>
    private Vector2 ClampToBounds(Vector2 topLeftPosition, Vector2 tooltipSize)
    {
        float minX = boundsLocalCorners[0].x + edgePadding;
        float minY = boundsLocalCorners[0].y + edgePadding;
        float maxX = boundsLocalCorners[2].x - edgePadding;
        float maxY = boundsLocalCorners[2].y - edgePadding;

        float x = Mathf.Clamp(topLeftPosition.x, minX, maxX - tooltipSize.x);
        float y = Mathf.Clamp(topLeftPosition.y, minY + tooltipSize.y, maxY);

        return new Vector2(x, y);
    }

    /// <summary>
    /// 应用提示框在背景范围内的本地位置。
    /// </summary>
    /// <param name="localPosition">背景范围内的本地位置。</param>
    private void ApplyLocalPosition(Vector2 localPosition)
    {
        tooltipRoot.position = boundsRoot.TransformPoint(localPosition);
    }
}