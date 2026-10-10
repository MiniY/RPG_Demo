using System;
using UnityEngine;

/// <summary>
/// 把地面奖励绑定到当前随机地图的既有清理事件，不拥有掉落、拾取或对象池规则。
/// </summary>
[DisallowMultipleComponent]
public sealed class GenerationScopedRewardLifetime : MonoBehaviour
{
    private MapGenerationController mapController;
    private Action returnToPool;
    private bool armed;

    /// <summary>
    /// 绑定当前地图的清理事件；对象池复用时会先解除旧绑定。
    /// </summary>
    public void Arm(MapGenerationController owner, Action onMapLifetimeEnded)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (onMapLifetimeEnded == null)
            throw new ArgumentNullException(nameof(onMapLifetimeEnded));

        Disarm();
        mapController = owner;
        returnToPool = onMapLifetimeEnded;
        mapController.MapGenerated += HandleMapGenerated;
        mapController.MapCleared += HandleMapCleared;
        armed = true;
    }

    /// <summary>
    /// 解除当前地图绑定；拾取回收和场景卸载都会走这里。
    /// </summary>
    public void Disarm()
    {
        if (mapController != null)
        {
            mapController.MapGenerated -= HandleMapGenerated;
            mapController.MapCleared -= HandleMapCleared;
        }

        mapController = null;
        returnToPool = null;
        armed = false;
    }

    private void HandleMapCleared()
    {
        if (!armed)
            return;

        Action callback = returnToPool;
        Disarm();
        callback?.Invoke();
    }

    private void HandleMapGenerated(MapData _)
    {
        HandleMapCleared();
    }

    private void OnDisable()
    {
        Disarm();
    }

    private void OnDestroy()
    {
        Disarm();
    }
}
