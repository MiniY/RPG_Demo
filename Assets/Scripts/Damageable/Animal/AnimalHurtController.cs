using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animal))]
// 控制动物受击后的位移反馈，不负责扣血和动画。
public class AnimalHurtController : MonoBehaviour
{
    [Header("受击位移")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.2f; // 受击后后退的距离。
    [SerializeField, Min(0.01f)] private float knockbackOutDuration = 0.06f; // 后退到最远点的时间。
    [SerializeField, Min(0.01f)] private float knockbackReturnDuration = 0.08f; // 回到原位的时间。

    private Animal animal; // 动物逻辑组件。
    private Coroutine hurtCoroutine; // 当前受击协程。

    // 初始化动物逻辑组件引用。
    private void Awake()
    {
        animal = GetComponent<Animal>();
    }

    // 启用时监听动物受伤和被击败事件。
    private void OnEnable()
    {
        if (animal != null)
        {
            animal.OnDamaged += HandleAnimalDamaged;
            animal.OnDefeated += HandleAnimalDefeated;
        }
    }

    // 禁用时解绑事件并停止受击位移。
    private void OnDisable()
    {
        if (animal != null)
        {
            animal.OnDamaged -= HandleAnimalDamaged;
            animal.OnDefeated -= HandleAnimalDefeated;
        }

        StopHurtCoroutine();
    }

    // 动物受到伤害时启动受击位移。
    private void HandleAnimalDamaged(BaseDamageable damageable, float damage, Vector3? damageSourcePosition)
    {
        if (animal == null || damageable != animal || damage <= 0f || animal.IsDefeated)
            return;

        if (damageSourcePosition.HasValue)
            PlayHurt(damageSourcePosition.Value);
        else
            PlayHurt(transform.position + Vector3.right);
    }

    // 动物被击败时停止普通受击位移，避免和死亡表现冲突。
    private void HandleAnimalDefeated(BaseDamageable damageable)
    {
        if (animal == null || damageable != animal)
            return;

        StopHurtCoroutine();
    }

    // 从伤害来源位置计算反方向，并播放受击位移。
    public void PlayHurt(Vector3 damageSourcePosition)
    {
        if (!isActiveAndEnabled)
            return;

        StopHurtCoroutine();
        hurtCoroutine = StartCoroutine(PlayHurtCoroutine(damageSourcePosition));
    }

    // 执行后退再回到原位的完整位移过程。
    private IEnumerator PlayHurtCoroutine(Vector3 damageSourcePosition)
    {
        Vector3 startPosition = transform.position; // 本次受击开始时的位置。
        Vector3 knockbackDirection = startPosition - damageSourcePosition;
        knockbackDirection.z = 0f;

        if (knockbackDirection.sqrMagnitude <= 0.0001f)
            knockbackDirection = Vector3.left;

        knockbackDirection.Normalize();

        Vector3 knockbackPosition = startPosition + knockbackDirection * knockbackDistance;

        yield return MoveToPosition(startPosition, knockbackPosition, knockbackOutDuration);
        yield return MoveToPosition(knockbackPosition, startPosition, knockbackReturnDuration);

        hurtCoroutine = null;
    }

    // 在指定时间内把动物从一个位置移动到另一个位置。
    private IEnumerator MoveToPosition(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress); // SmoothStep，让位移更顺。
            transform.position = Vector3.LerpUnclamped(from, to, progress);
            yield return null;
        }

        transform.position = to;
    }

    // 停止当前正在播放的受击位移。
    private void StopHurtCoroutine()
    {
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
            hurtCoroutine = null;
        }
    }
}
