using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Camera-relative weapon presentation; the existing weapon controller owns shots and ammo.</summary>
[DefaultExecutionOrder(160), DisallowMultipleComponent]
[RequireComponent(typeof(PlayerWeaponController), typeof(ThirdPersonPlayerController))]
public sealed class FirstPersonWeaponRig : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private Transform viewRoot;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private FirstPersonArms arms;
    [SerializeField] private Renderer[] bodyRenderers = System.Array.Empty<Renderer>();
    [SerializeField, Range(0, 31)] private int viewLayer = 30;
    [SerializeField, Min(0)] private float swayAmount = .45f;
    [SerializeField, Min(0)] private float bobAmount = .004f;
    [SerializeField, Min(.01f)] private float poseResponse = 14f;
    private PlayerWeaponController weapons;
    private ThirdPersonPlayerController movement;
    private PlayerAimController aim;
    private PlayerHealth health;
    private ThirdPersonCameraController cameraRig;
    private ShadowCastingMode[] originalShadows;
    private WeaponHitscan cover;
    private Vector3 position, angles;
    private float recoil, recoilVelocity, kick, kickVelocity, phase, reloadStart, obstruction, equip;
    private float aimPose;
    private bool reloading, initialized;
    public bool IsSprinting => movement != null && movement.IsSprinting;
    public float ReloadProgress => reloading && weapons.CurrentWeapon != null ?
        Mathf.Clamp01((Time.time - reloadStart) / weapons.CurrentWeapon.ReloadTime) : 0f;

    private void Awake()
    {
        weapons = GetComponent<PlayerWeaponController>();
        movement = GetComponent<ThirdPersonPlayerController>();
        aim = GetComponent<PlayerAimController>(); health = GetComponent<PlayerHealth>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera != null) cameraRig = worldCamera.GetComponent<ThirdPersonCameraController>();
        if (worldCamera == null || weaponCamera == null || viewRoot == null || weaponSocket == null || arms == null)
        {
            Debug.LogError("FirstPersonWeaponRig needs its camera, socket and arms references.", this);
            enabled = false; return;
        }
        cover = new WeaponHitscan(transform, Physics.DefaultRaycastLayers & ~(1 << viewLayer));
        arms.Bind(weapons, this);
        originalShadows = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            originalShadows[i] = bodyRenderers[i].shadowCastingMode;
        }
    }

    private void OnEnable()
    {
        if (weapons == null) return;
        weapons.ShotFired += OnShot;
        weapons.WeaponChanged += OnWeaponChanged;
        weapons.ReloadStateChanged += OnReloadChanged;
        for (int i = 0; i < bodyRenderers.Length; i++)
            if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        if (weaponCamera != null) weaponCamera.enabled = true;
    }

    public void ConfigureView(WeaponView view)
    {
        foreach (var child in view.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = viewLayer;
        foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
        { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
    }

    private void OnShot(WeaponShotInfo shot)
    {
        float push, rise;
        switch (weapons.CurrentWeaponCategory)
        {
            case WeaponCategory.SMG: push = .013f; rise = .65f; break;
            case WeaponCategory.Shotgun: push = .04f; rise = 2.8f; break;
            case WeaponCategory.Pistol: push = .023f; rise = 3.2f; break;
            default: push = .023f; rise = 1.3f; break;
        }
        // Impulse into a damped spring: fast rise followed by a continuous recovery.
        float aimScale = Mathf.Lerp(1, .65f, aimPose);
        recoilVelocity = Mathf.Min(recoilVelocity + push * 58f * aimScale, 4f);
        kickVelocity = Mathf.Min(kickVelocity + rise * 58f * aimScale, 260f);
    }
    private void OnWeaponChanged(WeaponData data)
    {
        equip = .16f; recoil = recoilVelocity = kick = kickVelocity = 0;
        reloading = false; initialized = false;
    }
    private void OnReloadChanged(bool value) { reloading = value; reloadStart = Time.time; }

    private void LateUpdate()
    {
        if (worldCamera == null || viewRoot == null) return;
        bool visible = health != null && !health.IsDead;
        viewRoot.gameObject.SetActive(visible);
        weaponCamera.enabled = visible;
        if (!visible || Time.deltaTime <= 0f) return;
        viewRoot.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        weaponCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
        weaponCamera.fieldOfView = worldCamera.fieldOfView;
        weaponCamera.rect = worldCamera.rect;
        weaponCamera.aspect = worldCamera.aspect;

        float dt = Time.deltaTime;
        float requestedAim = cameraRig != null ? cameraRig.AimBlend : aim != null && aim.IsAiming ? 1 : 0;
        aimPose = Mathf.Lerp(aimPose, reloading || IsSprinting ? 0 : requestedAim, 1 - Mathf.Exp(-16f * dt));
        float blend = aimPose;
        Vector3 hip, sight;
        switch (weapons.CurrentWeaponCategory)
        {
            case WeaponCategory.Pistol: hip = new Vector3(.16f, -.18f, .42f); sight = new Vector3(0, -.10f, .46f); break;
            case WeaponCategory.Shotgun: hip = new Vector3(.20f, -.25f, .09f); sight = new Vector3(0, -.125f, .13f); break;
            case WeaponCategory.SMG: hip = new Vector3(.19f, -.22f, .27f); sight = new Vector3(0, -.17f, .29f); break;
            default: hip = new Vector3(.20f, -.23f, .20f); sight = new Vector3(0, -.158f, .22f); break;
        }
        Vector3 target = Vector3.Lerp(hip, sight, blend);
        float motion = movement.IsGrounded ? Mathf.Clamp01(movement.PlanarSpeed / movement.RunSpeed) : 0;
        phase += movement.PlanarSpeed * dt * 4.5f;
        target += new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 2) * .6f, 0) * (bobAmount * motion * (1 - blend * .85f));
        bool lookAllowed = Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
            (cameraRig == null || cameraRig.GameplayInputEnabled);
        Vector3 targetAngles = lookAllowed ? new Vector3(Input.GetAxisRaw("Mouse Y"), -Input.GetAxisRaw("Mouse X"), 0) * (swayAmount * (1 - blend * .85f)) : Vector3.zero;
        targetAngles = Vector3.ClampMagnitude(targetAngles, 2f);
        float lower = IsSprinting ? 1 : 0;
        if (reloading)
        {
            float t = ReloadProgress;
            float envelope = WeaponReloadMotion.Hold(t);
            bool pistol = weapons.CurrentWeaponCategory == WeaponCategory.Pistol;
            target += new Vector3(-.015f, -.045f, -.035f) * envelope;
            targetAngles += (pistol ? new Vector3(8, -12, -24) : new Vector3(6, -8, -16)) * envelope;
        }
        float clearance = .95f;
        if (cover.Raycast(worldCamera.transform.position, worldCamera.transform.forward, clearance, out var hit))
            clearance = hit.distance;
        obstruction = Mathf.Lerp(obstruction, 1 - Mathf.InverseLerp(.25f, .85f, clearance), 1 - Mathf.Exp(-18 * dt));
        lower = Mathf.Max(lower, obstruction);
        target += new Vector3(0, -.10f, -.10f) * lower;
        targetAngles += new Vector3(22, 0, -8) * lower;
        Spring(ref recoil, ref recoilVelocity, 24f, dt);
        Spring(ref kick, ref kickVelocity, 22f, dt);
        equip = Mathf.MoveTowards(equip, 0, dt * .6f);
        target += new Vector3(0, -equip, 0);
        targetAngles.x += equip * 90f;
        if (!initialized) { position = target; angles = targetAngles; initialized = true; }
        float response = 1 - Mathf.Exp(-poseResponse * dt);
        position = Vector3.Lerp(position, target, response);
        angles = Vector3.Lerp(angles, targetAngles, response);
        weaponSocket.localPosition = position + new Vector3(0, 0, -Mathf.Min(.075f, recoil));
        weaponSocket.localRotation = Quaternion.Euler(angles + new Vector3(-Mathf.Min(7f, kick), 0, 0));
    }

    private static void Spring(ref float value, ref float velocity, float frequency, float dt)
    {
        // Exact critically damped solution; stable through varying frame lengths.
        float intermediate = velocity + frequency * value;
        float decay = Mathf.Exp(-frequency * dt);
        value = (value + intermediate * dt) * decay;
        velocity = (velocity - frequency * intermediate * dt) * decay;
    }

    private void OnDisable()
    {
        if (weapons != null)
        {
            weapons.ShotFired -= OnShot; weapons.WeaponChanged -= OnWeaponChanged;
            weapons.ReloadStateChanged -= OnReloadChanged;
        }
        if (originalShadows != null)
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = originalShadows[i];
        if (weaponCamera != null) weaponCamera.enabled = false;
    }
}
