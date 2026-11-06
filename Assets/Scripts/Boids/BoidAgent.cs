using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidAgent : Agent
{
    public enum LifeState { Alive, Dead, Gathered }
    public enum BoidAction { Flocking, Evading, Approaching, Interacting, Down, Collected }

    [Header("Movimiento")]
    [SerializeField] private float _maxSpeed = 4f;
    [SerializeField] private float _maxSteering = 8f;

    [Header("Flocking - Separation")]
    [SerializeField] private float _separationRadius = 1.5f;
    [SerializeField, Range(0f, 3f)] private float _separationWeight = 2.5f;

    [Header("Flocking - Alignment y Cohesion")]
    [SerializeField] private float _flockRadius = 4f;
    [SerializeField, Range(0f, 3f)] private float _alignmentWeight = 1f;
    [SerializeField, Range(0f, 3f)] private float _cohesionWeight = 1f;

    [Header("Deteccion")]
    [SerializeField] private LayerMask _boidMask = 1 << 6;
    [SerializeField] private LayerMask _pointOfInterestMask = 1 << 7;
    [SerializeField] private float _detectionInterval = 0.1f;

    [Header("Evade")]
    [SerializeField] private FieldOfViewSensor _hunterSensor;

    [Header("Puntos de interes")]
    [SerializeField] private float _poiDetectionRadius = 10f;
    [SerializeField] private float _interactDistance = 1.6f;
    [SerializeField] private float _interactInterval = 1f;
    [SerializeField] private float _damagePerInteraction = 10f;
    [SerializeField] private float _slowingDistance = 3f;
    [SerializeField] private float _minArriveDistance = 1.3f;

    [Header("Limites del coto")]
    [SerializeField] private float _boundsMargin = 2.5f;
    [SerializeField, Range(0f, 5f)] private float _boundsWeight = 3f;

    [Header("Vida")]
    [SerializeField] private float _maxHealth = 100f;

    [Header("Respawn")]
    [SerializeField] private float _respawnDelay = 5f;

    [Header("Gizmos")]
    [SerializeField] private bool _drawGizmos = true;

    private const int BufferSize = 16;

    private readonly Collider[] _overlapBuffer = new Collider[BufferSize];
    private readonly List<BoidAgent> _neighbours = new List<BoidAgent>();

    private LifeState _state = LifeState.Alive;
    private BoidAction _action = BoidAction.Flocking;
    private float _health;
    private float _spawnHeight;
    private PointOfInterest _currentTarget;
    private float _interactTimer;
    private float _detectionTimer;

    public LifeState State => _state;
    public BoidAction Action => _action;
    public bool IsAlive => _state == LifeState.Alive;
    public bool IsDead => _state == LifeState.Dead;
    public bool IsVisible => _state != LifeState.Gathered;
    public float HealthRatio => _maxHealth > 0f ? _health / _maxHealth : 0f;
    public int DetectedNeighbours => _neighbours.Count;
    public float SeparationRadius => _separationRadius;
    public float FlockRadius => _flockRadius;
    public PointOfInterest CurrentTarget => HasTarget ? _currentTarget : null;
    public Transform Threat => _hunterSensor != null && _hunterSensor.CanSeeTarget ? _hunterSensor.Target : null;

    private bool HasTarget => _currentTarget != null && !_currentTarget.IsDestroyed;

    private void Awake()
    {
        _health = _maxHealth;
        _spawnHeight = transform.position.y;
        _detectionTimer = Random.Range(0f, _detectionInterval);
        _velocity = RandomDirection() * _maxSpeed;
        transform.forward = _velocity;
    }

    private void Update()
    {
        if (_state != LifeState.Alive) return;

        UpdateDetection();
        TryInteractWithPointOfInterest();

        _velocity += SteeringVector();
        _velocity.y = 0f;
        _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);

        transform.position += _velocity * Time.deltaTime;

        if (ArenaBounds.Instance != null)
            transform.position = ArenaBounds.Instance.Clamp(transform.position);

        if (_velocity != Vector3.zero)
            transform.forward = _velocity;
    }

    private void UpdateDetection()
    {
        _detectionTimer -= Time.deltaTime;
        if (_detectionTimer > 0f) return;

        _detectionTimer = _detectionInterval;

        RefreshNeighbours();
        RefreshPointOfInterestTarget();
    }

    private void RefreshNeighbours()
    {
        _neighbours.Clear();

        float radius = Mathf.Max(_flockRadius, _separationRadius);
        int found = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapBuffer, _boidMask);

        for (int i = 0; i < found; i++)
        {
            if (!_overlapBuffer[i].TryGetComponent(out BoidAgent neighbour)) continue;
            if (neighbour == this) continue;

            _neighbours.Add(neighbour);
        }
    }

    private void RefreshPointOfInterestTarget()
    {
        if (HasTarget) return;

        _currentTarget = FindNearestPointOfInterest();
        _interactTimer = 0f;
    }

    private PointOfInterest FindNearestPointOfInterest()
    {
        int found = Physics.OverlapSphereNonAlloc(transform.position, _poiDetectionRadius, _overlapBuffer, _pointOfInterestMask);

        PointOfInterest nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < found; i++)
        {
            if (!_overlapBuffer[i].TryGetComponent(out PointOfInterest poi)) continue;
            if (poi.IsDestroyed) continue;

            float sqrDist = (poi.transform.position - transform.position).sqrMagnitude;

            if (sqrDist >= nearestSqrDist) continue;

            nearest = poi;
            nearestSqrDist = sqrDist;
        }

        return nearest;
    }

    private Vector3 SteeringVector()
    {
        Vector3 containment = Containment();
        Transform threat = Threat;

        if (threat != null)
        {
            _action = BoidAction.Evading;
            return Evade(threat) + containment;
        }

        Vector3 steering = Flocking() + containment;

        if (!HasTarget)
        {
            _action = BoidAction.Flocking;
            return steering;
        }

        float distance = Vector3.Distance(transform.position, _currentTarget.transform.position);
        _action = distance <= _interactDistance ? BoidAction.Interacting : BoidAction.Approaching;

        return steering + Arrive(_currentTarget.transform.position);
    }

    // Empuja al boid hacia adentro antes de que llegue al borde del coto.
    private Vector3 Containment()
    {
        if (ArenaBounds.Instance == null) return Vector3.zero;

        Vector3 inward = ArenaBounds.Instance.InwardDirection(transform.position, _boundsMargin);

        if (inward == Vector3.zero) return Vector3.zero;

        return CalculateSteering(inward * _maxSpeed) * _boundsWeight;
    }

    private Vector3 Flocking()
    {
        return CalculateSeparation() * _separationWeight
             + CalculateAlignment() * _alignmentWeight
             + CalculateCohesion() * _cohesionWeight;
    }

    private Vector3 CalculateSeparation()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent other in _neighbours)
        {
            if (other == null || !other.IsVisible) continue;

            Vector3 offset = other.transform.position - transform.position;
            float distance = offset.magnitude;

            if (distance > _separationRadius || distance <= Mathf.Epsilon) continue;

            desired -= offset / (distance * distance);
            count++;
        }

        if (count == 0 || desired == Vector3.zero) return Vector3.zero;

        return CalculateSteering(desired.normalized * _maxSpeed);
    }

    private Vector3 CalculateAlignment()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent other in _neighbours)
        {
            if (other == null || !other.IsAlive) continue;
            if (!InRange(other.transform.position, _flockRadius)) continue;

            desired += other.Velocity;
            count++;
        }

        if (count == 0) return Vector3.zero;

        desired /= count;

        if (desired == Vector3.zero) return Vector3.zero;

        return CalculateSteering(desired.normalized * _maxSpeed);
    }

    private Vector3 CalculateCohesion()
    {
        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (BoidAgent other in _neighbours)
        {
            if (other == null || !other.IsAlive) continue;
            if (!InRange(other.transform.position, _flockRadius)) continue;

            desired += other.transform.position;
            count++;
        }

        if (count == 0) return Vector3.zero;

        desired /= count;

        return Seek(desired);
    }

    private bool InRange(Vector3 pos, float radius) => (pos - transform.position).sqrMagnitude <= radius * radius;

    private Vector3 RandomDirection()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }

    private Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - _velocity;
        return Vector3.ClampMagnitude(steering, _maxSteering * Time.deltaTime);
    }

    private Vector3 DesiredVector(Vector3 target) => (target - transform.position).normalized * _maxSpeed;

    private Vector3 Seek(Vector3 target) => CalculateSteering(DesiredVector(target));

    private Vector3 Flee(Vector3 target) => CalculateSteering(-DesiredVector(target));

    private Vector3 Arrive(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        float distance = direction.magnitude;

        if (distance < _minArriveDistance)
            return CalculateSteering(Vector3.zero);

        float desiredSpeed = Mathf.Min(_maxSpeed * (distance / _slowingDistance), _maxSpeed);
        Vector3 desired = direction.normalized * desiredSpeed;

        return CalculateSteering(desired);
    }

    private Vector3 Evade(Transform target)
    {
        Vector3 futurePosition = CalculateFuture(target);
        return Flee(futurePosition);
    }

    private Vector3 CalculateFuture(Transform target)
    {
        IVelocityProvider velocityProvider = target.GetComponent<IVelocityProvider>();
        Vector3 targetVelocity = velocityProvider != null ? velocityProvider.Velocity : Vector3.zero;

        Vector3 direction = target.position - transform.position;
        float prediction = direction.magnitude / (_maxSpeed + targetVelocity.magnitude);

        return target.position + targetVelocity * prediction;
    }

    private void TryInteractWithPointOfInterest()
    {
        if (!HasTarget) return;

        float distance = Vector3.Distance(transform.position, _currentTarget.transform.position);
        if (distance > _interactDistance) return;

        _interactTimer += Time.deltaTime;
        if (_interactTimer < _interactInterval) return;

        _interactTimer = 0f;
        _currentTarget.TakeDamage(_damagePerInteraction);
    }

    public void TakeDamage(float amount)
    {
        if (_state != LifeState.Alive) return;

        _health = Mathf.Max(0f, _health - amount);

        if (_health > 0f) return;

        _state = LifeState.Dead;
        _action = BoidAction.Down;
        _velocity = Vector3.zero;
        _currentTarget = null;
        _neighbours.Clear();
    }

    public void Gather()
    {
        if (_state != LifeState.Dead) return;

        _state = LifeState.Gathered;
        _action = BoidAction.Collected;
        SetVisible(false);
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        _health = _maxHealth;
        _currentTarget = null;
        _interactTimer = 0f;
        _detectionTimer = 0f;
        _neighbours.Clear();

        transform.position = RespawnPoint();
        _velocity = RandomDirection() * _maxSpeed;
        transform.forward = _velocity;

        SetVisible(true);
        _state = LifeState.Alive;
        _action = BoidAction.Flocking;
    }

    private Vector3 RespawnPoint()
    {
        if (ArenaBounds.Instance == null) return transform.position;

        Vector3 point = ArenaBounds.Instance.GetRandomPoint();
        point.y = _spawnHeight;
        return point;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = visible;

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = visible;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _separationRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _flockRadius);

        if (_hunterSensor == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _hunterSensor.ViewRadius);
    }
}
