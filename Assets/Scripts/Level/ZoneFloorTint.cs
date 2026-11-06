using UnityEngine;

[ExecuteAlways]
public class ZoneFloorTint : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Color _color = Color.gray;

    private MaterialPropertyBlock _block;

    public void SetColor(Color color)
    {
        _color = color;
        Apply();
    }

    private void Awake() => Apply();

    private void OnEnable() => Apply();

    private void OnValidate() => Apply();

    private void Apply()
    {
        if (!TryGetComponent(out Renderer renderer)) return;

        _block ??= new MaterialPropertyBlock();

        renderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, _color);
        _block.SetColor(ColorId, _color);
        renderer.SetPropertyBlock(_block);
    }
}
