using UnityEngine;

/// <summary>Stationary greybox damage receiver for the Phase 3 practice range.</summary>
public sealed class ShootingTarget : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)]
    private float maxHealth = 200f;
    [SerializeField, Min(0.1f)]
    private float resetDelay = 3f;
    [SerializeField]
    private Renderer targetRenderer;
    [SerializeField]
    private bool logHits = true;
    private MaterialPropertyBlock properties;
    private float resetAt = float.PositiveInfinity;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        ResetTarget();
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f || amount <= 0f)
            return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        SetColor(CurrentHealth <= 0f ? Color.red : new Color(0.8f, 0.55f, 0.2f));
        if (logHits)
            Debug.Log($"{name}: {amount:0.##} damage, {CurrentHealth:0.##}/{maxHealth:0.##} HP", this);
        if (CurrentHealth <= 0f)
            resetAt = Time.time + resetDelay;
    }

    private void Update()
    {
        if (Time.time >= resetAt)
            ResetTarget();
    }

    private void ResetTarget()
    {
        CurrentHealth = maxHealth;
        resetAt = float.PositiveInfinity;
        SetColor(new Color(0.6f, 0.65f, 0.7f));
    }

    private void SetColor(Color color)
    {
        if (targetRenderer == null)
            return;
        targetRenderer.GetPropertyBlock(properties);
        properties.SetColor("_Color", color);
        properties.SetColor("_BaseColor", color);
        targetRenderer.SetPropertyBlock(properties);
    }
}
