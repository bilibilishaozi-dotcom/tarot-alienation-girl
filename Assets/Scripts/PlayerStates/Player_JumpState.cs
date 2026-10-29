using UnityEngine;

public class Player_JumpState : Player_AiredState
{
    public Player_JumpState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.SetVelocity(rb.linearVelocity.x, player.jumpForce);


    }

    public override void Update()
    {
        base.Update();

        // 父类可能已经切走（冲刺、空中攻击），这时不能再往下切第二次。
        if (!stateMachine.IsActive(this))
            return;

        if (rb.linearVelocity.y < 0f)
            stateMachine.ChangeState(player.fallState);
    }
}
