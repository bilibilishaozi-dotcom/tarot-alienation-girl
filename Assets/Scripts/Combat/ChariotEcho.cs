using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChariotEcho : MonoBehaviour
{
    private const float TrailDistance = 0.95f;

    private Player player;
    private SpriteRenderer source;
    private SpriteRenderer body;
    private SpriteRenderer mid;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    private readonly Collider2D[] overlaps = new Collider2D[8];
    private int damage = 1;
    private bool armed = true;

    public static ChariotEcho Spawn(Player owner, int direction)
    {
        if (owner == null || !owner.HasChariot)
            return null;

        SpriteRenderer visual = owner.GetComponentInChildren<SpriteRenderer>();
        GameObject root = new GameObject("ChariotEcho");
        ChariotEcho echo = root.AddComponent<ChariotEcho>();
        echo.player = owner;
        echo.source = visual;
        echo.damage = Mathf.Max(1, owner.ChariotEchoDamage);
        echo.body = CreateBody(root.transform, "Body", 0);
        echo.mid = CreateBody(root.transform, "Mid", 1);
        echo.Place(direction);
        return echo;
    }

    public void Tick(int direction)
    {
        if (!armed || player == null)
            return;

        Place(direction);
        Strike(direction);
    }

    public void End()
    {
        armed = false;
        if (isActiveAndEnabled)
            StartCoroutine(FadeAndDestroy());
        else
            Destroy(gameObject);
    }

    private void Place(int direction)
    {
        int face = direction >= 0 ? 1 : -1;
        Vector3 origin = source != null ? source.transform.position : player.transform.position;
        Vector3 back = Vector3.right * face;
        transform.position = origin - back * TrailDistance;
        transform.rotation = player.transform.rotation;
        CopySprite(body, 0.62f);
        CopySprite(mid, 0.28f);
        if (mid != null)
            mid.transform.position = origin - back * (TrailDistance * 0.45f);
    }

    private void CopySprite(SpriteRenderer target, float alpha)
    {
        if (target == null)
            return;

        if (source != null)
        {
            target.sprite = source.sprite;
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.sortingLayerID = source.sortingLayerID;
        }

        target.color = new Color(0.55f, 0.82f, 1f, alpha);
    }

    private void Strike(int direction)
    {
        Vector3 origin = source != null ? source.transform.position : player.transform.position;
        int face = direction >= 0 ? 1 : -1;
        Vector3 tail = origin - Vector3.right * face * TrailDistance;
        Vector2 center = (origin + tail) * 0.5f;
        Vector2 size = new Vector2(TrailDistance + 0.7f, 1.55f);
        int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlaps[i];
            overlaps[i] = null;
            if (hit == null || hit.transform.IsChildOf(player.transform))
                continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !alreadyHit.Add(damageable))
                continue;

            damageable.TakeDamage(damage, center);
            AttackFeedback.Hit(hit.bounds.center, false);
        }
    }

    private IEnumerator FadeAndDestroy()
    {
        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            Tint(body, 0.62f * alpha);
            Tint(mid, 0.28f * alpha);
            yield return null;
        }

        Destroy(gameObject);
    }

    private static void Tint(SpriteRenderer sprite, float alpha)
    {
        if (sprite == null)
            return;

        Color color = sprite.color;
        color.a = alpha;
        sprite.color = color;
    }

    private static SpriteRenderer CreateBody(Transform parent, string name, int order)
    {
        GameObject bodyObject = new GameObject(name);
        bodyObject.transform.SetParent(parent, false);
        SpriteRenderer sprite = bodyObject.AddComponent<SpriteRenderer>();
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 8 + order;
        return sprite;
    }
}
