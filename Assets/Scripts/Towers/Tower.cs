using System;
using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Runtime Data")]
    [SerializeField] private TowerData data;
    [SerializeField] private float currentHP;

    private GridCell occupiedCell;
    private MeshRenderer meshRenderer;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propBlock;
    private float cellSize = 1f;
    private float actionTimer;

    public TowerData Data => data;
    public string TowerName => data != null ? data.towerName : "UnknownTower";
    public int TowerTier => data != null ? data.tier : 1;
    public float CurrentHP => currentHP;
    public float MaxHP => data != null ? data.maxHP : 0f;
    public float Damage => data != null ? data.attackDamage : 0f;
    public float ActionCooldown => data != null ? data.actionCooldown : 1f;
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

    private void Update()
    {
        if (data == null || occupiedCell == null || currentHP <= 0f)
        {
            return;
        }

        // Khusus Blue Tower (Single-target Attacker)
        if (data.role == TowerRole.Attacker && data.color == TowerColor.Blue)
        {
            HandleBlueAttack();
        }
        else if (data.role == TowerRole.Healer)
        {
            HandleHealerBeam();
            HandleHealAura();
        }

        UpdateBeamVisual();
    }

    // ------------------------------------------------------------------
    // Green Healer
    // ------------------------------------------------------------------

    private void HandleHealerBeam()
    {
        FireManager fire = FireManager.Instance;
        if (fire == null || data.fireReducePerShot <= 0)
        {
            return;
        }

        actionTimer += Time.deltaTime;
        if (actionTimer < ActionCooldown)
        {
            return;
        }

        // GDD: Shoot range global -> boleh target api di mana saja
        GridCell target = fire.FindPriorityFireTarget(transform.position);
        if (target == null)
        {
            // Tetap "siap tembak" supaya langsung menembak begitu ada api baru
            actionTimer = ActionCooldown;
            return;
        }

        actionTimer = 0f;
        fire.ReduceFire(target, data.fireReducePerShot);
        ShowBeam(target.transform.position + Vector3.up * 0.1f);
    }

    private void HandleHealAura()
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

        float amount = data.auraHealPerSecond * Time.deltaTime;
        foreach (GridCell cell in grid.GetCellsInArea(occupiedCell.Row, occupiedCell.Column, data.auraArea))
        {
            // Wall tidak bisa di-heal (GDD), dan Wall bukan Tower jadi otomatis tidak terkena
            if (cell.IsOccupied && cell.OccupyingTower != null)
            {
                cell.OccupyingTower.Heal(amount);
            }
        }
    }

    // Beam placeholder pakai LineRenderer supaya kelihatan di Game view (Debug.DrawLine cuma di Scene view)
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

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                beam.material = new Material(shader);
            }

            Color green = new Color(0.45f, 1f, 0.55f, 1f);
            beam.startColor = green;
            beam.endColor = new Color(0.8f, 1f, 0.9f, 0.6f);
        }

        beam.SetPosition(0, transform.position + Vector3.up * 0.4f);
        beam.SetPosition(1, targetPosition);
        beam.enabled = true;
        beamTimer = BeamDuration;
    }

    private void UpdateBeamVisual()
    {
        if (beam == null || !beam.enabled)
        {
            return;
        }

        beamTimer -= Time.deltaTime;
        if (beamTimer <= 0f)
        {
            beam.enabled = false;
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

        Debug.Log($"{data.towerName} (Tier {data.tier}) ditempatkan di Row {occupiedCell.Row}, Col {occupiedCell.Column}.");
    }

    private void HandleBlueAttack()
    {
        actionTimer += Time.deltaTime;
        if (actionTimer < ActionCooldown)
        {
            return;
        }

        Enemy target = FindTargetInRange();
        if (target == null)
        {
            return;
        }

        actionTimer = 0f;
        Debug.DrawLine(transform.position, target.transform.position, Color.cyan, 0.25f);
        target.TakeDamage(Damage);
    }

    private Enemy FindTargetInRange()
    {
        Enemy bestTarget = null;
        int highestProgress = -1;

        for (int i = Enemy.ActiveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = Enemy.ActiveEnemies[i];
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            if (!IsEnemyInRange(enemy))
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

    private bool IsEnemyInRange(Enemy enemy)
    {
        Vector3 offset = enemy.transform.position - occupiedCell.transform.position;
        int colDiff = Mathf.RoundToInt(offset.x / cellSize);
        int rowDiff = Mathf.RoundToInt(offset.z / cellSize);

        // Sumber area yang sama dengan preview penempatan (TowerData.GetRangeOffsets)
        Vector2Int[] offsets = data.GetRangeOffsets();
        for (int i = 0; i < offsets.Length; i++)
        {
            if (offsets[i].x == rowDiff && offsets[i].y == colDiff)
            {
                return true;
            }
        }
        return false;
    }

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

        ApplyTowerData(data.nextTier, restoreFullHP: true);
        TriggerMergeEffect();
        return true;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHP <= 0f)
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
        if (data == null || amount <= 0f || currentHP <= 0f)
        {
            return;
        }

        currentHP = Mathf.Min(currentHP + amount, data.maxHP);
        OnHealthChanged?.Invoke(currentHP, MaxHP);
    }

    private void ApplyTowerData(TowerData newData, bool restoreFullHP)
    {
        data = newData;
        currentHP = restoreFullHP ? data.maxHP : Mathf.Min(currentHP, data.maxHP);

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
            spriteRenderer.color = Color.white; // Warna asli gambar, tidak ditimpa tint

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
        }
        else
        {
            // Otomatis warnai dummy 3D berdasarkan dropdown Color di TowerData
            Color autoColor = GetColorFromTowerColor(data.color);
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", autoColor); // Untuk URP Lit / Unlit
            propBlock.SetColor("_Color", autoColor);     // Fallback shader standar
            meshRenderer.SetPropertyBlock(propBlock);
        }
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

    private void TriggerMergeEffect()
    {
        Debug.Log($"Merge berhasil! Naik ke {data.towerName} (Tier {data.tier}), HP penuh: {currentHP}/{MaxHP}.");
    }

    private void Die()
    {
        if (occupiedCell != null)
        {
            occupiedCell.RemoveTower();
        }

        OnTowerDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}