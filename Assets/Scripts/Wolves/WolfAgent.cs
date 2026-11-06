using System.Collections.Generic;
using UnityEngine;

public enum WolfStates
{
    Chase = 0,
    Investigate = 1,
    Search = 2,
    Patrol = 3
}

public class WolfAgent : Agent
{
    [Header("Navegacion")]
    [SerializeField] private NavGraph _graph;
    [SerializeField] private float _patrolSpeed = 3f;
    [SerializeField] private float _chaseSpeed = 4.5f;
    [SerializeField] private float _waypointRadius = 0.7f;
    [SerializeField] private float _repathInterval = 0.5f;

    [Header("Patrulla")]
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private bool _pingPong = true;

    [Header("Percepcion")]
    [SerializeField] private FieldOfViewSensor _sensor;
    [SerializeField] private LayerMask _obstacleMask = 1 << 8;

    [Header("Alerta")]
    [SerializeField] private float _howlRadius = 28f;
    [SerializeField] private float _howlInterval = 2.5f;
    [SerializeField] private LayerMask _wolfMask = 1 << 9;

    [Header("Captura")]
    [SerializeField] private float _captureRadius = 1.6f;

    [Header("Busqueda")]
    [SerializeField] private float _searchDuration = 5f;
    [SerializeField] private float _searchRadius = 6f;

    [Header("Registro")]
    [SerializeField] private int _actionLogSize = 5;

    private const int BufferSize = 16;

    private readonly Collider[] _overlapBuffer = new Collider[BufferSize];
    private readonly List<Vector3> _pathBuffer = new List<Vector3>();
    private readonly List<string> _actionLog = new List<string>();

    private StateMachine _stateMachine;
    private AStar _pathfinder;
    private PathFollower _follower;

    private HunterAgent _detectedHunter;
    private Vector3 _lastKnownPosition;
    private float _lastHowlTime = -99f;
    private float _lastAlertTime = -99f;
    private int _currentWaypoint;
    private int _direction = 1;

    public float PatrolSpeed => _patrolSpeed;
    public float ChaseSpeed => _chaseSpeed;
    public float WaypointRadius => _waypointRadius;
    public float RepathInterval => _repathInterval;
    public float CaptureRadius => _captureRadius;
    public float SearchDuration => _searchDuration;
    public float SearchRadius => _searchRadius;
    public float HowlInterval => _howlInterval;
    public float HowlRadius => _howlRadius;
    public NavGraph Graph => _graph;
    public FieldOfViewSensor Sensor => _sensor;

    public HunterAgent DetectedHunter => _detectedHunter;
    public bool HasLastKnown { get; private set; }
    public Vector3 LastKnownPosition => _lastKnownPosition;
    public PathFollower Follower => _follower;
    public string Activity { get; private set; } = "Patrullando";
    public IReadOnlyList<string> ActionLog => _actionLog;
    public float TimeSinceHowl => Time.time - _lastHowlTime;
    public float TimeSinceAlert => Time.time - _lastAlertTime;

    public WolfStates CurrentState => _stateMachine != null && _stateMachine.CurrentKey is WolfStates key
        ? key
        : WolfStates.Patrol;

    // Los lobos no pueden navegar por las Zonas seguras.
    public static bool NavigationFilter(NavNode node) => node.zone != ZoneType.Safe;

    private void Awake()
    {
        if (_graph == null) _graph = FindFirstObjectByType<NavGraph>();
        if (_sensor == null) _sensor = GetComponent<FieldOfViewSensor>();

        _pathfinder = new AStar(_graph);
        _follower = new PathFollower();

        _stateMachine = new StateMachine();
        _stateMachine.RegisterState(WolfStates.Patrol, new WolfPatrolState(this, _stateMachine));
        _stateMachine.RegisterState(WolfStates.Chase, new WolfChaseState(this, _stateMachine));
        _stateMachine.RegisterState(WolfStates.Investigate, new WolfInvestigateState(this, _stateMachine));
        _stateMachine.RegisterState(WolfStates.Search, new WolfSearchState(this, _stateMachine));

        _stateMachine.StateChanged += key => ReportAction($"Estado -> {key}");
        _stateMachine.ChangeState(WolfStates.Patrol);
    }

    private void Update()
    {
        UpdatePerception();
        _stateMachine.Update();
    }

    private void UpdatePerception()
    {
        _detectedHunter = null;

        if (_sensor == null || !_sensor.CanSeeTarget || _sensor.Target == null) return;
        if (!_sensor.Target.TryGetComponent(out HunterAgent hunter)) return;

        if (hunter.IsInZone(ZoneType.Safe))
        {
            if (HasLastKnown)
            {
                ClearLastKnown();
                ReportAction("Objetivo a salvo: se descarta la posicion");
            }

            return;
        }

        _detectedHunter = hunter;
        SetLastKnown(hunter.transform.position);
    }

    public void SetLastKnown(Vector3 position)
    {
        _lastKnownPosition = position;
        HasLastKnown = true;
    }

    public void ClearLastKnown() => HasLastKnown = false;

    public float HorizontalDistanceTo(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;

        return offset.magnitude;
    }

    public bool HasLineOfSightTo(Vector3 point)
    {
        Vector3 origin = transform.position + Vector3.up;
        Vector3 target = point + Vector3.up;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon) return true;

        return !Physics.Raycast(origin, direction / distance, distance, _obstacleMask);
    }

    // Pasa la posicion por valor: es la ultima conocida, no una referencia que se siga actualizando.
    public void Howl()
    {
        _lastHowlTime = Time.time;

        int found = Physics.OverlapSphereNonAlloc(transform.position, _howlRadius, _overlapBuffer, _wolfMask);
        int reached = 0;

        for (int i = 0; i < found; i++)
        {
            if (!_overlapBuffer[i].TryGetComponent(out WolfAgent other)) continue;
            if (other == this) continue;

            other.ReceiveAlert(_lastKnownPosition);
            reached++;
        }

        ReportAction($"Aullido: {reached} lobos alertados");
    }

    public void ReceiveAlert(Vector3 position)
    {
        _lastAlertTime = Time.time;
        SetLastKnown(position);

        ReportAction("Alerta recibida");
    }

    public bool ShouldHowlAgain() => TimeSinceHowl >= _howlInterval;

    public bool SetDestination(Vector3 destination)
    {
        if (_graph == null) return false;

        if (!_pathfinder.TryFindPath(transform.position, destination, _pathBuffer, NavigationFilter))
        {
            _follower.Clear();
            return false;
        }

        _follower.SetPath(_pathBuffer);
        return true;
    }

    public void ClearPath() => _follower.Clear();

    public bool FollowPath(float speed)
    {
        if (!_follower.HasPath)
        {
            Stop();
            return true;
        }

        _follower.Smooth(_graph, transform.position);

        if (_follower.Advance(transform.position, _waypointRadius))
        {
            Stop();
            return true;
        }

        MoveTowards(_follower.CurrentWaypoint, speed);
        return false;
    }

    public void MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }

        direction.Normalize();
        _velocity = direction * speed;

        transform.position += _velocity * Time.deltaTime;
        transform.forward = direction;
    }

    public void Stop() => _velocity = Vector3.zero;

    public bool HasWaypoints => _waypoints != null && _waypoints.Length > 0;

    public Transform CurrentWaypoint =>
        HasWaypoints ? _waypoints[Mathf.Clamp(_currentWaypoint, 0, _waypoints.Length - 1)] : null;

    public void AdvanceWaypoint()
    {
        if (!HasWaypoints || _waypoints.Length < 2) return;

        if (!_pingPong)
        {
            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            return;
        }

        _currentWaypoint += _direction;

        if (_currentWaypoint >= _waypoints.Length)
        {
            _currentWaypoint = _waypoints.Length - 2;
            _direction = -1;
        }
        else if (_currentWaypoint < 0)
        {
            _currentWaypoint = 1;
            _direction = 1;
        }
    }

    public bool TryCapture(HunterAgent hunter)
    {
        if (hunter == null) return false;
        if (HorizontalDistanceTo(hunter.transform.position) > _captureRadius) return false;

        hunter.Kill();
        ClearLastKnown();
        ReportAction("Cazador alcanzado");

        return true;
    }

    public Vector3 RandomPointNear(Vector3 center)
    {
        Vector2 offset = Random.insideUnitCircle * _searchRadius;

        return new Vector3(center.x + offset.x, center.y, center.z + offset.y);
    }

    public void SetActivity(string activity) => Activity = activity;

    public void ReportAction(string action)
    {
        _actionLog.Insert(0, $"{Time.timeSinceLevelLoad:0.0}s  {action}");

        if (_actionLog.Count > _actionLogSize)
            _actionLog.RemoveAt(_actionLog.Count - 1);
    }

    private void OnDrawGizmos()
    {
        if (!HasWaypoints) return;

        Gizmos.color = new Color(0.5f, 0.5f, 0.55f, 0.6f);

        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null) continue;

            Gizmos.DrawSphere(_waypoints[i].position, 0.25f);

            Transform next = _waypoints[(i + 1) % _waypoints.Length];
            if (next != null) Gizmos.DrawLine(_waypoints[i].position, next.position);
        }

        if (!HasLastKnown) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(_lastKnownPosition + Vector3.up, Vector3.one * 0.8f);
    }
}
