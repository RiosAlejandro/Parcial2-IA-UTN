using System;
using System.Collections.Generic;
using UnityEngine;

public enum HunterStates
{
    Dead = 0,
    Flee = 1,
    Exit = 2,
    Gather = 3,
    Attack = 4,
    Search = 5
}

public class HunterAgent : Agent
{
    [Header("Navegacion")]
    [SerializeField] private NavGraph _graph;
    [SerializeField] private float _moveSpeed = 6f;
    [SerializeField] private float _waypointRadius = 0.6f;
    [SerializeField] private float _repathInterval = 0.5f;

    [Header("Percepcion")]
    [SerializeField] private float _visionRadius = 9f;
    [SerializeField] private float _wolfDetectionRadius = 9f;
    [SerializeField] private LayerMask _boidMask = 1 << 6;
    [SerializeField] private LayerMask _wolfMask = 1 << 9;
    [SerializeField] private LayerMask _obstacleMask = 1 << 8;
    [SerializeField] private LayerMask _zoneMask = 1 << 10;
    [SerializeField] private float _eyeHeight = 1f;
    [SerializeField] private float _detectionInterval = 0.1f;

    [Header("Ataque")]
    [SerializeField] private float _tba = 3f;
    [SerializeField] private float _rangeAttackRadius = 5f;
    [SerializeField] private float _meleeAttackRadius = 1.8f;
    [SerializeField] private float _meleeDamage = 34f;
    [SerializeField] private float _rangedDamage = 25f;

    [Header("Objetivo")]
    [SerializeField] private int _requiredGathers = 3;
    [SerializeField] private float _respawnTime = 5f;
    [SerializeField] private float _safeZoneWaitTime = 4f;
    [SerializeField] private float _gatherDuration = 2f;
    [SerializeField] private float _gatherRadius = 1.8f;

    [Header("Objetos de interes")]
    [SerializeField] private GameObject _pointOfInterestPrefab;
    [SerializeField] private float _poiSpawnInterval = 6f;
    [SerializeField] private int _maxActivePoi = 5;
    [SerializeField] private float _poiSpawnHeight = 0.5f;

    [Header("Registro de acciones")]
    [SerializeField] private int _actionLogSize = 7;

    private const int BufferSize = 32;

    private readonly Collider[] _overlapBuffer = new Collider[BufferSize];
    private readonly List<BoidAgent> _detectedBoids = new List<BoidAgent>();
    private readonly List<PointOfInterest> _spawnedPoints = new List<PointOfInterest>();
    private readonly List<string> _actionLog = new List<string>();
    private readonly List<Vector3> _pathBuffer = new List<Vector3>();
    private readonly List<int> _zoneNodes = new List<int>();

    private StateMachine _stateMachine;
    private AStar _pathfinder;
    private PathFollower _follower;

    private float _tbaTimer;
    private float _poiTimer;
    private float _detectionTimer;
    private Transform _wolf;
    private ZoneVolume _currentZone;

    public float VisionRadius => _visionRadius;
    public float TBA => _tba;
    public float RangeAttackRadius => _rangeAttackRadius;
    public float MeleeAttackRadius => _meleeAttackRadius;
    public int RequiredGathers => _requiredGathers;
    public float RespawnTime => _respawnTime;
    public float SafeZoneWaitTime => _safeZoneWaitTime;
    public float GatherDuration => _gatherDuration;
    public float GatherRadius => _gatherRadius;
    public NavGraph Graph => _graph;

    public bool IsAttackReady => _tbaTimer >= _tba;
    public float TbaRemaining => Mathf.Max(0f, _tba - _tbaTimer);
    public int Gathers { get; private set; }
    public bool HasEnoughGathers => Gathers >= _requiredGathers;
    public bool IsDowned { get; private set; }
    public Transform Wolf => _wolf;
    public bool WolfDetected => _wolf != null;
    public ZoneVolume CurrentZone => _currentZone;
    public BoidAgent Target { get; private set; }
    public BoidAgent Prey { get; private set; }
    public string Activity { get; private set; } = "Iniciando";
    public int DetectedBoids => _detectedBoids.Count;
    public int ActivePointsOfInterest => _spawnedPoints.Count;
    public IReadOnlyList<string> ActionLog => _actionLog;
    public PathFollower Follower => _follower;

    public HunterStates CurrentState => _stateMachine != null && _stateMachine.CurrentKey is HunterStates key
        ? key
        : HunterStates.Search;

    private void Awake()
    {
        if (_graph == null) _graph = FindFirstObjectByType<NavGraph>();

        _pathfinder = new AStar(_graph);
        _follower = new PathFollower();
        _tbaTimer = _tba;

        _stateMachine = new StateMachine();
        _stateMachine.RegisterState(HunterStates.Search, new HunterSearchState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Attack, new HunterAttackState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Gather, new HunterGatherState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Flee, new HunterFleeState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Exit, new HunterExitState(this, _stateMachine));
        _stateMachine.RegisterState(HunterStates.Dead, new HunterDeadState(this, _stateMachine));

        _stateMachine.StateChanged += OnStateChanged;
        _stateMachine.ChangeState(HunterStates.Search);
    }

    private void OnDestroy()
    {
        if (_stateMachine != null)
            _stateMachine.StateChanged -= OnStateChanged;
    }

    private void Update()
    {
        _tbaTimer += Time.deltaTime;
        _poiTimer += Time.deltaTime;

        UpdateDetection();
        _stateMachine.Update();
    }

    private void OnStateChanged(Enum key) => ReportAction($"Estado -> {key}");

    private void UpdateDetection()
    {
        _detectionTimer -= Time.deltaTime;
        if (_detectionTimer > 0f) return;

        _detectionTimer = _detectionInterval;

        DetectBoids();
        UpdatePrey();
        DetectWolf();
        DetectZone();

        _spawnedPoints.RemoveAll(point => point == null || point.IsDestroyed);
    }

    private void DetectBoids()
    {
        _detectedBoids.Clear();

        if (IsDowned) return;

        int found = Physics.OverlapSphereNonAlloc(transform.position, _visionRadius, _overlapBuffer, _boidMask);

        for (int i = 0; i < found; i++)
        {
            if (!_overlapBuffer[i].TryGetComponent(out BoidAgent boid)) continue;
            if (!HasLineOfSightTo(boid.transform.position)) continue;

            _detectedBoids.Add(boid);
        }
    }

    private void DetectWolf()
    {
        _wolf = null;

        if (IsDowned) return;
        if (_currentZone != null && _currentZone.Type == ZoneType.Safe) return;

        int found = Physics.OverlapSphereNonAlloc(transform.position, _wolfDetectionRadius, _overlapBuffer, _wolfMask);

        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < found; i++)
        {
            Transform candidate = _overlapBuffer[i].transform;

            if (!HasLineOfSightTo(candidate.position)) continue;

            float sqrDist = (candidate.position - transform.position).sqrMagnitude;
            if (sqrDist >= nearestSqrDist) continue;

            _wolf = candidate;
            nearestSqrDist = sqrDist;
        }
    }

    private void DetectZone()
    {
        _currentZone = null;

        int found = Physics.OverlapSphereNonAlloc(transform.position, 0.1f, _overlapBuffer, _zoneMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < found; i++)
        {
            if (!_overlapBuffer[i].TryGetComponent(out ZoneVolume zone)) continue;

            _currentZone = zone;
            break;
        }
    }

    // Los agentes se mueven en horizontal y estan a distinta altura: medir en 3D achica los rangos.
    public float HorizontalDistanceTo(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;

        return offset.magnitude;
    }

    public bool HasLineOfSightTo(Vector3 point)
    {
        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 target = point + Vector3.up * _eyeHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon) return true;

        return !Physics.Raycast(origin, direction / distance, distance, _obstacleMask);
    }

    // Mantiene el mismo objetivo mientras siga vivo y percibido, para no repartir el ataque entre toda la bandada.
    private void UpdatePrey()
    {
        if (Prey == null) return;
        if (Prey.IsAlive && _detectedBoids.Contains(Prey)) return;

        Prey = null;
    }

    public void SetPrey(BoidAgent prey) => Prey = prey;

    public bool IsInZone(ZoneType type) => _currentZone != null && _currentZone.Type == type;

    public BoidAgent FindNearestAliveBoid() => FindNearestBoid(boid => boid.IsAlive);

    public BoidAgent FindNearestDeadBoid() => FindNearestBoid(boid => boid.IsDead);

    private BoidAgent FindNearestBoid(Func<BoidAgent, bool> predicate)
    {
        BoidAgent nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (BoidAgent boid in _detectedBoids)
        {
            if (boid == null || !predicate(boid)) continue;

            float sqrDist = (boid.transform.position - transform.position).sqrMagnitude;
            if (sqrDist >= nearestSqrDist) continue;

            nearest = boid;
            nearestSqrDist = sqrDist;
        }

        return nearest;
    }

    public bool SetDestination(Vector3 destination)
    {
        if (_graph == null) return false;

        if (!_pathfinder.TryFindPath(transform.position, destination, _pathBuffer))
        {
            _follower.Clear();
            return false;
        }

        _follower.SetPath(_pathBuffer);
        return true;
    }

    public bool FollowPath()
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

        MoveTowards(_follower.CurrentWaypoint);
        return false;
    }

    public void ClearPath() => _follower.Clear();

    public float RepathInterval => _repathInterval;

    public bool SetDestinationToZone(ZoneType zone)
    {
        if (_graph == null) return false;

        if (!_pathfinder.TryFindPathToZone(transform.position, zone, _pathBuffer))
        {
            _follower.Clear();
            return false;
        }

        LastDestinationCost = _pathfinder.LastPathCost;
        _follower.SetPath(_pathBuffer);
        return true;
    }

    public float LastDestinationCost { get; private set; }

    public bool TryGetRandomZoneNode(ZoneType zone, out Vector3 position)
    {
        position = Vector3.zero;

        if (_graph == null) return false;

        _graph.CollectNodesInZone(zone, _zoneNodes);

        if (_zoneNodes.Count == 0) return false;

        position = _graph.GetPosition(_zoneNodes[UnityEngine.Random.Range(0, _zoneNodes.Count)]);
        return true;
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }

        direction.Normalize();
        _velocity = direction * _moveSpeed;

        transform.position += _velocity * Time.deltaTime;
        transform.forward = direction;
    }

    public void Pursue(BoidAgent target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        float prediction = toTarget.magnitude / Mathf.Max(0.01f, _moveSpeed + target.Velocity.magnitude);

        MoveTowards(target.transform.position + target.Velocity * prediction);
    }

    public void Stop() => _velocity = Vector3.zero;

    public void SetTarget(BoidAgent target) => Target = target;

    public void SetActivity(string activity) => Activity = activity;

    public void ResetAttackTimer() => _tbaTimer = 0f;

    public void MeleeAttack(BoidAgent target)
    {
        target.TakeDamage(_meleeDamage);
        ReportAction(target.IsDead ? "Melee: boid abatido" : "Ataque cuerpo a cuerpo");
    }

    public void RangedAttack(BoidAgent target)
    {
        target.TakeDamage(_rangedDamage);
        ReportAction(target.IsDead ? "Distancia: boid abatido" : "Ataque a distancia");
    }

    public void AddGather()
    {
        Gathers++;
        ReportAction($"Recoleccion {Gathers}/{_requiredGathers}");
    }

    public void TrySpawnPointOfInterest()
    {
        if (_poiTimer < _poiSpawnInterval) return;

        _poiTimer = 0f;

        if (_pointOfInterestPrefab == null || ArenaBounds.Instance == null) return;

        _spawnedPoints.RemoveAll(point => point == null || point.IsDestroyed);

        if (_spawnedPoints.Count >= _maxActivePoi) return;

        Vector3 spawnPosition = ArenaBounds.Instance.GetRandomPoint();
        spawnPosition.y += _poiSpawnHeight;

        GameObject spawned = Instantiate(_pointOfInterestPrefab, spawnPosition, Quaternion.identity);

        if (spawned.TryGetComponent(out PointOfInterest point))
            _spawnedPoints.Add(point);

        ReportAction("Objeto de interes generado");
    }

    public void Kill()
    {
        if (IsDowned) return;

        IsDowned = true;
        ReportAction("Alcanzado por un lobo");
    }

    public void SetVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = visible;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = visible;
    }

    public void RespawnAtStart()
    {
        if (TryGetRandomZoneNode(ZoneType.Start, out Vector3 start))
            transform.position = new Vector3(start.x, transform.position.y, start.z);

        Gathers = 0;
        IsDowned = false;
        Target = null;
        _wolf = null;
        _follower.Clear();
        Stop();

        ReportAction("Reaparece en la Zona inicial");
    }

    public void ReportAction(string action)
    {
        _actionLog.Insert(0, $"{Time.timeSinceLevelLoad:0.0}s  {action}");

        if (_actionLog.Count > _actionLogSize)
            _actionLog.RemoveAt(_actionLog.Count - 1);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _visionRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _rangeAttackRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _meleeAttackRadius);

        _follower?.DrawGizmo(transform.position, Color.cyan);
    }
}
