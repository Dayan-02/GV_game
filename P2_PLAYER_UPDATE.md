# P2 Survivalist player update

Follow-up: [Player leg and zombie gait repair](LEG_AND_GAIT_REPAIR.md) documents the corrected foot IK, zombie reference pose, cadence and current review limits.

Selected character: **Survivalist by Slayver**. Applied to `Assets/Scenes/ZombieMap.unity` and `Assets/Prefabs/Player/PlayerPhase3.prefab`.

## Changes

- Textured Survivalist with cap, vest/pouches and backpack; duplicate first-person arm meshes disabled.
- Dedicated `Assets/Animations/Survivalist/Survivalist.overrideController` and controller.
- Supplied idle, forward walk/run, jump and death motion. Existing Unity-licensed sprint, falling, backward and strafe animations remain available.
- Native Humanoid pelvis mapping uses `pelvis`, not the ground-level `root` bone.
- Camera-relative acceleration/braking and damped yaw; aiming retains strafe movement.
- Walk 1.6 m/s, run 3.6 m/s, sprint 6 m/s, aim movement 1.25 m/s.
- Clip playback rates calibrated from planted-foot travel on the imported humanoid. The exploration tree blends idle/walk only once. Aim directions are normalised against aim movement speed.
- Animation parameter damping 0.035 seconds, turning 0.09 seconds, acceleration 14 m/s², braking 20 m/s².
- WeaponSocket follows the new right hand; left arm support remains driven by each weapon's grip target.
- Corrected Unity finger-channel bindings in generated carry/fire/reload clips.
- Aiming uses a grounded full-body idle; the weapon carry pose applies only to the arms, avoiding the old downward body offset.
- `HumanoidFootPlacement` applies limited terrain corrections near foot contact, fading out in air and on death.

## Controls

WASD move; Shift run; Ctrl sprint; Space jump; right mouse aim; left mouse fire; R reload; existing weapon switching controls remain in place.

## Editor setup

The player root retains its health, movement, weapon, camera-target and gameplay references. Its visual child is now `SurvivalistVisual`. The visual Animator has SupportHandGrip, ShooterBodyPose and HumanoidFootPlacement components.

Use **Zombie Outbreak > Art > Apply selected P2 Survivalist player** to reproduce this assembly. The command saves open scenes, updates ZombieMap and the player prefab, and does not rebuild enemy prefabs. It requires the downloaded assets already present.

The base Animator layer enables IK. Foot placement uses non-allocating physics queries and ignores colliders belonging to the player. Keep terrain colliders present and large obstacles within the controller's step/slope limits.

## Review procedure

1. Open ZombieMap and enter Play Mode. Walk, release movement, turn, then run and sprint.
2. Aim and move forward/backward/sideways; check foot timing and smooth transitions.
3. Jump, land, and walk across road/pavement edges. Watch for foot clipping on unusually high steps.
4. Switch all four weapons, aim up/down, fire and reload. Check the right-hand grip and left-hand support position.
5. Pause/resume and trigger death/restart using normal gameplay.

These are manual review steps, not a record of completed gameplay testing. Clip cadence measurements and model renders support the authoring changes; they do not prove every terrain or weapon pose is artifact-free. Backward motion is still a reversed walk clip; reload/fire are fitted procedural upper-body poses, not newly recorded weapon-specific motion capture.

## Recovery and attribution

Pre-change player scripts, prefab and ZombieMap were archived at `Backups/BeforeP2/PlayerBeforeP2.zip`. Credits and license details are in `THIRD_PARTY_CHARACTER_ASSETS.md`. Imported art does not satisfy any assignment requirement for original student-created models.

Suggested commits:

- `feat(player): integrate selected P2 Survivalist model and materials`
- `fix(animation): correct humanoid pelvis and weapon grip channels`
- `feat(movement): smooth locomotion and calibrate animation cadence`
