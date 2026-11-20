using UnityEngine;

public enum TileType
{
    Grass,
    BarePath,
    Forest,
    Wall
}

public class GridCell : MonoBehaviour
{
    [Header("Grid Coordinate")]
    [SerializeField] private int row;
    [SerializeField] private int column;

    [Header("Cell State")]
    [SerializeField] private TileType tileType = TileType.Grass;
    [SerializeField] private bool isOccupied;
    [SerializeField] private int fireLevel;
    [SerializeField] private Tower occupyingTower;

    [Header("Visual")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material selectedMaterial;

    private MeshRenderer meshRenderer;

    public int Row => row;
    public int Column => column;
    public TileType Type => tileType;
    public bool IsOccupied => isOccupied;
    public int FireLevel => fireLevel;
    public Tower OccupyingTower => occupyingTower;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError(
                $"GridCell pada {gameObject.name} tidak memiliki MeshRenderer."
            );
        }
    }

    public void Setup(
        int newRow,
        int newColumn,
        TileType newTileType,
        Material newNormalMaterial,
        Material newSelectedMaterial)
    {
        row = newRow;
        column = newColumn;
        tileType = newTileType;

        normalMaterial = newNormalMaterial;
        selectedMaterial = newSelectedMaterial;

        isOccupied = false;
        fireLevel = 0;
        occupyingTower = null;

        gameObject.name = $"Cell_{row}_{column}_{tileType}";

        ApplyNormalMaterial();
    }

    public bool CanPlaceTower()
    {
        if (isOccupied)
        {
            return false;
        }

        if (tileType != TileType.Grass)
        {
            return false;
        }

        return true;
    }

    public void PlaceTower(Tower newTower)
    {
        if (newTower == null)
        {
            Debug.LogError(
                $"Tower untuk Cell_{row}_{column} bernilai null."
            );

            return;
        }

        occupyingTower = newTower;
        isOccupied = true;
    }

    public void RemoveTower()
    {
        occupyingTower = null;
        isOccupied = false;
    }

    public void SelectCell()
    {
        if (meshRenderer == null || selectedMaterial == null)
        {
            return;
        }

        meshRenderer.sharedMaterial = selectedMaterial;
    }

    public void DeselectCell()
    {
        ApplyNormalMaterial();
    }

    public void SetFireLevel(int newFireLevel)
    {
        fireLevel = Mathf.Clamp(newFireLevel, 0, 3);
    }

    private void ApplyNormalMaterial()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer != null && normalMaterial != null)
        {
            meshRenderer.sharedMaterial = normalMaterial;
        }
    }
}