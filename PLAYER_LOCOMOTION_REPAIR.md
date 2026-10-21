# Grounded player movement repair — 14 October 2026

## Cause and changes

The recorded hopping also appeared when the supplied P2 run played alone, with IK and upper-body layers removed. On the actual P2 mesh its lowest boot ranged from **-0.146 m to +0.145 m** relative to the animation ground plane. The P2 walk also penetrated the floor, and the old sprint retargeted poorly. These findings establish a clip/retargeting problem; changing IK alone could not repair it.

The existing CharacterController has an 8 cm skin width, while the visual root originally had no offset. The corrected visual root accounts for that contact offset. Movement speeds and physics remain controlled by the existing motor.

Implemented:

- Separate `Assets/Animations/PlayerGrounded/PlayerGrounded.controller` and override controller; original motion assets retained.
- Target-rig grounded idle, walk, backward/strafe/aim movement and a restrained jog derived from the existing Unity locomotion assets. Jog uses a phase-aligned 25% walk contribution to reduce the bounding gait.
- Root-height curves fitted to the visible boot mesh, followed by retargeted contact refinement. The jog retains a brief small flight phase; it is not a jump animation.
- Walk/jog footfall phase aligned across blends, with playback calibrated to the measured target-skeleton stride. Sprint uses the same consistent jog at increased cadence rather than the old unstable sprint retarget.
- Contact curves for left/right feet. Terrain IK uses smooth contact weights, retains animated ankle rotation, applies gentle knee guidance, and skips correction on the level ground already handled by the clips.
- Player prefab and current enhanced scene use the new controller and visual offset. Weapon/action masks, jump/fall/death clips and gameplay references are retained.

No additional Unity package or downloaded animation is required. Existing free-asset credits still apply. Backups: `Backups/BeforePlayerGaitRepair/PlayerGaitBefore.zip`.

## Authoring and settings

Menu: **Tools > Dead city > Repair player locomotion**.

- `Assets/Editor/Art/PlayerLocomotionRepair.cs` generates and applies the corrected clips/controllers.
- `Assets/Scripts/Player/HumanoidFootPlacement.cs` consumes contact curves and limits terrain corrections.
- `Assets/Editor/Art/PlayerLocomotionReview.cs` performs a bounded runtime animation/physics replay with synthetic velocity inputs and scene-camera captures; runtime changes are not saved.
- Current scene: `Assets/Scenes/EnhancedCity/ZombieMap.unity`.
- Visual root Y offset: minus CharacterController skin width, currently -0.08 m.
- Foot IK weight 0.45, maximum terrain correction 0.06 m, Environment/World layer 8, animation contacts enabled.
- Animator parameter damping 0.025 seconds. Existing walk/run/sprint targets stay 1.6/3.6/6 m/s; aim movement stays 1.25 m/s.

## Evidence and limits

Isolated corrected clips were measured with the capsule skin offset accounted for and runtime IK disabled: idle/walk boot contact approximately **4 mm** above the plane; jog lowest-boot clearance **3.7–38.9 mm**. This is a substantial reduction from the original run's penetration/float range. These measurements concern the lower of the two boots; the swinging foot remains lifted.

Native side-view candidate and corrected renders are in `Previews/PlayerGaitRepair/`. Runtime replay results and captures are recorded under `Logs/PlayerRuntimeGait.txt` and `Logs/Captures/PlayerRuntime_*`. These are deterministic sampled/replayed animation views, not a human keyboard/mouse recording. See `VERIFICATION_REPORT.md` for the final result and build status.

The final runtime replay passed through walk, run, sprint, aim forward, aim left, aim backward and deceleration to idle with the actual animation driver, weapon layers and foot IK active. Across that replay the lower boot's clearance ranged from **-36.6 mm to +41.3 mm**; transitions still have small contact error, so this is not a claim of perfect foot locking. Frame captures were visually reviewed for the large hopping and reversed-knee behavior. Earlier harness field lookup and inactive-editor startup issues were corrected before the successful isolated replay.

Review manually: walk, run, sprint, accelerate/brake, aim forward/backward/diagonally, reverse direction, turn quickly, jump/land and cross curbs. Confirm weapon alignment remains usable during movement and that knees do not snap. This repair does not implement motion matching or full world-space foot locking on every terrain shape.

Suggested commit: `fix: ground player locomotion and align walk run footfalls`
