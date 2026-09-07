using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>숫자와 작은 PTS를 한 덩어리로 연결하는 둥근 검은 실루엣.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DecibelSilhouetteGraphic : MaskableGraphic
{
    private readonly List<Vector2> perimeter = new List<Vector2>(54);
    private readonly Vector2[] corners = new Vector2[6];
    private Vector2 center;
    private bool hasShape;

    public void FitToText(TMP_Text label, TMP_TextInfo textInfo)
    {
        if (textInfo.characterCount < 7)
            return;

        // Un-shear the glyph quads first, then shear the shared outline by the
        // same amount. Its sides stay parallel to the text at every font size.
        TMP_CharacterInfo first = textInfo.characterInfo[0];
        float shear = (first.topLeft.x - first.bottomLeft.x)
            / Mathf.Max(0.001f, first.topLeft.y - first.bottomLeft.y);
        float sdfPadding = ShaderUtilities.GetPadding(label.fontSharedMaterial, label.extraPadding, false);
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float join = float.NegativeInfinity;
        float bottom = float.PositiveInfinity;
        float numberTop = float.NegativeInfinity;
        float ptsTop = float.NegativeInfinity;
        for (int i = 0; i < 7; i++)
        {
            TMP_CharacterInfo glyph = textInfo.characterInfo[i];
            float padding = sdfPadding * glyph.scale;
            float xMin = glyph.bottomLeft.x - shear * glyph.bottomLeft.y + padding;
            float xMax = glyph.topRight.x - shear * glyph.topRight.y - padding;
            float yMin = glyph.bottomLeft.y + padding;
            float yMax = glyph.topLeft.y - padding;
            left = Mathf.Min(left, xMin);
            right = Mathf.Max(right, xMax);
            bottom = Mathf.Min(bottom, yMin);
            if (i < 4)
            {
                join = Mathf.Max(join, xMax);
                numberTop = Mathf.Max(numberTop, yMax);
            }
            else
                ptsTop = Mathf.Max(ptsTop, yMax);
        }

        float border = label.fontSize * 0.07f;
        left -= border;
        right += border;
        join += border;
        bottom -= border;
        numberTop += border;
        ptsTop += border;
        corners[0] = new Vector2(left, bottom);
        corners[1] = new Vector2(right, bottom);
        corners[2] = new Vector2(right, ptsTop);
        corners[3] = new Vector2(join, ptsTop);
        corners[4] = new Vector2(join, numberTop);
        corners[5] = new Vector2(left, numberTop);

        perimeter.Clear();
        float radius = border * 1.25f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 corner = corners[i];
            Vector2 before = corners[(i + corners.Length - 1) % corners.Length] - corner;
            Vector2 after = corners[(i + 1) % corners.Length] - corner;
            float trim = Mathf.Min(radius, before.magnitude * 0.45f, after.magnitude * 0.45f);
            Vector2 start = corner + before.normalized * trim;
            Vector2 end = corner + after.normalized * trim;
            for (int segment = 0; segment <= 8; segment++)
            {
                float t = segment / 8f;
                Vector2 point = (1f - t) * (1f - t) * start
                    + 2f * (1f - t) * t * corner + t * t * end;
                point.x += shear * point.y;
                perimeter.Add(point);
            }
        }

        // This point lies in the kernel of the stepped polygon, so the fan
        // also fills the concave PTS join without gaps or overlapping letters.
        center = new Vector2((left + join) * 0.5f, (bottom + ptsTop) * 0.5f);
        center.x += shear * center.y;
        hasShape = true;
        if (CanvasUpdateRegistry.IsRebuildingGraphics())
            UpdateGeometry();
        else
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!hasShape)
            return;

        Color32 black = new Color32(0, 0, 0, 255);
        Color32 transparent = new Color32(0, 0, 0, 0);
        mesh.AddVert(center, black, Vector2.zero);
        int count = perimeter.Count;
        for (int i = 0; i < count; i++)
            mesh.AddVert(perimeter[i], black, Vector2.zero);

        // A narrow transparent fringe keeps the silhouette smooth even on
        // overlay canvases that do not receive camera MSAA.
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        float pixelsPerUnit = rootCanvas != null
            ? rootCanvas.scaleFactor * Mathf.Abs(rectTransform.lossyScale.y
                / rootCanvas.transform.lossyScale.y) : 1f;
        float feather = 0.75f / Mathf.Max(0.01f, pixelsPerUnit);
        for (int i = 0; i < count; i++)
        {
            Vector2 incoming = (perimeter[i] - perimeter[(i + count - 1) % count]).normalized;
            Vector2 outgoing = (perimeter[(i + 1) % count] - perimeter[i]).normalized;
            Vector2 normal = new Vector2(incoming.y + outgoing.y, -incoming.x - outgoing.x).normalized;
            mesh.AddVert(perimeter[i] + normal * feather, transparent, Vector2.zero);
        }
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            mesh.AddTriangle(0, 1 + i, 1 + next);
            mesh.AddTriangle(1 + i, 1 + count + i, 1 + count + next);
            mesh.AddTriangle(1 + i, 1 + count + next, 1 + next);
        }
    }
}
