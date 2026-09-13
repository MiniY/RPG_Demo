using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; } // 对象池管理器单例。

    private readonly Dictionary<GameObject, Queue<GameObject>> poolMap =
        new Dictionary<GameObject, Queue<GameObject>>(); // 预制体和对象队列的映射。

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

        GameObject poolObject = poolQueue.Count > 0
            ? poolQueue.Dequeue()
            : CreatePooledObject(prefab);

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
