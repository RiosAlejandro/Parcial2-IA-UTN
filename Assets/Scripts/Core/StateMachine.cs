using System.Collections.Generic;
using System;

public class StateMachine
{
    public event Action<Enum> StateChanged;

    public State CurrentState { get; private set; }
    public Enum CurrentKey { get; private set; }

    private Dictionary<Enum, State> states = new Dictionary<Enum, State>();

    public void RegisterState(Enum key, State state)
    {
        states[key] = state;
    }

    public void ChangeState(Enum key)
    {
        if (!states.TryGetValue(key, out State newState))
            return;

        if (newState == CurrentState)
            return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentKey = key;
        StateChanged?.Invoke(key);
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}
