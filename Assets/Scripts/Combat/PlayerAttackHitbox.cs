using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackHitbox : MonoBehaviour
{
    private Player player;
    private BoxCollider2D hitCollider;
    private Vector2 baseSize;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    public void Initialize(Player owner, BoxCollider2D collider)
    {
        player = owner;
        hitCollider = collider;
        baseSize = collider.size;
        hitCollider.enabled = false;
    }

    public void BeginSwing()
    {
        if (hitCollider == null || player == null)
            return;

        alreadyHit.Clear();
        hitCollider.size = new Vector2(baseSize.x * player.AttackReachMultiplier, baseSize.y);
        hitCollider.enabled = true;
    }

    public void EndSwing()
    {
        if (hitCollider == null)
            return;

        hitCollider.enabled = false;
        alreadyHit.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hitCollider == null || !hitCollider.enabled || player == null)
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !alreadyHit.Add(damageable))
            return;

        damageable.TakeDamage(player.CurrentAttackDamage, player.transform.position);
    }
}
