using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Plant))]
// 控制植物受击时的整体显隐闪烁，不负责动画和扣血。
public class PlantBehaviorController : MonoBehaviour
{
    [Header("闪烁表现")]
    [SerializeField] private GameObject visualObject; // 需要整体显隐切换的 Visual 子物体。
    [SerializeField, Min(0.01f)] private float flashInterval = 0.05f; // 每次显隐切换之间的间隔。
    [SerializeField, Min(1)] private int flashCount = 2; // 闪烁次数，每次包含一次隐藏和一次显示。

    private Plant plant; // 植物逻辑组件。
    private Coroutine flashCoroutine; // 当前正在执行的闪烁协程。

    // 初始化植物逻辑和 Visual 引用。
    private void Awake()
    {
        plant = GetComponent<Plant>();

        if (visualObject == null)
        {
            Transform visualTransform = transform.Find("Visual");

            if (visualTransform != null)
                visualObject = visualTransform.gameObject;
        }
    }

    // 启用时监听植物事件，并恢复 Visual 显示。
    private void OnEnable()
    {
        if (plant != null)
        {
            plant.OnDamaged += HandlePlantDamaged;
            plant.OnDefeated += HandlePlantDefeated;
        }

        SetVisualVisible(true);
    }

    // 禁用时解绑事件并停止闪烁。
    private void OnDisable()
    {
        if (plant != null)
        {
            plant.OnDamaged -= HandlePlantDamaged;
            plant.OnDefeated -= HandlePlantDefeated;
        }

        StopFlashCoroutine();
        SetVisualVisible(true);
    }

    // 植物受到伤害时播放整体显隐闪烁。
    private void HandlePlantDamaged(BaseDamageable damageable, float damage, Vector3? damageSourcePosition)
    {
        if (plant == null || damageable != plant || damage <= 0f || plant.IsDefeated)
            return;

        PlayFlash();
    }

    // 植物被摧毁时停止闪烁并隐藏 Visual。
    private void HandlePlantDefeated(BaseDamageable damageable)
    {
        if (plant == null || damageable != plant)
            return;

        StopFlashCoroutine();
        SetVisualVisible(false);
    }

    // 播放植物受击时的整体闪烁。
    private void PlayFlash()
    {
        if (!isActiveAndEnabled || visualObject == null)
            return;

        StopFlashCoroutine();
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    // 执行整体显隐闪烁。
    private IEnumerator FlashCoroutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            SetVisualVisible(false);
            yield return new WaitForSeconds(flashInterval);

            SetVisualVisible(true);
            yield return new WaitForSeconds(flashInterval);
        }

        flashCoroutine = null;
    }

    // 设置 Visual 是否显示。
    private void SetVisualVisible(bool isVisible)
    {
        if (visualObject != null)
            visualObject.SetActive(isVisible);
    }

    // 停止当前正在执行的闪烁。
    private void StopFlashCoroutine()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
    }
}
