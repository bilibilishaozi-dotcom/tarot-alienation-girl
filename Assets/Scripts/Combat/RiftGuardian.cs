using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一关栏杆后的裂隙守卫。固定站位，只用挥击、突进、落点三种预兆攻击。
/// 不依赖动画事件：预兆时间结束就进入打击，打击时间结束就进入反击窗口。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class RiftGuardian : MonoBehaviour, IDamageable, IHudTarget
{
    private enum Step
    {
        Dormant,
        Wake,
        Idle,
        Windup,
        Strike,
        Recover
    }

    private enum Move
    {
        Slash,
        Thrust,
        Drop
    }

    private struct Zone
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;

        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        public Vector2 Size => new Vector2(Mathf.Max(0.05f, MaxX - MinX), Mathf.Max(0.05f, MaxY - MinY));
    }

    private const int AttackDamage = 1;
    private const float WakeTime = 0.72f;
    private const float RecoverTime = 0.8f;
    private const float IdleGap = 0.25f;
    private static readonly Move[] Pattern = { Move.Slash, Move.Thrust, Move.Drop };

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer aura;
    private Collider2D bodyCollider;
    private Player player;
    private Transform telegraphRoot;
    private SpriteRenderer zoneFill;
    private SpriteRenderer zoneLip;
    private SpriteRenderer zoneEdge;
    private SpriteRenderer zoneEdgeFar;
    private GameObject labelRoot;
    private Text moveLabel;
    private Vector3 baseScale = Vector3.one;
    private Color bodyColor = new Color(0.9f, 0.24f, 0.18f, 1f);
    private Step step = Step.Dormant;
    private Move currentMove = Move.Slash;
    private Zone lockedZone;
    private float timer;
    private float hitFlash;
    private float slashMinX;
    private float grateX;
    private float thrustMinX;
    private float groundY;
    private float meleeTop = 2.2f;
    private float dropHeight = 0.8f;
    private float dropWidth = 1.5f;
    private int health = 10;
    private int maxHealth = 10;
    private int patternIndex;
    private bool bound;
    private bool foolReady;
    private bool awake;
    private bool hasAttacked;
    private bool strikeConnected;
    private bool dead;
    private bool deathNotified;
    private bool heavyFlash;
    private bool warned;

    public int CurrentHealth => health;
    public int MaxHealth => maxHealth;
    public string DisplayName { get; private set; } = "裂隙守卫";
    public bool IsDead => dead;
    public bool IsAwake => awake;
    public bool HasAttacked => hasAttacked;
    public bool IsTelegraphing => step == Step.Windup || step == Step.Strike;
    public Vector3 HudWorldPoint => transform.position + Vector3.up * 1.35f;
    public event Action HealthChanged;
    public event Action Died;
    public static event Action<RiftGuardian> Spawned;
    public static event Action<RiftGuardian> Removed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = PixelSprite();
        spriteRenderer.color = bodyColor;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;

        BuildTelegraph();
        BuildLabel();
    }

    private void OnEnable()
    {
        Spawned?.Invoke(this);
    }

    private void OnDisable()
    {
        Removed?.Invoke(this);
    }

    private void OnDestroy()
    {
        DestroyTelegraph();
    }

    public void Bind(Player target, int healthValue, string name, float slashMin, float grate, float thrustMin, float ground, float headTop, float hazardHeight, float hazardWidth)
    {
        player = target;
        maxHealth = Mathf.Max(1, healthValue);
        health = maxHealth;
        if (!string.IsNullOrEmpty(name))
            DisplayName = name;

        slashMinX = Mathf.Min(slashMin, grate);
        grateX = Mathf.Max(slashMin, grate);
        thrustMinX = Mathf.Min(thrustMin, slashMinX);
        groundY = ground;
        meleeTop = Mathf.Max(groundY + 0.4f, headTop);
        dropHeight = Mathf.Clamp(hazardHeight, 0.45f, meleeTop - groundY);
        dropWidth = Mathf.Clamp(hazardWidth, 0.8f, Mathf.Max(0.8f, grateX - thrustMinX));
        bound = player != null;
        Transform auraTransform = transform.Find("Aura");
        if (auraTransform != null)
            aura = auraTransform.GetComponent<SpriteRenderer>();

        if (!bound)
            Warn("裂隙守卫没有玩家目标，不会发动攻击。");
        else if (grateX - slashMinX < 0.12f || slashMinX - thrustMinX < 0.2f)
            Warn("裂隙守卫的攻击范围过窄。挥击和突进都应由玩家的普通攻击距离算出来。");

        HealthChanged?.Invoke();
    }

    public void SetFoolReady(bool ready)
    {
        foolReady = ready;
    }

    private void Update()
    {
        if (dead)
            return;

        if (hitFlash > 0f)
            hitFlash -= Time.deltaTime;

        if (!bound || player == null)
        {
            ApplyPose();
            return;
        }

        if (player.IsDefeated)
        {
            HideZone();
            ApplyPose();
            return;
        }

        timer -= Time.deltaTime;
        switch (step)
        {
            case Step.Dormant:
                if (foolReady && PlayerInArena())
                    BeginWake();
                break;
            case Step.Wake:
                if (timer <= 0f)
                    BeginIdle(0.42f);
                break;
            case Step.Idle:
                if (timer > 0f)
                    break;
                if (PlayerInArena())
                    BeginWindup(NextMove());
                else
                    timer = 0.2f;
                break;
            case Step.Windup:
                if (timer <= 0f)
                    BeginStrike();
                break;
            case Step.Strike:
                if (!strikeConnected && HitsPlayer(lockedZone))
                {
                    strikeConnected = true;
                    player.TakeDamage(AttackDamage, transform.position);
                }

                if (timer <= 0f)
                    BeginRecover();
                break;
            case Step.Recover:
                if (timer <= 0f)
                    BeginIdle(IdleGap);
                break;
        }

        ApplyPose();
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (dead || amount <= 0)
            return;

        health = Mathf.Max(0, health - amount);
        heavyFlash = amount >= 2;
        hitFlash = heavyFlash ? 0.16f : 0.1f;
        HealthChanged?.Invoke();
        if (health > 0)
            return;

        Die();
    }

    private void BeginWake()
    {
        awake = true;
        step = Step.Wake;
        timer = WakeTime;
        AttackFeedback.Thud(transform.position);
    }

    private void BeginIdle(float duration)
    {
        step = Step.Idle;
        timer = duration;
        HideZone();
    }

    private void BeginWindup(Move move)
    {
        if (dead)
            return;

        currentMove = move;
        lockedZone = ZoneFor(move);
        step = Step.Windup;
        timer = WindupTime(move);
        strikeConnected = false;
    }

    private void BeginStrike()
    {
        if (dead || step != Step.Windup)
            return;

        step = Step.Strike;
        timer = StrikeTime(currentMove);
        hasAttacked = true;
        Vector3 center = lockedZone.Center;
        Bounds bounds = new Bounds(center, lockedZone.Size);
        AttackFeedback.Swing(bounds, -1, currentMove == Move.Thrust);
    }

    private void BeginRecover()
    {
        step = Step.Recover;
        timer = RecoverTime;
        HideZone();
    }

    private void Die()
    {
        if (dead)
            return;

        dead = true;
        step = Step.Dormant;
        HideZone();
        if (labelRoot != null)
            labelRoot.SetActive(false);
        if (bodyCollider != null)
            bodyCollider.enabled = false;

        AttackFeedback.Thud(transform.position);
        AttackFeedback.Hit((Vector2)transform.position, true);
        HealthChanged?.Invoke();
        if (!deathNotified)
        {
            deathNotified = true;
            Died?.Invoke();
        }

        StartCoroutine(PlayDeath());
    }

    private IEnumerator PlayDeath()
    {
        transform.localScale = baseScale;
        if (spriteRenderer != null)
            spriteRenderer.color = bodyColor;
        if (aura != null)
        {
            Color auraColor = bodyColor;
            auraColor.a = 0.33f;
            aura.color = auraColor;
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                startColors[i] = sprites[i].color;
        }

        float elapsed = 0f;
        const float duration = 0.7f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * 0.12f, t);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                Color color = startColors[i];
                color.a = startColors[i].a * (1f - t);
                sprites[i].color = color;
            }

            yield return null;
        }

        DestroyTelegraph();
        Destroy(gameObject);
    }

    private Move NextMove()
    {
        for (int i = 0; i < Pattern.Length; i++)
        {
            Move pick = Pattern[patternIndex % Pattern.Length];
            patternIndex++;
            if (pick == Move.Slash && !PlayerInSlash())
                continue;
            return pick;
        }

        return Move.Drop;
    }

    private Zone ZoneFor(Move move)
    {
        if (move == Move.Drop)
        {
            float half = dropWidth * 0.5f;
            float minCenter = thrustMinX + half;
            float maxCenter = grateX - half;
            float center = player != null ? player.transform.position.x : (thrustMinX + grateX) * 0.5f;
            if (maxCenter >= minCenter)
                center = Mathf.Clamp(center, minCenter, maxCenter);
            else
                center = (thrustMinX + grateX) * 0.5f;

            return new Zone
            {
                MinX = center - half,
                MaxX = center + half,
                MinY = groundY,
                MaxY = groundY + dropHeight
            };
        }

        float minX = move == Move.Thrust ? thrustMinX : slashMinX;
        return new Zone
        {
            MinX = minX,
            MaxX = grateX,
            MinY = groundY,
            MaxY = meleeTop
        };
    }

    private bool HitsPlayer(Zone zone)
    {
        if (player == null)
            return false;

        Collider2D body = player.GetComponent<Collider2D>();
        Bounds bounds;
        if (body != null)
            bounds = body.bounds;
        else
            bounds = new Bounds(player.transform.position, new Vector3(0.7f, 1.6f, 0.2f));

        return bounds.max.x >= zone.MinX && bounds.min.x <= zone.MaxX
            && bounds.max.y >= zone.MinY && bounds.min.y <= zone.MaxY;
    }

    private bool PlayerInArena()
    {
        float x = player.transform.position.x;
        return x >= thrustMinX && x <= grateX + 0.8f;
    }

    private bool PlayerInSlash()
    {
        return player != null && player.transform.position.x >= slashMinX;
    }

    private void ApplyPose()
    {
        if (spriteRenderer == null)
            return;

        bool showZone = step == Step.Windup || step == Step.Strike;
        if (hitFlash > 0f)
        {
            spriteRenderer.color = heavyFlash ? new Color(1f, 0.86f, 0.35f, 1f) : Color.white;
            transform.localScale = new Vector3(baseScale.x * (heavyFlash ? 1.22f : 1.1f), baseScale.y * (heavyFlash ? 0.5f : 0.68f), baseScale.z);
        }
        else
        {
            spriteRenderer.color = StepColor();
            transform.localScale = StepScale();
        }

        if (aura != null)
        {
            Color auraColor = spriteRenderer.color;
            auraColor.a = 0.33f;
            aura.color = auraColor;
        }

        if (showZone)
            ShowZone(step == Step.Strike);
        else
            HideZone();

        if (moveLabel != null)
        {
            moveLabel.text = LabelFor(step, currentMove);
            moveLabel.color = LabelColor();
        }

        if (labelRoot != null)
            labelRoot.transform.position = transform.position + new Vector3(0f, 2.35f, 0f);
    }

    private Color StepColor()
    {
        switch (step)
        {
            case Step.Wake:
                float pulse = 0.5f + Mathf.PingPong(Time.time * 3f, 0.5f);
                return Color.Lerp(bodyColor, Color.white, pulse);
            case Step.Windup:
            case Step.Strike:
                return MoveColor(currentMove);
            case Step.Recover:
                return new Color(0.42f, 0.62f, 0.95f, 1f);
            default:
                return bodyColor;
        }
    }

    private Vector3 StepScale()
    {
        if (step == Step.Windup)
            return new Vector3(baseScale.x * 1.08f, baseScale.y * 1.08f, baseScale.z);
        if (step == Step.Strike)
            return new Vector3(baseScale.x * 1.18f, baseScale.y * 0.9f, baseScale.z);
        if (step == Step.Recover)
            return new Vector3(baseScale.x * 0.94f, baseScale.y * 0.94f, baseScale.z);
        return baseScale;
    }

    private static Color MoveColor(Move move)
    {
        switch (move)
        {
            case Move.Thrust:
                return new Color(1f, 0.5f, 0.08f, 1f);
            case Move.Drop:
                return new Color(0.58f, 0.28f, 0.95f, 1f);
            default:
                return new Color(0.95f, 0.16f, 0.12f, 1f);
        }
    }

    private static string LabelFor(Step current, Move move)
    {
        switch (current)
        {
            case Step.Wake:
                return "醒来";
            case Step.Windup:
            case Step.Strike:
                if (move == Move.Thrust)
                    return "突进";
                if (move == Move.Drop)
                    return "落点";
                return "挥击";
            case Step.Recover:
                return "反击";
            default:
                return string.Empty;
        }
    }

    private Color LabelColor()
    {
        if (step == Step.Recover)
            return new Color(0.7f, 0.88f, 1f, 1f);
        if (step == Step.Wake)
            return new Color(1f, 0.9f, 0.7f, 1f);
        if (step == Step.Windup || step == Step.Strike)
            return MoveColor(currentMove);
        return new Color(1f, 0.94f, 0.75f, 0f);
    }

    private void ShowZone(bool striking)
    {
        if (zoneFill == null)
            return;

        Color color = MoveColor(currentMove);
        float alpha = striking ? 0.9f : 0.38f + Mathf.PingPong(Time.time * 2.4f, 0.22f);
        color.a = alpha;
        PlaceSprite(zoneFill, lockedZone.Center, lockedZone.Size, color);

        float lipHeight = Mathf.Min(0.16f, lockedZone.Size.y);
        Vector3 lipCenter = new Vector3(lockedZone.Center.x, lockedZone.MinY + lipHeight * 0.5f, 0f);
        Color lip = color;
        lip.a = striking ? 1f : 0.85f;
        PlaceSprite(zoneLip, lipCenter, new Vector2(lockedZone.Size.x, lipHeight), lip);

        Color edge = Color.white;
        edge.a = striking ? 1f : 0.9f;
        PlaceSprite(zoneEdge, new Vector3(lockedZone.MinX + 0.04f, lockedZone.Center.y, 0f), new Vector2(0.08f, lockedZone.Size.y), edge);
        bool showFar = currentMove == Move.Drop;
        zoneEdgeFar.enabled = showFar;
        if (showFar)
            PlaceSprite(zoneEdgeFar, new Vector3(lockedZone.MaxX - 0.04f, lockedZone.Center.y, 0f), new Vector2(0.08f, lockedZone.Size.y), edge);
    }

    private void HideZone()
    {
        if (zoneFill != null)
            zoneFill.enabled = false;
        if (zoneLip != null)
            zoneLip.enabled = false;
        if (zoneEdge != null)
            zoneEdge.enabled = false;
        if (zoneEdgeFar != null)
            zoneEdgeFar.enabled = false;
    }

    private static void PlaceSprite(SpriteRenderer sprite, Vector3 center, Vector2 size, Color color)
    {
        sprite.enabled = true;
        sprite.transform.position = center;
        sprite.transform.localScale = new Vector3(size.x, size.y, 1f);
        sprite.color = color;
    }

    private static float WindupTime(Move move)
    {
        switch (move)
        {
            case Move.Thrust:
                return 0.9f;
            case Move.Drop:
                return 1.02f;
            default:
                return 0.62f;
        }
    }

    private static float StrikeTime(Move move)
    {
        return move == Move.Drop ? 0.16f : 0.12f;
    }

    private void BuildTelegraph()
    {
        GameObject rootObject = new GameObject("RiftTelegraph");
        telegraphRoot = rootObject.transform;
        zoneFill = MakeSprite("Fill", telegraphRoot, "Ground", 6);
        zoneLip = MakeSprite("Lip", telegraphRoot, "Ground", 7);
        zoneEdge = MakeSprite("Edge", telegraphRoot, "Ground", 8);
        zoneEdgeFar = MakeSprite("EdgeFar", telegraphRoot, "Ground", 8);
        HideZone();
    }

    private static SpriteRenderer MakeSprite(string name, Transform parent, string sortingLayer, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        SpriteRenderer sprite = obj.AddComponent<SpriteRenderer>();
        sprite.sprite = PixelSprite();
        sprite.sortingLayerName = sortingLayer;
        sprite.sortingOrder = sortingOrder;
        sprite.enabled = false;
        return sprite;
    }

    private void BuildLabel()
    {
        labelRoot = new GameObject("MoveLabel");
        labelRoot.transform.SetParent(telegraphRoot, false);
        labelRoot.transform.localScale = Vector3.one * 0.012f;
        Canvas canvas = labelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Player";
        canvas.sortingOrder = 14;
        RectTransform rect = labelRoot.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 70f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(labelRoot.transform, false);
        moveLabel = textObject.AddComponent<Text>();
        moveLabel.font = GameFont.Get();
        moveLabel.fontSize = 36;
        moveLabel.alignment = TextAnchor.MiddleCenter;
        moveLabel.color = new Color(1f, 0.94f, 0.75f, 0f);
        moveLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        moveLabel.verticalOverflow = VerticalWrapMode.Overflow;
        moveLabel.raycastTarget = false;
        RectTransform textRect = moveLabel.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void DestroyTelegraph()
    {
        if (telegraphRoot == null)
            return;

        Destroy(telegraphRoot.gameObject);
        telegraphRoot = null;
        zoneFill = null;
        zoneLip = null;
        zoneEdge = null;
        zoneEdgeFar = null;
        labelRoot = null;
        moveLabel = null;
    }

    private void Warn(string message)
    {
        if (warned)
            return;

        warned = true;
        Debug.LogWarning(message);
    }

    private static Sprite pixel;

    private static Sprite PixelSprite()
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
