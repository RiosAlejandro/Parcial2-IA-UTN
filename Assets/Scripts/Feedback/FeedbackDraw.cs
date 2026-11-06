using UnityEngine;

public static class FeedbackDraw
{
    private const string ShaderName = "Sprites/Default";
    private const string FallbackShaderName = "Unlit/Color";
    private const float LabelWidth = 220f;
    private const float LabelHeight = 52f;

    private static Material _sharedMaterial;
    private static GUIStyle _labelStyle;

    public static LineRenderer CreateLine(Transform parent, string name, Color color, float width, int points)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localScale = InverseScale(parent);

        LineRenderer line = holder.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetMaterial();
        line.widthMultiplier = width;
        line.positionCount = points;
        line.startColor = color;
        line.endColor = color;
        line.enabled = false;

        return line;
    }

    public static void UpdateCircle(LineRenderer line, Vector3 center, float radius)
    {
        int points = line.positionCount;
        if (points < 2) return;

        for (int i = 0; i < points; i++)
        {
            float angle = i / (float)(points - 1) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;

            line.SetPosition(i, center + offset);
        }
    }

    public static void SetColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    public static void DrawLabel(Camera camera, Vector3 worldPosition, string text, Color color)
    {
        if (camera == null) return;

        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f) return;

        Rect area = new Rect(screenPosition.x - LabelWidth / 2f, Screen.height - screenPosition.y - LabelHeight, LabelWidth, LabelHeight);
        GUIStyle style = GetLabelStyle();

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, style);

        style.normal.textColor = color;
        GUI.Label(area, text, style);
    }

    private static Vector3 InverseScale(Transform parent)
    {
        if (parent == null) return Vector3.one;

        Vector3 scale = parent.lossyScale;

        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }

    private static GUIStyle GetLabelStyle()
    {
        if (_labelStyle != null) return _labelStyle;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false
        };

        return _labelStyle;
    }

    private static Material GetMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) shader = Shader.Find(FallbackShaderName);
        if (shader == null) return null;

        _sharedMaterial = new Material(shader);
        return _sharedMaterial;
    }
}
