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

        // 站在敌人头上 / 箱子上时 groundDetected 是 false，但 supportDetected 是 true，
        // 这时候不该切回下落，否则会一直悬在半空抖。
        if (rb.linearVelocity.y < 0 && player.groundDetected == false && player.supportDetected == false)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        if ((player.groundDetected || player.supportDetected) && input.Player.Jump.WasPerformedThisFrame())
        {
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (input.Player.Attack.WasPerformedThisFrame())
            stateMachine.ChangeState(player.basicAttackState);
    }
}
