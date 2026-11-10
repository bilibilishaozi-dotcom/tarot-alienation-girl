using UnityEngine;

public class Player_GroundedState : EntityState
{
    public Player_GroundedState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.RestoreFoolLeap();
        // 真正落地才把空中攻击还回来。下落、二段跳、贴墙都不走这里。
        player.RestoreAirAttack();
        player.ClearAirAttackArm();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        // 站在敌人头上 / 箱子上时 groundDetected 是 false，但 supportDetected 是 true，
        // 这时候不该切回下落，否则会一直悬在半空抖。
        if (rb.linearVelocity.y < 0 && player.groundDetected == false && player.supportDetected == false)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        // 跳和攻击同一帧或紧邻的一帧：先跳，攻击留到离地后打。不要在地上把这次攻击打出去。
        if ((player.groundDetected || player.supportDetected) && player.JumpPressedRecently)
        {
            if (player.AttackPressedRecently)
                player.ArmAirAttack();
            player.ConsumeJumpPress();
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        // 按下的这一帧先等跳跃。下一帧还没有跳，才是地面攻击。
        if (player.AttackPressedRecently && player.AttackPressAge >= 1)
        {
            player.ConsumeAttackPress();
            stateMachine.ChangeState(player.basicAttackState);
        }
    }
}
