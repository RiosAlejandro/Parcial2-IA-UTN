using UnityEngine;

public class WolfSearchState : WolfState
{
    private float _timer;
    private float _repathTimer;
    private bool _hasDestination;
    private Vector3 _center;

    public WolfSearchState(WolfAgent wolf, StateMachine stateMachine)
        : base(wolf, stateMachine, WolfStates.Search) { }

    public override void Enter()
    {
        _center = Wolf.LastKnownPosition;
        _timer = 0f;
        _repathTimer = 0f;
        _hasDestination = false;

        // Consume la posicion conocida: sin esto rebota entre Investigate y Search para siempre.
        Wolf.ClearLastKnown();
        Wolf.ClearPath();
        Wolf.SetActivity("Buscando en la zona");
        Wolf.ReportAction("Sin rastro: busca por la zona");
    }

    public override void Exit()
    {
        Wolf.Stop();
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        _timer += Time.deltaTime;

        if (_timer >= Wolf.SearchDuration)
        {
            StateMachine.ChangeState(WolfStates.Patrol);
            return;
        }

        if (_hasDestination)
        {
            if (Wolf.FollowPath(Wolf.PatrolSpeed))
                _hasDestination = false;

            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f) return;

        _repathTimer = Wolf.RepathInterval;
        _hasDestination = Wolf.SetDestination(Wolf.RandomPointNear(_center));
    }
}
