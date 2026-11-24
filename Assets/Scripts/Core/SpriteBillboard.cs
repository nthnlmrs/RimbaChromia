using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprite 2D yang "berdiri" di dunia 3D (gaya 2.5D seperti Cult of the Lamb).
/// - Diputar SEJAJAR bidang kamera (bukan tegak lurus tanah), jadi sprite selalu tampil utuh,
///   tidak gepeng walau kamera miring & di-zoom.
/// - Skala selalu seragam (1:1) dan PPU sama (64), jadi ukuran pixel tower & musuh konsisten.
/// - Bayangan bulat lembut di kaki supaya karakter terasa menapak di tile 3D.
/// Pivot sprite harus di tengah-bawah (sudah diatur di import setting sprite tower & musuh).
/// </summary>
public class SpriteBillboard : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Transform shadow;
    private Camera cam;

    public SpriteRenderer Renderer => spriteRenderer;

    /// <summary>Tinggi sprite di dunia (sudah memperhitungkan kemiringan kamera).</summary>
    public float VisualHeight
    {
        get
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return 0.5f;
            float tilt = cam != null ? Mathf.Cos(cam.transform.eulerAngles.x * Mathf.Deg2Rad) : 1f;
            return spriteRenderer.sprite.bounds.size.y * transform.lossyScale.y * tilt;
        }
    }

    public static SpriteBillboard Create(Transform parent, Sprite sprite, bool withShadow, float shadowSize = 0.7f)
    {
        GameObject go = new GameObject("Sprite 2D");
        go.transform.SetParent(parent, false);

        SpriteBillboard billboard = go.AddComponent<SpriteBillboard>();
        billboard.spriteRenderer = go.AddComponent<SpriteRenderer>();
        billboard.spriteRenderer.sprite = sprite;
        billboard.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        billboard.spriteRenderer.receiveShadows = false;

        if (withShadow)
        {
            GameObject s = new GameObject("Shadow");
            s.transform.SetParent(parent, false);
            s.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            s.transform.localScale = new Vector3(shadowSize, 1f, shadowSize * 0.6f);
            s.AddComponent<MeshFilter>().sharedMesh = TowerVfx.FlatQuad;
            MeshRenderer mr = s.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ShadowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            billboard.shadow = s.transform;
        }

        billboard.FaceCamera();
        return billboard;
    }

    public void SetSprite(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        spriteRenderer.color = color;
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Sejajar bidang kamera -> tidak ada foreshortening (tidak gepeng)
        transform.rotation = cam.transform.rotation;
    }

    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    // ------------------------------------------------------------------
    // Material & sprite placeholder (dibuat sekali, dicek ulang karena Domain Reload dimatikan)
    // ------------------------------------------------------------------

    private static Material shadowMaterial;
    private static Texture2D shadowTexture;

    private static Material ShadowMaterial
    {
        get
        {
            if (shadowMaterial == null)
            {
                if (shadowTexture == null)
                {
                    const int size = 32;
                    shadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BlobShadow", wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2f - 1f;
                            float dy = (y + 0.5f) / size * 2f - 1f;
                            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                            shadowTexture.SetPixel(x, y, new Color(0f, 0f, 0f, a * a * 0.55f));
                        }
                    }
                    shadowTexture.Apply();
                }

                shadowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BlobShadow", mainTexture = shadowTexture, renderQueue = TowerVfx.GroundDecalQueue + 5 };
            }
            return shadowMaterial;
        }
    }

    private static readonly Dictionary<string, Sprite> placeholderCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// Sprite placeholder pixel-art (badan bulat berwarna + outline + mata) untuk karakter yang belum punya art.
    /// widthPx / heightPx dalam pixel pada 64 PPU, jadi skalanya konsisten dengan sprite asli.
    /// </summary>
    public static Sprite Placeholder(Color color, int widthPx, int heightPx)
    {
        string key = $"{ColorUtility.ToHtmlStringRGBA(color)}_{widthPx}x{heightPx}";
        if (placeholderCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D tex = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Placeholder_" + key
        };

        Color outline = Color.Lerp(color, Color.black, 0.65f);
        Color light = Color.Lerp(color, Color.white, 0.35f);
        Color clear = new Color(0f, 0f, 0f, 0f);
        float rx = widthPx / 2f, ry = heightPx / 2f;

        for (int y = 0; y < heightPx; y++)
        {
            for (int x = 0; x < widthPx; x++)
            {
                // Bentuk kapsul: elips dengan sudut membulat
                float nx = (x + 0.5f - rx) / rx;
                float ny = (y + 0.5f - ry) / ry;
                float d = Mathf.Pow(Mathf.Abs(nx), 2.6f) + Mathf.Pow(Mathf.Abs(ny), 2.6f);
                Color c = clear;
                if (d <= 1f)
                {
                    float edge = 1f - 2.6f / Mathf.Min(widthPx, heightPx);
                    c = d > edge ? outline : (ny > 0.35f && nx < 0.1f ? light : color);
                }
                tex.SetPixel(x, y, c);
            }
        }

        // Dua mata kecil supaya terbaca sebagai karakter
        int eyeY = Mathf.RoundToInt(heightPx * 0.62f);
        int eyeL = Mathf.RoundToInt(widthPx * 0.36f), eyeR = Mathf.RoundToInt(widthPx * 0.64f) - 1;
        Color eye = new Color(1f, 0.95f, 0.6f);
        tex.SetPixel(eyeL, eyeY, eye); tex.SetPixel(eyeR, eyeY, eye);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, widthPx, heightPx), new Vector2(0.5f, 0f), 64f);
        sprite.name = tex.name;
        placeholderCache[key] = sprite;
        return sprite;
    }
}
