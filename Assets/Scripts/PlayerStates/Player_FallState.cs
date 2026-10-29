using UnityEngine;

public class Player_FallState : Player_AiredState
{
    public Player_FallState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        // 父类可能已经切走（冲刺、空中攻击），这时不能再往下切第二次。
        if (!stateMachine.IsActive(this))
            return;

        if (player.groundDetected || (player.supportDetected && rb.linearVelocity.y <= 0.05f))
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        if (player.wallDetected)
            stateMachine.ChangeState(player.wallSlideState);
    }
}
