using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menghidupkan model lingkungan (background hutan & markas):
/// - Pohon / semak (bidang datar bergambar) diputar SEJAJAR bidang kamera seperti sprite tower & musuh,
///   jadi semuanya tegak ke arah yang sama di layar (tidak miring ke kiri/kanan karena perspektif).
/// - Bergoyang pelan tertiup angin (fase acak per pohon).
/// - Warna dicerahkan + variasi kecil per objek supaya tidak terlihat datar / seragam.
/// Dipasang otomatis oleh ArenaArt; semua pengaturan ada di aset ArenaArt.
/// </summary>
public class EnvironmentFoliage : MonoBehaviour
{
    private struct Card
    {
        public Transform transform;
        public Quaternion correction;
        public float phase;
        public float amplitude;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    private readonly List<Card> cards = new List<Card>();
    private ArenaArt art;
    private Camera cam;

    /// <param name="arena">Area arena (X/Z dunia). Diisi = tambah pohon & semak acak di sekitarnya.</param>
    /// <param name="treeSpots">Titik tile hutan di pinggir arena yang ditanami pohon (Y = permukaan tile).</param>
    /// <param name="bushSpots">Titik tile hutan yang cukup ditanami semak (baris dekat kamera).</param>
    public static void Apply(GameObject root, ArenaArt art, Rect? arena = null, List<Vector3> treeSpots = null, List<Vector3> bushSpots = null)
    {
        if (root == null || art == null) return;
        EnvironmentFoliage foliage = root.AddComponent<EnvironmentFoliage>();
        foliage.art = art;
        if (arena.HasValue) foliage.Populate(arena.Value, treeSpots, bushSpots);
        foliage.Setup();
    }

    /// <summary>
    /// Rimbunkan hutan: salin pohon / semak dari model ke posisi acak di atas tanah latar,
    /// di luar arena. Pohon tinggi hanya di kiri, kanan & belakang supaya tidak menutupi arena dari kamera.
    /// </summary>
    private void Populate(Rect arena, List<Vector3> treeSpots, List<Vector3> bushSpots)
    {
        List<Transform> trees = new List<Transform>();
        List<Transform> bushes = new List<Transform>();
        Bounds ground = default;
        bool hasGround = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material mat = r.sharedMaterial;
            if (mat == null) continue;
            if (mat.name.Contains("TreeBig")) trees.Add(r.transform);
            else if (mat.name.Contains("Bush")) bushes.Add(r.transform);
            else if (IsGround(mat))
            {
                if (!hasGround) { ground = r.bounds; hasGround = true; }
                else ground.Encapsulate(r.bounds);
            }
        }

        float c = Mathf.Max(0f, art.forestClearance);
        Rect clear = Rect.MinMaxRect(arena.xMin - c, arena.yMin - c, arena.xMax + c, arena.yMax + c);
        // Hutan menyebar di sekitar arena sampai menutupi lantai hijau di kejauhan
        float depth = Mathf.Max(arena.width, arena.height) * Mathf.Max(0.1f, art.forestDepth);
        Rect area = Rect.MinMaxRect(clear.xMin - depth, clear.yMin - depth, clear.xMax + depth, clear.yMax + depth);

        // Tile hutan di pinggir arena: 1-2 pohon per tile, sedikit digeser acak
        if (treeSpots != null) foreach (Vector3 spot in treeSpots) PlaceOnTile(trees, spot, 0.3f);
        if (bushSpots != null) foreach (Vector3 spot in bushSpots) PlaceOnTile(bushes, spot, 0.3f);

        // Barisan rapat tepat di tepi arena, supaya hutan langsung mulai dari pinggir arena
        if (art.edgeTreeSpacing > 0.05f)
        {
            EdgeRow(trees, new Vector2(clear.xMin, clear.yMin), new Vector2(clear.xMin, clear.yMax), Vector2.left);
            EdgeRow(trees, new Vector2(clear.xMax, clear.yMin), new Vector2(clear.xMax, clear.yMax), Vector2.right);
            EdgeRow(trees, new Vector2(clear.xMin, clear.yMax), new Vector2(clear.xMax, clear.yMax), Vector2.up);
            EdgeRow(bushes, new Vector2(clear.xMin, clear.yMin), new Vector2(clear.xMax, clear.yMin), Vector2.down);
        }
        Scatter(trees, art.extraTrees, area, clear, false);
        Scatter(bushes, art.extraBushes, area, clear, true);
    }

    private void EdgeRow(List<Transform> prototypes, Vector2 from, Vector2 to, Vector2 outward)
    {
        if (prototypes.Count == 0) return;
        float length = Vector2.Distance(from, to);
        int count = Mathf.Max(1, Mathf.RoundToInt(length / art.edgeTreeSpacing));
        for (int i = 0; i <= count; i++)
        {
            float t = (i + Random.Range(-0.3f, 0.3f)) / count;
            Vector2 p = Vector2.Lerp(from, to, Mathf.Clamp01(t)) + outward * Random.Range(0f, art.edgeTreeSpacing * 0.6f);
            Place(prototypes, p);
        }
    }

    private void PlaceOnTile(List<Transform> prototypes, Vector3 spot, float jitter)
    {
        Vector2 p = new Vector2(spot.x + Random.Range(-jitter, jitter), spot.z + Random.Range(-jitter, jitter));
        Place(prototypes, p, spot.y);
    }

    private void Place(List<Transform> prototypes, Vector2 p, float? y = null)
    {
        if (prototypes.Count == 0) return;
        Transform proto = prototypes[Random.Range(0, prototypes.Count)];
        Transform copy = Instantiate(proto.gameObject, proto.parent).transform;
        copy.name = proto.name + " (extra)";
        copy.position = new Vector3(p.x, y ?? proto.position.y, p.y);
        copy.localScale = proto.localScale * Random.Range(0.8f, 1.25f);
    }

    private void Scatter(List<Transform> prototypes, int count, Rect area, Rect clear, bool allowFront)
    {
        if (prototypes.Count == 0 || count <= 0) return;
        int placed = 0;
        for (int tries = 0; placed < count && tries < count * 30; tries++)
        {
            Vector2 p = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
            if (clear.Contains(p)) continue;
            // Depan arena (sisi kamera, -Z): hanya semak pendek, kecuali jauh di samping kiri / kanan
            if (!allowFront && p.y < clear.yMin && p.x > clear.xMin - 1.5f && p.x < clear.xMax + 1.5f) continue;

            Place(prototypes, p);
            placed++;
        }
    }

    private static bool IsCard(Material mat)
    {
        // Pohon & semak = bidang datar bergambar (bukan daun 3D yang menempel di tembok)
        return mat != null && (mat.name.Contains("Bush") || mat.name.Contains("TreeBig"));
    }

    private static bool IsFoliage(Material mat)
    {
        return mat != null && (IsCard(mat) || mat.name.Contains("Lea"));
    }

    private static bool IsGround(Material mat)
    {
        return mat != null && mat.name.Contains("Ground");
    }

    private void Setup()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material mat = r.sharedMaterial;
            if (mat == null) continue;

            Color tint;
            float glow;
            bool foliage = IsFoliage(mat);
            if (foliage) { tint = art.foliageTint; glow = 0f; }
            else if (IsGround(mat)) { tint = art.groundTint; glow = art.groundGlow; }
            else { tint = art.propTint; glow = 0f; }

            // Variasi kecil per objek: terang-gelap & sedikit lebih hijau / kuning
            float v = art.colorVariation;
            float bright = 1f + Random.Range(-v, v);
            Color variation = new Color(bright * (1f + Random.Range(-v, v) * 0.5f), bright, bright * (1f + Random.Range(-v, v) * 0.5f), 1f);

            Color baseColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Multiply(Multiply(baseColor, tint), variation));
            if (mat.IsKeywordEnabled("_EMISSION"))
            {
                // Cahaya sendiri dari teksturnya: bagian gelap / membelakangi matahari tetap berwarna
                if (foliage)
                {
                    // Warna rata (bukan dari tekstur) = kabut hangat: hijau jadi lebih kalem & hangat
                    block.SetTexture(EmissionMapId, Texture2D.whiteTexture);
                    block.SetColor(EmissionColorId, Multiply(art.foliageHaze, variation));
                }
                else
                {
                    block.SetColor(EmissionColorId, Multiply(tint, variation) * glow);
                }
            }
            r.SetPropertyBlock(block);

            if (art.billboardFoliage && IsCard(mat))
            {
                Transform t = r.transform;
                // Simpan rotasi "tegak menghadap depan" bawaan model, supaya hanya arah hadapnya yang diganti
                Vector3 forward = Vector3.ProjectOnPlane(t.rotation * Vector3.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                Quaternion correction = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)) * t.rotation;
                if (art.flipFoliageFacing) correction = Quaternion.Euler(0f, 180f, 0f) * correction;

                cards.Add(new Card
                {
                    transform = t,
                    correction = correction,
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    amplitude = art.windSway * Random.Range(0.6f, 1.2f),
                });
            }
        }
        LateUpdate();
    }

    private static Color Multiply(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }

    private void LateUpdate()
    {
        if (cards.Count == 0) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Quaternion camRotation = cam.transform.rotation;
        float time = Time.time * art.windSpeed;
        foreach (Card card in cards)
        {
            if (card.transform == null) continue;
            // Goyang = berputar di pangkal (pivot model di bawah), searah bidang layar
            float sway = card.amplitude * Mathf.Sin(time + card.phase) + card.amplitude * 0.35f * Mathf.Sin(time * 2.3f + card.phase * 1.7f);
            card.transform.rotation = camRotation * Quaternion.Euler(0f, 0f, sway) * card.correction;
        }
    }
}
