using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sinar matahari yang menembus celah pohon (seperti di hutan).
/// - Satu SINAR BESAR menyorot ke tengah stage + genangan cahaya hangat di lantainya.
///   Sekaligus indikator bahaya: makin rendah HP The Wall, sinar makin redup & berkedip
///   (seperti harapan yang memudar). Saat Wall hancur, sinar padam.
/// - Opsional: sinar kecil di tepi arena yang berpendar & berpindah tempat.
/// Tiap sinar = bidang tipis transparan, miring searah cahaya matahari scene, selalu menghadap kamera
/// (berputar di sumbu panjangnya). Pengaturan di aset ArenaArt (bagian Sinar matahari).
/// </summary>
public class SunRays : MonoBehaviour
{
    private class Ray
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Vector3 ground;
        public float width;
        public float length;
        public float phase;
        public float life;
        public float age;
        public bool main;
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Texture2D rayTexture;
    private static Texture2D poolTexture;
    private static Material rayMaterial;
    private static Material poolMaterial;

    private readonly List<Ray> rays = new List<Ray>();
    private MaterialPropertyBlock block;
    private ArenaArt art;
    private Rect arena;
    private float groundY;
    private Vector3 lightDirection;
    private Camera cam;
    private WallHealth wall;
    private MeshRenderer pool;
    private float danger;
    private float mainFade = 1f;
    private float nextWallSearch;

    public static SunRays Create(ArenaArt art, Rect arenaBounds, float groundY, Transform parent)
    {
        if (art == null || !art.sunRays) return null;
        if (!art.mainSunRay && art.sunRayCount <= 0) return null;
        if (RayMaterial == null) return null;

        GameObject go = new GameObject("Sun Rays");
        go.transform.SetParent(parent, false);
        SunRays sun = go.AddComponent<SunRays>();
        sun.art = art;
        sun.arena = arenaBounds;
        sun.groundY = groundY;
        sun.block = new MaterialPropertyBlock();
        sun.lightDirection = FindLightDirection();

        if (art.mainSunRay)
        {
            // Sinar besar jatuh di tengah stage
            Ray main = sun.NewRay(RayMaterial);
            main.main = true;
            main.ground = new Vector3(arenaBounds.center.x, groundY, arenaBounds.center.y);
            main.width = arenaBounds.width * art.mainRayWidth;
            main.length = Mathf.Max(1f, art.mainRayLength);
            main.phase = Random.Range(0f, 100f);
            sun.rays.Add(main);

            // Genangan cahaya di lantai
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Quad);
            p.name = "Sun Pool";
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(go.transform, false);
            p.transform.position = main.ground + Vector3.up * 0.03f;
            p.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            p.transform.localScale = new Vector3(arenaBounds.width * art.mainRayWidth * 1.3f, arenaBounds.height * art.mainRayWidth * 1.3f, 1f);
            sun.pool = p.GetComponent<MeshRenderer>();
            sun.pool.sharedMaterial = PoolMaterial;
            sun.pool.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sun.pool.receiveShadows = false;
        }

        for (int i = 0; i < art.sunRayCount; i++)
        {
            Ray ray = sun.NewRay(RayMaterial);
            sun.Respawn(ray);
            ray.age = Random.Range(0f, ray.life); // jangan muncul bersamaan
            sun.rays.Add(ray);
        }
        sun.LateUpdate();
        return sun;
    }

    private Ray NewRay(Material mat)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Sun Ray";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(transform, false);
        MeshRenderer mr = q.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return new Ray { transform = q.transform, renderer = mr };
    }

    /// <summary>Arah cahaya dari matahari scene, dipaksa cukup miring ke bawah.</summary>
    private static Vector3 FindLightDirection()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        Vector3 dir = sun != null ? sun.transform.forward : new Vector3(0.35f, -1f, 0.3f);
        if (dir.y > -0.55f) dir.y = -0.55f; // tetap jatuh ke tanah walau matahari rendah
        return dir.normalized;
    }

    /// <summary>Sinar kecil: titik jatuh baru di tepi kiri / kanan / belakang arena (dekat kanopi hutan).</summary>
    private void Respawn(Ray ray)
    {
        float inset = Random.Range(0.4f, 2.2f);
        float t = Random.value;
        Vector2 p;
        switch (Random.Range(0, 4))
        {
            case 0: p = new Vector2(arena.xMin + inset, Mathf.Lerp(arena.yMin + 1.5f, arena.yMax, t)); break; // kiri
            case 1: p = new Vector2(arena.xMax - inset, Mathf.Lerp(arena.yMin + 1.5f, arena.yMax, t)); break; // kanan
            default: p = new Vector2(Mathf.Lerp(arena.xMin, arena.xMax, t), arena.yMax - inset); break;      // belakang (lebih sering)
        }
        ray.ground = new Vector3(p.x, groundY, p.y);
        ray.width = Random.Range(Mathf.Min(art.sunRayWidth.x, art.sunRayWidth.y), Mathf.Max(art.sunRayWidth.x, art.sunRayWidth.y));
        ray.length = Mathf.Max(0.5f, art.sunRayLength);
        ray.phase = Random.Range(0f, 100f);
        ray.life = Random.Range(7f, 14f);
        ray.age = 0f;
    }

    /// <summary>
    /// 0 = aman, 1 = Wall hampir hancur. Mulai naik saat HP di bawah Danger Start HP.
    /// </summary>
    private float TargetDanger()
    {
        if (wall == null && Time.unscaledTime >= nextWallSearch)
        {
            nextWallSearch = Time.unscaledTime + 1f; // di main menu tidak ada Wall: jangan cari tiap frame
            wall = FindFirstObjectByType<WallHealth>();
        }
        if (wall == null || wall.MaxHP <= 0f) return 0f;
        if (wall.IsDestroyed) return 1f;
        float hp = wall.CurrentHP / wall.MaxHP;
        return Mathf.Clamp01(Mathf.InverseLerp(art.dangerStartHP, 0f, hp));
    }

    /// <summary>Kecerahan sinar besar: redup sesuai bahaya + berkedip makin sering & dalam.</summary>
    private float MainIntensity(float time)
    {
        float breathe = 0.85f + 0.15f * Mathf.Sin(time * 0.6f);
        float dim = Mathf.Lerp(1f, 0.3f, danger);

        // Kedip: noise cepat yang makin dalam saat bahaya (seperti cahaya yang tertutup asap)
        float flickerSpeed = Mathf.Lerp(2f, 9f, danger);
        float noise = Mathf.PerlinNoise(time * flickerSpeed, 3.7f);
        float dip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.15f, noise)); // sesekali padam sesaat
        float flicker = 1f - danger * (0.35f * (1f - noise) + 0.6f * dip);

        bool destroyed = wall != null && wall.IsDestroyed;
        mainFade = Mathf.MoveTowards(mainFade, destroyed ? 0f : 1f, Time.unscaledDeltaTime * 0.8f);
        return breathe * dim * Mathf.Clamp01(flicker) * mainFade;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        float time = Time.unscaledTime; // tetap hidup walau game dijeda
        danger = Mathf.MoveTowards(danger, TargetDanger(), Time.unscaledDeltaTime * 0.5f);
        float mainIntensity = MainIntensity(time);

        foreach (Ray ray in rays)
        {
            Color c;
            float width = ray.width;
            if (ray.main)
            {
                c = art.mainRayColor;
                c.a *= mainIntensity;
            }
            else
            {
                ray.age += Time.deltaTime;
                if (ray.age >= ray.life) Respawn(ray);

                // Muncul & hilang pelan, plus berpendar seperti cahaya yang terhalang daun bergoyang
                float lifeFade = Mathf.Clamp01(ray.age / 1.5f) * Mathf.Clamp01((ray.life - ray.age) / 1.5f);
                float shimmer = 0.65f + 0.35f * Mathf.PerlinNoise(ray.phase, time * 0.6f);
                c = art.sunRayColor;
                c.a *= lifeFade * shimmer * Mathf.Lerp(1f, 0.4f, danger);
                width *= 1f + 0.08f * Mathf.Sin(time * 0.7f + ray.phase);
            }
            block.SetColor(ColorId, c);
            ray.renderer.SetPropertyBlock(block);

            // Bidang dari tanah ke arah datangnya cahaya, menghadap kamera di sumbu panjangnya
            Vector3 axis = -lightDirection;
            Vector3 center = ray.ground + axis * (ray.length * 0.5f);
            Vector3 toCamera = cam != null ? cam.transform.position - center : Vector3.back;
            Vector3 normal = Vector3.ProjectOnPlane(toCamera, axis);
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.back;

            ray.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-normal, axis));
            ray.transform.localScale = new Vector3(width, ray.length, 1f);
        }

        if (pool != null)
        {
            Color c = art.mainPoolColor;
            c.a *= mainIntensity;
            block.SetColor(ColorId, c);
            pool.SetPropertyBlock(block);
        }
    }

    private static Material RayMaterial
    {
        get
        {
            if (rayMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                rayMaterial = new Material(shader) { name = "Sun Ray", mainTexture = RayTexture, renderQueue = 3050 };
            }
            return rayMaterial;
        }
    }

    private static Material PoolMaterial
    {
        get
        {
            if (poolMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                // Sebelum sprite tower & musuh (seperti decal lantai lain), jadi tidak menutupi mereka
                poolMaterial = new Material(shader) { name = "Sun Pool", mainTexture = PoolTexture, renderQueue = 2980 };
            }
            return poolMaterial;
        }
    }

    /// <summary>
    /// Berkas cahaya dari atas: paling terang di pangkal atas (celah kanopi), melebar & memudar ke tanah,
    /// dengan beberapa garis sinar halus di dalamnya (seperti god ray di hutan).
    /// </summary>
    private static Texture2D RayTexture
    {
        get
        {
            if (rayTexture != null) return rayTexture;
            const int w = 64, h = 256;
            rayTexture = NewTexture("Sun Ray Gradient", w, h);
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f); // 0 = tanah, 1 = atas (arah matahari)
                // Terang di atas, memudar halus ke bawah sampai hilang sebelum menyentuh tanah
                float along = Mathf.Lerp(0.15f, 1f, Mathf.Pow(v, 0.7f)) * Mathf.SmoothStep(0f, 1f, v / 0.35f);
                along *= Mathf.SmoothStep(0f, 1f, (1f - v) / 0.06f); // ujung atas tidak terpotong tajam
                float halfWidth = Mathf.Lerp(1f, 0.38f, v); // sempit di atas, melebar ke bawah
                for (int x = 0; x < w; x++)
                {
                    float u = (x / (w - 1f) - 0.5f) * 2f;      // -1..1
                    float d = Mathf.Abs(u) / halfWidth;
                    float across = d >= 1f ? 0f : Mathf.Pow(Mathf.Cos(d * Mathf.PI * 0.5f), 1.6f);
                    // Garis-garis sinar halus (mengikuti bentuk melebar)
                    float k = u / halfWidth;
                    float streak = 0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(k * 11f + 1.3f)) * (0.5f + 0.5f * Mathf.Sin(k * 5.3f - 0.7f));
                    pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(along * across * streak));
                }
            }
            rayTexture.SetPixels(pixels);
            rayTexture.Apply();
            return rayTexture;
        }
    }

    /// <summary>Lingkaran lembut untuk genangan cahaya di lantai.</summary>
    private static Texture2D PoolTexture
    {
        get
        {
            if (poolTexture != null) return poolTexture;
            const int size = 64;
            poolTexture = NewTexture("Sun Pool Gradient", size, size);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x / (size - 1f) * 2f - 1f;
                    float dy = y / (size - 1f) * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.SmoothStep(1f, 0f, d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            poolTexture.SetPixels(pixels);
            poolTexture.Apply();
            return poolTexture;
        }
    }

    private static Texture2D NewTexture(string name, int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }
}
