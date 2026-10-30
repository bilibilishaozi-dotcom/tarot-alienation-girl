using UnityEngine;

// 冰蓝星辉魔法少女：整张立绘 + 程序化整体表演（呼吸 / 行走起伏 / 起跳下落 / 攻击前压 / 冲刺 / 受击 / 死亡）。
// 不拆部件——立绘拆分反复出现接缝和错位，先保证「看起来是对的」。
public class WitchVisual : MonoBehaviour
{
    private const float PPU = 100f;

    private Player player;
    private SpriteRenderer renderer;
    private Transform spriteTransform;
    private float feetLocalY;
    private float baseScale = 1f;
    private float facing = 1f;
    private float halfHeight;      // 半个 sprite 高度（世界单位）
    private float spriteBaseY;     // 脚底贴地时 sprite 的本地 y

    private float walk;
    private float rise;
    private float fall;
    private float attack;
    private float dash;
    private float hurt;
    private float deathT;

    public static WitchVisual Attach(Player owner, float feetY, float height)
    {
        GameObject obj = new GameObject("WitchVisual");
        obj.SetActive(false);
        obj.transform.SetParent(owner.transform, false);
        WitchVisual visual = obj.AddComponent<WitchVisual>();
        visual.player = owner;
        visual.feetLocalY = feetY;
        visual.Build(height);
        obj.SetActive(true);
        return visual;
    }

    private void Build(float height)
    {
        GameObject obj = new GameObject("Sprite");
        obj.transform.SetParent(transform, false);
        spriteTransform = obj.transform;
        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>("Art/Witch/full");
        renderer.sortingLayerName = "Player";
        renderer.sortingOrder = 10;

        if (renderer.sprite == null)
        {
            Debug.LogWarning("女巫整张立绘没加载到：Resources/Art/Witch/full");
            return;
        }

        float artHeight = renderer.sprite.rect.height / PPU;
        baseScale = height / Mathf.Max(0.1f, artHeight);
        ApplyScale();
        halfHeight = artHeight * baseScale * 0.5f;

        // 先粗放，再用真实渲染包围盒把脚底压到地面上（不依赖枢轴/缩放的具体数值）
        spriteTransform.localPosition = new Vector3(0f, feetLocalY + halfHeight, 0f);
        Physics2D.SyncTransforms();

        float desiredMin = player.transform.position.y + feetLocalY;
        float delta = desiredMin - renderer.bounds.min.y;
        spriteTransform.position += new Vector3(0f, delta, 0f);
        spriteBaseY = spriteTransform.localPosition.y;
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(baseScale * facing, baseScale, 1f);
    }

    private void LateUpdate()
    {
        if (player == null || renderer == null || spriteTransform == null)
            return;

        float dt = Time.deltaTime;
        float t = Time.time;

        float wanted = player.facingDir < 0 ? -1f : 1f;
        if (!Mathf.Approximately(facing, wanted))
        {
            facing = Mathf.MoveTowards(facing, wanted, dt * 18f);
            ApplyScale();
        }

        bool moving = player.IsMoving && player.groundDetected && !player.IsDefeated;
        bool airborne = !player.groundDetected && !player.IsDefeated;
        bool rising = airborne && player.VerticalSpeed > 0.15f;

        walk = Mathf.MoveTowards(walk, moving ? 1f : 0f, dt * 8f);
        rise = Mathf.MoveTowards(rise, rising ? 1f : 0f, dt * 11f);
        fall = Mathf.MoveTowards(fall, airborne && !rising ? 1f : 0f, dt * 9f);
        attack = Mathf.MoveTowards(attack, player.IsAttacking ? 1f : 0f, dt * (player.IsAttacking ? 18f : 8f));
        dash = Mathf.MoveTowards(dash, player.IsDodging ? 1f : 0f, dt * 12f);
        hurt = Mathf.MoveTowards(hurt, player.IsHurtNow ? 1f : 0f, dt * 12f);

        if (player.IsDefeated)
            deathT = Mathf.Min(1f, deathT + dt * 1.6f);

        // 整体上下：走路两倍频小跳，待机慢呼吸
        float step = Mathf.Sin(t * 9f);
        float bob = Mathf.Abs(step) * 0.045f * walk + Mathf.Sin(t * 1.7f) * 0.012f * (1f - walk);

        // 挤压：走路时轻微上下压扁，起跳拉长、落地压扁
        float squashY = 1f - 0.03f * Mathf.Abs(step) * walk - 0.05f * fall + 0.05f * rise;
        float squashX = 1f + 0.02f * Mathf.Abs(step) * walk + 0.03f * fall - 0.03f * rise;

        spriteTransform.localPosition = new Vector3(
            0f,
            spriteBaseY + (1f - squashY) * halfHeight + bob,
            0f);
        spriteTransform.localScale = new Vector3(squashX, squashY, 1f);

        // 倾斜：走路前倾、冲刺更前倾、攻击前压、起跳前倾、下落略后仰、受伤后仰、死亡倒地
        float angle =
            -3f * walk
            - 12f * dash
            + 7f * attack
            - 6f * rise
            + 4f * fall
            - 9f * hurt
            + 78f * deathT
            + Mathf.Sin(t * 1.7f) * 0.8f * (1f - walk);
        spriteTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.85f, 0.9f, 1f - deathT);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f, 1f), hurt);

        renderer.color = tint;
    }
}
