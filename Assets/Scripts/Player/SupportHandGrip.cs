using UnityEngine;

/// <summary>Optional visual-only two-bone support arm alignment, after weapon aiming. No firing or root motion.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class SupportHandGrip : MonoBehaviour
{
    [SerializeField, Range(0,1)] private float maximumWeight = .95f;
    [SerializeField, Min(.01f)] private float blendSeconds = .16f;
    private Transform upper, lower, hand, chest;
    private PlayerWeaponController weapons;
    private PlayerHealth health;
    private ThirdPersonPlayerController movement;
    private Animator animator;
    private float weight;
    private void Awake()
    {
        animator=GetComponent<Animator>();
        if(!animator.isHuman){enabled=false;return;}
        upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
        chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        weapons=GetComponentInParent<PlayerWeaponController>();health=GetComponentInParent<PlayerHealth>();
        movement=GetComponentInParent<ThirdPersonPlayerController>();
    }
    private void LateUpdate()
    {
        if(upper==null || lower==null || hand==null || chest==null || weapons==null || Time.deltaTime<=0)return;
        var view=weapons.ActiveView;var target=view!=null?view.SupportHand:null;
        bool active=target!=null && animator.enabled && health!=null && !health.IsDead && !weapons.IsReloading && movement!=null && movement.IsGrounded && !movement.IsSprinting;
        weight=Mathf.MoveTowards(weight,active?maximumWeight:0,Time.deltaTime/blendSeconds);
        if(target==null || weight<=.001f || health==null || health.IsDead)return;
        Vector3 start=upper.position, end=Vector3.Lerp(hand.position,target.position,weight);
        float a=Vector3.Distance(start,lower.position), b=Vector3.Distance(lower.position,hand.position);
        if(a<.001f || b<.001f)return;
        Vector3 direction=end-start;float d=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.001f,a+b-.003f);
        direction=direction.sqrMagnitude>.00001f?direction.normalized:transform.forward;
        Vector3 pole=-transform.right*.7f-transform.forward*.25f-Vector3.up*.5f;
        Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
        if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(direction,transform.up).normalized;
        float along=(a*a+d*d-b*b)/(2*d), height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 elbow=start+direction*along+bend*height;
        Quaternion wristRotation=hand.rotation;
        upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+direction*d-lower.position)*lower.rotation;
        hand.rotation=wristRotation;
    }
    private void OnDisable(){weight=0;}
}
