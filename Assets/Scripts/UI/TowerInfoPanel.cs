using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel info tower yang diklik (GDD UI/UX "Tapped tower: give the tower range and info besides the tower").
/// Muncul di samping tower; area jangkauannya ditampilkan PlacementPreview.
/// </summary>
public class TowerInfoPanel : MonoBehaviour
{
    private const float Width = 400f;

    private GridManager grid;
    private RectTransform panel;
    private RectTransform canvasRect;
    private Image icon;
    private TextMeshProUGUI title;
    private TextMeshProUGUI body;
    private Camera cam;

    private Tower shownTower;
    private TowerData shownData;
    private float refreshTimer;

    public void Build(GridManager gridManager, RectTransform canvasRoot)
    {
        grid = gridManager;
        canvasRect = canvasRoot;

        panel = (RectTransform)transform;
        UIKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Width, 240f));
        Image bg = panel.gameObject.AddComponent<Image>();
        bg.color = UIKit.PanelColor;

        icon = UIKit.Panel("Icon", panel, Color.white, false);
        UIKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(56f, 84f));
        icon.preserveAspect = true;

        title = UIKit.Label("Title", panel, "", 24f, TextAlignmentOptions.TopLeft, UIKit.TextColor, true);
        UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(82f, -14f), new Vector2(Width - 130f, 70f));
        title.textWrappingMode = TextWrappingModes.Normal;

        Button close = UIKit.Button("Close", panel, "X", 20f, () => grid.ClearSelection());
        UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(36f, 36f));

        body = UIKit.Label("Body", panel, "", 19f, TextAlignmentOptions.TopLeft, UIKit.TextColor);
        UIKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -106f), new Vector2(Width - 32f, 200f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.richText = true;

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

        if (tower == null || cam == null)
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            shownTower = null;
            return;
        }

        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);

        // Isi teks diperbarui kalau tower/tier berganti, atau tiap 0.25 detik (HP berubah)
        refreshTimer -= Time.unscaledDeltaTime;
        if (tower != shownTower || tower.Data != shownData || refreshTimer <= 0f)
        {
            shownTower = tower;
            shownData = tower.Data;
            refreshTimer = 0.25f;
            Fill(tower);
        }

        Position(tower);
    }

    private void Fill(Tower tower)
    {
        TowerData d = tower.Data;
        if (d == null) return;

        icon.sprite = d.towerSprite;
        icon.color = d.towerSprite != null ? Color.white : UIKit.ColorOfTower(d.color);

        string colorHex = UIKit.Hex(UIKit.ColorOfTower(d.color));
        title.text = $"{d.towerName}\n<size=70%><color={colorHex}>Tier {d.tier}</color>  <color={UIKit.Hex(UIKit.TextMuted)}>{RoleName(d.role)}</color></size>";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"HP  <b>{Mathf.CeilToInt(tower.CurrentHP)} / {Mathf.CeilToInt(tower.MaxHP)}</b>");
        if (tower.Damage > 0f) sb.AppendLine($"ATK <b>{tower.Damage:0}</b>");
        foreach (string line in Describe(d)) sb.AppendLine("• " + line);

        if (tower.IsImmune) sb.AppendLine($"<color={UIKit.Hex(UIKit.Accent)}>• Sedang kebal damage</color>");

        string good = UIKit.Hex(UIKit.Good);
        if (d.nextTier != null)
            sb.AppendLine($"<color={UIKit.Hex(UIKit.TextMuted)}>Merge 2 tower ini -> {d.nextTier.towerName}</color>");
        else
            sb.AppendLine($"<color={good}>Tier maksimal</color>");

        body.text = sb.ToString().TrimEnd();
        body.ForceMeshUpdate();

        float height = 106f + body.preferredHeight + 18f;
        panel.sizeDelta = new Vector2(Width, Mathf.Max(150f, height));
    }

    private void Position(Tower tower)
    {
        Vector3 screen = cam.WorldToScreenPoint(tower.transform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        // Default di kanan tower; kalau mepet kanan layar, pindah ke kiri
        Vector2 half = canvasRect.rect.size / 2f;
        float x = local.x + 70f;
        if (x + Width > half.x - 10f) x = local.x - 70f - Width;

        float h = panel.sizeDelta.y;
        float y = Mathf.Clamp(local.y + 40f, -half.y + h / 2f + 240f, half.y - h / 2f - 130f);

        panel.anchoredPosition = new Vector2(x, y);
    }

    public static string RoleName(TowerRole role)
    {
        switch (role)
        {
            case TowerRole.Barrier: return "Penghalang";
            case TowerRole.Trap: return "Perangkap";
            case TowerRole.InsightGenerator: return "Penghasil Insight";
            case TowerRole.Healer: return "Pemadam & Penyembuh";
            default: return "Penyerang";
        }
    }

    private static System.Collections.Generic.IEnumerable<string> Describe(TowerData d)
    {
        switch (d.role)
        {
            case TowerRole.Barrier:
                yield return "Menghalangi jalan, musuh berhenti & menyerangnya";
                if (d.mergeImmunityDuration > 0f) yield return $"Setelah merge: kebal {d.mergeImmunityDuration:0} detik";
                break;

            case TowerRole.Trap:
                yield return $"SEKALI PAKAI: meledak saat diinjak, {d.attackDamage:0} dmg area {AreaName(d.trapDamageArea)}";
                if (d.slowPercent > 0f) yield return $"Memperlambat musuh {d.slowPercent * 100f:0}% selama {d.slowDuration:0.#} dtk";
                break;

            case TowerRole.InsightGenerator:
                yield return $"+{d.insightPerTick} Insight tiap {d.actionCooldown:0.#} dtk";
                if (d.mergeInsightBonus > 0) yield return $"Saat merge: langsung +{d.mergeInsightBonus} Insight";
                break;

            case TowerRole.Healer:
                yield return $"Beam ke api mana saja: -{d.fireReducePerShot} level api tiap {d.actionCooldown:0.#} dtk";
                if (d.auraHealPerSecond > 0f) yield return $"Aura heal {d.auraHealPerSecond:0} HP/dtk area {AreaName(d.auraArea)}";
                break;

            case TowerRole.Attacker:
                yield return d.isAreaAttack
                    ? $"Serang SEMUA musuh di area 5x5 tiap {d.actionCooldown:0.#} dtk"
                    : $"Tembak 1 musuh di area 5x5 tiap {d.actionCooldown:0.#} dtk";
                if (d.bonusDamagePerAttack > 0f) yield return $"Bonus +{d.bonusDamagePerAttack:0} dmg per serangan";
                if (d.GivesBuff) yield return $"Buff tower sekitar ({AreaName(d.buffArea)}): +{d.buffAttack:0} ATK, +{d.buffHP:0} HP";
                break;
        }
    }

    private static string AreaName(AreaPattern pattern)
    {
        switch (pattern)
        {
            case AreaPattern.Single: return "1 tile";
            case AreaPattern.Plus5: return "plus (5 tile)";
            case AreaPattern.Square9: return "3x3";
            case AreaPattern.Diamond13: return "13 tile";
            case AreaPattern.Square25: return "5x5";
            default: return "-";
        }
    }
}
