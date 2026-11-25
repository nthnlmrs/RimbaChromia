using UnityEngine;

public enum TowerColor { Red, Orange, Yellow, Green, Blue, Purple }
public enum TowerRole { Barrier, Trap, InsightGenerator, Healer, Attacker }

[CreateAssetMenu(fileName = "NewTowerData", menuName = "GIMERSIA/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    public string towerName;
    public TowerColor color;
    public TowerRole role;
    [Range(1, 3)] public int tier = 1;

    [Header("Placement Rules")]
    public int cost = 10;
    public bool canPlaceOnBarePath = false; // Centang TRUE hanya untuk Red & Orange
    public bool canPlaceOnGrass = true;

    [Header("Combat & Utility Stats")]
    public float maxHP = 100f;
    public float attackDamage = 0f;
    public float actionCooldown = 1f;
    public bool isGlobalRange = false; // Centang TRUE untuk Green (Healer)
    public Vector2Int[] rangeOffsets;  // Koordinat relatif (row, col) dari posisi tower

    [Header("Healer (Green)")]
    [Tooltip("Berapa level api yang dikurangi per tembakan beam (GDD: T1=1, T2=2, T3=3)")]
    public int fireReducePerShot = 0;
    [Tooltip("Heal HP per detik untuk tower di sekitar (GDD: T2=5, T3=10). 0 = tidak ada aura")]
    public float auraHealPerSecond = 0f;
    [Tooltip("Area aura heal (GDD: T2 = 9 grid, T3 = 13 grid)")]
    public AreaPattern auraArea = AreaPattern.None;

    [Header("Barrier (Red)")]
    [Tooltip("GDD merge effect: kebal damage selama X detik setelah merge (T2 = 2, T3 = 5)")]
    public float mergeImmunityDuration = 0f;

    [Header("Trap (Orange)")]
    [Tooltip("Area ledakan saat musuh menginjak trap")]
    public AreaPattern trapDamageArea = AreaPattern.Square9;
    [Tooltip("Perlambatan musuh 0-1 (0.25 = 25% lebih lambat). GDD: T2 '-5 speed', T3 '-15 speed'")]
    [Range(0f, 0.9f)] public float slowPercent = 0f;
    public float slowDuration = 2f;

    [Header("Insight Generator (Yellow)")]
    [Tooltip("Insight per tick. Interval tick = Action Cooldown (GDD: 3 / 2 / 1 detik)")]
    public int insightPerTick = 0;
    [Tooltip("GDD merge effect: langsung dapat insight saat merge (T2 = 3, T3 = 7)")]
    public int mergeInsightBonus = 0;

    [Header("Attacker (Blue / Purple)")]
    [Tooltip("Centang untuk Purple: semua musuh dalam jangkauan terkena (AoE)")]
    public bool isAreaAttack = false;
    [Tooltip("GDD merge effect Blue: tambahan damage per serangan (T2 = 10, T3 = 20)")]
    public float bonusDamagePerAttack = 0f;

    [Header("Support Buff (Purple)")]
    [Tooltip("GDD merge effect Purple: +ATK untuk tower di sekitar (T2 = 5, T3 = 15)")]
    public float buffAttack = 0f;
    [Tooltip("GDD merge effect Purple: +HP maksimal untuk tower di sekitar (T2 = 5, T3 = 15)")]
    public float buffHP = 0f;
    public AreaPattern buffArea = AreaPattern.Square9;

    public bool GivesBuff => buffAttack > 0f || buffHP > 0f;

    [Header("Upgrade / Merge")]
    public TowerData nextTier; // Kosongkan jika sudah Tier 3

    [Header("Arah hadap sprite")]
    [Tooltip("Centang kalau gambar tower menghadap KANAN. Tower penembak (Blue) & Green dibalik menghadap targetnya")]
    public bool spriteFacesRight = false;

    [Header("Final Visual (2.5D Sprite)")]
    public Sprite towerSprite; // Isi nanti kalau gambar dari artist sudah jadi

    [Header("Placeholder Visual (Dummy 3D)")]
    public Material placeholderMaterial; // Boleh dibiarkan kosong, warna otomatis ikut dropdown Color
    public Vector3 placeholderScale = Vector3.one;

    /// <summary>
    /// Area jangkauan tower (offset row, col dari posisi tower).
    /// Dipakai untuk logika serangan DAN preview area saat menaruh tower, supaya selalu sama.
    /// Prioritas: rangeOffsets manual > default per role.
    /// </summary>
    public Vector2Int[] GetRangeOffsets()
    {
        if (rangeOffsets != null && rangeOffsets.Length > 0)
        {
            return rangeOffsets;
        }

        switch (role)
        {
            case TowerRole.Attacker:
                return GridArea.GetOffsets(AreaPattern.Square25); // GDD belum menulis range, default 5x5
            case TowerRole.Trap:
                return GridArea.GetOffsets(trapDamageArea); // area ledakan trap
            case TowerRole.Healer:
                // Beam healer global; yang ditampilkan sebagai area adalah aura heal (T2/T3)
                return auraArea != AreaPattern.None
                    ? GridArea.GetOffsets(auraArea)
                    : GridArea.GetOffsets(AreaPattern.Single);
            default:
                return GridArea.GetOffsets(AreaPattern.Single);
        }
    }
}