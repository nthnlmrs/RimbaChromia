using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistem Fire Tile sesuai GDD:
/// - 1 tile api menyebar ke 2 tile acak setiap 3 detik, kandidatnya area "5 grid" (plus) di sekitarnya.
/// - Api naik level setiap 3 detik, maksimal level 3.
/// - Damage per detik: Lv1 = 5, Lv2 = 10, Lv3 = 15. Damage TIDAK menumpuk.
/// - Api hanya hidup di Grass Field (tidak di Bare Path / Forest / Wall).
/// - Saat jeda 15 detik antar wave: api TIDAK menyebar & TIDAK naik level, tapi tetap ada.
///
/// Sistem lain cukup memanggil:
///   FireManager.Instance.Ignite(cell)          -> musuh menyulut api
///   FireManager.Instance.ReduceFire(cell, n)   -> Green tower memadamkan api
///   FireManager.Instance.SetSimulationPaused() -> WaveManager saat jeda antar wave
/// </summary>
public class FireManager : MonoBehaviour
{
    public static FireManager Instance { get; private set; }

    public const int MaxFireLevel = 3;

    [Header("Spread (GDD: 2 tile tiap 3 detik, area 5 grid)")]
    [SerializeField] private float spreadInterval = 3f;
    [SerializeField] private int tilesPerSpread = 2;
    [SerializeField] private AreaPattern spreadArea = AreaPattern.Plus5;

    [Header("Level Up (GDD: naik level tiap 3 detik)")]
    [SerializeField] private float levelUpInterval = 3f;

    [Header("Damage per detik per level (index 0 = tidak terbakar)")]
    [SerializeField] private float[] damagePerSecond = { 0f, 5f, 10f, 15f };

    [Tooltip("Area di sekitar tile Wall yang dianggap 'mengenai' Wall. Damage tetap tidak menumpuk.")]
    [SerializeField] private AreaPattern wallExposureArea = AreaPattern.Square9;

    [Header("Pause (jeda antar wave)")]
    [Tooltip("Kalau true, api tidak menyebar dan tidak naik level.")]
    [SerializeField] private bool simulationPaused = false;
    [Tooltip("GDD: 'Fire persist' -> api yang sudah ada tetap memberi damage walau sedang jeda.")]
    [SerializeField] private bool damageWhilePaused = true;

    [Header("Api awal (opsional, untuk testing). x = row, y = column")]
    [SerializeField] private Vector2Int[] startingFires = new Vector2Int[0];

    [Header("Visual - Placeholder Warna (dipakai kalau model kosong)")]
    [SerializeField] private Color level1Tint = new Color(1.00f, 0.65f, 0.15f);
    [SerializeField] private Color level2Tint = new Color(0.95f, 0.30f, 0.05f);
    [SerializeField] private Color level3Tint = new Color(0.55f, 0.05f, 0.02f);

    // Model 3D api diambil dari aset ArenaArt di GridManager (Data/ArenaArt).
    // Kalau kosong, tile terbakar ditandai warna (level1Tint..level3Tint).

    [Header("Debug Controls (matikan sebelum build final)")]
    [SerializeField] private bool enableDebugControls = true;
    [Tooltip("Tahan F lalu klik/tekan: sulut api / naikkan level di tile bawah kursor")]
    [SerializeField] private Key debugIgniteKey = Key.F;
    [SerializeField] private Key debugExtinguishKey = Key.X;
    [SerializeField] private Key debugTogglePauseKey = Key.P;
    [SerializeField] private bool logFireEvents = false;

    private class FireState
    {
        public float spreadTimer;
        public float levelTimer;
        public GameObject model;
        public int modelLevel;
    }

    private readonly Dictionary<GridCell, FireState> burning = new Dictionary<GridCell, FireState>();
    private readonly List<GridCell> iterationBuffer = new List<GridCell>();
    private readonly List<GridCell> candidateBuffer = new List<GridCell>();
    private readonly List<GridCell> wallCells = new List<GridCell>();

    private GridManager grid;
    private WallHealth wall;
    private Transform modelContainer;

    public bool IsSimulationPaused => simulationPaused;
    public int BurningCount => burning.Count;
    public IReadOnlyCollection<GridCell> BurningCells => burning.Keys;

    public event Action<GridCell, int> OnFireLevelChanged; // (cell, level baru). level 0 = padam

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ada lebih dari satu FireManager di scene. Yang baru dihapus.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        grid = GridManager.Instance != null ? GridManager.Instance : FindFirstObjectByType<GridManager>();
        wall = FindFirstObjectByType<WallHealth>();

        if (grid == null)
        {
            Debug.LogError("FireManager tidak menemukan GridManager di scene.");
            enabled = false;
            return;
        }

        GameObject container = new GameObject("Fire Visuals");
        container.transform.SetParent(transform, false);
        modelContainer = container.transform;

        if (grid.IsGridReady)
        {
            HandleGridReady();
        }
        else
        {
            grid.OnGridGenerated += HandleGridReady;
        }
    }

    private void HandleGridReady()
    {
        grid.OnGridGenerated -= HandleGridReady;

        wallCells.Clear();
        foreach (GridCell cell in grid.AllCells())
        {
            if (cell.Type == TileType.Wall)
            {
                wallCells.Add(cell);
            }
        }

        foreach (Vector2Int coord in startingFires)
        {
            Ignite(grid.GetCell(coord.x, coord.y));
        }
    }

    private void Update()
    {
        if (grid == null || !grid.IsGridReady)
        {
            return;
        }

        if (enableDebugControls)
        {
            HandleDebugInput();
        }

        float dt = Time.deltaTime;

        if (!simulationPaused)
        {
            TickSpreadAndLevel(dt);
        }

        if (!simulationPaused || damageWhilePaused)
        {
            ApplyFireDamage(dt);
        }
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Sulut api di cell. Kalau sudah terbakar, levelnya dinaikkan ke minimal 'level'.</summary>
    public bool Ignite(GridCell cell, int level = 1)
    {
        if (cell == null || !cell.IsFlammable || level <= 0)
        {
            return false;
        }

        level = Mathf.Clamp(level, 1, MaxFireLevel);

        if (burning.ContainsKey(cell))
        {
            if (level > cell.FireLevel)
            {
                SetLevel(cell, level);
            }
            return true;
        }

        burning.Add(cell, new FireState());
        SetLevel(cell, level);
        return true;
    }

    /// <summary>Kurangi level api. Kalau jadi 0, api padam. Return true kalau ada api yang dikurangi.</summary>
    public bool ReduceFire(GridCell cell, int amount)
    {
        if (cell == null || amount <= 0 || !burning.TryGetValue(cell, out FireState state))
        {
            return false;
        }

        int newLevel = cell.FireLevel - amount;

        if (newLevel <= 0)
        {
            Extinguish(cell);
        }
        else
        {
            state.levelTimer = 0f; // beri jeda sebelum api naik level lagi
            SetLevel(cell, newLevel);
        }

        return true;
    }

    public void Extinguish(GridCell cell)
    {
        if (cell == null || !burning.TryGetValue(cell, out FireState state))
        {
            return;
        }

        if (state.model != null)
        {
            Destroy(state.model);
        }

        burning.Remove(cell);
        cell.SetFireLevel(0);
        cell.SetFireTint(false, Color.white);
        cell.SetBaseVisible(true);

        if (logFireEvents) Debug.Log($"Api padam di Cell ({cell.Row}, {cell.Column}).");
        OnFireLevelChanged?.Invoke(cell, 0);
    }

    public void ExtinguishAll()
    {
        iterationBuffer.Clear();
        iterationBuffer.AddRange(burning.Keys);
        foreach (GridCell cell in iterationBuffer)
        {
            Extinguish(cell);
        }
    }

    /// <summary>Dipanggil WaveManager: true saat jeda/persiapan, false saat wave berjalan.</summary>
    public void SetSimulationPaused(bool paused)
    {
        simulationPaused = paused;
        Debug.Log(paused ? "Api: JEDA (tidak menyebar / naik level)." : "Api: AKTIF kembali.");
    }

    public float GetDamagePerSecond(int level)
    {
        if (damagePerSecond == null || damagePerSecond.Length == 0) return 0f;
        return damagePerSecond[Mathf.Clamp(level, 0, damagePerSecond.Length - 1)];
    }

    /// <summary>
    /// Target terbaik untuk tower healer:
    /// 1) tile terbakar yang ada tower-nya, 2) tile yang mengenai Wall, 3) level api tertinggi,
    /// tie-break: paling dekat ke 'from'.
    /// </summary>
    public GridCell FindPriorityFireTarget(Vector3 from)
    {
        GridCell best = null;
        float bestScore = float.MinValue;

        foreach (GridCell cell in burning.Keys)
        {
            if (cell == null) continue;

            float score = cell.FireLevel * 10f;
            if (cell.IsOccupied) score += 100f;
            if (IsExposingWall(cell)) score += 50f;
            score -= Vector3.Distance(from, cell.transform.position) * 0.01f;

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------
    // Simulasi
    // ------------------------------------------------------------------

    private void TickSpreadAndLevel(float dt)
    {
        if (burning.Count == 0) return;

        iterationBuffer.Clear();
        iterationBuffer.AddRange(burning.Keys);

        List<GridCell> toIgnite = null;

        foreach (GridCell cell in iterationBuffer)
        {
            if (cell == null || !burning.TryGetValue(cell, out FireState state)) continue;

            // Naik level
            if (cell.FireLevel < MaxFireLevel)
            {
                state.levelTimer += dt;
                if (state.levelTimer >= levelUpInterval)
                {
                    state.levelTimer -= levelUpInterval;
                    SetLevel(cell, cell.FireLevel + 1);
                }
            }
            else
            {
                state.levelTimer = 0f;
            }

            // Menyebar
            state.spreadTimer += dt;
            if (state.spreadTimer >= spreadInterval)
            {
                state.spreadTimer -= spreadInterval;
                if (toIgnite == null) toIgnite = new List<GridCell>();
                PickSpreadTargets(cell, toIgnite);
            }
        }

        // Disulut setelah iterasi supaya tile baru tidak ikut menyebar di frame yang sama
        if (toIgnite != null)
        {
            foreach (GridCell target in toIgnite)
            {
                if (!burning.ContainsKey(target))
                {
                    Ignite(target, 1);
                }
            }
        }
    }

    private void PickSpreadTargets(GridCell source, List<GridCell> output)
    {
        candidateBuffer.Clear();

        foreach (GridCell neighbor in grid.GetCellsInArea(source.Row, source.Column, spreadArea))
        {
            if (neighbor == source || !neighbor.IsFlammable) continue;
            if (burning.ContainsKey(neighbor) || output.Contains(neighbor)) continue;
            candidateBuffer.Add(neighbor);
        }

        int count = Mathf.Min(tilesPerSpread, candidateBuffer.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(i, candidateBuffer.Count);
            (candidateBuffer[i], candidateBuffer[pick]) = (candidateBuffer[pick], candidateBuffer[i]);
            output.Add(candidateBuffer[i]);
        }
    }

    private void ApplyFireDamage(float dt)
    {
        if (burning.Count == 0) return;

        // Tower: 1 tower hanya berdiri di 1 tile, jadi otomatis tidak menumpuk
        iterationBuffer.Clear();
        iterationBuffer.AddRange(burning.Keys);
        foreach (GridCell cell in iterationBuffer)
        {
            if (cell == null || !cell.IsOccupied || cell.OccupyingTower == null) continue;
            cell.OccupyingTower.TakeDamage(GetDamagePerSecond(cell.FireLevel) * dt);
        }

        // Wall: ambil level api TERTINGGI yang mengenai wall, bukan dijumlah (GDD: tidak stack)
        if (wall != null && !wall.IsDestroyed && wallCells.Count > 0)
        {
            int highestLevel = 0;
            foreach (GridCell wallCell in wallCells)
            {
                foreach (GridCell near in grid.GetCellsInArea(wallCell.Row, wallCell.Column, wallExposureArea))
                {
                    if (near.FireLevel > highestLevel) highestLevel = near.FireLevel;
                }
            }

            if (highestLevel > 0)
            {
                wall.TakeDamage(GetDamagePerSecond(highestLevel) * dt);
            }
        }
    }

    private bool IsExposingWall(GridCell cell)
    {
        foreach (GridCell near in grid.GetCellsInArea(cell.Row, cell.Column, wallExposureArea))
        {
            if (near.Type == TileType.Wall) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Visual
    // ------------------------------------------------------------------

    private void SetLevel(GridCell cell, int level)
    {
        level = Mathf.Clamp(level, 1, MaxFireLevel);
        cell.SetFireLevel(level);
        UpdateModel(cell, level);
        // Warna placeholder hanya dipakai kalau tidak ada model api 3D
        bool hasModel = burning.TryGetValue(cell, out FireState st) && st.model != null;
        cell.SetFireTint(!hasModel, GetTint(level));

        if (logFireEvents) Debug.Log($"Api Lv{level} di Cell ({cell.Row}, {cell.Column}).");
        OnFireLevelChanged?.Invoke(cell, level);
    }

    private Color GetTint(int level)
    {
        switch (level)
        {
            case 1: return level1Tint;
            case 2: return level2Tint;
            default: return level3Tint;
        }
    }

    private void UpdateModel(GridCell cell, int level)
    {
        if (!burning.TryGetValue(cell, out FireState state)) return;
        if (state.model != null && state.modelLevel == level) return;

        ArenaArt art = grid != null ? grid.Art : null;
        GameObject prefab = art != null ? ArenaArt.PickRandom(art.GetFireModels(level)) : null;

        if (state.model != null)
        {
            Destroy(state.model);
            state.model = null;
        }

        if (prefab == null)
        {
            cell.SetBaseVisible(true);
            return;
        }

        // Model api menggantikan tile rumput: disesuaikan otomatis ke ukuran 1 tile
        float groundY = cell.transform.position.y - cell.transform.lossyScale.y * 0.5f;
        state.model = art.SpawnFitted(prefab, cell.transform.position, grid.CellSize, groundY, modelContainer, out _);
        state.model.name = $"Fire_L{level}_R{cell.Row}_C{cell.Column}";
        state.modelLevel = level;
        cell.SetBaseVisible(false);
    }

    // ------------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------------

    private void HandleDebugInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[debugTogglePauseKey].wasPressedThisFrame)
        {
            SetSimulationPaused(!simulationPaused);
        }

        bool ignite = keyboard[debugIgniteKey].wasPressedThisFrame;
        bool extinguish = keyboard[debugExtinguishKey].wasPressedThisFrame;
        if (!ignite && !extinguish) return;

        GridCell cell = grid.GetCellUnderCursor();
        if (cell == null) return;

        if (ignite)
        {
            if (!cell.IsFlammable)
            {
                Debug.Log($"[Debug] Cell ({cell.Row}, {cell.Column}) bertipe {cell.Type}, tidak bisa terbakar.");
                return;
            }

            Ignite(cell, Mathf.Min(cell.FireLevel + 1, MaxFireLevel));
            Debug.Log($"[Debug] Api Lv{cell.FireLevel} di Cell ({cell.Row}, {cell.Column}).");
        }
        else
        {
            Extinguish(cell);
        }
    }
}
