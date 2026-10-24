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
    private Collider2D bodyCollider;
    private MapData visualGateMap;
    private bool stairVisualPriority;

    private void Awake()
    {
        if (sortingGroup == null)
            sortingGroup = GetComponentInChildren<SortingGroup>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bodyCollider = GetComponent<Collider2D>();
    }

    private void LateUpdate()
    {
        int order = hasOverride
            ? overrideOrder
            : useYSorting ? CalculateYOrder() : baseOrderOffset;
        if (!hasOverride)
        {
            int elevation = MapElevationRuntimeQuery.GetElevation(transform.position);
            PlayerColliderRoles roles = GetComponent<PlayerColliderRoles>();
            Collider2D movementBody = roles != null && roles.MovementCollider != null
                ? roles.MovementCollider : bodyCollider;
            if (MapElevationRuntimeQuery.TryGetActiveMap(out MapData map, out MapCoordinateBoundary coordinates) &&
                movementBody != null && movementBody.enabled)
            {
                if (!ReferenceEquals(map, visualGateMap)) stairVisualPriority = false;
                visualGateMap = map;
                Bounds visual = movementBody.bounds;
                foreach (SpriteRenderer renderer in spriteRenderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null)
                        visual.Encapsulate(renderer.bounds);
                stairVisualPriority = MapOutboardStairGeometry.TryGetWestSideVisualTransition(map, coordinates,
                    movementBody.bounds, visual, stairVisualPriority, out int visualElevation, out _);
                if (stairVisualPriority) elevation = Mathf.Max(elevation, visualElevation);
            }
            else
            {
                visualGateMap = null;
                stairVisualPriority = false;
            }
            order += elevation * 10;
        }

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

    private void OnDisable()
    {
        visualGateMap = null;
        stairVisualPriority = false;
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
