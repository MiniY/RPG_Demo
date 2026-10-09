using UnityEngine;

// 动物的通用数据入口，具体行为和动画由独立控制器处理。
[DisallowMultipleComponent]
public class Animal : BaseDamageable, IAnimalPlacementTarget, IMapDependentReinitializable
{
    private bool placementAvailable = true;

    public Transform PlacementTransform => transform;
    public GameObject ReinitializationTarget => gameObject;

    public void SetPlacementAvailable(bool available)
    {
        if (placementAvailable == available)
            return;

        placementAvailable = available;
        foreach (Collider2D animalCollider in GetComponentsInChildren<Collider2D>(true))
            animalCollider.enabled = available && !IsDefeated;
        foreach (Renderer animalRenderer in GetComponentsInChildren<Renderer>(true))
            animalRenderer.enabled = available;
    }

    // 动物类型在日志中的显示名称。
    protected override string DamageableLabel => "动物";

    // 地图重定位只清理会继续写入旧地图坐标的瞬时受击位移，不重置生命或死亡状态。
    public void ReinitializeForMap()
    {
        AnimalHurtController hurtController = GetComponent<AnimalHurtController>();
        if (hurtController != null)
            hurtController.CancelTransientMapState();
    }
}
