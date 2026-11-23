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
    [SerializeField] private Material hoverMaterial;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private bool isHovered;
    private bool isSelected;
    private bool hasFireTint;
    private Color fireTint = Color.white;

    public int Row => row;
    public int Column => column;
    public TileType Type => tileType;
    public bool IsOccupied => isOccupied;
    public int FireLevel => fireLevel;
    public bool IsOnFire => fireLevel > 0;
    public Tower OccupyingTower => occupyingTower;

    // Sesuai GDD: api hanya bisa hidup & menyebar di Grass Field
    public bool IsFlammable => tileType == TileType.Grass;

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
        Material newSelectedMaterial,
        Material newHoverMaterial = null)
    {
        row = newRow;
        column = newColumn;
        tileType = newTileType;

        normalMaterial = newNormalMaterial;
        selectedMaterial = newSelectedMaterial;
        hoverMaterial = newHoverMaterial != null ? newHoverMaterial : newSelectedMaterial;

        isOccupied = false;
        fireLevel = 0;
        occupyingTower = null;
        isHovered = false;
        isSelected = false;

        gameObject.name = $"Cell_{row}_{column}_{tileType}";

        UpdateVisual();
    }

    public bool CanPlaceTower(TowerData towerData)
    {
        if (towerData == null || isOccupied)
        {
            return false;
        }

        // Sesuai GDD: Tower tetap bisa dipasang di Grass meskipun tile sedang terbakar
        if (tileType == TileType.Grass && towerData.canPlaceOnGrass)
        {
            return true;
        }

        // Sesuai GDD: Hanya Red Tower & Orange Tower yang boleh dipasang di Bare Path
        if (tileType == TileType.BarePath && towerData.canPlaceOnBarePath)
        {
            return true;
        }

        // Forest dan Wall tidak bisa dipasangi tower
        return false;
    }

    public bool CanMergeWith(TowerData incomingData)
    {
        if (!isOccupied || occupyingTower == null || incomingData == null)
        {
            return false;
        }

        return occupyingTower.CanMergeWith(incomingData);
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

    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
        {
            return;
        }

        isHovered = hovered;
        UpdateVisual();
    }

    public void SelectCell()
    {
        isSelected = true;
        UpdateVisual();
    }

    public void DeselectCell()
    {
        isSelected = false;
        UpdateVisual();
    }

    /// <summary>
    /// Dipanggil oleh FireManager. Jangan panggil langsung dari sistem lain,
    /// gunakan FireManager.Ignite / FireManager.ReduceFire supaya timer api ikut ter-update.
    /// </summary>
    public void SetFireLevel(int newFireLevel)
    {
        // Sesuai GDD: Api tidak membakar Bare Path, Surrounding Forest, maupun Wall
        if (!IsFlammable)
        {
            fireLevel = 0;
            return;
        }

        fireLevel = Mathf.Clamp(newFireLevel, 0, 3);
    }

    /// <summary>Warna placeholder tile terbakar (dipakai jika model api belum dipasang).</summary>
    public void SetFireTint(bool enabled, Color color)
    {
        hasFireTint = enabled;
        fireTint = color;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer == null)
        {
            return;
        }

        bool showFireTint = false;

        // Urutan prioritas: hover > api > selected > normal
        if (isHovered && hoverMaterial != null)
        {
            meshRenderer.sharedMaterial = hoverMaterial;
        }
        else if (IsOnFire && hasFireTint && normalMaterial != null)
        {
            meshRenderer.sharedMaterial = normalMaterial;
            showFireTint = true;
        }
        else if (isSelected && selectedMaterial != null)
        {
            meshRenderer.sharedMaterial = selectedMaterial;
        }
        else if (normalMaterial != null)
        {
            meshRenderer.sharedMaterial = normalMaterial;
        }

        if (showFireTint)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", fireTint); // URP Lit / Unlit
            propBlock.SetColor("_Color", fireTint);     // Fallback shader standar
            meshRenderer.SetPropertyBlock(propBlock);
        }
        else
        {
            meshRenderer.SetPropertyBlock(null);
        }
    }
}