using UnityEngine;

[RequireComponent(typeof(BoidAgent))]
public class BoidFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Colores por accion")]
    [SerializeField] private Color _flockingColor = new Color(0.8f, 0.85f, 0.9f);
    [SerializeField] private Color _approachingColor = new Color(0.25f, 0.7f, 1f);
    [SerializeField] private Color _interactingColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color _evadingColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private Color _downColor = new Color(0.25f, 0.25f, 0.28f);

    [Header("Etiqueta")]
    [SerializeField] private bool _showLabel = true;
    [SerializeField] private float _labelHeight = 1.6f;

    [Header("Linea de objetivo")]
    [SerializeField] private bool _showTargetLine = true;
    [SerializeField] private float _lineWidth = 0.06f;

    private BoidAgent _agent;
    private Material _material;
    private LineRenderer _targetLine;
    private Camera _camera;

    private void Awake()
    {
        _agent = GetComponent<BoidAgent>();

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponent<Renderer>();

        if (_bodyRenderer != null)
            _material = _bodyRenderer.material;

        if (_showTargetLine)
            _targetLine = FeedbackDraw.CreateLine(transform, "TargetLine", _flockingColor, _lineWidth, 2);
    }

    private void Update()
    {
        UpdateColor();
        UpdateTargetLine();
    }

    private void UpdateColor()
    {
        if (_material == null) return;

        _material.color = ActionColor();
    }

    private Color ActionColor()
    {
        switch (_agent.Action)
        {
            case BoidAgent.BoidAction.Evading: return _evadingColor;
            case BoidAgent.BoidAction.Approaching: return _approachingColor;
            case BoidAgent.BoidAction.Interacting: return _interactingColor;
            case BoidAgent.BoidAction.Down: return _downColor;
            default: return _flockingColor;
        }
    }

    private void UpdateTargetLine()
    {
        if (_targetLine == null) return;

        Transform threat = _agent.Threat;

        if (!_agent.IsAlive)
        {
            _targetLine.enabled = false;
            return;
        }

        if (threat != null)
        {
            DrawLineTo(threat.position, _evadingColor);
            return;
        }

        PointOfInterest target = _agent.CurrentTarget;

        if (target != null)
        {
            DrawLineTo(target.transform.position, _approachingColor);
            return;
        }

        _targetLine.enabled = false;
    }

    private void DrawLineTo(Vector3 position, Color color)
    {
        _targetLine.enabled = true;
        FeedbackDraw.SetColor(_targetLine, color);
        _targetLine.SetPosition(0, transform.position);
        _targetLine.SetPosition(1, position);
    }

    private void OnGUI()
    {
        if (!_showLabel) return;
        if (Event.current.type != EventType.Repaint) return;
        if (_agent.State == BoidAgent.LifeState.Gathered) return;

        if (_camera == null) _camera = Camera.main;

        FeedbackDraw.DrawLabel(_camera, transform.position + Vector3.up * _labelHeight, LabelText(), ActionColor());
    }

    private string LabelText()
    {
        if (_agent.IsDead)
            return $"{name}\nELIMINADO";

        return $"{name}\n{ActionText()}  |  Vida {Mathf.RoundToInt(_agent.HealthRatio * 100f)}%\nVecinos: {_agent.DetectedNeighbours}";
    }

    private string ActionText()
    {
        switch (_agent.Action)
        {
            case BoidAgent.BoidAction.Evading: return "Evade";
            case BoidAgent.BoidAction.Approaching: return "Arrive";
            case BoidAgent.BoidAction.Interacting: return "Interactuando";
            default: return "Flocking";
        }
    }
}
