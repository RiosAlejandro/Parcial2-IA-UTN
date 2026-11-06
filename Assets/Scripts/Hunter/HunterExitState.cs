using UnityEngine;

public class HunterExitState : HunterState
{
    private float _repathTimer;
    private float _respawnTimer;
    private bool _hasDestination;
    private bool _completed;

    public HunterExitState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Exit) { }

    public override void Enter()
    {
        Hunter.SetTarget(null);
        Hunter.ClearPath();
        Hunter.SetActivity("Yendo a la Salida");

        _completed = false;
        _respawnTimer = 0f;
        _repathTimer = 0f;
        _hasDestination = ChooseExit();
    }

    public override void Exit()
    {
        Hunter.Stop();
    }

    public override void Update()
    {
        if (_completed)
        {
            WaitForRespawn();
            return;
        }

        if (TryTransitionToHigherPriority()) return;

        if (Hunter.IsInZone(ZoneType.Exit))
        {
            Complete();
            return;
        }

        Travel();
    }

    private void Travel()
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
        _hasDestination = ChooseExit();
    }

    private bool ChooseExit() => Hunter.SetDestinationToZone(ZoneType.Exit);

    private void Complete()
    {
        _completed = true;
        _respawnTimer = 0f;

        Hunter.Stop();
        Hunter.ClearPath();
        Hunter.SetVisible(false);
        Hunter.SetActivity("Objetivo cumplido");
        Hunter.ReportAction("Llego a la Salida: objetivo cumplido");
    }

    private void WaitForRespawn()
    {
        _respawnTimer += Time.deltaTime;

        if (_respawnTimer < Hunter.RespawnTime) return;

        Hunter.RespawnAtStart();
        Hunter.SetVisible(true);

        StateMachine.ChangeState(HunterStates.Search);
    }
}
