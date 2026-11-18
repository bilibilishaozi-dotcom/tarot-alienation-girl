using System.Collections.Generic;
using UnityEngine;

// 打击感入口。启用时只冻参与者的动画和移动，不改 Time.timeScale，所以暂停菜单和 UI 不受影响。
public static class HitFeel
{
    public enum Cue
    {
        Swing,
        Hit,
        Block,
        Hurt,
        Jump,
        Dash,
        BossTell,
        BossImpact
    }

    private static HitFeelSettings settings;
    private static bool searched;
    private static AudioSource source;

    public static HitFeelSettings Settings
    {
        get
        {
            if (settings == null && !searched)
            {
                searched = true;
                settings = Resources.Load<HitFeelSettings>("HitFeel/HitFeelSettings");
            }

            return settings;
        }
    }

    public static bool Enabled => Settings != null && Settings.enabled;

    public static bool IsFrozen(GameObject target)
    {
        if (!Enabled || target == null)
            return false;

        HitFeelBody body = target.GetComponent<HitFeelBody>();
        return body != null && body.Holding;
    }

    public static void ReleaseAll()
    {
        HitFeelBody[] bodies = Object.FindObjectsByType<HitFeelBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].ReleaseNow();
        }
    }

    public static void WeaponSwing()
    {
        if (!Enabled)
            return;

        Play(Cue.Swing);
    }

    public static void WeaponHit(Player player, Component victim, Vector2 point, bool blocked, bool forceHeavy = false)
    {
        bool air = player != null && player.IsAirAttack;
        int combo = player != null ? player.AttackComboIndex : 1;
        bool heavy = forceHeavy || air || combo >= 3;
        int damage = player != null ? player.CurrentAttackDamage : 1;
        bool big = heavy || damage >= 2;

        if (!Enabled)
        {
            if (blocked)
            {
                Juice.Clang();
                Juice.Shake(0.12f);
                Juice.HitStop(0.03f);
            }
            else
            {
                Juice.Number(point, damage, big);
                Juice.Thump();
                Juice.Shake(big ? 0.34f : 0.2f);
                Juice.HitStop(big ? 0.075f : 0.045f);
            }

            return;
        }

        HitFeelSettings set = Settings;
        float stop = blocked ? set.blockHitStop : (heavy ? set.heavyHitStop : set.lightHitStop);
        Hold(player != null ? player.gameObject : null, victim != null ? victim.gameObject : null, stop);
        Shake(blocked ? set.blockShake : (heavy ? set.heavyShake : set.hitShake));
        Spark(point, heavy);
        Play(blocked ? Cue.Block : Cue.Hit);
        if (!blocked)
            Juice.Number(point, damage, big);

        if (blocked || victim == null)
            return;

        bool boss = victim.GetComponent<RiftGuardian>() != null;
        float dir = victim.transform.position.x >= (player != null ? player.transform.position.x : point.x) ? 1f : -1f;
        Shove(victim.transform, dir, boss ? set.bossShove : set.enemyShove);
        Flash(victim.gameObject, set.enemyFlash, set.flashDuration);
    }

    public static void PlayerHurt(Player player)
    {
        if (player == null)
            return;

        if (!Enabled)
        {
            Juice.Thump();
            Juice.Shake(0.4f);
            Juice.HitStop(0.06f);
            return;
        }

        HitFeelSettings set = Settings;
        Hold(player.gameObject, null, set.hurtHitStop);
        Shake(set.hurtShake);
        Flash(player.gameObject, set.playerFlash, set.flashDuration);
        Play(Cue.Hurt);
    }

    public static void Jump()
    {
        if (Enabled)
            Play(Cue.Jump);
    }

    public static void Dash()
    {
        if (Enabled)
            Play(Cue.Dash);
    }

    public static void BossTell()
    {
        if (Enabled)
            Play(Cue.BossTell);
    }

    public static void GroundImpact(Vector3 point)
    {
        if (!Enabled)
            return;

        Shake(Settings.impactShake);
        Spark(point, true);
        Play(Cue.BossImpact);
    }

    public static void Shake(float amplitude)
    {
        if (!Enabled || amplitude <= 0f)
            return;

        JuiceCameraShake shake = JuiceCameraShake.Get();
        if (shake != null)
            shake.PlayWorld(amplitude, Settings.shakeDuration);
    }

    private static void Hold(GameObject attacker, GameObject victim, float seconds)
    {
        if (seconds <= 0f)
            return;

        HoldOne(attacker, seconds);
        if (victim != attacker)
            HoldOne(victim, seconds);
    }

    private static void HoldOne(GameObject target, float seconds)
    {
        if (target == null)
            return;

        HitFeelBody body = target.GetComponent<HitFeelBody>();
        if (body == null)
            body = target.AddComponent<HitFeelBody>();
        body.Hold(seconds);
    }

    private static void Flash(GameObject target, Color color, float seconds)
    {
        if (target == null || seconds <= 0f)
            return;

        HitFeelFlash flash = target.GetComponent<HitFeelFlash>();
        if (flash == null)
            flash = target.AddComponent<HitFeelFlash>();
        flash.Play(color, seconds);
    }

    private static void Shove(Transform target, float direction, float distance)
    {
        if (target == null || distance <= 0f)
            return;

        HitFeelShove shove = target.GetComponent<HitFeelShove>();
        if (shove == null)
            shove = target.gameObject.AddComponent<HitFeelShove>();
        shove.Play(direction >= 0f ? 1f : -1f, distance);
    }

    private static void Spark(Vector2 point, bool heavy)
    {
        HitFeelSettings set = Settings;
        int count = Mathf.Max(1, set.sparkCount + (heavy ? 2 : 0));
        float size = set.sparkSize * (heavy ? 1.35f : 1f);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-12f, 12f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            HitFeelSpark.Spawn(point, dir, size, set.sparkLife, heavy);
        }
    }

    private static void Play(Cue cue)
    {
        HitFeelSettings set = Settings;
        AudioClip clip = Clip(set, cue);
        if (clip == null)
            return;

        if (source == null)
        {
            GameObject obj = new GameObject("HitFeelSfx");
            Object.DontDestroyOnLoad(obj);
            source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.pitch = Random.Range(0.96f, 1.04f);
        source.PlayOneShot(clip, set.sfxVolume);
    }

    private static AudioClip Clip(HitFeelSettings set, Cue cue)
    {
        switch (cue)
        {
            case Cue.Swing: return set.swing;
            case Cue.Hit: return set.hit;
            case Cue.Block: return set.block;
            case Cue.Hurt: return set.hurt;
            case Cue.Jump: return set.jump;
            case Cue.Dash: return set.dash;
            case Cue.BossTell: return set.bossTell;
            default: return set.bossImpact;
        }
    }
}

// 冻住一个角色的移动和 Animator。时间到了或暂停时一定把速度和重力还回去。
[DefaultExecutionOrder(1000)]
public class HitFeelBody : MonoBehaviour
{
    private Rigidbody2D body;
    private Animator animator;
    private float savedGravity;
    private Vector2 savedVelocity;
    private float savedAnimSpeed = 1f;
    private bool savedAnim;
    private float releaseAt;
    private bool holding;

    public bool Holding => holding;

    public void Hold(float seconds)
    {
        if (!holding)
            Capture();
        else if (body != null)
            savedVelocity = body.linearVelocity;

        releaseAt = Mathf.Max(releaseAt, Time.unscaledTime + seconds);
        holding = true;
        ApplyHold();
    }

    public void ReleaseNow()
    {
        if (holding)
            Release();
    }

    private void Capture()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (body != null)
        {
            savedVelocity = body.linearVelocity;
            savedGravity = body.gravityScale;
        }

        if (animator != null)
        {
            savedAnimSpeed = animator.speed;
            savedAnim = true;
        }
    }

    private void ApplyHold()
    {
        if (body != null)
        {
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
        }

        if (animator != null)
            animator.speed = 0f;
    }

    private void FixedUpdate()
    {
        if (holding && body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!holding)
            return;

        ApplyHold();
        if (Time.unscaledTime >= releaseAt)
            Release();
    }

    private void OnDisable()
    {
        if (holding)
            Release();
    }

    private void Release()
    {
        holding = false;
        if (body != null)
        {
            body.gravityScale = savedGravity;
            body.linearVelocity = savedVelocity;
        }

        if (savedAnim && animator != null)
            animator.speed = savedAnimSpeed;
    }
}

// 短闪。LateUpdate 里盖住本帧的颜色，结束时还原开始时记下的颜色。
[DefaultExecutionOrder(20000)]
public class HitFeelFlash : MonoBehaviour
{
    private struct Slot
    {
        public SpriteRenderer Renderer;
        public Color Color;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private Color flash;
    private float releaseAt;
    private bool playing;

    public void Play(Color color, float seconds)
    {
        if (!playing)
            Capture();

        flash = color;
        releaseAt = Mathf.Max(releaseAt, Time.unscaledTime + seconds);
        playing = true;
        Paint();
    }

    private void Capture()
    {
        slots.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            slots.Add(new Slot { Renderer = renderers[i], Color = renderers[i].color });
        }
    }

    private void Paint()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Renderer == null)
                continue;

            Color color = flash;
            color.a = slots[i].Color.a;
            slots[i].Renderer.color = color;
        }
    }

    private void LateUpdate()
    {
        if (!playing)
            return;

        if (Time.unscaledTime >= releaseAt)
        {
            Restore();
            return;
        }

        Paint();
    }

    private void OnDisable()
    {
        if (playing)
            Restore();
    }

    private void Restore()
    {
        playing = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Renderer != null)
                slots[i].Renderer.color = slots[i].Color;
        }

        slots.Clear();
    }
}

// 受击时往旁边挪一点，再在原处拉回。不写刚体速度。
public class HitFeelShove : MonoBehaviour
{
    private Vector3 applied;
    private float strength;
    private float sign = 1f;
    private float age;
    private float life = 0.08f;
    private bool playing;

    public void Play(float direction, float distance)
    {
        sign = direction >= 0f ? 1f : -1f;
        strength = distance;
        age = 0f;
        life = 0.08f;
        playing = true;
    }

    private void LateUpdate()
    {
        if (!playing)
            return;

        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        float offset = strength * (1f - t);
        Vector3 desired = Vector3.right * sign * offset;
        transform.position += desired - applied;
        applied = desired;
        if (t >= 1f)
        {
            transform.position -= applied;
            applied = Vector3.zero;
            playing = false;
        }
    }

    private void OnDisable()
    {
        if (playing)
        {
            transform.position -= applied;
            applied = Vector3.zero;
            playing = false;
        }
    }
}

public class HitFeelSpark : MonoBehaviour
{
    private static Sprite pixel;
    private Vector2 velocity;
    private float life;
    private float age;
    private SpriteRenderer sprite;
    private Color color;

    public static void Spawn(Vector2 point, Vector2 direction, float size, float duration, bool heavy)
    {
        GameObject obj = new GameObject("HitSpark");
        obj.transform.position = point;
        obj.transform.localScale = Vector3.one * size;
        obj.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
        HitFeelSpark spark = obj.AddComponent<HitFeelSpark>();
        spark.velocity = direction * Random.Range(1.4f, 2.6f);
        spark.life = Mathf.Max(0.04f, duration);
        spark.sprite = obj.AddComponent<SpriteRenderer>();
        spark.sprite.sprite = Pixel();
        spark.sprite.sortingLayerName = "Player";
        spark.sprite.sortingOrder = 24;
        spark.color = heavy ? new Color(1f, 0.78f, 0.28f, 1f) : new Color(1f, 0.96f, 0.9f, 1f);
        spark.sprite.color = spark.color;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
        velocity *= 0.92f;
        if (sprite != null)
        {
            Color faded = color;
            faded.a = 1f - t;
            sprite.color = faded;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    private static Sprite Pixel()
    {
        if (pixel != null)
            return pixel;

        Texture2D texture = new Texture2D(4, 4);
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        pixel.hideFlags = HideFlags.HideAndDontSave;
        return pixel;
    }
}
