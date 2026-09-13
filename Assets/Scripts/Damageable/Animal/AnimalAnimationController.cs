using UnityEngine;

// 控制动物死亡动画表现，不负责受击位移和扣血。
public class AnimalAnimationController : MonoBehaviour
{
    [SerializeField] private Animal animal; // 动物逻辑组件。
    [SerializeField] private Animator animator; // 动物动画控制器。

    [Header("Animator 参数")]
    [SerializeField] private string isDieParameterName = "IsDie"; // 死亡状态参数名。

    // 初始化组件引用。
    private void Awake()
    {
        if (animal == null)
            animal = GetComponentInParent<Animal>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // 启用时监听动物事件。
    private void OnEnable()
    {
        if (animal != null)
        {
            animal.OnDefeated += HandleAnimalDefeated;
        }

        ResetAnimatorState();
    }

    // 禁用时取消监听动物事件。
    private void OnDisable()
    {
        if (animal != null)
        {
            animal.OnDefeated -= HandleAnimalDefeated;
        }
    }

    // 动物被击败时切换到死亡动画。
    private void HandleAnimalDefeated(BaseDamageable damageable)
    {
        if (animal == null || damageable != animal)
            return;

        SetDieState(true);
    }

    // 重置动画状态，保证对象池复用时回到初始表现。
    private void ResetAnimatorState()
    {
        SetDieState(false);
    }

    // 设置动物死亡状态。
    private void SetDieState(bool isDie)
    {
        if (animator == null || string.IsNullOrWhiteSpace(isDieParameterName))
            return;

        animator.SetBool(isDieParameterName, isDie);
    }
}
