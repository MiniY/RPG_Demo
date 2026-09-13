using UnityEngine;
using UnityEngine.Rendering;

public class PlayerSortingController : MonoBehaviour
{
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private bool useYSorting = false;
    [SerializeField] private float yFactor = 1f;
    [SerializeField] private int baseOrderOffset = 5;

    private bool hasOverride;
    private int overrideOrder;

    private void Awake()
    {
        if (sortingGroup == null)
            sortingGroup = GetComponentInChildren<SortingGroup>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void LateUpdate()
    {
        int order = hasOverride
            ? overrideOrder
            : useYSorting ? CalculateYOrder() : baseOrderOffset;

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            return;
        }

        foreach (var renderer in spriteRenderers)
        {
            if (renderer != null)
                renderer.sortingOrder = order;
        }
    }

    private int CalculateYOrder()
    {
        return baseOrderOffset - Mathf.RoundToInt(transform.position.y * yFactor);
    }

    public void SetOverrideOrder(int order)
    {
        hasOverride = true;
        overrideOrder = order;
    }

    public void ClearOverrideOrder()
    {
        hasOverride = false;
    }
}
