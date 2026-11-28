using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum StageState
{
    Loading,
    Preparing,   // 15 detik awal sebelum wave 1
    WaveRunning,
    Break,       // 15 detik jeda antar wave
    Victory,
    Defeat
}

/// <summary>
/// Mengatur alur satu stage sesuai GDD:
/// Persiapan 15 detik -> Wave 1 -> Jeda 15 detik -> Wave 2 -> ... -> Victory.
/// Wall hancur -> Defeat.
/// Saat Persiapan & Jeda, api tidak menyebar dan tidak naik level (FireManager di-pause).
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    /// <summary>
    /// Stage yang dipilih dari menu / tombol Restart / Next Stage (index 0-based).
    /// -1 = pakai "Start Stage" di Inspector. Static supaya tetap tersimpan saat scene di-reload.
    /// </summary>
    public static int SelectedStageIndex = -1;

    /// <summary>Restart stage tidak perlu mengulang cerita intro.</summary>
    private static bool skipIntroOnce;

    /// <summary>Tutorial menahan hitung mundur sampai pemain selesai mengikuti panduan.</summary>
    public bool HoldCountdown { get; set; }

    // Project ini mematikan Domain Reload (Enter Play Mode Options), jadi variabel static
    // TIDAK otomatis ter-reset saat Stop -> Play. Reset manual di sini setiap kali Play ditekan.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        SelectedStageIndex = -1;
        skipIntroOnce = false;
        Instance = null;
    }

    [Header("Stages (urutan = Stage 1, 2, 3)")]
    [SerializeField] private StageData[] stages = new StageData[0];

    [Tooltip("Stage yang dimainkan saat tekan Play (urutan list Stages). Default: 1 = Tutorial, 2 = Stage 1, 3 = Stage 2, 4 = Stage 3")]
    [FormerlySerializedAs("startStageIndex")]
    [SerializeField, Min(1)] private int startStage = 1;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WallHealth wallHealth;
    [SerializeField] private Enemy enemyPrefab;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "mainMenu";

    [Header("Debug")]
    [Tooltip("Tekan untuk langsung mulai wave tanpa menunggu hitung mundur")]
    [SerializeField] private Key skipCountdownKey = Key.N;
    [Tooltip("HUD teks lama (IMGUI) untuk debugging. HUD utama = GameHUD (otomatis).")]
    [SerializeField] private bool showDebugHud = false;

    private StageData currentStage;
    private int stageIndex;
    private StageState state = StageState.Loading;
    private int currentWaveIndex = -1;
    private float stateTimer;
    private int runningSpawners;
    private int spawnedThisWave;
    private int totalThisWave;

    public StageData CurrentStage => currentStage;
    public int StageIndex => stageIndex;
    public int StageCount => stages != null ? stages.Length : 0;
    public bool HasNextStage => stageIndex + 1 < StageCount;
    public StageState State => state;
    public int CurrentWaveNumber => currentWaveIndex + 1; // 1-based untuk UI
    public int TotalWaves => currentStage != null && currentStage.waves != null ? currentStage.waves.Length : 0;
    public float StateTimeRemaining => Mathf.Max(0f, stateTimer);
    public int EnemiesAlive => Enemy.ActiveEnemies.Count;
    public int EnemiesLeftThisWave => (totalThisWave - spawnedThisWave) + Enemy.ActiveEnemies.Count;

    public event Action<StageState> OnStateChanged;
    public event Action<int> OnWaveStarted; // nomor wave 1-based

    private void Awake()
    {
        Instance = this;

        // Pastikan waktu normal (bisa saja tersisa 0 dari Victory/Defeat sebelumnya)
        Time.timeScale = 1f;

        stageIndex = SelectedStageIndex >= 0 ? SelectedStageIndex : startStage - 1;
        if (stages != null && stages.Length > 0)
        {
            stageIndex = Mathf.Clamp(stageIndex, 0, stages.Length - 1);
            currentStage = stages[stageIndex];
        }

        if (currentStage == null)
        {
            Debug.LogWarning("WaveManager: belum ada StageData. Grid memakai layout default dan tidak ada wave.");
        }

        if (showDebugHud && GetComponent<StageDebugHUD>() == null)
        {
            gameObject.AddComponent<StageDebugHUD>();
        }

        if (FindFirstObjectByType<GameHUD>() == null)
        {
            gameObject.AddComponent<GameHUD>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (gridManager == null) gridManager = GridManager.Instance;
        if (wallHealth == null) wallHealth = FindFirstObjectByType<WallHealth>();

        if (wallHealth != null)
        {
            wallHealth.OnWallDestroyed += HandleWallDestroyed;
        }

        if (gridManager == null)
        {
            Debug.LogError("WaveManager tidak menemukan GridManager.");
            enabled = false;
            return;
        }

        if (gridManager.IsGridReady)
        {
            BeginStage();
        }
        else
        {
            gridManager.OnGridGenerated += BeginStage;
        }
    }

    private void BeginStage()
    {
        gridManager.OnGridGenerated -= BeginStage;

        if (currentStage == null || TotalWaves == 0)
        {
            Debug.LogWarning("WaveManager: stage tidak punya wave.");
            return;
        }

        // GDD Tutorial: pasang & merge tower tanpa biaya
        if (InsightManager.Instance != null)
        {
            InsightManager.Instance.SetFreeCostMode(currentStage.freeCost);
        }

        Debug.Log($"=== {currentStage.stageName} dimulai: {TotalWaves} wave ===");

        bool playIntro = currentStage.introDialogue != null && !skipIntroOnce;
        skipIntroOnce = false;

        if (playIntro)
        {
            // Cerita dulu (game di-pause), persiapan dimulai setelah cerita selesai
            StoryPlayer.Play(currentStage.introDialogue, StartPreparation);
        }
        else
        {
            StartPreparation();
        }
    }

    private void StartPreparation()
    {
        EnterState(StageState.Preparing, currentStage.prepTime);
    }

    private void Update()
    {
        HandleDebugShortcuts();

        switch (state)
        {
            case StageState.Preparing:
            case StageState.Break:
                if (HoldCountdown)
                {
                    break; // tutorial: tunggu pemain selesai mengikuti panduan
                }

                stateTimer -= Time.deltaTime;

                bool skip = Keyboard.current != null && Keyboard.current[skipCountdownKey].wasPressedThisFrame;
                if (stateTimer <= 0f || skip)
                {
                    StartWave(currentWaveIndex + 1);
                }
                break;

            case StageState.WaveRunning:
                if (runningSpawners == 0 && Enemy.ActiveEnemies.Count == 0)
                {
                    HandleWaveCleared();
                }
                break;
        }
    }

    // ------------------------------------------------------------------
    // Alur stage
    // ------------------------------------------------------------------

    private void StartWave(int waveIndex)
    {
        currentWaveIndex = waveIndex;
        WaveData wave = currentStage.waves[waveIndex];

        totalThisWave = wave.TotalEnemies;
        spawnedThisWave = 0;
        runningSpawners = 0;

        EnterState(StageState.WaveRunning, 0f);
        Debug.Log($"--- Wave {CurrentWaveNumber}/{TotalWaves} dimulai ({totalThisWave} musuh) ---");
        OnWaveStarted?.Invoke(CurrentWaveNumber);

        // Tiap titik spawn jalan paralel, grup di titik yang sama keluar berurutan
        foreach (int spawnPoint in wave.SpawnPoints())
        {
            List<SpawnGroup> groups = new List<SpawnGroup>();
            foreach (SpawnGroup group in wave.groups)
            {
                if (group != null && group.spawnPoint == spawnPoint) groups.Add(group);
            }

            runningSpawners++;
            StartCoroutine(SpawnRoutine(spawnPoint, groups));
        }
    }

    private IEnumerator SpawnRoutine(int spawnPoint, List<SpawnGroup> groups)
    {
        List<Vector2Int> path = null;

        if (gridManager.TryGetSpawnPoint(spawnPoint, out Vector2Int start))
        {
            path = gridManager.FindPathToWall(start);
        }
        else
        {
            Debug.LogError($"Titik spawn '{spawnPoint}' tidak ada di layout {currentStage.stageName}.");
        }

        foreach (SpawnGroup group in groups)
        {
            if (group.startDelay > 0f)
            {
                yield return new WaitForSeconds(group.startDelay);
            }

            for (int i = 0; i < group.count; i++)
            {
                if (state != StageState.WaveRunning) break;

                if (path != null)
                {
                    SpawnEnemy(group.enemy, path, spawnPoint);
                }
                spawnedThisWave++;

                if (i < group.count - 1)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.1f, group.interval));
                }
            }

            // Jeda kecil antar grup supaya musuh berat tidak menempel ke grup sebelumnya
            yield return new WaitForSeconds(1f);
        }

        runningSpawners--;
    }

    private void SpawnEnemy(EnemyData data, List<Vector2Int> path, int spawnPoint)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("WaveManager: Enemy Prefab belum dipasang.");
            return;
        }

        Enemy enemy = Instantiate(enemyPrefab);
        string enemyName = data != null ? data.enemyName : "Enemy";
        enemy.name = $"{enemyName}_{currentStage.GetSpawnPointName(spawnPoint)}_{spawnedThisWave + 1}";
        enemy.Setup(gridManager, path, wallHealth, data);
    }

    private void HandleWaveCleared()
    {
        Debug.Log($"--- Wave {CurrentWaveNumber} selesai ---");

        if (currentWaveIndex + 1 >= TotalWaves)
        {
            EnterState(StageState.Victory, 0f);
            Debug.Log($"=== VICTORY! {currentStage.stageName} selesai ===");
            Time.timeScale = 0f;

            // GDD: Ending (visual novel) setelah stage terakhir. Panel hasil muncul setelah cerita selesai.
            // Stage terakhir: lanjut credit bergulir setelah cerita
            string credits = HasNextStage ? null : currentStage.credits;
            if (currentStage.outroDialogue != null)
            {
                StoryPlayer.Play(currentStage.outroDialogue, () => CreditsRoll.Play(credits, null));
            }
            else
            {
                CreditsRoll.Play(credits, null);
            }
            return;
        }

        EnterState(StageState.Break, currentStage.breakTime);
    }

    private void HandleWallDestroyed()
    {
        if (state == StageState.Victory || state == StageState.Defeat) return;

        StopAllCoroutines();
        runningSpawners = 0;
        EnterState(StageState.Defeat, 0f);
        Debug.Log($"=== DEFEAT di {currentStage?.stageName} ===");
    }

    private void EnterState(StageState newState, float duration)
    {
        state = newState;
        stateTimer = duration;

        switch (newState)
        {
            case StageState.WaveRunning: AudioManager.WaveStart(); break;
            case StageState.Victory: AudioManager.Victory(); break;
            case StageState.Defeat: AudioManager.Defeat(); break;
        }

        // GDD: api tidak menyebar & tidak naik level saat persiapan / jeda
        if (FireManager.Instance != null)
        {
            bool pauseFire = newState != StageState.WaveRunning;
            if (FireManager.Instance.IsSimulationPaused != pauseFire)
            {
                FireManager.Instance.SetSimulationPaused(pauseFire);
            }
        }

        OnStateChanged?.Invoke(newState);
    }

    // ------------------------------------------------------------------
    // Dipanggil tombol UI (Victory / Defeat / Pause menu nanti)
    // ------------------------------------------------------------------

    public void RestartStage()
    {
        SelectedStageIndex = stageIndex;
        skipIntroOnce = true;
        ReloadScene();
    }

    public void LoadNextStage()
    {
        SelectedStageIndex = HasNextStage ? stageIndex + 1 : stageIndex;
        ReloadScene();
    }

    /// <summary>Langsung mulai wave berikutnya tanpa menunggu hitung mundur (tombol "Mulai Sekarang").</summary>
    public void SkipCountdown()
    {
        if ((state == StageState.Preparing || state == StageState.Break) && !HoldCountdown)
        {
            StartWave(currentWaveIndex + 1);
        }
    }

    // ------------------------------------------------------------------
    // Shortcut debug untuk mencoba stage / wave tertentu (Editor & development build saja)
    //   F1..F4 = Tutorial, Stage 1, Stage 2, Stage 3 (tanpa cerita pembuka)
    //   J      = lompat ke wave terakhir (boss Stage 3), K = kalahkan semua musuh di layar
    // ------------------------------------------------------------------

    private void HandleDebugShortcuts()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        Key[] stageKeys = { Key.F1, Key.F2, Key.F3, Key.F4 };
        for (int i = 0; i < stageKeys.Length && i < StageCount; i++)
        {
            if (kb[stageKeys[i]].wasPressedThisFrame)
            {
                SelectedStageIndex = i;
                skipIntroOnce = true;
                ReloadScene();
                return;
            }
        }

        if (kb.kKey.wasPressedThisFrame) KillAllEnemies();
        if (kb.jKey.wasPressedThisFrame) JumpToLastWave();
    }

    private static void KillAllEnemies()
    {
        foreach (Enemy enemy in new List<Enemy>(Enemy.ActiveEnemies))
        {
            if (enemy != null && !enemy.IsDead) enemy.TakeDamage(1000000f);
        }
    }

    /// <summary>Hentikan wave sekarang, bersihkan musuh, lalu jeda singkat sebelum wave terakhir.</summary>
    private void JumpToLastWave()
    {
        if (currentStage == null || TotalWaves == 0 || state == StageState.Victory || state == StageState.Defeat) return;
        StopAllCoroutines();
        runningSpawners = 0;
        KillAllEnemies();
        currentWaveIndex = TotalWaves - 2; // wave berikutnya = wave terakhir
        HoldCountdown = false;
        EnterState(StageState.Break, 3f);
        Debug.Log($"[Debug] Lompat ke wave terakhir ({TotalWaves}/{TotalWaves}) dalam 3 detik");
    }

    public void LoadMainMenu()
    {
        SelectedStageIndex = -1;
        LoadScene(mainMenuScene);
    }

    private void ReloadScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    private void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
