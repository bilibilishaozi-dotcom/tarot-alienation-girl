using UnityEngine;

public class Player_IdleState : Player_GroundedState
{
    public Player_IdleState(Player player, StateMachine stateMachine, string stateName) : base(player, stateMachine, stateName)
    //函数的书写方法从EntityState继承，其中状态机的调取写法是 用了StateMachine类 命名为 stateMachine
    //base（）函数的作用，将stateMachine, stateName这两个参数返还给父类也就是EntityState
    {

    }

    public override void Enter()
    {
        base.Enter();

        player.SetVelocity(0, rb.linearVelocity.y);
    }
    public override void Update()
    {
        base.Update();

        // 父类可能已经切走（下落、跳跃、攻击），这时不要再往下切。
        if (!stateMachine.IsActive(this))
            return;

        // 顶着墙站着时不要再切 move（否则 push 会一直抖）
        if (player.wallDetected && Mathf.Sign(player.moveInput.x) == player.facingDir && Mathf.Abs(player.moveInput.x) > 0.01f)
            return;

        if (player.moveInput.x != 0)
            stateMachine.ChangeState(player.moveState);
    }
}
