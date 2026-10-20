using UnityEngine;

/// <summary>Positions the existing Survivalist FPS arm mesh at the current weapon grips.</summary>
[DefaultExecutionOrder(350), DisallowMultipleComponent]
public sealed class FirstPersonArms : MonoBehaviour
{
    [System.Serializable]
    public sealed class Arm
    {
        public Transform upper, lower, hand;
        public Quaternion wristRotation;
        [System.NonSerialized] public Quaternion upperRest, lowerRest;
    }
    [SerializeField] private Arm left = new Arm();
    [SerializeField] private Arm right = new Arm();
    private PlayerWeaponController weapons;
    private FirstPersonWeaponRig rig;

    public void Bind(PlayerWeaponController source, FirstPersonWeaponRig presentation)
    { weapons = source; rig = presentation; }

    private void Awake()
    {
        Cache(left); Cache(right);
    }
    private static void Cache(Arm arm)
    {
        if (arm.upper == null || arm.lower == null) return;
        arm.upperRest = arm.upper.localRotation; arm.lowerRest = arm.lower.localRotation;
    }
    private void LateUpdate()
    {
        if (weapons == null || weapons.ActiveView == null || Time.deltaTime <= 0) return;
        var view = weapons.ActiveView;
        Vector3 rightTarget = view.transform.TransformPoint(new Vector3(0, -.015f, -.025f));
        Vector3 leftTarget = view.SupportHand != null ? view.SupportHand.position : view.transform.TransformPoint(new Vector3(-.04f, -.025f, .1f));
        if (weapons.IsReloading && rig != null)
        {
            float amount = Mathf.Sin(Mathf.Clamp01((rig.ReloadProgress - .08f) / .84f) * Mathf.PI);
            leftTarget = Vector3.Lerp(leftTarget, view.transform.TransformPoint(new Vector3(-.045f, -.15f, .1f)), amount);
        }
        Solve(right, rightTarget, view.transform.rotation, new Vector3(.7f, -.7f, -.15f));
        Solve(left, leftTarget, view.transform.rotation, new Vector3(-.7f, -.7f, -.15f));
    }

    private void Solve(Arm arm, Vector3 target, Quaternion weaponRotation, Vector3 pole)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null) return;
        arm.upper.localRotation = arm.upperRest; arm.lower.localRotation = arm.lowerRest;
        Vector3 start = arm.upper.position;
        float a = Vector3.Distance(start, arm.lower.position), b = Vector3.Distance(arm.lower.position, arm.hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 delta = target - start;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .002f);
        Vector3 direction = delta.sqrMagnitude > .00001f ? delta.normalized : transform.forward;
        Vector3 bend = Vector3.ProjectOnPlane(transform.TransformDirection(pole), direction).normalized;
        if (bend.sqrMagnitude < .001f) bend = transform.right;
        float along = (a * a + distance * distance - b * b) / (2 * distance);
        Vector3 elbow = start + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        arm.upper.rotation = Quaternion.FromToRotation(arm.lower.position - start, elbow - start) * arm.upper.rotation;
        arm.lower.rotation = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, start + direction * distance - arm.lower.position) * arm.lower.rotation;
        arm.hand.rotation = weaponRotation * arm.wristRotation;
    }
}
