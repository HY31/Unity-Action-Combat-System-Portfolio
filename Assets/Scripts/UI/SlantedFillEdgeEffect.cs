using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가로 Filled Image의 움직이는 끝단만 사선으로 만든다.
/// fillAmount와 UV는 그대로 사용하므로 게이지의 전투 규칙에는 영향을 주지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class SlantedFillEdgeEffect : BaseMeshEffect
{
    [SerializeField, Range(-1f, 1f)]
    private float horizontalShear = 0.4663f;

    [SerializeField, Min(0.0001f)]
    private float edgeTolerance = 0.01f;

    public float HorizontalShear
    {
        get => horizontalShear;
        set
        {
            horizontalShear = Mathf.Clamp(value, -1f, 1f);
            graphic?.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount < 2)
            return;

        Image image = graphic as Image;
        if (image == null ||
            image.type != Image.Type.Filled ||
            image.fillMethod != Image.FillMethod.Horizontal)
        {
            return;
        }

        // 완전히 찬 상태에서는 원본 스프라이트의 둥근 사선 캡을 그대로 쓴다.
        if (image.fillAmount >= 0.9999f)
            return;

        UIVertex vertex = default;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int index = 0; index < vertexHelper.currentVertCount; index++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, index);
            Vector3 position = vertex.position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
        }

        bool fillsFromLeft =
            image.fillOrigin == (int)Image.OriginHorizontal.Left;
        float movingEdgeX = fillsFromLeft ? maxX : minX;
        float fixedEdgeX = fillsFromLeft ? minX : maxX;
        float visibleWidth = Mathf.Abs(movingEdgeX - fixedEdgeX);
        float height = maxY - minY;
        float requestedShift = Mathf.Abs(horizontalShear) * height;
        float safeShift = visibleWidth * 0.9f;
        float shiftScale = requestedShift > 0f
            ? Mathf.Min(1f, safeShift / requestedShift)
            : 1f;
        float edgeAnchorY = horizontalShear >= 0f ? maxY : minY;

        for (int index = 0; index < vertexHelper.currentVertCount; index++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, index);
            Vector3 position = vertex.position;
            if (Mathf.Abs(position.x - movingEdgeX) > edgeTolerance)
                continue;

            position.x +=
                (position.y - edgeAnchorY) * horizontalShear * shiftScale;
            vertex.position = position;
            vertexHelper.SetUIVertex(vertex, index);
        }
    }
}