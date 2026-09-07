using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>데시벨 숫자와 PTS의 표시만 담당한다. 전투 자원 값은 변경하지 않는다.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class DecibelHudText : MonoBehaviour
{
    [SerializeField, Range(0, 3000)] private int displayedValue;
    [SerializeField] private TMP_FontAsset displayFont;

    private const string FontPath = "Fonts/BarlowCondensed/BarlowCondensed-Black";
    private static TMP_FontAsset hudFont;
    private TMP_Text label;
    private DecibelSilhouetteGraphic silhouette;
    private int lastRenderedValue = -1;
    private Color32 leftColor;
    private Color32 rightColor;

    public int DisplayedValue => displayedValue;

    public void ConfigureFont(TMP_FontAsset font)
    {
        displayFont = font;
        if (label != null) ConfigureAppearance();
    }

    private void OnEnable()
    {
        label = GetComponent<TMP_Text>();
        ConfigureAppearance();
        EnsureSilhouette();
        label.OnPreRenderText -= ApplyContinuousGradient;
        label.OnPreRenderText += ApplyContinuousGradient;
        lastRenderedValue = -1;
        SetValue(displayedValue);
    }

    private void OnDisable()
    {
        if (label != null)
            label.OnPreRenderText -= ApplyContinuousGradient;
        if (silhouette != null)
        {
            GameObject generated = silhouette.gameObject;
            silhouette.enabled = false;
            silhouette = null;
            if (Application.isPlaying) Destroy(generated);
#if UNITY_EDITOR
            else UnityEditor.EditorApplication.delayCall += () =>
            {
                if (generated != null) DestroyImmediate(generated);
            };
#endif
        }
    }

    private void LateUpdate()
    {
        SynchronizeSilhouetteTransform();
    }

    private void EnsureSilhouette()
    {
        if (silhouette == null)
        {
            GameObject generated = new GameObject("DecibelSharedSilhouette", typeof(RectTransform),
                typeof(UnityEngine.UI.LayoutElement), typeof(DecibelSilhouetteGraphic));
            generated.hideFlags = HideFlags.HideAndDontSave;
            generated.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            silhouette = generated.GetComponent<DecibelSilhouetteGraphic>();
            silhouette.raycastTarget = false;
        }
        SynchronizeSilhouetteTransform();
    }

    private void SynchronizeSilhouetteTransform()
    {
        if (silhouette == null || label == null)
            return;
        RectTransform source = label.rectTransform;
        RectTransform target = silhouette.rectTransform;
        if (target.parent != source.parent) target.SetParent(source.parent, false);
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        int textIndex = source.GetSiblingIndex();
        if (target.GetSiblingIndex() != textIndex - 1)
            target.SetSiblingIndex(target.GetSiblingIndex() < textIndex ? textIndex - 1 : textIndex);
        silhouette.enabled = label.enabled;
    }

    private void OnValidate()
    {
        displayedValue = Mathf.Clamp(displayedValue, 0, 3000);
        lastRenderedValue = -1;
        if (label != null)
            SetValue(displayedValue);
    }

    private void ConfigureAppearance()
    {
        if (displayFont == null && hudFont == null)
        {
            Font source = Resources.Load<Font>(FontPath);
            if (source != null)
            {
                // 충분한 SDF 여백을 확보해 검은 외곽선 끝이 잘리지 않게 한다.
                hudFont = TMP_FontAsset.CreateFontAsset(
                    source, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024);
                hudFont.name = "Decibel Barlow Condensed Black SDF";
                hudFont.TryAddCharacters("0123456789PTS");
            }
        }

        TMP_FontAsset selectedFont = displayFont != null ? displayFont : hudFont;
        if (selectedFont != null)
        {
            label.font = selectedFont;
            label.fontSharedMaterial = selectedFont.material;
        }

        label.fontStyle = FontStyles.Italic;
        label.fontWeight = FontWeight.Black;
        label.characterSpacing = -1.5f;
        label.richText = true;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = Color.white;
        label.enableVertexGradient = false;
        // The black outline is one shared silhouette, not separate glyph strokes.
        // Keep the saved font/material references persistent. fontMaterial and
        // the outline setters would create unsaved per-instance materials.
        Material material = label.fontSharedMaterial;
        material.DisableKeyword("OUTLINE_ON");
        material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        label.extraPadding = true;
        label.raycastTarget = false;
        label.UpdateMeshPadding();
    }

    public void SetValue(float value)
    {
        displayedValue = Mathf.Clamp(Mathf.FloorToInt(value), 0, 3000);
        if (label == null || displayedValue == lastRenderedValue)
            return;

        lastRenderedValue = displayedValue;
        ResolveColors(displayedValue, out leftColor, out rightColor);
        // 같은 baseline을 사용하므로 60% 크기의 PTS는 숫자 오른쪽 아래에 붙는다.
        label.text = displayedValue.ToString("0000") + "<space=0.08em><size=60%>PTS</size>";
        label.SetVerticesDirty();
    }

    public static void ResolveColors(int value, out Color32 left, out Color32 right)
    {
        if (value >= 3000)
        {
            left = right = new Color32(255, 128, 12, 255);
        }
        else if (value >= 2000)
        {
            left = new Color32(119, 235, 34, 255);
            right = new Color32(233, 246, 42, 255);
        }
        else if (value >= 1000)
        {
            left = new Color32(26, 184, 242, 255);
            right = new Color32(29, 235, 155, 255);
        }
        else
        {
            left = right = new Color32(205, 211, 212, 255);
        }
    }

    private void ApplyContinuousGradient(TMP_TextInfo textInfo)
    {
        if (silhouette != null)
        {
            SynchronizeSilhouetteTransform();
            silhouette.FitToText(label, textInfo);
        }
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible)
                continue;

            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = vertices[character.vertexIndex + corner].x;
                minimumX = Mathf.Min(minimumX, x);
                maximumX = Mathf.Max(maximumX, x);
            }
        }

        if (float.IsInfinity(minimumX))
            return;

        float width = Mathf.Max(0.001f, maximumX - minimumX);
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible)
                continue;

            TMP_MeshInfo mesh = textInfo.meshInfo[character.materialReferenceIndex];
            for (int corner = 0; corner < 4; corner++)
            {
                int vertex = character.vertexIndex + corner;
                float t = (mesh.vertices[vertex].x - minimumX) / width;
                mesh.colors32[vertex] = Color32.Lerp(leftColor, rightColor, t);
            }
        }
    }
}
