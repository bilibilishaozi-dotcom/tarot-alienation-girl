using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BasicEnemy : MonoBehaviour, IDamageable
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
    [SerializeField] private MonsterCardData dropCard;
    [SerializeField] private bool grantFoolCardIfEmpty = true;
    [SerializeField] private float deathDelay = 0.45f;
    [SerializeField] private Vector2 knockback = new Vector2(3.5f, 2.5f);

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

    public int AttackDamage => attackDamage;
    public bool IsStrikeActive => !dead && action == EnemyAction.Strike;

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

    private void Start()
    {
        origin = transform.position;
        if (dropCard == null && grantFoolCardIfEmpty)
            dropCard = Resources.Load<MonsterCardData>("Cards/Fool");
    }

    public void Configure(int healthValue, bool dropsFoolCard, Player targetPlayer)
    {
        maxHealth = healthValue;
        health = healthValue;
        player = targetPlayer;
        grantFoolCardIfEmpty = dropsFoolCard;
        if (!dropsFoolCard)
            dropCard = null;
        else if (dropCard == null)
            dropCard = Resources.Load<MonsterCardData>("Cards/Fool");

        if (bodyCollider is CapsuleCollider2D capsule)
        {
            capsule.size = new Vector2(0.8f, 1.5f);
            capsule.offset = new Vector2(0f, -0.05f);
        }
    }

    private void Update()
    {
        if (dead)
            return;

        actionTimer -= Time.deltaTime;
        switch (action)
        {
            case EnemyAction.Patrol:
                Patrol();
                TryNoticePlayer();
                break;
            case EnemyAction.Chase:
                Chase();
                break;
            case EnemyAction.Windup:
                HoldStill();
                if (!IsPlayerInAttackRange(attackRange + windupCancelPadding))
                {
                    CancelWindup();
                    break;
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
                    action = CanSeePlayer() ? EnemyAction.Chase : EnemyAction.Patrol;
                }
                break;
        }
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (dead)
            return;

        EndStrike();
        health = Mathf.Max(0, health - amount);
        spriteRenderer.color = new Color(1f, 0.75f, 0.75f, 1f);
        transform.localScale = baseScale;

        float knockDirection = transform.position.x >= hitFrom.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(knockDirection * knockback.x, knockback.y);
        Face((int)knockDirection);

        if (health <= 0)
        {
            Die();
            return;
        }

        action = EnemyAction.Hurt;
        actionTimer = hurtDuration;
    }

    private void Patrol()
    {
        float left = origin.x - patrolDistance;
        float right = origin.x + patrolDistance;

        if (transform.position.x >= right)
            moveDirection = -1;
        else if (transform.position.x <= left)
            moveDirection = 1;

        if (ObstacleAhead(moveDirection))
            moveDirection *= -1;

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

        if (ObstacleAhead(direction))
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
        spriteRenderer.color = new Color(1f, 0.72f, 0.2f, 1f);
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
