using UnityEngine;
using UnityEngine.UI;

/// <summary>프레임 외곽 안쪽을 채우는 공통 바탕. 원본 스프라이트를 변경하지 않는다.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerHudFrameBackground : MaskableGraphic
{
    [SerializeField] private RectTransform frame;
    // Each normalized band is (left, right, bottom, top), baked from the frame alpha.
    [SerializeField, HideInInspector] private Vector4[] bands = new Vector4[0];

    public void Configure(RectTransform sourceFrame, Vector4[] silhouetteBands, Color backgroundColor)
    {
        frame = sourceFrame;
        bands = silhouetteBands;
        color = backgroundColor;
        raycastTarget = false;
        FollowFrame();
        SetAllDirty();
    }

    private void LateUpdate() => FollowFrame();

    private void FollowFrame()
    {
        if (frame == null || frame.parent != transform.parent)
            return;
        var target = rectTransform;
        if (target.anchorMin != frame.anchorMin) target.anchorMin = frame.anchorMin;
        if (target.anchorMax != frame.anchorMax) target.anchorMax = frame.anchorMax;
        if (target.pivot != frame.pivot) target.pivot = frame.pivot;
        if (target.sizeDelta != frame.sizeDelta) target.sizeDelta = frame.sizeDelta;
        if (target.anchoredPosition3D != frame.anchoredPosition3D) target.anchoredPosition3D = frame.anchoredPosition3D;
        if (target.localRotation != frame.localRotation) target.localRotation = frame.localRotation;
        if (target.localScale != frame.localScale) target.localScale = frame.localScale;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (bands == null) return;
        var rect = GetPixelAdjustedRect();
        Color32 tint = color;
        foreach (var band in bands)
        {
            int start = vh.currentVertCount;
            float left = rect.xMin + band.x * rect.width;
            float right = rect.xMin + band.y * rect.width;
            float bottom = rect.yMin + band.z * rect.height;
            float top = rect.yMin + band.w * rect.height;
            vh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            vh.AddVert(new Vector3(left, top), tint, Vector2.zero);
            vh.AddVert(new Vector3(right, top), tint, Vector2.zero);
            vh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
