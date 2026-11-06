using UnityEngine;

public class WolfPatrolState : WolfState
{
    private float _repathTimer;
    private bool _usingPath;

    public WolfPatrolState(WolfAgent wolf, StateMachine stateMachine)
        : base(wolf, stateMachine, WolfStates.Patrol) { }

    public override void Enter()
    {
        Wolf.ClearPath();
        Wolf.SetActivity("Patrullando");

        _usingPath = false;
        _repathTimer = 0f;
    }

    public override void Exit()
    {
        Wolf.Stop();
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        Transform waypoint = Wolf.CurrentWaypoint;

        if (waypoint == null)
        {
            Wolf.Stop();
            return;
        }

        if (Wolf.HorizontalDistanceTo(waypoint.position) <= Wolf.WaypointRadius)
        {
            Wolf.AdvanceWaypoint();
            Wolf.ClearPath();
            _usingPath = false;
            return;
        }

        if (Wolf.HasLineOfSightTo(waypoint.position))
        {
            if (_usingPath)
            {
                Wolf.ClearPath();
                _usingPath = false;
            }

            Wolf.SetActivity("Patrullando");
            Wolf.MoveTowards(waypoint.position, Wolf.PatrolSpeed);
            return;
        }

        // Sin linea de vista al waypoint, se va por A*.
        FollowPathToWaypoint(waypoint);
    }

    private void FollowPathToWaypoint(Transform waypoint)
    {
        Wolf.SetActivity("Patrullando con A*");

        if (_usingPath)
        {
            if (Wolf.FollowPath(Wolf.PatrolSpeed))
                _usingPath = false;

            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f) return;

        _repathTimer = Wolf.RepathInterval;
        _usingPath = Wolf.SetDestination(waypoint.position);
    }
}
