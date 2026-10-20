using UnityEngine;

/// <summary>Only this sensor reads live player state. Brains consume bounded observations.</summary>
public sealed class EnemyPerception : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float sampleInterval = .2f;
    [SerializeField, Min(1)] private float sightRange = 45;
    [SerializeField, Range(30, 360)] private float fieldOfView = 130;
    [SerializeField, Min(0)] private float closeAwarenessRange = 2.4f;
    [SerializeField, Min(1)] private float memorySeconds = 16;
    [SerializeField, Min(.1f)] private float eyeHeight = 1.5f;
    private EnemyActor actor;
    private CharacterController playerCapsule;
    private float nextSample, heardUntil, lastKnowledgeAt = float.NegativeInfinity;
    private int noiseCursor;
    public float Distance { get; private set; } = float.MaxValue;
    public bool Visible { get; private set; }
    public bool Heard => Time.time < heardUntil;
    public float PlayerWeakness { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public Vector3 ObservedForward { get; private set; }
    public Vector3 ObservedVelocity { get; private set; }
    public WeaponCategory Weapon { get; private set; }
    public bool PlayerFiring { get; private set; }
    public bool PlayerReloading { get; private set; }
    public bool HasMemory { get; private set; }
    public float MemoryRemaining => HasMemory ? Mathf.Max(0, memorySeconds - (Time.time - lastKnowledgeAt)) : 0;
    public int ObservationVersion { get; private set; }
    public string Knowledge => Visible ? "Visible" : HasMemory ? Heard ? "Heard / investigate" : "Last known / search" : "Unaware";

    public void Initialize(EnemyActor value)
    {
        actor = value;
        playerCapsule = actor.World.player != null ? actor.World.player.GetComponent<CharacterController>() : null;
        Sample();
    }

    private void Update()
    {
        if (actor != null && actor.CanThink && Time.time >= nextSample) Sample();
    }

    public void Sample()
    {
        if (actor == null || actor.World == null || actor.World.player == null) return;
        nextSample = Time.time + sampleInterval;
        var world = actor.World;
        Vector3 position = world.player.transform.position;
        Vector3 delta = position - transform.position;
        float distance = delta.magnitude;
        Vector3 planar = Vector3.ProjectOnPlane(delta, Vector3.up);
        bool inView = distance <= closeAwarenessRange || Vector3.Angle(transform.forward, planar) <= fieldOfView * .5f;
        Vector3 targetChest = playerCapsule != null ? playerCapsule.bounds.center : position + Vector3.up;
        bool wasVisible = Visible;
        Visible = !world.player.IsDead && distance <= sightRange && inView &&
            !Physics.Linecast(transform.position + Vector3.up * eyeHeight, targetChest, world.obstructionMask, QueryTriggerInteraction.Ignore);
        bool heardNow = world.TryHear(transform.position, ref noiseCursor, out var noise);
        if (heardNow) heardUntil = Time.time + .8f;
        PlayerFiring = Visible && world.weapons != null && world.weapons.IsFiring;
        PlayerReloading = Visible && world.weapons != null && world.weapons.IsReloading;
        Weapon = Visible && world.weapons != null ? world.weapons.CurrentWeaponCategory : WeaponCategory.None;
        PlayerWeakness = Visible ? 1 - world.player.CurrentHealth / Mathf.Max(1, world.player.MaxHealth) : 0;
        ObservedVelocity = Visible && world.movement != null ? world.movement.PlanarVelocity : Vector3.zero;
        if (Visible)
        {
            ObservedForward = world.player.transform.forward;
            Remember(position, !wasVisible);
        }
        else if (heardNow) Remember(noise.Position, true);
        else if (HasMemory && Time.time - lastKnowledgeAt >= memorySeconds) Forget();
        TargetPosition = HasMemory ? LastKnownPosition : Vector3.zero;
        Distance = HasMemory ? Vector3.Distance(transform.position, LastKnownPosition) : float.MaxValue;
    }

    private void Remember(Vector3 position, bool newEvidence)
    {
        if (!HasMemory || newEvidence || (position - LastKnownPosition).sqrMagnitude > 4) ObservationVersion++;
        LastKnownPosition = position;
        lastKnowledgeAt = Time.time;
        HasMemory = true;
    }

    public void Forget()
    {
        HasMemory = false;
        Visible = false;
        LastKnownPosition = TargetPosition = ObservedVelocity = ObservedForward = Vector3.zero;
        PlayerFiring = PlayerReloading = false;
        PlayerWeakness = 0;
        Weapon = WeaponCategory.None;
        Distance = float.MaxValue;
        ObservationVersion++;
    }
}
