using System;
using UnityEngine;

public class InsightManager : MonoBehaviour
{
    [Header("Insight Settings")]
    [SerializeField] private int initialInsight = 15;
    [SerializeField] private int maxInsight = 100;
    [SerializeField] private float passiveRegenInterval = 1f;
    [SerializeField] private int passiveRegenAmount = 1;
    [SerializeField] private bool isFreeCostMode = false;

    public static InsightManager Instance { get; private set; }

    private int currentInsight;
    private float regenTimer;

    private void Awake()
    {
        Instance = this;
        currentInsight = Mathf.Clamp(initialInsight, 0, maxInsight);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int CurrentInsight => currentInsight;
    public int MaxInsight => maxInsight;
    public bool IsFreeCostMode => isFreeCostMode;

    // UI dan Tower Card nanti cukup subscribe ke event ini
    public event Action<int, int> OnInsightChanged;

    private void Start()
    {
        currentInsight = Mathf.Clamp(initialInsight, 0, maxInsight);
        OnInsightChanged?.Invoke(currentInsight, maxInsight);
    }

    private void Update()
    {
        if (currentInsight >= maxInsight)
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer >= passiveRegenInterval)
        {
            regenTimer -= passiveRegenInterval;
            AddInsight(passiveRegenAmount);
        }
    }

    /// <summary>Dipakai stage Tutorial (GDD: pasang & merge tower tanpa biaya).</summary>
    public void SetFreeCostMode(bool free)
    {
        isFreeCostMode = free;
        OnInsightChanged?.Invoke(currentInsight, maxInsight);
    }

    public bool HasEnoughInsight(int cost)
    {
        if (isFreeCostMode) return true;
        return currentInsight >= cost;
    }

    public bool TrySpendInsight(int cost)
    {
        if (isFreeCostMode) return true;
        if (currentInsight < cost) return false;

        currentInsight -= cost;
        OnInsightChanged?.Invoke(currentInsight, maxInsight);
        return true;
    }

    public void AddInsight(int amount)
    {
        if (amount <= 0) return;

        int previous = currentInsight;
        currentInsight = Mathf.Clamp(currentInsight + amount, 0, maxInsight);

        if (currentInsight != previous)
        {
            OnInsightChanged?.Invoke(currentInsight, maxInsight);
        }
    }
}