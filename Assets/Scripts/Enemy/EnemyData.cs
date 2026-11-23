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
