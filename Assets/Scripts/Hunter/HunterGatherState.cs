using UnityEngine;

public class HunterGatherState : HunterState
{
    private BoidAgent _target;
    private float _gatherTimer;
    private bool _isGathering;

    public HunterGatherState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Gather) { }

    public override void Enter()
    {
        _target = Hunter.FindNearestDeadBoid();
        _gatherTimer = 0f;
        _isGathering = false;

        Hunter.SetTarget(_target);
        Hunter.ClearPath();
        Hunter.SetActivity("Yendo al boid eliminado");
    }

    public override void Exit()
    {
        Hunter.Stop();
        Hunter.SetTarget(null);
        _target = null;
        _isGathering = false;
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        if (_target == null || !_target.IsDead)
        {
            StateMachine.ChangeState(HunterStates.Search);
            return;
        }

        if (!_isGathering)
        {
            Approach();
            return;
        }

        _gatherTimer += Time.deltaTime;

        if (_gatherTimer < Hunter.GatherDuration) return;

        Collect();
    }

    private void Approach()
    {
        float distance = Hunter.HorizontalDistanceTo(_target.transform.position);

        if (distance <= Hunter.GatherRadius)
        {
            _isGathering = true;
            _gatherTimer = 0f;
            Hunter.Stop();
            Hunter.SetActivity("Recolectando");
            Hunter.ReportAction("Recoleccion iniciada");
            return;
        }

        if (Hunter.HasLineOfSightTo(_target.transform.position))
        {
            Hunter.ClearPath();
            Hunter.MoveTowards(_target.transform.position);
            return;
        }

        if (!Hunter.Follower.HasPath)
            Hunter.SetDestination(_target.transform.position);

        Hunter.FollowPath();
    }

    private void Collect()
    {
        _target.Gather();
        Hunter.AddGather();

        StateMachine.ChangeState(Hunter.HasEnoughGathers ? HunterStates.Exit : HunterStates.Search);
    }
}
