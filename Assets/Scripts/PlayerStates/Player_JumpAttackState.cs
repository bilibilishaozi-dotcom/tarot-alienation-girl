using UnityEngine;

public class Player_JumpAttackState : EntityState
{
    private const float HitStart = 0.06f;

    private bool touchedGround;
    private bool swingActive;
    private bool hurtOnEnter;
    private float elapsed;

    public Player_JumpAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Enter()
    {
        base.Enter();
        touchedGround = false;
        swingActive = false;
        hurtOnEnter = player.IsHurtNow;
        elapsed = 0f;
        player.basicAttackState.ResetCombo();

        float drop = player.HasLandingSlam ? -15f : player.jumpAttackVelocity.y;
        player.SetVelocity(player.jumpAttackVelocity.x * player.AttackLungeMultiplier * player.facingDir, drop);
    }
    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        elapsed += Time.deltaTime;
        UpdateHitWindow();

        // 死亡，或攻击过程中新挨的打，立刻离开，避免停在这个状态里出不去。
        // 进状态时已经在受伤闪烁里的，不把这次攻击直接取消。
        if (player.IsDefeated || (!hurtOnEnter && player.IsHurtNow))
        {
            ReleaseToMovement(false);
            return;
        }

        // 打中就结束。判定框里同一目标只会记一次，不会在停住的姿势上每帧再打。
        if (player.AttackHasHit)
        {
            ReleaseToMovement(false);
            return;
        }

        // 上升时擦到敌人头顶不算落地，否则贴着敌人跳攻会立刻被判着地而退出。
        // 和坠落落地同一条：地面，或已经向下并且踩到能站住的东西。
        bool landed = player.groundDetected || (player.supportDetected && rb.linearVelocity.y <= 0.05f);
        if (landed && touchedGround == false)
        {
            touchedGround = true;
            if (player.HasLandingSlam)
                player.PlayLandingSlam();
            ReleaseToMovement(true);
        }
    }

    // 还在空中就回下落（画面仍按竖直速度播 jump 或 fall）。着地回待机，待机里再切跑步。
    private void ReleaseToMovement(bool landed)
    {
        EndSwing();
        bool onGround = landed || player.groundDetected || player.supportDetected;
        stateMachine.ChangeState(onGround ? player.idleState : player.fallState);
    }

    private void UpdateHitWindow()
    {
        bool inWindow = !touchedGround && elapsed >= HitStart;
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

    public override void Exit()
    {
        base.Exit();
        player.EndAttackSwing();
    }
}