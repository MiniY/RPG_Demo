using System.Collections.Generic;
using UnityEngine;

public class MountainGateTrigger : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private int playerOrderInside = 15;
    [SerializeField, Range(0f, 1f)] private float directionThreshold = 0.25f;
    [SerializeField] private float moveDeadZone = 0.01f;
    [SerializeField] private float positionDeadZone = 0.05f;

    [Header("Direction")]
    [SerializeField] private Transform uphillDirectionReference;
    [SerializeField] private Vector2 fallbackUphillDirection = Vector2.up;

    [Header("Colliders")]
    [SerializeField] private Collider2D[] mountainColliders;
    [SerializeField] private Collider2D[] borderColliders;
    [SerializeField] private bool showDebugLog = true;

    private readonly Dictionary<Collider2D, bool> originalStates = new Dictionary<Collider2D, bool>();
    private PlayerSortingController currentPlayer;
    private bool isOnMountain;

    private void Awake()
    {
        CacheOriginalStates(mountainColliders);
        CacheOriginalStates(borderColliders);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleGate(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleGate(other);
    }

    private void OnDisable()
    {
        if (currentPlayer != null)
            currentPlayer.ClearOverrideOrder();

        RestoreOriginalStates();
        currentPlayer = null;
        isOnMountain = false;
    }

    private void TryHandleGate(Collider2D other)
    {
        var playerAction = other.GetComponentInParent<PlayerAction>();
        if (playerAction == null || !playerAction.CompareTag(playerTag))
            return;

        var playerSorting = playerAction.GetComponent<PlayerSortingController>();
        if (playerSorting == null)
            return;

        currentPlayer = playerSorting;

        if (!TryGetTransition(playerAction, out bool enterMountain))
            return;

        if (enterMountain)
            EnterMountain(playerSorting);
        else
            ExitMountain(playerSorting);
    }

    private bool TryGetTransition(PlayerAction playerAction, out bool enterMountain)
    {
        Vector2 uphillDirection = GetUphillDirection();
        Vector2 moveInput = playerAction.MoveInput;

        if (moveInput.sqrMagnitude > moveDeadZone * moveDeadZone)
        {
            float moveDot = Vector2.Dot(moveInput.normalized, uphillDirection);

            if (moveDot >= directionThreshold)
            {
                enterMountain = true;
                return true;
            }

            if (moveDot <= -directionThreshold)
            {
                enterMountain = false;
                return true;
            }
        }

        Vector2 relativePosition = (Vector2)playerAction.transform.position - (Vector2)transform.position;
        float sideDot = Vector2.Dot(relativePosition, uphillDirection);

        if (Mathf.Abs(sideDot) <= positionDeadZone)
        {
            enterMountain = false;
            return false;
        }

        enterMountain = sideDot < 0f;
        return true;
    }

    private Vector2 GetUphillDirection()
    {
        Vector2 direction = uphillDirectionReference != null
            ? (Vector2)uphillDirectionReference.up
            : fallbackUphillDirection;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        return direction.normalized;
    }

    private void EnterMountain(PlayerSortingController playerSorting)
    {
        if (isOnMountain)
            return;

        isOnMountain = true;
        playerSorting.SetOverrideOrder(playerOrderInside);
        SetCollidersEnabled(mountainColliders, false);
        SetCollidersEnabled(borderColliders, true);

        if (showDebugLog)
            Debug.Log($"[{name}] Enter mountain.", this);
    }

    private void ExitMountain(PlayerSortingController playerSorting)
    {
        if (!isOnMountain)
            return;

        isOnMountain = false;
        playerSorting.ClearOverrideOrder();
        SetCollidersEnabled(mountainColliders, true);
        SetCollidersEnabled(borderColliders, false);

        if (showDebugLog)
            Debug.Log($"[{name}] Exit mountain.", this);
    }

    private void CacheOriginalStates(Collider2D[] sourceColliders)
    {
        foreach (var col in GetControlledColliders(sourceColliders))
        {
            if (col != null && !originalStates.ContainsKey(col))
                originalStates.Add(col, col.enabled);
        }
    }

    private void SetCollidersEnabled(Collider2D[] sourceColliders, bool enabled)
    {
        foreach (var col in GetControlledColliders(sourceColliders))
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    private void RestoreOriginalStates()
    {
        foreach (var pair in originalStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }
    }

    private IEnumerable<Collider2D> GetControlledColliders(Collider2D[] sourceColliders)
    {
        var visited = new HashSet<Collider2D>();

        if (sourceColliders == null)
            yield break;

        foreach (var source in sourceColliders)
        {
            if (source == null)
                continue;

            foreach (var sibling in source.GetComponents<Collider2D>())
            {
                if (visited.Add(sibling))
                    yield return sibling;
            }
        }
    }
}
