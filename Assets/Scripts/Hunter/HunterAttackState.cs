using UnityEngine;

public class HunterAttackState : HunterState
{
    private BoidAgent _target;

    public HunterAttackState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Attack) { }

    public override void Enter()
    {
        _target = Hunter.Prey != null ? Hunter.Prey : Hunter.FindNearestAliveBoid();

        if (_target != null)
            Hunter.SetPrey(_target);

        Hunter.SetTarget(_target);
        Hunter.ClearPath();
        Hunter.SetActivity("Buscando rango de ataque");
    }

    public override void Exit()
    {
        Hunter.Stop();
        Hunter.SetTarget(null);
        _target = null;
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        if (_target == null || !_target.IsAlive)
        {
            StateMachine.ChangeState(HunterStates.Search);
            return;
        }

        float distance = Hunter.HorizontalDistanceTo(_target.transform.position);

        // Si pierde de vista al objetivo vuelve a Search sin reiniciar el TBA.
        if (distance > Hunter.VisionRadius || !Hunter.HasLineOfSightTo(_target.transform.position))
        {
            StateMachine.ChangeState(HunterStates.Search);
            return;
        }

        if (distance <= Hunter.MeleeAttackRadius)
        {
            Hunter.MeleeAttack(_target);
            FinishAttack();
            return;
        }

        if (distance <= Hunter.RangeAttackRadius)
        {
            Hunter.RangedAttack(_target);
            FinishAttack();
            return;
        }

        Hunter.SetActivity("Pursuit al boid");
        Hunter.Pursue(_target);
    }

    private void FinishAttack()
    {
        Hunter.ResetAttackTimer();
        StateMachine.ChangeState(HunterStates.Search);
    }
}
