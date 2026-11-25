using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panduan stage Tutorial (GDD Progression: "Tutorial stage, player can deploy and merge tower regardless of their cost").
/// Taduh memberi petunjuk langkah demi langkah; tiap langkah selesai otomatis saat pemain melakukannya.
/// Hitung mundur wave ditahan sampai panduan sampai langkah terakhir.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    private enum Step
    {
        PlaceBarrier,
        PlaceAttacker,
        Merge,
        Extinguish,
        StartWave,
        FirstWave,
        BreakTip,
        Done
    }

    private static readonly string[] Texts =
    {
        "Enemies come from the left toward The Wall. Drag the <b>Red Pillar [1]</b> card onto the <b>PATH</b> to block them.",
        "Nice! Now drag a <b>Blue Sprinkler [5]</b> onto the <b>GRASS</b> next to the path. It shoots enemies passing by.",
        "Two <b>identical</b> towers can merge into a stronger one. Place a second Blue, then <b>drag one Blue onto the other</b>.",
        "Look, the grass is on fire! Fire spreads and hurts towers. Place a <b>Green Sprout [4]</b> to put it out from anywhere.",
        "You're ready! Press <b>START NOW</b> or wait for the countdown. Protect The Wall!",
        "",
        "The next wave brings a <b>Walking Furnace</b> that throws burning rocks. Add more attackers and keep your Green alive!",
        ""
    };

    private const int GuidedSteps = 5;

    private WaveManager waves;
    private GridManager grid;
    private GameObject panel;
    private TextMeshProUGUI body;
    private TextMeshProUGUI progress;
    private Step step = Step.PlaceBarrier;
    private bool demoFireLit;

    public void Build(WaveManager waveManager, GridManager gridManager)
    {
        waves = waveManager;
        grid = gridManager;

        RectTransform root = (RectTransform)transform;
        UIKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1180f, 156f));
        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = UIKit.PanelColor;
        bg.raycastTarget = false;
        panel = root.gameObject;

        Image accent = UIKit.Panel("Accent", root, UIKit.Good, false);
        UIKit.Place(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 156f));

        TextMeshProUGUI name = UIKit.Label("Speaker", root, "Taduh", 32f, TextAlignmentOptions.TopLeft, UIKit.Good, true);
        UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(300f, 38f));

        progress = UIKit.Label("Progress", root, "", 24f, TextAlignmentOptions.TopRight, UIKit.TextMuted);
        UIKit.Place(progress.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(260f, 32f));

        body = UIKit.Label("Text", root, "", 30f, TextAlignmentOptions.TopLeft, UIKit.TextColor);
        UIKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -54f), new Vector2(1124f, 96f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.richText = true;

        // Hitung mundur ditahan selama pemain belajar
        if (waves != null) waves.HoldCountdown = true;
        Show(step);
    }

    private void Update()
    {
        if (waves == null) return;

        switch (step)
        {
            case Step.PlaceBarrier:
                if (HasTower(t => t.role == TowerRole.Barrier)) Next();
                break;

            case Step.PlaceAttacker:
                if (HasTower(t => t.role == TowerRole.Attacker)) Next();
                break;

            case Step.Merge:
                if (HasTower(t => t.tier >= 2)) Next();
                break;

            case Step.Extinguish:
                if (!demoFireLit) LightDemoFire();
                bool fireOut = FireManager.Instance == null || FireManager.Instance.BurningCount == 0;
                if (HasTower(t => t.role == TowerRole.Healer) && fireOut) Next();
                break;

            case Step.StartWave:
                if (waves.State == StageState.WaveRunning) Next();
                break;

            case Step.FirstWave:
                if (waves.State == StageState.Break) Next();
                break;

            case Step.BreakTip:
                if (waves.State == StageState.WaveRunning) Next();
                break;
        }

        // Sembunyikan saat stage berakhir
        if (waves.State == StageState.Victory || waves.State == StageState.Defeat) panel.SetActive(false);
    }

    private void Next()
    {
        step++;
        if (step == Step.StartWave && waves != null) waves.HoldCountdown = false; // hitung mundur mulai berjalan
        Show(step);
    }

    private void Show(Step s)
    {
        string text = Texts[(int)s];
        panel.SetActive(!string.IsNullOrEmpty(text));
        body.text = text;
        progress.text = (int)s < GuidedSteps ? $"Step {(int)s + 1} / {GuidedSteps}" : "Tip";
    }

    private static bool HasTower(System.Func<TowerData, bool> match)
    {
        foreach (Tower t in Tower.ActiveTowers)
        {
            if (t != null && t.Data != null && match(t.Data)) return true;
        }
        return false;
    }

    /// <summary>Nyalakan api contoh di rumput dekat jalur (tengah map) supaya pemain bisa mencoba Green.</summary>
    private void LightDemoFire()
    {
        demoFireLit = true;
        if (grid == null || FireManager.Instance == null) return;

        GridCell best = null;
        float bestDistance = float.MaxValue;
        Vector3 center = grid.transform.position;

        foreach (GridCell cell in grid.AllCells())
        {
            if (!cell.IsFlammable || cell.IsOccupied) continue;

            bool nearPath = false;
            foreach (GridCell n in grid.GetCellsInArea(cell.Row, cell.Column, AreaPattern.Plus5))
            {
                if (n.Type == TileType.BarePath) { nearPath = true; break; }
            }
            if (!nearPath) continue;

            float d = (cell.transform.position - center).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cell;
            }
        }

        if (best != null) FireManager.Instance.Ignite(best, 2);
    }
}
