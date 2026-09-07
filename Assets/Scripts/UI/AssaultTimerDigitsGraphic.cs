using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강습전 타이머를 외부 폰트 없이 선명한 디지털 세그먼트로 그린다.
/// 해상도와 무관하게 숫자 외곽선 두께가 함께 스케일된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssaultTimerDigitsGraphic : MaskableGraphic
{
    private const int Top = 1 << 0;
    private const int UpperRight = 1 << 1;
    private const int LowerRight = 1 << 2;
    private const int Bottom = 1 << 3;
    private const int LowerLeft = 1 << 4;
    private const int UpperLeft = 1 << 5;
    private const int Middle = 1 << 6;

    private static readonly int[] DigitSegments =
    {
        Top | UpperRight | LowerRight | Bottom | LowerLeft | UpperLeft,
        UpperRight | LowerRight,
        Top | UpperRight | Middle | LowerLeft | Bottom,
        Top | UpperRight | LowerRight | Middle | Bottom,
        UpperLeft | Middle | UpperRight | LowerRight,
        Top | UpperLeft | Middle | LowerRight | Bottom,
        Top | UpperLeft | Middle | LowerLeft | LowerRight | Bottom,
        Top | UpperRight | LowerRight,
        Top | UpperRight | LowerRight | Bottom | LowerLeft | UpperLeft | Middle,
        Top | UpperRight | LowerRight | Bottom | UpperLeft | Middle
    };

    [SerializeField] private string value = "00:03:00";
    [SerializeField] private Color outlineColor = new Color32(3, 5, 6, 255);
    [SerializeField, Range(0.04f, 0.2f)] private float segmentThickness = 0.12f;
    [SerializeField, Range(0.02f, 0.15f)] private float outlineThickness = 0.038f;
    [SerializeField, Range(0f, 0.3f)] private float characterSpacing = 0.07f;

    public string Value
    {
        get => value;
        set
        {
            string next = string.IsNullOrEmpty(value) ? "--:--:--" : value;
            if (this.value == next)
                return;

            this.value = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (string.IsNullOrEmpty(value))
            return;

        Rect bounds = rectTransform.rect;
        float unitHeight = bounds.height;
        float digitUnits = 0.54f;
        float colonUnits = 0.14f;
        float totalUnits = 0f;

        for (int i = 0; i < value.Length; i++)
        {
            totalUnits += value[i] == ':' ? colonUnits : digitUnits;
            if (i + 1 < value.Length)
                totalUnits += characterSpacing;
        }

        float scale = Mathf.Min(
            unitHeight,
            bounds.width / Mathf.Max(0.01f, totalUnits));
        float contentWidth = totalUnits * scale;
        float contentHeight = scale;
        float cursor = bounds.xMin + (bounds.width - contentWidth) * 0.5f;
        float y = bounds.yMin + (bounds.height - contentHeight) * 0.5f;

        Color32 face = color;
        Color32 edge = outlineColor;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            float characterWidth =
                (character == ':' ? colonUnits : digitUnits) * scale;

            if (character == ':')
            {
                DrawColon(vertexHelper, cursor, y, characterWidth, scale, edge, face);
            }
            else if (character >= '0' && character <= '9')
            {
                DrawDigit(
                    vertexHelper,
                    character - '0',
                    cursor,
                    y,
                    characterWidth,
                    scale,
                    edge,
                    face);
            }

            cursor += characterWidth + characterSpacing * scale;
        }
    }

    private void DrawDigit(
        VertexHelper vertexHelper,
        int digit,
        float x,
        float y,
        float width,
        float height,
        Color32 edge,
        Color32 face)
    {
        int mask = DigitSegments[Mathf.Clamp(digit, 0, 9)];
        float thickness = height * segmentThickness;
        float outline = height * outlineThickness;
        float half = height * 0.5f;
        float horizontalInset = thickness * 0.75f;
        float verticalHeight = half - thickness * 2f;

        if ((mask & Top) != 0)
            DrawSegment(vertexHelper, new Rect(x + horizontalInset, y + height - thickness, width - horizontalInset * 2f, thickness), outline, edge, face);
        if ((mask & Middle) != 0)
            DrawSegment(vertexHelper, new Rect(x + horizontalInset, y + half - thickness * 0.5f, width - horizontalInset * 2f, thickness), outline, edge, face);
        if ((mask & Bottom) != 0)
            DrawSegment(vertexHelper, new Rect(x + horizontalInset, y, width - horizontalInset * 2f, thickness), outline, edge, face);
        if ((mask & UpperLeft) != 0)
            DrawSegment(vertexHelper, new Rect(x, y + half + thickness, thickness, verticalHeight), outline, edge, face);
        if ((mask & UpperRight) != 0)
            DrawSegment(vertexHelper, new Rect(x + width - thickness, y + half + thickness * 0.55f, thickness, verticalHeight), outline, edge, face);
        if ((mask & LowerLeft) != 0)
            DrawSegment(vertexHelper, new Rect(x, y + thickness, thickness, verticalHeight), outline, edge, face);
        if ((mask & LowerRight) != 0)
            DrawSegment(vertexHelper, new Rect(x + width - thickness, y + thickness * 0.6f, thickness, verticalHeight), outline, edge, face);
    }

    private void DrawColon(
        VertexHelper vertexHelper,
        float x,
        float y,
        float width,
        float height,
        Color32 edge,
        Color32 face)
    {
        float size = Mathf.Min(width, height * segmentThickness * 1.05f);
        float outline = height * outlineThickness;
        float centerX = x + width * 0.5f - size * 0.5f;
        DrawSegment(vertexHelper, new Rect(centerX, y + height * 0.66f, size, size), outline, edge, face);
        DrawSegment(vertexHelper, new Rect(centerX, y + height * 0.25f, size, size), outline, edge, face);
    }

    private static void DrawSegment(
        VertexHelper vertexHelper,
        Rect rect,
        float outline,
        Color32 edge,
        Color32 face)
    {
        bool horizontal = rect.width >= rect.height;
        Rect outlineRect = new Rect(
            rect.xMin - outline,
            rect.yMin - outline,
            rect.width + outline * 2f,
            rect.height + outline * 2f);
        AddBeveledSegment(vertexHelper, outlineRect, horizontal, edge);
        AddBeveledSegment(vertexHelper, rect, horizontal, face);
    }

    private static void AddBeveledSegment(
        VertexHelper vertexHelper,
        Rect rect,
        bool horizontal,
        Color32 color)
    {
        float shortSide = horizontal ? rect.height : rect.width;
        float longSide = horizontal ? rect.width : rect.height;
        float bevel = Mathf.Min(shortSide * 0.46f, longSide * 0.14f);
        float centerX = rect.center.x;
        float centerY = rect.center.y;
        Vector3[] points = horizontal
            ? new[]
            {
                new Vector3(rect.xMin + bevel, rect.yMin),
                new Vector3(rect.xMax - bevel, rect.yMin),
                new Vector3(rect.xMax, centerY),
                new Vector3(rect.xMax - bevel, rect.yMax),
                new Vector3(rect.xMin + bevel, rect.yMax),
                new Vector3(rect.xMin, centerY)
            }
            : new[]
            {
                new Vector3(centerX, rect.yMin),
                new Vector3(rect.xMax, rect.yMin + bevel),
                new Vector3(rect.xMax, rect.yMax - bevel),
                new Vector3(centerX, rect.yMax),
                new Vector3(rect.xMin, rect.yMax - bevel),
                new Vector3(rect.xMin, rect.yMin + bevel)
            };

        int start = vertexHelper.currentVertCount;
        for (int index = 0; index < points.Length; index++)
            vertexHelper.AddVert(points[index], color, Vector2.zero);

        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start, start + 2, start + 3);
        vertexHelper.AddTriangle(start, start + 3, start + 4);
        vertexHelper.AddTriangle(start, start + 4, start + 5);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
