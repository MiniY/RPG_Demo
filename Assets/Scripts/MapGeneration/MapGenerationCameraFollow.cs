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
    /// 摄像机当前的平滑速度缓存。
    /// </summary>
    private Vector3 currentVelocity;

    /// <summary>
    /// 在所有角色移动完成后更新摄像机位置。
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position + offset;
        transform.position = smoothTime <= 0f
            ? targetPosition
            : Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }
}
