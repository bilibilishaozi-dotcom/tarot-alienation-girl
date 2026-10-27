using UnityEngine;

public class StateMachine
    //StateMachine：状态管理员
    //核心思想：“同一时间只能处于一个主状态”
    //它专门负责：当前状态是谁；如何切换状态；进入状态；离开状态
    //封装的作用为：可以在Unity属性栏中看到数值，但是只可以在原本代码中更改数值/作出修改
{
    public EntityState currentState { get; private set; }
    //声明一个类型是 EntityState 的变量 currentState 并且给 currentState 附上属性 外部可读取，不可修改
    //IdleState    RunState    JumpState    AttackState之类的状态在这里被调用


    public void Initialize(EntityState startState)
        //告知“currentState”起点在哪里
    {
        currentState = startState;
        //这里的startState，来源于 Player中的private void Start()
        //而IdleState继承于EntityState，需要再括号里写它的名字
        currentState.Enter();
        //将当前状态变量更改为另一个状态
    }

    public void ChangeState(EntityState newState)
    //用来切换状态：待机 → 跑步 跑步 → 跳跃 跳跃 → 攻击 等等
    {
        currentState.Exit();
        //离开当前状态
        currentState = newState;
        //当前状态 = 新状态
        currentState.Enter();
        //进入新状态
    }

    public void UpdateActiveState()
    {
        currentState.Update();
    }
}

