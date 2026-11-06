public abstract class HunterState : State
{
    protected readonly HunterAgent Hunter;

    private readonly HunterStates _key;

    protected HunterState(HunterAgent hunter, StateMachine stateMachine, HunterStates key) : base(stateMachine)
    {
        Hunter = hunter;
        _key = key;
    }

    // Prioridad Dead > Flee > Exit > Gather > Attack > Search: el enum va de mayor a menor.
    protected bool TryTransitionToHigherPriority()
    {
        if (IsAbove(HunterStates.Dead) && Hunter.IsDowned)
            return Switch(HunterStates.Dead);

        if (IsAbove(HunterStates.Flee) && Hunter.WolfDetected)
            return Switch(HunterStates.Flee);

        if (IsAbove(HunterStates.Exit) && Hunter.HasEnoughGathers)
            return Switch(HunterStates.Exit);

        if (IsAbove(HunterStates.Gather) && Hunter.FindNearestDeadBoid() != null)
            return Switch(HunterStates.Gather);

        if (IsAbove(HunterStates.Attack) && Hunter.IsAttackReady && Hunter.FindNearestAliveBoid() != null)
            return Switch(HunterStates.Attack);

        return false;
    }

    private bool IsAbove(HunterStates other) => _key > other;

    private bool Switch(HunterStates next)
    {
        StateMachine.ChangeState(next);
        return true;
    }
}
