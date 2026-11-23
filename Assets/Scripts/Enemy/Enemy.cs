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

        Vector3 startPos = gridManager.GetCellWorldPosition(pathCoords[0].x, pathCoords[0].y);
        transform.position = startPos + Vector3.up * heightOffset;
        targetIndex = 1;
    }

    private void Update()
    {
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

        if (IsBlockedByBarrier(targetCell, out Tower barrierTower))
        {
            AttackTower(barrierTower);
            return;
        }

        if (targetCell.Type == TileType.Wall)
        {
            AttackWall();
            return;
        }

        attackTimer = 0f;
        MoveToward(targetCell);
    }

    private void ApplyData(EnemyData enemyData)
    {
        data = enemyData;
        maxHP = data.maxHP;
        attackDamage = data.attackDamage;
        attackCooldown = data.attackCooldown;
        moveSpeed = data.MoveSpeed;

        if (data.placeholderScale != Vector3.zero)
        {
            transform.localScale = data.placeholderScale;
            // Kubus placeholder: alasnya pas di atas permukaan tile (tile tebal 0.15, pusat di y=0)
            heightOffset = 0.075f + data.placeholderScale.y * 0.5f;
        }

        // Warna placeholder supaya tiap jenis musuh bisa dibedakan sebelum sprite final masuk
        Renderer meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", data.placeholderColor);
            block.SetColor("_Color", data.placeholderColor);
            meshRenderer.SetPropertyBlock(block);
        }
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

    private void MoveToward(GridCell targetCell)
    {
        Vector3 destination = targetCell.transform.position + Vector3.up * heightOffset;
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime
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
        Debug.Log($"{gameObject.name} menyerang {tower.TowerName} ({attackDamage} dmg). Sisa HP Tower: {tower.CurrentHP}");
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
        Debug.Log($"{gameObject.name} terkena {amount} dmg. Sisa HP: {currentHP}");

        if (currentHP <= 0f)
        {
            Debug.Log($"{gameObject.name} mati!");
            Destroy(gameObject);
        }
    }
}