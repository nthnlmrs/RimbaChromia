using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Perilaku 6 warna tower sesuai GDD:
///  Red    (Barrier)   : menghalangi jalan musuh. Merge: kebal damage 2 / 5 detik.
///  Orange (Trap)      : meledak (AoE) saat musuh menginjak tile-nya. T2/T3: memperlambat musuh.
///  Yellow (Insight)   : +1 insight tiap 3 / 2 / 1 detik. Merge: langsung +3 / +7 insight.
///  Green  (Healer)    : beam global memadamkan api 1 / 2 / 3 level. T2/T3: aura heal.
///  Blue   (Attacker)  : peluru ke 1 musuh tiap detik. T2/T3: +10 / +20 damage.
///  Purple (Attacker)  : serangan area tiap 3 detik. T2/T3: buff +ATK & +HP tower sekitar.
/// "Merge effect" di GDD = kemampuan tier 2/3 (tier itu hanya bisa didapat lewat merge).
/// </summary>
public class Tower : MonoBehaviour
{
    /// <summary>Semua tower yang sedang terpasang (dipakai health bar UI).</summary>
    public static readonly List<Tower> ActiveTowers = new List<Tower>();

    // Domain Reload dimatikan di project ini -> bersihkan list static setiap kali Play ditekan
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnPlay()
    {
        ActiveTowers.Clear();
    }

    private void OnEnable()
    {
        if (!ActiveTowers.Contains(this)) ActiveTowers.Add(this);
    }

    private void OnDisable()
    {
        ActiveTowers.Remove(this);
    }

    [Header("Runtime Data")]
    [SerializeField] private TowerData data;
    [SerializeField] private float currentHP;

    [Header("Runtime Buff (read only)")]
    [SerializeField] private float buffAttackBonus;
    [SerializeField] private float buffHPBonus;

    private GridCell occupiedCell;
    private MeshRenderer meshRenderer;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propBlock;
    private float cellSize = 1f;
    private float actionTimer;
    private float immuneTimer;
    private float buffRefreshTimer;

    private const float BuffRefreshInterval = 0.5f;
    private static readonly List<Enemy> enemyBuffer = new List<Enemy>();

    public TowerData Data => data;
    public string TowerName => data != null ? data.towerName : "UnknownTower";
    public int TowerTier => data != null ? data.tier : 1;
    public float CurrentHP => currentHP;
    public float MaxHP => data != null ? data.maxHP + buffHPBonus : 0f;
    public float Damage => data != null ? data.attackDamage + data.bonusDamagePerAttack + buffAttackBonus : 0f;
    public float ActionCooldown => data != null ? Mathf.Max(0.05f, data.actionCooldown) : 1f;
    public bool IsImmune => immuneTimer > 0f;
    public GridCell OccupiedCell => occupiedCell;

    public event Action<float, float> OnHealthChanged;
    public event Action<Tower> OnTowerDestroyed;

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        propBlock = new MaterialPropertyBlock();

        if (meshRenderer == null && spriteRenderer == null)
        {
            Debug.LogWarning($"Tower pada {gameObject.name} tidak memiliki MeshRenderer maupun SpriteRenderer.");
        }
    }

    public void Setup(GridCell newOccupiedCell, TowerData towerData, float newCellSize = 1f)
    {
        if (newOccupiedCell == null || towerData == null)
        {
            Debug.LogError($"{gameObject.name} dibuat tanpa GridCell atau TowerData.");
            return;
        }

        occupiedCell = newOccupiedCell;
        cellSize = newCellSize > 0f ? newCellSize : 1f;
        actionTimer = 0f;
        ApplyTowerData(towerData, restoreFullHP: true);
        RefreshBuffs();

        TowerVfx.Pulse(transform.position, cellSize * 0.9f, Color.white, 0.3f);
        Debug.Log($"{data.towerName} (Tier {data.tier}) ditempatkan di Row {occupiedCell.Row}, Col {occupiedCell.Column}.");
    }

    private void Update()
    {
        if (data == null || occupiedCell == null || currentHP <= 0f)
        {
            return;
        }

        float dt = Time.deltaTime;

        if (immuneTimer > 0f)
        {
            immuneTimer -= dt;
            if (immuneTimer <= 0f) ApplyVisual(); // kembalikan warna normal
        }

        buffRefreshTimer -= dt;
        if (buffRefreshTimer <= 0f)
        {
            buffRefreshTimer = BuffRefreshInterval;
            RefreshBuffs();
        }

        switch (data.role)
        {
            case TowerRole.Trap:
                HandleTrap(dt);
                break;

            case TowerRole.InsightGenerator:
                HandleInsight(dt);
                break;

            case TowerRole.Healer:
                HandleHealerBeam(dt);
                HandleHealAura(dt);
                break;

            case TowerRole.Attacker:
                if (data.isAreaAttack) HandleAreaAttack(dt);
                else HandleSingleAttack(dt);
                break;

            // Barrier: tidak ada aksi aktif, cukup menghalangi musuh (dicek di Enemy)
        }

        UpdateBeamVisual(dt);
    }

    /// <summary>Naikkan timer; true kalau sudah siap beraksi (timer tidak di-reset di sini).</summary>
    private bool TickReady(float dt)
    {
        if (actionTimer < ActionCooldown)
        {
            actionTimer += dt;
        }
        return actionTimer >= ActionCooldown;
    }

    // ------------------------------------------------------------------
    // Orange - Trap
    // ------------------------------------------------------------------

    private void HandleTrap(float dt)
    {
        if (!TickReady(dt))
        {
            return;
        }

        // Trap baru meledak kalau ada musuh yang benar-benar menginjak tile-nya
        bool stepped = false;
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy != null && !enemy.IsDead && GetGridOffset(enemy) == Vector2Int.zero)
            {
                stepped = true;
                break;
            }
        }

        if (!stepped)
        {
            return;
        }

        actionTimer = 0f;
        CollectEnemiesInArea(GridArea.GetOffsets(data.trapDamageArea), enemyBuffer);

        foreach (Enemy enemy in enemyBuffer)
        {
            enemy.TakeDamage(Damage);
            if (data.slowPercent > 0f)
            {
                enemy.ApplySlow(data.slowPercent, data.slowDuration);
            }
        }

        TowerVfx.Pulse(occupiedCell.transform.position, AreaSize(data.trapDamageArea), TowerVfx.ColorOf(data.color), 0.4f);
    }

    // ------------------------------------------------------------------
    // Yellow - Insight Generator
    // ------------------------------------------------------------------

    private void HandleInsight(float dt)
    {
        if (data.insightPerTick <= 0 || !TickReady(dt))
        {
            return;
        }

        actionTimer = 0f;

        InsightManager insight = InsightManager.Instance;
        if (insight != null)
        {
            insight.AddInsight(data.insightPerTick);

            // Kilatan di atas kepala tower supaya tidak tertutup badan silinder placeholder
            float top = transform.position.y + transform.localScale.y + 0.05f;
            TowerVfx.PulseAtHeight(transform.position, top, cellSize * 1.4f, new Color(1f, 0.95f, 0.3f), 0.5f);
        }
    }

    // ------------------------------------------------------------------
    // Green - Healer
    // ------------------------------------------------------------------

    private void HandleHealerBeam(float dt)
    {
        FireManager fire = FireManager.Instance;
        if (fire == null || data.fireReducePerShot <= 0 || !TickReady(dt))
        {
            return;
        }

        // GDD: Shoot range global -> boleh target api di mana saja
        GridCell target = fire.FindPriorityFireTarget(transform.position);
        if (target == null)
        {
            return; // tetap siap, langsung menembak begitu ada api
        }

        actionTimer = 0f;
        fire.ReduceFire(target, data.fireReducePerShot);
        ShowBeam(target.transform.position + Vector3.up * 0.1f);
    }

    private void HandleHealAura(float dt)
    {
        if (data.auraHealPerSecond <= 0f || data.auraArea == AreaPattern.None)
        {
            return;
        }

        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            return;
        }

        float amount = data.auraHealPerSecond * dt;
        foreach (GridCell cell in grid.GetCellsInArea(occupiedCell.Row, occupiedCell.Column, data.auraArea))
        {
            // Wall tidak bisa di-heal (GDD), dan Wall bukan Tower jadi otomatis tidak terkena
            if (cell.IsOccupied && cell.OccupyingTower != null)
            {
                cell.OccupyingTower.Heal(amount);
            }
        }
    }

    // ------------------------------------------------------------------
    // Blue - Single target
    // ------------------------------------------------------------------

    private void HandleSingleAttack(float dt)
    {
        if (!TickReady(dt))
        {
            return;
        }

        Enemy target = FindTargetInRange();
        if (target == null)
        {
            return;
        }

        actionTimer = 0f;
        TowerVfx.FireProjectile(transform.position + Vector3.up * 0.4f, target, Damage, TowerVfx.ColorOf(data.color));
    }

    private Enemy FindTargetInRange()
    {
        Enemy bestTarget = null;
        int highestProgress = -1;
        Vector2Int[] offsets = data.GetRangeOffsets();

        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDead || !IsOffsetIn(GetGridOffset(enemy), offsets))
            {
                continue;
            }

            // Prioritaskan musuh yang paling dekat dengan The Wall
            if (enemy.PathProgress > highestProgress)
            {
                highestProgress = enemy.PathProgress;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    // ------------------------------------------------------------------
    // Purple - Area attack
    // ------------------------------------------------------------------

    private void HandleAreaAttack(float dt)
    {
        if (!TickReady(dt))
        {
            return;
        }

        CollectEnemiesInArea(data.GetRangeOffsets(), enemyBuffer);
        if (enemyBuffer.Count == 0)
        {
            return; // tunggu sampai ada musuh, jangan buang serangan
        }

        actionTimer = 0f;
        foreach (Enemy enemy in enemyBuffer)
        {
            enemy.TakeDamage(Damage);
        }

        TowerVfx.Pulse(occupiedCell.transform.position, cellSize * 5f, TowerVfx.ColorOf(data.color), 0.45f);
    }

    // ------------------------------------------------------------------
    // Purple buff (diterima tower lain)
    // ------------------------------------------------------------------

    /// <summary>
    /// Cari Purple T2/T3 di sekitar yang area buff-nya mencakup tower ini.
    /// Buff dari beberapa Purple tidak ditumpuk, diambil yang terbesar.
    /// </summary>
    private void RefreshBuffs()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || occupiedCell == null) return;

        float bestAttack = 0f;
        float bestHP = 0f;

        foreach (GridCell cell in grid.GetCellsInArea(occupiedCell.Row, occupiedCell.Column, AreaPattern.Square25))
        {
            Tower other = cell.OccupyingTower;
            if (other == null || other == this || other.data == null || !other.data.GivesBuff) continue;

            Vector2Int myOffset = new Vector2Int(occupiedCell.Row - cell.Row, occupiedCell.Column - cell.Column);
            if (!IsOffsetIn(myOffset, GridArea.GetOffsets(other.data.buffArea))) continue;

            bestAttack = Mathf.Max(bestAttack, other.data.buffAttack);
            bestHP = Mathf.Max(bestHP, other.data.buffHP);
        }

        buffAttackBonus = bestAttack;

        if (!Mathf.Approximately(bestHP, buffHPBonus))
        {
            float delta = bestHP - buffHPBonus;
            buffHPBonus = bestHP;

            // Buff HP naik -> HP sekarang ikut naik. Buff hilang -> HP dipotong ke maksimal baru.
            currentHP = delta > 0f ? currentHP + delta : Mathf.Min(currentHP, MaxHP);
            OnHealthChanged?.Invoke(currentHP, MaxHP);
        }
    }

    // ------------------------------------------------------------------
    // Helper posisi musuh
    // ------------------------------------------------------------------

    /// <summary>Selisih (row, column) posisi musuh terhadap tile tower ini.</summary>
    private Vector2Int GetGridOffset(Enemy enemy)
    {
        Vector3 offset = enemy.transform.position - occupiedCell.transform.position;
        return new Vector2Int(
            Mathf.RoundToInt(offset.z / cellSize),
            Mathf.RoundToInt(offset.x / cellSize));
    }

    private void CollectEnemiesInArea(Vector2Int[] offsets, List<Enemy> output)
    {
        output.Clear();
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy != null && !enemy.IsDead && IsOffsetIn(GetGridOffset(enemy), offsets))
            {
                output.Add(enemy);
            }
        }
    }

    private static bool IsOffsetIn(Vector2Int offset, Vector2Int[] offsets)
    {
        for (int i = 0; i < offsets.Length; i++)
        {
            if (offsets[i] == offset) return true;
        }
        return false;
    }

    private float AreaSize(AreaPattern pattern)
    {
        switch (pattern)
        {
            case AreaPattern.Square9: return cellSize * 3f;
            case AreaPattern.Diamond13:
            case AreaPattern.Square25: return cellSize * 5f;
            case AreaPattern.Plus5: return cellSize * 3f;
            default: return cellSize;
        }
    }

    // ------------------------------------------------------------------
    // Merge
    // ------------------------------------------------------------------

    public bool CanMergeWith(TowerData incomingData)
    {
        if (data == null || incomingData == null)
        {
            return false;
        }

        return data == incomingData && data.nextTier != null;
    }

    public bool TryMergeUpgrade()
    {
        if (data == null || data.nextTier == null)
        {
            return false;
        }

        ApplyTowerData(data.nextTier, restoreFullHP: true); // GDD: merge memulihkan 100% HP
        TriggerMergeEffect();
        return true;
    }

    /// <summary>
    /// Tower ini "dilebur" ke tower lain saat drag-merge: dihapus tanpa dianggap hancur/mati.
    /// </summary>
    public void ConsumeForMerge()
    {
        if (occupiedCell != null)
        {
            occupiedCell.RemoveTower();
            occupiedCell = null;
        }

        Destroy(gameObject);
    }

    private void TriggerMergeEffect()
    {
        // Red: kebal damage sementara
        if (data.mergeImmunityDuration > 0f)
        {
            immuneTimer = data.mergeImmunityDuration;
            ApplyVisual();
        }

        // Yellow: insight instan
        if (data.mergeInsightBonus > 0 && InsightManager.Instance != null)
        {
            InsightManager.Instance.AddInsight(data.mergeInsightBonus);
        }

        actionTimer = 0f;
        TowerVfx.Pulse(transform.position, cellSize * 1.6f, TowerVfx.ColorOf(data.color), 0.5f);
        Debug.Log($"Merge berhasil! Naik ke {data.towerName} (Tier {data.tier}), HP penuh: {currentHP}/{MaxHP}.");
    }

    // ------------------------------------------------------------------
    // HP
    // ------------------------------------------------------------------

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHP <= 0f || IsImmune)
        {
            return;
        }

        currentHP = Mathf.Max(0f, currentHP - amount);
        OnHealthChanged?.Invoke(currentHP, MaxHP);

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (data == null || amount <= 0f || currentHP <= 0f || currentHP >= MaxHP)
        {
            return;
        }

        currentHP = Mathf.Min(currentHP + amount, MaxHP);
        OnHealthChanged?.Invoke(currentHP, MaxHP);
    }

    private void Die()
    {
        if (occupiedCell != null)
        {
            occupiedCell.RemoveTower();
        }

        TowerVfx.Pulse(transform.position, cellSize, new Color(0.2f, 0.2f, 0.2f), 0.4f);
        OnTowerDestroyed?.Invoke(this);
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------
    // Visual
    // ------------------------------------------------------------------

    private void ApplyTowerData(TowerData newData, bool restoreFullHP)
    {
        data = newData;
        currentHP = restoreFullHP ? MaxHP : Mathf.Min(currentHP, MaxHP);

        ApplyVisual();

        if (data.placeholderScale != Vector3.zero)
        {
            transform.localScale = data.placeholderScale;
        }

        if (occupiedCell != null)
        {
            gameObject.name = $"{data.towerName}_T{data.tier}_R{occupiedCell.Row}_C{occupiedCell.Column}";
        }

        OnHealthChanged?.Invoke(currentHP, MaxHP);
    }

    private void ApplyVisual()
    {
        // 1. Jika sprite final sudah diisi & prefab punya SpriteRenderer -> tampilkan gambar asli
        if (spriteRenderer != null && data.towerSprite != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sprite = data.towerSprite;
            spriteRenderer.color = IsImmune ? new Color(1f, 1f, 1f, 0.7f) : Color.white;

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
            return;
        }

        // 2. Mode Placeholder / Dummy 3D
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.enabled = true;

        if (data.placeholderMaterial != null)
        {
            meshRenderer.sharedMaterial = data.placeholderMaterial;
            meshRenderer.SetPropertyBlock(null);
            return;
        }

        // Otomatis warnai dummy 3D berdasarkan dropdown Color di TowerData.
        // Saat kebal (Red setelah merge) warnanya dibuat lebih terang.
        Color autoColor = GetColorFromTowerColor(data.color);
        if (IsImmune) autoColor = Color.Lerp(autoColor, Color.white, 0.55f);

        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", autoColor); // Untuk URP Lit / Unlit
        propBlock.SetColor("_Color", autoColor);     // Fallback shader standar
        meshRenderer.SetPropertyBlock(propBlock);
    }

    private Color GetColorFromTowerColor(TowerColor towerColor)
    {
        switch (towerColor)
        {
            case TowerColor.Red:    return new Color(0.85f, 0.15f, 0.15f);
            case TowerColor.Orange: return new Color(1.00f, 0.50f, 0.05f);
            case TowerColor.Yellow: return new Color(0.95f, 0.85f, 0.15f);
            case TowerColor.Green:  return new Color(0.20f, 0.80f, 0.30f);
            case TowerColor.Blue:   return new Color(0.15f, 0.50f, 0.95f);
            case TowerColor.Purple: return new Color(0.60f, 0.25f, 0.85f);
            default:                return Color.white;
        }
    }

    // Beam healer placeholder pakai LineRenderer supaya kelihatan di Game view
    private LineRenderer beam;
    private float beamTimer;
    private const float BeamDuration = 0.25f;

    private void ShowBeam(Vector3 targetPosition)
    {
        if (beam == null)
        {
            GameObject beamObject = new GameObject("HealBeam");
            beamObject.transform.SetParent(transform, false);
            beam = beamObject.AddComponent<LineRenderer>();
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.startWidth = 0.12f;
            beam.endWidth = 0.05f;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.sharedMaterial = TowerVfx.UnlitMaterial;

            beam.startColor = new Color(0.45f, 1f, 0.55f, 1f);
            beam.endColor = new Color(0.8f, 1f, 0.9f, 0.6f);
        }

        beam.SetPosition(0, transform.position + Vector3.up * 0.4f);
        beam.SetPosition(1, targetPosition);
        beam.enabled = true;
        beamTimer = BeamDuration;
    }

    private void UpdateBeamVisual(float dt)
    {
        if (beam == null || !beam.enabled)
        {
            return;
        }

        beamTimer -= dt;
        if (beamTimer <= 0f)
        {
            beam.enabled = false;
        }
    }
}
