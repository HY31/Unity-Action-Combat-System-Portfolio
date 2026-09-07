using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 남은 시간 비율을 오른쪽 기준의 둥근 사각형으로 직접 그린다.
/// 작은 스프라이트를 가로로 늘리지 않아 게이지 끝과 두께가 변형되지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssaultTimerGaugeGraphic : MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] private float value = 1f;
    [SerializeField, Min(0f)] private float cornerRadius = 5f;
    [SerializeField, Min(0f)] private float borderThickness = 2f;
    [SerializeField] private Color borderColor = new Color32(116, 31, 18, 255);
    [SerializeField] private Color topColor = new Color32(255, 112, 31, 255);
    [SerializeField] private Color bottomColor = new Color32(225, 57, 31, 255);

    private const int CornerSegments = 5;

    public float Value
    {
        get => value;
        set
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(this.value, next))
                return;

            this.value = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (value <= 0f)
            return;

        Rect fullRect = GetPixelAdjustedRect();
        float visibleWidth = fullRect.width * value;
        if (visibleWidth <= 0.01f)
            return;

        Rect outerRect = new Rect(
            fullRect.xMax - visibleWidth,
            fullRect.yMin,
            visibleWidth,
            fullRect.height);
        float outerRadius = Mathf.Min(
            cornerRadius,
            outerRect.width * 0.5f,
            outerRect.height * 0.5f);
        AddRoundedRect(vertexHelper, outerRect, outerRadius, borderColor, borderColor);

        float inset = Mathf.Min(
            borderThickness,
            outerRect.width * 0.25f,
            outerRect.height * 0.25f);
        Rect innerRect = new Rect(
            outerRect.xMin + inset,
            outerRect.yMin + inset,
            Mathf.Max(0f, outerRect.width - inset * 2f),
            Mathf.Max(0f, outerRect.height - inset * 2f));
        if (innerRect.width <= 0.01f || innerRect.height <= 0.01f)
            return;

        float innerRadius = Mathf.Max(0f, outerRadius - inset);
        AddRoundedRect(vertexHelper, innerRect, innerRadius, bottomColor, topColor);
    }

    private static void AddRoundedRect(
        VertexHelper vertexHelper,
        Rect rect,
        float radius,
        Color bottom,
        Color top)
    {
        int centerIndex = vertexHelper.currentVertCount;
        UIVertex center = UIVertex.simpleVert;
        center.position = rect.center;
        center.color = Color.Lerp(bottom, top, 0.5f);
        vertexHelper.AddVert(center);

        int perimeterStart = vertexHelper.currentVertCount;
        AddCorner(vertexHelper, rect, radius, 0f, 90f, bottom, top);
        AddCorner(vertexHelper, rect, radius, 90f, 180f, bottom, top);
        AddCorner(vertexHelper, rect, radius, 180f, 270f, bottom, top);
        AddCorner(vertexHelper, rect, radius, 270f, 360f, bottom, top);

        int perimeterCount = vertexHelper.currentVertCount - perimeterStart;
        for (int index = 0; index < perimeterCount; index++)
        {
            int next = (index + 1) % perimeterCount;
            vertexHelper.AddTriangle(
                centerIndex,
                perimeterStart + index,
                perimeterStart + next);
        }
    }

    private static void AddCorner(
        VertexHelper vertexHelper,
        Rect rect,
        float radius,
        float startAngle,
        float endAngle,
        Color bottom,
        Color top)
    {
        Vector2 center;
        if (startAngle < 90f)
            center = new Vector2(rect.xMax - radius, rect.yMax - radius);
        else if (startAngle < 180f)
            center = new Vector2(rect.xMin + radius, rect.yMax - radius);
        else if (startAngle < 270f)
            center = new Vector2(rect.xMin + radius, rect.yMin + radius);
        else
            center = new Vector2(rect.xMax - radius, rect.yMin + radius);

        float height = Mathf.Max(0.0001f, rect.height);
        for (int segment = 0; segment <= CornerSegments; segment++)
        {
            float t = segment / (float)CornerSegments;
            float radians = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            Vector2 position = center + new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians)) * radius;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            float vertical = Mathf.Clamp01((position.y - rect.yMin) / height);
            vertex.color = Color.Lerp(bottom, top, vertical);
            vertexHelper.AddVert(vertex);
        }
    }
}
