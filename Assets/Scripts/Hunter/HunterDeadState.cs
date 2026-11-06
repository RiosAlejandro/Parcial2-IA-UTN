using UnityEngine;

public class HunterDeadState : HunterState
{
    private float _timer;

    public HunterDeadState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Dead) { }

    public override void Enter()
    {
        _timer = 0f;

        Hunter.Stop();
        Hunter.ClearPath();
        Hunter.SetTarget(null);
        Hunter.SetVisible(false);
        Hunter.SetActivity("Eliminado");
    }

    public override void Exit()
    {
        Hunter.SetVisible(true);
    }

    public override void Update()
    {
        _timer += Time.deltaTime;

        if (_timer < Hunter.RespawnTime) return;

        Hunter.RespawnAtStart();
        StateMachine.ChangeState(HunterStates.Search);
    }
}
