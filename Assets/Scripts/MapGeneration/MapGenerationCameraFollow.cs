using UnityEngine;

/// <summary>
/// 让测试摄像机平滑跟随玩家，避免依赖现有山地或 UI 系统。
/// </summary>
public class MapGenerationCameraFollow : MonoBehaviour
{
    /// <summary>
    /// 摄像机需要跟随的目标 Transform（变换组件）。
    /// </summary>
    [SerializeField] private Transform target;

    /// <summary>
    /// 摄像机相对目标的世界坐标偏移。
    /// </summary>
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    /// <summary>
    /// 摄像机追踪目标时使用的平滑时间。
    /// </summary>
    [SerializeField, Min(0f)] private float smoothTime = 0.08f;

    /// <summary>
    /// 提供地图世界边界的 Tilemap 渲染器。
    /// </summary>
    [SerializeField] private MapTilemapRenderer tilemapRenderer;

    /// <summary>
    /// 是否把摄像机限制在地图边界内。
    /// </summary>
    [SerializeField] private bool clampToMapBounds = true;

    /// <summary>
    /// 摄像机与地图边界之间额外保留的世界坐标间距。
    /// </summary>
    [SerializeField] private Vector2 boundaryPadding = Vector2.zero;

    /// <summary>
    /// 摄像机当前的平滑速度缓存。
    /// </summary>
    private Vector3 currentVelocity;

    /// <summary>
    /// 当前对象上的 Camera（摄像机）组件缓存。
    /// </summary>
    private Camera cameraComponent;

    /// <summary>
    /// 缓存摄像机组件，并在场景没有显式引用时寻找地图渲染器。
    /// </summary>
    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();

        if (tilemapRenderer == null)
            tilemapRenderer = FindObjectOfType<MapTilemapRenderer>();
    }

    /// <summary>
    /// 在所有角色移动完成后更新摄像机位置。
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position + offset;
        Vector2 minimumCameraPosition = Vector2.zero;
        Vector2 maximumCameraPosition = Vector2.zero;
        bool hasBounds = false;

        if (clampToMapBounds)
        {
            hasBounds = TryGetCameraLimits(
                out minimumCameraPosition,
                out maximumCameraPosition);
        }

        if (hasBounds)
        {
            targetPosition = ClampPositionToBounds(
                targetPosition,
                minimumCameraPosition,
                maximumCameraPosition);
        }

        Vector3 nextPosition = smoothTime <= 0f
            ? targetPosition
            : Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

        transform.position = hasBounds
            ? ClampPositionToBounds(nextPosition, minimumCameraPosition, maximumCameraPosition)
            : nextPosition;
    }

    /// <summary>
    /// 根据地图边界和正交摄像机参数计算摄像机中心的允许范围。
    /// </summary>
    /// <param name="mapBounds">地图世界坐标边界。</param>
    /// <param name="orthographicSize">正交摄像机的可视半高。</param>
    /// <param name="aspect">摄像机视口宽高比。</param>
    /// <param name="padding">地图边界内侧额外保留的间距。</param>
    /// <param name="minimumCameraPosition">摄像机中心的最小坐标。</param>
    /// <param name="maximumCameraPosition">摄像机中心的最大坐标。</param>
    public static void CalculateCameraLimits(
        Bounds mapBounds,
        float orthographicSize,
        float aspect,
        Vector2 padding,
        out Vector2 minimumCameraPosition,
        out Vector2 maximumCameraPosition)
    {
        float halfHeight = Mathf.Max(0f, orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);

        minimumCameraPosition = new Vector2(
            mapBounds.min.x + halfWidth + Mathf.Max(0f, padding.x),
            mapBounds.min.y + halfHeight + Mathf.Max(0f, padding.y));
        maximumCameraPosition = new Vector2(
            mapBounds.max.x - halfWidth - Mathf.Max(0f, padding.x),
            mapBounds.max.y - halfHeight - Mathf.Max(0f, padding.y));
    }

    /// <summary>
    /// 把摄像机位置限制在允许的二维坐标范围内。
    /// </summary>
    /// <param name="position">待限制的摄像机位置。</param>
    /// <param name="minimumCameraPosition">摄像机中心的最小坐标。</param>
    /// <param name="maximumCameraPosition">摄像机中心的最大坐标。</param>
    /// <returns>限制后的摄像机位置。</returns>
    public static Vector3 ClampPositionToBounds(
        Vector3 position,
        Vector2 minimumCameraPosition,
        Vector2 maximumCameraPosition)
    {
        position.x = ClampAxis(
            position.x,
            minimumCameraPosition.x,
            maximumCameraPosition.x);
        position.y = ClampAxis(
            position.y,
            minimumCameraPosition.y,
            maximumCameraPosition.y);
        return position;
    }

    /// <summary>
    /// 获取当前摄像机中心可以使用的最小和最大坐标。
    /// </summary>
    /// <param name="minimumCameraPosition">摄像机中心的最小坐标。</param>
    /// <param name="maximumCameraPosition">摄像机中心的最大坐标。</param>
    /// <returns>地图和摄像机参数都有效时返回 true。</returns>
    private bool TryGetCameraLimits(
        out Vector2 minimumCameraPosition,
        out Vector2 maximumCameraPosition)
    {
        minimumCameraPosition = Vector2.zero;
        maximumCameraPosition = Vector2.zero;

        if (cameraComponent == null || !cameraComponent.orthographic || tilemapRenderer == null)
            return false;

        if (!tilemapRenderer.TryGetWorldBounds(out Bounds mapBounds))
            return false;

        CalculateCameraLimits(
            mapBounds,
            cameraComponent.orthographicSize,
            cameraComponent.aspect,
            boundaryPadding,
            out minimumCameraPosition,
            out maximumCameraPosition);
        return true;
    }

    /// <summary>
    /// 处理地图小于摄像机可视区域时的单轴范围限制。
    /// </summary>
    /// <param name="value">待限制的坐标。</param>
    /// <param name="minimum">最小坐标。</param>
    /// <param name="maximum">最大坐标。</param>
    /// <returns>限制后的坐标。</returns>
    private static float ClampAxis(float value, float minimum, float maximum)
    {
        if (minimum > maximum)
            return (minimum + maximum) * 0.5f;

        return Mathf.Clamp(value, minimum, maximum);
    }

    /// <summary>
    /// 在 Inspector（检视面板）修改参数时修正边界配置。
    /// </summary>
    private void OnValidate()
    {
        smoothTime = Mathf.Max(0f, smoothTime);
        boundaryPadding.x = Mathf.Max(0f, boundaryPadding.x);
        boundaryPadding.y = Mathf.Max(0f, boundaryPadding.y);
    }
}
