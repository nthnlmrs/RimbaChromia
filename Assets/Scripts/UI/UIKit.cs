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
        if (MainFont != null) tmp.font = MainFont;
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

    // ------------------------------------------------------------------
    // Font utama UI: Jersey 25
    // ------------------------------------------------------------------

    /// <summary>Lokasi font di Resources (tanpa ekstensi): Assets/Resources/Fonts/Jersey25-Regular.ttf</summary>
    public const string MainFontResource = "Fonts/Jersey25-Regular";

    private static TMP_FontAsset mainFont;
    private static bool mainFontMissing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFontOnPlay()
    {
        mainFontMissing = false; // cek ulang tiap Play (mungkin file font baru ditambahkan)
    }

    /// <summary>
    /// Font utama semua teks UI. Dibuat otomatis saat runtime dari file TTF di Resources,
    /// jadi tidak perlu membuat TMP Font Asset manual. Kalau file belum ada, pakai font default TMP.
    /// </summary>
    public static TMP_FontAsset MainFont
    {
        get
        {
            if (mainFont != null || mainFontMissing) return mainFont;

            Font source = Resources.Load<Font>(MainFontResource);
            if (source == null)
            {
                mainFontMissing = true;
                Debug.LogWarning($"Font UI '{MainFontResource}' tidak ditemukan di Resources. Pakai font default TMP.");
                return null;
            }

            mainFont = TMP_FontAsset.CreateFontAsset(source);
            if (mainFont == null)
            {
                mainFontMissing = true;
                return null;
            }

            mainFont.name = "Jersey25 (runtime)";

            // Huruf yang tidak ada di Jersey 25 (misal simbol khusus) diambil dari font default
            if (TMP_Settings.defaultFontAsset != null)
            {
                mainFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            }
            return mainFont;
        }
    }

    // ------------------------------------------------------------------
    // Elemen berbasis art dari artist (UISkin)
    // ------------------------------------------------------------------

    /// <summary>Skala art UI terhadap canvas 1920x1080 (mockup artist dibuat di kanvas ~1440 px).</summary>
    public const float ArtScale = 0.85f;

    /// <summary>Warna angka di mockup (krem) + outline coklat tua.</summary>
    public static readonly Color ArtText = new Color(0.86f, 0.77f, 0.64f);
    public static readonly Color ArtOutline = new Color(0.16f, 0.08f, 0.04f);

    /// <summary>Gambar art dengan ukuran asli sprite x skala.</summary>
    public static Image ArtImage(string name, Transform parent, Sprite sprite, float scale = ArtScale, bool raycast = false)
    {
        RectTransform rt = Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = raycast;
        img.preserveAspect = true;
        if (sprite != null) rt.sizeDelta = sprite.rect.size * scale;
        return img;
    }

    /// <summary>Tombol art: gambar normal, ganti ke gambar "ditekan" saat diklik (SpriteSwap).</summary>
    public static Button ArtButton(string name, Transform parent, Sprite normal, Sprite pressed, float scale, UnityAction onClick)
    {
        Image img = ArtImage(name, parent, normal, scale, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        if (pressed != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = new SpriteState
            {
                highlightedSprite = normal,
                pressedSprite = pressed,
                selectedSprite = normal,
                disabledSprite = pressed
            };
            button.spriteState = states;
        }
        else
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    /// <summary>Teks bergaya art (angka insight, HP wall, dll): Jersey 25 krem dengan outline gelap.</summary>
    public static TextMeshProUGUI ArtLabel(string name, Transform parent, string text, float size,
        TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
    {
        TextMeshProUGUI tmp = Label(name, parent, text, size, align, color ?? ArtText);
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = ArtOutline;
        return tmp;
    }

    /// <summary>Taruh anak di posisi pixel art induknya (koordinat dari KIRI-ATAS gambar induk, seperti di file PNG).</summary>
    public static RectTransform PlaceOnArt(RectTransform rt, Vector2 pixelCenter, Vector2 pixelSize, float scale = ArtScale)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(pixelCenter.x * scale, -pixelCenter.y * scale);
        rt.sizeDelta = pixelSize * scale;
        return rt;
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
