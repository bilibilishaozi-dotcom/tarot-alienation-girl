using UnityEngine;

public class Player_JumpAttackState : EntityState
{
    private bool touchedGround;
    private bool swingActive;
    private bool hurtOnEnter;

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

        float drop = player.HasLandingSlam ? -15f : player.jumpAttackVelocity.y;
        player.SetVelocity(player.jumpAttackVelocity.x * player.AttackLungeMultiplier * player.facingDir, drop);
    }
    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        bool poseVisible = AirAttackPoseVisible();
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
        player.EndAttackSwing();
    }
}