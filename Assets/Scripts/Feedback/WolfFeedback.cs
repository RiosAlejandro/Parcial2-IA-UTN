using UnityEngine;

[RequireComponent(typeof(WolfAgent))]
public class WolfFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Colores por estado")]
    [SerializeField] private Color _patrolColor = new Color(0.45f, 0.45f, 0.5f);
    [SerializeField] private Color _chaseColor = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private Color _investigateColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private Color _searchColor = new Color(0.95f, 0.85f, 0.25f);
    [SerializeField] private Color _alertFlashColor = Color.white;

    [Header("Aullido")]
    [SerializeField] private bool _showHowl = true;
    [SerializeField] private float _howlPulseDuration = 0.8f;
    [SerializeField] private int _howlSegments = 41;
    [SerializeField] private float _howlWidth = 0.15f;

    [Header("Alerta recibida")]
    [SerializeField] private float _alertFlashDuration = 1.2f;

    [Header("Camino y objetivo")]
    [SerializeField] private bool _showPath = true;
    [SerializeField] private Color _pathColor = new Color(1f, 0.45f, 0.1f);
    [SerializeField] private float _pathWidth = 0.1f;
    [SerializeField] private int _maxPathPoints = 64;

    [Header("Etiqueta")]
    [SerializeField] private bool _showLabel = true;
    [SerializeField] private float _labelHeight = 2f;

    private WolfAgent _agent;
    private Material _material;
    private LineRenderer _pathLine;
    private LineRenderer _knownLine;
    private LineRenderer _howlRing;
    private Camera _camera;

    private void Awake()
    {
        _agent = GetComponent<WolfAgent>();

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponent<Renderer>();

        if (_bodyRenderer != null)
            _material = _bodyRenderer.material;

        _knownLine = FeedbackDraw.CreateLine(transform, "LastKnownLine", _investigateColor, _pathWidth * 0.8f, 2);

        if (_showPath)
            _pathLine = FeedbackDraw.CreateLine(transform, "PathLine", _pathColor, _pathWidth, _maxPathPoints);

        if (_showHowl)
            _howlRing = FeedbackDraw.CreateLine(transform, "HowlPulse", _chaseColor, _howlWidth, _howlSegments);
    }

    private void Update()
    {
        UpdateColor();
        UpdateHowlPulse();
        UpdateKnownLine();
        UpdatePathLine();
    }

    private void UpdateColor()
    {
        if (_material == null) return;

        _material.color = ReceivedAlertRecently && _agent.CurrentState != WolfStates.Chase
            ? Color.Lerp(StateColor(), _alertFlashColor, Mathf.PingPong(Time.time * 6f, 1f))
            : StateColor();
    }

    private bool ReceivedAlertRecently => _agent.TimeSinceAlert < _alertFlashDuration;

    private Color StateColor()
    {
        switch (_agent.CurrentState)
        {
            case WolfStates.Chase: return _chaseColor;
            case WolfStates.Investigate: return _investigateColor;
            case WolfStates.Search: return _searchColor;
            default: return _patrolColor;
        }
    }

    private void UpdateHowlPulse()
    {
        if (_howlRing == null) return;

        float elapsed = _agent.TimeSinceHowl;

        if (elapsed > _howlPulseDuration)
        {
            _howlRing.enabled = false;
            return;
        }

        float progress = Mathf.Clamp01(elapsed / _howlPulseDuration);

        _howlRing.enabled = true;
        FeedbackDraw.SetColor(_howlRing, new Color(_chaseColor.r, _chaseColor.g, _chaseColor.b, 1f - progress));
        FeedbackDraw.UpdateCircle(_howlRing, transform.position + Vector3.up * 0.1f, progress * _agent.HowlRadius);
    }

    private void UpdateKnownLine()
    {
        if (_knownLine == null) return;

        if (!_agent.HasLastKnown)
        {
            _knownLine.enabled = false;
            return;
        }

        _knownLine.enabled = true;
        FeedbackDraw.SetColor(_knownLine, StateColor());
        _knownLine.SetPosition(0, transform.position);
        _knownLine.SetPosition(1, _agent.LastKnownPosition + Vector3.up);
    }

    private void UpdatePathLine()
    {
        if (_pathLine == null) return;

        PathFollower follower = _agent.Follower;

        if (follower == null || !follower.HasPath)
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
        if (!_showLabel) return;
        if (Event.current.type != EventType.Repaint) return;

        if (_camera == null) _camera = Camera.main;

        FeedbackDraw.DrawLabel(_camera, transform.position + Vector3.up * _labelHeight, LabelText(), StateColor());
    }

    private string LabelText()
    {
        string header = _agent.TimeSinceHowl < _howlPulseDuration
            ? $"{name}  AULLIDO"
            : ReceivedAlertRecently
                ? $"{name}  ALERTA"
                : name;

        return $"{header}\n{_agent.CurrentState}\n{_agent.Activity}";
    }
}
