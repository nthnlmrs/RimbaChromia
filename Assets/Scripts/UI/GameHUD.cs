using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD gameplay memakai art dari artist (Assets/Art/UI, mockup di Art/UI/_Reference), dibangun lewat kode:
///  - Kiri atas   : plate Insight + jumlah insight
///  - Tengah atas : HP The Wall (plate + isi bar)
///  - Kanan atas  : plate "WAVE x/y" (saat wave berjalan) atau tombol START NOW (saat persiapan/jeda) + tombol Pause
///  - Bawah tengah atas: "WAVE N IS STARTING IN..." + hitung mundur
///  - Bawah       : kartu tower (TowerCardBar) + panel deskripsi
///  - Health bar tower & musuh (HealthBarLayer), panel info tower (TowerInfoPanel)
///  - Menu PAUSED (Esc / tombol pause): SOUND & MUSIC, QUIT, RESTART, X
///  - Layar VICTORY / DEFEATED: QUIT, RESTART, NEXT (lanjut stage)
/// </summary>
public class GameHUD : MonoBehaviour
{
    private WaveManager waves;
    private GridManager grid;
    private WallHealth wall;
    private InsightManager insight;
    private UISkin skin;

    private RectTransform root;

    private TextMeshProUGUI insightText;
    private Image wallFill;
    private TextMeshProUGUI wallText;

    private Image wavePlate;
    private TextMeshProUGUI wavePlateFallback;
    private GameObject startNowButton;

    private GameObject countdownGroup;
    private Image countdownImage;
    private TextMeshProUGUI countdownFallback;
    private TextMeshProUGUI countdownNumber;

    private TextMeshProUGUI announceText;
    private float announceTimer;

    private TextMeshProUGUI toastText;
    private float toastTimer;

    private GameObject pauseOverlay;
    private VolumeRow soundRow;
    private VolumeRow musicRow;

    private GameObject resultOverlay;
    private Image resultDim;
    private Image resultBanner;
    private GameObject nextStageButton;

    private bool isPaused;
    private StageState shownResultFor = StageState.Loading;

    private void Start()
    {
        waves = WaveManager.Instance;
        grid = GridManager.Instance;
        wall = FindFirstObjectByType<WallHealth>();
        insight = InsightManager.Instance;
        skin = UISkin.Current;

        if (skin == null)
        {
            Debug.LogWarning("UISkin tidak ditemukan di Resources. HUD memakai tampilan sederhana.");
        }

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
        if (grid != null)
        {
            grid.OnActionRejected -= ShowToast;
        }
    }

    private void ShowToast(string message)
    {
        toastText.text = message;
        toastText.alpha = 1f;
        toastText.gameObject.SetActive(true);
        toastTimer = 1.8f;
    }

    private void RefreshToast()
    {
        if (toastTimer <= 0f) return;
        toastTimer -= Time.unscaledDeltaTime;
        toastText.alpha = Mathf.Clamp01(toastTimer / 0.5f);
        if (toastTimer <= 0f) toastText.gameObject.SetActive(false);
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

        BuildInsight();
        BuildWall();
        BuildTopRight();
        BuildCountdown();

        announceText = UIKit.ArtLabel("Wave Announce", root, "", 96f);
        UIKit.Place(announceText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1200f, 220f));
        announceText.gameObject.SetActive(false);

        // Pesan singkat (misal merge ditolak)
        toastText = UIKit.ArtLabel("Toast", root, "", 30f, TextAlignmentOptions.Center, new Color(1f, 0.78f, 0.62f));
        UIKit.Place(toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1000f, 44f));
        toastText.gameObject.SetActive(false);
        if (grid != null) grid.OnActionRejected += ShowToast;

        if (grid != null)
        {
            RectTransform cardBar = UIKit.Rect("Tower Cards", root);
            cardBar.gameObject.AddComponent<TowerCardBar>().Build(grid, root);

            RectTransform info = UIKit.Rect("Tower Info", root);
            info.gameObject.AddComponent<TowerInfoPanel>().Build(grid, root);
        }

        // Stage Tutorial: panduan Taduh langkah demi langkah
        if (waves != null && waves.CurrentStage != null && waves.CurrentStage.isTutorial)
        {
            RectTransform guide = UIKit.Rect("Tutorial Guide", root);
            guide.gameObject.AddComponent<TutorialGuide>().Build(waves, grid);
        }

        TextMeshProUGUI controls = UIKit.ArtLabel("Controls Hint", root,
            "WASD move camera  |  Scroll zoom  |  R reset camera  |  Esc pause", 20f, TextAlignmentOptions.TopLeft);
        UIKit.Place(controls.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -150f), new Vector2(700f, 28f));
        controls.alpha = 0.75f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TextMeshProUGUI debugHint = UIKit.ArtLabel("Debug Hint", root,
            "Debug: F fire  X extinguish  P pause fire  N skip", 18f, TextAlignmentOptions.TopRight);
        UIKit.Place(debugHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -150f), new Vector2(600f, 26f));
        debugHint.alpha = 0.55f;
#endif

        BuildPauseMenu();
        BuildResultScreen();
    }

    private void BuildInsight()
    {
        Image plate = UIKit.ArtImage("Insight", root, skin != null ? skin.insightPlate : null);
        UIKit.Place(plate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -14f),
            skin != null && skin.insightPlate != null ? skin.insightPlate.rect.size * UIKit.ArtScale : new Vector2(320f, 120f));
        if (skin == null) plate.color = UIKit.PanelColor;

        // Angka di bagian papan kayu (kanan ikon)
        insightText = UIKit.ArtLabel("Value", plate.transform, "0", 48f);
        UIKit.Place(insightText.rectTransform, new Vector2(0.67f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 70f));
    }

    private void BuildWall()
    {
        Sprite plateSprite = skin != null ? skin.wallPlate : null;
        Image plate = UIKit.ArtImage("Wall", root, plateSprite);
        UIKit.Place(plate.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f),
            plateSprite != null ? plateSprite.rect.size * UIKit.ArtScale : new Vector2(340f, 94f));
        if (plateSprite == null) plate.color = UIKit.PanelColor;

        // Isi bar ditempatkan di slot gelap pada plate (slot: x 138..373, y 42..60 dari atas, plate 397x110)
        wallFill = UIKit.ArtImage("Fill", plate.transform, skin != null ? skin.wallFill : UIKit.WhiteSprite);
        wallFill.preserveAspect = false;
        wallFill.type = Image.Type.Filled;
        wallFill.fillMethod = Image.FillMethod.Horizontal;
        wallFill.fillOrigin = 0;
        RectTransform fill = wallFill.rectTransform;
        fill.anchorMin = new Vector2(139f / 397f, 1f - 60f / 110f);
        fill.anchorMax = new Vector2(372f / 397f, 1f - 43f / 110f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        // Angka HP di antara ikon kastil dan bar
        wallText = UIKit.ArtLabel("Value", plate.transform, "100", 30f);
        UIKit.Place(wallText.rectTransform, new Vector2(106f / 397f, 1f - 52f / 110f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 44f));
    }

    private void BuildTopRight()
    {
        float s = UIKit.ArtScale;

        Button pause = UIKit.ArtButton("Pause Button", root, skin?.pauseNormal, skin?.pausePressed, s, TogglePause);
        RectTransform pauseRt = (RectTransform)pause.transform;
        UIKit.Place(pauseRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -18f), pauseRt.sizeDelta == Vector2.zero ? new Vector2(94f, 94f) : pauseRt.sizeDelta);
        if (skin == null) { pause.GetComponent<Image>().color = UIKit.ButtonColor; }

        float leftOfPause = -36f - pauseRt.sizeDelta.x - 18f;

        // Plate WAVE x/y (saat wave berjalan)
        wavePlate = UIKit.ArtImage("Wave Plate", root, null);
        UIKit.Place(wavePlate.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(leftOfPause, -26f), new Vector2(256f, 93f) * s);
        wavePlateFallback = UIKit.ArtLabel("Text", wavePlate.transform, "", 34f);
        UIKit.Stretch(wavePlateFallback.rectTransform);

        // START NOW (saat persiapan / jeda)
        Button startNow = UIKit.ArtButton("Start Now", root, skin?.startNow, null, s, () => waves?.SkipCountdown());
        RectTransform startRt = (RectTransform)startNow.transform;
        UIKit.Place(startRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(leftOfPause, -18f),
            startRt.sizeDelta == Vector2.zero ? new Vector2(218f, 94f) : startRt.sizeDelta);
        if (skin == null || skin.startNow == null)
        {
            startNow.GetComponent<Image>().color = UIKit.ButtonColor;
            TextMeshProUGUI t = UIKit.ArtLabel("Text", startRt, "START NOW", 30f);
            UIKit.Stretch(t.rectTransform);
        }
        startNowButton = startNow.gameObject;
    }

    private void BuildCountdown()
    {
        countdownGroup = UIKit.Rect("Countdown", root).gameObject;
        RectTransform group = (RectTransform)countdownGroup.transform;
        UIKit.Place(group, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(640f, 50f));

        countdownImage = UIKit.ArtImage("Title", group, null);
        UIKit.Place(countdownImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(417f, 33f));

        countdownFallback = UIKit.ArtLabel("Title Text", group, "", 34f);
        UIKit.Place(countdownFallback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(560f, 50f));

        countdownNumber = UIKit.ArtLabel("Number", group, "15", 44f, TextAlignmentOptions.Left);
        UIKit.Place(countdownNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(110f, 56f));
    }

    // ------------------------------------------------------------------
    // Menu PAUSED
    // ------------------------------------------------------------------

    private void BuildPauseMenu()
    {
        Image dim = UIKit.Panel("Pause Menu", root, new Color(0f, 0f, 0f, 0.45f));
        UIKit.Stretch(dim.rectTransform);
        pauseOverlay = dim.gameObject;

        if (skin == null || skin.pausePanel == null)
        {
            // Placeholder sederhana kalau art belum ada
            Button resume = UIKit.Button("Resume", dim.transform, "RESUME", 32f, TogglePause);
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(320f, 70f));
            Button restart = UIKit.Button("Restart", dim.transform, "RESTART", 32f, () => waves?.RestartStage());
            UIKit.Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(320f, 70f));
            Button quit = UIKit.Button("Quit", dim.transform, "QUIT", 32f, () => waves?.LoadMainMenu());
            UIKit.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(320f, 70f));
            pauseOverlay.SetActive(false);
            return;
        }

        // Panel dari artist (sudah berisi judul PAUSED, label SOUND/MUSIC, dan gambar tombol)
        Image panel = UIKit.ArtImage("Panel", dim.transform, skin.pausePanel);
        UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), panel.rectTransform.sizeDelta);
        RectTransform p = panel.rectTransform;

        // Tombol interaktif ditumpuk tepat di atas gambar tombol pada panel (koordinat pixel panel 795x629)
        Button close = UIKit.ArtButton("Close", p, skin.exitNormal, skin.exitPressed, UIKit.ArtScale, TogglePause);
        UIKit.PlaceOnArt((RectTransform)close.transform, new Vector2(740f, 74f), new Vector2(102f, 102f));

        Button quitButton = UIKit.ArtButton("Quit", p, skin.quitNormal, skin.quitPressed, UIKit.ArtScale, () => waves?.LoadMainMenu());
        UIKit.PlaceOnArt((RectTransform)quitButton.transform, new Vector2(196.5f, 553.5f), new Vector2(303f, 129f));

        Button restartButton = UIKit.ArtButton("Restart", p, skin.restartNormal, skin.restartPressed, UIKit.ArtScale, () => waves?.RestartStage());
        UIKit.PlaceOnArt((RectTransform)restartButton.transform, new Vector2(565.5f, 553.5f), new Vector2(303f, 129f));

        // Slider daun SOUND & MUSIC (sejajar label di panel)
        soundRow = new VolumeRow(p, skin, 275f, () => GameSettings.SoundLevel, v => GameSettings.SoundLevel = v);
        musicRow = new VolumeRow(p, skin, 374f, () => GameSettings.MusicLevel, v => GameSettings.MusicLevel = v);

        pauseOverlay.SetActive(false);
    }

    /// <summary>Satu baris volume: panah kiri, 6 daun (klik daun = set level), panah kanan.</summary>
    private class VolumeRow
    {
        private readonly Image[] leaves = new Image[GameSettings.MaxLevel];
        private readonly UISkin skin;
        private readonly System.Func<int> get;
        private readonly System.Action<int> set;

        public VolumeRow(RectTransform panel, UISkin skin, float rowY, System.Func<int> get, System.Action<int> set)
        {
            this.skin = skin;
            this.get = get;
            this.set = set;

            Button left = UIKit.ArtButton("Volume Down", panel, skin.volumeArrowLeft, null, UIKit.ArtScale, () => Change(-1));
            UIKit.PlaceOnArt((RectTransform)left.transform, new Vector2(275f, rowY), new Vector2(36f, 44f));

            for (int i = 0; i < leaves.Length; i++)
            {
                int level = i + 1;
                Button leaf = UIKit.ArtButton($"Leaf {level}", panel, skin.volumeLeafEmpty, null, UIKit.ArtScale,
                    () => this.set(this.get() == level ? level - 1 : level)); // klik daun yang sama = turun satu (bisa ke 0)
                UIKit.PlaceOnArt((RectTransform)leaf.transform, new Vector2(337f + 60f * i, rowY - 2f), new Vector2(48f, 53f));
                leaves[i] = leaf.GetComponent<Image>();
            }

            Button right = UIKit.ArtButton("Volume Up", panel, skin.volumeArrowRight, null, UIKit.ArtScale, () => Change(1));
            UIKit.PlaceOnArt((RectTransform)right.transform, new Vector2(697f, rowY), new Vector2(36f, 44f));

            Refresh();
        }

        private void Change(int delta)
        {
            set(get() + delta);
        }

        public void Refresh()
        {
            int level = get();
            for (int i = 0; i < leaves.Length; i++)
            {
                Sprite lit = skin.volumeLeaves != null && i < skin.volumeLeaves.Length ? skin.volumeLeaves[i] : null;
                leaves[i].sprite = i < level && lit != null ? lit : skin.volumeLeafEmpty;
            }
        }
    }

    // ------------------------------------------------------------------
    // Layar VICTORY / DEFEATED
    // ------------------------------------------------------------------

    private void BuildResultScreen()
    {
        resultDim = UIKit.Panel("Result", root, new Color(0f, 0f, 0f, 0.45f));
        UIKit.Stretch(resultDim.rectTransform);
        resultOverlay = resultDim.gameObject;

        resultBanner = UIKit.ArtImage("Banner", resultDim.transform, null);
        UIKit.Place(resultBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1920f, 600f));

        float s = UIKit.ArtScale * 0.9f;
        float y = -300f;

        Button quit = UIKit.ArtButton("Quit", resultDim.transform, skin?.quitNormal, skin?.quitPressed, s, () => waves?.LoadMainMenu());
        UIKit.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-165f, y), ((RectTransform)quit.transform).sizeDelta);

        Button restart = UIKit.ArtButton("Restart", resultDim.transform, skin?.restartNormal, skin?.restartPressed, s, () => waves?.RestartStage());
        UIKit.Place((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(165f, y), ((RectTransform)restart.transform).sizeDelta);

        // Panah NEXT: lanjut ke stage berikutnya (hanya saat menang dan masih ada stage)
        Button next = UIKit.ArtButton("Next Stage", resultDim.transform, skin?.nextNormal, skin?.nextPressed, UIKit.ArtScale * 2.2f, () => waves?.LoadNextStage());
        UIKit.Place((RectTransform)next.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(390f, y), ((RectTransform)next.transform).sizeDelta);
        nextStageButton = next.gameObject;

        if (skin == null)
        {
            foreach (Button b in new[] { quit, restart, next }) b.GetComponent<Image>().color = UIKit.ButtonColor;
        }

        resultOverlay.SetActive(false);
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
        RefreshToast();
        RefreshResult();

        if (isPaused)
        {
            soundRow?.Refresh();
            musicRow?.Refresh();
        }
    }

    private void HandlePauseKey()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        // Esc yang barusan dipakai membatalkan penempatan / drag tidak ikut membuka menu jeda
        if (grid != null && grid.LastCancelFrame == Time.frameCount) return;
        if (StoryPlayer.IsPlaying) return;

        TogglePause();
    }

    private void TogglePause()
    {
        if (waves != null && (waves.State == StageState.Victory || waves.State == StageState.Defeat)) return;
        if (StoryPlayer.IsPlaying) return; // cerita sedang diputar (game sudah di-pause oleh StoryPlayer)

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
        if (insight != null)
        {
            insightText.text = insight.IsFreeCostMode ? "FREE" : insight.CurrentInsight.ToString();
        }

        if (wall != null)
        {
            float ratio = wall.MaxHP > 0f ? wall.CurrentHP / wall.MaxHP : 0f;
            wallFill.fillAmount = ratio;
            // Art bar sudah berwarna; saat kritis diberi semburat merah
            wallFill.color = ratio > 0.3f ? Color.white : Color.Lerp(new Color(1f, 0.35f, 0.3f), Color.white, ratio / 0.3f);
            wallText.text = Mathf.CeilToInt(wall.CurrentHP).ToString();
        }

        if (waves == null) return;

        bool waiting = waves.State == StageState.Preparing || waves.State == StageState.Break;
        bool canStart = waiting && !waves.HoldCountdown;
        if (startNowButton.activeSelf != canStart) startNowButton.SetActive(canStart);

        // Plate wave: wave yang sedang / terakhir berjalan; saat menunggu (tutorial) tampilkan wave berikutnya
        bool showPlate = !canStart && waves.TotalWaves > 0;
        if (wavePlate.gameObject.activeSelf != showPlate) wavePlate.gameObject.SetActive(showPlate);
        if (showPlate)
        {
            int current = waiting ? waves.CurrentWaveNumber + 1 : Mathf.Max(1, waves.CurrentWaveNumber);
            current = Mathf.Clamp(current, 1, waves.TotalWaves);
            Sprite plate = skin != null ? skin.WavePlate(current, waves.TotalWaves) : null;
            wavePlate.sprite = plate;
            wavePlate.color = plate != null ? Color.white : UIKit.PanelColor;
            wavePlateFallback.text = plate != null ? "" : $"WAVE {current}/{waves.TotalWaves}";
        }
    }

    private void RefreshCountdown()
    {
        bool show = waves != null && (waves.State == StageState.Preparing || waves.State == StageState.Break);
        if (countdownGroup.activeSelf != show) countdownGroup.SetActive(show);
        if (!show) return;

        int next = waves.CurrentWaveNumber + 1;
        Sprite title = skin != null ? skin.Countdown(next) : null;
        countdownImage.enabled = title != null;
        if (title != null && countdownImage.sprite != title)
        {
            countdownImage.sprite = title;
            countdownImage.rectTransform.sizeDelta = title.rect.size * UIKit.ArtScale;
        }
        countdownFallback.text = title != null ? "" : $"WAVE {next} IS STARTING IN...";

        countdownNumber.text = waves.HoldCountdown ? "" : Mathf.CeilToInt(waves.StateTimeRemaining).ToString();
    }

    private void HandleWaveStarted(int waveNumber)
    {
        bool last = waves != null && waveNumber == waves.TotalWaves;
        announceText.text = last ? $"WAVE {waveNumber}\n<size=45%>FINAL WAVE!</size>" : $"WAVE {waveNumber}";
        announceText.gameObject.SetActive(true);
        announceTimer = 1.8f;
    }

    private void RefreshAnnouncement()
    {
        if (announceTimer <= 0f) return;

        announceTimer -= Time.unscaledDeltaTime;
        announceText.alpha = Mathf.Clamp01(announceTimer / 0.6f);

        if (announceTimer <= 0f) announceText.gameObject.SetActive(false);
    }

    private void RefreshResult()
    {
        if (waves == null) return;

        StageState state = waves.State;
        bool ended = state == StageState.Victory || state == StageState.Defeat;
        if (!ended || shownResultFor == state) return;
        if (StoryPlayer.IsPlaying) return; // tunggu cerita Ending selesai dulu

        shownResultFor = state;
        isPaused = false;
        pauseOverlay.SetActive(false);
        resultOverlay.SetActive(true);

        bool victory = state == StageState.Victory;
        Sprite banner = skin != null ? (victory ? skin.victoryBanner : skin.defeatedBanner) : null;

        // Victory: layar diredupkan abu-abu. Defeated: semburat merah (sesuai mockup)
        resultDim.color = victory ? new Color(0f, 0f, 0f, 0.45f) : new Color(0.36f, 0.16f, 0.17f, 0.82f);

        if (banner != null)
        {
            resultBanner.sprite = banner;
            float width = 1920f;
            resultBanner.rectTransform.sizeDelta = new Vector2(width, width * banner.rect.height / banner.rect.width);
        }
        else
        {
            resultBanner.color = victory ? UIKit.Good : UIKit.Danger;
            TextMeshProUGUI t = UIKit.ArtLabel("Title", resultBanner.transform, victory ? "VICTORY" : "DEFEATED", 120f);
            UIKit.Stretch(t.rectTransform);
        }

        nextStageButton.SetActive(victory && waves.HasNextStage);
    }
}
