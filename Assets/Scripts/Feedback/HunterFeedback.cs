using System.Text;
using UnityEngine;

[RequireComponent(typeof(HunterAgent))]
public class HunterFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Colores por estado")]
    [SerializeField] private Color _searchColor = new Color(0.25f, 0.5f, 1f);
    [SerializeField] private Color _attackColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private Color _gatherColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color _fleeColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color _exitColor = new Color(0.6f, 0.4f, 1f);
    [SerializeField] private Color _deadColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("Rangos")]
    [SerializeField] private bool _showRanges = true;
    [SerializeField] private int _circleSegments = 49;
    [SerializeField] private float _circleWidth = 0.08f;
    [SerializeField] private float _circleHeight = 0.05f;

    [Header("Camino")]
    [SerializeField] private bool _showPath = true;
    [SerializeField] private Color _pathColor = new Color(0.2f, 0.9f, 0.9f);
    [SerializeField] private float _pathWidth = 0.12f;
    [SerializeField] private int _maxPathPoints = 64;

    [Header("Panel")]
    [SerializeField] private bool _showPanel = true;
    [SerializeField] private Vector2 _panelPosition = new Vector2(12f, 12f);
    [SerializeField] private Vector2 _panelSize = new Vector2(330f, 270f);

    [Header("Etiqueta")]
    [SerializeField] private bool _showLabel = true;
    [SerializeField] private float _labelHeight = 2.6f;

    private HunterAgent _agent;
    private Material _material;
    private LineRenderer _visionCircle;
    private LineRenderer _rangeCircle;
    private LineRenderer _meleeCircle;
    private LineRenderer _targetLine;
    private LineRenderer _pathLine;
    private Camera _camera;
    private readonly StringBuilder _panelText = new StringBuilder();

    private void Awake()
    {
        _agent = GetComponent<HunterAgent>();

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponent<Renderer>();

        if (_bodyRenderer != null)
            _material = _bodyRenderer.material;

        _targetLine = FeedbackDraw.CreateLine(transform, "TargetLine", _attackColor, _circleWidth, 2);

        if (_showPath)
            _pathLine = FeedbackDraw.CreateLine(transform, "PathLine", _pathColor, _pathWidth, _maxPathPoints);

        if (!_showRanges) return;

        _visionCircle = FeedbackDraw.CreateLine(transform, "VisionRadius", Color.yellow, _circleWidth, _circleSegments);
        _rangeCircle = FeedbackDraw.CreateLine(transform, "RangeAttackRadius", Color.magenta, _circleWidth, _circleSegments);
        _meleeCircle = FeedbackDraw.CreateLine(transform, "MeleeAttackRadius", Color.red, _circleWidth, _circleSegments);
    }

    private void Update()
    {
        UpdateColor();
        UpdateRanges();
        UpdateTargetLine();
        UpdatePathLine();
    }

    private void UpdateColor()
    {
        if (_material == null) return;

        _material.color = StateColor();
    }

    private Color StateColor()
    {
        switch (_agent.CurrentState)
        {
            case HunterStates.Attack: return _attackColor;
            case HunterStates.Gather: return _gatherColor;
            case HunterStates.Flee: return _fleeColor;
            case HunterStates.Exit: return _exitColor;
            case HunterStates.Dead: return _deadColor;
            default: return _searchColor;
        }
    }

    private void UpdateRanges()
    {
        if (!_showRanges) return;

        Vector3 center = transform.position;
        center.y += _circleHeight;

        DrawCircle(_visionCircle, center, _agent.VisionRadius);
        DrawCircle(_rangeCircle, center, _agent.RangeAttackRadius);
        DrawCircle(_meleeCircle, center, _agent.MeleeAttackRadius);
    }

    private void DrawCircle(LineRenderer line, Vector3 center, float radius)
    {
        if (line == null) return;

        line.enabled = _agent.CurrentState != HunterStates.Dead;

        if (line.enabled)
            FeedbackDraw.UpdateCircle(line, center, radius);
    }

    private void UpdateTargetLine()
    {
        if (_targetLine == null) return;

        Transform target = TargetTransform();

        if (target == null)
        {
            _targetLine.enabled = false;
            return;
        }

        _targetLine.enabled = true;
        FeedbackDraw.SetColor(_targetLine, _agent.WolfDetected ? _fleeColor : StateColor());
        _targetLine.SetPosition(0, transform.position);
        _targetLine.SetPosition(1, target.position);
    }

    private Transform TargetTransform()
    {
        if (_agent.WolfDetected) return _agent.Wolf;
        if (_agent.Target != null) return _agent.Target.transform;

        return null;
    }

    private void UpdatePathLine()
    {
        if (_pathLine == null) return;

        PathFollower follower = _agent.Follower;

        if (follower == null || !follower.HasPath || _agent.CurrentState == HunterStates.Dead)
        {
            _pathLine.enabled = false;
            return;
        }

        int remaining = follower.Path.Count - follower.Index;
        int points = Mathf.Min(remaining + 1, _maxPathPoints);

        if (points < 2)
        {
            _pathLine.enabled = false;
            return;
        }

        _pathLine.enabled = true;
        _pathLine.positionCount = points;
        _pathLine.SetPosition(0, transform.position);

        for (int i = 1; i < points; i++)
            _pathLine.SetPosition(i, follower.Path[follower.Index + i - 1]);
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        if (_showPanel)
            DrawPanel();

        if (!_showLabel) return;
        if (_agent.CurrentState == HunterStates.Dead) return;

        if (_camera == null) _camera = Camera.main;

        string label = $"CAZADOR\n{_agent.CurrentState}  {_agent.Gathers}/{_agent.RequiredGathers}";

        FeedbackDraw.DrawLabel(_camera, transform.position + Vector3.up * _labelHeight, label, StateColor());
    }

    private void DrawPanel()
    {
        Rect area = new Rect(_panelPosition.x, _panelPosition.y, _panelSize.x, _panelSize.y);

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Box(area, GUIContent.none);
        GUI.color = Color.white;

        _panelText.Clear();
        _panelText.AppendLine("NPC CAZADOR");
        _panelText.AppendLine($"Estado: {_agent.CurrentState}");
        _panelText.AppendLine($"Accion: {_agent.Activity}");
        _panelText.AppendLine($"Recolecciones: {_agent.Gathers}/{_agent.RequiredGathers}");
        _panelText.AppendLine($"Zona actual: {ZoneName()}");
        _panelText.AppendLine($"TBA restante: {_agent.TbaRemaining:0.0}s / {_agent.TBA:0.0}s");
        _panelText.AppendLine($"Boids percibidos: {_agent.DetectedBoids}");
        _panelText.AppendLine($"Objetos activos: {_agent.ActivePointsOfInterest}");
        _panelText.AppendLine($"Lobo detectado: {(_agent.WolfDetected ? _agent.Wolf.name : "no")}");
        _panelText.AppendLine($"Destino: {DestinationName()}");
        _panelText.AppendLine();
        _panelText.AppendLine("Ultimas acciones:");

        foreach (string entry in _agent.ActionLog)
            _panelText.AppendLine(entry);

        GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), _panelText.ToString());
    }

    private string ZoneName() => _agent.CurrentZone != null ? _agent.CurrentZone.Type.ToString() : "pasillo";

    private string DestinationName()
    {
        PathFollower follower = _agent.Follower;

        if (follower == null || !follower.HasPath) return "ninguno";

        Vector3 destination = follower.Destination;

        return $"({destination.x:0}, {destination.z:0})";
    }
}
