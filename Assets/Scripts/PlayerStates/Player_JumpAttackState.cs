using UnityEngine;

public class Player_JumpAttackState : EntityState
{
    // 力量原来一进状态就把竖直速度设成 -15。挥到第 3 帧要大约 0.15 秒，这段时间能掉下去大约两个身位，爪击还没画出来人已经落地。
    private const float StrengthSlamSpeed = -15f;
    // 正常大约 0.15 秒就画出爪击。到点还没画出来也要下砸，不能一直停在空中。
    private const float StrengthWindupLimit = 0.35f;

    private bool touchedGround;
    private bool swingActive;
    private bool hurtOnEnter;
    private bool strengthDropWaiting;
    private bool strengthGravityHeld;
    private float strengthSavedGravity;
    private float strengthWindupTime;

    public Player_JumpAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Enter()
    {
        base.Enter();
        touchedGround = false;
        swingActive = false;
        hurtOnEnter = player.IsHurtNow;
        // 上一次打中留下的标记不能带到这次。否则一进状态、刀光还没画出来就被判成已经命中。
        player.ClearAttackHit();
        player.basicAttackState.ResetCombo();
        AirAttackTrace.Log("进入跳跃攻击");

        strengthDropWaiting = false;
        strengthGravityHeld = false;
        float drop = player.jumpAttackVelocity.y;
        if (player.HasLandingSlam)
        {
            // 只停力量这一下的起手。爪击帧画出来之后再按原来的 -15 下砸。重力先关掉，避免这 0.15 秒被拽到地上。
            strengthDropWaiting = true;
            strengthWindupTime = 0f;
            strengthSavedGravity = rb.gravityScale;
            rb.gravityScale = 0f;
            strengthGravityHeld = true;
            drop = 0f;
        }
        player.SetVelocity(player.jumpAttackVelocity.x * player.AttackLungeMultiplier * player.facingDir, drop);
    }
    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        bool poseVisible = AirAttackPoseVisible();
        UpdateStrengthDrop(poseVisible);
        UpdateHitWindow(poseVisible);

        // 死亡，或攻击过程中新挨的打，立刻离开，避免停在这个状态里出不去。
        // 进状态时已经在受伤闪烁里的，不把这次攻击直接取消。
        if (player.IsDefeated)
        {
            ReleaseToMovement(false, "死亡");
            return;
        }

        if (!hurtOnEnter && player.IsHurtNow)
        {
            ReleaseToMovement(false, "受伤");
            return;
        }

        // 刀光帧已经画出来之后的命中才算这次攻击打中。判定框也是到这一帧才打开。
        if (poseVisible && player.AttackHasHit)
        {
            ReleaseToMovement(false, "命中 " + player.LastAttackHitName);
            return;
        }

        // 真正踩到地面可以马上结束。脚下的敌人要等刀光帧出来、并且人已经往下，才算落地。
        // 否则贴着敌人起跳，刀光还没画出来就被判成着地退出。
        bool onFloor = player.groundDetected;
        bool onSupport = poseVisible && player.supportDetected && rb.linearVelocity.y <= 0.05f;
        if ((onFloor || onSupport) && touchedGround == false)
        {
            touchedGround = true;
            if (player.HasLandingSlam)
                player.PlayLandingSlam();
            ReleaseToMovement(true, onFloor ? "落地" : "踩到支撑");
        }
    }

    // 力量起手悬着。WitchFrames 画出第 3 帧之后的下一帧，这里才看到，然后恢复重力并下砸。
    private void UpdateStrengthDrop(bool poseVisible)
    {
        if (!strengthDropWaiting)
            return;

        strengthWindupTime += Time.deltaTime;
        if (!poseVisible && strengthWindupTime < StrengthWindupLimit)
        {
            player.SetVelocity(rb.linearVelocity.x, 0f);
            return;
        }

        strengthDropWaiting = false;
        RestoreStrengthGravity();
        player.SetVelocity(rb.linearVelocity.x, StrengthSlamSpeed);
        AirAttackTrace.Log(poseVisible ? "力量挥爪已画出，开始下砸" : "力量起手超时，开始下砸");
    }

    private void RestoreStrengthGravity()
    {
        if (!strengthGravityHeld)
            return;

        rb.gravityScale = strengthSavedGravity;
        strengthGravityHeld = false;
    }

    // WitchFrames 在 LateUpdate 里画。这里读到的是上一帧已经画出来的结果。
    private bool AirAttackPoseVisible()
    {
        WitchFrames frames = player.GetComponentInChildren<WitchFrames>(true);
        return frames != null && frames.AirAttackHoldVisible;
    }

    // 还在空中就回下落（画面仍按竖直速度播 jump 或 fall）。着地回待机，待机里再切跑步。
    private void ReleaseToMovement(bool landed, string reason)
    {
        AirAttackTrace.Log("退出跳跃攻击 " + reason
            + " 地面=" + player.groundDetected
            + " 支撑=" + player.supportDetected
            + " vy=" + rb.linearVelocity.y.ToString("0.00"));
        EndSwing();
        bool onGround = landed || player.groundDetected || player.supportDetected;
        stateMachine.ChangeState(onGround ? player.idleState : player.fallState);
    }

    private void UpdateHitWindow(bool poseVisible)
    {
        bool inWindow = !touchedGround && poseVisible;
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
        // 受伤、死亡、落地、冲刺打断都走这里。没下砸也要把重力还回去。
        strengthDropWaiting = false;
        RestoreStrengthGravity();
        player.EndAttackSwing();
    }
}