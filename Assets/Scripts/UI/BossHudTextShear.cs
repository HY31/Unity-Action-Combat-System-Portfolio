using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Text의 기준선은 수평으로 유지하고 글자 위쪽만 오른쪽으로 눕힌다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHudTextShear : BaseMeshEffect
{
    [SerializeField, Range(-1f, 1f)] private float horizontalShear = 0.364f;

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
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        UIVertex vertex = default;
        RectTransform rect = graphic.rectTransform;
        float centerY = rect.rect.center.y;

        for (int index = 0; index < vertexHelper.currentVertCount; index++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, index);
            Vector3 position = vertex.position;
            position.x += (position.y - centerY) * horizontalShear;
            vertex.position = position;
            vertexHelper.SetUIVertex(vertex, index);
        }
    }
}
