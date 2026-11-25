using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel info tower yang diklik (GDD UI/UX "Tapped tower: give the tower range and info besides the tower").
/// Memakai art panel per warna & tier dari artist (UISkin.towerInfo). Teks deskripsi sudah ada di art;
/// kode hanya mengisi angka ATK & HP di sebelah labelnya. Area jangkauan ditampilkan PlacementPreview.
/// </summary>
public class TowerInfoPanel : MonoBehaviour
{
    private const float ValueFontSize = 20f;

    private GridManager grid;
    private RectTransform panel;
    private RectTransform canvasRect;
    private Image art;
    private TextMeshProUGUI atkText;
    private TextMeshProUGUI hpText;
    private Camera cam;

    private Tower shownTower;
    private TowerData shownData;
    private float refreshTimer;

    public void Build(GridManager gridManager, RectTransform canvasRoot)
    {
        grid = gridManager;
        canvasRect = canvasRoot;

        panel = (RectTransform)transform;
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(354f, 314f));

        art = UIKit.ArtImage("Art", panel, null);
        UIKit.Stretch(art.rectTransform);

        atkText = UIKit.ArtLabel("ATK Value", panel, "", ValueFontSize, TextAlignmentOptions.Left, Color.white);
        hpText = UIKit.ArtLabel("HP Value", panel, "", ValueFontSize, TextAlignmentOptions.Left, Color.white);
        atkText.richText = true;
        hpText.richText = true;

        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;

        Tower tower = null;
        if (grid != null && !grid.IsPlacing && grid.DragSource == null && Time.timeScale > 0f)
        {
            GridCell cell = grid.SelectedCell;
            tower = cell != null ? cell.OccupyingTower : null;
        }

        TowerInfoArt info = tower != null && tower.Data != null && UISkin.Current != null
            ? UISkin.Current.InfoFor(tower.Data.color, tower.Data.tier)
            : null;

        if (tower == null || cam == null || info == null || info.sprite == null)
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            shownTower = null;
            return;
        }

        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);

        // Ganti art kalau tower / tier berganti, angka diperbarui tiap 0.25 detik
        if (tower != shownTower || tower.Data != shownData)
        {
            shownTower = tower;
            shownData = tower.Data;
            ApplyArt(info);
            refreshTimer = 0f;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.25f;
            FillValues(tower);
        }

        Position(tower);
    }

    private void ApplyArt(TowerInfoArt info)
    {
        art.sprite = info.sprite;
        panel.sizeDelta = info.sprite.rect.size * UIKit.ArtScale;

        // Nilai ditulis tepat setelah label "ATK:" / "HP:" di gambar
        PlaceValue(atkText.rectTransform, info.atkLabel);
        PlaceValue(hpText.rectTransform, info.hpLabel);
    }

    private static void PlaceValue(RectTransform rt, Vector2 label)
    {
        float s = UIKit.ArtScale;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2((label.x + 8f) * s, -label.y * s);
        rt.sizeDelta = new Vector2(220f * s, 28f * s);
    }

    private void FillValues(Tower tower)
    {
        TowerData d = tower.Data;
        string good = UIKit.Hex(UIKit.Good);
        string gold = UIKit.Hex(UIKit.Accent);

        // ATK (dengan bonus buff Purple). Tower tanpa serangan ditulis "-"
        if (tower.Damage > 0f)
        {
            float buff = tower.Damage - d.attackDamage - d.bonusDamagePerAttack;
            atkText.text = buff > 0.01f ? $"{tower.Damage:0} <color={good}>(+{buff:0})</color>" : $"{tower.Damage:0}";
        }
        else
        {
            atkText.text = "-";
        }

        string hp = $"{Mathf.CeilToInt(tower.CurrentHP)} / {Mathf.CeilToInt(tower.MaxHP)}";
        if (tower.IsImmune) hp += $"  <color={gold}>IMMUNE</color>";
        hpText.text = hp;
    }

    private void Position(Tower tower)
    {
        Vector3 screen = cam.WorldToScreenPoint(tower.transform.position + Vector3.up * 0.8f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        // Default di kanan tower; kalau mepet kanan layar, pindah ke kiri
        Vector2 half = canvasRect.rect.size / 2f;
        float width = panel.sizeDelta.x;
        float x = local.x + 80f;
        if (x + width > half.x - 10f) x = local.x - 80f - width;

        float h = panel.sizeDelta.y;
        float y = Mathf.Clamp(local.y + 40f, -half.y + h / 2f + 290f, half.y - h / 2f - 130f);

        panel.anchoredPosition = new Vector2(x, y);
    }
}
