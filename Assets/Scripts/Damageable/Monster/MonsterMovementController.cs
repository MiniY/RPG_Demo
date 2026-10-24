using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody2D))]
// 统一处理怪物的二维移动、停止和朝向，不负责决定移动目标。
public class MonsterMovementController : MonoBehaviour
{
    [Header("移动参数")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f; // 怪物每秒移动距离。
    [SerializeField, Min(0.01f)] private float stoppingDistance = 0.05f; // 视为到达目标点的距离。

    [Header("碰撞参数")]
    [SerializeField, Min(0f)] private float collisionSkinWidth = 0.02f; // 与障碍物之间保留的最小间距。
    [SerializeField] private LayerMask obstacleLayers = Physics2D.AllLayers; // 能够阻挡怪物移动的图层。

    private Rigidbody2D body; // 怪物使用的二维刚体。
    private Vector2 destination; // 当前移动目标位置。
    private bool hasDestination; // 当前是否存在移动目标。
    private bool isMoving; // 怪物当前是否正在移动。
    private bool isMovementLocked; // 受击等临时行为是否正在锁定普通移动。
    private Vector2 facingDirection = Vector2.down; // 怪物当前面对方向。
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8]; // 单次移动检测复用的碰撞结果数组。
    private ContactFilter2D movementContactFilter; // 移动前形状投射使用的碰撞过滤条件。
    private readonly List<Vector2Int> terrainPath = new List<Vector2Int>();
    private MapData terrainPathMap;
    private Vector2Int terrainPathTarget;
    private int terrainPathIndex;
    private SortingGroup visualSortingGroup;
    private SpriteRenderer[] visualRenderers;
    private int[] originalSortingOrders;
    private int originalGroupOrder;
    private EcologyMovementStatus navigationStatus;
    public EcologyMovementStatus NavigationStatus => navigationStatus;
    public int MovementBlockingLayers => obstacleLayers.value;
    public float MovementCollisionSkin => collisionSkinWidth;
    public float MoveSpeed => moveSpeed;

    public event Action<bool> OnMovementChanged; // 移动状态变化时通知动画表现模块。
    public event Action<Vector2> OnFacingDirectionChanged; // 面对方向变化时通知动画表现模块。

    public bool IsMoving => isMoving; // 对外提供怪物是否正在移动。
    public bool IsMovementLocked => isMovementLocked; // 对外提供普通移动是否被临时锁定。
    public bool HasReachedDestination => !hasDestination; // 对外提供怪物是否已经到达目标点。
    public Vector2 FacingDirection => facingDirection; // 对外提供怪物当前面对方向。

    // 初始化二维刚体引用。
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        ConfigureMovementContactFilter();
        visualSortingGroup = GetComponentInChildren<SortingGroup>(true);
        if (visualSortingGroup != null)
            originalGroupOrder = visualSortingGroup.sortingOrder;
        else
        {
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalSortingOrders = new int[visualRenderers.Length];
            for (int i = 0; i < visualRenderers.Length; i++)
                originalSortingOrders[i] = visualRenderers[i].sortingOrder;
        }
    }

    // 在固定时间步中执行实际移动。
    private void FixedUpdate()
    {
        MoveTowardsDestination();
    }

    private void LateUpdate()
    {
        int heightOffset = MapElevationRuntimeQuery.GetElevation(transform.position) * 10;
        if (visualSortingGroup != null)
            visualSortingGroup.sortingOrder = originalGroupOrder + heightOffset;
        else if (visualRenderers != null)
            for (int i = 0; i < visualRenderers.Length; i++)
                if (visualRenderers[i] != null)
                    visualRenderers[i].sortingOrder = originalSortingOrders[i] + heightOffset;
    }

    // 禁用或回收到对象池时停止移动。
    private void OnDisable()
    {
        isMovementLocked = false;
        Stop();
        var resident = GetComponent<EcologyResidentBinding>();
        if (resident != null && resident.IsBound) navigationStatus = EcologyMovementStatus.Retired;
    }

    // 设置新的移动目标位置。
    public void SetDestination(Vector2 targetPosition)
    {
        navigationStatus = EcologyMovementStatus.Moving;
        destination = targetPosition;
        hasDestination = true;
    }

    // 在不产生位移时更新怪物面对方向，供追击停止和攻击准备使用。
    public void FaceDirection(Vector2 direction)
    {
        SetFacingDirection(direction);
    }

    // 停止怪物移动并清除当前目标位置。
    public void Stop()
    {
        navigationStatus = EcologyMovementStatus.Idle;
        hasDestination = false;
        terrainPath.Clear();
        terrainPathMap = null;

        if (body != null)
            body.velocity = Vector2.zero;

        SetMoving(false);
    }

    // 设置普通移动锁定状态，供受击等临时行为独占位置控制。
    public void SetMovementLocked(bool isLocked)
    {
        isMovementLocked = isLocked;

        if (isMovementLocked)
        {
            if (body != null)
                body.velocity = Vector2.zero;

            SetMoving(false);
        }
    }

    // 根据当前位置向目标位置移动一步。
    private void MoveTowardsDestination()
    {
        if (isMovementLocked || !hasDestination || body == null)
        {
            SetMoving(false);
            return;
        }

        if (!TryTerrainWaypoint(out Vector2 moveTarget))
        {
            Stop();
            var resident = GetComponent<EcologyResidentBinding>();
            if (resident != null && resident.IsBound) navigationStatus = resident.CanAct ? EcologyMovementStatus.NoPath : EcologyMovementStatus.Retired;
            return;
        }
        Vector2 offset = moveTarget - body.position;
        float distance = offset.magnitude;

        var returnResident = GetComponent<EcologyResidentBinding>();
        bool needsCompleteHome = returnResident != null && returnResident.IsBound && returnResident.Excursion.ReturnLatched &&
            returnResident.TryGetHomeDistance(out int remainingHomeDistance, out _) && remainingHomeDistance > 0;
        // A waypoint tolerance cannot certify that a returning full foot is home.
        // Finish the remaining sub-step instead of stopping just outside its domain.
        if (distance <= stoppingDistance && !needsCompleteHome)
        {
            var resident = GetComponent<EcologyResidentBinding>();
            if (resident != null && resident.IsBound)
            {
                bool arrived = (destination - body.position).sqrMagnitude <= stoppingDistance * stoppingDistance ||
                    resident.Excursion.ReturnLatched && resident.TryGetHomeDistance(out int homeDistance, out _) && homeDistance == 0;
                Stop(); navigationStatus = arrived ? EcologyMovementStatus.Arrived : EcologyMovementStatus.Blocked;
                return;
            }
            if (terrainPathIndex < terrainPath.Count)
                terrainPathIndex++;
            else
                Stop();
            return;
        }

        Vector2 direction = offset / distance;
        SetFacingDirection(direction);

        float stepDistance = moveSpeed * Time.fixedDeltaTime;

        if (stepDistance <= 0f)
        {
            SetMoving(false);
            return;
        }

        float requestedMoveDistance = Mathf.Min(stepDistance, distance);
        float allowedMoveDistance = GetAllowedMoveDistance(direction, requestedMoveDistance);

        if (allowedMoveDistance <= 0.0001f)
        {
            Stop();
            navigationStatus = EcologyMovementStatus.Blocked;
            return;
        }

        Vector2 nextPosition = body.position + direction * allowedMoveDistance;
        bool wasBlocked = allowedMoveDistance + 0.0001f < requestedMoveDistance;

        if (!EcologyMotionBody.TryRequest(transform, nextPosition, EcologyMotionKind.Normal))
            body.MovePosition(nextPosition);
        SetMoving(true);

        if (wasBlocked)
        {
            Stop();
            navigationStatus = EcologyMovementStatus.Blocked;
            return;
        }
        if ((moveTarget - nextPosition).sqrMagnitude <=
            stoppingDistance * stoppingDistance)
        {
            var resident = GetComponent<EcologyResidentBinding>();
            if (resident != null && resident.IsBound) return; // Confirm regional arrival from the next actual body pose.
            if (terrainPathIndex < terrainPath.Count)
                terrainPathIndex++;
            else
                hasDestination = false;
        }
    }

    private bool TryTerrainWaypoint(out Vector2 waypoint)
    {
        waypoint = destination;
        var resident = GetComponent<EcologyResidentBinding>();
        if (resident != null && resident.IsBound)
            return resident.TryMovementWaypoint(body.position, destination, out waypoint);
        if (!MapElevationRuntimeQuery.TryGetActiveMap(out MapData map,
                out MapCoordinateBoundary coordinates))
            return true;

        Vector2Int currentCell = coordinates.WorldToCell(body.position);
        Vector2Int targetCell = coordinates.WorldToCell(destination);
        if (!map.IsInside(currentCell) || !map.IsInside(targetCell) ||
            !map.IsWalkable(currentCell) || !map.IsWalkable(targetCell))
            return false;
        if (currentCell == targetCell)
        {
            terrainPath.Clear();
            return true;
        }

        bool needsPath = terrainPathMap != map || terrainPathTarget != targetCell ||
            terrainPathIndex >= terrainPath.Count ||
            (terrainPath.Count > 0 && currentCell != terrainPath[terrainPathIndex - 1] &&
             currentCell != terrainPath[terrainPathIndex]);
        if (needsPath)
        {
            terrainPathMap = map;
            terrainPathTarget = targetCell;
            if (!MapElevationRuntimeQuery.TryFindPath(map, currentCell,
                    targetCell, terrainPath))
                return false;
            terrainPathIndex = 1;
        }
        while (terrainPathIndex < terrainPath.Count &&
               ((Vector2)coordinates.CellToWorld(terrainPath[terrainPathIndex]) - body.position)
               .sqrMagnitude <= stoppingDistance * stoppingDistance)
            terrainPathIndex++;
        if (terrainPathIndex < terrainPath.Count)
            waypoint = coordinates.CellToWorld(terrainPath[terrainPathIndex]);
        return true;
    }

    // 把怪物刚体配置为脚本驱动，避免玩家的物理碰撞直接推动怪物。
    private void ConfigureRigidbody()
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.velocity = Vector2.zero;
    }

    // 创建忽略触发器且只检测指定障碍图层的移动过滤条件。
    private void ConfigureMovementContactFilter()
    {
        movementContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        movementContactFilter.SetLayerMask(obstacleLayers);
    }

    // 在真正移动前投射怪物碰撞体，计算不会穿入障碍物的安全距离。
    private float GetAllowedMoveDistance(Vector2 direction, float requestedMoveDistance)
    {
        if (body == null || requestedMoveDistance <= 0f)
            return 0f;

        float castDistance = requestedMoveDistance + collisionSkinWidth;
        int hitCount = body.Cast(direction, movementContactFilter, movementHits, castDistance);
        float allowedMoveDistance = requestedMoveDistance;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit2D hit = movementHits[hitIndex]; // 当前检测到的障碍物。

            if (hit.collider == null)
                continue;

            // A cast starting in a slight overlap reports distance zero even when
            // this step is separating the Monster from the obstacle.
            if (hit.distance <= 0f && Vector2.Dot(hit.normal, direction) > 0f)
                continue;

            float distanceBeforeObstacle = Mathf.Max(0f, hit.distance - collisionSkinWidth);
            allowedMoveDistance = Mathf.Min(allowedMoveDistance, distanceBeforeObstacle);
        }

        return allowedMoveDistance;
    }

    // 更新移动状态，并只在状态改变时发出事件。
    private void SetMoving(bool moving)
    {
        if (isMoving == moving)
            return;

        isMoving = moving;
        OnMovementChanged?.Invoke(isMoving);
    }

    // 更新面对方向，并只在方向真正改变时发出事件。
    private void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 normalizedDirection = direction.normalized;

        if ((facingDirection - normalizedDirection).sqrMagnitude <= 0.0001f)
            return;

        facingDirection = normalizedDirection;
        OnFacingDirectionChanged?.Invoke(facingDirection);
    }

    // 在 Inspector 修改数值时保证参数合法。
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0.01f, stoppingDistance);
        collisionSkinWidth = Mathf.Max(0f, collisionSkinWidth);
        ConfigureMovementContactFilter();
    }
}
