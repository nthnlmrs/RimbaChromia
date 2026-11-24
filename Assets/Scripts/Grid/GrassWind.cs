using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rumput tertiup angin: semua helai rumput (kartu dekorasi di model GrassTile) bergoyang.
/// - Gelombang angin merambat melintasi arena (helai bersebelahan bergerak berurutan)
/// - Hembusan kuat-lemah berubah pelan
/// - Helai yang sedang ditindih tower hampir diam
/// Satu komponen untuk seluruh arena (otomatis ditambahkan GridManager).
/// </summary>
public class GrassWind : MonoBehaviour
{
    [Header("Angin")]
    [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.4f);
    [Tooltip("Sudut goyang maksimal (derajat)")]
    [SerializeField] private float swayAngle = 9f;
    [Tooltip("Kecepatan gelombang angin")]
    [SerializeField] private float waveSpeed = 1.8f;
    [Tooltip("Kerapatan gelombang (makin besar makin pendek jarak antar gelombang)")]
    [SerializeField] private float waveDensity = 0.45f;
    [Tooltip("Seberapa lambat kuat-lemah hembusan berubah")]
    [SerializeField] private float gustSpeed = 0.2f;

    private struct Blade
    {
        public Transform transform;
        public Quaternion rotation;
        public float phase;
        public float seed;
        public GridCell cell;
    }

    private readonly List<Blade> blades = new List<Blade>();
    private GridManager grid;

    private void Start()
    {
        grid = GetComponent<GridManager>();
        if (grid == null) { enabled = false; return; }

        if (grid.IsGridReady) Collect();
        else grid.OnGridGenerated += Collect;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.OnGridGenerated -= Collect;
    }

    private void Collect()
    {
        grid.OnGridGenerated -= Collect;
        blades.Clear();

        Vector2 dir = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;
        foreach (GridCell cell in grid.AllCells())
        {
            foreach (Transform t in cell.Decorations)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                blades.Add(new Blade
                {
                    transform = t,
                    rotation = t.localRotation,
                    phase = (p.x * dir.x + p.z * dir.y) * waveDensity,
                    seed = Random.value * 6.28f,
                    cell = cell
                });
            }
        }
    }

    private void Update()
    {
        if (blades.Count == 0) return;

        float t = Time.time;
        float gust = 0.55f + 0.45f * Mathf.PerlinNoise(t * gustSpeed, 0.37f);

        for (int i = 0; i < blades.Count; i++)
        {
            Blade b = blades[i];
            if (b.transform == null || !b.transform.gameObject.activeInHierarchy) continue;

            // Gelombang utama + getaran kecil per helai
            float wave = Mathf.Sin(b.phase - t * waveSpeed);
            float flutter = Mathf.Sin(t * 4.3f + b.seed) * 0.25f;
            float calm = 1f - b.cell.SquashAmount * 0.85f;
            float angle = (wave * gust + flutter) * swayAngle * calm;

            // Sumbu Z lokal = miring ke samping, sumbu X lokal = condong maju/mundur (pivot di pangkal helai)
            b.transform.localRotation = b.rotation * Quaternion.Euler(angle * 0.35f, 0f, angle);
        }
    }
}
