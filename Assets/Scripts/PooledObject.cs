using UnityEngine;

public class PooledObject : MonoBehaviour
{
    [SerializeField] private GameObject sourcePrefab; // 这个对象来自哪个预制体。

    public GameObject SourcePrefab => sourcePrefab; // 对外提供来源预制体。

    public void SetSourcePrefab(GameObject prefab)
    {
        sourcePrefab = prefab;
    }
}
