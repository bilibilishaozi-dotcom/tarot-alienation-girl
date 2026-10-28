using System.Collections.Generic;
using UnityEngine;

public static class StrengthSlam
{
    private const float Reach = 2.55f;
    private const float Height = 0.85f;

    public static void Fire(Player player, int damage)
    {
        if (player == null)
            return;

        int facing = player.facingDir >= 0 ? 1 : -1;
        Bounds body = player.GetComponent<Collider2D>() != null
            ? player.GetComponent<Collider2D>().bounds
            : new Bounds(player.transform.position, new Vector3(0.8f, 1.6f, 0.2f));
        float near = facing > 0 ? body.max.x : body.min.x;
        float far = near + facing * Reach;
        float left = Mathf.Min(near, far);
        float right = Mathf.Max(near, far);
        float ground = body.min.y;
        Vector2 center = new Vector2((left + right) * 0.5f, ground + Height * 0.5f);
        Vector2 size = new Vector2(Mathf.Max(0.4f, right - left), Height);

        StrengthClawVisual claw = player.GetComponentInChildren<StrengthClawVisual>();
        if (claw != null)
            claw.Pulse();

        SpawnWave(center, size, facing);
        AttackFeedback.Thud(center);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        HashSet<IDamageable> already = new HashSet<IDamageable>();
        int amount = Mathf.Max(1, damage);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(player.transform))
                continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !already.Add(damageable))
                continue;

            damageable.TakeDamage(amount, center);
            AttackFeedback.Hit(hit.bounds.center, true);
        }
    }

    private static void SpawnWave(Vector2 center, Vector2 size, int facing)
    {
        GameObject wave = new GameObject("StrengthWave");
        wave.transform.position = new Vector3(center.x, center.y, 0f);
        wave.transform.localScale = new Vector3(size.x * facing, size.y, 1f);
        SpriteRenderer sprite = wave.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = new Color(1f, 0.42f, 0.18f, 0.8f);
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 12;
        wave.AddComponent<StrengthWaveFade>();
    }

}

public class StrengthWaveFade : MonoBehaviour
{
    private SpriteRenderer sprite;
    private float age;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / 0.22f);
        if (sprite != null)
        {
            Color color = sprite.color;
            color.a = 0.8f * (1f - t);
            sprite.color = color;
        }

        transform.localScale += new Vector3(Time.deltaTime * 1.4f, 0f, 0f);
        if (t >= 1f)
            Destroy(gameObject);
    }
}

public class BreakableProp : MonoBehaviour, IDamageable
{
    private int health = 1;
    private bool broken;

    public void Arm(int amount)
    {
        health = Mathf.Max(1, amount);
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (broken || amount <= 0)
            return;

        health -= amount;
        if (health > 0)
            return;

        broken = true;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;
        AttackFeedback.Thud(transform.position);
        StartCoroutine(BreakApart());
    }

    private System.Collections.IEnumerator BreakApart()
    {
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        Vector3 scale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.25f);
            transform.localScale = Vector3.Lerp(scale, scale * 0.2f, t);
            if (sprite != null)
            {
                Color color = sprite.color;
                color.a = 1f - t;
                sprite.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
