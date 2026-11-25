using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprite 2D yang "berdiri" di dunia 3D (gaya 2.5D seperti Cult of the Lamb).
/// - Diputar SEJAJAR bidang kamera, jadi sprite selalu tampil utuh (tidak gepeng) walau kamera miring & di-zoom.
/// - Skala selalu seragam & PPU sama (64), jadi ukuran pixel tower & musuh konsisten.
/// - Supaya terasa MENAPAK (tidak mengambang):
///     * digambar sebagai cutout yang menulis depth -> helai rumput di depan kaki ikut menutupi,
///     * menjatuhkan bayangan siluet sungguhan dari cahaya matahari scene,
///     * bayangan kontak lembut tepat di kaki.
/// Material ada di Resources/Sprites2D (SpriteCutout = URP Unlit, SpriteShadowCaster = URP Lit).
/// Pivot sprite harus di tengah-bawah (sudah diatur di import setting sprite tower & musuh).
/// </summary>
public class SpriteBillboard : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowCaster;
    private Transform contactShadow;
    private Camera cam;
    private MaterialPropertyBlock block;
    private Color tint = Color.white;
    private bool translucent;

    // Skala (uniform) dengan transisi halus + efek "pop" saat merge
    private float targetScale = 1f;
    private float displayScale = 1f;
    private float popTimer;
    private float shadowSize = 0.9f;
    private BoxCollider clickCollider;

    private const float PopDuration = 0.35f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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

    /// <summary>Bayangan siluet dari cahaya matahari (jatuh ke tile).</summary>
    public static bool CastSilhouetteShadow = true;

    /// <summary>Bayangan bulat lembut di kaki.</summary>
    public static bool UseContactShadow = true;

    /// <summary>
    /// Geser sprite tower dari tengah tile. Z negatif = maju ke arah kamera (lebih dekat ke pemain).
    /// Y negatif = ditanam ke tile.
    /// </summary>
    public static readonly Vector3 TowerGroundOffset = new Vector3(0f, 0f, -0.2f);

    /// <summary>Geser sprite musuh dari titik berdiri (zero = berdiri tepat di permukaan tile).</summary>
    public static readonly Vector3 EnemyGroundOffset = Vector3.zero;

    /// <param name="grounded">true = karakter di map (cutout, menulis depth). false = bayangan preview (transparan).</param>
    /// <param name="groundOffset">
    /// Geser sprite dari titik berdiri. Y negatif = DITANAM ke dalam tile: bagian kaki tertutup permukaan tile
    /// (karena sprite menulis depth), jadi terlihat menancap, bukan menempel / melayang.
    /// </param>
    public static SpriteBillboard Create(Transform parent, Sprite sprite, bool grounded, Vector3 groundOffset = default, float shadowSize = 0.9f)
    {
        GameObject go = new GameObject("Sprite 2D");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = groundOffset;

        SpriteBillboard billboard = go.AddComponent<SpriteBillboard>();
        billboard.block = new MaterialPropertyBlock();
        billboard.spriteRenderer = go.AddComponent<SpriteRenderer>();
        billboard.spriteRenderer.sprite = sprite;
        billboard.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        billboard.spriteRenderer.receiveShadows = false;
        billboard.translucent = !grounded;
        billboard.shadowSize = shadowSize;

        if (grounded && CastSilhouetteShadow)
        {
            // Bayangan siluet sungguhan (renderer khusus, tidak terlihat, hanya menjatuhkan bayangan)
            Material shadowMat = SpriteMaterials.ShadowCaster;
            if (shadowMat != null)
            {
                GameObject s = new GameObject("Shadow Caster");
                s.transform.SetParent(go.transform, false);
                billboard.shadowCaster = s.AddComponent<SpriteRenderer>();
                billboard.shadowCaster.sprite = sprite;
                billboard.shadowCaster.sharedMaterial = shadowMat;
                billboard.shadowCaster.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                billboard.shadowCaster.receiveShadows = false;
            }
        }

        if (grounded && UseContactShadow)
        {
            // Bayangan kontak lembut di kaki
            GameObject c = new GameObject("Contact Shadow");
            c.transform.SetParent(parent, false);
            c.transform.localPosition = new Vector3(groundOffset.x, 0.012f, groundOffset.z); // ikut posisi kaki sprite
            c.transform.localScale = new Vector3(shadowSize, 1f, shadowSize * 0.55f);
            c.AddComponent<MeshFilter>().sharedMesh = TowerVfx.FlatQuad;
            MeshRenderer mr = c.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ContactShadowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            billboard.contactShadow = c.transform;
        }

        billboard.ApplyLook();
        billboard.FaceCamera();
        return billboard;
    }

    /// <summary>
    /// Apakah sinar (dari kursor) mengenai pixel sprite yang TERGAMBAR (bukan area transparan di sekitarnya).
    /// Memakai mesh "Tight" sprite yang mengikuti bentuk pixel, dan ikut dibalik kalau sprite di-flip.
    /// </summary>
    public bool RaycastSprite(Ray ray, out float distance)
    {
        distance = 0f;
        Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite == null) return false;

        Plane plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out float enter) || enter <= 0f) return false;

        Vector3 local = transform.InverseTransformPoint(ray.GetPoint(enter));
        if (spriteRenderer.flipX) local.x = -local.x;
        Vector2 p = new Vector2(local.x, local.y);

        Vector2[] v = sprite.vertices;
        ushort[] tri = sprite.triangles;
        for (int i = 0; i + 2 < tri.Length; i += 3)
        {
            if (InTriangle(p, v[tri[i]], v[tri[i + 1]], v[tri[i + 2]]))
            {
                distance = enter;
                return true;
            }
        }
        return false;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    /// <summary>
    /// Posisi dunia dari titik pixel di gambar (dihitung dari pivot tengah-bawah, untuk gambar asli tanpa dibalik).
    /// Ikut sisi hadap (flip), skala, dan kemiringan billboard. Dipakai untuk titik keluar peluru (moncong).
    /// </summary>
    public Vector3 PixelToWorld(Vector2 pixelOffset)
    {
        Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        float ppu = sprite != null ? sprite.pixelsPerUnit : 64f;
        float x = pixelOffset.x / ppu * (spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f);
        return transform.TransformPoint(new Vector3(x, pixelOffset.y / ppu, 0f));
    }

    /// <summary>
    /// Hadapkan sprite ke kiri / kanan (dibalik horizontal). artFacesRight = arah hadap gambar aslinya.
    /// </summary>
    public void SetFacing(bool faceRight, bool artFacesRight)
    {
        bool flip = faceRight != artFacesRight;
        if (spriteRenderer.flipX == flip) return;
        spriteRenderer.flipX = flip;
        if (shadowCaster != null) shadowCaster.flipX = flip;
    }

    public void SetSprite(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
        if (shadowCaster != null) shadowCaster.sprite = sprite;
        ApplyLook();
        FitClickCollider();
    }

    /// <summary>
    /// Skala sprite (selalu seragam, jadi pixel art tidak gepeng).
    /// instant = langsung (preview); pop = efek membesar sesaat (merge naik tier).
    /// </summary>
    public void SetScale(float scale, bool instant = false, bool pop = false)
    {
        targetScale = Mathf.Max(0.1f, scale);
        if (instant) displayScale = targetScale;
        if (pop) popTimer = PopDuration;
        ApplyScale();
    }

    /// <summary>
    /// Collider klik yang mengikuti bentuk & kemiringan sprite (bukan kapsul tegak),
    /// supaya bagian atas sprite yang condong ke belakang tetap bisa diklik / di-drag.
    /// </summary>
    public void EnableClickCollider()
    {
        if (clickCollider == null) clickCollider = gameObject.AddComponent<BoxCollider>();
        FitClickCollider();
    }

    private void FitClickCollider()
    {
        if (clickCollider == null || spriteRenderer.sprite == null) return;

        Bounds b = spriteRenderer.sprite.bounds; // ruang lokal sprite (sudah termasuk pivot di kaki)
        clickCollider.center = new Vector3(b.center.x, b.center.y, 0f);
        clickCollider.size = new Vector3(b.size.x * 0.8f, b.size.y, 0.1f);
    }

    private void ApplyScale()
    {
        float pop = popTimer > 0f ? Mathf.Sin((1f - popTimer / PopDuration) * Mathf.PI) * 0.18f : 0f;
        float s = displayScale * (1f + pop);
        transform.localScale = new Vector3(s, s, s);

        if (contactShadow != null)
        {
            contactShadow.localScale = new Vector3(shadowSize * displayScale, 1f, shadowSize * 0.55f * displayScale);
        }
    }

    /// <summary>Tint warna (kebal, diperlambat, preview). Alpha &lt; 1 otomatis memakai mode transparan.</summary>
    public void SetColor(Color color)
    {
        tint = color;
        ApplyLook();
    }

    private void ApplyLook()
    {
        Sprite sprite = spriteRenderer.sprite;
        Material cutout = SpriteMaterials.Cutout;
        bool useCutout = !translucent && tint.a >= 0.99f && cutout != null && sprite != null;

        if (useCutout)
        {
            // URP Unlit membaca _BaseMap (bukan _MainTex milik SpriteRenderer), jadi teksturnya diisi manual
            spriteRenderer.sharedMaterial = cutout;
            spriteRenderer.color = Color.white;
            spriteRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, sprite.texture);
            block.SetColor(BaseColorId, tint);
            spriteRenderer.SetPropertyBlock(block);
        }
        else
        {
            spriteRenderer.sharedMaterial = SpriteMaterials.DefaultSprite;
            spriteRenderer.SetPropertyBlock(null);
            spriteRenderer.color = tint;
        }

        if (shadowCaster != null && sprite != null)
        {
            shadowCaster.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, sprite.texture);
            block.SetColor(BaseColorId, Color.white);
            shadowCaster.SetPropertyBlock(block);
        }
    }

    private void LateUpdate()
    {
        FaceCamera();

        float dt = Time.deltaTime;
        if (popTimer > 0f) popTimer = Mathf.Max(0f, popTimer - dt);
        if (!Mathf.Approximately(displayScale, targetScale) || popTimer > 0f)
        {
            displayScale = Mathf.MoveTowards(displayScale, targetScale, dt * 2.5f);
            ApplyScale();
        }
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
        if (contactShadow != null) Destroy(contactShadow.gameObject);
    }

    // ------------------------------------------------------------------
    // Material & sprite placeholder (dicek ulang karena Domain Reload dimatikan)
    // ------------------------------------------------------------------

    private static Material contactShadowMaterial;
    private static Texture2D contactShadowTexture;

    private static Material ContactShadowMaterial
    {
        get
        {
            if (contactShadowMaterial == null)
            {
                if (contactShadowTexture == null)
                {
                    const int size = 32;
                    contactShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ContactShadow", wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2f - 1f;
                            float dy = (y + 0.5f) / size * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            // Inti gelap yang tegas di tengah, pinggir memudar
                            float a = Mathf.Clamp01(1f - d);
                            a = Mathf.SmoothStep(0f, 1f, a);
                            contactShadowTexture.SetPixel(x, y, new Color(0f, 0f, 0f, a * 0.75f));
                        }
                    }
                    contactShadowTexture.Apply();
                }

                contactShadowMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "ContactShadow",
                    mainTexture = contactShadowTexture,
                    renderQueue = TowerVfx.GroundDecalQueue + 5
                };
            }
            return contactShadowMaterial;
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

/// <summary>Material sprite dari Resources/Sprites2D (dimuat sekali).</summary>
public static class SpriteMaterials
{
    private static Material cutout;
    private static Material shadowCaster;
    private static Material defaultSprite;

    /// <summary>URP Unlit + alpha clip: warna pixel art apa adanya, tapi menulis depth.</summary>
    public static Material Cutout => cutout != null ? cutout : (cutout = Resources.Load<Material>("Sprites2D/SpriteCutout"));

    /// <summary>URP Lit + alpha clip: dipakai renderer ShadowsOnly untuk bayangan siluet.</summary>
    public static Material ShadowCaster => shadowCaster != null ? shadowCaster : (shadowCaster = Resources.Load<Material>("Sprites2D/SpriteShadowCaster"));

    /// <summary>Material sprite transparan bawaan (dipakai preview bayangan tower).</summary>
    public static Material DefaultSprite
    {
        get
        {
            if (defaultSprite == null)
            {
                defaultSprite = new Material(Shader.Find("Sprites/Default")) { name = "SpriteDefault" };
            }
            return defaultSprite;
        }
    }
}
