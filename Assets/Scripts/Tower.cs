using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Tower Identity")]
    [SerializeField] private string towerName = "DummyTower";
    [SerializeField] private int towerLevel = 1;

    [Header("Combat Stats")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 1f;

    private GridCell occupiedCell;

    public string TowerName => towerName;
    public int TowerLevel => towerLevel;
    public float AttackRange => attackRange;
    public float Damage => damage;
    public GridCell OccupiedCell => occupiedCell;

    public void Setup(GridCell newOccupiedCell)
    {
        occupiedCell = newOccupiedCell;

        if (occupiedCell == null)
        {
            Debug.LogError(
                $"{gameObject.name} dibuat tanpa GridCell."
            );

            return;
        }

        gameObject.name =
            $"{towerName}_R{occupiedCell.Row}_C{occupiedCell.Column}";

        Debug.Log(
            $"{towerName} ditempatkan pada " +
            $"Row {occupiedCell.Row}, " +
            $"Column {occupiedCell.Column}."
        );
    }
}