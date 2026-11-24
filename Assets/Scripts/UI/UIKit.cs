using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Helper membangun UI lewat kode (tanpa prefab), supaya HUD jalan tanpa setup manual.
/// Gaya & warna dikumpulkan di sini, jadi gampang diganti saat art UI final dari artist masuk.
/// Resolusi referensi 1920x1080 (CanvasScaler).
/// </summary>
public static class UIKit
{
    // Palet "roh hutan": hijau gelap + krem + aksen emas
    public static readonly Color PanelColor = new Color(0.09f, 0.14f, 0.10f, 0.88f);
    public static readonly Color PanelLight = new Color(0.16f, 0.24f, 0.17f, 0.95f);
    public static readonly Color TextColor = new Color(0.96f, 0.93f, 0.83f);
    public static readonly Color TextMuted = new Color(0.75f, 0.78f, 0.68f);
    public static readonly Color Accent = new Color(1.00f, 0.82f, 0.30f);
    public static readonly Color Danger = new Color(0.95f, 0.33f, 0.28f);
    public static readonly Color Good = new Color(0.45f, 0.90f, 0.45f);
    public static readonly Color ButtonColor = new Color(0.24f, 0.36f, 0.24f, 1f);
    public static readonly Color ButtonHover = new Color(0.32f, 0.48f, 0.30f, 1f);

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Tombol UI butuh EventSystem. Scene Game belum punya, jadi dibuat otomatis.</summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>(); // project memakai Input System (bukan input lama)
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Atur anchor + pivot + posisi + ukuran sekaligus.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
        return rt;
    }

    public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
    {
        RectTransform rt = Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
        TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null, bool bold = false)
    {
        RectTransform rt = Rect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color ?? TextColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    public static Button Button(string name, Transform parent, string text, float fontSize, UnityAction onClick)
    {
        Image bg = Panel(name, parent, ButtonColor);
        Button button = bg.gameObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1.2f;
        button.colors = colors;

        if (onClick != null) button.onClick.AddListener(onClick);

        TextMeshProUGUI label = Label("Label", bg.transform, text, fontSize, TextAlignmentOptions.Center, TextColor, true);
        Stretch(label.rectTransform);
        return button;
    }

    /// <summary>Bar (HP dll). Return Image fill (pakai fillAmount 0..1).</summary>
    public static Image Bar(string name, Transform parent, Color background, Color fill, out RectTransform root)
    {
        Image bg = Panel(name, parent, background, false);
        root = bg.rectTransform;

        Image fillImage = Panel("Fill", bg.transform, fill, false);
        Stretch(fillImage.rectTransform, 2f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;
        // Image.Filled butuh sprite supaya fillAmount bekerja
        fillImage.sprite = WhiteSprite;
        return fillImage;
    }

    private static Sprite whiteSprite;

    /// <summary>Sprite putih 1x1 (dibutuhkan Image tipe Filled).</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                whiteSprite.name = "UIKit_White";
            }
            return whiteSprite;
        }
    }

    public static Color ColorOfTower(TowerColor color)
    {
        return PlacementPreview.GetTowerColor(color);
    }

    public static string Hex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
