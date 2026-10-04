using System;
using UnityEngine;

[RequireComponent(typeof(BaseMonster))]
// 持续检测玩家距离和视线，不负责决定怪物应该进入哪个行为状态。
public class MonsterPerceptionController : MonoBehaviour
{
    [Header("目标检测")]
    [SerializeField, Min(0f)] private float detectionRadius = 4f; // 怪物能够发现玩家的最大半径。
    [SerializeField] private string targetTag = "Player"; // 能够被怪物识别为目标的标签。
    [SerializeField] private Transform sightOrigin; // 视线检测起点；为空时使用怪物父物体位置。

    [Header("视线检测")]
    [SerializeField] private LayerMask sightBlockingLayers = 1; // 能够阻挡视线的图层，默认只检测 Default 图层。

    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[16]; // 复用的射线命中结果数组。
    private Transform cachedTarget; // 已找到并缓存的玩家目标。
    private Transform currentVisibleTarget; // 当前同时满足距离与视线条件的目标。
    private Transform lastEvaluatedTarget; // 最近一次执行视线检测的目标，仅用于场景调试显示。
    private bool lastVisibilityResult; // 最近一次视线检测结果，仅用于场景调试显示。

    public event Action<Transform> OnVisibleTargetChanged; // 当前可见目标变化时通知 AI 状态机。

    public float DetectionRadius => detectionRadius; // 对外提供当前感知半径。
    public Transform CurrentVisibleTarget => currentVisibleTarget; // 对外提供最新的感知事实，不暴露检测实现。

    // 每帧独立刷新距离与视线感知，并在结果变化时发布事件。
    private void Update()
    {
        RefreshPerception();
    }

    // 禁用或回收到对象池时清除目标缓存和调试状态。
    private void OnDisable()
    {
        cachedTarget = null;
        currentVisibleTarget = null;
        lastEvaluatedTarget = null;
        lastVisibilityResult = false;
    }

    // 计算本帧可见目标；只有结果变化时才通知订阅者。
    private void RefreshPerception()
    {
        Transform target = ResolveTarget(); // 当前场景中可供检测的玩家目标。
        Transform visibleTarget = IsTargetVisible(target) ? target : null; // 本帧得到的可见目标事实。

        if (visibleTarget == currentVisibleTarget)
            return;

        currentVisibleTarget = visibleTarget;
        OnVisibleTargetChanged?.Invoke(currentVisibleTarget);
    }

    // 判断指定目标是否同时满足启用、距离和视线条件。
    private bool IsTargetVisible(Transform target)
    {
        lastEvaluatedTarget = target;
        lastVisibilityResult = false;

        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        Vector2 origin = sightOrigin != null
            ? sightOrigin.position
            : transform.position; // 本次射线检测的世界坐标起点。
        Vector2 targetPosition = GetTargetPosition(target); // 本次射线检测指向的目标中心。
        Vector2 offset = targetPosition - origin; // 怪物到目标中心的方向与距离。
        float distanceSqr = offset.sqrMagnitude; // 避免不必要开方的距离平方。

        if (distanceSqr > detectionRadius * detectionRadius)
            return false;

        if (distanceSqr <= 0.0001f)
        {
            lastVisibilityResult = true;
            return true;
        }

        float distance = Mathf.Sqrt(distanceSqr); // 射线需要检测的实际距离。
        Vector2 direction = offset / distance; // 已归一化的视线方向。
        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            direction,
            sightHits,
            distance,
            sightBlockingLayers); // 视线范围内命中的碰撞体数量。

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider2D hitCollider = sightHits[hitIndex].collider; // 当前射线命中的二维碰撞体。

            if (hitCollider == null)
                continue;

            // 忽略包围射线起点的边界碰撞体，例如覆盖整个地图的 Confiner。
            if (sightHits[hitIndex].distance <= 0.0001f && hitCollider.OverlapPoint(origin))
                continue;

            Transform hitTransform = hitCollider.transform; // 当前碰撞体所属的 Transform。

            if (IsPartOfMonster(hitTransform) || IsPartOfTarget(hitTransform, target))
                continue;

            return false;
        }

        lastVisibilityResult = true;
        return true;
    }

    // 返回缓存玩家；缓存失效时按照目标标签重新查找。
    private Transform ResolveTarget()
    {
        if (cachedTarget != null && cachedTarget.gameObject.activeInHierarchy)
            return cachedTarget;

        GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag); // 当前场景中拥有目标标签的对象。
        cachedTarget = targetObject != null ? targetObject.transform : null;
        return cachedTarget;
    }

    // 优先使用目标碰撞体中心，避免目标轴心位置导致射线偏移。
    private Vector2 GetTargetPosition(Transform target)
    {
        Collider2D targetCollider = target.GetComponent<Collider2D>(); // 目标根物体上的二维碰撞体。

        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();

        return targetCollider != null
            ? (Vector2)targetCollider.bounds.center
            : (Vector2)target.position;
    }

    // 判断射线命中的对象是否属于当前怪物自身层级。
    private bool IsPartOfMonster(Transform hitTransform)
    {
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    // 判断射线命中的对象是否属于目标自身层级。
    private bool IsPartOfTarget(Transform hitTransform, Transform target)
    {
        return hitTransform == target ||
               hitTransform.IsChildOf(target) ||
               target.IsChildOf(hitTransform);
    }

    // 在 Scene 视图显示感知半径以及最近一次视线检测结果。
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = sightOrigin != null ? sightOrigin.position : transform.position; // 调试圆和视线的起点。

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectionRadius);

        if (!Application.isPlaying || lastEvaluatedTarget == null)
            return;

        Gizmos.color = lastVisibilityResult ? Color.green : Color.red;
        Gizmos.DrawLine(origin, GetTargetPosition(lastEvaluatedTarget));
    }

    // 在 Inspector 修改数值时保证感知半径合法。
    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
    }
}
