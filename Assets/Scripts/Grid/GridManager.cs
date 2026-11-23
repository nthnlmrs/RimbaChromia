using System;
using System.Collections.Generic;
using UnityEngine;
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
        HandleHotbarShortcuts();

        if (Mouse.current == null)
        {
            return;
        }

        GridCell cellUnderCursor = GetCellUnderCursor();
        UpdateHover(cellUnderCursor);

        // Batal menaruh tower: klik kanan / Esc
        bool cancel = Mouse.current.rightButton.wasPressedThisFrame ||
                      (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        if (cancel && isPlacing)
        {
            CancelPlacement();
        }

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

    public GridCell GetCellUnderCursor()
    {
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

        foreach (RaycastHit hit in hits)
        {
            GridCell cell = hit.collider.GetComponentInParent<GridCell>();
            if (cell != null)
            {
                return cell;
            }

            // Kursor di badan tower -> anggap memilih tile tempat tower berdiri (penting untuk merge)
            Tower tower = hit.collider.GetComponentInParent<Tower>();
            if (tower != null && tower.OccupiedCell != null)
            {
                return tower.OccupiedCell;
            }
        }

        return null;
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

                newCell.transform.localScale = new Vector3(
                    cellSize * 0.95f,
                    0.15f,
                    cellSize * 0.95f
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

        Debug.Log($"Grid berhasil dibuat: {rows} x {columns} = {rows * columns} cell.");
        OnGridGenerated?.Invoke();
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