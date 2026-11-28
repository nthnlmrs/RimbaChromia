using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pemutar musik & efek suara (dibuat otomatis, hidup lintas scene).
/// - Musik: main menu / gameplay dipilih dari nama scene, berganti dengan fade.
/// - Efek suara dipanggil dari mana saja: AudioManager.Play(lib => lib.towerPlaced).
///   Suara yang sama dibatasi jaraknya (tidak menumpuk saat banyak tower menembak bersamaan).
/// - Volume mengikuti menu Settings: SOUND untuk efek, MUSIC untuk musik.
/// Klip diatur di Assets/Resources/AudioLibrary.asset.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string MainMenuScene = "mainMenu";
    private const float MusicFade = 1.2f;

    private static AudioManager instance;

    private AudioLibrary library;
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource fireLoop;
    private float fireLevel;
    private AudioClip currentMusic;
    private float musicFadeLevel = 1f;
    private Coroutine musicRoutine;
    private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
    private bool waitingForStoryCue;
    private float fadeInDuration = MusicFade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Ensure();
    }

    private static AudioManager Ensure()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("Audio Manager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioManager>();
        instance.Init();
        return instance;
    }

    private void Init()
    {
        library = Resources.Load<AudioLibrary>("AudioLibrary");

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        // Suara api: berulang terus, volume mengikuti banyaknya tile terbakar
        fireLoop = gameObject.AddComponent<AudioSource>();
        fireLoop.loop = true;
        fireLoop.playOnAwake = false;
        fireLoop.spatialBlend = 0f;
        fireLoop.volume = 0f;

        GameSettings.OnChanged += ApplyVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVolume();
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        GameSettings.OnChanged -= ApplyVolume;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (library == null) return;
        waitingForStoryCue = false;
        if (scene.name == MainMenuScene)
        {
            PlayMusic(library.menuMusic);
            return;
        }
        StartCoroutine(StartGameplayMusicAfterIntroCheck());
    }

    /// <summary>
    /// Scene stage: tunggu 1 frame (WaveManager memulai cerita di Start). Kalau cerita pembuka sedang
    /// diputar, musik menu dibiarkan dan musik gameplay baru masuk saat ledakan (StoryCue) / cerita selesai.
    /// </summary>
    private IEnumerator StartGameplayMusicAfterIntroCheck()
    {
        yield return null;
        if (StoryPlayer.IsPlaying)
        {
            waitingForStoryCue = true;
            yield break;
        }
        PlayMusic(library.gameplayMusic);
    }

    /// <summary>Momen dramatis di cerita (baris bergetar / ledakan): musik gameplay mulai, makin lama makin keras.</summary>
    public static void StoryCue()
    {
        AudioManager m = Ensure();
        if (!m.waitingForStoryCue || m.library == null) return;
        m.waitingForStoryCue = false;
        m.PlayMusic(m.library.gameplayMusic, fadeIn: 7f, fadeOut: 0.6f);
    }

    /// <summary>Cerita selesai tanpa ledakan: pastikan musik gameplay jalan.</summary>
    public static void StoryFinished()
    {
        AudioManager m = Ensure();
        if (!m.waitingForStoryCue || m.library == null) return;
        m.waitingForStoryCue = false;
        m.PlayMusic(m.library.gameplayMusic);
    }

    private void ApplyVolume()
    {
        float musicBase = library != null ? library.musicVolume : 0.55f;
        if (musicSource != null) musicSource.volume = musicBase * GameSettings.MusicVolume * musicFadeLevel;
        if (sfxSource != null) sfxSource.volume = GameSettings.SoundVolume;
    }

    private void Update()
    {
        // Api: makin banyak tile terbakar makin keras (maks. di ~8 tile), hening saat tidak ada api / game dijeda
        FireManager fire = FireManager.Instance;
        int burning = fire != null ? fire.BurningCount : 0;
        float target = burning > 0 && Time.timeScale > 0f ? Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((burning - 1) / 7f)) : 0f;
        fireLevel = Mathf.MoveTowards(fireLevel, target, Time.unscaledDeltaTime * 0.8f);

        AudioClip clip = library != null ? library.fireIgnite : null;
        if (clip != null && fireLevel > 0.001f)
        {
            if (fireLoop.clip != clip) fireLoop.clip = clip;
            if (!fireLoop.isPlaying) fireLoop.Play();
        }
        else if (fireLoop.isPlaying && fireLevel <= 0.001f)
        {
            fireLoop.Stop();
        }
        fireLoop.volume = fireLevel * (library != null ? library.fireVolume : 0.45f) * GameSettings.SoundVolume;
    }

    // ------------------------------------------------------------------
    // Musik
    // ------------------------------------------------------------------

    private void PlayMusic(AudioClip clip, float fadeIn = MusicFade, float fadeOut = MusicFade)
    {
        if (clip == currentMusic && musicSource.isPlaying) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(SwitchMusic(clip, fadeIn, fadeOut));
    }

    private IEnumerator SwitchMusic(AudioClip clip, float fadeIn = MusicFade, float fadeOut = MusicFade)
    {
        // Fade keluar musik lama (waktu unscaled: tetap jalan saat game dijeda)
        while (musicSource.isPlaying && musicFadeLevel > 0f)
        {
            musicFadeLevel = Mathf.MoveTowards(musicFadeLevel, 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeOut));
            ApplyVolume();
            yield return null;
        }

        musicSource.Stop();
        musicFadeLevel = 0f;
        ApplyVolume();
        currentMusic = clip;
        if (clip == null) yield break;

        musicSource.clip = clip;
        musicSource.Play();
        while (musicFadeLevel < 1f)
        {
            // Fade masuk dengan kurva (pelan di awal, membesar di akhir) untuk efek crescendo
            musicFadeLevel = Mathf.MoveTowards(musicFadeLevel, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeIn));
            ApplyVolume();
            yield return null;
        }
        musicRoutine = null;
    }

    /// <summary>Musik memudar (dipakai saat Victory / Defeat supaya jingle terdengar jelas).</summary>
    public static void FadeOutMusic()
    {
        AudioManager m = Ensure();
        if (m.musicRoutine != null) m.StopCoroutine(m.musicRoutine);
        m.musicRoutine = m.StartCoroutine(m.SwitchMusic(null));
    }

    // ------------------------------------------------------------------
    // Efek suara
    // ------------------------------------------------------------------

    /// <param name="minInterval">Jeda minimum antar pemutaran klip yang sama (detik, waktu nyata).</param>
    public static void Play(System.Func<AudioLibrary, AudioClip> pick, float volume = 1f, float minInterval = 0.05f)
    {
        AudioManager m = Ensure();
        if (m.library == null || pick == null) return;
        AudioClip clip = pick(m.library);
        if (clip == null) return;

        float now = Time.unscaledTime;
        if (m.lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;
        m.lastPlayed[clip] = now;

        m.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static float Vol(System.Func<AudioLibrary, float> pick)
    {
        AudioManager m = Ensure();
        return m.library != null ? pick(m.library) : 1f;
    }

    // Pintasan yang dipakai kode game
    public static void Click() => Play(l => l.buttonClick, Vol(l => l.uiVolume), 0.05f);
    public static void TowerPlaced() => Play(l => l.towerPlaced, Vol(l => l.towerVolume), 0.05f);
    public static void TowerMerged() => Play(l => l.towerMerge, Vol(l => l.towerVolume), 0.05f);
    public static void BlueShoot() => Play(l => l.blueShoot, Vol(l => l.shootVolume), 0.08f);
    public static void BlueSplash() => Play(l => l.blueSplash, Vol(l => l.shootVolume), 0.08f);
    public static void Explosion() => Play(l => l.explosion, Vol(l => l.towerVolume), 0.15f);
    public static void FireOut() => Play(l => l.fireOut, Vol(l => l.fireVolume), 0.3f);
    public static void WallHit() => Play(l => l.wallHit, Vol(l => l.towerVolume), 0.7f);
    public static void WaveStart() => Play(l => l.waveStart, Vol(l => l.jingleVolume), 1f);

    public static void Victory()
    {
        FadeOutMusic();
        Play(l => l.victory, Vol(l => l.jingleVolume), 1f);
    }

    public static void Defeat()
    {
        FadeOutMusic();
        Play(l => l.defeat, Vol(l => l.jingleVolume), 1f);
    }
}
