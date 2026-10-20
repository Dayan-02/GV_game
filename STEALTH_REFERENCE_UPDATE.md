# Movement and art update from the supplied gameplay video

Reference reviewed: the user's local `Mastering Stealth _ The Last of Us Part 1 _ Combat Tips.mp4` (2:00, 1280×720). The opening street traversal, low crouched movement and later garden sequences informed this pass. No footage or proprietary game assets are included in the project or build.

## Controls and behavior

- **C:** toggle crouch. Standing is blocked beneath low ceilings. Jump and sprint require standing.
- **WASD:** existing camera-relative movement. Crouch travels at **1.05 m/s**; crouched aiming travels at **0.85 m/s**.
- **Right mouse:** aim; crouched aim supports forward, backward and sideways movement. Existing fire, reload and weapon-switch controls remain available.
- Normal carry lowers the rifle. Aim blends back to the established shoulder pose.
- Acceleration **9 m/s²**, braking **13 m/s²**, turn smoothing **0.12 s**. Movement slows when facing away from the requested travel direction.
- Camera eases down **0.38 m** while crouching; its collision solver remains active. The muzzle obstruction origin follows stance height too.

## Animation implementation

`Assets/Animations/StealthLocomotion/ReferenceSurvivor.overrideController` retains the calibrated standing locomotion and action clips. Its base controller adds `IsCrouching`, a five-point directional crouch blend tree and short stance/aim transitions.

The existing licensed Survivalist crouch-walk clip is retargeted to the corrected Avatar, lowered with a two-bone leg solve, and recalibrated against the actual boot mesh. Backward movement reverses the forward cycle. Sideways movement adapts the existing grounded strafe clips. The crouched idle derives from a planted two-foot idle; it does not freeze a raised foot in mid-step. Playback rates match measured stance travel at the configured crouch speed.

These are adapted clips and procedural corrections, not new motion capture. In particular, backward and sideways crouching remain approximations and need human review in sharp turns and uneven terrain. This update does not establish animation or asset parity with the reference game.

## Runtime files

- `Assets/Scripts/Player/PlayerStanceController.cs`: stance intent, ceiling check, capsule resizing, shared camera blend and stance event. Obstruction mask is **World (layer 8)**. Standing capsule remains **1.8 m**, crouched capsule **1.55 m**; the capsule base stays fixed.
- `ThirdPersonPlayerController.cs`: crouch speed, sprint/jump restrictions and turn-dependent acceleration target.
- `PlayerAnimatorDriver.cs`: stance parameter and velocity-driven crouch blending; compatible with controllers lacking `IsCrouching`.
- `HumanoidFootPlacement.cs`: penetration-only crouch correction uses evaluated ankles and pre-baked boot sole samples because generated muscle clips do not supply reliable imported foot-goal curves. Lifted swing feet retain their animation. The samples are serialized in the prefab/scene; no runtime mesh read/write import setting or per-frame mesh baking is needed. When replacing the boot mesh, run **Bake boot sole contact samples** on the component in Edit Mode and save the prefab/scene.
- `ThirdPersonCameraController.cs`: stance-height offset before camera collision handling.
- `Assets/Scripts/Weapons/PlayerWeaponController.cs`: stance-adjusted barrel obstruction origin.
- `Assets/Editor/Art/StealthLocomotionSetup.cs`: asset/controller authoring through Unity APIs. `Apply` is a one-time setup; `RefineCrouch` updates existing clips while retaining references.
- `StealthPosePreview.cs` and `StealthLocomotionReview.cs`: isolated pose review and synthetic Play Mode replay.

All paths above are relative to `D:/GV project`. Complete current source is provided in `CURRENT_SOURCE.md`.

## Environment

The enhanced gameplay scene remains `Assets/Scenes/EnhancedCity/ZombieMap.unity`. Added fern fronds, leaf litter, wall-base shrubs and denser ivy sit inside existing roadside/facade footprints. The foliage shader now adds small vein and color variation. Road dampness and moss are adjusted; puddles retain the road's base texture/tint.

All **6,762** pre-existing environment transforms and **126** collider definitions were preserved. Navigation data is unchanged. Added detail contributes **85,160 triangles** before culling; this is a geometry count, not a frame-rate measurement. No new asset purchases or packages are required.

## Review in Unity

1. Open the enhanced scene, enter Play Mode and click the Game view.
2. Press C while idle; inspect both feet during lowering and standing. Move forward, turn, stop and change direction.
3. Hold right mouse while crouched and move in all four directions. Fire, reload and switch each weapon.
4. Crouch under a low obstacle; pressing C must keep the player crouched until the overhead space clears.
5. Stand, then walk/run/sprint and jump. Pause and resume while crouched.
6. Inspect curb transitions, close walls, camera clipping, extreme aim pitch and performance on the demonstration computer.

Synthetic review evidence and build results are recorded in `VERIFICATION_REPORT.md`. Native previews are under `Previews/StealthReference`. The replay supplies velocity/aim values directly, so it does not establish keyboard/mouse feel.

## Scope

The four enemy behaviors, wave rules and existing layout remain intact. No listen mode, takedown, grab/drag or stealth-detection system was introduced. Existing character meshes and licensed animation sources are retained.

## Suggested Git commits

- `feat(player): add safe crouch stance and directional animation blending`
- `fix(animation): calibrate crouch contact and lower exploration weapon carry`
- `art(city): add roadside ferns ivy and damp surface detail`
- `docs: record video reference changes and review evidence`

Include all new `.meta` files. Keep generated Logs, Previews, Backups and Builds outside Git, consistent with this repository's ignore rules.
