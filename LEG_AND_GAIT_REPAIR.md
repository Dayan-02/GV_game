# Player legs and zombie sliding repair — 14 October 2026

## What changed

- Player terrain IK now adjusts the IK goal height while retaining animated ankle rotation. Previously, imported foot-bone rotation was incorrectly used as the IK-goal rotation. Corrections are limited to 8 cm, weighted to 0.65, restricted to the Environment layer, and guided by the animated knee. Airborne/dead players release contact.
- Zombie visuals now use a forward-facing wrapper and a corrected Humanoid reference pose. Their previous +X-facing Avatar produced sideways/crossed legs when retargeting ordinary forward locomotion.
- Separate walking and chasing states replace the mismatched walk/run blend. Walking changes to running above 2.7 m/s and returns below 2.4 m/s, with a 0.12-second transition. Stopped enemies use a grounded idle.
- Step cadence follows actual horizontal NavMesh velocity. Measured target-skeleton stance speeds are 1.675673 m/s for walking and 4.528451 m/s for running; visual scale is also included. A Hunter moving at 2.25 m/s now plays the walk at approximately 1.343 times normal speed.
- Native attack/death poses are re-encoded against the corrected Avatar. Combat timers and the four AI decision systems are retained.
- Removed old SurfaceBelt, SurfaceEquipment, KneeProtection and BootSole geometry left outside the animated skeleton. Each enemy prefab has one Animator and one skinned mesh, including all four variants.

## Main files

- `Assets/Scripts/Player/HumanoidFootPlacement.cs`
- `Assets/Scripts/AI/Shared/EnemyAnimationController.cs`
- `Assets/Editor/Art/LegAndGaitRepair.cs`
- `Assets/Editor/Art/GroundedZombieRig.cs`
- `Assets/Editor/Art/GroundedZombieActions.cs`
- `Assets/Animations/GroundedEnemies/`
- `Assets/Prefabs/Enemies/` and `Assets/Prefabs/Player/PlayerPhase3.prefab`
- `Assets/Scenes/ZombieMap.unity`

## Reproduce in Unity

With Play Mode stopped, use **Dead city > Art > Repair player feet and zombie locomotion**. This saves open scenes, regenerates the corrected rig/actions/controllers, updates the five enemy prefabs, configures player feet and saves ZombieMap. Existing licensed assets must remain present. Running older full art-setup menus may restore the older rigs; rerun this repair afterward.

No additional package is required. Player IK needs the existing base-layer IK pass and Environment-layer colliders. Enemy root motion stays disabled because NavMesh controls translation.

## Inspection performed and limits

Inspected sampled Unity walking/running/action poses with CPU-baked skinned meshes and captured the live ZombieMap camera. Live Hunters at 2.25 m/s used GroundedWalk at cadence 1.343; stationary Hunters used GroundedIdle. Read all five prefab structures: one Animator, one skin and no prototype knee/sole meshes each. Player idle leg positions were symmetrical after the IK correction.

No automated tests or full manual gameplay play-through were run. The studio animation preview is sampled motion, not recorded gameplay. Sharp direction changes, uneven terrain, every player movement/aim combination and long sessions still need player review. Cadence matching reduces sliding but does not implement full world-space foot locking or motion warping.

## Manual review procedure

1. Launch the updated Windows executable or enter Play in ZombieMap.
2. Walk, run and sprint; stop and reverse direction. Check knees and ankle orientation.
3. Aim and strafe, jump, land and walk over curbs. Feet should not snap sideways.
4. Watch a slow Hunter approach and stop. Its feet should step instead of remaining rigid while it moves.
5. Observe faster Runner/Ambusher/Elite movement, attack, hit and death. Look for a single visible body and no brown blocks underneath.

Suggested commit: `fix: correct player foot IK and zombie rig gait alignment`
