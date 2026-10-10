using System;
using UnityEngine;

/// <summary>Assembly boundary for reusing main BaseDamageable without coupling MapGeneration to it.</summary>
public interface IDestructiblePlacementTarget
{
    UnityEngine.Object PlacementInstance { get; }
    GameObject PlacementGameObject { get; }
    GameObject Spawn(Vector3 position, Quaternion rotation);
    void Return(GameObject instance);
    void SubscribeDefeated(Action callback);
    void UnsubscribeDefeated(Action callback);
}
