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

    [Header("Upgrade / Merge")]
    public TowerData nextTier; // Kosongkan jika sudah Tier 3

    [Header("Final Visual (2.5D Sprite)")]
    public Sprite towerSprite; // Isi nanti kalau gambar dari artist sudah jadi

    [Header("Placeholder Visual (Dummy 3D)")]
    public Material placeholderMaterial; // Boleh dibiarkan kosong, warna otomatis ikut dropdown Color
    public Vector3 placeholderScale = Vector3.one;
}