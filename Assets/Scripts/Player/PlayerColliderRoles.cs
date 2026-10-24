using System;
using UnityEngine;

/// <summary>Serialized Player movement and body references; owns no damage or elevation state.</summary>
[DisallowMultipleComponent]
public sealed class PlayerColliderRoles : MonoBehaviour
{
    [SerializeField] private Collider2D movementCollider;
    [SerializeField] private Collider2D hurtbox;

    public Collider2D MovementCollider => movementCollider != null && movementCollider.enabled &&
        movementCollider.gameObject.activeInHierarchy ? movementCollider : null;
    public Collider2D Hurtbox => hurtbox;

    private void Awake()
    {
        // Runtime-only legacy fixtures configure after AddComponent; the formal prefab has both references.
        if (movementCollider != null || hurtbox != null)
            Configure(movementCollider, hurtbox);
    }

    public void Configure(Collider2D movement, Collider2D body)
    {
        if (movement == null || body == null || movement == body || movement.isTrigger || !body.isTrigger ||
            movement.attachedRigidbody == null || movement.attachedRigidbody != body.attachedRigidbody ||
            body.transform != transform)
            throw new ArgumentException("Collider roles require one solid movement shape and a root body trigger on the same Rigidbody2D.");
        movementCollider = movement;
        hurtbox = body;
    }
}
