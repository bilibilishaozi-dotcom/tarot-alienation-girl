using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackHitbox : MonoBehaviour
{
    private Player player;
    private BoxCollider2D hitCollider;
    private Vector2 baseSize;
    private Vector2 baseOffset;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    // airattack_1 里那道脚下刀光的包围盒。相对玩家原点，朝右时 x 为正。
    // 画布 243、PPU 97、轴心 (0.471, 0.535)。最大亮色刀光块约 154×85 像素，
    // 中心在轴心右 0.49、下 0.84。地面攻击仍用原来的 1.25×0.85。
    private static readonly Vector2 AirSlashSize = new Vector2(1.59f, 0.88f);
    private static readonly Vector2 AirSlashCenter = new Vector2(0.49f, -0.84f);

    public void Initialize(Player owner, BoxCollider2D collider)
    {
        player = owner;
        hitCollider = collider;
        baseSize = collider.size;
        baseOffset = collider.offset;
        hitCollider.enabled = false;
    }

    public void BeginSwing()
    {
        if (hitCollider == null || player == null)
            return;

        alreadyHit.Clear();
        if (player.IsAirAttack)
        {
            hitCollider.size = AirSlashSize;
            hitCollider.offset = AirSlashCenter - (Vector2)transform.localPosition;
        }
        else
        {
            float reach = Mathf.Max(1f, player.AttackReachMultiplier);
            float extra = baseSize.x * (reach - 1f);
            hitCollider.size = new Vector2(baseSize.x + extra, baseSize.y);
            hitCollider.offset = new Vector2(baseOffset.x + extra * 0.5f, baseOffset.y);
        }
        hitCollider.enabled = true;
        Physics2D.SyncTransforms();
        bool feel = HitFeel.Enabled;
        AttackFeedback.Swing(hitCollider.bounds, player.facingDir, player.AttackReachMultiplier > 1.01f, !feel);
        if (feel)
            HitFeel.WeaponSwing();
        else
            Juice.Slash();
        TryHitOverlapping();
    }

    public void EndSwing()
    {
        if (hitCollider == null)
            return;

        hitCollider.enabled = false;
        hitCollider.size = baseSize;
        hitCollider.offset = baseOffset;
        alreadyHit.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
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
        if (hitCollider == null || !hitCollider.enabled || player == null || other == null)
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !alreadyHit.Add(damageable))
            return;

        bool empowered = player.AttackReachMultiplier > 1.01f || player.CurrentAttackDamage > 1;
        int damage = player.CurrentAttackDamage;
        damageable.TakeDamage(damage, player.transform.position);
        Component hitComponent = damageable as Component;
        player.NotifyAttackHit(hitComponent != null ? hitComponent.name : other.name);
        BasicEnemy enemy = other.GetComponentInParent<BasicEnemy>();
        bool blocked = enemy != null && enemy.IsBlocked;
        bool feel = HitFeel.Enabled;
        if (!feel)
            AttackFeedback.Hit(other.bounds.center, empowered);
        HitFeel.WeaponHit(player, hitComponent != null ? hitComponent : other, other.bounds.center, blocked);
    }
}
