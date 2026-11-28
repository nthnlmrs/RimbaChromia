using UnityEngine;

/// <summary>
/// Efek visual tower & musuh (peluru, gelombang, lemparan api), bergaya pixel art lewat PixelVfx.
/// Bisa diganti sprite dari artist nanti (Asset List GDD: "Attack animation (Tower)" & "Effect").
/// </summary>
public static class TowerVfx
{
    private static Material unlitMaterial;
    private static Material groundMaterial;

    /// <summary>
    /// Urutan render (Transparent = 3000):
    ///  - Dekal tanah (overlay tile, gelombang, bayangan) di bawah 3000 -> digambar SEBELUM sprite karakter,
    ///    jadi tidak menimpa sprite tower/musuh yang berdiri di depannya.
    ///  - Sprite karakter 3000 (bawaan SpriteRenderer).
    ///  - Proyektil / beam di atas 3000 -> selalu terlihat di depan sprite.
    /// </summary>
    public const int GroundDecalQueue = 2980;

    /// <summary>Material untuk efek datar di tanah (overlay tile, gelombang).</summary>
    public static Material GroundMaterial
    {
        get
        {
            if (groundMaterial == null)
            {
                groundMaterial = new Material(Shader.Find("Sprites/Default")) { name = "TowerVfx_Ground", renderQueue = GroundDecalQueue };
            }
            return groundMaterial;
        }
    }
    private static Mesh flatQuad;
    private static Mesh sphereMesh;

    /// <summary>Material unlit transparan (warna diatur lewat MaterialPropertyBlock / vertex color).</summary>
    public static Material UnlitMaterial
    {
        get
        {
            // Dicek ulang tiap akses: Domain Reload dimatikan, jadi material dari sesi Play lama bisa sudah hancur
            if (unlitMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                unlitMaterial = new Material(shader) { name = "TowerVfx_Unlit", renderQueue = 3150 };
            }
            return unlitMaterial;
        }
    }

    public static Color ColorOf(TowerColor color)
    {
        return PlacementPreview.GetTowerColor(color);
    }

    /// <summary>Gelombang datar yang membesar lalu memudar di atas tile.</summary>
    public static void Pulse(Vector3 center, float size, Color color, float duration)
    {
        // Di atas permukaan tile tertinggi supaya tidak tertutup model rumput 3D
        float ground = GridManager.Instance != null ? GridManager.Instance.MaxSurfaceY : 0.075f;
        PulseAtHeight(center, Mathf.Max(0.12f, ground + 0.04f), size, color, duration);
    }

    /// <summary>Sama seperti Pulse, tapi di ketinggian tertentu (misal di atas kepala tower).</summary>
    public static void PulseAtHeight(Vector3 center, float height, float size, Color color, float duration)
    {
        GameObject go = new GameObject("Vfx_Pulse");
        go.transform.position = new Vector3(center.x, height, center.z);

        go.AddComponent<MeshFilter>().sharedMesh = GetFlatQuad();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        SetupRenderer(mr);

        // Gelombang di tanah = dekal (di belakang sprite); gelombang di atas kepala tower tetap di depan
        float ground = GridManager.Instance != null ? GridManager.Instance.MaxSurfaceY : 0.075f;
        if (height <= ground + 0.15f) mr.sharedMaterial = GroundMaterial;

        // Bentuk cincin pixel (bukan kotak penuh)
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture("_MainTex", PixelArt.RingTexture());
        mr.SetPropertyBlock(block);

        VfxPulse pulse = go.AddComponent<VfxPulse>();
        pulse.Init(mr, size, color, duration);
    }

    /// <summary>Peluru yang terbang ke musuh, damage masuk saat kena.</summary>
    public static void FireProjectile(Vector3 from, Enemy target, float damage, Color color)
    {
        if (target == null) return;

        GameObject go = new GameObject("Vfx_Projectile");
        go.transform.position = from;

        // Bola air pixel (Blue Sprinkler menyemprotkan air)
        Color water = Color.Lerp(color, PixelVfx.WaterBlue, 0.5f);
        PixelBillboard.Attach(go, PixelArt.Orb(water), 1.2f);

        go.AddComponent<VfxProjectile>().Init(target, damage, water);
    }

    /// <summary>
    /// Proyektil melengkung (batu api / bola api musuh). onLand dipanggil saat mendarat di 'to'.
    /// </summary>
    public static void Lob(Vector3 from, Vector3 to, Color color, float flightTime, System.Action onLand, float size = 0.16f)
    {
        GameObject go = new GameObject("Vfx_Lob");
        go.transform.position = from;

        // Bola api / batu panas pixel
        PixelBillboard.Attach(go, PixelArt.Orb(color), 1.3f);

        go.AddComponent<VfxLob>().Init(from, to, flightTime, onLand, color);
    }

    public static void SetColor(Renderer renderer, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private static void SetupRenderer(MeshRenderer mr)
    {
        mr.sharedMaterial = UnlitMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>Quad datar 1x1 menghadap ke atas (dipakai overlay tile).</summary>
    public static Mesh FlatQuad => GetFlatQuad();

    /// <summary>Mesh sphere bawaan Unity (dipakai percikan bara, proyektil).</summary>
    public static Mesh SphereMesh => GetSphere();

    private static Mesh GetFlatQuad()
    {
        if (flatQuad == null)
        {
            flatQuad = new Mesh { name = "Vfx_FlatQuad" };
            flatQuad.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),   new Vector3(0.5f, 0f, -0.5f)
            };
            flatQuad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            flatQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            flatQuad.RecalculateNormals();
            flatQuad.RecalculateBounds();
        }
        return flatQuad;
    }

    private static Mesh GetSphere()
    {
        if (sphereMesh == null)
        {
            // Ambil mesh sphere bawaan Unity tanpa menyisakan GameObject / collider
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
        }
        return sphereMesh;
    }
}

/// <summary>Komponen gelombang: membesar 60% -> 100% sambil memudar, lalu hilang.</summary>
public class VfxPulse : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private float size;
    private Color color;
    private float duration;
    private float elapsed;

    public void Init(MeshRenderer renderer, float targetSize, Color pulseColor, float pulseDuration)
    {
        meshRenderer = renderer;
        size = targetSize;
        color = pulseColor;
        duration = Mathf.Max(0.05f, pulseDuration);
        Apply(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        float s = Mathf.Lerp(0.6f, 1f, t) * size;
        transform.localScale = new Vector3(s, 1f, s);

        Color c = color;
        c.a = Mathf.Lerp(0.9f, 0f, t);
        TowerVfx.SetColor(meshRenderer, c);
    }
}

/// <summary>Proyektil parabola: terbang dari A ke B dalam waktu tetap, lalu jalankan onLand.</summary>
public class VfxLob : MonoBehaviour
{
    private Vector3 from;
    private Vector3 to;
    private float duration;
    private float elapsed;
    private float arcHeight;
    private System.Action onLand;
    private Color color;
    private float trailTimer;

    public void Init(Vector3 start, Vector3 end, float flightTime, System.Action landed, Color trailColor)
    {
        from = start;
        to = end;
        duration = Mathf.Max(0.1f, flightTime);
        onLand = landed;
        color = trailColor;
        arcHeight = 1f + Vector3.Distance(start, end) * 0.35f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 pos = Vector3.Lerp(from, to, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = pos;

        // Jejak bara di belakang bola api
        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.04f;
            Color ember = Random.value < 0.5f ? color : new Color(1f, 0.85f, 0.35f);
            PixelBillboard.Spawn(pos, PixelArt.Square(2), 1f, ember, 0.35f, new Vector3(0f, 0.3f, 0f));
        }

        if (t >= 1f)
        {
            PixelVfx.Burst(to + Vector3.up * 0.2f, PixelArt.BurstStyle.Fire, color, 1.1f);
            onLand?.Invoke();
            Destroy(gameObject);
        }
    }
}

/// <summary>Peluru sederhana: kejar musuh, damage saat sampai. Hilang kalau musuh sudah mati.</summary>
public class VfxProjectile : MonoBehaviour
{
    private const float Speed = 12f;
    private const float MaxLifetime = 3f;

    private Enemy target;
    private float damage;
    private float lifetime;
    private Color color;
    private float trailTimer;

    public void Init(Enemy enemyTarget, float hitDamage, Color waterColor)
    {
        target = enemyTarget;
        damage = hitDamage;
        color = waterColor;
    }

    private void Update()
    {
        lifetime += Time.deltaTime;
        if (target == null || target.IsDead || lifetime > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.transform.position + Vector3.up * (target.VisualHeight * 0.5f);
        transform.position = Vector3.MoveTowards(transform.position, destination, Speed * Time.deltaTime);

        // Jejak tetesan air
        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.035f;
            PixelBillboard.Spawn(transform.position, PixelArt.Square(2), 1f, Color.Lerp(color, Color.white, 0.4f), 0.25f, Vector3.zero, 2f);
        }

        if ((transform.position - destination).sqrMagnitude < 0.04f)
        {
            target.TakeDamage(damage);
            AudioManager.BlueSplash();
            PixelVfx.Burst(destination, PixelArt.BurstStyle.Water, color, 0.7f); // cipratan
            PixelVfx.Debris(destination, color, 4, 1f);
            Destroy(gameObject);
        }
    }
}
