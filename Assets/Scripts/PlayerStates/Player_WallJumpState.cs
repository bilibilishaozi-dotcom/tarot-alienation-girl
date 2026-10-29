using UnityEngine;

public class Player_WallJumpState : EntityState
{
    public Player_WallJumpState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }


    public override void Enter()
    {
        base.Enter();
        stateTimer = 0.12f;
        // 蹬出去之后短时间内别再被同一面墙吸回去
        player.ReleaseWall(0.3f);
        player.SetVelocity(player.wallJumpForce.x * -player.facingDir, player.wallJumpForce.y);
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        if (rb.linearVelocity.y < 0)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        if (stateTimer < 0f && player.wallLatched)
            stateMachine.ChangeState(player.wallSlideState);
    }
}
