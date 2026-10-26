using UnityEngine;

public class Player_BasicAttackState : EntityState
{
    //攻击位移持续时间
    private float attackVelocityTimer;
    //上一次攻击时间
    private float lastTimeAttacked;

    private bool comboAttackQueued;
    private int attackDir;
    private int comboIndex = 1;
    private int comboLimit = 3;
    //const 代表：常量，永远不能改
    private const int FirstComboIndex = 1;

    public Player_BasicAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
        //连击数是否和攻击位移数组长度一致
        if (comboLimit != player.attackVelocity.Length)
        {
            Debug.LogWarning("我已经根据攻击速度数组调整了组合限制");
            comboLimit = player.attackVelocity.Length;
        }
    }

    public override void Enter()
    {
        base.Enter();
        comboAttackQueued = false;
        ResetComboIndexIfNeeded();

        //如果玩家正在按左右方向键，就朝输入方向冲刺；如果没有方向输入，就朝角色当前面朝方向冲刺。
        attackDir = player.moveInput.x != 0 ? ((int)player.moveInput.x) : player.facingDir;

        anim.SetInteger("basicAttackIndex", comboIndex);
        ApplyAttackVelocity();
        player.BeginAttackSwing();
    }



    public override void Update()
    {
        base.Update();
        HandleAttackVelocity();

        if (input.Player.Attack.WasPressedThisFrame())
            QueueNextAttack();

        if (triggerCalled)
            HandleStateExit();
    }

    public override void Exit()
    {
        base.Exit();
        player.EndAttackSwing();
        comboIndex++;
        lastTimeAttacked = Time.time;
    }

    public void HandleStateExit()
    {
        if (comboAttackQueued)
        {
            anim.SetBool(animBoolName, false);
            player.EnterAttackStateWithDelay();
        }
        else
            stateMachine.ChangeState(player.idleState);
    }

    private void QueueNextAttack()
    {
        if (comboIndex < comboLimit)
            comboAttackQueued = true;
    }

    private void HandleAttackVelocity()
    {
        attackVelocityTimer -= Time.deltaTime;

        if (attackVelocityTimer < 0)
            player.SetVelocity(0, rb.linearVelocity.y);
    }

    private void ApplyAttackVelocity()
    {
        Vector2 attackVelocity = player.attackVelocity[comboIndex - 1];
        attackVelocity.x *= player.AttackLungeMultiplier;

        attackVelocityTimer = player.attackVelocityDuration;
        player.SetVelocity(attackVelocity.x * attackDir, attackVelocity.y);
    }

    private void ResetComboIndexIfNeeded()
    {
        if (Time.time > lastTimeAttacked + player.comboResetTime)
            comboIndex = FirstComboIndex;

        if (comboIndex > comboLimit)
            comboIndex = FirstComboIndex;
    }
}
