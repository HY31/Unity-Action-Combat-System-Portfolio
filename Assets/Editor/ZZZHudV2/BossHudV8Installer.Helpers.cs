using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class BossHudV8Installer
{
    private static void ConfigureSpriteImporters()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { SpriteRoot });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void BindAssaultHud(GameObject root, BossHudView view)
    {
        AssaultBattleHUD hud = root.GetComponent<AssaultBattleHUD>();
        if (hud == null)
            throw new MissingComponentException(nameof(AssaultBattleHUD));

        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("bossHealthFill").objectReferenceValue =
            view.HealthFill;
        serializedHud.FindProperty("bossStunFill").objectReferenceValue =
            view.StunFill;
        serializedHud.FindProperty("bossHealthText").objectReferenceValue = null;
        serializedHud.FindProperty("bossStunText").objectReferenceValue =
            view.StunText;
        serializedHud.FindProperty("bossHealthColor").colorValue = Color.white;
        serializedHud.FindProperty("bossStunColor").colorValue = Color.white;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite LoadSprite(string name)
    {
        string path = $"{SpriteRoot}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new MissingReferenceException(path);
        return sprite;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        gameObject.AddComponent<CanvasRenderer>();
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateFillImage(
        string name,
        Transform parent,
        Sprite sprite)
    {
        Image image = CreateImage(name, parent, sprite);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Right;
        image.fillClockwise = true;
        image.fillAmount = 1f;
        return image;
    }

    private static Text CreateStunText(Transform parent)
    {
        GameObject gameObject = CreateUiObject("StunText", parent);
        gameObject.AddComponent<CanvasRenderer>();

        Text text = gameObject.AddComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        text.text = "00";
        text.fontSize = 35;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 138, 23, 255);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.025f, 0.03f, 1f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        BossHudTextShear shear = gameObject.AddComponent<BossHudTextShear>();
        shear.HorizontalShear = 0.364f;

        SetCentered(
            gameObject.GetComponent<RectTransform>(),
            new Vector2(58f, 46f),
            new Vector2(-353f, 8.5f));
        return text;
    }

    private static void SetTopRight(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
