using System.Collections.Generic;
using UnityEngine;

public class TestEnemySpawner : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WallHealth wallHealth;
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxSpawnCount = 5;

    private float timer;
    private int spawnedCount;
    private List<Vector2Int> testPath;

    private void Start()
    {
        // Jalur lurus di Row 8 dari Column 0 sampai Column 20 (Wall)
        testPath = new List<Vector2Int>();
        for (int col = 0; col <= 20; col++)
        {
            testPath.Add(new Vector2Int(8, col));
        }

        timer = spawnInterval;
    }

    private void Update()
    {
        if (gridManager == null || enemyPrefab == null || spawnedCount >= maxSpawnCount)
        {
            return;
        }

        if (wallHealth != null && wallHealth.IsDestroyed)
        {
            return;
        }

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        Enemy newEnemy = Instantiate(enemyPrefab);
        newEnemy.name = $"EmberBot_{spawnedCount + 1}";
        newEnemy.Setup(gridManager, testPath, wallHealth);
        spawnedCount++;
    }
}