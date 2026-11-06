using UnityEngine;

public class WolfInvestigateState : WolfState
{
    private float _repathTimer;
    private bool _hasDestination;
    private Vector3 _destination;

    public WolfInvestigateState(WolfAgent wolf, StateMachine stateMachine)
        : base(wolf, stateMachine, WolfStates.Investigate) { }

    public override void Enter()
    {
        Wolf.ClearPath();
        Wolf.SetActivity("Yendo a la ultima posicion conocida");

        _repathTimer = 0f;
        _hasDestination = false;
    }

    public override void Exit()
    {
        Wolf.Stop();
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        if (!Wolf.HasLastKnown)
        {
            StateMachine.ChangeState(WolfStates.Patrol);
            return;
        }

        if (_hasDestination && _destination != Wolf.LastKnownPosition)
            _hasDestination = false;

        if (_hasDestination)
        {
            if (Wolf.FollowPath(Wolf.ChaseSpeed))
                StateMachine.ChangeState(WolfStates.Search);

            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f) return;

        _repathTimer = Wolf.RepathInterval;
        _destination = Wolf.LastKnownPosition;
        _hasDestination = Wolf.SetDestination(_destination);

        if (!_hasDestination && Wolf.Follower.Path.Count == 0)
            StateMachine.ChangeState(WolfStates.Search);
    }
}
