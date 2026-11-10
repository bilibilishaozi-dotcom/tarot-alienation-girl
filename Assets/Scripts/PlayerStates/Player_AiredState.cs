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

        // 装备了愚者牌才多一次空中跳。没装备时 TryFoolLeap 直接失败，这里不会改速度。
        if (input.Player.Jump.WasPressedThisFrame() && player.TryFoolLeap())
        {
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        // 按下沿只在按下的那一帧为真，按住不会连进。同一滞空用掉后，再按也不再进。
        if (input.Player.Attack.WasPressedThisFrame())
        {
            if (player.CanAirAttack)
            {
                player.SpendAirAttack();
                stateMachine.ChangeState(player.jumpAttackState);
            }
            return;
        }
    }
}
