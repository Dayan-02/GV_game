using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class EnemyAnimationController : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [Header("Measured foot travel at clip playback speed 1")]
    [SerializeField, Min(.01f)] private float walkStrideSpeed = 1.675673f;
    [SerializeField, Min(.01f)] private float chaseStrideSpeed = 4.528451f;
    [SerializeField, Min(.01f)] private float visualStrideScale = 1f;
    [Header("Locomotion variation")]
    [SerializeField] private bool randomizeLocomotionPhase = true;
    [Header("Contact-timed action clips")]
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip heavyAttackClip;
    [Header("Hit presentation")]
    [SerializeField, Min(.1f)] private float minimumHitReactionInterval = .3f;
    private float nextHitReaction;
    private EnemyHealth health;
    private EnemyMovement movement;
    private EnemyCombat combat;
    private bool hasDeathVariants;
    private static readonly int DeathVariant = Animator.StringToHash("DeathVariant");
    private static readonly int DeathSpeed = Animator.StringToHash("DeathSpeed");
    private static readonly int Speed = Animator.StringToHash("Speed"), Attack = Animator.StringToHash("Attack"), Heavy = Animator.StringToHash("Heavy"), Hit = Animator.StringToHash("Hit"), Dead = Animator.StringToHash("Dead"), AttackSpeed = Animator.StringToHash("AttackSpeed");
    private static readonly int Locomotion = Animator.StringToHash("Base Layer.Locomotion");
    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        combat = GetComponent<EnemyCombat>();
        if (animator != null)
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == DeathVariant) hasDeathVariants = true;
    }

    private void Start()
    {
        // Offset only the initial movement cycle so a spawned group does not march
        // in lockstep. Attacks, hits and death always keep their authored timing.
        if (randomizeLocomotionPhase && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.avatar != null &&
            animator.avatar.isValid && animator.HasState(0, Locomotion) &&
            !health.IsDead && !combat.IsAttacking)
            animator.Play(Locomotion, 0, Random.value);
    }

    private void OnEnable()
    {
        nextHitReaction = 0f;
        health.Hit += OnHit;
        health.Died += OnDeath;
        combat.AttackStarted += OnAttack;
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Died -= OnDeath;
        combat.AttackStarted -= OnAttack;
    }

    private void Update()
    {
        if (animator != null && !health.IsDead)
        {
            float speed = Vector3.ProjectOnPlane(movement.Velocity, Vector3.up).magnitude;
            animator.SetFloat(Speed, speed < .05f ? 0f : speed, speed < .05f ? 0f : .04f, Time.deltaTime);
            float reference = animator.GetCurrentAnimatorStateInfo(0).IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                float nextReference = next.IsName("Chase") ? chaseStrideSpeed : walkStrideSpeed;
                reference = Mathf.Lerp(reference, nextReference, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            }
            // Only locomotion states consume this parameter; attack/death timing is separate.
            float cadence = speed < .05f ? 1f : Mathf.Clamp(speed / (reference * visualStrideScale), .08f, 3f);
            animator.SetFloat("GaitScale", cadence);
        }
    }

    private void OnHit(float amount)
    {
        if (animator == null || health.IsDead || combat.IsAttacking || Time.time < nextHitReaction)
            return;
        // Rapid fire/shotgun pellets must not queue repeated whole-body flinches.
        // Damage still applies for every hit; this only gates the presentation.
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0) || state.IsName("Hit") || state.IsName("Death"))
            return;
        nextHitReaction = Time.time + minimumHitReactionInterval;
        animator.ResetTrigger(Hit);
        animator.SetTrigger(Hit);
    }

    private void OnDeath(EnemyHealth value)
    {
        if (animator != null)
        {
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.SetFloat(Speed, 0);
            if (hasDeathVariants)
            {
                animator.SetInteger(DeathVariant, Random.Range(0, 3));
                animator.SetFloat(DeathSpeed, Random.Range(.96f, 1.04f));
            }
            animator.SetBool(Dead, true);
        }
    }

    private void OnAttack(bool heavy, float duration)
    {
        if (animator != null)
        {
            var clip = heavy ? heavyAttackClip : attackClip;
            // Clip lengths are not necessarily one second. Match the actual action
            // to the combat clock so the visible strike and damage share a contact time.
            float length = clip != null ? clip.length : 1f;
            animator.SetFloat(AttackSpeed, length / Mathf.Max(.05f, duration));
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Heavy);
            animator.ResetTrigger(Hit);
            animator.CrossFadeInFixedTime(heavy ? "Base Layer.Heavy" : "Base Layer.Attack", .06f, 0, 0f);
        }
    }
}
