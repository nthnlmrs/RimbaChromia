using System.Collections.Generic;
using UnityEngine;

public enum PlacementResult { Invalid, Place, Merge }

/// <summary>
/// Preview saat menaruh tower (GDD UI/UX "Deploy tower": layar gelap kecuali tower & range-nya, grid terlihat).
/// - Bayangan (ghost) tower transparan di tile yang di-hover.
/// - Tile dalam jangkauan diberi warna tower, tile lain digelapkan.
/// - Tile tujuan: hijau = bisa pasang, kuning = merge, merah = tidak bisa / insight kurang.
/// - Saat TIDAK sedang menaruh tower, hover tower yang sudah terpasang menampilkan jangkauannya.
/// Otomatis ditambahkan oleh GridManager.
/// </summary>
public class PlacementPreview : MonoBehaviour
{
    [Header("Warna Overlay")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField, Range(0f, 1f)] private float rangeAlpha = 0.35f;
    [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.4f, 0.6f);
    [SerializeField] private Color mergeColor = new Color(1f, 0.9f, 0.2f, 0.65f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("Ghost Tower")]
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.45f;

    [Header("Posisi")]
    [Tooltip("Jarak overlay di atas permukaan tile (model 3D / kotak placeholder)")]
    [SerializeField] private float overlayHeight = 0.025f;

    private enum OverlayKind { Hidden, Dim, Range, Valid, Merge, Invalid }

    private GridManager grid;
    private Transform overlayRoot;
    private readonly Dictionary<GridCell, MeshRenderer> overlays = new Dictionary<GridCell, MeshRenderer>();
    private readonly Dictionary<GridCell, OverlayKind> overlayState = new Dictionary<GridCell, OverlayKind>();
    private readonly HashSet<GridCell> rangeBuffer = new HashSet<GridCell>();

    private Material dimMat, rangeMat, validMat, mergeMat, invalidMat, ghostMat;
    private GameObject ghost;
    private SpriteBillboard ghostSprite;
    private Mesh quadMesh;

    // Cache supaya overlay hanya dihitung ulang kalau ada yang berubah
    private string lastKey;

    private void Awake()
    {
        grid = GetComponent<GridManager>();
    }

    private void Start()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("PlacementPreview: shader Sprites/Default tidak ditemukan, preview dimatikan.");
            enabled = false;
            return;
        }

        dimMat = NewMaterial(shader, dimColor);
        rangeMat = NewMaterial(shader, Color.white);
        validMat = NewMaterial(shader, validColor);
        mergeMat = NewMaterial(shader, mergeColor);
        invalidMat = NewMaterial(shader, invalidColor);
        ghostMat = NewMaterial(shader, Color.white);

        overlayRoot = new GameObject("Placement Preview").transform;
        overlayRoot.SetParent(transform, false);

        quadMesh = BuildQuadMesh();
    }

    private void LateUpdate()
    {
        if (grid == null || !grid.IsGridReady) return;

        if (overlays.Count == 0)
        {
            BuildOverlays();
        }

        GridCell hovered = grid.HoveredCell;

        if (grid.DragSource != null)
        {
            ShowDragMergePreview(grid.DragSource, hovered);
        }
        else if (grid.IsPlacing)
        {
            ShowPlacementPreview(grid.SelectedTowerData, hovered);
        }
        else if (hovered != null && hovered.IsOccupied && hovered.OccupyingTower != null)
        {
            ShowPlacedTowerRange(hovered);
        }
        else if (grid.SelectedCell != null && grid.SelectedCell.OccupyingTower != null && Time.timeScale > 0f)
        {
            // Tower yang diklik (panel info terbuka) tetap menampilkan jangkauannya
            ShowPlacedTowerRange(grid.SelectedCell);
        }
        else
        {
            HideAll();
        }
    }

    // ------------------------------------------------------------------
    // Mode
    // ------------------------------------------------------------------

    private void ShowPlacementPreview(TowerData data, GridCell cell)
    {
        if (cell == null)
        {
            // Kursor di luar grid: tetap gelap supaya pemain tahu sedang mode menaruh tower
            string key = $"place-none-{data.GetInstanceID()}";
            if (key == lastKey) return;
            lastKey = key;

            foreach (GridCell c in overlays.Keys) SetOverlay(c, OverlayKind.Dim);
            SetGhostVisible(false);
            return;
        }

        PlacementResult result = grid.EvaluatePlacement(cell, data);
        TowerData previewData = data;
        if (result == PlacementResult.Merge && cell.OccupyingTower.Data.nextTier != null)
        {
            previewData = cell.OccupyingTower.Data.nextTier; // tampilkan jangkauan tier setelah merge
        }

        bool affordable = grid.CanAfford(data);
        string newKey = $"place-{cell.GetInstanceID()}-{data.GetInstanceID()}-{result}-{affordable}";
        if (newKey == lastKey) return;
        lastKey = newKey;

        CollectRange(cell, previewData);
        bool global = previewData.isGlobalRange;
        rangeMat.color = WithAlpha(GetTowerColor(previewData.color), rangeAlpha);

        foreach (GridCell c in overlays.Keys)
        {
            if (rangeBuffer.Contains(c)) SetOverlay(c, OverlayKind.Range);
            else if (global) SetOverlay(c, OverlayKind.Hidden); // jangkauan global: tidak ada yang digelapkan
            else SetOverlay(c, OverlayKind.Dim);
        }

        bool ok = result != PlacementResult.Invalid && affordable;
        OverlayKind target = !ok ? OverlayKind.Invalid
            : result == PlacementResult.Merge ? OverlayKind.Merge
            : OverlayKind.Valid;
        SetOverlay(cell, target);

        ShowGhost(cell, previewData, ok);
    }

    private void ShowDragMergePreview(Tower source, GridCell cell)
    {
        TowerData data = source.Data;
        GridCell sourceCell = source.OccupiedCell;
        if (data == null || sourceCell == null)
        {
            HideAll();
            return;
        }

        bool overSource = cell == null || cell == sourceCell;
        bool canMerge = !overSource && cell.OccupyingTower != null && cell.OccupyingTower.CanMergeWith(data);

        string newKey = $"drag-{source.GetInstanceID()}-{(cell != null ? cell.GetInstanceID() : 0)}-{canMerge}";
        if (newKey == lastKey) return;
        lastKey = newKey;

        // Area yang ditampilkan: hasil setelah merge (kalau valid), kalau tidak area tower sumber
        GridCell center = canMerge ? cell : sourceCell;
        TowerData shown = canMerge && data.nextTier != null ? data.nextTier : data;
        CollectRange(center, shown);
        rangeMat.color = WithAlpha(GetTowerColor(shown.color), rangeAlpha);

        foreach (GridCell c in overlays.Keys)
        {
            SetOverlay(c, rangeBuffer.Contains(c) ? OverlayKind.Range : OverlayKind.Dim);
        }

        // Tandai semua tower yang bisa jadi tujuan merge
        foreach (GridCell c in overlays.Keys)
        {
            if (c != sourceCell && c.OccupyingTower != null && c.OccupyingTower.CanMergeWith(data))
            {
                SetOverlay(c, OverlayKind.Merge);
            }
        }

        if (!overSource && !canMerge)
        {
            SetOverlay(cell, OverlayKind.Invalid);
        }

        SetGhostVisible(false);
    }

    private void ShowPlacedTowerRange(GridCell cell)
    {
        TowerData data = cell.OccupyingTower.Data;
        if (data == null)
        {
            HideAll();
            return;
        }

        string newKey = $"inspect-{cell.GetInstanceID()}-{data.GetInstanceID()}";
        if (newKey == lastKey) return;
        lastKey = newKey;

        CollectRange(cell, data);
        rangeMat.color = WithAlpha(GetTowerColor(data.color), rangeAlpha);

        foreach (GridCell c in overlays.Keys)
        {
            SetOverlay(c, rangeBuffer.Contains(c) ? OverlayKind.Range : OverlayKind.Hidden);
        }

        SetGhostVisible(false);
    }

    private void HideAll()
    {
        if (lastKey == "hidden") return;
        lastKey = "hidden";

        foreach (GridCell c in overlays.Keys) SetOverlay(c, OverlayKind.Hidden);
        SetGhostVisible(false);
    }

    private void CollectRange(GridCell center, TowerData data)
    {
        rangeBuffer.Clear();
        foreach (Vector2Int offset in data.GetRangeOffsets())
        {
            GridCell c = grid.GetCell(center.Row + offset.x, center.Column + offset.y);
            if (c != null) rangeBuffer.Add(c);
        }
    }

    // ------------------------------------------------------------------
    // Overlay tile
    // ------------------------------------------------------------------

    private void BuildOverlays()
    {
        float size = grid.CellSize * 0.95f;

        foreach (GridCell cell in grid.AllCells())
        {
            GameObject quad = new GameObject($"Overlay_{cell.Row}_{cell.Column}");
            quad.transform.SetParent(overlayRoot, false);
            Vector3 cellPos = cell.transform.position;
            quad.transform.position = new Vector3(cellPos.x, cell.SurfaceY + overlayHeight, cellPos.z);
            quad.transform.localScale = new Vector3(size, 1f, size);

            quad.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            MeshRenderer mr = quad.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            overlays[cell] = mr;
            overlayState[cell] = OverlayKind.Hidden;
        }
    }

    private void SetOverlay(GridCell cell, OverlayKind kind)
    {
        if (!overlays.TryGetValue(cell, out MeshRenderer mr)) return;
        if (overlayState[cell] == kind) return;
        overlayState[cell] = kind;

        if (kind == OverlayKind.Hidden)
        {
            mr.enabled = false;
            return;
        }

        mr.sharedMaterial = kind switch
        {
            OverlayKind.Dim => dimMat,
            OverlayKind.Range => rangeMat,
            OverlayKind.Valid => validMat,
            OverlayKind.Merge => mergeMat,
            _ => invalidMat
        };
        mr.enabled = true;
    }

    // ------------------------------------------------------------------
    // Ghost tower
    // ------------------------------------------------------------------

    private void ShowGhost(GridCell cell, TowerData data, bool ok)
    {
        if (ghost == null) BuildGhost();

        // Bayangan tower = sprite 2D tower itu sendiri, transparan (merah kalau tidak bisa dipasang)
        Sprite sprite = data.towerSprite != null
            ? data.towerSprite
            : SpriteBillboard.Placeholder(GetTowerColor(data.color), 40, 60);
        ghostSprite.SetSprite(sprite);
        ghostSprite.SetColor(ok
            ? new Color(1f, 1f, 1f, ghostAlpha + 0.15f)
            : new Color(1f, 0.35f, 0.35f, ghostAlpha + 0.15f));

        Vector3 cellPos = cell.transform.position;
        ghost.transform.position = new Vector3(cellPos.x, cell.SurfaceY, cellPos.z);

        SetGhostVisible(true);
    }

    private void BuildGhost()
    {
        ghost = new GameObject("Tower Ghost");
        ghost.transform.SetParent(overlayRoot, false);
        ghostSprite = SpriteBillboard.Create(ghost.transform, null, false, SpriteBillboard.TowerGroundOffset);
        ghost.SetActive(false);
    }

    private void SetGhostVisible(bool visible)
    {
        if (ghost != null && ghost.activeSelf != visible)
        {
            ghost.SetActive(visible);
        }
    }

    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    private static Material NewMaterial(Shader shader, Color color)
    {
        Material mat = new Material(shader) { color = color };
        mat.renderQueue = TowerVfx.GroundDecalQueue - 10; // dekal tanah: sebelum sprite karakter (3000)
        return mat;
    }

    private static Mesh BuildQuadMesh()
    {
        // Quad datar menghadap ke atas (bidang XZ), ukuran 1x1, tanpa collider
        Mesh mesh = new Mesh { name = "PreviewQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),   new Vector3(0.5f, 0f, -0.5f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    public static Color GetTowerColor(TowerColor towerColor)
    {
        switch (towerColor)
        {
            case TowerColor.Red:    return new Color(0.95f, 0.25f, 0.25f);
            case TowerColor.Orange: return new Color(1.00f, 0.55f, 0.10f);
            case TowerColor.Yellow: return new Color(1.00f, 0.90f, 0.20f);
            case TowerColor.Green:  return new Color(0.30f, 0.95f, 0.45f);
            case TowerColor.Blue:   return new Color(0.25f, 0.60f, 1.00f);
            case TowerColor.Purple: return new Color(0.70f, 0.35f, 0.95f);
            default:                return Color.white;
        }
    }

    private void OnDestroy()
    {
        foreach (Object created in new Object[] { dimMat, rangeMat, validMat, mergeMat, invalidMat, ghostMat, quadMesh })
        {
            if (created != null) Destroy(created);
        }
    }
}
