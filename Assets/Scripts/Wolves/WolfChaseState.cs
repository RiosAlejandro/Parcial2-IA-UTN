using UnityEngine;

public class WolfChaseState : WolfState
{
    public WolfChaseState(WolfAgent wolf, StateMachine stateMachine)
        : base(wolf, stateMachine, WolfStates.Chase) { }

    public override void Enter()
    {
        Wolf.ClearPath();
        Wolf.SetActivity("Persiguiendo");
        Wolf.ReportAction("Cazador detectado");
        Wolf.Howl();
    }

    public override void Exit()
    {
        Wolf.Stop();
    }

    public override void Update()
    {
        HunterAgent hunter = Wolf.DetectedHunter;

        if (hunter == null)
        {
            StateMachine.ChangeState(Wolf.HasLastKnown ? WolfStates.Investigate : WolfStates.Patrol);
            return;
        }

        if (Wolf.TryCapture(hunter))
        {
            StateMachine.ChangeState(WolfStates.Patrol);
            return;
        }

        if (Wolf.ShouldHowlAgain())
            Wolf.Howl();

        Wolf.MoveTowards(hunter.transform.position, Wolf.ChaseSpeed);
    }
}
