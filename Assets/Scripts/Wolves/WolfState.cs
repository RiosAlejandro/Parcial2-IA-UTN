public abstract class WolfState : State
{
    protected readonly WolfAgent Wolf;

    private readonly WolfStates _key;

    protected WolfState(WolfAgent wolf, StateMachine stateMachine, WolfStates key) : base(stateMachine)
    {
        Wolf = wolf;
        _key = key;
    }

    protected bool TryTransitionToHigherPriority()
    {
        if (IsAbove(WolfStates.Chase) && Wolf.DetectedHunter != null)
            return Switch(WolfStates.Chase);

        if (IsAbove(WolfStates.Investigate) && Wolf.HasLastKnown)
            return Switch(WolfStates.Investigate);

        return false;
    }

    private bool IsAbove(WolfStates other) => _key > other;

    private bool Switch(WolfStates next)
    {
        StateMachine.ChangeState(next);
        return true;
    }
}
