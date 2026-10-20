using UnityEngine;

/// <summary>Small additive torso inertia and breathing after animation, before weapon/hand IK.
/// Never changes the root, pelvis, legs, movement speed, or combat timing.</summary>
[DefaultExecutionOrder(130), RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public sealed class CharacterMotionPolish : MonoBehaviour
{
    [SerializeField, Range(0, 1)] private float amount = .8f;
    [SerializeField, Range(0, 1)] private float breathing = .45f;
    private Animator animator;
    private Transform chest, spine, owner;
    private ThirdPersonPlayerController player;
    private PlayerHealth playerHealth;
    private PlayerAimController aim;
    private PlayerWeaponController weapons;
    private EnemyMovement enemy;
    private EnemyHealth enemyHealth;
    private EnemyCombat combat;
    private Vector3 lastVelocity, smoothedAcceleration;
    private float lastYaw, turn, phase;
    private Quaternion chestBase, spineBase;
    private bool applied, initialized;

    private void Awake()
    {
        animator=GetComponent<Animator>();
        if(!animator.isHuman){enabled=false;return;}
        chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        spine=animator.GetBoneTransform(HumanBodyBones.Spine);
        player=GetComponentInParent<ThirdPersonPlayerController>();
        playerHealth=GetComponentInParent<PlayerHealth>();aim=GetComponentInParent<PlayerAimController>();
        weapons=GetComponentInParent<PlayerWeaponController>();
        enemy=GetComponentInParent<EnemyMovement>();enemyHealth=GetComponentInParent<EnemyHealth>();combat=GetComponentInParent<EnemyCombat>();
        owner=player!=null?player.transform:enemy!=null?enemy.transform:transform;
        phase=Mathf.Abs(GetEntityId().GetHashCode()%97)*.17f;
        lastYaw=owner.eulerAngles.y;
    }

    private void Update()=>RestorePose();

    private void LateUpdate()
    {
        float dt=Time.deltaTime;
        if(dt<=0||chest==null||spine==null||!animator.enabled)return;
        bool inactive=(playerHealth!=null&&playerHealth.IsDead)||(enemyHealth!=null&&enemyHealth.IsDead)||
            (player!=null&&!player.IsGrounded)||(weapons!=null&&weapons.IsReloading)||(combat!=null&&combat.IsAttacking);
        Vector3 velocity=player!=null?player.PlanarVelocity:enemy!=null?enemy.Velocity:Vector3.zero;
        if(!initialized){lastVelocity=velocity;lastYaw=owner.eulerAngles.y;initialized=true;}
        Vector3 acceleration=Vector3.ClampMagnitude((velocity-lastVelocity)/Mathf.Max(.016f,dt),18);
        lastVelocity=velocity;
        float yaw=owner.eulerAngles.y;
        float yawRate=Mathf.Clamp(Mathf.DeltaAngle(lastYaw,yaw)/Mathf.Max(.016f,dt),-180,180);
        lastYaw=yaw;
        float blend=1-Mathf.Exp(-9*dt);
        smoothedAcceleration=Vector3.Lerp(smoothedAcceleration,inactive?Vector3.zero:acceleration,blend);
        turn=Mathf.Lerp(turn,inactive?0:yawRate,blend);
        if(inactive)return;
        float weight=amount*(aim!=null&&aim.IsAiming?.22f:1);
        Vector3 local=owner.InverseTransformDirection(smoothedAcceleration);
        float pitch=Mathf.Clamp(local.z*.22f,-3,4)*weight;
        float roll=Mathf.Clamp(-local.x*.17f-turn*.012f,-3,3)*weight;
        float breath=Mathf.Sin(Time.time*1.65f+phase)*breathing*Mathf.Lerp(1,.3f,Mathf.Clamp01(velocity.magnitude/2));
        chestBase=chest.localRotation;spineBase=spine.localRotation;
        spine.rotation=Quaternion.AngleAxis(pitch*.4f,owner.right)*Quaternion.AngleAxis(roll*.4f,owner.forward)*spine.rotation;
        chest.rotation=Quaternion.AngleAxis(pitch*.6f+breath,owner.right)*Quaternion.AngleAxis(roll*.6f,owner.forward)*chest.rotation;
        applied=true;
    }

    private void RestorePose()
    {
        if(!applied)return;
        if(spine!=null)spine.localRotation=spineBase;
        if(chest!=null)chest.localRotation=chestBase;
        applied=false;
    }
    private void OnDisable(){RestorePose();initialized=false;smoothedAcceleration=Vector3.zero;turn=0;}
}
