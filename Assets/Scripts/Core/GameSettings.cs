using UnityEngine;

/// <summary>
/// Pengaturan pemain (menu Pause: SOUND & MUSIC), disimpan di PlayerPrefs.
/// Level 0..6 sesuai 6 daun di UI. Sound langsung mengatur volume global (AudioListener);
/// Music disimpan untuk AudioManager/musik latar nanti (aset audio belum ada).
/// </summary>
public static class GameSettings
{
    public const int MaxLevel = 6;

    private const string SoundKey = "settings.sound";
    private const string MusicKey = "settings.music";

    public static event System.Action OnChanged;

    public static int SoundLevel
    {
        get => PlayerPrefs.GetInt(SoundKey, MaxLevel);
        set
        {
            PlayerPrefs.SetInt(SoundKey, Mathf.Clamp(value, 0, MaxLevel));
            PlayerPrefs.Save();
            Apply();
        }
    }

    public static int MusicLevel
    {
        get => PlayerPrefs.GetInt(MusicKey, MaxLevel);
        set
        {
            PlayerPrefs.SetInt(MusicKey, Mathf.Clamp(value, 0, MaxLevel));
            PlayerPrefs.Save();
            Apply();
        }
    }

    /// <summary>0..1 untuk efek suara.</summary>
    public static float SoundVolume => SoundLevel / (float)MaxLevel;

    /// <summary>0..1 untuk musik (dipakai AudioSource musik nanti).</summary>
    public static float MusicVolume => MusicLevel / (float)MaxLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnStart()
    {
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = SoundVolume;
        OnChanged?.Invoke();
    }
}
