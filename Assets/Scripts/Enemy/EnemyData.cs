using UnityEngine;

public enum EnemySpeed { Slow, Normal, Fast }

/// <summary>
/// Data statis tiap jenis musuh (sesuai tabel Enemies di GDD).
/// Perilaku khusus (lempar batu api, semburan api, ledakan boss) belum diimplementasi:
/// sementara semua musuh berjalan di jalur & menyerang Barrier / Wall dengan stat masing-masing.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "GIMERSIA/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Ember Bot";

    [Header("Stats (GDD)")]
    public float maxHP = 70f;
    public float attackDamage = 20f;
    public float attackCooldown = 2f;
    public EnemySpeed speed = EnemySpeed.Normal;

    [Header("Serangan Jarak Jauh (kosongkan untuk Ember Bot)")]
    [Tooltip("Aktifkan untuk musuh yang menembak / melempar api. Interval = Attack Cooldown.")]
    public bool rangedAttack = false;
    [Tooltip("Jangkauan serangan dalam tile ke segala arah (2 = area 5x5, 3 = area 7x7)")]
    public int attackRadius = 2;
    [Tooltip("Berapa tower yang diserang sekaligus tiap serangan (Red diutamakan)")]
    public int towersPerAttack = 0;
    [Tooltip("Berapa tile rumput yang disulut api tiap serangan")]
    public int grassTilesPerAttack = 0;
    [Tooltip("Walking Furnace: tembak rumput hanya kalau tidak ada tower dalam jangkauan")]
    public bool grassOnlyIfNoTower = false;
    [Tooltip("Serangan ke tower juga menyulut api di tile tower tsb (kalau rumput)")]
    public bool igniteTowerTile = false;
    [Tooltip("Area api di titik ledakan (Flame Director). Ledakan tidak melukai tower sekitar.")]
    public AreaPattern explosionArea = AreaPattern.Single;
    [Tooltip("Lama berhenti berjalan setiap kali menyerang (detik)")]
    public float stopDuration = 0.6f;
    [Tooltip("Fire Spitter: false = tidak bisa menyembur saat dihalangi Red")]
    public bool canAttackWhileBlocked = true;
    public float projectileFlightTime = 0.7f;
    public Color projectileColor = new Color(1f, 0.45f, 0.1f);

    [Header("Placeholder Visual")]
    public Color placeholderColor = new Color(0f, 0.75f, 1f);
    public Vector3 placeholderScale = Vector3.one;

    public float MoveSpeed
    {
        get
        {
            switch (speed)
            {
                case EnemySpeed.Slow: return 0.9f;
                case EnemySpeed.Fast: return 2.4f;
                default: return 1.5f;
            }
        }
    }
}
