using UnityEngine;

public class Player_GroundedState : EntityState
{
    public Player_GroundedState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        if (rb.linearVelocity.y < 0 && player.groundDetected == false)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        if (player.groundDetected && input.Player.Jump.WasPerformedThisFrame())
        {
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (input.Player.Attack.WasPerformedThisFrame())
            stateMachine.ChangeState(player.basicAttackState);
    }
}
