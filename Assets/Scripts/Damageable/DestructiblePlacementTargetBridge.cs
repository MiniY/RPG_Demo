using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Adapts main BaseDamageable defeat events to the MapGeneration lifecycle seam.</summary>
[DisallowMultipleComponent]
public sealed class DestructiblePlacementTargetBridge : MonoBehaviour,
    IDestructiblePlacementTarget
{
    private readonly Dictionary<Action, Action<BaseDamageable>> handlers =
        new Dictionary<Action, Action<BaseDamageable>>();
    private BaseDamageable damageable;

    public UnityEngine.Object PlacementInstance =>
        damageable != null ? damageable : GetComponent<BaseDamageable>();
    public GameObject PlacementGameObject => gameObject;

    public GameObject Spawn(Vector3 position, Quaternion rotation) =>
        ObjectPoolManager.Spawn(gameObject, position, rotation);

    public void Return(GameObject instance) =>
        ObjectPoolManager.ReturnOrDeactivate(instance);

    private void Awake()
    {
        damageable = GetComponent<BaseDamageable>();
    }

    public void SubscribeDefeated(Action callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        if (damageable == null)
            damageable = GetComponent<BaseDamageable>();
        if (damageable == null)
            throw new InvalidOperationException(
                "Destructible placement target requires BaseDamageable.");
        if (handlers.ContainsKey(callback))
            return;

        Action<BaseDamageable> handler = _ => callback();
        handlers.Add(callback, handler);
        damageable.OnDefeated += handler;
    }

    public void UnsubscribeDefeated(Action callback)
    {
        if (callback == null || damageable == null)
            return;
        if (!handlers.TryGetValue(callback, out Action<BaseDamageable> handler))
            return;
        damageable.OnDefeated -= handler;
        handlers.Remove(callback);
    }
}
