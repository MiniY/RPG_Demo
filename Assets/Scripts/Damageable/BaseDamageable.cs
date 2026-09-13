using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider2D))]
// 管理所有可破坏对象的血量、受伤、死亡、奖励掉落和对象池回收。
public abstract class BaseDamageable : MonoBehaviour
{
    [Header("生命值")]
    [SerializeField, Min(1f)] private float maxHealth = 100f; // 最大生命值。
    private float currentHealth; // 当前生命值。
    private bool isDefeated; // 是否已经被击败或被摧毁。

    [Header("奖励映射")]
    [SerializeField] private string damageableId; // 用于查找奖励表的唯一编号。
    [SerializeField] private DamageableDieRewardListSO rewardList; // 所有可破坏对象的奖励表列表。
    [SerializeField] private Transform dropPoint; // 奖励生成位置，为空时使用对象位置。
    [SerializeField, Min(0f)] private float dropScatterRadius = 0.45f; // 奖励散落范围。

    [Header("被击败表现")]
    [SerializeField] private GameObject poolPrefab; // 场景手动摆放时，对应的对象池来源预制体。
    [FormerlySerializedAs("destroyDelay")]
    [SerializeField, Min(0f)] private float returnToPoolDelay = 0.5f; // 被击败后回收到对象池前的等待时间。

    private Collider2D[] colliders; // 对象身上的碰撞体。

    public event Action<BaseDamageable, float, Vector3?> OnDamaged; // 对象受到伤害时触发的事件。
    public event Action<BaseDamageable> OnDefeated; // 对象被击败或摧毁时触发的事件。
    public float MaxHealth => maxHealth; // 对外提供最大生命值。
    public float CurrentHealth => currentHealth; // 对外提供当前生命值。
    public bool IsDefeated => isDefeated; // 对外提供是否已经被击败。

    // 让子类用更贴近语义的名字输出日志。
    protected virtual string DamageableLabel => "可破坏对象";

    // 初始化对象身上的碰撞体引用。
    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>();
    }

    // 对象启用或从对象池取出时重置状态。
    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDefeated = false;
        SetCollidersEnabled(true);
    }

    // 让对象受到伤害，不提供伤害来源。
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, null);
    }

    // 让对象受到伤害，并提供伤害来源用于受击方向计算。
    public void TakeDamage(float damage, Transform damageSource)
    {
        Vector3? damageSourcePosition = damageSource != null ? damageSource.position : null;
        ApplyDamage(damage, damageSourcePosition);
    }

    // 执行扣血逻辑，并在未被击败时发出受击事件。
    private void ApplyDamage(float damage, Vector3? damageSourcePosition)
    {
        if (isDefeated || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (currentHealth <= 0f)
        {
            Defeat();
            return;
        }

        OnDamaged?.Invoke(this, damage, damageSourcePosition);
    }

    // 处理对象被击败或被摧毁后的流程。
    protected virtual void Defeat()
    {
        if (isDefeated)
            return;

        isDefeated = true;
        SetCollidersEnabled(false);
        SpawnRewards();
        OnDefeated?.Invoke(this);
        StartCoroutine(ReturnToPoolAfterDelay());
    }

    // 设置对象所有碰撞体是否启用。
    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        foreach (Collider2D damageableCollider in colliders)
        {
            if (damageableCollider != null)
                damageableCollider.enabled = enabled;
        }
    }

    // 根据奖励表生成地面掉落物。
    private void SpawnRewards()
    {
        if (rewardList == null)
        {
            Debug.LogWarning($"{DamageableLabel} {name} 没有设置奖励列表。", this);
            return;
        }

        if (!rewardList.TryGetRewardTable(damageableId, out DamageableDieRewardSO rewardTable))
        {
            Debug.LogWarning($"找不到{DamageableLabel} {name} 对应的奖励表，damageableId：{damageableId}", this);
            return;
        }

        foreach (RewardDropRule rule in rewardTable.dieReward)
        {
            if (rule == null || rule.reward == null || rule.reward.rewardPrefab == null)
                continue;

            // 每条奖励独立判定，因此一次击败可以掉落多个奖励种类。
            if (UnityEngine.Random.value > rule.dropChance)
                continue;

            int amount = UnityEngine.Random.Range(rule.minAmount, rule.maxAmount + 1);
            Vector3 spawnPosition = GetDropPosition();
            GameObject rewardObject = ObjectPoolManager.Spawn(rule.reward.rewardPrefab, spawnPosition, Quaternion.identity);

            if (rewardObject == null)
                continue;

            RewardPickup pickup = rewardObject.GetComponent<RewardPickup>();

            if (pickup == null)
                pickup = rewardObject.AddComponent<RewardPickup>();

            pickup.Initialize(rule.reward, amount);
        }
    }

    // 计算奖励掉落在对象附近的随机位置。
    private Vector3 GetDropPosition()
    {
        Vector3 origin = dropPoint != null ? dropPoint.position : transform.position;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
        return origin + new Vector3(offset.x, offset.y, 0f);
    }

    // 等待一段时间后把对象回收到对象池。
    private IEnumerator ReturnToPoolAfterDelay()
    {
        if (returnToPoolDelay > 0f)
            yield return new WaitForSeconds(returnToPoolDelay);

        if (poolPrefab != null)
        {
            ObjectPoolManager.Return(gameObject, poolPrefab);
            yield break;
        }

        ObjectPoolManager.ReturnOrDeactivate(gameObject);
    }

    // 在 Inspector 修改数值时保证参数合法。
    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        dropScatterRadius = Mathf.Max(0f, dropScatterRadius);
        returnToPoolDelay = Mathf.Max(0f, returnToPoolDelay);
    }
}
