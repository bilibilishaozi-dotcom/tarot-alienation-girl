using UnityEngine;

public class Player_WallSlideState : EntityState
{
    public Player_WallSlideState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        // 按离墙方向 = 主动脱墙，立刻交回下落状态。
        // 以前这里只是把横向速度设成输入值，但 SetVelocity 会顺手翻转朝向，
        // 翻转之后「推向墙」的判定又成立，速度被清 0，人就卡在墙上了。
        float inputX = player.moveInput.x;
        bool pressingAway = Mathf.Abs(inputX) > 0.01f && Mathf.Sign(inputX) != player.facingDir;
        if (pressingAway)
        {
            player.ReleaseWall();
            stateMachine.ChangeState(player.fallState);
            return;
        }

        HandleWallSlide();

        if (input.Player.Jump.WasPressedThisFrame())
        {
            stateMachine.ChangeState(player.wallJumpState);
            return;
        }

        if (player.wallDetected == false)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        if (player.groundDetected)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

    }

    private void HandleWallSlide()
    {
        float verticalSpeed = player.moveInput.y < 0
            ? rb.linearVelocity.y
            : rb.linearVelocity.y * player.wallSlideSlowMultiplier;

        // 贴墙时横向锁死。这里必须用 0，不能传输入值：
        // 传输入值会触发 HandleFlip 改变朝向，贴墙判定就会跟着乱。
        player.SetVelocity(0f, verticalSpeed);
    }
}
