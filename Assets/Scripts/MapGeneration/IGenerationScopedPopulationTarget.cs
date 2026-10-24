using System;
using UnityEngine;

/// <summary>Assembly boundary for pooled Damageable actors owned by one map generation.</summary>
public interface IGenerationScopedPopulationTarget
{
    UnityEngine.Object PlacementInstance { get; }
    GameObject PlacementGameObject { get; }
    GameObject ReinitializationTarget { get; }
    GameObject Spawn(Vector3 position, Quaternion rotation);
    void Return(GameObject instance);
    void SubscribeDefeated(Action callback);
    void UnsubscribeDefeated(Action callback);
}

/// <summary>Explicit opt-in factory seam; the existing active Spawn contract is unchanged.</summary>
public interface IGenerationScopedPreparedPopulationTarget
{
    GameObject SpawnPrepared(Vector3 position, Quaternion rotation, Action<GameObject> prepare);
}

/// <summary>Original actor health and collision settings across the runtime assembly boundary.</summary>
public interface IEcologyResidentRuntime
{
    bool IsAlive { get; }
    int MovementBlockingLayers { get; }
    float MovementCollisionSkin { get; }
}

/// <summary>Explicit handoff for an inactive opt-in instance borrowed by the original Spawn path.</summary>
public interface IPooledLegacyActivation
{
    void PrepareForLegacyActivation();
}
