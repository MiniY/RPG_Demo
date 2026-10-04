using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BaseMonster))]
// 控制怪物受击后的位移反馈，不负责扣血和动画。
public class MonsterHurtController : MonoBehaviour
{
    [Header("受击位移")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.2f; // 受击后后退的距离。
    [SerializeField, Min(0.01f)] private float knockbackOutDuration = 0.06f; // 后退到最远点的时间。
    [SerializeField, Min(0.01f)] private float knockbackReturnDuration = 0.08f; // 回到原位的时间。

    private BaseMonster baseMonster; // 怪物逻辑组件。
    private MonsterMovementController movementController; // 怪物普通移动控制器。
    private Rigidbody2D body; // 执行受击位移的二维刚体。
    private Coroutine hurtCoroutine; // 当前受击协程。

    // 初始化怪物逻辑组件引用。
    private void Awake()
    {
        baseMonster = GetComponent<BaseMonster>();
        movementController = GetComponent<MonsterMovementController>();
        body = GetComponent<Rigidbody2D>();
    }

    // 启用时监听怪物受伤和被击败事件。
    private void OnEnable()
    {
        if (baseMonster != null)
        {
            baseMonster.OnDamaged += HandleMonsterDamaged;
            baseMonster.OnDefeated += HandleMonsterDefeated;
        }
    }

    // 禁用时解绑事件并停止受击位移。
    private void OnDisable()
    {
        if (baseMonster != null)
        {
            baseMonster.OnDamaged -= HandleMonsterDamaged;
            baseMonster.OnDefeated -= HandleMonsterDefeated;
        }

        StopHurtCoroutine();
    }

    // 怪物受到伤害时启动受击位移。
    private void HandleMonsterDamaged(BaseDamageable damageable, DamageInfo damageInfo)
    {
        if (baseMonster == null || damageable != baseMonster || damageInfo.Amount <= 0f || baseMonster.IsDefeated)
            return;

        if (damageInfo.SourcePosition.HasValue)
            PlayHurt(damageInfo.SourcePosition.Value);
        else
            PlayHurt(transform.position + Vector3.right);
    }

    // 怪物被击败时停止普通受击位移，避免和死亡表现冲突。
    private void HandleMonsterDefeated(BaseDamageable damageable)
    {
        if (baseMonster == null || damageable != baseMonster)
            return;

        StopHurtCoroutine();
    }

    // 从伤害来源位置计算反方向，并播放受击位移。
    public void PlayHurt(Vector3 damageSourcePosition)
    {
        if (!isActiveAndEnabled)
            return;

        StopHurtCoroutine();
        movementController?.SetMovementLocked(true);
        hurtCoroutine = StartCoroutine(PlayHurtCoroutine(damageSourcePosition));
    }

    // 执行后退再回到原位的完整位移过程。
    private IEnumerator PlayHurtCoroutine(Vector3 damageSourcePosition)
    {
        Vector2 startPosition = body != null ? body.position : (Vector2)transform.position; // 本次受击开始时的位置。
        Vector2 knockbackDirection = startPosition - (Vector2)damageSourcePosition;

        if (knockbackDirection.sqrMagnitude <= 0.0001f)
            knockbackDirection = Vector3.left;

        knockbackDirection.Normalize();

        Vector2 knockbackPosition = startPosition + knockbackDirection * knockbackDistance;

        yield return MoveToPosition(startPosition, knockbackPosition, knockbackOutDuration);
        yield return MoveToPosition(knockbackPosition, startPosition, knockbackReturnDuration);

        hurtCoroutine = null;
        movementController?.SetMovementLocked(false);
    }

    // 在指定时间内把怪物从一个位置移动到另一个位置。
    private IEnumerator MoveToPosition(Vector2 from, Vector2 to, float duration)
    {
        if (duration <= 0f)
        {
            SetPosition(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();

            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress); // SmoothStep，让位移更顺。
            SetPosition(Vector2.LerpUnclamped(from, to, progress));
        }

        SetPosition(to);
    }

    // 使用二维刚体设置位置，没有刚体时才回退为修改 Transform。
    private void SetPosition(Vector2 position)
    {
        if (body != null)
            body.MovePosition(position);
        else
            transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    // 停止当前正在播放的受击位移。
    private void StopHurtCoroutine()
    {
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
            hurtCoroutine = null;
        }

        movementController?.SetMovementLocked(false);
    }
}
