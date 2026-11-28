using UnityEngine;

/// <summary>
/// Semua musik & efek suara dalam satu aset: Assets/Resources/AudioLibrary.asset.
/// Ganti / tambah suara cukup drag klip baru di Inspector. Klip kosong = tidak bersuara (aman).
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "GIMERSIA/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Musik (diputar berulang)")]
    public AudioClip menuMusic;
    public AudioClip gameplayMusic;
    [Range(0f, 1f)] public float musicVolume = 0.7f;

    [Header("UI")]
    public AudioClip buttonClick;

    [Header("Tower")]
    public AudioClip towerPlaced;
    public AudioClip towerMerge;
    [Tooltip("Blue menembak")]
    public AudioClip blueShoot;
    [Tooltip("Peluru air Blue mengenai musuh")]
    public AudioClip blueSplash;
    [Tooltip("Ledakan: trap Orange & ledakan Flame Director")]
    public AudioClip explosion;

    [Header("Api")]
    [Tooltip("Suara api berulang; volumenya mengikuti jumlah tile yang terbakar")]
    public AudioClip fireIgnite;
    [Tooltip("Green memadamkan / mengecilkan api")]
    public AudioClip fireOut;

    [Header("The Wall & Wave")]
    public AudioClip wallHit;
    public AudioClip waveStart;
    public AudioClip victory;
    public AudioClip defeat;

    [Header("Volume efek suara")]
    [Range(0f, 1f)] public float uiVolume = 0.7f;
    [Range(0f, 1f)] public float towerVolume = 0.8f;
    [Range(0f, 1f)] public float shootVolume = 0.45f;
    [Range(0f, 1f)] public float fireVolume = 0.45f;
    [Range(0f, 1f)] public float jingleVolume = 0.9f;
}
