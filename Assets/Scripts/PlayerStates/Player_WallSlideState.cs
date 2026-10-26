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
            player.Flip();
        }

    }

    private void HandleWallSlide()
    {
        float verticalSpeed = player.moveInput.y < 0
            ? rb.linearVelocity.y
            : rb.linearVelocity.y * player.wallSlideSlowMultiplier;

        float inputX = player.moveInput.x;
        bool pushingIntoWall = Mathf.Abs(inputX) > 0.01f && Mathf.Sign(inputX) == player.facingDir;
        player.SetVelocity(pushingIntoWall ? 0f : inputX, verticalSpeed);


    }
}
