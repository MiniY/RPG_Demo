using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 将场景中原本按静态地图摆放的对象映射到最近一次生成的随机地图。
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
    /// 保存每个对象相对原玩家位置的网格偏移。
    /// </summary>
    private readonly Dictionary<Transform, Vector2Int> authoredCellOffsets =
        new Dictionary<Transform, Vector2Int>();

    /// <summary>
    /// 是否已经在随机地图移动对象前捕获原场景布局。
    /// </summary>
    private bool hasCapturedAuthoredLayout;

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

    /// <summary>Explicit objects whose map-derived runtime state must be refreshed after PlayerReady.</summary>
    public IReadOnlyList<GameObject> RestartAfterPlacement => restartAfterPlacement;

    /// <summary>
    /// 在其他组件的 Start（启动）前捕获 Main 场景原始相对布局。
    /// </summary>
    private void Awake()
    {
        if (!LoadSerializedAuthoredLayout())
            CaptureAuthoredLayout();
    }

    /// <summary>
    /// 监听地图生成完成事件。
    /// </summary>
    private void OnEnable()
    {
        if (mapController == null)
            return;

        mapController.MapGenerated -= HandleMapGenerated;
        mapController.MapGenerated += HandleMapGenerated;
    }

    /// <summary>
    /// 覆盖地图控制器先于适配器完成 Start 的极端执行顺序。
    /// </summary>
    private void Start()
    {
        if (mapController != null && mapController.LastGeneratedMap != null)
            PlaceObjects(mapController.LastGeneratedMap);
    }

    /// <summary>
    /// 取消地图事件订阅。
    /// </summary>
    private void OnDisable()
    {
        if (mapController != null)
            mapController.MapGenerated -= HandleMapGenerated;
    }

    /// <summary>
    /// 把已登记对象放到可行走且未被随机装饰占用的地图单元。
    /// </summary>
    /// <param name="mapData">本次生成完成的地图数据。</param>
    public void PlaceObjects(MapData mapData)
    {
        if (mapData == null)
            throw new ArgumentNullException(nameof(mapData));

        if (!hasCapturedAuthoredLayout &&
            !LoadSerializedAuthoredLayout() &&
            !CaptureAuthoredLayout())
        {
            return;
        }

        if (mapController == null || mapController.TilemapRenderer == null)
            return;

        HashSet<Vector2Int> unavailableCells = new HashSet<Vector2Int>
        {
            mapData.SpawnCell,
            mapData.ExitCell
        };

        MapSimpleDecorationData decorations =
            mapController.LastGeneratedSimpleDecorations;

        if (decorations != null)
        {
            foreach (Vector2Int occupiedCell in decorations.OccupiedCells)
                unavailableCells.Add(occupiedCell);
        }

        foreach (Transform target in GetTransitionalTargets())
        {
            if (target == null || !authoredCellOffsets.TryGetValue(target, out Vector2Int offset))
                continue;

            Vector2Int preferredCell = mapData.SpawnCell + offset;
            if (!TryFindNearestAvailableCell(
                    mapData,
                    preferredCell,
                    unavailableCells,
                    out Vector2Int placementCell))
            {
                Debug.LogWarning($"无法为场景对象 {target.name} 找到随机地图可用位置。", target);
                continue;
            }

            MoveTargetToCell(
                target,
                mapData,
                placementCell);
            unavailableCells.Add(placementCell);
        }
    }

    /// <summary>
    /// 在生成完成后执行场景对象定位。
    /// </summary>
    /// <param name="mapData">本次生成完成的地图数据。</param>
    private void HandleMapGenerated(MapData mapData)
    {
        PlaceObjects(mapData);
    }

    /// <summary>
    /// 捕获每个对象相对 Main 玩家原始位置的网格偏移。
    /// </summary>
    /// <returns>必要引用完整时返回 true。</returns>
    private bool CaptureAuthoredLayout()
    {
        if (mapController == null || mapController.TilemapRenderer == null ||
            mapController.TilemapRenderer.GroundTilemap == null || placementAnchor == null)
        {
            return false;
        }

        authoredCellOffsets.Clear();
        MapCoordinateBoundary coordinates = mapController.TilemapRenderer.Coordinates;
        Vector2Int anchorCell = coordinates.WorldToCell(placementAnchor.position);

        foreach (Transform target in GetTransitionalTargets())
        {
            if (target == null || target == placementAnchor)
                continue;

            Vector2Int targetCell = coordinates.WorldToCell(target.position);
            authoredCellOffsets[target] = targetCell - anchorCell;
        }

        hasCapturedAuthoredLayout = true;
        return true;
    }

    /// <summary>
    /// 读取集成时记录的 Main 场景相对布局，避免生成后移动的玩家改变偏移基准。
    /// </summary>
    /// <returns>序列化布局完整时返回 true。</returns>
    private bool LoadSerializedAuthoredLayout()
    {
        if (mapAnchoredObjects == null || authoredCellOffsetValues == null ||
            mapAnchoredObjects.Length != authoredCellOffsetValues.Length)
        {
            return false;
        }

        authoredCellOffsets.Clear();

        for (int index = 0; index < mapAnchoredObjects.Length; index++)
        {
            Transform target = mapAnchoredObjects[index];
            if (IsTransitionalTarget(target))
                authoredCellOffsets[target] = authoredCellOffsetValues[index];
        }

        hasCapturedAuthoredLayout = true;
        return true;
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
               target.GetComponent<IAnimalPlacementTarget>() == null;
    }

    /// <summary>
    /// 按曼哈顿距离寻找最接近期望位置的可用单元。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    /// <param name="preferredCell">对象期望使用的网格坐标。</param>
    /// <param name="unavailableCells">装饰、出生点、出口和其他对象占用的单元。</param>
    /// <param name="result">找到的可用网格坐标。</param>
    /// <returns>找到可用单元时返回 true。</returns>
    private static bool TryFindNearestAvailableCell(
        MapData mapData,
        Vector2Int preferredCell,
        HashSet<Vector2Int> unavailableCells,
        out Vector2Int result)
    {
        int maximumRadius = mapData.Width + mapData.Height;

        for (int radius = 0; radius <= maximumRadius; radius++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                int offsetY = radius - Mathf.Abs(offsetX);

                Vector2Int upperCandidate = preferredCell +
                    new Vector2Int(offsetX, offsetY);
                if (IsAvailable(mapData, upperCandidate, unavailableCells))
                {
                    result = upperCandidate;
                    return true;
                }

                if (offsetY == 0)
                    continue;

                Vector2Int lowerCandidate = preferredCell +
                    new Vector2Int(offsetX, -offsetY);
                if (IsAvailable(mapData, lowerCandidate, unavailableCells))
                {
                    result = lowerCandidate;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// 判断单元是否可以承载一个 Main 场景对象。
    /// </summary>
    private static bool IsAvailable(
        MapData mapData,
        Vector2Int cell,
        HashSet<Vector2Int> unavailableCells)
    {
        return mapData.IsInside(cell) &&
               mapData.IsWalkable(cell) &&
               !unavailableCells.Contains(cell);
    }

    /// <summary>
    /// 移动对象。地图依赖状态由 PlayerReady 之后的 reinitialization service 统一刷新。
    /// </summary>
    private void MoveTargetToCell(
        Transform target,
        MapData mapData,
        Vector2Int placementCell)
    {
        Vector3 worldPosition = mapController.TilemapRenderer.Coordinates.CellToWorld(
            mapData,
            placementCell);
        worldPosition.z = target.position.z;
        target.position = worldPosition;
    }
}
