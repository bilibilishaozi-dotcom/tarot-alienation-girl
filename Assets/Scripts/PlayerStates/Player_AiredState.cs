using UnityEngine;

public class Player_AiredState : EntityState
{
    public Player_AiredState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Update()
    {
        base.Update();

        if (player.moveInput.x != 0)
            player.SetVelocity(player.moveInput.x * (player.moveSpeed * player.inAirMoveMultiplier), rb.linearVelocity.y);

        // 愚者形态多一次空中跳。原版 TryFoolLeap 直接失败，这里不会改速度。
        if (input.Player.Jump.WasPressedThisFrame() && player.TryFoolLeap())
        {
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (input.Player.Attack.WasPressedThisFrame())
            stateMachine.ChangeState(player.jumpAttackState);
    }
}
