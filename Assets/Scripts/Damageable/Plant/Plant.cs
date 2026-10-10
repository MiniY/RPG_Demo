using UnityEngine;

// 植物的通用数据入口，具体行为和动画由独立控制器处理。
[DisallowMultipleComponent]
public class Plant : BaseDamageable, IPlantPlacementTarget, IMapDependentReinitializable
{
    private bool placementAvailable = true;

    public Transform PlacementTransform => transform;
    public GameObject ReinitializationTarget => gameObject;

    public void SetPlacementAvailable(bool available)
    {
        if (placementAvailable == available)
            return;

        placementAvailable = available;
        foreach (Collider2D plantCollider in GetComponentsInChildren<Collider2D>(true))
            plantCollider.enabled = available && !IsDefeated;
        foreach (Renderer plantRenderer in GetComponentsInChildren<Renderer>(true))
            plantRenderer.enabled = available && !IsDefeated;
    }

    // 植物类型在日志中的显示名称。
    protected override string DamageableLabel => "植物";

    // 地图重定位只清理瞬时受击闪烁，不重置生命、死亡或掉落状态。
    public void ReinitializeForMap()
    {
        PlantBehaviorController behaviorController = GetComponent<PlantBehaviorController>();
        if (behaviorController != null)
            behaviorController.CancelTransientMapState();
    }
}
