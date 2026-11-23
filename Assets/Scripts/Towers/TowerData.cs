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

    [Header("Upgrade / Merge")]
    public TowerData nextTier; // Kosongkan jika sudah Tier 3

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