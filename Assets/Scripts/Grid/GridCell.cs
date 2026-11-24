using UnityEngine;

public enum TileType
{
    Grass,
    BarePath,
    Forest,
    Wall
}

/// <summary>
/// Satu tile arena. Visualnya:
///  - Model 3D dari ArenaArt kalau ada (kotak placeholder disembunyikan, collider-nya tetap dipakai untuk klik),
///    kalau tidak ada pakai kotak placeholder berwarna.
///  - Overlay transparan di atas permukaan tile untuk hover / selected / warna api placeholder,
///    supaya tetap terlihat di atas model 3D apa pun.
/// </summary>
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

    private static readonly Color HoverColor = new Color(1f, 1f, 0.75f, 0.35f);
    private static readonly Color SelectedColor = new Color(1f, 0.82f, 0.3f, 0.5f);

    private MeshRenderer meshRenderer;
    private bool isHovered;
    private bool isSelected;
    private bool hasFireTint;
    private Color fireTint = Color.white;

    private GameObject artModel;
    private float surfaceY;
    private MeshRenderer overlay;

    public int Row => row;
    public int Column => column;
    public TileType Type => tileType;
    public bool IsOccupied => isOccupied;
    public int FireLevel => fireLevel;
    public bool IsOnFire => fireLevel > 0;
    public Tower OccupyingTower => occupyingTower;
    public bool HasArtModel => artModel != null;

    /// <summary>Tinggi permukaan atas tile (dunia): tempat tower / musuh berdiri dan overlay digambar.</summary>
    public float SurfaceY => surfaceY;

    // Sesuai GDD: api hanya bisa hidup & menyebar di Grass Field
    public bool IsFlammable => tileType == TileType.Grass;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError($"GridCell pada {gameObject.name} tidak memiliki MeshRenderer.");
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

        // Permukaan kotak placeholder (tinggi kotak = scale Y, pusat di posisi cell)
        surfaceY = transform.position.y + transform.lossyScale.y * 0.5f;

        gameObject.name = $"Cell_{row}_{column}_{tileType}";

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && normalMaterial != null) meshRenderer.sharedMaterial = normalMaterial;

        UpdateVisual();
    }

    /// <summary>Pasang model 3D tile (dari ArenaArt). Kotak placeholder disembunyikan.</summary>
    public void SetArtModel(GameObject model, float topY)
    {
        if (artModel != null) Destroy(artModel);

        artModel = model;
        decorations.Clear();
        decorationScales.Clear();

        if (artModel != null)
        {
            surfaceY = topY;
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (overlay != null) PositionOverlay();

            // Dekorasi (helai rumput) = semua renderer selain balok tile -> bisa "penyet" saat ditindih tower
            Renderer[] renderers = artModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 1)
            {
                Renderer baseRenderer = ArenaArt.FindBaseRenderer(renderers);
                foreach (Renderer r in renderers)
                {
                    if (r == baseRenderer || r.transform == baseRenderer.transform) continue;
                    decorations.Add(r.transform);
                    decorationScales.Add(r.transform.localScale);
                }
            }

            if (isOccupied) SetSquashed(true, instant: true);
        }
    }

    // ------------------------------------------------------------------
    // Rumput "penyet" saat tower berdiri di atasnya
    // ------------------------------------------------------------------

    private readonly System.Collections.Generic.List<Transform> decorations = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Vector3> decorationScales = new System.Collections.Generic.List<Vector3>();
    private Coroutine squashRoutine;

    private const float SquashHeight = 0.3f;  // tinggi helai saat ditindih (30%)
    private const float SquashSpread = 1.2f;  // sedikit melebar ke samping
    private const float SquashDuration = 0.18f;

    private void SetSquashed(bool squashed, bool instant = false)
    {
        if (decorations.Count == 0) return;
        if (squashRoutine != null) StopCoroutine(squashRoutine);

        if (instant || !isActiveAndEnabled)
        {
            ApplySquash(squashed ? 1f : 0f);
            return;
        }

        squashRoutine = StartCoroutine(SquashRoutine(squashed));
    }

    private System.Collections.IEnumerator SquashRoutine(bool squashed)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / SquashDuration;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            // Saat ditindih sedikit "memantul" (overshoot), saat dilepas kembali tegak pelan
            float k = squashed ? eased + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.15f : 1f - eased;
            ApplySquash(k);
            yield return null;
        }
        ApplySquash(squashed ? 1f : 0f);
        squashRoutine = null;
    }

    /// <summary>k = 0 tegak normal, k = 1 penyet penuh.</summary>
    private void ApplySquash(float k)
    {
        for (int i = 0; i < decorations.Count; i++)
        {
            if (decorations[i] == null) continue;
            Vector3 s = decorationScales[i];
            // Kartu rumput: sumbu Y lokal = tinggi helai (pivot di pangkal), jadi Y diperkecil, X dilebarkan
            decorations[i].localScale = new Vector3(
                s.x * Mathf.Lerp(1f, SquashSpread, k),
                s.y * Mathf.Lerp(1f, SquashHeight, k),
                s.z);
        }
    }

    /// <summary>Sembunyikan / tampilkan visual dasar tile (dipakai saat model api menggantikan rumput).</summary>
    public void SetBaseVisible(bool visible)
    {
        if (artModel != null)
        {
            artModel.SetActive(visible);
        }
        else if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
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
            Debug.LogError($"Tower untuk Cell_{row}_{column} bernilai null.");
            return;
        }

        occupyingTower = newTower;
        isOccupied = true;
        SetSquashed(true); // rumput tertindih tower
    }

    public void RemoveTower()
    {
        occupyingTower = null;
        isOccupied = false;
        SetSquashed(false); // rumput kembali tegak
    }

    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered) return;
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
        // Prioritas: hover > api (placeholder) > selected > tidak ada overlay
        Color? color = null;
        if (isHovered) color = HoverColor;
        else if (IsOnFire && hasFireTint) color = new Color(fireTint.r, fireTint.g, fireTint.b, 0.7f);
        else if (isSelected) color = SelectedColor;

        if (color == null)
        {
            if (overlay != null) overlay.enabled = false;
            return;
        }

        EnsureOverlay();
        TowerVfx.SetColor(overlay, color.Value);
        overlay.enabled = true;
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;

        // Bukan child cell: cell punya scale gepeng (0.95, 0.15, 0.95) yang akan ikut ke overlay
        GameObject go = new GameObject($"Overlay_{row}_{column}");
        go.transform.SetParent(transform.parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = TowerVfx.FlatQuad;
        overlay = go.AddComponent<MeshRenderer>();
        overlay.sharedMaterial = TowerVfx.GroundMaterial; // dekal tanah: dirender sebelum sprite karakter
        overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        PositionOverlay();
    }

    private void PositionOverlay()
    {
        float size = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        overlay.transform.position = new Vector3(transform.position.x, surfaceY + 0.015f, transform.position.z);
        overlay.transform.localScale = new Vector3(size, 1f, size);
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
