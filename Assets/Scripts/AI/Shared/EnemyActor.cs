using UnityEngine;

/// <summary>Composition root. Decisions live in the individual member folders.</summary>
[RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement), typeof(EnemyCombat))]
public sealed class EnemyActor : MonoBehaviour
{
    public EnemyWorld World { get; private set; }
    public EnemyHealth Health { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyCombat Combat { get; private set; }
    public EnemyPerception Perception { get; private set; }
    public string Decision { get; set; } = "Uninitialized";
    public float EncouragedUntil { get; set; }
    public bool IsEncouraged => Time.time < EncouragedUntil;
    private Vector3 searchGoal, searchCenter;
    private int searchVersion = -1, searchStep;
    private bool hasSearchGoal, initialVisitComplete;
    private float searchPauseUntil;
    private float searchAngle;
    public bool CanThink => World != null && World.player != null && !World.player.IsDead && !Health.IsDead && Time.timeScale > 0;

    private void Awake()
    {
        searchAngle = Random.Range(0f, 360f);
        Health = GetComponent<EnemyHealth>();
        Movement = GetComponent<EnemyMovement>();
        Combat = GetComponent<EnemyCombat>();
        Perception = GetComponent<EnemyPerception>();
    }

    private void OnEnable() => Health.Died += Die;
    private void OnDisable()
    {
        Health.Died -= Die;
        if (World != null)
            World.Remove(this);
    }

    public void Initialize(EnemyWorld world)
    {
        World = world;
        world.Register(this);
        Perception.Initialize(this);
        Combat.Initialize(this);
        Decision = "Searching";
    }

    public bool TickDue(ref float next, float interval)
    {
        if (!CanThink || Time.time < next)
            return false;
        next = Time.time + interval;
        return true;
    }

    /// <summary>Finite search around evidence; never substitutes the live player's position.</summary>
    public bool InvestigationGoal(out Vector3 goal)
    {
        if (Perception.HasMemory && searchVersion != Perception.ObservationVersion)
        {
            searchVersion = Perception.ObservationVersion;
            searchCenter = Perception.LastKnownPosition;
            searchGoal = searchCenter;
            searchStep = 0;
            hasSearchGoal = true;
            searchPauseUntil = 0;
        }
        if (!Perception.HasMemory)
        {
            if (searchVersion >= 0 || initialVisitComplete)
            { goal = transform.position; Decision = "Alert idle / no target"; return false; }
            searchGoal = World.InitialSearchPosition;
            hasSearchGoal = true;
        }
        if (hasSearchGoal && (Vector3.Distance(transform.position, searchGoal) < 1.5f || Movement.Failed))
        {
            hasSearchGoal = false;
            searchPauseUntil = Time.time + .8f;
            if (!Perception.HasMemory) initialVisitComplete = true;
        }
        if (!hasSearchGoal && Perception.HasMemory && Time.time >= searchPauseUntil && searchStep < 4)
        {
            for (; searchStep < 4; searchStep++)
            {
                float angle = searchAngle + searchStep * 97;
                Vector3 candidate = searchCenter + Quaternion.Euler(0, angle, 0) * Vector3.forward * (3 + searchStep);
                if (!Movement.Reachable(candidate)) continue;
                searchGoal = candidate;
                searchStep++;
                hasSearchGoal = true;
                break;
            }
        }
        goal = searchGoal;
        Decision = hasSearchGoal ? searchStep == 0 ? "Investigate last evidence" : "Search nearby routes" : "Listen / search pause";
        return hasSearchGoal;
    }

    public void Investigate(float speed)
    {
        if (InvestigationGoal(out var goal))
        {
            Movement.MoveTo(goal, speed, .7f);
            Movement.OpenNearbyDoor();
        }
        else Movement.Stop();
    }

    private void Update()
    {
        if (CanThink && !Combat.IsAttacking && Perception.Visible && Perception.Distance <= Combat.Range + .5f)
        {
            Movement.Face(Perception.TargetPosition);
            return;
        }
        if (CanThink && !Combat.IsAttacking && !Perception.Visible && Movement.Status == "Stopped" &&
            (Decision.Contains("Listen") || Decision.Contains("idle")))
            transform.Rotate(0, 35 * Time.deltaTime, 0);
    }

    public void Approach(float speed)
    {
        Vector3 target = World.ApproachPosition(this);
        if (!Movement.MoveTo(target, speed))
        {
            Movement.Stop();
            Decision = "Route blocked / waiting";
        }
        Movement.OpenNearbyDoor();
    }

    private void Die(EnemyHealth health)
    {
        Decision = "Dead";
        Movement.Stop();
        Combat.Cancel();
        if (World != null)
            World.Remove(this);
        GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        // Allow the full collapse and a settled corpse pose before cleanup.
        Destroy(gameObject, 9);
    }
}
