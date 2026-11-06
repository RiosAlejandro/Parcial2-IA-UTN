using UnityEngine;

[RequireComponent(typeof(PointOfInterest))]
public class PointOfInterestFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer _bodyRenderer;

    [Header("Colores por vida")]
    [SerializeField] private Color _fullColor = new Color(0.95f, 0.85f, 0.2f);
    [SerializeField] private Color _damagedColor = new Color(0.8f, 0.2f, 0.15f);

    [Header("Etiqueta")]
    [SerializeField] private bool _showLabel = true;
    [SerializeField] private float _labelHeight = 1f;

    private PointOfInterest _point;
    private Material _material;
    private Camera _camera;

    private void Awake()
    {
        _point = GetComponent<PointOfInterest>();

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponent<Renderer>();

        if (_bodyRenderer != null)
            _material = _bodyRenderer.material;
    }

    private void Update()
    {
        if (_material == null) return;

        _material.color = Color.Lerp(_damagedColor, _fullColor, _point.HealthRatio);
    }

    private void OnGUI()
    {
        if (!_showLabel) return;
        if (Event.current.type != EventType.Repaint) return;

        if (_camera == null) _camera = Camera.main;

        string text = $"OBJETO\n{Mathf.RoundToInt(_point.HealthRatio * 100f)}%";

        FeedbackDraw.DrawLabel(_camera, transform.position + Vector3.up * _labelHeight, text, _fullColor);
    }
}
