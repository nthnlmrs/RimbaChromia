using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efek serangan bergaya PIXEL ART yang dibuat lewat kode (tanpa aset tambahan), supaya senada dengan sprite tower.
/// Ukuran pixel efek disamakan dengan ukuran pixel sprite tower di layar (PixelsPerUnit).
/// Kalau nanti artist membuat sprite sheet efek, cukup ganti generator di PixelArt dengan sprite dari artist.
/// </summary>
public static class PixelArt
{
    /// <summary>~ ukuran pixel sprite tower (64 PPU x skala tower 1.25).</summary>
    public const float PixelsPerUnit = 51f;

    private static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();

    private static T Cached<T>(string key, System.Func<T> create) where T : Object
    {
        if (cache.TryGetValue(key, out Object existing) && existing != null) return (T)existing;
        T made = create();
        cache[key] = made;
        return made;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] fill = new Color[w * h];
        for (int i = 0; i < fill.Length; i++) fill[i] = clear;
        t.SetPixels(fill);
        return t;
    }

    private static Sprite ToSprite(Texture2D t)
    {
        t.Apply();
        Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        s.name = t.name;
        return s;
    }

    private static void Disc(Texture2D t, float cx, float cy, float r, Color c)
    {
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) t.SetPixel(x, y, c);
    }

    private static Color Dark(Color c, float k) => new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a);
    private static Color Light(Color c, float k) => Color.Lerp(c, Color.white, k);

    // ------------------------------------------------------------------
    // Sprite efek
    // ------------------------------------------------------------------

    /// <summary>Bola pixel 9x9 dengan outline gelap, inti terang, dan 1 pixel kilap.</summary>
    public static Sprite Orb(Color color)
    {
        return Cached("orb" + ColorUtility.ToHtmlStringRGB(color), () =>
        {
            Texture2D t = NewTexture(9, 9, "Pixel Orb");
            Disc(t, 4.5f, 4.5f, 4.5f, Dark(color, 0.55f));
            Disc(t, 4.5f, 4.5f, 3.5f, color);
            Disc(t, 4.0f, 5.0f, 2.0f, Light(color, 0.45f));
            t.SetPixel(3, 6, Color.white);
            return ToSprite(t);
        });
    }

    /// <summary>Kotak pixel solid (percikan, tetesan). Warna diatur lewat SpriteRenderer.color.</summary>
    public static Sprite Square(int size = 2)
    {
        return Cached("square" + size, () =>
        {
            Texture2D t = NewTexture(size, size, "Pixel Square");
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, Color.white);
            return ToSprite(t);
        });
    }

    /// <summary>Kilau bintang 7x7 (tanda plus dengan inti terang).</summary>
    public static Sprite Sparkle(Color color)
    {
        return Cached("sparkle" + ColorUtility.ToHtmlStringRGB(color), () =>
        {
            Texture2D t = NewTexture(7, 7, "Pixel Sparkle");
            for (int i = 0; i < 7; i++)
            {
                t.SetPixel(3, i, color);
                t.SetPixel(i, 3, color);
            }
            t.SetPixel(2, 2, Dark(color, 0.2f)); t.SetPixel(4, 4, Dark(color, 0.2f));
            t.SetPixel(2, 4, Dark(color, 0.2f)); t.SetPixel(4, 2, Dark(color, 0.2f));
            t.SetPixel(3, 3, Color.white);
            return ToSprite(t);
        });
    }

    public enum BurstStyle { Fire, Water, Smoke, Magic }

    /// <summary>Animasi ledakan / cipratan 5 frame (24x24) dengan dithering ala pixel art.</summary>
    private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

    public static Sprite[] Burst(BurstStyle style, Color tint)
    {
        string key = "burst" + style + ColorUtility.ToHtmlStringRGB(tint);
        if (frameCache.TryGetValue(key, out Sprite[] existing) && existing[0] != null) return existing;
        {
            const int size = 24;
            Color core = style == BurstStyle.Fire ? new Color(1f, 0.95f, 0.65f) : Light(tint, 0.7f);
            Color mid = style == BurstStyle.Fire ? new Color(1f, 0.62f, 0.12f) : tint;
            Color outer = style == BurstStyle.Fire ? new Color(0.85f, 0.25f, 0.08f) : Dark(tint, 0.35f);
            Color smoke = style == BurstStyle.Water ? Light(tint, 0.5f) : new Color(0.38f, 0.34f, 0.33f, 0.9f);

            System.Random rng = new System.Random(style.GetHashCode() * 31 + 7);
            Sprite[] frames = new Sprite[5];
            for (int f = 0; f < frames.Length; f++)
            {
                Texture2D t = NewTexture(size, size, $"Pixel Burst {style} {f}");
                float c = size / 2f;
                switch (f)
                {
                    case 0:
                        Disc(t, c, c, 4f, mid); Disc(t, c, c, 2.5f, core);
                        break;
                    case 1:
                        Disc(t, c, c, 7f, outer); Disc(t, c, c, 5.5f, mid); Disc(t, c, c, 3.5f, core);
                        break;
                    case 2:
                        Disc(t, c, c, 10f, outer); Disc(t, c, c, 8f, mid); Disc(t, c, c, 4f, core);
                        Holes(t, rng, 0.25f);
                        break;
                    case 3:
                        Disc(t, c, c, 11f, smoke); Disc(t, c, c, 6f, outer);
                        Holes(t, rng, 0.55f);
                        break;
                    default:
                        Disc(t, c, c, 12f, smoke);
                        Holes(t, rng, 0.8f);
                        break;
                }
                frames[f] = ToSprite(t);
            }
            frameCache[key] = frames;
            return frames;
        }
    }

    /// <summary>Cincin pixel 32x32 (gelombang di tanah). Warna diatur lewat material _Color.</summary>
    public static Texture2D RingTexture()
    {
        return Cached("ring", () =>
        {
            const int size = 32;
            Texture2D t = NewTexture(size, size, "Pixel Ring");
            float c = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    if (d >= 13f && d <= 16f) t.SetPixel(x, y, Color.white);                          // garis luar tegas
                    else if (d >= 10f && d < 13f && (x + y) % 2 == 0) t.SetPixel(x, y, new Color(1, 1, 1, 0.55f)); // dither dalam
                }
            }
            t.Apply();
            return t;
        });
    }

    /// <summary>Tekstur garis beam pixel (diulang sepanjang LineRenderer).</summary>
    public static Material BeamMaterial(Color color)
    {
        return Cached("beam" + ColorUtility.ToHtmlStringRGB(color), () =>
        {
            Texture2D t = NewTexture(8, 6, "Pixel Beam");
            t.wrapMode = TextureWrapMode.Repeat;
            for (int x = 0; x < 8; x++)
            {
                bool bright = x % 4 < 2;
                t.SetPixel(x, 0, Dark(color, 0.3f));
                t.SetPixel(x, 1, color);
                t.SetPixel(x, 2, bright ? Color.white : Light(color, 0.6f));
                t.SetPixel(x, 3, bright ? Color.white : Light(color, 0.6f));
                t.SetPixel(x, 4, color);
                t.SetPixel(x, 5, Dark(color, 0.3f));
            }
            t.Apply();
            return new Material(Shader.Find("Sprites/Default")) { name = "Pixel Beam", mainTexture = t, renderQueue = 3150 };
        });
    }

    private static void Holes(Texture2D t, System.Random rng, float amount)
    {
        for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
                if (t.GetPixel(x, y).a > 0f && rng.NextDouble() < amount) t.SetPixel(x, y, new Color(0, 0, 0, 0));
    }
}

/// <summary>
/// Sprite efek yang menghadap kamera (billboard), bisa beranimasi frame, bergerak, dan memudar.
/// </summary>
public class PixelBillboard : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float fps;
    private float life;
    private float age;
    private Vector3 velocity;
    private float gravity;
    private Color color;
    private Camera cam;

    public static PixelBillboard Spawn(Vector3 position, Sprite[] frames, float fps, float scale, Color color,
        float life = -1f, Vector3 velocity = default, float gravity = 0f)
    {
        GameObject go = new GameObject("Pixel Vfx");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;

        PixelBillboard b = go.AddComponent<PixelBillboard>();
        b.spriteRenderer = go.AddComponent<SpriteRenderer>();
        b.spriteRenderer.sharedMaterial = SpriteMaterials.DefaultSprite;
        b.spriteRenderer.sortingOrder = 20; // di depan sprite tower/musuh
        b.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        b.spriteRenderer.color = color;
        b.spriteRenderer.sprite = frames[0];
        b.frames = frames;
        b.fps = fps;
        b.color = color;
        b.velocity = velocity;
        b.gravity = gravity;
        // Tanpa durasi -> hilang setelah animasi selesai
        b.life = life > 0f ? life : (frames.Length > 1 && fps > 0f ? frames.Length / fps : 1f);
        b.Face();
        return b;
    }

    public static PixelBillboard Spawn(Vector3 position, Sprite sprite, float scale, Color color, float life,
        Vector3 velocity = default, float gravity = 0f)
    {
        return Spawn(position, new[] { sprite }, 0f, scale, color, life, velocity, gravity);
    }

    /// <summary>Dipakai proyektil: billboard tanpa batas umur (dihancurkan pemiliknya).</summary>
    public static SpriteRenderer Attach(GameObject owner, Sprite sprite, float scale)
    {
        GameObject go = new GameObject("Pixel Sprite");
        go.transform.SetParent(owner.transform, false);
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = SpriteMaterials.DefaultSprite;
        sr.sortingOrder = 20;
        sr.sprite = sprite;
        go.AddComponent<FaceCamera>();
        return sr;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        if (frames.Length > 1 && fps > 0f)
        {
            spriteRenderer.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * fps))];
        }

        velocity.y -= gravity * dt;
        transform.position += velocity * dt;

        // Memudar di 40% akhir umur
        Color c = color;
        c.a *= Mathf.Clamp01((life - age) / (life * 0.4f));
        spriteRenderer.color = c;

        Face();
    }

    private void Face()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}

/// <summary>Selalu menghadap kamera (untuk sprite yang menempel di objek lain).</summary>
public class FaceCamera : MonoBehaviour
{
    private Camera cam;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}

/// <summary>Kumpulan efek siap pakai.</summary>
public static class PixelVfx
{
    public static readonly Color WaterBlue = new Color(0.35f, 0.7f, 1f);
    public static readonly Color InsightBlue = new Color(0.45f, 0.9f, 1f);
    public static readonly Color HealGreen = new Color(0.45f, 1f, 0.55f);

    /// <summary>Ledakan / cipratan animasi.</summary>
    public static void Burst(Vector3 position, PixelArt.BurstStyle style, Color tint, float scale = 1f)
    {
        PixelBillboard.Spawn(position, PixelArt.Burst(style, tint), 16f, scale, Color.white);
    }

    /// <summary>Beberapa kilau kecil yang naik lalu memudar.</summary>
    public static void Sparkles(Vector3 position, Color color, int count, float spread = 0.3f, float rise = 0.9f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-spread, spread), Random.Range(0f, spread), Random.Range(-spread, spread) * 0.5f);
            Vector3 velocity = new Vector3(Random.Range(-0.15f, 0.15f), rise * Random.Range(0.7f, 1.2f), 0f);
            PixelBillboard.Spawn(position + offset, PixelArt.Sparkle(color), 1f, Color.white, Random.Range(0.45f, 0.75f), velocity);
        }
    }

    /// <summary>Percikan kotak pixel yang terlempar lalu jatuh (cipratan air / serpihan).</summary>
    public static void Debris(Vector3 position, Color color, int count, float speed = 1.4f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 velocity = new Vector3(dir.x * speed, Random.Range(0.8f, 1.6f) * speed, dir.y * speed * 0.5f);
            PixelBillboard.Spawn(position, PixelArt.Square(2), 1f, color, Random.Range(0.35f, 0.55f), velocity, 6f);
        }
    }
}
