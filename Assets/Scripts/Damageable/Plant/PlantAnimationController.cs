using UnityEngine;

// 控制植物 Visual 的显示状态，不负责植物行为、扣血和奖励掉落。
public class PlantAnimationController : MonoBehaviour
{
    [SerializeField] private Plant plant; // 植物逻辑组件。
    [SerializeField] private Renderer[] visualRenderers; // Visual 下需要显示或隐藏的渲染器。
    [SerializeField] private Animator animator; // 可选的植物动画控制器。

    // 初始化组件引用。
    private void Awake()
    {
        if (plant == null)
            plant = GetComponentInParent<Plant>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = GetComponentsInChildren<Renderer>(true);
    }

    // 启用时监听植物事件。
    private void OnEnable()
    {
        if (plant != null)
            plant.OnDefeated += HandlePlantDefeated;

        SetVisualVisible(true);
    }

    // 禁用时取消监听植物事件。
    private void OnDisable()
    {
        if (plant != null)
            plant.OnDefeated -= HandlePlantDefeated;
    }

    // 植物被摧毁时立即隐藏 Visual，不播放死亡动画。
    private void HandlePlantDefeated(BaseDamageable damageable)
    {
        if (plant == null || damageable != plant)
            return;

        SetVisualVisible(false);
    }

    // 设置 Visual 是否可见，保证对象池复用时能够重新显示植物。
    private void SetVisualVisible(bool isVisible)
    {
        if (visualRenderers != null)
        {
            foreach (Renderer visualRenderer in visualRenderers)
            {
                if (visualRenderer != null)
                    visualRenderer.enabled = isVisible;
            }
        }

        if (animator != null)
            animator.enabled = isVisible;
    }
}
