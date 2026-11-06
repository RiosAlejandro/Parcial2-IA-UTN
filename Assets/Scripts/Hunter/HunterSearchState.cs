using UnityEngine;

public class HunterSearchState : HunterState
{
    private float _repathTimer;
    private bool _hasDestination;

    public HunterSearchState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Search) { }

    public override void Enter()
    {
        Hunter.SetTarget(null);
        Hunter.ClearPath();

        _hasDestination = false;
        _repathTimer = 0f;
    }

    public override void Exit()
    {
        Hunter.Stop();
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        bool insideCoto = Hunter.IsInZone(ZoneType.Hunt);

        Hunter.SetActivity(insideCoto ? "Recorriendo el Coto" : "Yendo al Coto por A*");

        if (insideCoto)
            Hunter.TrySpawnPointOfInterest();

        UpdateNavigation(insideCoto);
    }

    private void UpdateNavigation(bool insideCoto)
    {
        if (_hasDestination)
        {
            if (Hunter.FollowPath())
                _hasDestination = false;

            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f) return;

        _repathTimer = Hunter.RepathInterval;
        _hasDestination = ChooseDestination(insideCoto);
    }

    private bool ChooseDestination(bool insideCoto)
    {
        if (!insideCoto)
            return Hunter.SetDestinationToZone(ZoneType.Hunt);

        return Hunter.TryGetRandomZoneNode(ZoneType.Hunt, out Vector3 destination)
            && Hunter.SetDestination(destination);
    }
}
