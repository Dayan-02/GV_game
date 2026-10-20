using System.Collections.Generic;
using UnityEngine;

public enum EnemyNoiseKind { Gunshot, Sprint, Explosion }
public struct EnemyNoise
{
    public int Sequence;
    public Vector3 Position;
    public float Radius, Time;
    public EnemyNoiseKind Kind;
}

/// <summary>Scene references, bounded hearing history and shared tactical reservations.</summary>
public sealed class EnemyWorld : MonoBehaviour
{
    public PlayerHealth player;
    public PlayerWeaponController weapons;
    public ThirdPersonPlayerController movement;
    public MapMarker[] tacticalPoints;
    public LayerMask obstructionMask = 1 << 8;
    [Header("Group pressure")]
    [SerializeField, Min(1)] private int maximumAttackers = 2;
    [SerializeField, Min(2)] private float waitingRadius = 3.2f;
    [Header("Hearing")]
    [SerializeField, Min(0)] private float sprintNoiseRadius = 12;
    [SerializeField, Min(.1f)] private float sprintNoiseInterval = .65f;
    private readonly HashSet<EnemyActor> living = new HashSet<EnemyActor>();
    private readonly HashSet<EnemyActor> attackers = new HashSet<EnemyActor>();
    private readonly Dictionary<MapMarker, EnemyActor> reservations = new Dictionary<MapMarker, EnemyActor>();
    private readonly Dictionary<EnemyActor, int> formation = new Dictionary<EnemyActor, int>();
    private readonly EnemyNoise[] noises = new EnemyNoise[32];
    private int noiseSequence, nextFormation;
    private float nextFootstep;
    private PlayerHealth subscribedPlayer;
    private PlayerWeaponController subscribedWeapons;
    public IReadOnlyCollection<EnemyActor> Living => living;
    public WeaponShotInfo LastNoise { get; private set; }
    public bool HasNoise { get; private set; }
    public Vector3 InitialSearchPosition { get; private set; }
    public int ActiveAttackers => attackers.Count;
    public int MaximumAttackers => maximumAttackers;

    private void Awake()
    {
        if (player != null) InitialSearchPosition = player.transform.position;
    }

    private void OnEnable()
    {
        subscribedPlayer = player;
        subscribedWeapons = weapons;
        if (subscribedPlayer != null) subscribedPlayer.Died += StopAll;
        if (subscribedWeapons != null) subscribedWeapons.ShotFired += RememberNoise;
        GrenadeProjectile.Detonated += OnExplosion;
    }

    private void OnDisable()
    {
        if (subscribedPlayer != null) subscribedPlayer.Died -= StopAll;
        if (subscribedWeapons != null) subscribedWeapons.ShotFired -= RememberNoise;
        GrenadeProjectile.Detonated -= OnExplosion;
        StopAll();
        reservations.Clear();
    }

    private void Update()
    {
        if (player == null || player.IsDead || movement == null || Time.timeScale <= 0) return;
        if (movement.IsSprinting && movement.PlanarSpeed > .5f && Time.time >= nextFootstep)
        {
            nextFootstep = Time.time + sprintNoiseInterval;
            ReportNoise(player.transform.position, sprintNoiseRadius, EnemyNoiseKind.Sprint);
        }
    }

    private void RememberNoise(WeaponShotInfo shot)
    {
        LastNoise = shot;
        HasNoise = true;
        ReportNoise(shot.Position, shot.NoiseRadius, EnemyNoiseKind.Gunshot);
    }

    private void OnExplosion(Vector3 position, float radius) => ReportNoise(position, radius, EnemyNoiseKind.Explosion);

    public void ReportNoise(Vector3 position, float radius, EnemyNoiseKind kind)
    {
        int sequence = ++noiseSequence;
        noises[sequence % noises.Length] = new EnemyNoise
        { Sequence = sequence, Position = position, Radius = radius, Time = Time.time, Kind = kind };
    }

    // Examine each event once. A quiet footstep cannot overwrite a still-audible gunshot.
    public bool TryHear(Vector3 listener, ref int cursor, out EnemyNoise heard)
    {
        heard = default;
        float strongest = float.NegativeInfinity;
        for (int i = Mathf.Max(cursor + 1, noiseSequence - noises.Length + 1); i <= noiseSequence; i++)
        {
            var noise = noises[i % noises.Length];
            float distance = Vector3.Distance(listener, noise.Position);
            if (noise.Sequence != i || Time.time - noise.Time > 2 || distance > noise.Radius) continue;
            float strength = noise.Radius - distance;
            if (strength > strongest) { strongest = strength; heard = noise; }
        }
        cursor = noiseSequence;
        return strongest > float.NegativeInfinity;
    }

    public void Register(EnemyActor actor)
    {
        if (living.Add(actor)) formation[actor] = nextFormation++;
    }

    public void Remove(EnemyActor actor)
    {
        living.Remove(actor);
        formation.Remove(actor);
        Release(actor);
        ReleaseAttack(actor);
    }

    public bool HasAttackSlot(EnemyActor actor) => attackers.Contains(actor);
    public bool AttackSlotAvailable(EnemyActor actor) => HasAttackSlot(actor) || attackers.Count < maximumAttackers;
    public bool TryAcquireAttack(EnemyActor actor)
    {
        if (!actor.CanThink || !AttackSlotAvailable(actor)) return false;
        attackers.Add(actor);
        return true;
    }
    public void ReleaseAttack(EnemyActor actor) => attackers.Remove(actor);

    public Vector3 ApproachPosition(EnemyActor actor)
    {
        var senses = actor.Perception;
        if (!senses.Visible || senses.Distance > 8 || (actor.Combat.IsReady && AttackSlotAvailable(actor))) return senses.TargetPosition;
        formation.TryGetValue(actor, out int index);
        float radius = Mathf.Max(waitingRadius, actor.Combat.Range + 1) + (index / 8 % 3) * .8f;
        return senses.TargetPosition + Quaternion.Euler(0, index * 137.508f, 0) * Vector3.forward * radius;
    }

    public int AlliesNear(EnemyActor owner, float range)
    {
        int count = 0;
        foreach (var a in living)
            if (a != null && a != owner && a.CanThink && (a.transform.position - owner.transform.position).sqrMagnitude < range * range) count++;
        return count;
    }

    public void EncourageAdvance(EnemyActor leader, float range, float seconds)
    {
        foreach (var a in living)
            if (a != null && a != leader && a.CanThink && a.Perception.Visible &&
                (a.transform.position - leader.transform.position).sqrMagnitude <= range * range)
                a.EncouragedUntil = Time.time + seconds;
    }

    public bool Available(MapMarker marker, EnemyActor owner) => marker != null &&
        (!reservations.TryGetValue(marker, out var occupied) || occupied == null || occupied == owner);
    public bool Reserve(MapMarker marker, EnemyActor owner)
    {
        if (!Available(marker, owner)) return false;
        Release(owner);
        reservations[marker] = owner;
        return true;
    }

    public void Release(EnemyActor owner)
    {
        MapMarker found = null;
        foreach (var pair in reservations)
            if (pair.Value == owner) { found = pair.Key; break; }
        if (found != null) reservations.Remove(found);
    }

    private void StopAll()
    {
        foreach (var a in living)
            if (a != null) { a.Movement.Stop(); a.Combat.Cancel(); }
        attackers.Clear();
    }
}
