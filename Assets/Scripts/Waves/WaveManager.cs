using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum WaveState
{
    Waiting,
    Spawning,
    Combat,
    Intermission,
    Victory,
    Stopped
}

/// <summary>Tracks only its own spawned enemies through death events; never scans the scene.</summary>
public sealed class WaveManager : MonoBehaviour
{
    [SerializeField]
    private WaveDefinition[] waves;
    [SerializeField]
    private MapMarker[] entrances;
    [SerializeField]
    private EnemyWorld world;
    [SerializeField]
    private GameUIController screens;
    [SerializeField]
    private float initialDelay = 6;
    [SerializeField]
    private float betweenWaves = 7;
    [SerializeField]
    private int supplyMagazines = 2;
    private readonly HashSet<EnemyHealth> living = new HashSet<EnemyHealth>();
    private Coroutine sequence;
    private int unspawned, spawnCursor;
    private bool spawningDone;
    public int CurrentWave { get; private set; }
    public int Remaining => living.Count + unspawned;
    public int LivingCount => living.Count;
    public WaveState State { get; private set; }
    public string Announcement { get; private set; } = "PREPARE TO SURVIVE";

    public event Action Changed;
    public event Action<EnemyActor> EnemySpawned;
    private void OnEnable()
    {
        if (world != null && world.player != null)
            world.player.Died += StopProgression;
    }

    private void Start()
    {
        if (world == null || screens == null || entrances == null || entrances.Length == 0 || waves == null || waves.Length != 5)
        {
            Debug.LogError("WaveManager needs five waves, entrances, EnemyWorld and GameUI.", this);
            enabled = false;
            return;
        }

        foreach (var w in waves)
            if (w == null || !w.Valid)
            {
                Debug.LogError("Invalid WaveDefinition", this);
                enabled = false;
                return;
            }

        sequence = StartCoroutine(Run());
    }

    private void OnDisable()
    {
        if (world != null && world.player != null)
            world.player.Died -= StopProgression;
        if (sequence != null)
            StopCoroutine(sequence);
        foreach (var enemy in living)
            if (enemy != null)
                enemy.Died -= OnDeath;
        living.Clear();
    }

    private IEnumerator Run()
    {
        State = WaveState.Waiting;
        Changed?.Invoke();
        yield return new WaitForSeconds(initialDelay);
        for (int index = 0; index < waves.Length; index++)
        {
            if (world.player.IsDead)
                yield break;
            var wave = waves[index];
            CurrentWave = index + 1;
            unspawned = wave.Total;
            spawningDone = false;
            State = WaveState.Spawning;
            Announcement = "WAVE " + CurrentWave + " / " + wave.title;
            Changed?.Invoke();
            int[] pending = new int[wave.entries.Length];
            for (int i = 0; i < pending.Length; i++)
                pending[i] = wave.entries[i].count;
            int failures = 0;
            while (unspawned > 0 && !world.player.IsDead)
            {
                int slot = UnityEngine.Random.Range(0, pending.Length);
                while (pending[slot] == 0)
                    slot = (slot + 1) % pending.Length;
                if (!TrySpawn(wave.entries[slot].prefab))
                {
                    if (++failures >= 10)
                    {
                        State = WaveState.Stopped;
                        Announcement = "SPAWN ROUTES BLOCKED";
                        Changed?.Invoke();
                        Debug.LogError("All spawn entrances are blocked. Inspect entrance markers.", this);
                        yield break;
                    }

                    yield return new WaitForSeconds(.5f);
                    continue;
                }

                failures = 0;
                pending[slot]--;
                unspawned--;
                Changed?.Invoke();
                yield return new WaitForSeconds(wave.spawnInterval);
            }

            spawningDone = true;
            State = WaveState.Combat;
            Changed?.Invoke();
            while (living.Count > 0 && !world.player.IsDead)
                yield return null;
            if (world.player.IsDead)
                yield break;
            if (index == waves.Length - 1)
            {
                State = WaveState.Victory;
                Announcement = "DISTRICT SECURED";
                Changed?.Invoke();
                screens.ShowVictory();
                yield break;
            }

            State = WaveState.Intermission;
            Announcement = "WAVE CLEAR / AMMO SUPPLY RECEIVED";
            world.weapons.GrantReserveMagazines(supplyMagazines);
            Changed?.Invoke();
            yield return new WaitForSeconds(betweenWaves);
        }
    }

    private bool TrySpawn(EnemyActor prefab)
    {
        for (int offset = 0; offset < entrances.Length; offset++)
        {
            int index = (spawnCursor + offset) % entrances.Length;
            var marker = entrances[index];
            if (marker == null)
                continue;
            if (Vector3.Distance(marker.transform.position, world.player.transform.position) < 8)
                continue;
            if (!NavMesh.SamplePosition(marker.transform.position, out var hit, 2, NavMesh.AllAreas))
                continue;
            if (Physics.CheckSphere(hit.position + Vector3.up, .55f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore))
                continue;
            var actor = Instantiate(prefab, hit.position, marker.transform.rotation);
            actor.Initialize(world);
            living.Add(actor.Health);
            actor.Health.Died += OnDeath;
            spawnCursor = (index + 1) % entrances.Length;
            EnemySpawned?.Invoke(actor);
            return true;
        }

        return false;
    }

    private void OnDeath(EnemyHealth enemy)
    {
        if (!living.Remove(enemy))
            return;
        enemy.Died -= OnDeath;
        Changed?.Invoke();
        // Run waits for both spawningDone and zero living; early kills never skip queued spawns.
        if (spawningDone && living.Count == 0)
            Announcement = "WAVE CLEAR";
    }

    private void StopProgression()
    {
        if (sequence != null)
            StopCoroutine(sequence);
        sequence = null;
        State = WaveState.Stopped;
        Announcement = "OUTBREAK OVERRAN THE DISTRICT";
        Changed?.Invoke();
    }
}
