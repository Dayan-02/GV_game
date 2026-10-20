# Phase 2 — Humanoid animation and aiming

This is the Phase 2 setup reference. The current project includes Phase 3 shooting; see [PHASE3_GUIDE.md](<D:/GV project/PHASE3_GUIDE.md>) for current setup, complete code and testing status. Phase 2 originally added aim movement, two camera poses, a Humanoid Animator and animation action hooks; Phase 3 connects those hooks to weapons.

**Updated during Phase 3:** Unity 6000.5.3f1 compiled the project and generated the native Avatar, clips, controller, prefabs and ZombieMap. Three invalid AnimationClipSettings fields in the original builder were corrected. Phase 2 structure validation and Phase 3 ammo, physics and Play Mode checks passed. Physical input and animation feel still need the manual play checklist.

The current complete code is in [PHASE3_SOURCE.md](<D:/GV project/PHASE3_SOURCE.md>). PHASE2_SOURCE.md is an archived earlier snapshot; do not use it to overwrite the current scripts.

## 1. Packages and first setup

This implementation uses Unity 6, built-in Mecanim animation, CharacterController physics, and legacy mouse/keyboard input. Cinemachine and Animation Rigging are optional and are not dependencies of these scripts.

Phase 3 enabled the required built-in Animation, Physics, Audio and Particle System modules through Package Manager. They are recorded in Packages/manifest.json and packages-lock.json. No Asset Store animation pack is required.

Set **Project Settings > Player > Other Settings > Active Input Handling** to **Input Manager (Old)** or **Both**. Keep the standard `Mouse X` and `Mouse Y` axes in Input Manager. Restart if Unity requests it.

Then:

1. Let the scripts finish compiling.
2. Select **Tools > Zombie Outbreak > Phase 2 > Set Up ZombieMap**.
3. Save any modified scene when Unity prompts. Cancelling stops the setup.
4. The command creates missing animation assets, upgrades the player in `Assets/Scenes/ZombieMap.unity`, and saves a separate `PlayerPhase2.prefab`. If ZombieMap is absent, it first runs the existing Phase 1 greybox builder.
5. Select **Tools > Zombie Outbreak > Phase 2 > Validate Active Scene**. Read any validation error before entering Play Mode.
6. Press Play and focus the Game view.

The command preserves an existing ZombieMap environment and does not rebuild it when the scene exists. It preserves existing generated clips, controller assets, and camera mode settings on later runs. It configures the player's components and controller, so save or commit your work before rerunning it. A custom Animator Override Controller based on the generated controller is preserved.

## 2. Files and responsibilities

All paths below are relative to the full project root `D:\GV project\`. The source listing expands every path in full.

| File | GameObject / setup | Responsibility and Inspector values |
| --- | --- | --- |
| `Assets/Scripts/Player/ThirdPersonPlayerController.cs` | Player root, beside CharacterController | Existing movement extended with aim-facing rotation, strafe movement, and animation telemetry. Assign Main Camera, camera rig, GroundCheck, PlayerAimController and PlayerHealth. Walk `2.2`, run `4.2`, sprint `6.5`, rotation `720`, jump `1.25`, gravity `-20`, ground radius `0.2`. |
| `Assets/Scripts/Player/PlayerAimController.cs` | Player root | Owns `IsAiming` and `AimChanged(bool)`. Health references PlayerHealth, aim key is Mouse1, Require Locked Cursor is on, Aim Move Speed is `2.2`. Future crosshair UI can observe this state/event. |
| `Assets/Scripts/Player/ThirdPersonCameraController.cs` | Main Camera with Camera and AudioListener | Updates one shared yaw/pitch and blends between the two camera mode components. Assign CameraTarget, AimTarget, aim controller and both modes. Blend time `0.2`, sensitivity `2.5`, pivot smooth time `0.08`, pitch start `12`, limits `-25..65`, collision radius `0.2`, padding `0.05`, aim ray distance `100`. |
| `Assets/Scripts/Player/ThirdPersonCameraMode.cs` | Exploration Camera and Aim Camera, under Main Camera/Camera Modes | Stores distance, shoulder offset, FOV and sensitivity multiplier. These are virtual poses; do not add Camera or AudioListener components to them. Defaults are listed below. |
| `Assets/Scripts/Player/PlayerAnimatorDriver.cs` | Player root | Assign the child Animator, movement, aim, and health components. Parameter damp time `0.1`. Writes movement parameters after movement Update. Fades the upper-body action layer, exposes action methods, and listens to health death. |
| `Assets/Editor/Phase2/Phase2HumanoidFactory.cs` | Editor utility; do not attach | Builds a T-pose skeleton, maps a valid Humanoid Avatar, attaches rigid greybox meshes to bones, and samples original poses into native Humanoid muscle curves. No Inspector fields. |
| `Assets/Editor/Phase2/Phase2AnimationAssets.cs` | Editor utility; do not attach | Creates 18 clips, nested blend trees, state transitions and an upper-body Avatar Mask. Existing generated assets are reused. No Inspector fields. |
| `Assets/Editor/Phase2/Phase2Setup.cs` | Editor menu command; do not attach | Wires the existing player, creates camera mode objects, attaches WeaponSocket to the right hand, saves a prefab and the scene, and validates setup. No Inspector fields. |

Phase 1 dependencies remain `Assets/Scripts/Player/PlayerHealth.cs`, `Assets/Scripts/Core/IInteractable.cs`, and `Assets/Editor/Phase1SceneBuilder.cs`. Their complete source is included for reference. PlayerHealth and IInteractable were not extended with gameplay systems.

Keep the editor utilities in an `Editor` folder so they are excluded from a player build. Runtime scripts depend only on UnityEngine and each other.

## 3. Humanoid model and Animator setup

If the Player has a valid Humanoid Animator, setup uses it. An existing Animator with an invalid or Generic Avatar produces an actionable error instead of adding a second character. If no Animator exists, setup instantiates the generated jointed humanoid and disables the original `Humanoid_Greybox` visual group.

Generated assets:

```text
Assets/Animations/Phase2/
  PlayerHumanoid.controller
  GreyboxHumanoidAvatar.asset
  UpperBody.mask
  Clips/
    Idle.anim
    WalkForward.anim
    WalkBackward.anim
    StrafeLeft.anim
    StrafeRight.anim
    Run.anim
    Sprint.anim
    Jump.anim
    Falling.anim
    AimIdle.anim
    AimForward.anim
    AimBackward.anim
    AimLeft.anim
    AimRight.anim
    Fire.anim
    Reload.anim
    Hit.anim
    Death.anim
Assets/Materials/Phase2Greybox.mat
Assets/Prefabs/Player/Phase2Humanoid.prefab
Assets/Prefabs/Player/PlayerPhase2.prefab
```

The generated motions are deliberately simple greybox animation, not motion capture or final character art. They contain actual Humanoid curves rather than empty placeholder slots. The Avatar is built and checked using Unity's [AvatarBuilder API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AvatarBuilder.BuildHumanAvatar.html).

On the model's Animator:

- Controller: `PlayerHumanoid.controller`.
- Avatar: valid Humanoid Avatar belonging to that skeleton.
- Apply Root Motion: **off**. CharacterController owns displacement, gravity and jumping.
- Update Mode: **Normal**.
- Culling Mode: **Always Animate** for this small greybox scene.
- Model root: local position zero, local rotation zero, scale one, facing local +Z.

CameraTarget stays on the unanimated Player root so animation does not shake the camera. WeaponSocket is moved under the right hand; its default local position is `(0, 0, 0.08)`. Tune the socket's orientation to an imported hand later. Nothing is attached to it in Phase 2.

## 4. Parameters and blend trees

| Parameter | Type | Meaning |
| --- | --- | --- |
| Speed | Float | Semantic speed: idle `0`, walk `1`, run `2`, sprint `3`, interpolated between them from actual displacement. |
| Horizontal | Float | Actual local horizontal movement divided by walk speed, clamped to `-1..1`. |
| Vertical | Float | Actual local forward/backward movement divided by walk speed, clamped to `-1..1`. |
| IsAiming | Bool | Aim mode is held and player is alive. |
| IsGrounded | Bool | Ground contact after movement. |
| IsSprinting | Bool | Grounded, moving, sprint requested, and not aiming. Exposed for later animation extensions; Speed selects the sprint clip. |
| VerticalVelocity | Float | Signed vertical speed in metres/second. |
| IsDead | Bool | Latched death state used to block other transitions/actions. |
| Fire / Reload / Hit / Death | Trigger | Animation requests only. |

`PlayerAnimatorDriver` supplies these values. Do not attach a second script that writes the same parameters or re-enable root motion.

Base Layer has five states: **Locomotion**, **Aim Locomotion**, **Jump**, **Falling**, and **Death**. Idle, directional walks, Run, Sprint and aim movement are motions inside the blend trees.

Locomotion contains a **1D Speed** tree:

| Threshold | Motion |
| --- | --- |
| 0 | Idle |
| 1 | Nested Walk Direction tree |
| 2 | Run |
| 3 | Sprint |

Walk Direction is **2D Simple Directional**, using Horizontal/Vertical:

| Position | Motion |
| --- | --- |
| `(0, 0)` | Idle |
| `(0, 1)` | WalkForward |
| `(0, -1)` | WalkBackward |
| `(-1, 0)` | StrafeLeft |
| `(1, 0)` | StrafeRight |

Aim Locomotion is also **2D Simple Directional**, using the same parameters:

| Position | Motion |
| --- | --- |
| `(0, 0)` | AimIdle |
| `(0, 1)` | AimForward |
| `(0, -1)` | AimBackward |
| `(-1, 0)` | AimLeft |
| `(1, 0)` | AimRight |

Each directional tree has one motion per direction; speed selection happens in the outer 1D tree. This matches Unity's guidance for [2D Simple Directional blending](https://docs.unity3d.com/6000.0/Documentation/Manual/BlendTree-2DBlending.html).

Transition rules:

- Locomotion → Aim Locomotion: IsAiming true and IsGrounded true; blend `0.15 s`, no exit time.
- Aim Locomotion → Locomotion: IsAiming false and IsGrounded true; blend `0.15 s`, no exit time.
- Either grounded state → Jump: IsGrounded false, VerticalVelocity greater than zero; blend `0.06 s`.
- Either grounded state → Falling: IsGrounded false, VerticalVelocity less than `0.01`; blend `0.06 s`.
- Jump → Falling: VerticalVelocity less than `0.01`; blend `0.1 s`.
- Jump/Falling → appropriate grounded state: IsGrounded true and matching aim flag; blend `0.1 s`.
- Any State → Death: Death trigger plus IsDead true; blend `0.12 s`; no self-transition or outgoing transition.
- All ordinary base-layer transitions require IsDead false.

Jump, Fire, Reload, Hit and Death do not loop. The other clips loop. Death holds its final pose; restarting Play Mode resets it.

## 5. Aim movement and camera modes

| Input | Result |
| --- | --- |
| WASD | Camera-relative movement |
| Left Shift | Run in exploration |
| Left Control | Sprint in exploration; takes precedence over Shift |
| Space | Jump |
| Hold right mouse | Aim; rotate toward camera yaw, strafe instead of turning toward movement |
| Mouse | Shared orbit direction in both camera modes |
| Escape | Unlock cursor and exit aim |
| Left click | Relock cursor; no firing |

Aiming caps movement at Aim Move Speed and disables sprint. Releasing aim restores movement-facing rotation and the normal walk/run/sprint policy. Camera input runs first, then aim intent, then movement, then Animator parameter updates. The camera follows in LateUpdate.

| Camera mode | Distance | Shoulder X/Y | FOV | Sensitivity multiplier |
| --- | --- | --- | --- | --- |
| Exploration Camera | `4.5` | `(0.2, 0.05)` | `60` | `1.0` |
| Aim Camera | `2.25` | `(0.6, 0.08)` | `45` | `0.65` |

The positive shoulder offset moves the lens to the right, leaving the character toward the left side of the view. Both profiles share yaw/pitch so switching cannot reset the view. One Main Camera renders and owns the AudioListener. The collision solve runs after the profile and pivot blend, accounts for the near plane, ignores the player's hierarchy and triggers, and retracts the camera before an obstacle.

AimTarget is placed on the camera's center ray, using the nearest environment collider or a point 100 metres away. This ray positions a transform only; it does not implement hits or damage. `AimChanged(bool)` and `IsAiming` are the future crosshair hooks.

Cinemachine is not installed in this repository, so the implementation extends the existing camera without a new package. Exploration Camera and Aim Camera are custom virtual pose components, not CinemachineCamera components. If the team later migrates to Cinemachine, use its Brain as the sole output-camera owner and remove this rig's transform updates before connecting two virtual cameras; do not run both camera drivers at once.

## 6. Fire, Reload, Hit and Death hooks

Call `PlayerAnimatorDriver.PlayFire()`, `PlayReload()`, or `PlayHit()` from later gameplay. These set Animator triggers only. There are no keys bound to weapon actions, no weapon objects, and no ammo logic.

The **Upper Body Actions** layer is Override and uses `UpperBody.mask`. Body, head, arms and fingers are enabled; root, legs and IK are disabled. Its Empty state has no motion. Any State transitions to Fire, Reload or Hit on the matching trigger while IsDead is false. Each action returns to Empty at normalized exit time `0.9`. The driver fades layer weight toward one during actions and back to zero afterward; this prevents an empty override layer from freezing the last upper-body pose.

`PlayerHealth.Died` calls `PlayDeath()`. The driver also checks for an already-dead player in Update, so re-enabling it cannot miss death. Death is a full-body Base Layer state; it clears action triggers and suppresses the upper layer. The movement and aim components stop accepting input when PlayerHealth is dead. Gravity continues so an airborne dead player can settle to the floor.

In Play Mode, the PlayerAnimatorDriver component's context menu provides **Animation Preview** entries for Fire, Reload, Hit and Death. The death preview calls PlayerHealth.Die; the other entries only animate.

## 7. Expected hierarchy

```text
ZombieMap
├── Directional Light
├── Environment                         [Phase 1 greybox]
├── Player                              [CharacterController, movement, health, aim, animator driver]
│   ├── Humanoid_Greybox                 [old visual, disabled if generated rig is used]
│   ├── Humanoid_Phase2                  [Animator; valid Humanoid Avatar]
│   │   └── Hips
│   │       ├── Spine / Chest / Neck / Head
│   │       │   ├── LeftShoulder / LeftUpperArm / LeftLowerArm / LeftHand
│   │       │   └── RightShoulder / RightUpperArm / RightLowerArm / RightHand
│   │       │       └── WeaponSocket     [empty]
│   │       ├── LeftUpperLeg / LeftLowerLeg / LeftFoot / LeftToes
│   │       └── RightUpperLeg / RightLowerLeg / RightFoot / RightToes
│   ├── CameraTarget
│   │   └── AimTarget
│   └── GroundCheck
└── Main Camera                         [Camera, AudioListener, ThirdPersonCameraController]
    └── Camera Modes
        ├── Exploration Camera          [ThirdPersonCameraMode]
        └── Aim Camera                  [ThirdPersonCameraMode]
```

The arms branch from Chest; slash-separated names above abbreviate chains. Greybox meshes are children of the bones they represent. An imported model can have different bone names and nesting as long as its Avatar mapping is valid.

## 8. Replacing the greybox animations

Import a model with Rig > Animation Type **Humanoid**, then Configure the Avatar and confirm its required bones and T-pose. Import animation clips as Humanoid too. Use Copy From Other Avatar only when the animation file has the matching skeleton; otherwise configure its own valid Humanoid Avatar for retargeting.

Create an Animator Override Controller based on `PlayerHumanoid.controller`, replace its 18 motions, and assign it to the Animator. This keeps the parameter names, blend trees and hooks intact. The setup command preserves an override based on this controller.

Use in-place clips with looping enabled for locomotion/aim/falling and disabled for one-shot actions/death. Bake root XZ movement and rotation into pose and keep Apply Root Motion off. Check root Y settings for your jumping/falling clips so they don't fight the physical controller. Match the model's forward axis and scale. The generated clips are for functional greybox presentation; stride lengths and hand poses will need tuning for final art.

## 9. Animation Rigging later

For a later hand-placement pass, Unity Animation Rigging can add a RigBuilder and a Rig layer after the Animator. A **Two Bone IK** constraint on each arm can place the hand on a grip target; an elbow hint controls bending direction. See Unity's [Two Bone IK documentation](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html).

Keep grip targets on the held object's hierarchy, constrain the support hand to its grip, and avoid a dependency cycle where a hand drives a socket that drives the same hand's target. A chest/head aim constraint can later follow AimTarget and improve vertical aiming. Blend rig weight with aim/reload state. Phase 2 leaves these as preparation guidance; it does not install the package or build a weapon rig.

## 10. Testing steps

These are the required Unity checks after generation, not results claimed from this machine.

| Check | Action and expected result |
| --- | --- |
| Setup and rerun | Run Set Up ZombieMap, then Validate Active Scene. Run setup a second time: no duplicate humanoid or camera modes; existing generated clip GUIDs remain stable. |
| Avatar and curves | Inspect Animator Avatar and preview Idle/Walk/Sprint. The joints move; the character is not stuck in a T-pose. Validate reports a human Avatar and 18 nonempty Humanoid clips. |
| Exploration | Move W/A/S/D. Character turns smoothly toward travel, Speed approaches 1. Diagonal speed matches straight movement. |
| Run and sprint | Move with Shift and Control separately: Speed approaches 2 then 3, motions change, IsSprinting is only true when actually moving fast on the ground. |
| Aim idle | Hold right mouse while stationary: IsAiming becomes true, arms adopt the aim pose, player turns with the camera, and camera blends to its right shoulder. |
| Aim movement | Hold right mouse and press W, S, A and D individually, then diagonals. W uses AimForward, S uses AimBackward, A/D strafe while the torso keeps facing the camera yaw. |
| Aim speed | While aiming, hold Shift/Control. Aim movement stays at 2.2 and IsSprinting remains false. |
| Transitions | Repeatedly press/release right mouse during motion and orbit. Camera yaw remains continuous, FOV/offset/distance blend smoothly, no second AudioListener warning occurs. |
| Jump and fall | Jump in both modes; Jump starts on ascent, Falling on descent, then the appropriate ground tree on landing. Walk off a raised greybox block to enter Falling directly. |
| Ceiling and walls | Jump under a block: upward velocity is cleared on ceiling contact. Walk into a wall: actual speed falls toward zero instead of playing a full-speed walk. |
| Camera collision | Aim and switch modes next to a wall; orbit around corners. The camera retracts rather than crossing solid colliders. Test at wide and narrow Game view aspect ratios. |
| Upper actions | Use the driver component's Fire/Reload/Hit preview entries in Play Mode. Watch Upper Body Actions transitions and weight return to zero. No projectile or damage is produced. |
| Death | Use Death preview. Death plays once, stays in its last pose, upper actions are suppressed, and WASD/right mouse no longer animate locomotion. Restart Play to reset health. |
| Cursor/focus | Escape releases cursor and aim; keyboard motion stops. Left click relocks. Alt-tab does not leave aim stuck. |
| Retargeting | On a copy of the scene, assign a valid imported Humanoid model and an override controller. Repeat direction/jump/action checks and tune stride/socket offsets. |

## 11. Common animation bugs

| Symptom | Fix |
| --- | --- |
| Scripts cannot find Animator, CharacterController, or AudioListener | Enable Animation, Physics and Audio in built-in packages; this scaffold initially has an empty package manifest. |
| Input exception about Active Input Handling | Select Input Manager (Old) or Both and restart if prompted. |
| Setup command missing | Fix Console compile errors and keep all builder scripts under `Assets/Editor/`. |
| T-pose or frozen limbs | Check Animator.enabled, valid Humanoid Avatar, correct controller, nonempty clips and mapped required bones. Run Validate Active Scene. |
| Avatar creation error | Check skeleton names, unscaled root, complete required bones and T-pose. Generated skeleton uses its own mapped Avatar; don't assign an unrelated Generic Avatar. |
| Moonwalking/backward clip while strafing | Confirm model faces +Z, signed Horizontal/Vertical bindings, and correct clip coordinates. Do not use raw world X/Z values as local blend parameters. |
| Foot sliding | Use in-place clips, tune their playback rate/stride to controller speeds, and preserve Apply Root Motion off. Generated greybox motions are approximate. |
| Double movement or floating | Remove competing movement/OnAnimatorMove scripts; only CharacterController moves the Player root. Review imported clip root-Y settings. |
| Aim camera centers the character again | Do not call LookAt(CameraTarget) after the rig runs. Its orbit rotation deliberately preserves the shoulder offset. |
| Camera shake with walking | Keep CameraTarget off animated head/hips; no second script or Cinemachine Brain should also move Main Camera. |
| Arms stay stuck after a trigger | Check the Empty state and upper-layer weight returning to zero; driver must reference the correct Animator/layer name. |
| Walking restarts after death | Death must have no outgoing transition; ordinary transitions require IsDead false. Use PlayerHealth.Die for gameplay death rather than only an animation trigger. |
| Grounded flickers next to walls | Keep GroundCheck just above feet, include ground colliders in the mask, and retain the downward probe and slope-normal filtering. |
| Pink greybox | Use a material shader matching the active renderer. The factory supports Built-in Standard and URP/Lit; custom pipelines need a compatible material. |
| Imported override ignored | Base it on PlayerHumanoid.controller and assign it to the model Animator, not the root driver. |

## 12. Phase 2 checklist

- [x] Aim intent component and a future UI event.
- [x] Camera-relative aim strafing and camera-facing yaw; movement-facing exploration.
- [x] Exploration Camera and Aim Camera virtual poses with smooth distance/offset/FOV switching.
- [x] Camera obstruction handling during switching and orbiting.
- [x] Humanoid skeleton, Avatar and native animation generation code.
- [x] Idle, directional walk, run, sprint, jump, fall, aim idle and directional aim motion definitions.
- [x] 1D exploration speed tree and two 2D directional trees.
- [x] All requested Animator parameters plus action/death hooks.
- [x] Upper-body action layer/mask and full-body terminal death state.
- [x] Scene upgrade, prefab creation, setup validation, full source and manual test guide.
- [x] C# syntax check passed for all 11 scripts.
- [ ] Generate native assets in a working Unity Editor and pass setup validation.
- [ ] Complete the Play Mode checks above and inspect the actual greybox animation.

## 13. Suggested Git commit messages

- `feat(player): add aim strafing and animation movement state`
- `feat(camera): blend exploration and shoulder aim camera modes`
- `feat(animation): add humanoid clips and locomotion blend trees`
- `feat(animation): add masked action hooks and death state`
- `chore(unity): add Phase 2 scene upgrade and setup validation`
- `docs: document Phase 2 setup source and play-mode checks`

Commit generated `.controller`, `.anim`, `.mask`, `.asset`, `.prefab`, `.unity`, package lock/settings changes, and their `.meta` files after running Unity setup. Keep Library, Logs, Temp, and UserSettings out of Git. No commits or remote pushes were made by this task.
