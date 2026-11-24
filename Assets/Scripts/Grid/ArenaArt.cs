using UnityEngine;

/// <summary>
/// Kumpulan model 3D arena (GDD: arena 3D, karakter 2D).
/// Satu aset ini dipegang tim art: isi model FBX per jenis tile, sisanya otomatis.
/// Model disesuaikan otomatis ke ukuran 1 tile (berdasarkan bounding box), jadi skala / pivot FBX
/// dari Blender tidak perlu persis. Tile yang modelnya kosong tetap memakai kotak placeholder.
/// </summary>
[CreateAssetMenu(fileName = "ArenaArt", menuName = "GIMERSIA/Arena Art")]
public class ArenaArt : ScriptableObject
{
    [Header("Model tile (boleh beberapa varian, dipilih acak)")]
    public GameObject[] grassModels = new GameObject[0];
    public GameObject[] barePathModels = new GameObject[0];
    public GameObject[] forestModels = new GameObject[0];
    public GameObject[] wallModels = new GameObject[0];

    [Header("Model api per level (menggantikan tile rumput saat terbakar)")]
    public GameObject[] fireLevel1Models = new GameObject[0];
    public GameObject[] fireLevel2Models = new GameObject[0];
    public GameObject[] fireLevel3Models = new GameObject[0];

    [Header("Penyesuaian")]
    [Tooltip("Rotasi tambahan kalau model dari Blender rebah / miring (misal X = -90)")]
    public Vector3 rotationOffset = Vector3.zero;
    [Tooltip("Putar acak 0/90/180/270 derajat supaya tile tidak terlihat berulang")]
    public bool randomYaw = true;
    [Tooltip("Lebar model dibanding 1 tile (1 = rapat tanpa celah)")]
    [Range(0.8f, 1.1f)] public float footprint = 1f;
    [Tooltip("Geser tinggi permukaan tempat tower/musuh berdiri (kalau ada rumput tinggi di model)")]
    public float surfaceOffset = 0f;

    public GameObject[] GetModels(TileType type)
    {
        switch (type)
        {
            case TileType.Grass: return grassModels;
            case TileType.BarePath: return barePathModels;
            case TileType.Forest: return forestModels;
            case TileType.Wall: return wallModels;
            default: return null;
        }
    }

    public GameObject[] GetFireModels(int level)
    {
        switch (level)
        {
            case 1: return fireLevel1Models;
            case 2: return fireLevel2Models;
            default: return fireLevel3Models;
        }
    }

    public static GameObject PickRandom(GameObject[] options)
    {
        if (options == null || options.Length == 0) return null;

        int start = Random.Range(0, options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            GameObject candidate = options[(start + i) % options.Length];
            if (candidate != null) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Buat model di posisi tile: diskalakan seragam supaya pas 1 tile,
    /// alasnya di dasar tile, tengahnya di tengah tile. Collider dimatikan (klik tetap ke tile).
    /// Return tinggi permukaan atas model (dunia).
    /// </summary>
    public GameObject SpawnFitted(GameObject prefab, Vector3 cellCenter, float cellSize, float groundY, Transform parent, out float topY)
    {
        topY = groundY;
        if (prefab == null) return null;

        // PENTING: pertahankan rotasi bawaan root model. FBX dari Blender (Z-up) dikoreksi Unity
        // dengan rotasi -90 derajat di root; kalau ditimpa, tile jadi berdiri tegak / rebah.
        float yaw = randomYaw ? 90f * Random.Range(0, 4) : 0f;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(rotationOffset) * prefab.transform.rotation;

        GameObject go = Instantiate(prefab, Vector3.zero, rotation, parent);
        foreach (Collider col in go.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            go.transform.position = cellCenter;
            return go;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float widest = Mathf.Max(bounds.size.x, bounds.size.z);
        if (widest > 0.0001f)
        {
            float scale = cellSize * footprint / widest;
            go.transform.localScale = go.transform.localScale * scale;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }

        // Tengah X/Z model ke tengah tile, alas model ke dasar tile
        Vector3 shift = new Vector3(cellCenter.x - bounds.center.x, groundY - bounds.min.y, cellCenter.z - bounds.center.z);
        go.transform.position += shift;

        // Permukaan = atas BALOK tile (bukan ujung helai rumput / lidah api), supaya tower berdiri di tanah
        Renderer baseRenderer = FindBaseRenderer(renderers);
        topY = baseRenderer.bounds.max.y + surfaceOffset;
        return go;
    }

    /// <summary>Renderer "balok tile" = yang tapak X/Z-nya paling luas. Sisanya dekorasi (rumput, api).</summary>
    public static Renderer FindBaseRenderer(Renderer[] renderers)
    {
        Renderer best = renderers[0];
        float bestArea = -1f;
        foreach (Renderer r in renderers)
        {
            float area = r.bounds.size.x * r.bounds.size.z;
            if (area > bestArea)
            {
                bestArea = area;
                best = r;
            }
        }
        return best;
    }
}
