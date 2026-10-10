using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Retained migration diagnostics for objects that historically used authored Legacy positions.
/// Stage 7 permanently removes this adapter from RandomGenerated runtime placement.
/// </summary>
[DisallowMultipleComponent]
public sealed class MapGeneratedObjectPlacementAdapter : MonoBehaviour
{
    /// <summary>
    /// 提供地图数据、渲染器和生成完成事件的控制器。
    /// </summary>
    [SerializeField] private MapGenerationController mapController;

    /// <summary>
    /// 用于保留场景对象相对布局的锚点；RPG 主场景中使用玩家。
    /// </summary>
    [SerializeField] private Transform placementAnchor;

    /// <summary>
    /// 需要随随机地图重新定位的 Main 场景对象。
    /// </summary>
    [SerializeField] private Transform[] mapAnchoredObjects = Array.Empty<Transform>();

    /// <summary>
    /// 与 mapAnchoredObjects 一一对应的原场景网格偏移。
    /// </summary>
    [SerializeField] private Vector2Int[] authoredCellOffsetValues = Array.Empty<Vector2Int>();

    /// <summary>
    /// 定位后需要重新启用的对象；用于让 AI 重新记录运行时出生点。
    /// </summary>
    [SerializeField] private GameObject[] restartAfterPlacement = Array.Empty<GameObject>();

    /// <summary>
    /// 获取当前地图控制器。
    /// </summary>
    public MapGenerationController MapController => mapController;

    /// <summary>
    /// 获取当前布局锚点。
    /// </summary>
    public Transform PlacementAnchor => placementAnchor;

    /// <summary>
    /// 获取所有由随机地图负责定位的场景对象。
    /// </summary>
    public IReadOnlyList<Transform> MapAnchoredObjects => GetTransitionalTargets();

    /// <summary>Number of unresolved production Roles still present in migration data.</summary>
    public int ProductionRoleCount => GetTransitionalTargets().Count;

    /// <summary>RandomGenerated never executes authored-offset placement after Stage 7.</summary>
    public bool RuntimePlacementEnabled => false;

    /// <summary>Explicit objects whose map-derived runtime state must be refreshed after PlayerReady.</summary>
    public IReadOnlyList<GameObject> RestartAfterPlacement => restartAfterPlacement;

    /// <summary>
    /// Compatibility entry point retained for old callers. It is intentionally inert:
    /// semantic placement services are the only RandomGenerated production owners.
    /// </summary>
    /// <param name="mapData">Map data is validated but never used for legacy placement.</param>
    public void PlaceObjects(MapData mapData)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));
    }

    /// <summary>
    /// Excludes Roles that already have a formal semantic placement owner even if stale
    /// serialized migration data still references them.
    /// </summary>
    private IReadOnlyList<Transform> GetTransitionalTargets()
    {
        List<Transform> targets = new List<Transform>();
        foreach (Transform target in mapAnchoredObjects ?? Array.Empty<Transform>())
        {
            if (IsTransitionalTarget(target))
                targets.Add(target);
        }
        return targets.AsReadOnly();
    }

    private bool IsTransitionalTarget(Transform target)
    {
        return target != null &&
               target != placementAnchor &&
               target.GetComponent<IMonsterPlacementTarget>() == null &&
               target.GetComponent<IAnimalPlacementTarget>() == null &&
               target.GetComponent<IPlantPlacementTarget>() == null;
    }

}
