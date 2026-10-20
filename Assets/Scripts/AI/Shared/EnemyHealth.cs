using System;
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)]
    private float maxHealth = 60;
    public float Current { get; private set; }
    public float Fraction => Current / maxHealth;
    public bool IsDead { get; private set; }

    public event Action<float> Hit;
    public event Action<EnemyHealth> Died;
    private void Awake() => Current = maxHealth;
    public void TakeDamage(float amount)
    {
        if (amount <= 0 || IsDead || !isActiveAndEnabled)
            return;
        Current = Mathf.Max(0, Current - amount);
        if (Current > 0)
            Hit?.Invoke(amount);
        else
        {
            IsDead = true;
            Died?.Invoke(this);
        }
    }
}
