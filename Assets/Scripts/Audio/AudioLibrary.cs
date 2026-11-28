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
    [Tooltip("Volume musik main menu (juga dipakai di cerita pembuka sebelum ledakan)")]
    [Range(0f, 1f)] public float menuMusicVolume = 1f;

    [Header("Ambience (diputar berulang di bawah musik)")]
    [Tooltip("Suara hutan di gameplay")]
    public AudioClip forestAmbience;
    [Range(0f, 1f)] public float ambienceVolume = 0.2f;
    [Tooltip("Ambience di main menu & selama cerita (lebih kecil dari gameplay)")]
    [Range(0f, 1f)] public float menuAmbienceVolume = 0.08f;

    [Header("UI")]
    public AudioClip buttonClick;
    [Tooltip("Memilih kartu tower di bar bawah (klik / mulai drag)")]
    public AudioClip cardSelect;
    [Tooltip("Kursor menyorot kartu tower / tower terpasang")]
    public AudioClip towerHover;
    [Tooltip("Tower tidak bisa dipasang / merge ditolak")]
    public AudioClip invalidPlace;
    [Tooltip("Teks dialog diketik huruf per huruf (diputar berulang selama mengetik)")]
    public AudioClip typing;
    [Tooltip("Hitung mundur 3 detik terakhir sebelum wave: [0] = '3', [1] = '2', [2] = '1'")]
    public AudioClip[] countdown = new AudioClip[3];

    [Header("Tower")]
    public AudioClip towerPlaced;
    public AudioClip towerMerge;
    [Tooltip("Blue menembak")]
    public AudioClip blueShoot;
    [Tooltip("Peluru air Blue mengenai musuh")]
    public AudioClip blueSplash;
    [Tooltip("Ledakan: trap Orange & ledakan Flame Director")]
    public AudioClip explosion;
    [Tooltip("Purple menyerang (AoE)")]
    public AudioClip purpleAttack;
    [Tooltip("Red jadi kebal beberapa detik setelah merge")]
    public AudioClip shieldUp;
    [Tooltip("Tower kehabisan HP dan lenyap")]
    public AudioClip towerVanish;

    [Header("Musuh (suara serangan per musuh diatur di EnemyData > Attack Sound)")]
    [Tooltip("Langkah kaki musuh (dibatasi supaya tidak bising saat musuh banyak)")]
    public AudioClip enemyStep;
    public AudioClip enemyDeath;

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
    [Range(0f, 1f)] public float enemyVolume = 0.55f;
    [Range(0f, 1f)] public float stepVolume = 0.25f;
    [Range(0f, 1f)] public float typingVolume = 0.6f;
    [Range(0f, 1f)] public float jingleVolume = 0.9f;
}
