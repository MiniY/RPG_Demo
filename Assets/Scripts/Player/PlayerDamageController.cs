using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// 接收玩家受到的伤害、修改统一生命值数据并发布受伤与被击败事件。
public class PlayerDamageController : MonoBehaviour
{
    private PlayerStatsRuntime playerStatsRuntime; // 游戏会话中统一保存的玩家属性数据。
    private bool isDefeated; // 玩家当前生命值是否已经归零。
    private bool hasWarnedMissingStats; // 是否已经提示过缺少玩家属性数据。

    public event Action<PlayerDamageController, DamageInfo> OnDamaged; // 玩家受到有效伤害时触发的事件。
    public event Action<PlayerDamageController> OnDefeated; // 玩家生命值首次降为零时触发的事件。

    public bool IsDefeated => isDefeated; // 对外提供玩家是否已经被击败。
    public float CurrentHealth => playerStatsRuntime != null ? playerStatsRuntime.CurrentHealth : 0f; // 对外提供当前生命值。

    // 初始化时尝试取得游戏会话中的玩家属性数据。
    private void Awake()
    {
        ResolvePlayerStats();
        RefreshDefeatedState();
    }

    // 场景对象重新启用时同步当前生命值状态，不擅自恢复生命值。
    private void OnEnable()
    {
        ResolvePlayerStats();
        RefreshDefeatedState();
    }

    // 让玩家受到一笔带有来源信息的伤害。
    public bool TakeDamage(float damage, Transform damageSource)
    {
        ResolvePlayerStats();

        if (playerStatsRuntime == null)
        {
            WarnMissingPlayerStats();
            return false;
        }

        if (isDefeated || damage <= 0f)
            return false;

        DamageInfo damageInfo = new DamageInfo(damage, damageSource); // 本次玩家伤害的完整信息。
        float previousHealth = playerStatsRuntime.CurrentHealth; // 扣血前的玩家生命值。
        playerStatsRuntime.SetCurrentHealth(previousHealth - damageInfo.Amount);

        if (Mathf.Approximately(previousHealth, playerStatsRuntime.CurrentHealth))
            return false;

        OnDamaged?.Invoke(this, damageInfo);

        if (playerStatsRuntime.CurrentHealth <= 0f)
        {
            isDefeated = true;
            OnDefeated?.Invoke(this);
        }

        return true;
    }

    // 从跨场景 GameSession 中取得唯一的玩家属性数据实例。
    private void ResolvePlayerStats()
    {
        if (playerStatsRuntime != null)
            return;

        if (GameSession.Instance != null)
            playerStatsRuntime = GameSession.Instance.PlayerStats;
    }

    // 根据统一生命值同步玩家是否已经被击败。
    private void RefreshDefeatedState()
    {
        isDefeated = playerStatsRuntime != null && playerStatsRuntime.CurrentHealth <= 0f;
    }

    // 缺少统一玩家属性时只输出一次警告，避免每次攻击重复刷屏。
    private void WarnMissingPlayerStats()
    {
        if (hasWarnedMissingStats)
            return;

        hasWarnedMissingStats = true;
        Debug.LogWarning("玩家伤害接收器找不到 PlayerStatsRuntime，无法结算伤害。", this);
    }
}
