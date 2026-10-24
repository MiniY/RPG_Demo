using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Exposes BaseMonster pooling and defeat events to map generation.</summary>
[RequireComponent(typeof(BaseMonster))]
[DisallowMultipleComponent]
public sealed class MonsterPopulationTargetBridge : MonoBehaviour,
    IGenerationScopedPopulationTarget, IGenerationScopedPreparedPopulationTarget, IEcologyResidentRuntime
{
    private readonly Dictionary<Action, Action<BaseDamageable>> handlers =
        new Dictionary<Action, Action<BaseDamageable>>();
    private BaseMonster monster;

    public UnityEngine.Object PlacementInstance => monster != null ? monster : GetComponent<BaseMonster>();
    public GameObject PlacementGameObject => gameObject;
    public GameObject ReinitializationTarget => gameObject;
    public bool IsAlive => (monster != null ? monster : GetComponent<BaseMonster>()) is BaseMonster actor && !actor.IsDefeated;
    public int MovementBlockingLayers => GetComponent<MonsterMovementController>().MovementBlockingLayers;
    public float MovementCollisionSkin => GetComponent<MonsterMovementController>().MovementCollisionSkin;

    private void Awake() => monster = GetComponent<BaseMonster>();

    public GameObject Spawn(Vector3 position, Quaternion rotation) =>
        ObjectPoolManager.Spawn(gameObject, position, rotation);

    public GameObject SpawnPrepared(Vector3 position, Quaternion rotation, Action<GameObject> prepare)
    {
        if (prepare == null) throw new ArgumentNullException(nameof(prepare));
        return ObjectPoolManager.SpawnPrepared(gameObject, position, rotation, actor =>
        {
            // A defeated pooled actor still has disabled feet until OnEnable. Exact Home binding
            // runs earlier, so the original reuse owner restores the inactive actor first.
            actor.GetComponent<BaseMonster>().ResetForGenerationReuse();
            prepare(actor);
        });
    }

    public void Return(GameObject instance) => ObjectPoolManager.ReturnOrDeactivate(instance);

    public void SubscribeDefeated(Action callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        if (monster == null)
            monster = GetComponent<BaseMonster>();
        if (handlers.ContainsKey(callback))
            return;

        Action<BaseDamageable> handler = _ => callback();
        handlers.Add(callback, handler);
        monster.OnDefeated += handler;
    }

    public void UnsubscribeDefeated(Action callback)
    {
        if (callback == null || monster == null ||
            !handlers.TryGetValue(callback, out Action<BaseDamageable> handler))
            return;
        monster.OnDefeated -= handler;
        handlers.Remove(callback);
    }
}
