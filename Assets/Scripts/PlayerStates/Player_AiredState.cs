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

        if (player.AttackPressedRecently)
            player.ArmAirAttack();

        // 还贴着地时只挂起，不消耗次数。离地后才进入一次。按住不会连进。
        if (!player.AirAttackArmed)
            return;

        if (!player.CanAirAttack)
        {
            AirAttackTrace.Log("拒绝空中攻击 这一跳已经用过");
            player.ClearAirAttackArm();
            player.ConsumeAttackPress();
            return;
        }

        if (player.groundDetected)
            return;

        player.ClearAirAttackArm();
        player.ConsumeAttackPress();
        player.SpendAirAttack();
        AirAttackTrace.Log("接受空中攻击");
        stateMachine.ChangeState(player.jumpAttackState);
    }
}
