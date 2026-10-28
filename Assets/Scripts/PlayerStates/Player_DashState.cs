using UnityEngine;

public class Player_DashState : EntityState
{

    private float originalGravtiyScale;
    private int dashDir;
    private ChariotEcho echo;
    public Player_DashState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Enter()
    {
        base.Enter();

        //如果玩家正在按左右方向键，就朝输入方向冲刺；如果没有方向输入，就朝角色当前面朝方向冲刺。
        dashDir = player.moveInput.x != 0 ? ((int)player.moveInput.x) : player.facingDir;
        stateTimer = player.dashDuration;

        originalGravtiyScale = rb.gravityScale;
        rb.gravityScale = 0;
        player.BeginDodge();
        if (player.HasChariot)
            echo = ChariotEcho.Spawn(player, dashDir);
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        UpdateDashDirection();

        if (player.IsSolidAhead(dashDir))
        {
            StopDash();
            return;
        }

        player.SetVelocity(player.dashSpeed * dashDir, 0);
        if (echo != null)
            echo.Tick(dashDir);

        if (stateTimer < 0)
            StopDash();
    }

    public override void Exit()
    {
        base.Exit();
        player.SetVelocity(0, 0);
        rb.gravityScale = originalGravtiyScale;
        player.EndDodge();
        if (echo != null)
        {
            echo.End();
            echo = null;
        }
    }

    private void UpdateDashDirection()
    {
        if (Mathf.Abs(player.moveInput.x) < 0.01f)
            return;

        dashDir = player.moveInput.x > 0f ? 1 : -1;
    }

    private void StopDash()
    {
        if (player.groundDetected)
            stateMachine.ChangeState(player.idleState);
        else if (player.wallDetected)
            stateMachine.ChangeState(player.wallSlideState);
        else
            stateMachine.ChangeState(player.fallState);
    }
}
