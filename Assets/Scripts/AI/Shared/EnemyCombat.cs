using System;
using UnityEngine;

/// <summary>One attack clock controls animation and hit timing; no damage every frame.</summary>
public sealed class EnemyCombat : MonoBehaviour
{
    [SerializeField]
    private float range = 1.8f;
    [SerializeField]
    private float damage = 10;
    [SerializeField]
    private float cooldown = 1.6f;
    [SerializeField]
    private float windup = .5f;
    [SerializeField, Range(.2f, .85f)] private float contactNormalizedTime = .56f;
    [SerializeField, Range(30f, 180f)] private float hitArc = 110f;
    private EnemyActor actor;
    private float readyAt, impactAt, endsAt, trackingEndsAt;
    private bool impacted;
    private bool heavy;
    public bool IsAttacking { get; private set; }
    public float Range => range;
    public float Duration => windup / Mathf.Max(.2f, contactNormalizedTime);
    public float CooldownRemaining => Mathf.Max(0, readyAt - Time.time);
    public bool IsReady => actor != null && actor.CanThink && !IsAttacking && Time.time >= readyAt;
    public bool CanStartAttack => IsReady && actor.Perception.Visible &&
        actor.Perception.Distance <= range && actor.World.AttackSlotAvailable(actor) && ClearStrike();
    public string Phase => IsAttacking ? Time.time < impactAt ? "Anticipation" : "Strike / recovery" :
        CooldownRemaining > 0 ? "Cooldown" : "Ready";

    private bool ClearStrike()
    {
        var target = actor.World.player.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
        return Vector3.Distance(transform.position, target) <= range &&
            (direction.sqrMagnitude < .01f || Vector3.Angle(transform.forward, direction) <= hitArc * .5f) &&
            !Physics.Linecast(transform.position + Vector3.up, target + Vector3.up,
                actor.World.obstructionMask, QueryTriggerInteraction.Ignore);
    }

    public event Action<bool, float> AttackStarted;
    public void Initialize(EnemyActor value) => actor = value;
    public bool TryAttack(bool heavyAttack = false)
    {
        if (!CanStartAttack || !actor.World.TryAcquireAttack(actor))
            return false;
        IsAttacking = true;
        impacted = false;
        heavy = heavyAttack;
        float duration = heavy ? Duration * 1.4f : Duration;
        impactAt = Time.time + (heavy ? windup * 1.4f : windup);
        endsAt = Time.time + duration;
        trackingEndsAt = Time.time + duration * .38f;
        readyAt = endsAt + cooldown * (heavy ? 1.4f : 1);
        actor.Movement.Stop();
        AttackStarted?.Invoke(heavy, duration);
        return true;
    }

    private void Update()
    {
        if (!IsAttacking)
            return;
        if (Time.timeScale <= 0) return;
        if (actor == null || !actor.CanThink)
        {
            Cancel();
            return;
        }

        // Track during anticipation, then commit to the strike. A dodge can miss;
        // the enemy cannot spin around during the contact/recovery pose.
        if (Time.time < trackingEndsAt && actor.Perception.Visible)
            actor.Movement.Face(actor.Perception.TargetPosition);
        if (Time.time >= impactAt)
            AnimationImpact();
        if (Time.time >= endsAt) Cancel();
    }

    // Optional imported-clip AnimationEvent calls this same idempotent damage gate.
    public void AnimationImpact()
    {
        if (!IsAttacking || impacted || Time.time < impactAt || actor == null || !actor.CanThink)
            return;
        impacted = true;
        var target = actor.World.player.transform;
        if (Vector3.Distance(transform.position, target.position) > range + .25f)
            return;
        var direction = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (direction.sqrMagnitude > .01f && Vector3.Angle(transform.forward, direction) > hitArc * .5f)
            return;
        if (Physics.Linecast(transform.position + Vector3.up, target.position + Vector3.up, actor.World.obstructionMask, QueryTriggerInteraction.Ignore))
            return;
        actor.World.player.TakeDamage(damage * (heavy ? 1.8f : 1));
    }

    public void Cancel()
    {
        IsAttacking = false;
        impacted = true;
        if (actor != null && actor.World != null) actor.World.ReleaseAttack(actor);
    }

    private void OnDisable() => Cancel();
}
