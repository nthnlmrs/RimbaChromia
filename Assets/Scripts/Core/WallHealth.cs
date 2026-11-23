using System;
using UnityEngine;

public class WallHealth : MonoBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private bool pauseOnGameOver = true;

    private float currentHP;
    private bool isDestroyed;

    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public bool IsDestroyed => isDestroyed;

    // Untuk UI Health Bar & layar Game Over nanti
    public event Action<float, float> OnHealthChanged;
    public event Action OnWallDestroyed;

    private void Start()
    {
        currentHP = maxHP;
        isDestroyed = false;
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    // Sesuai GDD: The Wall tidak punya fungsi Heal (tidak bisa di-heal tower)
    public void TakeDamage(float amount)
    {
        if (isDestroyed || amount <= 0f)
        {
            return;
        }

        int previousWholeHP = Mathf.CeilToInt(currentHP);
        currentHP = Mathf.Max(0f, currentHP - amount);
        OnHealthChanged?.Invoke(currentHP, maxHP);

        // Damage api masuk tiap frame (pecahan kecil), jadi log hanya saat angka HP bulat berubah
        if (Mathf.CeilToInt(currentHP) != previousWholeHP)
        {
            Debug.Log($"The Wall terkena damage! Sisa HP Wall: {Mathf.CeilToInt(currentHP)}/{maxHP}");
        }

        if (currentHP <= 0f)
        {
            HandleWallDestroyed();
        }
    }

    private void HandleWallDestroyed()
    {
        isDestroyed = true;
        Debug.LogWarning("GAME OVER! The Wall telah hancur!");
        OnWallDestroyed?.Invoke();

        if (pauseOnGameOver)
        {
            Time.timeScale = 0f;
        }
    }
}