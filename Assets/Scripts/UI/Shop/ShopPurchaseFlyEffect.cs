using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店购买飞行动画，负责生成临时商品图标并飞向背包图标位置。
/// </summary>
public class ShopPurchaseFlyEffect : MonoBehaviour
{
    /// <summary>
    /// 当前 UI 所在的 Canvas（画布）。
    /// </summary>
    [SerializeField] private Canvas rootCanvas; // 根画布。

    /// <summary>
    /// 飞行动画图标生成到的 UI 层，留空时使用当前物体。
    /// </summary>
    [SerializeField] private RectTransform effectLayer; // 特效层。

    /// <summary>
    /// 飞行动画的目标位置，建议绑定底部 PackageButton（背包按钮）。
    /// </summary>
    [SerializeField] private RectTransform targetTransform; // 目标位置。

    /// <summary>
    /// 飞行图标的初始尺寸。
    /// </summary>
    [SerializeField] private Vector2 startSize = new Vector2(72f, 72f); // 起始尺寸。

    /// <summary>
    /// 飞行图标到达目标时的尺寸倍率。
    /// </summary>
    [SerializeField, Range(0.05f, 1f)] private float endScale = 0.25f; // 结束缩放。

    /// <summary>
    /// 飞行动画持续时间。
    /// </summary>
    [SerializeField, Min(0.05f)] private float duration = 0.45f; // 动画时长。

    /// <summary>
    /// 位置插值曲线，让飞行动画更自然。
    /// </summary>
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 移动曲线。

    /// <summary>
    /// 透明度插值曲线，让图标到达时淡出。
    /// </summary>
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); // 透明度曲线。

    /// <summary>
    /// 播放购买飞行动画。
    /// </summary>
    /// <param name="sprite">要飞行的商品图标。</param>
    /// <param name="startTransform">动画起点，通常是商品格子。</param>
    public void Play(Sprite sprite, RectTransform startTransform)
    {
        if (sprite == null || startTransform == null || targetTransform == null)
            return;

        ResolveReferences();
        StartCoroutine(PlayRoutine(sprite, startTransform));
    }

    /// <summary>
    /// 自动补全必要引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (effectLayer == null)
            effectLayer = transform as RectTransform;
    }

    /// <summary>
    /// 执行一次商品图标飞行动画。
    /// </summary>
    /// <param name="sprite">商品图标。</param>
    /// <param name="startTransform">动画起点。</param>
    private IEnumerator PlayRoutine(Sprite sprite, RectTransform startTransform)
    {
        GameObject iconObject = new GameObject("ShopPurchaseFlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)); // 临时飞行图标对象。
        RectTransform iconTransform = iconObject.GetComponent<RectTransform>(); // 临时图标矩形变换。
        Image iconImage = iconObject.GetComponent<Image>(); // 临时图标图片。
        CanvasGroup canvasGroup = iconObject.GetComponent<CanvasGroup>(); // 临时图标画布组。

        iconObject.transform.SetParent(effectLayer == null ? transform : effectLayer, false);
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        iconTransform.sizeDelta = startSize;
        iconTransform.position = startTransform.position;

        Vector3 startPosition = startTransform.position; // 起始世界位置。
        Vector3 endPosition = targetTransform.position; // 目标世界位置。
        float elapsedTime = 0f; // 已播放时间。

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration); // 归一化时间。
            float moveAmount = moveCurve == null ? normalizedTime : moveCurve.Evaluate(normalizedTime); // 移动插值。
            float alphaAmount = alphaCurve == null ? 1f - normalizedTime : alphaCurve.Evaluate(normalizedTime); // 透明度插值。
            float scaleAmount = Mathf.Lerp(1f, endScale, normalizedTime); // 缩放插值。

            iconTransform.position = Vector3.LerpUnclamped(startPosition, endPosition, moveAmount);
            iconTransform.localScale = Vector3.one * scaleAmount;
            canvasGroup.alpha = alphaAmount;

            yield return null;
        }

        Destroy(iconObject);
    }
}
