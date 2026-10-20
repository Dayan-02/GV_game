using UnityEngine;

/// <summary>Small terrain corrections, weighted out during the swing phase and in the air.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class HumanoidFootPlacement : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayers = 1 << 8;
    [SerializeField, Range(0f, 1f)] private float weight = 0.85f;
    [SerializeField, Min(0.01f)] private float maximumCorrection = 0.08f;
    [SerializeField] private bool useAnimationContacts;
    private Animator animator;
    private ThirdPersonPlayerController movement;
    private PlayerHealth health;
    private Transform owner;
    private CharacterController capsule;
    private PlayerStanceController stance;
    private float blend;
    private float leftContact, rightContact;
    private bool hasContactCurves;
#if UNITY_EDITOR
    public Vector4 LastLeftContact { get; private set; }
    public Vector4 LastRightContact { get; private set; }
    public int IkCallbacks { get; private set; }
    public Vector4 IkProbe { get; private set; }
#endif
    private readonly RaycastHit[] hits = new RaycastHit[12];
    [System.Serializable]
    private struct SolePoint { public Transform Bone; public Vector3 Position; public bool Left; }
    [SerializeField] private SolePoint[] solePoints = System.Array.Empty<SolePoint>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<ThirdPersonPlayerController>();
        health = GetComponentInParent<PlayerHealth>();
        owner = movement != null ? movement.transform : transform;
        capsule = owner.GetComponent<CharacterController>();
        stance = owner.GetComponent<PlayerStanceController>();
        bool left = false, right = false;
        foreach (var parameter in animator.parameters)
        { left |= parameter.name == "LeftFootContact"; right |= parameter.name == "RightFootContact"; }
        hasContactCurves = useAnimationContacts && left && right;
    }

#if UNITY_EDITOR
    [ContextMenu("Bake boot sole contact samples")]
    public void BakeSoles()
    {
        // Cache boot vertices in their dominant foot/toe bone's space once.
        // This avoids per-frame mesh baking and tracks the sole as the ankle rolls.
        var points=new System.Collections.Generic.List<SolePoint>();
        animator=GetComponent<Animator>();
        var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var lt=animator.GetBoneTransform(HumanBodyBones.LeftToes);
        var rf=animator.GetBoneTransform(HumanBodyBones.RightFoot);var rt=animator.GetBoneTransform(HumanBodyBones.RightToes);
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(!skin.name.Contains("Boots")||skin.sharedMesh==null)continue;
            var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;var bind=skin.sharedMesh.bindposes;var bones=skin.bones;
            for(int i=0;i<vertices.Length;i++)
            {
                int b=weights[i].boneIndex0;var bone=bones[b];
                if(bone!=lf&&bone!=lt&&bone!=rf&&bone!=rt)continue;
                points.Add(new SolePoint{Bone=bone,Position=bind[b].MultiplyPoint3x4(vertices[i]),Left=bone==lf||bone==lt});
            }
        }
        solePoints=points.ToArray();
        if(solePoints.Length==0)throw new System.InvalidOperationException("No boot sole contact samples found.");
    }
#endif
    private float SoleY(bool left,float fallback)
    {
        float low=float.PositiveInfinity;
        foreach(var point in solePoints)if(point.Left==left&&point.Bone!=null)low=Mathf.Min(low,point.Bone.TransformPoint(point.Position).y);
        return float.IsPositiveInfinity(low)?fallback:low;
    }

    private void OnAnimatorIK(int layerIndex)
    {
#if UNITY_EDITOR
        IkCallbacks++;
        IkProbe=new Vector4(layerIndex, movement!=null&&movement.IsGrounded?1:0, blend, animator.isHuman?1:0);
#endif
        if (layerIndex != 0 || !animator.isHuman) return;
        bool active = movement != null && movement.IsGrounded && (health == null || !health.IsDead);
        // Death continues with an unscaled Animator behind the frozen Game Over panel.
        // Release contact immediately when gameplay delta time is zero on death.
        blend = health != null && health.IsDead ? 0f :
            Mathf.MoveTowards(blend, active ? weight : 0f, Time.deltaTime * 10f);
        Place(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, HumanBodyBones.LeftLowerLeg, animator.leftFeetBottomHeight, ref leftContact, "LeftFootContact");
        Place(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, HumanBodyBones.RightLowerLeg, animator.rightFeetBottomHeight, ref rightContact, "RightFootContact");
    }

    private void Place(AvatarIKGoal goal, AvatarIKHint hint, HumanBodyBones kneeBone, float soleHeight, ref float smoothedContact, string contactParameter)
    {
        animator.SetIKPositionWeight(goal, 0f);
        animator.SetIKRotationWeight(goal, 0f);
        animator.SetIKHintPositionWeight(hint, 0f);
        float authoredContact = hasContactCurves ? Mathf.Clamp01(animator.GetFloat(contactParameter)) : 1f;
        smoothedContact = Mathf.MoveTowards(smoothedContact, authoredContact, Time.deltaTime * 12f);
        if (blend <= 0f) { smoothedContact = 0f; return; }
        // IK goals and imported foot bones have different rotation conventions.
        // Preserve the animated ankle rotation and adjust only the goal height.
        bool stanceBlend = stance != null && (stance.IsCrouching || stance.Blend > .001f);
        // Authored muscle-only clips do not carry reliable IK goal curves. Their
        // reported goal can remain near the pelvis; probe the evaluated ankle.
        Vector3 position = stanceBlend
            ? animator.GetBoneTransform(goal==AvatarIKGoal.LeftFoot?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot).position
            : animator.GetIKPosition(goal);
        float reach = stanceBlend ? Mathf.Max(.16f, maximumCorrection) : maximumCorrection;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * reach,
            Vector3.down, hits, reach * 2f + soleHeight, groundLayers, QueryTriggerInteraction.Ignore);
#if UNITY_EDITOR
        IkProbe=new Vector4(position.y, soleHeight, count, blend);
#endif
        int best = -1;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(owner) || hits[i].normal.y < 0.65f) continue;
            if (hits[i].distance < closest) { best = i; closest = hits[i].distance; }
        }
        if (best < 0) return;
        RaycastHit hit = hits[best];
#if UNITY_EDITOR
        var diagnostic = new Vector4(position.y, soleHeight, hit.point.y, hit.point.y + soleHeight + .006f - position.y);
        if(goal==AvatarIKGoal.LeftFoot)LastLeftContact=diagnostic;else LastRightContact=diagnostic;
#endif
        // Muscle interpolation between standing and deeply bent knees can push a
        // sole through the floor even when both source clips are calibrated.
        // Correct penetration only; never pull a lifted swing foot down.
        if (stanceBlend)
        {
            float penetration = hit.point.y + .006f - SoleY(goal==AvatarIKGoal.LeftFoot,position.y-soleHeight);
            if (penetration > .004f && penetration < .16f)
            {
                animator.SetIKPositionWeight(goal, 1f);
                animator.SetIKPosition(goal, position + Vector3.up * penetration);
                animator.SetIKHintPositionWeight(hint, .6f);
                animator.SetIKHintPosition(hint, animator.GetBoneTransform(kneeBone).position + owner.forward * .18f);
                return;
            }
        }
        // The corrected clips already have sole contact on a level street. Avoid
        // interpreting normal ankle roll as terrain error and pulling the foot down.
        if (hasContactCurves && capsule != null && hit.normal.y > .995f)
        {
            float level = owner.position.y + capsule.center.y - capsule.height * .5f - capsule.skinWidth;
            if (Mathf.Abs(hit.point.y - level) < .012f) return;
        }
        float clearance = position.y - soleHeight - hit.point.y;
        // Lifted feet must remain lifted; IK only adjusts feet near a planted stance.
        float contact = 1f - Mathf.InverseLerp(0.04f, 0.16f, clearance);
        float amount = blend * contact * smoothedContact;
        Vector3 target = position;
        float correction = hit.point.y + soleHeight + 0.006f - position.y;
        // Do not perturb a planted pose for tiny flat-ground differences or pull
        // the legs toward a surface beyond the small terrain-adjustment budget.
        if (Mathf.Abs(correction) > maximumCorrection) return;
        amount *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.004f, .025f, Mathf.Abs(correction)));
        if (amount < .001f) return;
        target.y += correction;
        animator.SetIKPositionWeight(goal, amount);
        animator.SetIKPosition(goal, target);
        Transform knee = animator.GetBoneTransform(kneeBone);
        animator.SetIKHintPositionWeight(hint, amount * .5f);
        animator.SetIKHintPosition(hint, knee.position + owner.forward * .12f);
    }
}
