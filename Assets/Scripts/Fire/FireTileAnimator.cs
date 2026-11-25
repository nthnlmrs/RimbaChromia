using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animasi tile terbakar (dipasang otomatis oleh FireManager di model api 3D):
///  - kartu lidah api berkedip memanjang/memendek & meliuk (noise, tiap kartu beda fase)
///  - cahaya (emission) berdenyut, termasuk bara di tanah tile level 2/3
///  - percikan bara naik lalu memudar
/// Makin tinggi level api, makin cepat & kuat animasinya.
/// </summary>
public class FireTileAnimator : MonoBehaviour
{
    private struct Card
    {
        public Transform transform;
        public Vector3 scale;
        public Quaternion rotation;
        public float phase;
    }

    private readonly List<Card> cards = new List<Card>();
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private int level = 1;
    private float sparkTimer;
    private float seed;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public void Init(int fireLevel)
    {
        level = Mathf.Clamp(fireLevel, 1, 3);
        seed = Random.value * 100f;
        block = new MaterialPropertyBlock();

        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Renderer baseRenderer = ArenaArt.FindBaseRenderer(renderers);
        foreach (Renderer r in renderers)
        {
            if (r == baseRenderer) continue;
            cards.Add(new Card
            {
                transform = r.transform,
                scale = r.transform.localScale,
                rotation = r.transform.localRotation,
                phase = Random.value * 10f
            });
        }

        sparkTimer = Random.Range(0.1f, 0.5f);
    }

    private void Update()
    {
        if (block == null) return;

        float t = Time.time;
        float speed = 1f + (level - 1) * 0.35f;
        float strength = 0.10f + level * 0.04f;

        // Lidah api: kedip tinggi (pakai Perlin supaya tidak terlihat seperti sinus teratur) + liukan kecil
        foreach (Card c in cards)
        {
            if (c.transform == null) continue;

            float n = Mathf.PerlinNoise(c.phase, t * 3.2f * speed) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(c.phase + 31.7f, t * 5.1f * speed) * 2f - 1f;
            float stretch = 1f + n * strength + n2 * strength * 0.5f;

            c.transform.localScale = new Vector3(
                c.scale.x * (1f - (stretch - 1f) * 0.45f),
                c.scale.y * stretch,
                c.scale.z);

            float sway = Mathf.Sin(t * 2.6f * speed + c.phase) * (3f + level * 1.5f);
            c.transform.localRotation = c.rotation * Quaternion.Euler(0f, 0f, sway);
        }

        // Denyut cahaya
        float glow = 0.75f + Mathf.PerlinNoise(seed, t * 4f * speed) * (0.45f + level * 0.15f);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, Color.white * glow);
            r.SetPropertyBlock(block);
        }

        // Percikan bara
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            sparkTimer = Random.Range(0.35f, 0.8f) / level;
            FireSpark.Spawn(transform.position, level);
        }
    }
}

/// <summary>Percikan bara kecil yang naik bergoyang lalu memudar.</summary>
public class FireSpark : MonoBehaviour
{
    private static readonly Color[] Colors =
    {
        new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.35f, 0.05f)
    };

    private Vector3 velocity;
    private float life;
    private float age;
    private float wobble;
    private SpriteRenderer spriteRenderer;
    private Color color;

    public static void Spawn(Vector3 tileCenter, int level)
    {
        GameObject go = new GameObject("Fire Spark");
        go.transform.position = tileCenter + new Vector3(Random.Range(-0.35f, 0.35f), 0.15f, Random.Range(-0.35f, 0.35f));
        go.transform.localScale = Vector3.one * (Random.value < 0.5f ? 1f : 1.5f);

        // Kotak pixel (bukan bola) supaya senada dengan pixel art
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PixelArt.Square(2);
        sr.sharedMaterial = SpriteMaterials.DefaultSprite;
        sr.sortingOrder = 15;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<FaceCamera>();

        FireSpark spark = go.AddComponent<FireSpark>();
        spark.spriteRenderer = sr;
        spark.color = Colors[Random.Range(0, Colors.Length)];
        spark.velocity = new Vector3(0f, Random.Range(0.6f, 1.0f) + level * 0.15f, 0f);
        spark.life = Random.Range(0.6f, 1.1f);
        spark.wobble = Random.value * 10f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 drift = new Vector3(Mathf.Sin(age * 6f + wobble), 0f, Mathf.Cos(age * 5f + wobble)) * 0.15f;
        transform.position += (velocity + drift) * Time.deltaTime;

        Color c = color;
        c.a = 1f - age / life;
        spriteRenderer.color = c;
    }
}
