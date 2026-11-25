using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    // Domain Reload dimatikan di project ini -> bersihkan list static setiap kali Play ditekan
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        ActiveEnemies.Clear();
    }

    [Header("Ember Bot Stats")]
    [SerializeField] private float maxHP = 70f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float heightOffset = 0.5f;

    private float currentHP;
    private float attackTimer;

    // Serangan jarak jauh (Walking Furnace, Fire Spitter, Pyro Shooter, Flame Director)
    private float rangedTimer;
    private float stopTimer;
    public bool IsAttacking => stopTimer > 0f;

    // Slow dari Orange trap T2/T3. Tidak ditumpuk: yang paling kuat yang berlaku.
    private float slowPercent;
    private float slowTimer;

    public bool IsSlowed => slowTimer > 0f;

    public void ApplySlow(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f) return;

        if (!IsSlowed || percent >= slowPercent)
        {
            slowPercent = Mathf.Clamp01(percent);
        }
        slowTimer = Mathf.Max(slowTimer, duration);
        ApplyTint(); // tanda visual: musuh jadi pucat kebiruan selama diperlambat
    }

    private Color baseColor = new Color(0f, 0.75f, 1f);
    private SpriteBillboard billboard;

    /// <summary>Tinggi visual musuh di dunia (untuk posisi health bar & titik lempar).</summary>
    public float VisualHeight => billboard != null ? billboard.VisualHeight : transform.localScale.y;
    private static readonly Color SlowTint = new Color(0.75f, 0.9f, 1f);

    private GridManager gridManager;
    private WallHealth wallHealth;
    private List<Vector2Int> pathCoords;
    private int targetIndex;

    private EnemyData data;

    public EnemyData Data => data;
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public bool IsDead => currentHP <= 0f;
    public int PathProgress => targetIndex;

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    public void Setup(GridManager manager, List<Vector2Int> newPath, WallHealth wall, EnemyData enemyData = null)
    {
        if (enemyData != null)
        {
            ApplyData(enemyData);
        }

        gridManager = manager;
        pathCoords = newPath;
        wallHealth = wall;
        currentHP = maxHP;
        attackTimer = 0f;

        if (gridManager == null || pathCoords == null || pathCoords.Count == 0)
        {
            Debug.LogError("Enemy gagal di-setup: GridManager atau Path kosong.");
            Destroy(gameObject);
            return;
        }

        GridCell startCell = gridManager.GetCell(pathCoords[0].x, pathCoords[0].y);
        Vector3 startPos = startCell.transform.position;
        transform.position = new Vector3(startPos.x, startCell.SurfaceY + heightOffset, startPos.z);
        targetIndex = 1;
    }

    private void Update()
    {
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                slowTimer = 0f;
                ApplyTint(); // kembali ke warna normal
            }
        }

        if (gridManager == null || pathCoords == null || targetIndex >= pathCoords.Count)
        {
            return;
        }

        if (wallHealth != null && wallHealth.IsDestroyed)
        {
            return;
        }

        Vector2Int targetCoord = pathCoords[targetIndex];
        GridCell targetCell = gridManager.GetCell(targetCoord.x, targetCoord.y);

        if (targetCell == null)
        {
            return;
        }

        if (stopTimer > 0f)
        {
            stopTimer -= Time.deltaTime;
        }

        bool ranged = data != null && data.rangedAttack;

        if (IsBlockedByBarrier(targetCell, out Tower barrierTower))
        {
            // Dihalangi Red: musuh jarak jauh tetap menembak (kecuali Fire Spitter), yang lain memukul Red
            if (ranged && data.canAttackWhileBlocked) TickRangedAttack();
            else AttackTower(barrierTower);
            return;
        }

        if (targetCell.Type == TileType.Wall)
        {
            AttackWall();
            return;
        }

        if (ranged)
        {
            TickRangedAttack();
        }

        // GDD: berhenti berjalan setiap kali melempar / menyembur
        if (stopTimer > 0f)
        {
            return;
        }

        attackTimer = 0f;
        MoveToward(targetCell);
    }

    // ------------------------------------------------------------------
    // Serangan jarak jauh
    // ------------------------------------------------------------------

    private static readonly List<Tower> towerBuffer = new List<Tower>();
    private static readonly List<GridCell> grassBuffer = new List<GridCell>();
    private static readonly List<GridCell> burningGrassBuffer = new List<GridCell>();

    private void TickRangedAttack()
    {
        if (rangedTimer < attackCooldown)
        {
            rangedTimer += Time.deltaTime;
            return;
        }

        // Tidak ada target = tetap siap, langsung menyerang begitu ada target
        if (TryRangedAttack())
        {
            rangedTimer = 0f;
            stopTimer = data.stopDuration;
        }
    }

    /// <summary>Tile yang terakhir diinjak musuh (pusat jangkauan serangan).</summary>
    private Vector2Int CurrentCoord()
    {
        return pathCoords[Mathf.Clamp(targetIndex - 1, 0, pathCoords.Count - 1)];
    }

    private bool TryRangedAttack()
    {
        Vector2Int center = CurrentCoord();
        int radius = Mathf.Max(1, data.attackRadius);

        towerBuffer.Clear();
        grassBuffer.Clear();
        burningGrassBuffer.Clear();

        for (int r = -radius; r <= radius; r++)
        {
            for (int c = -radius; c <= radius; c++)
            {
                GridCell cell = gridManager.GetCell(center.x + r, center.y + c);
                if (cell == null) continue;

                if (cell.OccupyingTower != null) towerBuffer.Add(cell.OccupyingTower);

                if (cell.IsFlammable)
                {
                    if (cell.IsOnFire) burningGrassBuffer.Add(cell);
                    else grassBuffer.Add(cell);
                }
            }
        }

        // Tower: Red (Barrier) diutamakan (GDD: "tends to be targeted by enemies"), lalu yang terdekat
        Vector3 me = transform.position;
        towerBuffer.Sort((a, b) =>
        {
            bool aRed = a.Data != null && a.Data.role == TowerRole.Barrier;
            bool bRed = b.Data != null && b.Data.role == TowerRole.Barrier;
            if (aRed != bRed) return aRed ? -1 : 1;
            return (a.transform.position - me).sqrMagnitude.CompareTo((b.transform.position - me).sqrMagnitude);
        });

        int towerCount = Mathf.Min(data.towersPerAttack, towerBuffer.Count);
        int grassCount = data.grassTilesPerAttack;
        if (data.grassOnlyIfNoTower && towerCount > 0) grassCount = 0;

        if (towerCount == 0 && grassCount == 0) return false;

        Vector3 launch = transform.position + Vector3.up * (VisualHeight * 0.6f);
        if (towerCount > 0) FaceTowards(towerBuffer[0].transform.position);

        for (int i = 0; i < towerCount; i++)
        {
            Tower target = towerBuffer[i];
            GridCell targetCell = target.OccupiedCell;
            LaunchAt(launch, targetCell, target);
        }

        // Rumput yang belum terbakar diutamakan, baru yang sudah terbakar
        int launched = LaunchAtRandom(launch, grassBuffer, grassCount);
        launched += LaunchAtRandom(launch, burningGrassBuffer, grassCount - launched);

        return towerCount > 0 || launched > 0;
    }

    private int LaunchAtRandom(Vector3 launch, List<GridCell> pool, int count)
    {
        int launched = 0;
        while (launched < count && pool.Count > 0)
        {
            int pick = Random.Range(0, pool.Count);
            GridCell cell = pool[pick];
            pool.RemoveAt(pick);
            LaunchAt(launch, cell, null);
            launched++;
        }
        return launched;
    }

    private void LaunchAt(Vector3 launch, GridCell cell, Tower towerTarget)
    {
        if (cell == null) return;

        float damage = attackDamage;
        EnemyData d = data;
        Vector3 landing = cell.transform.position + Vector3.up * 0.2f;

        TowerVfx.Lob(launch, landing, d.projectileColor, d.projectileFlightTime, () =>
        {
            // Tower kena damage kalau masih berdiri di tile itu
            if (towerTarget != null && towerTarget.OccupiedCell == cell)
            {
                towerTarget.TakeDamage(damage);
            }

            FireManager fire = FireManager.Instance;
            if (fire != null)
            {
                bool igniteCenter = towerTarget == null || d.igniteTowerTile;
                if (igniteCenter) fire.Ignite(cell);

                // Ledakan Flame Director: api menyebar di area, tower sekitar tidak terluka
                if (d.explosionArea != AreaPattern.None && d.explosionArea != AreaPattern.Single && gridManager != null)
                {
                    foreach (GridCell near in gridManager.GetCellsInArea(cell.Row, cell.Column, d.explosionArea))
                    {
                        fire.Ignite(near);
                    }
                }
            }

            float size = d.explosionArea == AreaPattern.Plus5 || d.explosionArea == AreaPattern.Square9 ? 3f : 1.1f;
            TowerVfx.Pulse(cell.transform.position, size, d.projectileColor, 0.45f);
        });
    }

    private void ApplyData(EnemyData enemyData)
    {
        data = enemyData;
        maxHP = data.maxHP;
        attackDamage = data.attackDamage;
        attackCooldown = data.attackCooldown;
        moveSpeed = data.MoveSpeed;

        // GDD 2.5D: musuh = sprite 2D. Kubus placeholder disembunyikan (collider tetap ada).
        foreach (MeshRenderer cube in GetComponentsInChildren<MeshRenderer>())
        {
            cube.enabled = false;
        }

        // Skala root selalu seragam; ukuran diatur lewat spriteScale (jangan non-uniform -> pixel art gepeng)
        float scale = Mathf.Max(0.25f, data.spriteScale);
        transform.localScale = Vector3.one * scale;
        heightOffset = 0f; // pivot sprite di kaki -> berdiri tepat di permukaan tile

        Sprite sprite = data.sprite;
        if (sprite == null)
        {
            // Belum ada art: placeholder pixel 2D seukuran placeholderScale lama
            Vector3 ps = data.placeholderScale == Vector3.zero ? Vector3.one * 0.6f : data.placeholderScale;
            int w = Mathf.Clamp(Mathf.RoundToInt(48f * ps.x), 16, 96);
            sprite = SpriteBillboard.Placeholder(data.placeholderColor, w, Mathf.RoundToInt(w * 1.15f));
        }

        if (billboard == null) billboard = SpriteBillboard.Create(transform, sprite, true, SpriteBillboard.EnemyGroundOffset, 0.6f);
        else billboard.SetSprite(sprite);

        baseColor = Color.white; // sprite sudah berwarna, tint putih = warna asli
        ApplyTint();
    }

    /// <summary>Balik sprite menghadap ke kiri / kanan (arah jalan atau target serangan).</summary>
    private void FaceTowards(Vector3 worldTarget)
    {
        if (billboard == null || data == null) return;
        float dx = worldTarget.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return; // jalan lurus maju/mundur: pertahankan arah terakhir
        billboard.SetFacing(dx > 0f, data.spriteFacesRight);
    }

    private void ApplyTint()
    {
        if (billboard == null) return;

        // Diperlambat Orange trap: sprite jadi kebiruan pucat
        billboard.SetColor(IsSlowed ? SlowTint : baseColor);
    }

    private bool IsBlockedByBarrier(GridCell cell, out Tower barrierTower)
    {
        barrierTower = null;

        if (!cell.IsOccupied || cell.OccupyingTower == null || cell.OccupyingTower.Data == null)
        {
            return false;
        }

        if (cell.OccupyingTower.Data.role == TowerRole.Barrier)
        {
            barrierTower = cell.OccupyingTower;
            return true;
        }

        return false;
    }

    private float CurrentMoveSpeed()
    {
        return IsSlowed ? moveSpeed * (1f - slowPercent) : moveSpeed;
    }

    private void MoveToward(GridCell targetCell)
    {
        Vector3 cellPos = targetCell.transform.position;
        Vector3 destination = new Vector3(cellPos.x, targetCell.SurfaceY + heightOffset, cellPos.z);
        FaceTowards(destination);
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            CurrentMoveSpeed() * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, destination) <= 0.02f)
        {
            transform.position = destination;
            targetIndex++;
        }
    }

    private void AttackTower(Tower tower)
    {
        attackTimer += Time.deltaTime;
        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0f;
        tower.TakeDamage(attackDamage);
    }

    private void AttackWall()
    {
        attackTimer += Time.deltaTime;
        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0f;

        if (wallHealth != null)
        {
            wallHealth.TakeDamage(attackDamage);
        }
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        currentHP = Mathf.Max(0f, currentHP - amount);

        if (currentHP <= 0f)
        {
            Debug.Log($"{gameObject.name} mati!");
            PixelVfx.Burst(transform.position + Vector3.up * (VisualHeight * 0.5f), PixelArt.BurstStyle.Smoke, Color.gray, 1.2f);
            Destroy(gameObject);
        }
    }
}