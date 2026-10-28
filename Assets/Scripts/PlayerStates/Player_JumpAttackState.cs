using UnityEngine;

public class Player_JumpAttackState : EntityState
{
    private const float HitStart = 0.06f;

    private bool touchedGround;
    private bool swingActive;
    private float elapsed;

    public Player_JumpAttackState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }
    public override void Enter()
    {
        base.Enter();
        touchedGround = false;
        swingActive = false;
        elapsed = 0f;
        player.basicAttackState.ResetCombo();

        float drop = player.HasLandingSlam ? -15f : player.jumpAttackVelocity.y;
        player.SetVelocity(player.jumpAttackVelocity.x * player.AttackLungeMultiplier * player.facingDir, drop);
    }
    public override void Update()
    {
        base.Update();
        if (stateMachine.currentState != this)
            return;

        elapsed += Time.deltaTime;
        UpdateHitWindow();

        if (player.groundDetected && touchedGround == false)
        {
            touchedGround = true;
            anim.SetTrigger("jumpAttackTrigger");
            player.SetVelocity(0, rb.linearVelocity.y);
            EndSwing();
            if (player.HasLandingSlam)
                player.PlayLandingSlam();
        }

        if (triggerCalled && player.groundDetected)
            stateMachine.ChangeState(player.idleState);
    }

    private void UpdateHitWindow()
    {
        bool inWindow = !touchedGround && elapsed >= HitStart;
        if (inWindow && !swingActive)
        {
            swingActive = true;
            player.BeginAttackSwing();
        }
        else if (!inWindow && swingActive)
        {
            EndSwing();
        }
    }

    private void EndSwing()
    {
        swingActive = false;
        player.EndAttackSwing();
    }

    public override void Exit()
    {
        base.Exit();
        player.EndAttackSwing();
    }
}