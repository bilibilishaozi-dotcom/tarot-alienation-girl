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
    private float dustTimer;
    private float ghostTimer;
    private static Sprite whiteSprite;

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

        // 整体上下：走路轻微小跳，待机慢呼吸（不做挤压，避免整张图被拉伸得像纸板）
        float step = Mathf.Sin(t * 9f);
        float bob = Mathf.Abs(step) * 0.03f * walk + Mathf.Sin(t * 1.7f) * 0.01f * (1f - walk);

        spriteTransform.localPosition = new Vector3(0f, spriteBaseY + bob, 0f);
        spriteTransform.localScale = Vector3.one;

        // 倾斜只留给「明显该倒」的动作，待机和走路一律不倾——静止姿势再旋转会显得像纸片被拖动
        float angle =
            -8f * dash
            + 5f * attack
            - 5f * rise
            + 3f * fall
            - 8f * hurt
            + 78f * deathT;
        spriteTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 走路扬尘 + 冲刺残影（都是程序生成的，不需要美术资源）
        if (walk > 0.5f && player.groundDetected)
        {
            dustTimer -= dt;
            if (dustTimer <= 0f)
            {
                dustTimer = 0.22f;
                SpawnDust();
            }
        }

        if (dash > 0.25f)
        {
            ghostTimer -= dt;
            if (ghostTimer <= 0f)
            {
                ghostTimer = 0.05f;
                SpawnGhost();
            }
        }

        Color tint = Color.white;
        if (deathT > 0f)
            tint = new Color(1f, 0.85f, 0.9f, 1f - deathT);
        else if (hurt > 0.01f)
            tint = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f, 1f), hurt);

        renderer.color = tint;
    }

    // 走路脚底的小尘土（留在世界里，不跟着角色走）
    private void SpawnDust()
    {
        GameObject obj = new GameObject("Dust");
        obj.transform.SetParent(null, false);

        float side = Random.Range(-0.18f, 0.18f) - 0.28f * facing;
        Vector3 world = transform.position + new Vector3(side, feetLocalY + 0.06f, 0f);
        obj.transform.position = world;
        obj.transform.localScale = new Vector3(0.26f, 0.18f, 1f);

        SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
        sprite.sprite = WhiteSprite();
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 7;
        sprite.color = new Color(0.82f, 0.86f, 0.95f, 0.5f);

        FadeVfx fade = obj.AddComponent<FadeVfx>();
        fade.Init(0.35f, new Vector3(0.6f, 0.36f, 1f), 0f);
    }

    // 冲刺残影：复制当前姿势的半透明剪影
    private void SpawnGhost()
    {
        if (renderer == null || renderer.sprite == null)
            return;

        GameObject obj = new GameObject("Ghost");
        obj.transform.SetParent(null, false);
        obj.transform.position = spriteTransform.position;
        obj.transform.rotation = spriteTransform.rotation;
        obj.transform.localScale = new Vector3(
            spriteTransform.lossyScale.x * Mathf.Sign(transform.lossyScale.x),
            spriteTransform.lossyScale.y,
            1f);

        SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
        sprite.sprite = renderer.sprite;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 9;
        sprite.color = new Color(0.62f, 0.86f, 1f, 0.38f);

        FadeVfx fade = obj.AddComponent<FadeVfx>();
        fade.Init(0.28f, obj.transform.localScale, -0.1f);
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(4, 4);
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }
}

// 简易淡出特效：按时间淡出 + 轻微缩放
public class FadeVfx : MonoBehaviour
{
    private SpriteRenderer renderer;
    private float life;
    private float duration;
    private Color startColor;
    private Vector3 startScale;
    private Vector3 endScale;

    public void Init(float seconds, Vector3 targetScale, float scaleDelta)
    {
        renderer = GetComponent<SpriteRenderer>();
        duration = Mathf.Max(0.02f, seconds);
        life = duration;
        startColor = renderer != null ? renderer.color : Color.white;
        startScale = transform.localScale;
        endScale = targetScale + new Vector3(scaleDelta, scaleDelta, 0f);
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float k = 1f - Mathf.Clamp01(life / duration);
        if (renderer != null)
        {
            Color color = startColor;
            color.a = startColor.a * (1f - k);
            renderer.color = color;
        }

        transform.localScale = Vector3.Lerp(startScale, endScale, k);
    }
}
