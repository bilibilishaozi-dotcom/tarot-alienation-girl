using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BasicEnemy : MonoBehaviour, IDamageable, IHudTarget
{
    private enum EnemyAction
    {
        Patrol,
        Chase,
        Windup,
        Strike,
        Recover,
        Hurt
    }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private string displayName = "敌人";
    [SerializeField] private MonsterCardData dropCard;
    [SerializeField] private bool grantFoolCardIfEmpty = true;
    [SerializeField] private float deathDelay = 0.45f;
    [SerializeField] private Vector2 knockback = new Vector2(1.4f, 0.8f);

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.6f;
    [SerializeField] private float chaseSpeed = 2.6f;
    [SerializeField] private float patrolDistance = 2.5f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Combat")]
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float noticeRange = 7f;
    [SerializeField] private float attackRange = 1.45f;
    [SerializeField] private float windupDuration = 0.55f;
    [SerializeField] private float windupCancelPadding = 0.35f;
    [SerializeField] private float strikeDuration = 0.14f;
    [SerializeField] private float recoverDuration = 0.7f;
    [SerializeField] private float hurtDuration = 0.28f;
    [SerializeField] private Vector2 attackHitboxSize = new Vector2(0.95f, 0.8f);
    [SerializeField] private float attackHitboxOffset = 0.85f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private EnemyAttackHitbox attackHitbox;
    private Transform attackPoint;
    private Player player;
    private Color baseColor;
    private Vector3 baseScale;
    private Vector2 origin;
    private EnemyAction action = EnemyAction.Patrol;
    private float actionTimer;
    private int health;
    private int moveDirection = 1;
    private bool dead;
    private bool rewardGranted;
    private bool rooted;
    private bool platformLock;
    private float platformMinX;
    private float platformMaxX;
    private Vector3 platformHome;
    private LayerMask platformGround;

    public int AttackDamage => attackDamage;
    public int CurrentHealth => health;
    public int MaxHealth => maxHealth;
    public string DisplayName => displayName;
    public bool IsDead => dead;
    public Vector3 HudWorldPoint => transform.position + Vector3.up * 1.15f;
    public bool IsStrikeActive => !dead && action == EnemyAction.Strike;
    public event Action HealthChanged;
    public event Action Died;
    public static event Action<BasicEnemy> Spawned;
    public static event Action<BasicEnemy> Removed;

    // 敌人种类：靠这几个标记区分，共用同一套 巡逻/追击/蓄力/出手 状态机。
    private bool spore;      // 孢子怪：不追人，靠近后原地蓄力，然后大范围喷发
    private bool armored;    // 石甲虫：横向普通攻击会被甲壳弹开，只有从上方砸下来才吃伤害
    private Color windupColor = new Color(1f, 0.72f, 0.2f, 1f);


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        health = maxHealth;
        baseColor = new Color(0.45f, 0.18f, 0.55f, 1f);
        baseScale = transform.localScale;

        rb.gravityScale = 3.5f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = CreatePlaceholderSprite();

        spriteRenderer.color = baseColor;
        spriteRenderer.sortingLayerName = "Player";
        spriteRenderer.sortingOrder = 5;

        if (obstacleMask.value == 0)
            obstacleMask = LayerMask.GetMask("Ground");

        EnsureAttackHitbox();
    }

    private void OnEnable()
    {
        Spawned?.Invoke(this);
    }

    private void OnDisable()
    {
        Removed?.Invoke(this);
    }

    private void Start()
    {
        origin = transform.position;
        if (dropCard == null && grantFoolCardIfEmpty)
            dropCard = Resources.Load<MonsterCardData>("Cards/Fool");
    }

    public void Configure(int healthValue, bool dropsFoolCard, Player targetPlayer, string nameOverride = null)
    {
        maxHealth = Mathf.Max(1, healthValue);
        health = maxHealth;
        player = targetPlayer;
        if (!string.IsNullOrEmpty(nameOverride))
            displayName = nameOverride;
        grantFoolCardIfEmpty = dropsFoolCard;
        if (!dropsFoolCard)
            dropCard = null;
        else if (dropCard == null)
            dropCard = Resources.Load<MonsterCardData>("Cards/Fool");

        HealthChanged?.Invoke();

        if (bodyCollider is CapsuleCollider2D capsule)
        {
            capsule.size = new Vector2(0.8f, 1.5f);
            capsule.offset = new Vector2(0f, -0.05f);
        }
    }

    public void RootAt(Player targetPlayer, int healthValue, string nameOverride)
    {
        rooted = true;
        Configure(healthValue, false, targetPlayer, nameOverride);
    }

    public void SetBodyColor(Color color)
    {
        baseColor = color;
        if (spriteRenderer != null)
            spriteRenderer.color = color;
    }

    public void SetAsPractice()
    {
        noticeRange = 2.2f;
        patrolDistance = 1.4f;
        attackRange = 1.1f;
        chaseSpeed = 1.8f;
    }

    public void SetAsCharger()
    {
        noticeRange = 3.6f;
        chaseSpeed = 2.6f;
        attackRange = 1.2f;
        windupDuration = 0.82f;
    }

    // 孢子怪：不追人，玩家靠近就鼓起来，一段时间后原地大范围喷发；喷发前有明显预兆。
    public void SetAsSpore()
    {
        spore = true;
        noticeRange = 2.9f;
        patrolDistance = 0f;
        moveSpeed = 0f;
        chaseSpeed = 0f;
        attackRange = 2.3f;
        windupDuration = 1.15f;
        strikeDuration = 0.2f;
        recoverDuration = 1.5f;
        windupColor = new Color(0.62f, 1f, 0.45f, 1f);
        UseBurstHitbox();
    }

    // 骷髅兵：发现玩家就自己跑过来，抬手比普通怪快一点。
    public void SetAsSkeleton()
    {
        noticeRange = 9f;
        chaseSpeed = 3.2f;
        moveSpeed = 2.4f;
        attackRange = 1.5f;
        windupDuration = 0.5f;
        recoverDuration = 0.55f;
        windupColor = new Color(0.95f, 0.95f, 1f, 1f);
    }

    // 石甲虫：甲壳很硬，横向的普通攻击打不动，只有从上方砸下来才能破甲。
    public void SetAsArmored()
    {
        armored = true;
        noticeRange = 4f;
        chaseSpeed = 1.2f;
        attackRange = 1.3f;
    }

    // 攻击盒改成以自身为中心的大范围，用于孢子喷发。
    private void UseBurstHitbox()
    {
        if (attackHitbox == null || attackPoint == null)
            return;

        attackHitboxOffset = 0f;
        attackPoint.localPosition = new Vector3(0f, 0.15f, 0f);
        BoxCollider2D box = attackPoint.GetComponent<BoxCollider2D>();
        if (box != null)
            box.size = new Vector2(3.4f, 2.4f);
    }

    public void SetPushable()
    {
        if (rb != null)
            rb.mass = 0.3f;
    }

    public void StayOnPlatform(float minX, float maxX, LayerMask ground)
    {
        platformLock = true;
        platformMinX = Mathf.Min(minX, maxX);
        platformMaxX = Mathf.Max(minX, maxX);
        platformHome = transform.position;
        platformGround = ground;
        float room = (platformMaxX - platformMinX) * 0.5f;
        patrolDistance = Mathf.Min(patrolDistance, Mathf.Max(0.2f, room));
    }

    public void SetVisualScale(Vector3 scale)
    {
        transform.localScale = scale;
        baseScale = scale;
    }

    private void Update()
    {
        if (dead)
            return;

        actionTimer -= Time.deltaTime;
        switch (action)
        {
            case EnemyAction.Patrol:
                if (rooted)
                {
                    HoldStill();
                    FaceTowardPlayer();
                    break;
                }

                Patrol();
                TryNoticePlayer();
                break;
            case EnemyAction.Chase:
                if (rooted)
                {
                    HoldStill();
                    action = EnemyAction.Patrol;
                    break;
                }

                Chase();
                break;
            case EnemyAction.Windup:
                HoldStill();
                if (!IsPlayerInAttackRange(attackRange + windupCancelPadding))
                {
                    CancelWindup();
                    break;
                }

                // 孢子怪在蓄力期间会一鼓一鼓地胀大，给玩家时间跑开。
                if (spore)
                {
                    float pulse = Mathf.PingPong(Time.time * 5f, 1f);
                    transform.localScale = baseScale * (1.08f + 0.28f * pulse);
                }

                if (actionTimer <= 0f)
                    BeginStrike();
                break;
            case EnemyAction.Strike:
                HoldStill();
                if (actionTimer <= 0f)
                    BeginRecover();
                break;
            case EnemyAction.Recover:
                HoldStill();
                if (actionTimer <= 0f)
                    action = CanSeePlayer() ? EnemyAction.Chase : EnemyAction.Patrol;
                break;
            case EnemyAction.Hurt:
                if (actionTimer <= 0f)
                {
                    spriteRenderer.color = baseColor;
                    transform.localScale = baseScale;
                    action = !rooted && CanSeePlayer() ? EnemyAction.Chase : EnemyAction.Patrol;
                }
                break;
        }
    }

    private void LateUpdate()
    {
        if (dead || !platformLock)
            return;

        Vector3 position = rb.position;
        if (position.y < platformHome.y - 0.3f)
        {
            rb.position = platformHome;
            transform.position = platformHome;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float clampedX = Mathf.Clamp(position.x, platformMinX, platformMaxX);
        if (Mathf.Approximately(clampedX, position.x))
            return;

        position.x = clampedX;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (dead)
            return;

        // 石甲虫：只有从上方砸下来的攻击才吃伤害，横向砍只会「叮」一声被弹开。
        if (armored && hitFrom.y <= transform.position.y + 0.45f)
        {
            spriteRenderer.color = new Color(0.75f, 0.8f, 0.9f, 1f);
            transform.localScale = new Vector3(baseScale.x * 0.94f, baseScale.y * 1.06f, baseScale.z);
            action = EnemyAction.Hurt;
            actionTimer = 0.18f;
            AttackFeedback.Thud(transform.position);
            return;
        }

        EndStrike();
        health = Mathf.Max(0, health - amount);
        bool heavy = amount >= 2;
        spriteRenderer.color = heavy ? new Color(1f, 0.9f, 0.4f, 1f) : Color.white;
        transform.localScale = new Vector3(baseScale.x * (heavy ? 1.28f : 1.08f), baseScale.y * (heavy ? 0.38f : 0.68f), baseScale.z);

        float knockDirection = transform.position.x >= hitFrom.x ? 1f : -1f;
        rb.linearVelocity = rooted || platformLock ? Vector2.zero : new Vector2(knockDirection * knockback.x, knockback.y);
        Face((int)knockDirection);

        HealthChanged?.Invoke();
        if (health <= 0)
        {
            Die();
            return;
        }

        action = EnemyAction.Hurt;
        actionTimer = hurtDuration + (heavy ? 0.32f : 0f);
    }

    private void Patrol()
    {
        float left = origin.x - patrolDistance;
        float right = origin.x + patrolDistance;

        if (transform.position.x >= right)
            moveDirection = -1;
        else if (transform.position.x <= left)
            moveDirection = 1;

        if (ObstacleAhead(moveDirection) || LeavesPlatform(moveDirection))
            moveDirection *= -1;

        if (ObstacleAhead(moveDirection) || LeavesPlatform(moveDirection))
        {
            HoldStill();
            return;
        }

        Move(moveDirection, moveSpeed);
    }

    private void TryNoticePlayer()
    {
        if (!CanSeePlayer())
            return;

        FaceTowardPlayer();
        action = EnemyAction.Chase;
    }

    private void Chase()
    {
        if (!CanSeePlayer(noticeRange * 1.35f))
        {
            action = EnemyAction.Patrol;
            return;
        }

        float deltaX = player.transform.position.x - transform.position.x;
        int direction = deltaX >= 0f ? 1 : -1;
        Face(direction);

        if (IsPlayerInAttackRange(attackRange))
        {
            BeginWindup();
            return;
        }

        if (ObstacleAhead(direction) || LeavesPlatform(direction))
        {
            HoldStill();
            return;
        }

        Move(direction, chaseSpeed);
    }

    private bool IsPlayerInAttackRange(float range)
    {
        if (player == null)
            return false;

        float deltaX = Mathf.Abs(player.transform.position.x - transform.position.x);
        float deltaY = Mathf.Abs(player.transform.position.y - transform.position.y);
        return deltaX <= range && deltaY <= 1.6f;
    }

    private void BeginWindup()
    {
        if (dead)
            return;

        action = EnemyAction.Windup;
        actionTimer = windupDuration;
        HoldStill();
        FaceTowardPlayer();
        spriteRenderer.color = windupColor;
        transform.localScale = baseScale * 1.12f;
    }

    private void CancelWindup()
    {
        EndStrike();
        transform.localScale = baseScale;
        spriteRenderer.color = baseColor;
        action = EnemyAction.Recover;
        actionTimer = recoverDuration;
    }

    private void BeginStrike()
    {
        if (dead || action != EnemyAction.Windup)
            return;

        action = EnemyAction.Strike;
        actionTimer = strikeDuration;
        spriteRenderer.color = new Color(1f, 0.25f, 0.2f, 1f);
        if (spore)
            SpawnBurstVisual();
        if (attackHitbox != null)
            attackHitbox.BeginSwing();
    }

    private void BeginRecover()
    {
        EndStrike();
        action = EnemyAction.Recover;
        actionTimer = recoverDuration;
        spriteRenderer.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 1f);
        transform.localScale = baseScale;
    }

    private void EndStrike()
    {
        if (attackHitbox != null)
            attackHitbox.EndSwing();
    }

    // 孢子喷发的可视范围（占位美术：一个扩散淡出的方块，正式美术可以换成孢子云）
    private void SpawnBurstVisual()
    {
        GameObject ring = new GameObject("SporeBurst");
        ring.transform.position = transform.position + Vector3.up * 0.2f;
        SpriteRenderer ringSprite = ring.AddComponent<SpriteRenderer>();
        ringSprite.sprite = CreatePlaceholderSprite();
        ringSprite.color = new Color(0.65f, 1f, 0.45f, 0.5f);
        ringSprite.sortingLayerName = "Player";
        ringSprite.sortingOrder = 12;
        ring.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
        StartCoroutine(AnimateBurst(ring, ringSprite));
    }

    private IEnumerator AnimateBurst(GameObject ring, SpriteRenderer ringSprite)
    {
        float elapsed = 0f;
        const float duration = 0.35f;
        while (elapsed < duration && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ring.transform.localScale = new Vector3(Mathf.Lerp(1.2f, 3.4f, t), Mathf.Lerp(0.8f, 2.4f, t), 1f);
            Color color = ringSprite.color;
            color.a = 0.5f * (1f - t);
            ringSprite.color = color;
            yield return null;
        }

        if (ring != null)
            Destroy(ring);
    }

    private void Die()
    {
        if (dead)
            return;

        dead = true;
        action = EnemyAction.Recover;
        EndStrike();
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (bodyCollider != null)
            bodyCollider.enabled = false;

        if (!rewardGranted && dropCard != null)
        {
            rewardGranted = true;
            MonsterCardData reward = dropCard;
            dropCard = null;
            CardReward.Grant(reward);
        }

        Died?.Invoke();
        StartCoroutine(PlayDeath());
    }

    private IEnumerator PlayDeath()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        while (elapsed < deathDelay)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathDelay);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, t);
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, new Color(0.1f, 0.05f, 0.12f, 0f), t);
            yield return null;
        }

        Destroy(gameObject);
    }

    private bool CanSeePlayer(float range)
    {
        if (player == null)
            return false;

        Vector2 offset = player.transform.position - transform.position;
        return Mathf.Abs(offset.x) <= range && Mathf.Abs(offset.y) <= 2.5f;
    }

    private bool CanSeePlayer()
    {
        return CanSeePlayer(noticeRange);
    }

    private void Move(int direction, float speed)
    {
        Face(direction);
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
    }

    private void HoldStill()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void FaceTowardPlayer()
    {
        if (player == null)
            return;

        Face(player.transform.position.x >= transform.position.x ? 1 : -1);
    }

    private void Face(int direction)
    {
        if (direction == 0)
            return;

        moveDirection = direction > 0 ? 1 : -1;
        spriteRenderer.flipX = moveDirection < 0;
        if (attackPoint != null)
            attackPoint.localPosition = new Vector3(attackHitboxOffset * moveDirection, 0.1f, 0f);
    }

    private bool LeavesPlatform(int direction)
    {
        if (!platformLock || direction == 0)
            return false;

        float ahead = transform.position.x + direction * 0.12f;
        if (ahead < platformMinX || ahead > platformMaxX)
            return true;

        if (bodyCollider == null || platformGround.value == 0)
            return false;

        Vector2 probe = new Vector2(
            bodyCollider.bounds.center.x + direction * (bodyCollider.bounds.extents.x + 0.16f),
            bodyCollider.bounds.min.y + 0.05f);
        return Physics2D.Raycast(probe, Vector2.down, 0.6f, platformGround).collider == null;
    }

    private bool ObstacleAhead(int direction)
    {
        if (obstacleMask.value == 0 || bodyCollider == null)
            return false;

        Vector2 originPoint = bodyCollider.bounds.center;
        float distance = bodyCollider.bounds.extents.x + 0.08f;
        return Physics2D.Raycast(originPoint, Vector2.right * direction, distance, obstacleMask).collider != null;
    }

    private void EnsureAttackHitbox()
    {
        attackHitbox = GetComponentInChildren<EnemyAttackHitbox>(true);
        if (attackHitbox != null)
        {
            attackPoint = attackHitbox.transform;
            return;
        }

        GameObject hitboxObject = new GameObject("EnemyAttackHitbox");
        hitboxObject.transform.SetParent(transform);
        hitboxObject.transform.localRotation = Quaternion.identity;
        hitboxObject.transform.localPosition = new Vector3(attackHitboxOffset, 0.1f, 0f);
        hitboxObject.layer = gameObject.layer;
        attackPoint = hitboxObject.transform;

        BoxCollider2D box = hitboxObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = attackHitboxSize;
        box.enabled = false;
        attackHitbox = hitboxObject.AddComponent<EnemyAttackHitbox>();
        attackHitbox.Initialize(this, box);
    }

    private static Sprite CreatePlaceholderSprite()
    {
        const int width = 24;
        const int height = 36;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 24f);
    }
}
