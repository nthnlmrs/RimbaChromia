using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Size")]
    [SerializeField] private int rows = 17;
    [SerializeField] private int columns = 21;
    [SerializeField] private float cellSize = 1f;

    [Header("Grid Assets")]
    [SerializeField] private GridCell cellPrefab;
    [SerializeField] private Material grassMaterial;
    [SerializeField] private Material barePathMaterial;
    [SerializeField] private Material forestMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material selectedMaterial;
    [SerializeField] private Material hoverMaterial;

    [Header("Arena 3D (model FBX tile)")]
    [Tooltip("Aset Data/ArenaArt: model rumput, api, dll. Kosong = semua tile pakai kotak placeholder")]
    [SerializeField] private ArenaArt arenaArt;

    [Header("Systems")]
    [SerializeField] private InsightManager insightManager;
    [SerializeField] private TowerData selectedTowerData;
    [SerializeField] private TowerData[] towerHotbar;

    [Header("Tower Placement")]
    [SerializeField] private Tower towerPrefab;
    [SerializeField] private float towerHeightOffset = 0.575f;

    [Header("Raycast")]
    [SerializeField] private LayerMask gridLayerMask = ~0;
    [SerializeField] private float raycastDistance = 500f;

    [Header("Generated Objects")]
    [SerializeField] private Transform cellContainer;
    [SerializeField] private Transform towerContainer;

    private GridCell[,] cells;
    private GridCell selectedCell;
    private GridCell hoveredCell;
    private Camera mainCamera;

    public int Rows => rows;
    public int Columns => columns;
    public float CellSize => cellSize;
    public bool IsGridReady => cells != null;

    // Mode penempatan: aktif setelah pilih tower (tombol angka), selesai setelah tower dipasang / dibatalkan
    private bool isPlacing;
    public bool IsPlacing => isPlacing && selectedTowerData != null;
    public TowerData SelectedTowerData => selectedTowerData;
    public GridCell HoveredCell => hoveredCell;
    public Tower TowerPrefab => towerPrefab;
    public GridCell SelectedCell => selectedCell;
    public IReadOnlyList<TowerData> Hotbar => towerHotbar ?? Array.Empty<TowerData>();

    /// <summary>Frame terakhir Esc/klik kanan dipakai untuk membatalkan sesuatu (supaya Esc tidak sekaligus membuka pause).</summary>
    public int LastCancelFrame { get; private set; } = -1;

    /// <summary>Pesan untuk pemain saat merge / pemasangan ditolak (ditampilkan GameHUD).</summary>
    public event Action<string> OnActionRejected;

    /// <summary>Alasan merge ditolak, dalam bahasa pemain. Null kalau bukan kasus merge.</summary>
    private static string MergeRejectReason(Tower target, TowerData incoming)
    {
        if (target == null || target.Data == null || incoming == null) return null;
        if (target.Data.color != incoming.color) return "Merge needs two towers of the same type";
        if (target.Data.tier != incoming.tier)
            return $"Merge needs two towers of the same level (Level {incoming.tier} + Level {target.Data.tier})";
        if (target.Data.nextTier == null) return "This tower is already at max level";
        return null;
    }
    public float TowerHeightOffset => towerHeightOffset;

    public bool CanAfford(TowerData towerData)
    {
        return towerData != null && insightManager != null && insightManager.HasEnoughInsight(towerData.cost);
    }

    /// <summary>Hasil kalau tower dilepas di cell ini: bisa pasang baru, bisa merge, atau tidak valid.</summary>
    public PlacementResult EvaluatePlacement(GridCell cell, TowerData towerData)
    {
        if (cell == null || towerData == null) return PlacementResult.Invalid;
        if (cell.CanMergeWith(towerData)) return PlacementResult.Merge;
        if (cell.CanPlaceTower(towerData)) return PlacementResult.Place;
        return PlacementResult.Invalid;
    }

    // Dipanggil sekali setelah semua cell selesai dibuat (dipakai FireManager, WaveManager, dll.)
    public event Action OnGridGenerated;

    private void Awake()
    {
        Instance = this;

        // Sistem api wajib ada. Kalau belum dipasang di scene, buat otomatis
        // di GameObject yang sama supaya game tetap jalan tanpa setup manual.
        if (FindFirstObjectByType<FireManager>() == null)
        {
            gameObject.AddComponent<FireManager>();
        }

        // Bayangan tower + area jangkauan saat menaruh tower
        if (GetComponent<PlacementPreview>() == null)
        {
            gameObject.AddComponent<PlacementPreview>();
        }

        // Rumput tertiup angin
        if (GetComponent<GrassWind>() == null)
        {
            gameObject.AddComponent<GrassWind>();
        }

        // Kamera spectator (WASD geser, scroll zoom, R reset), tanpa rotasi
        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraController>() == null)
        {
            cam.gameObject.AddComponent<CameraController>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Layout yang benar-benar dipakai. Index 0 = baris paling ATAS layar.
    private string[] mapLayout;
    private readonly Dictionary<int, Vector2Int> spawnPoints = new Dictionary<int, Vector2Int>();

    // Fallback kalau scene tidak punya WaveManager / StageData (map test lama)
    private readonly string[] defaultLayout =
    {
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "1PPPPPPPPPPPPPPPPPPPW",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG"
    };

    private void Start()
    {
        mainCamera = Camera.main;

        StageData stage = WaveManager.Instance != null ? WaveManager.Instance.CurrentStage : null;
        mapLayout = stage != null ? stage.GetPlayLayout() : defaultLayout;

        if (mapLayout.Length > 0)
        {
            rows = mapLayout.Length;
            columns = mapLayout[0].Length;
        }

        if (!ValidateLayout())
        {
            enabled = false;
            return;
        }

        GenerateGrid();
    }

    private void Update()
    {
        // Game di-pause / Victory / Defeat -> grid tidak menerima input
        bool paused = Time.timeScale <= 0f;

        if (!paused)
        {
            HandleHotbarShortcuts();
        }

        if (Mouse.current == null)
        {
            return;
        }

        // Kursor di atas UI (kartu tower, tombol, panel) -> jangan tembus ke grid
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        GridCell cellUnderCursor = (paused || overUI) ? null : GetCellUnderCursor();

        // Hover biasa (tidak sedang menaruh / drag) pakai tile di TANAH, supaya area tower
        // hanya muncul saat kursor tepat di tile-nya, bukan saat lewat di atas badan sprite-nya.
        bool targeting = IsPlacing || dragSource != null;
        UpdateHover(targeting || paused || overUI ? cellUnderCursor : lastGroundCell);

        if (paused)
        {
            return;
        }

        // Batal menaruh tower / batal drag: klik kanan / Esc
        bool cancel = Mouse.current.rightButton.wasPressedThisFrame ||
                      (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        if (cancel && (isPlacing || dragSource != null))
        {
            CancelPlacement();
            dragSource = null;
            LastCancelFrame = Time.frameCount;
        }

        HandleDragMerge(cellUnderCursor);

        if (Mouse.current.leftButton.wasPressedThisFrame && cellUnderCursor != null)
        {
            HandleCellClicked(cellUnderCursor);
        }
    }

    /// <summary>Dipanggil kartu tower di UI nanti (Prioritas 4) untuk masuk mode penempatan.</summary>
    public void SetSelectedTowerData(TowerData newTowerData)
    {
        selectedTowerData = newTowerData;
        isPlacing = newTowerData != null;
    }

    public void CancelPlacement()
    {
        isPlacing = false;
    }

    /// <summary>
    /// Dipakai kartu tower saat di-drag lalu dilepas di atas grid.
    /// Return true kalau tower berhasil dipasang / di-merge.
    /// </summary>
    public bool TryPlaceSelectedAt(GridCell cell)
    {
        if (cell == null || selectedTowerData == null)
        {
            return false;
        }

        bool placed = TryPlaceOrMergeTower(cell);
        if (placed)
        {
            SelectCell(cell);
        }
        return placed;
    }

    public void ClearSelection()
    {
        if (selectedCell != null)
        {
            selectedCell.DeselectCell();
            selectedCell = null;
        }
    }

    // ------------------------------------------------------------------
    // Drag-merge (GDD: "drag the same kind tower and place it in the tower you want to upgrade")
    // ------------------------------------------------------------------

    private Tower dragSource;
    private Vector2 dragStartMouse;
    public Tower DragSource => dragSource;

    private void HandleDragMerge(GridCell cellUnderCursor)
    {
        Mouse mouse = Mouse.current;

        // Mulai drag: tekan kiri di atas tower yang sudah terpasang (bukan sedang mode menaruh tower)
        if (mouse.leftButton.wasPressedThisFrame && !IsPlacing &&
            cellUnderCursor != null && cellUnderCursor.OccupyingTower != null)
        {
            dragSource = cellUnderCursor.OccupyingTower;
            dragStartMouse = mouse.position.ReadValue();
            return;
        }

        if (dragSource == null)
        {
            return;
        }

        // Batal drag
        if (mouse.rightButton.wasPressedThisFrame ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            dragSource = null;
            return;
        }

        if (!mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        Tower source = dragSource;
        dragSource = null;

        if (source == null)
        {
            return;
        }

        Tower target = cellUnderCursor != null ? cellUnderCursor.OccupyingTower : null;
        if (target != null && target != source && target.CanMergeWith(source.Data))
        {
            // Gratis: kedua tower sudah dibayar saat dipasang. Tower sumber dilebur ke target.
            source.ConsumeForMerge();
            target.TryMergeUpgrade();
            return;
        }

        // Gagal merge. Alasan diambil dari tower yang BENAR-BENAR ditunjuk kursor (bukan tile di bawahnya),
        // supaya pemain selalu tahu kenapa, termasuk saat tower bertumpuk.
        Tower pointed = lastPointedTower != null ? lastPointedTower : (target != source ? target : null);
        // Klik biasa (kursor hampir tidak bergeser) bukan percobaan merge -> tanpa pesan
        bool moved = (mouse.position.ReadValue() - dragStartMouse).sqrMagnitude > 14f * 14f;
        if (pointed == null || !moved)
        {
            return;
        }

        string reason = MergeRejectReason(pointed, source.Data) ?? "Drop the tower onto an identical tower to merge";
        Debug.Log($"Drag-merge gagal: {source.TowerName} (T{source.TowerTier}) -> {pointed.TowerName} (T{pointed.TowerTier}). {reason}");
        OnActionRejected?.Invoke(reason);
    }

    private void HandleHotbarShortcuts()
    {
        if (Keyboard.current == null || towerHotbar == null || towerHotbar.Length == 0)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectHotbarSlot(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectHotbarSlot(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectHotbarSlot(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SelectHotbarSlot(3);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) SelectHotbarSlot(4);
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) SelectHotbarSlot(5);
    }

    private void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= towerHotbar.Length || towerHotbar[index] == null)
        {
            return;
        }

        // Tekan slot yang sama lagi = batal
        if (isPlacing && selectedTowerData == towerHotbar[index])
        {
            CancelPlacement();
            return;
        }

        selectedTowerData = towerHotbar[index];
        isPlacing = true;
        Debug.Log($"Tower dipilih [Slot {index + 1}]: {selectedTowerData.towerName} (Cost: {selectedTowerData.cost})");
    }

    private bool ValidateLayout()
    {
        if (mapLayout.Length != rows)
        {
            Debug.LogError(
                $"Jumlah baris layout adalah {mapLayout.Length}, " +
                $"tetapi GridManager membutuhkan {rows} baris."
            );
            return false;
        }

        for (int row = 0; row < mapLayout.Length; row++)
        {
            if (mapLayout[row].Length != columns)
            {
                Debug.LogError(
                    $"Layout row {row} mempunyai {mapLayout[row].Length} karakter. " +
                    $"Seharusnya {columns} karakter."
                );
                return false;
            }
        }

        return true;
    }

    private GridCell lastGroundCell;

    /// <summary>Tower paling depan di bawah kursor selain tower yang sedang di-drag (untuk pesan alasan).</summary>
    private Tower lastPointedTower;

    /// <summary>
    /// Tile yang ditunjuk kursor untuk AKSI (klik, pasang, merge). Sprite tower yang tinggi bisa menutupi
    /// tile di belakangnya, jadi aturannya:
    ///  - Sedang menaruh tower: tile di tanah, KECUALI kursor di badan tower yang bisa di-merge dengan kartu ini.
    ///  - Sedang drag-merge   : tile di tanah, KECUALI kursor di badan tower yang bisa jadi tujuan merge.
    ///  - Selain itu (klik)   : badan tower diutamakan (klik sprite = pilih / mulai drag tower itu).
    /// </summary>
    public GridCell GetCellUnderCursor()
    {
        lastGroundCell = null;
        lastPointedTower = null;

        if (Mouse.current == null)
        {
            return null;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError("Main Camera tidak ditemukan. Pastikan kamera memiliki tag MainCamera.");
            return null;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        // RaycastAll: collider tower / musuh / player tidak boleh "menutupi" tile di bawahnya
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, gridLayerMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return null;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        GridCell ground = null;
        foreach (RaycastHit hit in hits)
        {
            GridCell cell = hit.collider.GetComponentInParent<GridCell>();
            if (cell != null)
            {
                ground = cell;
                break;
            }
        }

        // Tower dipilih per PIXEL sprite (area transparan di sekitar sprite tidak menghalangi).
        //  - towerHit     : tower paling depan di bawah kursor (untuk klik biasa)
        //  - mergeTarget  : tower yang BISA di-merge di sepanjang garis kursor, walau tertutup tower lain
        //                   di depannya (misal Red di belakang Green Sprout yang tinggi)
        TowerData mergeData = IsPlacing ? selectedTowerData : dragSource != null ? dragSource.Data : null;
        Tower towerHit = null;
        Tower mergeTarget = null;
        float nearest = float.MaxValue;
        float nearestMerge = float.MaxValue;
        float pointedDistance = float.MaxValue;
        foreach (Tower tower in Tower.ActiveTowers)
        {
            if (tower == null || tower.OccupiedCell == null) continue;
            if (!tower.RaycastSprite(ray, out float distance)) continue;

            if (distance < nearest)
            {
                nearest = distance;
                towerHit = tower;
            }

            if (tower != dragSource && (lastPointedTower == null || distance < pointedDistance))
            {
                pointedDistance = distance;
                lastPointedTower = tower;
            }

            bool canMerge = mergeData != null && tower != dragSource && tower.CanMergeWith(mergeData);
            if (canMerge && distance < nearestMerge)
            {
                nearestMerge = distance;
                mergeTarget = tower;
            }
        }

        // Tile tanah di bawah kursor yang berisi pasangan merge juga dihitung
        if (mergeTarget == null && mergeData != null && ground != null)
        {
            Tower onGround = ground.OccupyingTower;
            if (onGround != null && onGround != dragSource && onGround.CanMergeWith(mergeData)) mergeTarget = onGround;
        }

        // Cadangan saat tower bertumpuk: pasangan merge yang badannya paling dekat ke kursor di layar
        if (mergeTarget == null && mergeData != null)
        {
            mergeTarget = NearestMergeTargetOnScreen(mergeData, mousePosition);
        }

        if (mergeData != null)
        {
            lastGroundCell = ground;
            return mergeTarget != null ? mergeTarget.OccupiedCell : ground;
        }

        // Klik biasa: tower paling depan di bawah kursor, kalau tidak ada pakai tile tanah
        lastGroundCell = ground;
        return towerHit != null ? towerHit.OccupiedCell : ground;
    }

    /// <summary>
    /// Tower pasangan merge yang garis badannya (kaki sampai puncak) paling dekat ke kursor di layar,
    /// dalam radius kecil. Membantu saat tower tertutup beberapa tower lain di depannya.
    /// </summary>
    private Tower NearestMergeTargetOnScreen(TowerData mergeData, Vector2 mouse)
    {
        float radius = 70f * Screen.height / 1080f;
        float best = radius * radius;
        Tower result = null;

        foreach (Tower tower in Tower.ActiveTowers)
        {
            if (tower == null || tower == dragSource || tower.OccupiedCell == null || !tower.CanMergeWith(mergeData)) continue;

            Vector3 foot = mainCamera.WorldToScreenPoint(tower.transform.position);
            Vector3 top = mainCamera.WorldToScreenPoint(tower.MuzzlePosition);
            if (foot.z <= 0f || top.z <= 0f) continue;

            float d = DistanceToSegmentSqr(mouse, foot, top);
            if (d < best)
            {
                best = d;
                result = tower;
            }
        }
        return result;
    }

    private static float DistanceToSegmentSqr(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return (a + ab * t - p).sqrMagnitude;
    }

    private void UpdateHover(GridCell cellUnderCursor)
    {
        if (hoveredCell == cellUnderCursor)
        {
            return;
        }

        if (hoveredCell != null)
        {
            hoveredCell.SetHovered(false);
        }

        hoveredCell = cellUnderCursor;

        if (hoveredCell != null)
        {
            hoveredCell.SetHovered(true);
        }
    }

    private void GenerateGrid()
    {
        if (!ValidateReferences())
        {
            return;
        }

        CreateContainers();

        GridCell[,] newCells = new GridCell[rows, columns];
        spawnPoints.Clear();

        float gridWidth = (columns - 1) * cellSize;
        float gridHeight = (rows - 1) * cellSize;

        Vector3 gridOffset = new Vector3(
            -gridWidth / 2f,
            0f,
            -gridHeight / 2f
        );

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                // Row 0 ada di dekat kamera (bawah layar), jadi baris teks dibaca dari bawah
                char tileCode = mapLayout[rows - 1 - row][column];
                TileType tileType = GetTileType(tileCode);

                if (char.IsDigit(tileCode) && tileCode != '0')
                {
                    spawnPoints[tileCode - '0'] = new Vector2Int(row, column);
                }
                Material tileMaterial = GetTileMaterial(tileType);

                Vector3 cellPosition = new Vector3(
                    column * cellSize,
                    0f,
                    row * cellSize
                );

                cellPosition += gridOffset;
                cellPosition += transform.position;

                GridCell newCell = Instantiate(
                    cellPrefab,
                    cellPosition,
                    Quaternion.identity,
                    cellContainer
                );

                // Jalur tanpa celah antar tile, supaya terlihat menyambung mulus (bukan kotak-kotak)
                float tileWidth = tileType == TileType.BarePath ? cellSize : cellSize * 0.95f;
                newCell.transform.localScale = new Vector3(
                    tileWidth,
                    0.15f,
                    tileWidth
                );

                newCell.Setup(
                    row,
                    column,
                    tileType,
                    tileMaterial,
                    selectedMaterial,
                    hoverMaterial
                );

                newCells[row, column] = newCell;
            }
        }

        // Baru di-assign setelah semua cell jadi, supaya IsGridReady tidak true terlalu cepat
        cells = newCells;

        SpawnArenaArt();
        CreateOuterGround();

        Debug.Log($"Grid berhasil dibuat: {rows} x {columns} = {rows * columns} cell.");
        OnGridGenerated?.Invoke();
    }

    // ------------------------------------------------------------------
    // Visual arena 3D
    // ------------------------------------------------------------------

    public ArenaArt Art => arenaArt;

    /// <summary>Permukaan tertinggi di arena (dipakai efek datar supaya tidak tertutup model tile).</summary>
    public float MaxSurfaceY { get; private set; } = 0.075f;

    /// <summary>Batas arena di dunia (X/Z), termasuk baris hutan di pinggir.</summary>
    public Rect WorldBounds
    {
        get
        {
            float halfW = columns * cellSize * 0.5f;
            float halfH = rows * cellSize * 0.5f;
            Vector3 c = transform.position;
            return new Rect(c.x - halfW, c.z - halfH, halfW * 2f, halfH * 2f);
        }
    }

    private void SpawnArenaArt()
    {
        MaxSurfaceY = 0.075f;
        if (arenaArt == null) return;

        Transform artRoot = new GameObject("Arena Art (3D)").transform;
        artRoot.SetParent(transform, false);

        int spawned = 0;
        List<Vector3> forestSpots = new List<Vector3>();
        List<Vector3> forestFrontSpots = new List<Vector3>();
        foreach (GridCell cell in AllCells())
        {
            GameObject prefab = ArenaArt.PickRandom(arenaArt.GetModels(cell.Type));
            if (prefab == null && cell.Type == TileType.Wall && arenaArt.baseWallModel != null)
            {
                // Markas berdiri di atas tanah berumput
                prefab = ArenaArt.PickRandom(arenaArt.grassModels);
            }
            bool forestCell = prefab == null && cell.Type == TileType.Forest && arenaArt.backgroundEnvironment != null;
            if (forestCell)
            {
                // Tile hutan pinggir arena: tanah berumput + pohon dari model background
                prefab = ArenaArt.PickRandom(arenaArt.grassModels);
            }
            if (prefab == null)
            {
                MaxSurfaceY = Mathf.Max(MaxSurfaceY, cell.SurfaceY);
                continue;
            }

            float groundY = cell.transform.position.y - cell.transform.lossyScale.y * 0.5f;
            GameObject model = arenaArt.SpawnFitted(prefab, cell.transform.position, cellSize, groundY, artRoot, out float topY);
            model.name = $"{prefab.name}_R{cell.Row}_C{cell.Column}";
            cell.SetArtModel(model, topY);
            if (forestCell)
            {
                // Baris terdepan (dekat kamera) cukup semak, supaya tidak menutupi arena
                (cell.Row == 0 ? forestFrontSpots : forestSpots).Add(new Vector3(cell.transform.position.x, topY, cell.transform.position.z));
            }
            MaxSurfaceY = Mathf.Max(MaxSurfaceY, topY);
            spawned++;
        }

        Debug.Log($"Arena Art: {spawned} model tile 3D dipasang.");

        arenaArt.SpawnBackground(transform.position, new Vector2(columns * cellSize, rows * cellSize), artRoot, forestSpots, forestFrontSpots);
        SpawnBaseWall(artRoot);
    }

    /// <summary>Markas di atas deretan tile Wall, pintu menghadap ke tengah arena.</summary>
    private void SpawnBaseWall(Transform artRoot)
    {
        if (arenaArt.baseWallModel == null) return;

        List<GridCell> walls = new List<GridCell>();
        foreach (GridCell cell in AllCells())
        {
            if (cell.Type == TileType.Wall) walls.Add(cell);
        }
        if (walls.Count == 0) return;

        Vector3 min = walls[0].transform.position, max = min, sum = Vector3.zero;
        float groundY = float.MinValue;
        foreach (GridCell cell in walls)
        {
            Vector3 p = cell.transform.position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            sum += p;
            groundY = Mathf.Max(groundY, cell.SurfaceY);
        }
        Vector3 center = sum / walls.Count;

        bool alongX = (max.x - min.x) >= (max.z - min.z);
        Vector3 line = alongX ? Vector3.right : Vector3.forward;
        float length = (alongX ? max.x - min.x : max.z - min.z) + cellSize;

        Vector3 toArena = transform.position - center;
        toArena = alongX ? new Vector3(0f, 0f, Mathf.Sign(toArena.z)) : new Vector3(Mathf.Sign(toArena.x), 0f, 0f);

        arenaArt.SpawnBaseWall(center, line, length, toArena, groundY, artRoot);
    }

    /// <summary>
    /// Lantai hutan luas di bawah arena, supaya area di luar map & celah antar tile tidak kosong
    /// (GDD: dark green = surrounding forest).
    /// </summary>
    private void CreateOuterGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Outer Forest Ground";
        ground.transform.SetParent(transform, false);
        ground.transform.position = transform.position + Vector3.down * 0.08f;
        ground.transform.localScale = new Vector3(14f, 1f, 14f); // plane bawaan 10x10 -> 140x140 unit
        Destroy(ground.GetComponent<Collider>());

        MeshRenderer mr = ground.GetComponent<MeshRenderer>();
        if (forestMaterial != null) mr.sharedMaterial = forestMaterial;
        if (arenaArt != null && arenaArt.backgroundEnvironment != null)
        {
            // Lantai hutan di luar model background: hijau tua senada, bukan hijau terang placeholder
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", arenaArt.outerGroundColor);
            block.SetColor("_Color", arenaArt.outerGroundColor);
            mr.SetPropertyBlock(block);
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private bool ValidateReferences()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Cell Prefab belum dipasang pada GridManager.");
            return false;
        }

        if (grassMaterial == null)
        {
            Debug.LogError("Grass Material belum dipasang pada GridManager.");
            return false;
        }

        if (barePathMaterial == null)
        {
            Debug.LogError("Bare Path Material belum dipasang pada GridManager.");
            return false;
        }

        if (forestMaterial == null)
        {
            Debug.LogError("Forest Material belum dipasang pada GridManager.");
            return false;
        }

        if (wallMaterial == null)
        {
            Debug.LogError("Wall Material belum dipasang pada GridManager.");
            return false;
        }

        if (selectedMaterial == null)
        {
            Debug.LogError("Selected Material belum dipasang pada GridManager.");
            return false;
        }

        return true;
    }

    private TileType GetTileType(char tileCode)
    {
        switch (tileCode)
        {
            case 'G':
                return TileType.Grass;
            case 'P':
                return TileType.BarePath;
            case 'F':
                return TileType.Forest;
            case 'W':
                return TileType.Wall;
            case char digit when char.IsDigit(digit) && digit != '0':
                return TileType.BarePath; // titik spawn musuh
            default:
                Debug.LogWarning($"Kode tile '{tileCode}' tidak dikenal. Tile diubah menjadi Grass.");
                return TileType.Grass;
        }
    }

    private Material GetTileMaterial(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass:
                return grassMaterial;
            case TileType.BarePath:
                return barePathMaterial;
            case TileType.Forest:
                return forestMaterial;
            case TileType.Wall:
                return wallMaterial;
            default:
                return grassMaterial;
        }
    }

    private void CreateContainers()
    {
        if (cellContainer == null)
        {
            GameObject cellContainerObject = new GameObject("Generated Cells");
            cellContainerObject.transform.SetParent(transform);
            cellContainerObject.transform.localPosition = Vector3.zero;
            cellContainer = cellContainerObject.transform;
        }

        if (towerContainer == null)
        {
            GameObject towerContainerObject = new GameObject("Placed Towers");
            towerContainerObject.transform.SetParent(transform);
            towerContainerObject.transform.localPosition = Vector3.zero;
            towerContainer = towerContainerObject.transform;
        }
    }

    public void HandleCellClicked(GridCell clickedCell)
    {
        if (clickedCell == null)
        {
            return;
        }

        SelectCell(clickedCell);

        if (!IsPlacing)
        {
            return;
        }

        bool placed = TryPlaceOrMergeTower(clickedCell);

        // Satu kali pasang lalu selesai (seperti drag kartu di GDD). Tahan Shift untuk pasang berulang.
        bool keepPlacing = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        if (placed && !keepPlacing)
        {
            CancelPlacement();
        }
    }

    private void SelectCell(GridCell newCell)
    {
        if (selectedCell != null)
        {
            selectedCell.DeselectCell();
        }

        selectedCell = newCell;
        selectedCell.SelectCell();
    }

    private bool TryPlaceOrMergeTower(GridCell targetCell)
    {
        if (towerPrefab == null || selectedTowerData == null || insightManager == null)
        {
            Debug.LogError("Pastikan Tower Prefab, Selected Tower Data, dan Insight Manager sudah dipasang di GridManager.");
            return false;
        }

        // Jika tile sudah ada tower yang sama & masih bisa naik tier -> Merge
        if (targetCell.CanMergeWith(selectedTowerData))
        {
            if (!insightManager.TrySpendInsight(selectedTowerData.cost))
            {
                Debug.LogWarning(
                    $"Insight tidak cukup untuk merge {selectedTowerData.towerName}. " +
                    $"Butuh {selectedTowerData.cost}, tersedia {insightManager.CurrentInsight}."
                );
                return false;
            }

            targetCell.OccupyingTower.TryMergeUpgrade();
            return true;
        }

        // Jika bukan merge, cek apakah tile valid untuk deploy baru
        if (!targetCell.CanPlaceTower(selectedTowerData))
        {
            string reason = MergeRejectReason(targetCell.OccupyingTower, selectedTowerData);
            if (reason == null && !targetCell.IsOccupied && targetCell.Type == TileType.BarePath) reason = "Only Red and Orange towers can be placed on the path";
            if (reason != null) OnActionRejected?.Invoke(reason);

            Debug.LogWarning(
                $"Tidak bisa pasang {selectedTowerData.towerName} di Cell ({targetCell.Row}, {targetCell.Column}) " +
                $"[{targetCell.Type}] (Occupied: {targetCell.IsOccupied})."
            );
            return false;
        }

        if (!insightManager.TrySpendInsight(selectedTowerData.cost))
        {
            Debug.LogWarning(
                $"Insight tidak cukup untuk pasang {selectedTowerData.towerName}. " +
                $"Butuh {selectedTowerData.cost}, tersedia {insightManager.CurrentInsight}."
            );
            return false;
        }

        Vector3 towerPosition = targetCell.transform.position + Vector3.up * towerHeightOffset;

        Tower newTower = Instantiate(
            towerPrefab,
            towerPosition,
            Quaternion.identity,
            towerContainer
        );

        newTower.Setup(targetCell, selectedTowerData, cellSize);
        targetCell.PlaceTower(newTower);
        return true;
    }

    public GridCell GetCell(int row, int column)
    {
        if (cells == null)
        {
            return null;
        }

        bool validRow = row >= 0 && row < rows;
        bool validColumn = column >= 0 && column < columns;

        if (!validRow || !validColumn)
        {
            return null;
        }

        return cells[row, column];
    }

    public bool TryGetSpawnPoint(int spawnPoint, out Vector2Int coord)
    {
        return spawnPoints.TryGetValue(spawnPoint, out coord);
    }

    /// <summary>
    /// Jalur terpendek (row, column) dari titik start melewati Bare Path sampai tile Wall terdekat.
    /// Elemen terakhir adalah tile Wall. Return null kalau tidak ada jalur.
    /// </summary>
    public List<Vector2Int> FindPathToWall(Vector2Int start)
    {
        if (GetCell(start.x, start.y) == null)
        {
            return null;
        }

        Vector2Int[] directions =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            GridCell currentCell = GetCell(current.x, current.y);

            if (currentCell.Type == TileType.Wall)
            {
                List<Vector2Int> path = new List<Vector2Int>();
                Vector2Int step = current;
                while (step != start)
                {
                    path.Add(step);
                    step = cameFrom[step];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (cameFrom.ContainsKey(next)) continue;

                GridCell nextCell = GetCell(next.x, next.y);
                if (nextCell == null) continue;
                if (nextCell.Type != TileType.BarePath && nextCell.Type != TileType.Wall) continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        Debug.LogError($"Tidak ada jalur Bare Path dari ({start.x}, {start.y}) ke Wall. Cek layout stage.");
        return null;
    }

    public IEnumerable<GridCell> AllCells()
    {
        if (cells == null)
        {
            yield break;
        }

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (cells[row, column] != null)
                {
                    yield return cells[row, column];
                }
            }
        }
    }

    /// <summary>Semua cell valid dalam pola area di sekitar (row, column).</summary>
    public List<GridCell> GetCellsInArea(int row, int column, AreaPattern pattern)
    {
        List<GridCell> result = new List<GridCell>();

        foreach (Vector2Int offset in GridArea.GetOffsets(pattern))
        {
            GridCell cell = GetCell(row + offset.x, column + offset.y);
            if (cell != null)
            {
                result.Add(cell);
            }
        }

        return result;
    }

    public Vector3 GetCellWorldPosition(int row, int column)
    {
        GridCell cell = GetCell(row, column);

        if (cell == null)
        {
            return Vector3.zero;
        }

        return cell.transform.position;
    }
}