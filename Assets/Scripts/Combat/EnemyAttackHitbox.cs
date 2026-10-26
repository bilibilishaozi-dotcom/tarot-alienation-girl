using UnityEngine;

public class EnemyAttackHitbox : MonoBehaviour
{
    private BasicEnemy owner;
    private Collider2D hitCollider;
    private bool hasHitThisSwing;

    public void Initialize(BasicEnemy enemy, Collider2D collider)
    {
        owner = enemy;
        hitCollider = collider;
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;
    }

    public void BeginSwing()
    {
        if (hitCollider == null)
            return;

        hasHitThisSwing = false;
        hitCollider.enabled = true;
        TryHitOverlapping();
    }

    public void EndSwing()
    {
        if (hitCollider == null)
            return;

        hitCollider.enabled = false;
        hasHitThisSwing = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHitOverlapping()
    {
        if (hitCollider == null)
            return;

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(hitCollider.bounds.center, hitCollider.bounds.size, 0f);
        for (int i = 0; i < overlaps.Length; i++)
            TryHit(overlaps[i]);
    }

    private void TryHit(Collider2D other)
    {
        if (hitCollider == null || !hitCollider.enabled || owner == null || !owner.IsStrikeActive || hasHitThisSwing)
            return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null)
            return;

        hasHitThisSwing = true;
        player.TakeDamage(owner.AttackDamage, owner.transform.position);
    }
}
