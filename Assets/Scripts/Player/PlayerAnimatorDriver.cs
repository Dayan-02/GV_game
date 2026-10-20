using UnityEngine;

/// <summary>Translates controller state into Animator parameters and exposes animation-only action hooks.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ThirdPersonPlayerController))]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private ThirdPersonPlayerController movement;
    [SerializeField]
    private PlayerAimController aim;
    [SerializeField]
    private PlayerHealth health;
    [SerializeField, Min(0f)]
    private float parameterDampTime = 0.1f;
    private int actionsLayer = -1;
    private bool deathRequested;
    private PlayerStanceController stance;
    private bool supportsCrouch;
    private float previousHealth;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Horizontal = Animator.StringToHash("Horizontal");
    private static readonly int Vertical = Animator.StringToHash("Vertical");
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    private static readonly int Fire = Animator.StringToHash("Fire");
    private static readonly int Reload = Animator.StringToHash("Reload");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Death = Animator.StringToHash("Death");
    private void Awake()
    {
        stance=GetComponent<PlayerStanceController>();
        if (movement == null)
            movement = GetComponent<ThirdPersonPlayerController>();
        if (aim == null)
            aim = GetComponent<PlayerAimController>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerAnimatorDriver needs an Animator with the Phase 2 controller.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        foreach(var parameter in animator.parameters)if(parameter.name=="IsCrouching")supportsCrouch=true;
        actionsLayer = animator.GetLayerIndex("Upper Body Actions");
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += PlayDeath;
            health.HealthChanged += OnHealthChanged;
            previousHealth = health.CurrentHealth;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= PlayDeath;
            health.HealthChanged -= OnHealthChanged;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead && !deathRequested)
            PlayDeath();
        bool dead = deathRequested || (health != null && health.IsDead);
        bool aiming = aim != null && aim.IsAiming && !dead;
        Vector3 localVelocity = transform.InverseTransformDirection(movement.PlanarVelocity);
        float referenceSpeed = aiming ? aim.AimMoveSpeed : movement.WalkSpeed;
        bool crouching=stance!=null&&stance.IsCrouching&&!dead;
        if(crouching)referenceSpeed=stance.CrouchedSpeed;
        Vector2 local = Vector2.ClampMagnitude(new Vector2(localVelocity.x, localVelocity.z) / Mathf.Max(.01f, referenceSpeed), 1f);
        float dt = Time.deltaTime;
        // Speed uses semantic knots: idle 0, walk 1, run 2, sprint 3.
        float normalizedSpeed = NormalizeSpeed(movement.PlanarSpeed);
        // The motor already accelerates/brakes smoothly. Smoothing Speed a second
        // time leaves the feet walking after the capsule slows or hits a wall.
        // Keep gait weights tied to measured displacement; damp direction only.
        animator.SetFloat(Speed, dead ? 0f : normalizedSpeed);
        animator.SetFloat(Horizontal, dead ? 0f : local.x, parameterDampTime, dt);
        animator.SetFloat(Vertical, dead ? 0f : local.y, parameterDampTime, dt);
        animator.SetBool(IsAiming, aiming);
        animator.SetBool(IsGrounded, movement.IsGrounded);
        animator.SetBool(IsSprinting, movement.IsSprinting && !dead);
        animator.SetFloat(VerticalVelocity, movement.VerticalVelocity);
        animator.SetBool(IsDead, dead);
        if(supportsCrouch)animator.SetBool("IsCrouching",crouching);
        if (actionsLayer >= 0)
        {
            bool active = !animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Empty") || (animator.IsInTransition(actionsLayer) && !animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Empty"));
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(actionsLayer), !dead && active ? 1f : 0f, dt / 0.08f);
            animator.SetLayerWeight(actionsLayer, dead ? 0f : weight);
        }
    }

    private float NormalizeSpeed(float speed)
    {
        if (speed <= movement.WalkSpeed)
            return speed / movement.WalkSpeed;
        if (speed <= movement.RunSpeed)
            return 1f + Mathf.InverseLerp(movement.WalkSpeed, movement.RunSpeed, speed);
        return 2f + Mathf.InverseLerp(movement.RunSpeed, movement.SprintSpeed, speed);
    }

    // These methods animate only. They do not deal damage, spawn objects, consume ammo, or read firing keys.
    public void PlayFire()
    {
        if (CanPlayAction())
            animator.SetTrigger(Fire);
    }

    public void PlayReload(float duration = 1.2f)
    {
        if (CanPlayAction())
        {
            animator.SetFloat("ReloadSpeed", 1.2f / Mathf.Max(.05f, duration));
            animator.SetTrigger(Reload);
        }
    }

    public void PlayHit()
    {
        if (CanPlayAction())
            animator.SetTrigger(Hit);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (animator == null)
            return;
        bool reloading = actionsLayer >= 0 && (animator.GetCurrentAnimatorStateInfo(actionsLayer).IsName("Reload") || animator.GetNextAnimatorStateInfo(actionsLayer).IsName("Reload"));
        if (current > 0 && current < previousHealth && !reloading)
            PlayHit();
        previousHealth = current;
    }

    public void CancelWeaponAction()
    {
        if (animator == null)
            return;
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        if (actionsLayer >= 0)
        {
            animator.Play("Empty", actionsLayer, 0f);
            animator.SetLayerWeight(actionsLayer, 0f);
        }
    }

    public void PlayDeath()
    {
        if (animator == null)
            return;
        deathRequested = true;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime; // Finish death behind the frozen Game Over panel.
        animator.ResetTrigger(Fire);
        animator.ResetTrigger(Reload);
        animator.ResetTrigger(Hit);
        animator.SetBool(IsDead, true);
        animator.SetTrigger(Death);
    }

    private bool CanPlayAction() => animator != null && isActiveAndEnabled && !deathRequested && (health == null || !health.IsDead);
    [ContextMenu("Animation Preview/Fire (Play Mode)")]
    private void PreviewFire()
    {
        if (Application.isPlaying)
            PlayFire();
    }

    [ContextMenu("Animation Preview/Reload (Play Mode)")]
    private void PreviewReload()
    {
        if (Application.isPlaying)
            PlayReload();
    }

    [ContextMenu("Animation Preview/Hit (Play Mode)")]
    private void PreviewHit()
    {
        if (Application.isPlaying)
            PlayHit();
    }

    [ContextMenu("Animation Preview/Death via PlayerHealth (Play Mode)")]
    private void PreviewDeath()
    {
        if (Application.isPlaying && health != null)
            health.Die();
    }
}
