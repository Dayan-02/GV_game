using System.Collections.Generic;
using UnityEngine;

/// <summary>Small reusable particle pool. Wave and weapon events provide the effect locations.</summary>
public sealed class CombatEffects : MonoBehaviour
{
    [SerializeField]
    private PlayerWeaponController weapons;
    [SerializeField]
    private WaveManager waves;
    [SerializeField]
    private ParticleSystem impactPrefab;
    [SerializeField]
    private ParticleSystem hitPrefab;
    [SerializeField]
    private ParticleSystem deathPrefab;
    [SerializeField, Range(4, 32)]
    private int poolSize = 16;
    private ParticleSystem[] impacts, hits, deaths;
    private int impactIndex, hitIndex, deathIndex;
    private readonly Dictionary<EnemyHealth, EnemyActor> tracked = new Dictionary<EnemyHealth, EnemyActor>();
    private readonly Dictionary<EnemyHealth, System.Action<float>> hitListeners = new Dictionary<EnemyHealth, System.Action<float>>();
    private void Awake()
    {
        impacts = MakePool(impactPrefab);
        hits = MakePool(hitPrefab);
        deaths = MakePool(deathPrefab);
    }

    private ParticleSystem[] MakePool(ParticleSystem prefab)
    {
        var pool = new ParticleSystem[poolSize];
        if (prefab != null)
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(prefab, transform);
                pool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

        return pool;
    }

    private void OnEnable()
    {
        weapons.HitDetected += OnImpact;
        waves.EnemySpawned += Track;
    }

    private void OnDisable()
    {
        weapons.HitDetected -= OnImpact;
        waves.EnemySpawned -= Track;
        foreach (var pair in tracked)
            if (pair.Key != null)
            {
                pair.Key.Died -= OnDeath;
                pair.Key.Hit -= hitListeners[pair.Key];
            }

        tracked.Clear();
        hitListeners.Clear();
    }

    private void OnImpact(RaycastHit hit, float damage) => Play(impacts, ref impactIndex, hit.point + hit.normal * .03f);
    private void Track(EnemyActor actor)
    {
        if (tracked.ContainsKey(actor.Health))
            return;
        tracked[actor.Health] = actor;
        System.Action<float> listener = _ => Play(hits, ref hitIndex, actor.transform.position + Vector3.up);
        hitListeners[actor.Health] = listener;
        actor.Health.Hit += listener;
        actor.Health.Died += OnDeath;
    }

    private void OnDeath(EnemyHealth health)
    {
        if (!tracked.TryGetValue(health, out var actor))
            return;
        Play(deaths, ref deathIndex, actor.transform.position + Vector3.up * .7f);
        health.Hit -= hitListeners[health];
        health.Died -= OnDeath;
        hitListeners.Remove(health);
        tracked.Remove(health);
    }

    private static void Play(ParticleSystem[] pool, ref int index, Vector3 position)
    {
        if (pool == null || pool.Length == 0 || pool[index] == null)
            return;
        var effect = pool[index];
        index = (index + 1) % pool.Length;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.transform.position = position;
        effect.Play(true);
    }
}
