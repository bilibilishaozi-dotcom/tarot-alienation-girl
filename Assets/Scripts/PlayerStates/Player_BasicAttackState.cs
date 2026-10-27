using UnityEngine;

public class Player_BasicAttackState : EntityState
{
    private static readonly float[] hitStart = { 0.12f, 0.08f, 0.16f };
    private static readonly float[] hitDuration = { 0.14f, 0.12f, 0.16f };
    private const float FinisherRecoverTime = 0.28f;

    private float attackVelocityTimer;
    private float lastTimeAttacked;
    private float elapsed;
    private float finisherTimer;
    private bool comboAttackQueued;
    private bool swingActive;
    private bool finisherStarted;
    private int attackDir;
    private int comboIndex = 1;
    private int comboLimit = 3;
    private const int FirstComboIndex = 1;

    public Player_BasicAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
        if (comboLimit != player.attackVelocity.Length)
            comboLimit = player.attackVelocity.Length;
    }

    public override void Enter()
    {
        base.Enter();
        comboAttackQueued = false;
        swingActive = false;
        finisherStarted = false;
        elapsed = 0f;
        ResetComboIndexIfNeeded();

        attackDir = player.moveInput.x != 0 ? ((int)player.moveInput.x) : player.facingDir;
        anim.SetInteger("basicAttackIndex", comboIndex);
        ApplyAttackVelocity();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        elapsed += Time.deltaTime;
        HandleAttackVelocity();

        if (finisherStarted)
        {
            finisherTimer -= Time.deltaTime;
            if (finisherTimer <= 0f)
                stateMachine.ChangeState(player.idleState);
            return;
        }

        if (input.Player.Attack.WasPressedThisFrame() && elapsed >= InputOpenTime())
            QueueNextAttack();

        UpdateHitWindow();

        if (triggerCalled)
            HandleStateExit();
    }

    public override void Exit()
    {
        base.Exit();
        EndSwing();
        comboIndex++;
        lastTimeAttacked = Time.time;
    }

    public void ResetCombo()
    {
        comboIndex = FirstComboIndex;
        comboAttackQueued = false;
        lastTimeAttacked = 0f;
    }

    private void HandleStateExit()
    {
        if (comboIndex >= comboLimit)
        {
            finisherStarted = true;
            finisherTimer = FinisherRecoverTime;
            EndSwing();
            player.SetVelocity(0f, rb.linearVelocity.y);
            return;
        }

        if (comboAttackQueued)
        {
            anim.SetBool(animBoolName, false);
            player.EnterAttackStateWithDelay();
            return;
        }

        stateMachine.ChangeState(player.idleState);
    }

    private void QueueNextAttack()
    {
        if (comboIndex < comboLimit)
            comboAttackQueued = true;
    }

    private void UpdateHitWindow()
    {
        int step = Mathf.Clamp(comboIndex - 1, 0, hitStart.Length - 1);
        bool inWindow = elapsed >= hitStart[step] && elapsed < hitStart[step] + hitDuration[step];
        if (inWindow && !swingActive)
        {
            swingActive = true;
            player.BeginAttackSwing();
        }
        else if (!inWindow && swingActive)
        {
            EndSwing();
        }
    }

    private void EndSwing()
    {
        swingActive = false;
        player.EndAttackSwing();
    }

    private float InputOpenTime()
    {
        int step = Mathf.Clamp(comboIndex - 1, 0, hitStart.Length - 1);
        return Mathf.Max(0.05f, hitStart[step] - 0.04f);
    }

    private void HandleAttackVelocity()
    {
        attackVelocityTimer -= Time.deltaTime;
        if (attackVelocityTimer < 0f)
            player.SetVelocity(0f, rb.linearVelocity.y);
    }

    private void ApplyAttackVelocity()
    {
        Vector2 attackVelocity = player.attackVelocity[comboIndex - 1];
        attackVelocityTimer = player.attackVelocityDuration;
        player.SetVelocity(attackVelocity.x * player.AttackLungeMultiplier * attackDir, 0f);
    }

    private void ResetComboIndexIfNeeded()
    {
        if (Time.time > lastTimeAttacked + player.comboResetTime)
            comboIndex = FirstComboIndex;

        if (comboIndex > comboLimit)
            comboIndex = FirstComboIndex;
    }
}
