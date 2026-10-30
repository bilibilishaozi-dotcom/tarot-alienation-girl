using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{

    public Animator anim { get; private set; }

    public Rigidbody2D rb { get; private set; }
    public PlayerInputSet input { get; private set; }
    private CapsuleCollider2D bodyCollider;
    private PlayerCardLoadout cardLoadout;
    private PlayerAttackHitbox attackHitbox;
    private readonly Collider2D[] overlapResults = new Collider2D[16];

    private StateMachine stateMachine;

    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_JumpState jumpState { get; private set; }
    public Player_FallState fallState { get; private set; }
    public Player_WallSlideState wallSlideState{ get; private set; }
    public Player_WallJumpState wallJumpState{ get; private set; }
    public Player_DashState dashState { get; private set; }
    public Player_BasicAttackState basicAttackState { get; private set; }
    public Player_JumpAttackState jumpAttackState { get; private set; }

    [Header("Attack details")]
    public Vector2[] attackVelocity;
    public Vector2 jumpAttackVelocity;
    public float attackVelocityDuration = 0.1f;
    public float comboResetTime = 0.45f;
    [SerializeField] private int baseAttackDamage = 1;
    [SerializeField] private Vector2 attackHitboxSize = new Vector2(1.25f, 0.85f);
    [SerializeField] private Vector2 attackHitboxOffset = new Vector2(0.95f, -0.1f);
    private Coroutine queuedAttackCo;

    [Header("Movement details")]
    public float moveSpeed;
    public float jumpForce = 5;
    public Vector2 wallJumpForce;

    //Range代表滑条(0,1)代表从0到1，方便调试
    [Range(0, 1)]
    //inAirMoveMultiplier 空中控制削弱
    public float inAirMoveMultiplier = 0.7f;
    [Range(0, 1)]
    //控制贴墙下滑速度减慢多少
    public float wallSlideSlowMultiplier = 0.7f;
    //[Space]:Inspector里加空行
    [Space]
    public float dashDuration = 0.25f;
    public float dashSpeed = 20;
    [SerializeField] private float collisionSkin = 0.05f;



    private bool facingRight = true;
    private SpriteRenderer bodySprite;
    private Color bodySpriteColor = Color.white;
    private float hitStunTimer;
    private float invulnerableTimer;
    private float nextHealTime;
    private const float WallDetachGrace = 0.08f;
    private float wallDetachTimer;
    // 玩家主动按离墙方向后的冷却：这段时间内不再判定为「贴在墙上」，
    // 否则按反方向时会被贴墙状态反复抓回去，看起来就是“离开不了”。
    private float wallReleaseTimer;
    public bool IsDodging { get; private set; }

    // 正在空中挥砍/下砸——石甲虫只有这种攻击能破甲
    public bool IsAirAttack => stateMachine != null && stateMachine.currentState == jumpAttackState;

    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDefeated { get; private set; }
    public event Action HealthChanged;
    public event Action<int> Damaged;

    //射线方向和跳墙方向可能会用到facingDir 检测是+1还是-1
    public int facingDir { get; private set; } = 1;
    public Vector2 moveInput { get; private set; }
    public int CurrentAttackDamage => Mathf.Max(1, Mathf.RoundToInt(baseAttackDamage * (cardLoadout != null ? cardLoadout.DamageMultiplier : 1f)));
    public float AttackLungeMultiplier => cardLoadout != null ? cardLoadout.LungeMultiplier : 1f;
    public float AttackReachMultiplier => cardLoadout != null ? cardLoadout.ReachMultiplier : 1f;
    public bool HasChariot => cardLoadout != null && cardLoadout.DashEcho;
    public int ChariotEchoDamage => HasChariot ? Mathf.Max(1, cardLoadout.EchoDamage) : 0;
    public bool HasLandingSlam => cardLoadout != null && cardLoadout.LandingSlam;

    public void PlayLandingSlam()
    {
        if (!HasLandingSlam)
            return;

        StrengthSlam.Fire(this, cardLoadout.SlamDamage);
    }

    public float AttackFrontReach(float reachMultiplier)
    {
        float extra = attackHitboxSize.x * (Mathf.Max(1f, reachMultiplier) - 1f);
        return attackHitboxOffset.x + attackHitboxSize.x * 0.5f + extra;
    }

    [Header("Collision detection")]
    //[SerializeField]代表虽然是私密值，但是显示在Unity属性栏中
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private float wallCheckDistance;
    [SerializeField] private float groundStickDistance = 0.12f;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private Transform primaryWallCheck;
    [SerializeField] private Transform secondaryWallCheck;
    public bool groundDetected { get; private set; }
    public bool wallDetected { get; private set; }
    // 脚下是否有实体（含敌人/箱子），用于「踩在怪物头上也算落地」
    public bool supportDetected { get; private set; }

    // ── 给骨骼动画（PlayerRig）读的只读状态 ──
    public bool IsMoving => Mathf.Abs(moveInput.x) > 0.01f;
    public float HorizontalSpeed => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
    public bool IsHurtNow => invulnerableTimer > 0.35f;
    public bool IsAttacking => stateMachine != null &&
        (stateMachine.currentState == basicAttackState || stateMachine.currentState == jumpAttackState);

    // 「现在能不能贴墙」= 真的贴着墙 + 不在脱墙冷却里 + 没有按离墙方向。
    // 状态机里判断贴墙请用这个，不要直接用 wallDetected。
    public bool wallLatched
    {
        get
        {
            if (!wallDetected || wallReleaseTimer > 0f)
                return false;

            // 正往墙的反方向推的时候，任何情况下都不算贴住。
            if (Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) != facingDir)
                return false;

            return true;
        }
    }

    public void ReleaseWall(float duration = 0.25f)
    {
        wallReleaseTimer = Mathf.Max(wallReleaseTimer, duration);
    }




    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        bodySprite = GetComponentInChildren<SpriteRenderer>();
        if (bodySprite != null)
            bodySpriteColor = bodySprite.color;
        cardLoadout = GetComponent<PlayerCardLoadout>();
        if (cardLoadout == null)
            cardLoadout = gameObject.AddComponent<PlayerCardLoadout>();
        if (GetComponent<CardSelectUI>() == null)
            gameObject.AddComponent<CardSelectUI>();
        if (GetComponent<CombatHud>() == null)
            gameObject.AddComponent<CombatHud>();
        if (GetComponent<TrainingEncounter>() == null)
            gameObject.AddComponent<TrainingEncounter>();
        // 血量低于 33% 时的红屏 + 心跳
        LowHealthWarning.Attach(this);

        // 换成冰蓝星辉魔法少女的骨骼：脚底沿用旧精灵的视觉底边（旧模型本来就站得对）
        float feetY = bodySprite != null
            ? bodySprite.bounds.min.y - transform.position.y
            : (bodyCollider != null ? bodyCollider.bounds.min.y - transform.position.y : -0.75f);
        WitchRig witch = WitchRig.Attach(this, feetY, 1.8f);
        if (witch != null)
        {
            // 关掉玩家身上所有旧的 SpriteRenderer（之前只关了 bodySprite，结果旧的还在显示，
            // 屏幕上会同时出现两个角色）
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].transform.IsChildOf(witch.transform))
                    continue;
                renderers[i].enabled = false;
            }

            if (anim != null)
                anim.enabled = false;
        }

        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;

        stateMachine = new StateMachine();
        input = new PlayerInputSet();

        idleState = new Player_IdleState(this, stateMachine, "idle");
        moveState = new Player_MoveState(this, stateMachine, "move");
        jumpState = new Player_JumpState(this, stateMachine, "jumpFall");
        fallState = new Player_FallState(this, stateMachine, "jumpFall");
        wallSlideState = new Player_WallSlideState(this, stateMachine, "wallSlide");
        wallJumpState = new Player_WallJumpState(this, stateMachine, "jumpFall");
        dashState = new Player_DashState(this, stateMachine, "dash");
        basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");
        jumpAttackState = new Player_JumpAttackState(this, stateMachine, "jumpAttack");
        EnsureAttackHitbox();
        if (GetComponent<FirstLevel>() == null)
            gameObject.AddComponent<FirstLevel>();
    }

    private void OnEnable()
    {
        //启动Input System 开始监听输入事件
        input.Enable();

        //当Movement输入发生时,更新moveInput
        // 用具名方法订阅，OnDisable 里才能退订；
        // 之前用 lambda，组件每次启用都会多挂一个回调，退不掉。
        input.Player.Movement.performed += OnMovePerformed;
        input.Player.Movement.canceled += OnMoveCanceled;
    }

    //OnDisable() 关闭输入系统
    private void OnDisable()
    {
        input.Player.Movement.performed -= OnMovePerformed;
        input.Player.Movement.canceled -= OnMoveCanceled;
        input.Disable();
    }

    private void OnDestroy()
    {
        input?.Dispose();
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void Start()
    {
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
        // H：一键回满血（有 1.2 秒冷却，避免连按）
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && Time.unscaledTime >= nextHealTime)
        {
            nextHealTime = Time.unscaledTime + 1.2f;
            RestoreFullHealth();
        }

        //每帧检测地面、墙壁
        TickHurtTimers();
        HandleCollisionDetection();
        stateMachine.UpdateActiveState();
    }

    private void FixedUpdate()
    {
        SeparateFromWalls();
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        if (input == null)
            return;

        if (enabled)
        {
            if (IsDefeated)
                return;

            input.Player.Enable();
            moveInput = ReadKeyboardMove();
        }
        else
        {
            input.Player.Disable();
            moveInput = Vector2.zero;
        }
    }

    private static Vector2 ReadKeyboardMove()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            x += 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            y -= 1f;
        return new Vector2(x, y);
    }

    public bool AttackSwingActive { get; private set; }

    public void BeginAttackSwing()
    {
        AttackSwingActive = true;
        if (attackHitbox != null)
            attackHitbox.BeginSwing();
    }

    public void EndAttackSwing()
    {
        AttackSwingActive = false;
        if (attackHitbox != null)
            attackHitbox.EndSwing();
    }

    public bool IsSolidAhead(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f)
            return false;

        return CastHorizontal(Mathf.Sign(direction), wallCheckDistance + collisionSkin).collider != null;
    }

    public void EnterAttackStateWithDelay()
    {
        //判断是否已经存在等待中的协程
        if (queuedAttackCo != null)
            StopCoroutine(queuedAttackCo);
        //启动协程 方便以后停止
        queuedAttackCo = StartCoroutine(EnterAttackStateWithDelayCo());
    }
    private IEnumerator EnterAttackStateWithDelayCo()
    {
        //等当前帧所有更新完成后再继续执行
        yield return new WaitForEndOfFrame();
        stateMachine.ChangeState(basicAttackState);
    }

    public void CallAnimationTrigger()
    {
        //动画最后一帧执行此代码，调用当前方法
        //如果当前状态时AttackState 实际执行AttackState.CallAnimationTrigger()
        //由于AttackState继承EntityState 所以最终执行：triggerCalled = true;（来源于EntityState中的同名方法）
        stateMachine.currentState.CallAnimationTrigger();
    }

    public void BeginDodge()
    {
        IsDodging = true;
        hitStunTimer = 0f;
        SetDodgeThroughEnemies(true);
    }

    // 一键回血（演示/试玩用）。H 键触发，见 Update。
    public void RestoreFullHealth()
    {
        if (IsDefeated || CurrentHealth >= maxHealth)
            return;

        CurrentHealth = maxHealth;
        HealthChanged?.Invoke();
    }

    public void EndDodge()
    {
        IsDodging = false;
        SetDodgeThroughEnemies(false);
    }

    // 冲刺穿过怪物身体：冲刺期间临时关掉玩家与所有敌人的物理碰撞，结束再恢复。
    // 敌人如果在冲刺途中死亡，它的 collider 被销毁，IgnoreCollision 记录也随之消失。
    private void SetDodgeThroughEnemies(bool ignore)
    {
        if (bodyCollider == null)
            return;

        BasicEnemy[] enemies = FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            Collider2D other = enemies[i] != null ? enemies[i].GetComponent<Collider2D>() : null;
            if (other != null)
                Physics2D.IgnoreCollision(bodyCollider, other, ignore);
        }

        RiftGuardian[] bosses = FindObjectsByType<RiftGuardian>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            Collider2D other = bosses[i] != null ? bosses[i].GetComponent<Collider2D>() : null;
            if (other != null)
                Physics2D.IgnoreCollision(bodyCollider, other, ignore);
        }
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (amount <= 0 || IsDefeated || IsDodging || invulnerableTimer > 0f)
            return;

        int lost = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= lost;
        invulnerableTimer = 0.7f;
        hitStunTimer = 0.22f;
        float knockDirection = transform.position.x >= hitFrom.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(knockDirection * 6f, 4.5f);
        if (bodySprite != null)
            bodySprite.color = new Color(1f, 0.3f, 0.3f, 1f);

        // 挨打也要有反馈：震一下 + 顿一下 + 一声闷响
        Juice.Thump();
        Juice.Shake(0.4f);
        Juice.HitStop(0.06f);

        if (CurrentHealth <= 0)
            Defeat();

        HealthChanged?.Invoke();
        Damaged?.Invoke(lost);
    }

    private void Defeat()
    {
        IsDefeated = true;
        EndAttackSwing();
        EndDodge();
        SetGameplayInputEnabled(false);
    }

    private void TickHurtTimers()
    {
        TickHitStun();

        if (IsDefeated)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (bodySprite != null)
                bodySprite.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            return;
        }

        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            if (bodySprite != null)
            {
                bool visible = Mathf.FloorToInt(Time.time * 16f) % 2 == 0;
                Color color = bodySpriteColor;
                color.a = visible ? 1f : 0.35f;
                bodySprite.color = color;
            }
        }
        else if (bodySprite != null && bodySprite.color != bodySpriteColor)
        {
            bodySprite.color = bodySpriteColor;
        }
    }

    private void TickHitStun()
    {
        if (hitStunTimer <= 0f)
            return;

        hitStunTimer -= Time.deltaTime;
        if (hitStunTimer > 0f)
            return;

        hitStunTimer = 0f;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    public void SetVelocity(float xVelocity, float yVelocity)
    {
        if (IsDefeated || hitStunTimer > 0f)
            return;

        Vector2 velocity = new Vector2(xVelocity, yVelocity);
        velocity.x = ClampHorizontalSpeed(velocity.x);
        velocity.y = ClampDownwardSpeed(velocity.y);
        rb.linearVelocity = velocity;
        HandleFlip(xVelocity);
    }

    private void HandleFlip(float xVelcoity)
    {
        if (xVelcoity > 0 && facingRight == false)
            Flip();
        else if (xVelcoity < 0 && facingRight)
            Flip();
    }

    public void Flip()
    {
        transform.Rotate(0, 180, 0);
        facingRight = !facingRight;
        facingDir = facingDir * -1;
    }

    private void HandleCollisionDetection()
    {
        float feetDistance = FeetDistanceFromOrigin();
        float rayLength = Mathf.Max(groundCheckDistance, feetDistance + groundStickDistance);
        RaycastHit2D groundHit = Physics2D.Raycast(transform.position, Vector2.down, rayLength, whatIsGround);
        groundDetected = groundHit.collider != null && groundHit.distance <= feetDistance + groundStickDistance;

        if (primaryWallCheck == null || secondaryWallCheck == null)
        {
            wallDetected = false;
            return;
        }

        Vector2 dir = Vector2.right * facingDir;
        // 射线从身体中线出发，所以长度要覆盖「半身宽 + 一点余量」，
        // 否则胶囊贴着墙停下时（中线离墙面约 0.39~0.44）0.4 的射线正好打不到。
        float halfWidth = bodyCollider != null ? bodyCollider.bounds.extents.x : 0.385f;
        float probe = Mathf.Max(wallCheckDistance, halfWidth + 0.12f);
        bool upperHit = Physics2D.Raycast(primaryWallCheck.position, dir, probe, whatIsGround);
        bool lowerHit = Physics2D.Raycast(secondaryWallCheck.position, dir, probe, whatIsGround);
        // 两条射线都从身体中线出发，贴着墙时可能都差一点，所以再补一次从碰撞体边缘出发的胶囊投射。
        bool wallNow = (upperHit && lowerHit) || IsSolidAhead(facingDir);

        // 贴墙判定保持 0.08 秒，避免推着墙的瞬间因为一帧没打中而掉出贴墙状态。
        if (wallNow)
        {
            wallDetected = true;
            wallDetachTimer = 0f;
        }
        else if (wallDetected)
        {
            wallDetachTimer += Time.deltaTime;
            if (wallDetachTimer >= WallDetachGrace)
                wallDetected = false;
        }

        UpdateSupport();

        if (wallReleaseTimer > 0f)
            wallReleaseTimer -= Time.deltaTime;
    }

    // 脚下有没有「能站住的东西」：地面、敌人脑袋、箱子都算。
    // groundDetected 只认 Ground 层，所以踩在怪物头上时它一直是 false，
    // 空中攻击就会因为等不到 groundDetected 而永远出不来（人卡在半空）。
    private void UpdateSupport()
    {
        supportDetected = false;
        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.06f);
        Vector2 size = new Vector2(Mathf.Max(0.12f, bounds.size.x * 0.7f), 0.12f);
        int mask = ~(1 << gameObject.layer);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.12f, mask);
        // 侧面擦到墙（normal 基本是 ±x）不算站住；触发器（卡牌之类的）也不算。
        supportDetected = hit.collider != null && !hit.collider.isTrigger && hit.normal.y > 0.5f;
    }

    private float FeetDistanceFromOrigin()
    {
        if (bodyCollider == null)
            return groundCheckDistance;

        return Mathf.Max(0.1f, transform.position.y - bodyCollider.bounds.min.y);
    }

    private float ClampHorizontalSpeed(float xVelocity)
    {
        if (Mathf.Abs(xVelocity) < 0.01f)
            return xVelocity;

        float direction = Mathf.Sign(xVelocity);
        float travel = Mathf.Abs(xVelocity) * Time.fixedDeltaTime;
        RaycastHit2D hit = CastHorizontal(direction, travel + collisionSkin);
        if (hit.collider == null)
            return xVelocity;

        float allowedDistance = Mathf.Max(0f, hit.distance - collisionSkin);
        float maxSpeed = allowedDistance / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        return direction * Mathf.Min(Mathf.Abs(xVelocity), maxSpeed);
    }

    private float ClampDownwardSpeed(float yVelocity)
    {
        if (yVelocity >= 0f || bodyCollider == null)
            return yVelocity;

        Vector2 size = ScaledColliderSize();
        size.x *= 0.8f;
        float travel = Mathf.Abs(yVelocity) * Time.fixedDeltaTime;
        RaycastHit2D hit = Physics2D.CapsuleCast(bodyCollider.bounds.center, size, bodyCollider.direction, 0f, Vector2.down, travel + collisionSkin, whatIsGround);
        if (hit.collider == null)
            return yVelocity;

        float allowedDistance = Mathf.Max(0f, hit.distance - collisionSkin);
        float maxSpeed = allowedDistance / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        return -Mathf.Min(Mathf.Abs(yVelocity), maxSpeed);
    }

    private RaycastHit2D CastHorizontal(float direction, float distance)
    {
        if (bodyCollider == null)
            return default;

        Vector2 size = ScaledColliderSize();
        size.y *= 0.55f;
        Vector2 origin = bodyCollider.bounds.center + Vector3.up * (bodyCollider.bounds.extents.y * 0.2f);
        return Physics2D.CapsuleCast(origin, size, bodyCollider.direction, 0f, Vector2.right * direction, distance, whatIsGround);
    }

    private Vector2 ScaledColliderSize()
    {
        Vector2 size = bodyCollider.size;
        size.x *= Mathf.Abs(transform.lossyScale.x);
        size.y *= Mathf.Abs(transform.lossyScale.y);
        return size;
    }

    private void SeparateFromWalls()
    {
        if (bodyCollider == null)
            return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(whatIsGround);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int count = bodyCollider.Overlap(filter, overlapResults);
        for (int i = 0; i < count; i++)
        {
            ColliderDistance2D distance = bodyCollider.Distance(overlapResults[i]);
            if (!distance.isOverlapped || Mathf.Abs(distance.normal.x) < 0.5f)
                continue;

            rb.position += distance.normal * distance.distance;
        }
    }

    private void EnsureAttackHitbox()
    {
        attackHitbox = GetComponentInChildren<PlayerAttackHitbox>(true);
        if (attackHitbox != null)
            return;

        GameObject hitboxObject = new GameObject("AttackHitbox");
        hitboxObject.transform.SetParent(transform);
        hitboxObject.transform.localPosition = attackHitboxOffset;
        hitboxObject.transform.localRotation = Quaternion.identity;
        hitboxObject.layer = gameObject.layer;

        BoxCollider2D box = hitboxObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = attackHitboxSize;
        box.enabled = false;

        attackHitbox = hitboxObject.AddComponent<PlayerAttackHitbox>();
        attackHitbox.Initialize(this, box);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, -groundCheckDistance));
        if (primaryWallCheck != null)
            Gizmos.DrawLine(primaryWallCheck.position, primaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
        if (secondaryWallCheck != null)
            Gizmos.DrawLine(secondaryWallCheck.position, secondaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
    }
}
