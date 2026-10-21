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
        if (weapons != null) weapons.HitDetected += OnImpact;
        if (waves != null) waves.EnemySpawned += Track;
    }

    private void OnDisable()
    {
        if (weapons != null) weapons.HitDetected -= OnImpact;
        if (waves != null) waves.EnemySpawned -= Track;
        foreach (var pair in tracked)
            if (pair.Key != null)
            {
                pair.Key.Died -= OnDeath;
            }

        tracked.Clear();
    }

    private void OnImpact(RaycastHit hit, float damage)
    {
        if (hit.collider == null) return;
        Quaternion rotation = Quaternion.LookRotation(hit.normal.sqrMagnitude > .001f ? hit.normal : Vector3.up);
        Vector3 position = hit.point + hit.normal * .015f;
        // One response at the bullet contact: flesh never also emits wall sparks.
        if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
            Play(hits, ref hitIndex, position, rotation);
        else
            Play(impacts, ref impactIndex, position, rotation);
    }
    private void Track(EnemyActor actor)
    {
        if (actor == null || actor.Health == null || tracked.ContainsKey(actor.Health))
            return;
        tracked[actor.Health] = actor;
        actor.Health.Died += OnDeath;
    }

    private void OnDeath(EnemyHealth health)
    {
        if (!tracked.TryGetValue(health, out var actor))
            return;
        Play(deaths, ref deathIndex, actor.transform.position + Vector3.up * .12f, Quaternion.LookRotation(Vector3.up));
        health.Died -= OnDeath;
        tracked.Remove(health);
    }

    private static void Play(ParticleSystem[] pool, ref int index, Vector3 position, Quaternion rotation)
    {
        if (pool == null || pool.Length == 0 || pool[index] == null)
            return;
        var effect = pool[index];
        index = (index + 1) % pool.Length;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.transform.SetPositionAndRotation(position, rotation);
        effect.Play(true);
    }
}
