using UnityEngine;

public class Player_MoveState : Player_GroundedState
{
    public Player_MoveState(Player player, StateMachine stateMachine, string stateName) : base(player, stateMachine, stateName)
    {
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        if (player.moveInput.x == 0)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        // 只有「朝墙的方向推」才停下来。
        // 原来写的是只要 wallDetected 就切 idle，而贴墙判定现在有 12cm 的余量 + 0.08 秒粘性，
        // 站在墙边按反方向会在 Move ↔ Idle 之间每帧来回切，看起来就是疯狂抽搐。
        bool pushingIntoWall = Mathf.Sign(player.moveInput.x) == player.facingDir && player.wallDetected;
        if (pushingIntoWall)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.SetVelocity(player.moveInput.x * player.moveSpeed, rb.linearVelocity.y);
    }
}
