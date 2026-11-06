using UnityEngine;

public class HunterFleeState : HunterState
{
    private float _waitTimer;
    private float _repathTimer;
    private bool _arrived;
    private Vector3 _refuge;
    private bool _hasRefuge;

    public HunterFleeState(HunterAgent hunter, StateMachine stateMachine)
        : base(hunter, stateMachine, HunterStates.Flee) { }

    public override void Enter()
    {
        Hunter.SetTarget(null);
        Hunter.ClearPath();

        _arrived = false;
        _waitTimer = 0f;
        _repathTimer = 0f;
        _hasRefuge = ChooseRefuge();

        Hunter.SetActivity(_hasRefuge ? "Huyendo a zona segura" : "Sin zona segura alcanzable");
    }

    public override void Exit()
    {
        Hunter.Stop();
    }

    public override void Update()
    {
        if (TryTransitionToHigherPriority()) return;

        // Una vez a salvo se queda quieto contando, aunque el test de zona parpadee.
        if (_arrived)
        {
            WaitInsideRefuge();
            return;
        }

        if (Hunter.IsInZone(ZoneType.Safe))
        {
            ArriveAtRefuge();
            return;
        }

        Travel();
    }

    private void ArriveAtRefuge()
    {
        _arrived = true;
        _waitTimer = 0f;

        Hunter.Stop();
        Hunter.ClearPath();
        Hunter.ReportAction("A salvo en zona segura");
    }

    private void WaitInsideRefuge()
    {
        _waitTimer += Time.deltaTime;

        Hunter.SetActivity($"Esperando en zona segura ({Mathf.Max(0f, Hunter.SafeZoneWaitTime - _waitTimer):0.0}s)");

        if (_waitTimer < Hunter.SafeZoneWaitTime) return;

        StateMachine.ChangeState(Hunter.HasEnoughGathers ? HunterStates.Exit : HunterStates.Search);
    }

    private void Travel()
    {
        if (_hasRefuge)
        {
            if (Hunter.FollowPath())
                _hasRefuge = false;

            return;
        }

        _repathTimer -= Time.deltaTime;
        if (_repathTimer > 0f) return;

        _repathTimer = Hunter.RepathInterval;
        _hasRefuge = ChooseRefuge();
    }

    // Elige el refugio mas cercano por longitud de camino A*, no en linea recta.
    private bool ChooseRefuge()
    {
        if (!Hunter.SetDestinationToZone(ZoneType.Safe)) return false;

        _refuge = Hunter.Follower.Destination;
        Hunter.ReportAction($"Zona segura elegida (costo {Hunter.LastDestinationCost:0.0})");

        return true;
    }
}
