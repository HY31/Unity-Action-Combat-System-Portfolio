using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crops only the fixed right end of a gauge. Does not resize its RectTransform,
/// remap its texture, or change fillAmount / the moving health and energy edge.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class PlayerHudRightEdgeClip : BaseMeshEffect
{
    [SerializeField, Min(0f), Tooltip("Right-end crop in this Image's local UI units. Does not scale the image.")]
    private float rightInset;

    private readonly List<UIVertex> input = new List<UIVertex>(6);
    private readonly List<UIVertex> output = new List<UIVertex>(12);
    private readonly UIVertex[] polygon = new UIVertex[4];

    public float RightInset
    {
        get => rightInset;
        set
        {
            rightInset = Mathf.Max(0f, value);
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public static void Configure(Image image, float inset)
    {
        if (image == null) return;
        var clip = image.GetComponent<PlayerHudRightEdgeClip>();
        if (clip == null) clip = image.gameObject.AddComponent<PlayerHudRightEdgeClip>();
        clip.RightInset = inset;
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || rightInset <= 0f || vertices.currentVertCount == 0) return;

        // Use the FULL rect, not the current filled mesh: partial fills must not
        // lose pixels at their moving edge when they are below this fixed limit.
        float right = graphic.GetPixelAdjustedRect().xMax - rightInset;
        UIVertex vertex = default;
        bool needsCrop = false;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            if (vertex.position.x > right) { needsCrop = true; break; }
        }
        if (!needsCrop) return;

        input.Clear();
        output.Clear();
        vertices.GetUIVertexStream(input);
        for (int i = 0; i + 2 < input.Count; i += 3)
        {
            int count = 0;
            UIVertex previous = input[i + 2];
            bool previousInside = previous.position.x <= right;
            for (int j = 0; j < 3; j++)
            {
                UIVertex current = input[i + j];
                bool currentInside = current.position.x <= right;
                if (currentInside != previousInside)
                {
                    float t = (right - previous.position.x) /
                              (current.position.x - previous.position.x);
                    polygon[count++] = Interpolate(previous, current, t, right);
                }
                if (currentInside) polygon[count++] = current;
                previous = current;
                previousInside = currentInside;
            }
            for (int j = 1; j + 1 < count; j++)
            {
                output.Add(polygon[0]);
                output.Add(polygon[j]);
                output.Add(polygon[j + 1]);
            }
        }
        vertices.Clear();
        vertices.AddUIVertexTriangleStream(output);
    }

    private static UIVertex Interpolate(UIVertex a, UIVertex b, float t, float right)
    {
        var vertex = new UIVertex
        {
            position = Vector3.LerpUnclamped(a.position, b.position, t),
            color = Color32.Lerp(a.color, b.color, t),
            uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t),
            uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t),
            uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t),
            uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t),
            normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
            tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t)
        };
        vertex.position.x = right;
        return vertex;
    }
}
