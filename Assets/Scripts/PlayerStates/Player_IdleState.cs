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

        if (player.moveInput.x == player.facingDir && player.wallDetected)
            return;

        if (player.moveInput.x != 0) 
            stateMachine.ChangeState(player.moveState);
    }
}
