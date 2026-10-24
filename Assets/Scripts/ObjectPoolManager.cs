using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; } // 对象池管理器单例。

    private readonly Dictionary<GameObject, Queue<GameObject>> poolMap =
        new Dictionary<GameObject, Queue<GameObject>>(); // 预制体和对象队列的映射。
    private Transform preparedCreationRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            return;
        }

        Instance = this;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (Instance == null)
            CreatePoolManager();

        return Instance.SpawnFromPool(prefab, position, rotation);
    }

    public static GameObject SpawnPrepared(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> prepare)
    {
        if (prepare == null) throw new ArgumentNullException(nameof(prepare));
        if (prefab == null) return null;
        if (Instance == null) CreatePoolManager();
        return Instance.SpawnPreparedFromPool(prefab, position, rotation, prepare);
    }

    private GameObject SpawnPreparedFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> prepare)
    {
        if (!poolMap.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>(); poolMap.Add(prefab, queue);
        }
        GameObject actor = null;
        while (queue.Count > 0 && actor == null) actor = queue.Dequeue();
        if (actor == null)
        {
            if (preparedCreationRoot == null)
            {
                var holder = new GameObject("PreparedPoolCreation");
                holder.SetActive(false); holder.transform.SetParent(transform, false); preparedCreationRoot = holder.transform;
            }
            // An active prefab instantiated without this inactive parent runs
            // Awake/OnEnable before its generation and home can be assigned.
            actor = Instantiate(prefab, preparedCreationRoot, false);
            actor.SetActive(false); actor.transform.SetParent(null, false);
            PooledObject pooled = actor.GetComponent<PooledObject>();
            if (pooled == null) pooled = actor.AddComponent<PooledObject>();
            pooled.SetSourcePrefab(prefab);
        }
        try
        {
            if (actor.activeSelf || actor.activeInHierarchy) throw new InvalidOperationException("Prepared pool lease was already active.");
            actor.transform.SetPositionAndRotation(position, rotation);
            prepare(actor);
            if (actor == null || actor.activeSelf || actor.activeInHierarchy)
                throw new InvalidOperationException("Preparation must preserve the inactive pool lease.");
            actor.SetActive(true);
            if (!actor.activeInHierarchy) throw new InvalidOperationException("Prepared pool lease could not activate.");
            return actor;
        }
        catch
        {
            if (actor != null) ReturnToPool(actor, prefab);
            throw;
        }
    }

    public static void ReturnOrDeactivate(GameObject poolObject)
    {
        if (poolObject == null)
            return;

        PooledObject pooledObject = poolObject.GetComponent<PooledObject>();
        Return(poolObject, pooledObject != null ? pooledObject.SourcePrefab : null);
    }

    public static void Return(GameObject poolObject, GameObject sourcePrefab)
    {
        if (poolObject == null)
            return;

        if (sourcePrefab != null)
        {
            if (Instance == null)
                CreatePoolManager();

            Instance.ReturnToPool(poolObject, sourcePrefab);
            return;
        }

        poolObject.SetActive(false);
    }

    private static void CreatePoolManager()
    {
        GameObject managerObject = new GameObject("ObjectPoolManager");
        Instance = managerObject.AddComponent<ObjectPoolManager>();
    }

    private GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!poolMap.TryGetValue(prefab, out Queue<GameObject> poolQueue))
        {
            poolQueue = new Queue<GameObject>();
            poolMap.Add(prefab, poolQueue);
        }

        // A scene/owner may destroy an inactive instance after it was returned.
        // Match prepared Spawn: consume dead Unity references before reusing a lease.
        GameObject poolObject = null;
        while (poolQueue.Count > 0 && poolObject == null) poolObject = poolQueue.Dequeue();
        if (poolObject == null) poolObject = CreatePooledObject(prefab);

        foreach (MonoBehaviour behaviour in poolObject.GetComponents<MonoBehaviour>())
            if (behaviour is IPooledLegacyActivation optIn) optIn.PrepareForLegacyActivation();
        poolObject.transform.SetPositionAndRotation(position, rotation);
        poolObject.SetActive(true);

        return poolObject;
    }

    private GameObject CreatePooledObject(GameObject prefab)
    {
        GameObject poolObject = Instantiate(prefab);
        PooledObject pooledObject = poolObject.GetComponent<PooledObject>();

        if (pooledObject == null)
            pooledObject = poolObject.AddComponent<PooledObject>();

        pooledObject.SetSourcePrefab(prefab);
        return poolObject;
    }

    private void ReturnToPool(GameObject poolObject, GameObject sourcePrefab)
    {
        if (!poolMap.TryGetValue(sourcePrefab, out Queue<GameObject> poolQueue))
        {
            poolQueue = new Queue<GameObject>();
            poolMap.Add(sourcePrefab, poolQueue);
        }

        PooledObject pooledObject = poolObject.GetComponent<PooledObject>();

        if (pooledObject == null)
            pooledObject = poolObject.AddComponent<PooledObject>();

        pooledObject.SetSourcePrefab(sourcePrefab);
        poolObject.SetActive(false);
        poolQueue.Enqueue(poolObject);
    }
}
