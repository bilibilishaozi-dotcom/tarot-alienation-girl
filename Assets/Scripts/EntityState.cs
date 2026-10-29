using UnityEngine;

public abstract class EntityState
//abstract的作用：EntityState 是一个“基础模板类”，不能直接 new 出来用，只能让别的类继承它。
{
    protected Player player;
    protected StateMachine stateMachine;
    protected string animBoolName;

    protected Animator anim;
    protected Rigidbody2D rb;
    protected PlayerInputSet input;

    protected float stateTimer;
    protected bool triggerCalled;

    public EntityState(Player player, StateMachine stateMachine, string animBoolName)
    //全局都可调用的“状态机”，需要在此处进行初始化
    {
        this.player = player;
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
        //把外部传进来的 stateMachine
        //保存到 当前对象自己的 stateMachine 里

        anim = player.anim;
        rb = player.rb;
        input = player.input;
    }

    public virtual void Enter()
    //此处的“virtual”代表在父类中可被调用重写的函数、相对应的子类中用“overrid”
    {
        //改变状态时，将调用此方法，为了初始化状态的默认设置
        //其他脚本里面每次调用这个脚本中的Enter时，会base.Enter，代表先执行一遍此脚本中的Enter一次
        //代码的含义是：进入状态时开启动画，并且重置动画tigger状态转换成为false，避免上一状态残留
        //当其他脚本调用Enter时，会通过Player脚本更改已经定义好的名称，并传输到这里，此时开始运行一次想要执行的动画
        //如果“move”是“true”状态，则切换到“move”动画
        anim.SetBool(animBoolName, true);
        triggerCalled = false;

    }

    public virtual void Update()
    {
        //例如进入“Dash” stateTimer = 0.2f 然后随着时间进行减少
        stateTimer -= Time.deltaTime;
        //在此运行状态的逻辑
        anim.SetFloat("yVelocity", rb.linearVelocity.y);

        //为什么Dash检测写在父类里：所有的状态下，都允许冲刺
        //如果某个状态不允许Dash怎么办：这里面就会“override Update”
        if (input.Player.Dash.WasPressedThisFrame() && CanDash())
            stateMachine.ChangeState(player.dashState);
    }

    public virtual void Exit()
    {
        //每次退出状态并切换到新状态时都会调用此方法
        anim.SetBool(animBoolName, false);
    }

    public void CallAnimationTrigger()
    {
        //可以让代码知道动画播完了，此方法在需要的动画的最后一帧被调用
        triggerCalled = true;
    }

    private bool CanDash()
    {
        // 只在真正贴着墙滑的时候禁掉冲刺；站在墙边（12cm 内）不该禁，
        // 否则“贴着墙往前跑 + 冲刺躲开”这种正常操作会被吃掉。
        if (stateMachine.currentState == player.wallSlideState)
            return false;
        if (stateMachine.currentState == player.dashState)
            return false;

        return true;
    }
}
