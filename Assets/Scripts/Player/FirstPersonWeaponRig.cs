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
    private float recoil, phase, reloadStart, obstruction, equip;
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

    private void OnShot(WeaponShotInfo shot) => recoil = Mathf.Min(.065f, recoil + .025f);
    private void OnWeaponChanged(WeaponData data) { equip = .1f; recoil = 0; reloading = false; }
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
        float blend = cameraRig != null ? cameraRig.AimBlend : aim.IsAiming ? 1 : 0;
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
            float envelope = Mathf.Sin(Mathf.Clamp01(t / .9f) * Mathf.PI);
            target += new Vector3(-.015f, -.13f, -.06f) * envelope;
            targetAngles += new Vector3(18, -10, -18) * envelope;
        }
        float clearance = .95f;
        if (cover.Raycast(worldCamera.transform.position, worldCamera.transform.forward, clearance, out var hit))
            clearance = hit.distance;
        obstruction = Mathf.Lerp(obstruction, 1 - Mathf.InverseLerp(.25f, .85f, clearance), 1 - Mathf.Exp(-18 * dt));
        lower = Mathf.Max(lower, obstruction);
        target += new Vector3(0, -.10f, -.10f) * lower;
        targetAngles += new Vector3(22, 0, -8) * lower;
        recoil = Mathf.MoveTowards(recoil, 0, dt * .22f);
        equip = Mathf.MoveTowards(equip, 0, dt * .6f);
        target += new Vector3(0, -equip, -recoil);
        targetAngles.x -= recoil * 90;
        if (!initialized) { position = target; initialized = true; }
        float response = 1 - Mathf.Exp(-poseResponse * dt);
        position = Vector3.Lerp(position, target, response);
        angles = Vector3.Lerp(angles, targetAngles, response);
        weaponSocket.localPosition = position;
        weaponSocket.localRotation = Quaternion.Euler(angles);
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
