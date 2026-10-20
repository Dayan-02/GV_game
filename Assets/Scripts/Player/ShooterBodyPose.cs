using UnityEngine;

/// <summary>Blends the weapon carry pose over retargeted locomotion without changing movement or firing.</summary>
[DefaultExecutionOrder(150), RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public sealed class ShooterBodyPose : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float blendSpeed = 8f;
    private Animator animator;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private PlayerAimController aim;
    private PlayerWeaponController weapons;
    private Transform chest, view;
    private float pitch;
    private int layer;
    private void Awake()
    {
        animator=GetComponent<Animator>();
        movement=GetComponentInParent<ThirdPersonPlayerController>();
        health=GetComponentInParent<PlayerHealth>();
        aim=GetComponentInParent<PlayerAimController>();
        weapons=GetComponentInParent<PlayerWeaponController>();
        chest=animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Chest):null;
        if(Camera.main!=null)view=Camera.main.transform;
        layer=animator.GetLayerIndex("Weapon Carry");
    }
    private void Update()
    {
        if(layer<0)return;
        bool carry=movement!=null&&movement.IsGrounded&&!movement.IsSprinting&&health!=null&&!health.IsDead;
        animator.SetLayerWeight(layer,Mathf.MoveTowards(animator.GetLayerWeight(layer),carry?1:0,Time.deltaTime*blendSpeed));
    }
    private void LateUpdate()
    {
        if(chest==null||view==null||health==null||health.IsDead||Time.deltaTime<=0)return;
        bool active=aim!=null&&aim.IsAiming&&weapons!=null&&!weapons.IsReloading&&movement!=null&&movement.IsGrounded;
        float target=active?Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(view.forward.y,-1,1))*Mathf.Rad2Deg,-45,45):0;
        pitch=Mathf.Lerp(pitch,target,1-Mathf.Exp(-blendSpeed*Time.deltaTime));
        // Runs before weapon aim and support-hand IK, so their targets include this upper-body motion.
        chest.rotation=Quaternion.AngleAxis(pitch*.75f,transform.right)*chest.rotation;
    }
}
