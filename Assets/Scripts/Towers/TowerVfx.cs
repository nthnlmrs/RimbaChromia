using UnityEngine;

/// <summary>
/// Efek visual PLACEHOLDER untuk tower (peluru, gelombang ledakan / aura).
/// Nanti diganti efek dari artist (Asset List GDD: "Attack animation (Tower)" & "Effect").
/// </summary>
public static class TowerVfx
{
    private static Material unlitMaterial;
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
        PulseAtHeight(center, 0.12f, size, color, duration);
    }

    /// <summary>Sama seperti Pulse, tapi di ketinggian tertentu (misal di atas kepala tower).</summary>
    public static void PulseAtHeight(Vector3 center, float height, float size, Color color, float duration)
    {
        GameObject go = new GameObject("Vfx_Pulse");
        go.transform.position = new Vector3(center.x, height, center.z);

        go.AddComponent<MeshFilter>().sharedMesh = GetFlatQuad();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        SetupRenderer(mr);

        VfxPulse pulse = go.AddComponent<VfxPulse>();
        pulse.Init(mr, size, color, duration);
    }

    /// <summary>Peluru yang terbang ke musuh, damage masuk saat kena.</summary>
    public static void FireProjectile(Vector3 from, Enemy target, float damage, Color color)
    {
        if (target == null) return;

        GameObject go = new GameObject("Vfx_Projectile");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.22f;

        go.AddComponent<MeshFilter>().sharedMesh = GetSphere();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        SetupRenderer(mr);
        SetColor(mr, color);

        go.AddComponent<VfxProjectile>().Init(target, damage);
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
        c.a = Mathf.Lerp(0.6f, 0f, t);
        TowerVfx.SetColor(meshRenderer, c);
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

    public void Init(Enemy enemyTarget, float hitDamage)
    {
        target = enemyTarget;
        damage = hitDamage;
    }

    private void Update()
    {
        lifetime += Time.deltaTime;
        if (target == null || target.IsDead || lifetime > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, Speed * Time.deltaTime);

        if ((transform.position - destination).sqrMagnitude < 0.04f)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
