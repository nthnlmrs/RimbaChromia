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
        SheetCache.Clear();
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
            stopTimer = Mathf.Max(data.stopDuration, AttackAnimLength);
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

        if (towerCount > 0) FaceTowards(towerBuffer[0].transform.position);

        List<LaunchOrder> orders = new List<LaunchOrder>();
        for (int i = 0; i < towerCount; i++)
        {
            Tower target = towerBuffer[i];
            orders.Add(new LaunchOrder { cell = target.OccupiedCell, tower = target });
        }

        // Rumput yang belum terbakar diutamakan, baru yang sudah terbakar
        int launched = PickRandom(orders, grassBuffer, grassCount);
        launched += PickRandom(orders, burningGrassBuffer, grassCount - launched);

        if (orders.Count == 0) return false;

        // Animasi serang diputar dulu, proyektil keluar di tengah animasi (saat api / lemparan muncul)
        PlayAttackAnimation();
        StartCoroutine(LaunchAfter(AttackAnimLength * 0.55f, orders));
        return true;
    }

    private struct LaunchOrder
    {
        public GridCell cell;
        public Tower tower;
    }

    private System.Collections.IEnumerator LaunchAfter(float delay, List<LaunchOrder> orders)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (IsDead) yield break;

        Vector3 launch = transform.position + Vector3.up * (VisualHeight * 0.6f);
        AudioManager.EnemyAttack(data != null ? data.attackSound : null);
        foreach (LaunchOrder order in orders)
        {
            LaunchAt(launch, order.cell, order.tower);
        }
    }

    private static int PickRandom(List<LaunchOrder> orders, List<GridCell> pool, int count)
    {
        int picked = 0;
        while (picked < count && pool.Count > 0)
        {
            int pick = Random.Range(0, pool.Count);
            orders.Add(new LaunchOrder { cell = pool[pick] });
            pool.RemoveAt(pick);
            picked++;
        }
        return picked;
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
                    AudioManager.Explosion();
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

        walkFrames = data.HasAnimation ? GetFrames(data.walkSheet, data) : null;
        attackFrames = data.attackSheet != null ? GetFrames(data.attackSheet, data) : null;
        animTime = 0f;
        attackAnimTimer = 0f;
        shownFrame = null;

        Sprite sprite = walkFrames != null && walkFrames.Length > 0 ? walkFrames[0] : data.sprite;
        if (sprite == null)
        {
            // Belum ada art: placeholder pixel 2D seukuran placeholderScale lama
            Vector3 ps = data.placeholderScale == Vector3.zero ? Vector3.one * 0.6f : data.placeholderScale;
            int w = Mathf.Clamp(Mathf.RoundToInt(48f * ps.x), 16, 96);
            sprite = SpriteBillboard.Placeholder(data.placeholderColor, w, Mathf.RoundToInt(w * 1.15f));
        }

        if (billboard == null) billboard = SpriteBillboard.Create(transform, sprite, true, SpriteBillboard.EnemyGroundOffset, 0.6f);
        else billboard.SetSprite(sprite);
        shownFrame = sprite;

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
        movedThisFrame = true;
        AudioManager.EnemyStep(); // dibatasi global, jadi tidak menumpuk walau musuh banyak
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
        FaceTowards(tower.transform.position);
        AnticipateMeleeAttack();
        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0f;
        AudioManager.EnemyAttack(data != null ? data.attackSound : null);
        tower.TakeDamage(attackDamage);
    }

    private void AttackWall()
    {
        attackTimer += Time.deltaTime;
        AnticipateMeleeAttack();
        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0f;
        AudioManager.EnemyAttack(data != null ? data.attackSound : null);

        if (wallHealth != null)
        {
            wallHealth.TakeDamage(attackDamage);
        }
    }

    // ------------------------------------------------------------------
    // Animasi spritesheet
    // ------------------------------------------------------------------

    private static readonly Dictionary<Texture2D, Sprite[]> SheetCache = new Dictionary<Texture2D, Sprite[]>();

    private Sprite[] walkFrames;
    private Sprite[] attackFrames;
    private Sprite shownFrame;
    private float animTime;
    private float attackAnimTimer;
    private bool movedThisFrame;

    private float AttackAnimLength =>
        attackFrames != null && attackFrames.Length > 0 && data != null ? attackFrames.Length / Mathf.Max(1f, data.attackFps) : 0f;

    /// <summary>Potong spritesheet jadi frame persegi (tinggi sheet = lebar frame). Hasil di-cache per tekstur.</summary>
    private static Sprite[] GetFrames(Texture2D sheet, EnemyData d)
    {
        if (sheet == null) return null;
        if (SheetCache.TryGetValue(sheet, out Sprite[] cached) && cached != null && cached.Length > 0 && cached[0] != null) return cached;

        int size = sheet.height;
        int count = Mathf.Max(1, sheet.width / size);
        int crop = Mathf.Clamp(d.sheetCropTop, 0, size - 1);
        Vector2 pivot = new Vector2(d.sheetPivotX, 0f);
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            Rect rect = new Rect(i * size, 0f, size, size - crop);
            frames[i] = Sprite.Create(sheet, rect, pivot, 64f, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{sheet.name}_{i}";
        }
        SheetCache[sheet] = frames;
        return frames;
    }

    private void PlayAttackAnimation()
    {
        if (AttackAnimLength <= 0f) return;
        attackAnimTimer = AttackAnimLength;
    }

    /// <summary>Serangan jarak dekat: animasi dimulai lebih awal supaya pukulan pas di akhir animasi.</summary>
    private void AnticipateMeleeAttack()
    {
        float length = AttackAnimLength;
        if (length <= 0f || attackAnimTimer > 0f) return;
        if (attackTimer >= attackCooldown - length * 0.7f) PlayAttackAnimation();
    }

    private void LateUpdate()
    {
        bool moved = movedThisFrame;
        movedThisFrame = false;
        if (billboard == null || data == null) return;

        Sprite frame = null;
        if (attackAnimTimer > 0f && attackFrames != null && attackFrames.Length > 0)
        {
            float length = AttackAnimLength;
            attackAnimTimer -= Time.deltaTime;
            int index = Mathf.Clamp(Mathf.FloorToInt((1f - Mathf.Max(0f, attackAnimTimer) / length) * attackFrames.Length), 0, attackFrames.Length - 1);
            frame = attackFrames[index];
        }
        else if (walkFrames != null && walkFrames.Length > 0)
        {
            // Jalan: putar walk cycle (ikut melambat saat kena slow). Diam: frame pertama.
            if (moved)
            {
                float speedFactor = moveSpeed > 0f ? CurrentMoveSpeed() / moveSpeed : 1f;
                animTime += Time.deltaTime * data.walkFps * speedFactor;
                frame = walkFrames[Mathf.FloorToInt(animTime) % walkFrames.Length];
            }
            else
            {
                frame = walkFrames[0];
            }
        }

        if (frame != null && frame != shownFrame)
        {
            shownFrame = frame;
            billboard.SetSprite(frame);
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
            AudioManager.EnemyDeath();
            PixelVfx.Burst(transform.position + Vector3.up * (VisualHeight * 0.5f), PixelArt.BurstStyle.Smoke, Color.gray, 1.2f);
            Destroy(gameObject);
        }
    }
}