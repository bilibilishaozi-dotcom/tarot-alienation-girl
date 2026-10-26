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
    private readonly Collider2D[] overlapResults = new Collider2D[4];

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
    public float comboResetTime = 1;
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
    public bool IsDodging { get; private set; }

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
    }

    private void OnEnable()
    {
        //启动Input System 开始监听输入事件
        input.Enable();

        //当Movement输入发生时,更新moveInput
        //Movement:方向键   performed:输入正在发生   ctx:输入信息  ReadValue<Vector2>():读取输入值
        input.Player.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>() ;
        //canceled代表输入结束  moveInput = Vector2.zero：没有方向输入了
        input.Player.Movement.canceled += ctx => moveInput = Vector2.zero;

    }
    //OnDisable() 关闭输入系统
    private void OnDisable()
    {
        input.Disable();
    }

    private void Start()
    {
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
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

    public void BeginAttackSwing()
    {
        if (attackHitbox != null)
            attackHitbox.BeginSwing();
    }

    public void EndAttackSwing()
    {
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
    }

    public void EndDodge()
    {
        IsDodging = false;
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
        if (IsDefeated)
        {
            if (hitStunTimer > 0f)
                hitStunTimer -= Time.deltaTime;
            else
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (bodySprite != null)
                bodySprite.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            return;
        }

        if (hitStunTimer > 0f)
            hitStunTimer -= Time.deltaTime;

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

        wallDetected = Physics2D.Raycast(primaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround)
                    && Physics2D.Raycast(secondaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
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
