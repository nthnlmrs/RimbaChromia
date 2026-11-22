using UnityEngine;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
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

    private readonly string[] mapLayout =
    {
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGGGGGGG",
        "PPPPPPPPPPPPPPPPPPPPW",
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

        if (Mouse.current.leftButton.wasPressedThisFrame && cellUnderCursor != null)
        {
            HandleCellClicked(cellUnderCursor);
        }
    }

    public void SetSelectedTowerData(TowerData newTowerData)
    {
        selectedTowerData = newTowerData;
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

        selectedTowerData = towerHotbar[index];
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

    private GridCell GetCellUnderCursor()
    {
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

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            raycastDistance,
            gridLayerMask
        );

        if (!hasHit)
        {
            return null;
        }

        return hit.collider.GetComponentInParent<GridCell>();
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

        cells = new GridCell[rows, columns];

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
                TileType tileType = GetTileType(mapLayout[row][column]);
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

                cells[row, column] = newCell;
            }
        }

        Debug.Log($"Grid berhasil dibuat: {rows} x {columns} = {rows * columns} cell.");
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
        TryPlaceOrMergeTower(clickedCell);
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

    private void TryPlaceOrMergeTower(GridCell targetCell)
    {
        if (towerPrefab == null || selectedTowerData == null || insightManager == null)
        {
            Debug.LogError("Pastikan Tower Prefab, Selected Tower Data, dan Insight Manager sudah dipasang di GridManager.");
            return;
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
                return;
            }

            targetCell.OccupyingTower.TryMergeUpgrade();
            return;
        }

        // Jika bukan merge, cek apakah tile valid untuk deploy baru
        if (!targetCell.CanPlaceTower(selectedTowerData))
        {
            Debug.LogWarning(
                $"Tidak bisa pasang {selectedTowerData.towerName} di Cell ({targetCell.Row}, {targetCell.Column}) " +
                $"[{targetCell.Type}] (Occupied: {targetCell.IsOccupied})."
            );
            return;
        }

        if (!insightManager.TrySpendInsight(selectedTowerData.cost))
        {
            Debug.LogWarning(
                $"Insight tidak cukup untuk pasang {selectedTowerData.towerName}. " +
                $"Butuh {selectedTowerData.cost}, tersedia {insightManager.CurrentInsight}."
            );
            return;
        }

        Vector3 towerPosition = targetCell.transform.position + Vector3.up * towerHeightOffset;

        Tower newTower = Instantiate(
            towerPrefab,
            towerPosition,
            Quaternion.identity,
            towerContainer
        );

        newTower.Setup(targetCell, selectedTowerData);
        targetCell.PlaceTower(newTower);
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