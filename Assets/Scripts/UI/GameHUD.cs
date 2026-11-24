using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD utama gameplay (Prioritas 4), dibangun otomatis lewat kode oleh WaveManager:
///  - Atas kiri  : nama stage, wave, status
///  - Atas tengah: HP The Wall
///  - Atas kanan : Insight + tombol pause
///  - Tengah atas: hitung mundur persiapan/jeda + tombol "Mulai Sekarang", pengumuman wave
///  - Bawah      : kartu tower (TowerCardBar)
///  - Health bar tower & musuh (HealthBarLayer), panel info tower (TowerInfoPanel)
///  - Menu Jeda (Esc) dan panel Kemenangan / Kekalahan
/// </summary>
public class GameHUD : MonoBehaviour
{
    private WaveManager waves;
    private GridManager grid;
    private WallHealth wall;
    private InsightManager insight;

    private RectTransform root;

    private TextMeshProUGUI stageText;
    private TextMeshProUGUI waveText;
    private TextMeshProUGUI statusText;
    private Image wallFill;
    private TextMeshProUGUI wallText;
    private TextMeshProUGUI insightText;

    private GameObject countdownPanel;
    private TextMeshProUGUI countdownTitle;
    private TextMeshProUGUI countdownNumber;

    private TextMeshProUGUI announceText;
    private float announceTimer;

    private GameObject pauseOverlay;
    private GameObject resultOverlay;
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultSubtitle;
    private GameObject nextStageButton;

    private bool isPaused;
    private StageState shownResultFor = StageState.Loading;

    private void Start()
    {
        waves = WaveManager.Instance;
        grid = GridManager.Instance;
        wall = FindFirstObjectByType<WallHealth>();
        insight = InsightManager.Instance;

        UIKit.EnsureEventSystem();
        Build();

        if (waves != null)
        {
            waves.OnWaveStarted += HandleWaveStarted;
        }
    }

    private void OnDestroy()
    {
        if (waves != null)
        {
            waves.OnWaveStarted -= HandleWaveStarted;
        }
    }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    private void Build()
    {
        Canvas canvas = UIKit.CreateCanvas("Game HUD", 10);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;

        // Health bar paling belakang supaya tidak menutupi panel lain
        RectTransform bars = UIKit.Stretch(UIKit.Rect("Health Bars", root));
        bars.gameObject.AddComponent<HealthBarLayer>();

        BuildTopLeft();
        BuildWallBar();
        BuildInsight();
        BuildCountdown();

        announceText = UIKit.Label("Wave Announce", root, "", 96f, TextAlignmentOptions.Center, UIKit.Accent, true);
        UIKit.Place(announceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 140f));
        announceText.outlineWidth = 0.2f;
        announceText.outlineColor = new Color32(20, 30, 20, 255);
        announceText.gameObject.SetActive(false);

        if (grid != null)
        {
            RectTransform cardBar = UIKit.Rect("Tower Cards", root);
            cardBar.gameObject.AddComponent<TowerCardBar>().Build(grid);

            RectTransform info = UIKit.Rect("Tower Info", root);
            info.gameObject.AddComponent<TowerInfoPanel>().Build(grid, root);
        }

        TextMeshProUGUI controls = UIKit.Label("Controls Hint", root,
            "WASD geser kamera  |  Scroll zoom\nR reset kamera  |  Esc jeda", 17f,
            TextAlignmentOptions.BottomLeft, new Color(1f, 1f, 1f, 0.6f));
        UIKit.Place(controls.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 10f), new Vector2(420f, 48f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TextMeshProUGUI debugHint = UIKit.Label("Debug Hint", root,
            "Debug: F api  X padam  P jeda api  N skip", 16f, TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.45f));
        UIKit.Place(debugHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 10f), new Vector2(500f, 24f));
#endif

        BuildPauseOverlay();
        BuildResultOverlay();
    }

    private void BuildTopLeft()
    {
        Image panel = UIKit.Panel("Stage Info", root, UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(420f, 124f));

        stageText = UIKit.Label("Stage", panel.transform, "Stage", 30f, TextAlignmentOptions.TopLeft, UIKit.TextColor, true);
        UIKit.Place(stageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(380f, 38f));

        waveText = UIKit.Label("Wave", panel.transform, "", 24f, TextAlignmentOptions.TopLeft, UIKit.Accent, true);
        UIKit.Place(waveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -52f), new Vector2(380f, 30f));

        statusText = UIKit.Label("Status", panel.transform, "", 20f, TextAlignmentOptions.TopLeft, UIKit.TextMuted);
        UIKit.Place(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -86f), new Vector2(380f, 28f));
    }

    private void BuildWallBar()
    {
        Image panel = UIKit.Panel("Wall", root, UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 84f));

        TextMeshProUGUI label = UIKit.Label("Label", panel.transform, "THE WALL", 20f, TextAlignmentOptions.Top, UIKit.TextMuted, true);
        UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(560f, 24f));

        wallFill = UIKit.Bar("HP Bar", panel.transform, new Color(0f, 0f, 0f, 0.6f), UIKit.Good, out RectTransform bar);
        UIKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(560f, 30f));

        wallText = UIKit.Label("HP", bar, "100 / 100", 20f, TextAlignmentOptions.Center, UIKit.TextColor, true);
        UIKit.Stretch(wallText.rectTransform);
    }

    private void BuildInsight()
    {
        Image panel = UIKit.Panel("Insight", root, UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -20f), new Vector2(250f, 84f));

        Image gem = UIKit.Panel("Icon", panel.transform, UIKit.Accent, false);
        UIKit.Place(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(30f, 30f));
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        TextMeshProUGUI label = UIKit.Label("Label", panel.transform, "INSIGHT", 16f, TextAlignmentOptions.TopLeft, UIKit.TextMuted, true);
        UIKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -10f), new Vector2(160f, 20f));

        insightText = UIKit.Label("Value", panel.transform, "15 / 100", 34f, TextAlignmentOptions.BottomLeft, UIKit.TextColor, true);
        UIKit.Place(insightText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(76f, 8f), new Vector2(170f, 44f));

        Button pause = UIKit.Button("Pause Button", root, "II", 30f, TogglePause);
        UIKit.Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(72f, 84f));
    }

    private void BuildCountdown()
    {
        Image panel = UIKit.Panel("Countdown", root, UIKit.PanelColor);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(520f, 150f));
        countdownPanel = panel.gameObject;

        countdownTitle = UIKit.Label("Title", panel.transform, "", 22f, TextAlignmentOptions.Top, UIKit.TextColor, true);
        UIKit.Place(countdownTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(500f, 28f));

        countdownNumber = UIKit.Label("Number", panel.transform, "15", 48f, TextAlignmentOptions.Center, UIKit.Accent, true);
        UIKit.Place(countdownNumber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(200f, 54f));

        Button skip = UIKit.Button("Skip", panel.transform, "Mulai Sekarang  [N]", 20f, () => waves?.SkipCountdown());
        UIKit.Place((RectTransform)skip.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(260f, 40f));
    }

    private GameObject BuildOverlay(string name, out RectTransform box, float boxHeight)
    {
        Image dim = UIKit.Panel(name, root, new Color(0f, 0f, 0f, 0.6f));
        UIKit.Stretch(dim.rectTransform);

        Image panel = UIKit.Panel("Box", dim.transform, UIKit.PanelColor);
        box = UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, boxHeight));

        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    private void BuildPauseOverlay()
    {
        pauseOverlay = BuildOverlay("Pause Menu", out RectTransform box, 400f);

        TextMeshProUGUI title = UIKit.Label("Title", box, "Jeda", 48f, TextAlignmentOptions.Center, UIKit.TextColor, true);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 64f));

        AddMenuButton(box, "Lanjut", -120f, TogglePause);
        AddMenuButton(box, "Ulangi Stage", -200f, () => waves?.RestartStage());
        AddMenuButton(box, "Menu Utama", -280f, () => waves?.LoadMainMenu());
    }

    private void BuildResultOverlay()
    {
        resultOverlay = BuildOverlay("Result", out RectTransform box, 460f);

        resultTitle = UIKit.Label("Title", box, "", 56f, TextAlignmentOptions.Center, UIKit.Accent, true);
        UIKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 70f));

        resultSubtitle = UIKit.Label("Subtitle", box, "", 22f, TextAlignmentOptions.Top, UIKit.TextColor);
        UIKit.Place(resultSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(460f, 70f));
        resultSubtitle.textWrappingMode = TextWrappingModes.Normal;

        nextStageButton = AddMenuButton(box, "Stage Berikutnya", -190f, () => waves?.LoadNextStage()).gameObject;
        AddMenuButton(box, "Ulangi Stage", -270f, () => waves?.RestartStage());
        AddMenuButton(box, "Menu Utama", -350f, () => waves?.LoadMainMenu());
    }

    private Button AddMenuButton(RectTransform box, string text, float y, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(text, box, text, 26f, onClick);
        UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(340f, 62f));
        return b;
    }

    // ------------------------------------------------------------------
    // Update
    // ------------------------------------------------------------------

    private void Update()
    {
        HandlePauseKey();
        RefreshTopBar();
        RefreshCountdown();
        RefreshAnnouncement();
        RefreshResult();
    }

    private void HandlePauseKey()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        // Esc yang barusan dipakai membatalkan penempatan / drag tidak ikut membuka menu jeda
        if (grid != null && grid.LastCancelFrame == Time.frameCount) return;

        TogglePause();
    }

    private void TogglePause()
    {
        if (waves != null && (waves.State == StageState.Victory || waves.State == StageState.Defeat)) return;

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        pauseOverlay.SetActive(isPaused);

        if (isPaused && grid != null)
        {
            grid.CancelPlacement();
        }
    }

    private void RefreshTopBar()
    {
        if (waves != null)
        {
            StageData stage = waves.CurrentStage;
            stageText.text = stage != null ? stage.stageName : "Stage";

            int shownWave = Mathf.Max(1, waves.State == StageState.WaveRunning || waves.State == StageState.Victory
                ? waves.CurrentWaveNumber
                : waves.CurrentWaveNumber + 1);
            waveText.text = $"Wave {Mathf.Min(shownWave, Mathf.Max(1, waves.TotalWaves))} / {waves.TotalWaves}";

            switch (waves.State)
            {
                case StageState.Preparing: statusText.text = "Persiapan - pasang tower!"; break;
                case StageState.Break: statusText.text = "Jeda - api berhenti menyebar"; break;
                case StageState.WaveRunning: statusText.text = $"Musuh tersisa: {waves.EnemiesLeftThisWave}"; break;
                case StageState.Victory: statusText.text = "Stage selesai!"; break;
                case StageState.Defeat: statusText.text = "The Wall hancur"; break;
                default: statusText.text = ""; break;
            }
        }

        if (wall != null)
        {
            float ratio = wall.MaxHP > 0f ? wall.CurrentHP / wall.MaxHP : 0f;
            wallFill.fillAmount = ratio;
            wallFill.color = Color.Lerp(UIKit.Danger, UIKit.Good, ratio);
            wallText.text = $"{Mathf.CeilToInt(wall.CurrentHP)} / {wall.MaxHP:0}";
        }

        if (insight != null)
        {
            insightText.text = insight.IsFreeCostMode ? "GRATIS" : $"{insight.CurrentInsight} / {insight.MaxInsight}";
        }
    }

    private void RefreshCountdown()
    {
        bool show = waves != null && (waves.State == StageState.Preparing || waves.State == StageState.Break);
        if (countdownPanel.activeSelf != show) countdownPanel.SetActive(show);
        if (!show) return;

        int next = waves.CurrentWaveNumber + 1;
        countdownTitle.text = waves.State == StageState.Preparing
            ? $"Persiapan - Wave {next} dimulai dalam"
            : $"Jeda - Wave {next} dimulai dalam";
        countdownNumber.text = Mathf.CeilToInt(waves.StateTimeRemaining).ToString();
    }

    private void HandleWaveStarted(int waveNumber)
    {
        bool last = waves != null && waveNumber == waves.TotalWaves;
        announceText.text = last ? $"WAVE {waveNumber}\n<size=45%>Wave terakhir!</size>" : $"WAVE {waveNumber}";
        announceText.gameObject.SetActive(true);
        announceTimer = 1.8f;
    }

    private void RefreshAnnouncement()
    {
        if (announceTimer <= 0f) return;

        announceTimer -= Time.unscaledDeltaTime;
        Color c = announceText.color;
        c.a = Mathf.Clamp01(announceTimer / 0.6f);
        announceText.color = c;

        if (announceTimer <= 0f) announceText.gameObject.SetActive(false);
    }

    private void RefreshResult()
    {
        if (waves == null) return;

        StageState state = waves.State;
        bool ended = state == StageState.Victory || state == StageState.Defeat;
        if (!ended || shownResultFor == state) return;

        shownResultFor = state;
        isPaused = false;
        pauseOverlay.SetActive(false);
        resultOverlay.SetActive(true);

        string stageName = waves.CurrentStage != null ? waves.CurrentStage.stageName : "Stage";

        if (state == StageState.Victory)
        {
            resultTitle.text = "KEMENANGAN!";
            resultTitle.color = UIKit.Accent;
            resultSubtitle.text = waves.HasNextStage
                ? $"{stageName} selesai. The Wall tersisa {Mathf.CeilToInt(wall != null ? wall.CurrentHP : 0f)} HP."
                : "Semua stage selesai! Hutan berhasil diselamatkan.";
            nextStageButton.SetActive(waves.HasNextStage);
        }
        else
        {
            resultTitle.text = "KALAH";
            resultTitle.color = UIKit.Danger;
            resultSubtitle.text = $"The Wall hancur di {stageName}. Coba susun ulang tower-mu!";
            nextStageButton.SetActive(false);
        }
    }
}
