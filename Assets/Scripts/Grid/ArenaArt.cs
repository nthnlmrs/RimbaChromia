using System.Collections.Generic;
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

    [Header("Lingkungan sekitar arena (FBX dari artist)")]
    [Tooltip("Hutan & tanah di luar arena. Punya lubang di tengah tempat arena berada")]
    public GameObject backgroundEnvironment;
    [Tooltip("Ukuran lubang arena di model background (meter, ukuran asli FBX). Dipakai untuk memaskan skala otomatis")]
    public Vector2 backgroundHoleSize = new Vector2(43.63f, 33.2f);
    [Tooltip("Pengali skala setelah dipaskan otomatis ke ukuran arena")]
    public float backgroundScale = 1f;
    public Vector3 backgroundOffset = Vector3.zero;
    [Tooltip("Centang kalau sisi hutan lebat muncul di depan kamera (otomatis diletakkan di belakang arena)")]
    public bool flipBackground = false;

    [Tooltip("Markas / tembok yang dijaga. Dipasang di atas deretan tile Wall, pintunya menghadap ke arena")]
    public GameObject baseWallModel;
    [Tooltip("Panjang tembok dibanding deretan tile Wall (1 = pas)")]
    public float baseWallLength = 1.15f;
    public Vector3 baseWallOffset = Vector3.zero;

    [Header("Kerimbunan hutan (ditambah acak di luar arena)")]
    [Tooltip("Jumlah pohon tambahan di kiri, kanan & belakang arena")]
    public int extraTrees = 450;
    [Tooltip("Jumlah semak tambahan (juga di depan arena, karena pendek tidak menutupi arena)")]
    public int extraBushes = 160;
    [Tooltip("Jarak bebas dari tepi arena (meter dunia)")]
    public float forestClearance = 0.05f;
    [Tooltip("Jarak antar pohon di barisan rapat tepat di tepi arena (kiri, kanan, belakang). 0 = tanpa barisan tepi")]
    public float edgeTreeSpacing = 0.9f;
    [Tooltip("Seberapa jauh hutan tambahan menyebar dari arena (kali ukuran arena)")]
    public float forestDepth = 1.4f;
    [Tooltip("Warna lantai hutan paling luar (di luar model background)")]
    public Color outerGroundColor = new Color(0.2f, 0.33f, 0.15f);

    [Header("Tampilan lingkungan (background & markas)")]
    [Tooltip("Pohon & semak selalu tegak menghadap kamera (seperti sprite tower & musuh)")]
    public bool billboardFoliage = true;
    [Tooltip("Centang kalau gambar pohon terlihat terbalik kiri-kanan / gelap")]
    public bool flipFoliageFacing = false;
    [Tooltip("Goyangan angin pohon (derajat). 0 = diam")]
    [Range(0f, 8f)] public float windSway = 2.5f;
    public float windSpeed = 1.2f;
    [Tooltip("Pengali warna pohon, semak & daun (>1 = lebih cerah)")]
    [ColorUsage(false, true)] public Color foliageTint = new Color(1.15f, 1.3f, 1.05f);
    [Tooltip("Cahaya sendiri pohon: bagian yang membelakangi matahari tetap cerah")]
    [Range(0f, 1f)] public float foliageGlow = 0.3f;
    [ColorUsage(false, true)] public Color groundTint = new Color(1.15f, 1.25f, 1f);
    [Range(0f, 1f)] public float groundGlow = 0.15f;
    [Tooltip("Pengali warna batu & markas")]
    [ColorUsage(false, true)] public Color propTint = new Color(1.35f, 1.3f, 1.25f);
    [Tooltip("Variasi warna acak per objek supaya tidak seragam")]
    [Range(0f, 0.3f)] public float colorVariation = 0.12f;

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

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static string MaterialNames(Renderer r)
    {
        string names = "";
        foreach (Material m in r.sharedMaterials) if (m != null) names += m.name + "|";
        return names;
    }

    private static GameObject InstantiateStatic(GameObject prefab, Transform parent)
    {
        // Rotasi bawaan root dipertahankan (koreksi sumbu Blender)
        GameObject go = Instantiate(prefab, Vector3.zero, prefab.transform.rotation, parent);
        foreach (Collider col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
        return go;
    }

    /// <summary>
    /// Pasang hutan sekitar arena. Skala dipaskan supaya lubang di tengah model = ukuran arena,
    /// dan sisi yang lebih panjang (hutan lebat) selalu di belakang arena (jauh dari kamera).
    /// </summary>
    public GameObject SpawnBackground(Vector3 arenaCenter, Vector2 arenaSize, Transform parent,
        List<Vector3> forestTileSpots = null, List<Vector3> forestFrontSpots = null)
    {
        if (backgroundEnvironment == null) return null;

        GameObject go = InstantiateStatic(backgroundEnvironment, parent);
        go.name = "Background Environment";

        float fit = Mathf.Max(arenaSize.x / Mathf.Max(0.01f, backgroundHoleSize.x), arenaSize.y / Mathf.Max(0.01f, backgroundHoleSize.y));
        go.transform.localScale = go.transform.localScale * fit * Mathf.Max(0.01f, backgroundScale);
        go.transform.position = arenaCenter;

        // Bagian yang menjorok lebih jauh = sisi belakang (+Z, menjauhi kamera)
        Bounds b = RendererBounds(go);
        bool extendsToCamera = b.center.z < arenaCenter.z - 0.01f;
        if (extendsToCamera != flipBackground)
        {
            go.transform.RotateAround(arenaCenter, Vector3.up, 180f);
        }

        go.transform.position += backgroundOffset;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            // Pohon / semak latar tidak perlu melempar bayangan ke arena
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        EnvironmentFoliage.Apply(go, this, new Rect(arenaCenter.x - arenaSize.x * 0.5f, arenaCenter.z - arenaSize.y * 0.5f, arenaSize.x, arenaSize.y),
            forestTileSpots, forestFrontSpots);
        return go;
    }

    /// <summary>
    /// Pasang markas di atas deretan tile Wall: panjangnya mengikuti deretan, pintunya menghadap ke arena.
    /// </summary>
    public GameObject SpawnBaseWall(Vector3 wallCenter, Vector3 lineDirection, float lineLength, Vector3 towardArena, float groundY, Transform parent)
    {
        if (baseWallModel == null) return null;

        GameObject go = InstantiateStatic(baseWallModel, parent);
        go.name = "Base Wall";
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            go.transform.position = wallCenter;
            return go;
        }

        // Badan tembok = renderer bermaterial "BaseWall" (bukan sulur / pohon yang kotak batasnya bisa lebih besar);
        // cadangan: renderer non-daun terlebar. Pintu = renderer bermaterial "Door".
        Renderer body = null;
        Renderer door = null;
        foreach (Renderer r in renderers)
        {
            string matName = MaterialNames(r);
            if (door == null && matName.Contains("Door")) door = r;
            if (!matName.Contains("BaseWall") || matName.Contains("Lea") || matName.Contains("Bush") || matName.Contains("Tree")) continue;
            if (body == null || r.bounds.size.x * r.bounds.size.y > body.bounds.size.x * body.bounds.size.y) body = r;
        }
        if (body == null)
        {
            foreach (Renderer r in renderers)
            {
                string matName = MaterialNames(r);
                if (matName.Contains("Lea") || matName.Contains("Bush") || matName.Contains("Tree")) continue;
                if (body == null || r.bounds.size.x * r.bounds.size.y > body.bounds.size.x * body.bounds.size.y) body = r;
            }
            if (body == null) body = renderers[0];
        }

        // Sumbu panjang model asli = X. Putar supaya sejajar deretan tile Wall.
        Vector3 line = new Vector3(lineDirection.x, 0f, lineDirection.z).normalized;
        float yaw = Vector3.SignedAngle(Vector3.right, line, Vector3.up);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;

        // Pintu harus menghadap ke arena; kalau terbalik putar 180 derajat
        if (door != null)
        {
            Vector3 toDoor = door.bounds.center - body.bounds.center;
            toDoor.y = 0f;
            if (Vector3.Dot(toDoor, towardArena) < 0f) go.transform.Rotate(Vector3.up, 180f, Space.World);
        }

        // Skala: panjang badan tembok = panjang deretan Wall
        Vector3 size = body.bounds.size;
        float bodyLength = Mathf.Abs(line.x) > Mathf.Abs(line.z) ? size.x : size.z;
        if (bodyLength > 0.0001f)
        {
            go.transform.localScale = go.transform.localScale * (lineLength * baseWallLength / bodyLength);
        }

        // Tengah badan ke tengah deretan, alas ke permukaan tanah
        Bounds bodyBounds = body.bounds;
        Vector3 shift = new Vector3(wallCenter.x - bodyBounds.center.x, groundY - bodyBounds.min.y, wallCenter.z - bodyBounds.center.z);
        go.transform.position += shift + baseWallOffset;
        EnvironmentFoliage.Apply(go, this);
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
