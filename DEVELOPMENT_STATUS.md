# Zombie Outbreak — implementation record

Unity 6000.5.3f1. Continuing the existing Phases 1–4; Phase 18 integration workflow updated 10 October 2026.
This record distinguishes implemented systems, native Unity checks and remaining human assessment work.

## Character, equipment and motion update — 12 October 2026

- Applied seven procedural surface families with normal/smoothness maps to the player, four enemy variants and all four weapons.
- Added tapered clothing meshes, plate carriers, pouches, belts, knees/boot details and distinct weapon assemblies with bevelled receivers, sights, trigger guards, magazines and support-hand markers.
- Refined generated player motion and added separate Hunter/Runner/Ambusher/Elite gait overrides while retaining shared combat timing and Humanoid rig references.
- Added optional support-arm alignment after weapon aiming, with smooth release during reload, air movement and sprint.
- Five new C# files; source handoff regenerated with 94 complete scripts. No automated gameplay suite was added or run for this update. Native art/animation previews were rendered and inspected.
- Settings, source file roles, manual review steps and limitations: [CHARACTER_WEAPON_ART.md](CHARACTER_WEAPON_ART.md).
- Suggested commits: `feat(art): texture characters and add distinct weapon visuals`, `feat(animation): refine humanoid locomotion and enemy gait variants`, `feat(player): add weapon support-hand alignment`.

## Abandoned-city art update — 10 October 2026

- Replaced flat environment finishes with six photographic surface sets and added four types of scanned street prop, with CC0 source records.
- Added boarded facades, warehouse structure, a surrounding skyline, wrecked cars, street drainage, litter, cables and wet patches. The saved scene is `Assets/Scenes/ZombieMap.unity`.
- Refined night lighting, distance fog and camera grading; preserved existing movement, weapons, AI, collider roots and navigation.
- Improved the generated humanoid silhouettes and clothing while retaining their rigs and controllers. Original team character/barricade models and detailed weapon art remain outstanding.
- Added five modular C# files, two shaders and an asset retrieval script. The current source handoff now contains 89 C# files.
- Full art controls, changed-file responsibilities and licensing: [ABANDONED_CITY_ART.md](ABANDONED_CITY_ART.md).
- Suggested commit: `feat(graphics): add weathered abandoned-city environment and horror lighting`.

## Evidence and reproducibility

- Original Phase 1–4 setup tools and assets are retained.
- Native scene/prefab/data assets are generated with Unity Editor APIs, preserving existing GUIDs.
- Per-phase Editor logs are under `Logs` (ignored by Git); summarized results belong in this document.
- AI ownership: Shared components support separate Hunter / Runner / Ambusher / Elite folders.
- Generated greybox characters and weapons are development placeholders, not evidence of team Blender/Maya modeling.
- No commits are attributed to human team members by automation.

## Progress

| Phase | Status | Scope |
|---|---|---|
| 5 | Passed | Native compilation, mesh bake and all marker reachability checks |
| 6–13 | Passed | Native compilation and scene/prefab generation; algorithm checks |
| 14 | Passed | Native Play Mode checks for all four agents and shared lifecycle |
| 15 | Passed | Night district, sparse atmosphere, combat effects and captured visual review |
| 16 | Passed | Humanoid enemy clips and synchronized action controllers |
| 17 | Passed | Flow, combat, 21-agent stress, camera, UI and four-weapon checks |
| 18 | Passed technical checks | Debug display, empty team-art catalog, model validation/import menu, Windows build and packaged startup; team modeling/human assessment tasks remain |

## Phase 5

Created `Assets/Scripts/Environment/MapMarker.cs`, `Assets/Editor/Outbreak/OutbreakEditor.cs`, `Assets/Editor/Outbreak/Phase5Setup.cs`.
Updated package bootstrap to install `com.unity.ai.navigation@2.0.15` through Unity Package Manager.

The map builder preserves the old environment as an inactive archive. The new 76 m district has an open combat street, cross street, warehouse, office, maintenance interior, alley, defensive positions, movable-prop placeholders and an elite arena marker. Four entrances per interior provide alternate routes. AI consumes transform markers.

Editor setup: run **Zombie Outbreak > Phase 5 - Build district** once on the existing project. Subsequent calls retain the authored district and rebuild its NavMesh. Layer 8 is World, layer 9 is Enemies. The navigation surface collects World colliders under OutbreakDistrict. Keep player and enemy colliders out of the bake.

Check: every marker must sample to the mesh and have a complete path to PlayerSpawn. In Play Mode traverse both streets and all three interiors, jump against boundaries, and inspect cover height relative to the humanoid.

Limitation: greybox appearance; art and movable carving are later phases.

Suggested commit: `feat(environment): build zombie map combat greybox`

## Phase 6

Created `InteractiveDoor`, `DestructibleBarricade` under Environment, `PlayerInteractor`, `PlayerPhysicsPush` under Player and `Phase6Setup` under Editor/Outbreak. Updated ZombieMap native scene and navigation data.

Use E within 3.2 m to open/close a door; locked doors expose a locked prompt. The hinge rotates at 140 degrees/s through 100 degrees. Crates weigh 35 kg, barrels 55 kg and barricades 70 kg; Rigidbody interpolation, continuous collision and damping stabilize movement. NavMeshModifier excludes moving objects from the permanent bake; NavMeshObstacle provides dynamic carving. Barricades have 100 health and visibly topple before removal. Door API allows future enemies to open unlocked doors.

Setup: **Zombie Outbreak > Phase 6 - Add interactions**. Do not combine a dynamic Rigidbody and NavMeshAgent on an enemy. For a new door place the root at its hinge, put the solid leaf underneath, and put the obstacle on the leaf. Configure lock, angle and speed on InteractiveDoor.

Test: E opens/closes; a locked door stays closed; push props from the side; shoot a barricade until it collapses; walk through the cleared opening. Inspect the blue NavMesh with dynamic carving in Play Mode.

Limitations: interaction prompt is connected during HUD integration; debris is a falling placeholder until team art is supplied.

Suggested commit: `feat(environment): add doors physics props and destructible barricades`

## Phase 7

Created `Assets/Scripts/AI/Shared/Enemy{Health,World,Actor,Movement,Perception,Combat,AnimationController}.cs`, `Assets/Editor/Outbreak/Phase7Setup.cs`, `EnemyBase.prefab`, humanoid avatar and enemy Animator Controller. Scene gains EnemySystems with explicit player references and a live registry.

Health implements IDamageable and emits hit/death events once. Movement wraps a NavMeshAgent, throttles destinations, probes complete paths and opens nearby unlocked doors. Perception samples every 0.2 s: distance, line of sight, recent noise, weapon type and player weakness. Combat uses a windup, one idempotent impact gate and cooldown, checking range/occlusion again at impact. Death cancels combat/navigation, clears colliders and removes the actor from the registry. Animation listens to these events.

Setup: **Zombie Outbreak > Phase 7 - Shared enemies**. Spawned actors must receive `Initialize(EnemyWorld)`; the later WaveManager supplies it. Base settings: health 60, radius 0.4 m, height 1.9 m, attack range 1.8 m, damage 10, windup 0.5 s, cooldown 1.6 s. No Rigidbody on agents. Enemy World obstruction mask is World only.

Test: instantiate on the baked NavMesh and initialize; damage twice past zero and observe one death callback; attack a player in range, leave during windup, and confirm no remote hit. Killing the player must stop all registered movement and attacks.

Limitations: no decision brain in the shared prefab. Attack clips are temporary reused placeholders until Phase 16; final team character art remains outstanding.

Suggested commit: `feat(ai): add shared enemy perception movement combat and health`

## Phase 8 — Member 1: Hunter

Created `AI/Hunter/HunterNavigationGraph.cs`, `HunterBrain.cs`, Editor/Outbreak `EnemyPrefabFactory.cs`, `Phase8Setup.cs`, and `Hunter.prefab`. Scene gains a shared sampled navigation graph.

Actual A*: each edge cost g is distance travelled, h is Euclidean distance to the goal, and the lowest f=g+h open node is expanded. The 2 m grid is sampled once from the baked NavMesh; edges are checked against current carving. A per-search expansion ceiling and 1.5 s replan interval bound work. A failed complete route stops the Hunter and retries later. NavMeshAgent handles local avoidance between chosen waypoints; it does not replace the A* decision algorithm.

Setup: **Zombie Outbreak > Phase 8 - Hunter A-star**. Graph bounds are Inspector data, not destination coordinates embedded in the brain. Hunter speed 2.25 m/s, health 60. Select an active Hunter and enable Draw Path for yellow waypoint lines; ExpandedNodes, PathCost and actor Decision support explanation.

Test: all six entrance-to-player routes are checked by the builder. In Play Mode lead a Hunter around a warehouse, close/move an obstacle and inspect replanning. An unreachable target must not cause rapid destination retries or console errors.

Limitations: a coarse grid can reject narrow passages that a NavMeshAgent alone could use; maintain at least 2 m usable passage width. Open-list selection is intentionally readable linear scanning, bounded to the small assignment map.

Suggested commit: `feat(ai-hunter): implement explicit A-star hunter navigation`

## Phase 9 — Member 2: Runner

Created `AI/Runner/RunnerBrain.cs`, `Editor/Outbreak/Phase9Setup.cs` and `Runner.prefab`. Shared systems remain unchanged. Normalized distance, own health, player weakness, shotgun threat, firing activity, visibility and path availability produce Chase / Attack / Caution / Flank / Disengage scores. Inspector weights adjust aggression, survival and tactics; hysteresis reduces oscillation.

Setup: **Zombie Outbreak > Phase 9 - Runner utility AI**. Health 45, chase 4.8 m/s, caution 2.5 m/s, decision interval 0.4 s. A close shotgun raises caution/flank scores; an injured player raises chase utility. Repositioning candidates are checked for complete paths.

Test: compare score snapshots with rifle and shotgun, then lower player health; damage the Runner to raise survival utility. The builder checks the shotgun/health relationships directly. Watch a Runner approach, circle or disengage instead of following the Hunter route at a different speed.

Limitations: utility tuning is intentionally simple and requires human gameplay balance feedback; no learned model is claimed.

Suggested commit: `feat(ai-runner): add utility-based runner decisions`

## Phase 10 — Member 3: Ambusher

Created `AI/Ambusher/AmbusherBrain.cs`, `Editor/Outbreak/Phase10Setup.cs` and `Ambusher.prefab`. The agent observes, evaluates, travels, waits, strikes and repositions. Each authored point is scored for flank angle, concealment, travel distance, preferred player distance and predicted player travel; unreachable or occupied points are rejected. EnemyWorld owns exclusive reservations and releases them on death/disable.

Setup: **Zombie Outbreak > Phase 10 - Tactical Ambusher**. Defaults: 70 health, travel 3.1 m/s, strike 4.4 m/s. Weights are visible on the prefab. Add MapMarker objects with Ambush kind and include them in EnemyWorld.tacticalPoints. Tactical destinations are transform references.

Test: spawn two Ambushers and inspect different reserved points. Move and turn the player to change flank/interception scores. Kill an occupant and verify another can reserve the point. Waiting is bounded to four seconds, then the agent attacks; it cannot hold the last wave open indefinitely by hiding.

Limitations: movement prediction is linear over 1.5 seconds; tactical points are hand-authored rather than generated from every piece of cover.

Suggested commit: `feat(ai-ambusher): add tactical position scoring and reservations`

## Phase 11 — Member 4: Elite

Created `AI/Elite/EliteBrain.cs`, `Editor/Outbreak/Phase11Setup.cs`, and `Elite.prefab`. A priority hierarchy handles death/busy combat and critical-health retreat before selecting weighted direct attack, heavy attack, push, distance maintenance or repositioning. Inputs include allies within 12 m, weapon threat, player weakness, path availability, firing and heavy-attack cooldown. Retreat lasts three seconds and has a 12 s cooldown.

Setup: **Zombie Outbreak > Phase 11 - Elite commander**. Health 350, movement 2.8 m/s, push 4 m/s, attack range 2.4 m, base damage 16 (heavy multiplier 1.8), windup 0.7 s, heavy cooldown 6 s. The larger visual also has a larger collider/agent radius. Its spawn belongs only in wave five.

Test: ready heavy attack should win at close range; reduce health below 25% to see bounded retreat; switch to shotgun to raise spacing; add allies to increase push utility. Shared combat still rechecks cover and range at impact.

Limitation: commander behavior adapts to nearby allies but does not issue a separate network of orders to them.

Suggested commit: `feat(ai-elite): implement adaptive commander decision hierarchy`

## Phase 12 — Loadout and equipment

Created `Inventory/LoadoutCatalog`, `LoadoutSession`, `GrenadeProjectile`; `Player/PlayerLoadoutApplier`, `PlayerEquipment`; `UI/LoadoutMenuController`; `Editor/Outbreak/Phase12Setup`; native MainMenu/Loadout scenes, catalog asset and grenade prefab. Updated weapon controller with a before-Awake loadout configuration API and reserve resupply API; reused existing UI factory methods.

MainMenu -> Loadout -> ZombieMap is in the build scene list. Choose Rifle/SMG/Shotgun plus a fixed Pistol and either Medic (3 medkits + 1 grenade) or Assault (1 medkit + 3 grenades). A single DontDestroyOnLoad session persists validated selection; runtime ammo/counts reset on a fresh gameplay scene. Direct opening of ZombieMap uses catalog defaults.

Setup: **Zombie Outbreak > Phase 12 - Loadout and equipment**. PlayerLoadoutApplier runs before weapon Awake. Q heals 45 up to max, consumes nothing at full health. G throws a continuous-collision Rigidbody grenade at 14 m/s with a 2.5 s fuse, 6 m radius, 100 center damage with falloff and physics impulse. Grenades damage enemies; this version has no self-damage. Explosion effect/audio fields are optional hooks. Reserve awards preserve per-slot ammo state.

Test: change both choices, enter the map, inspect the visible primary and switch to Pistol. Restart to confirm fresh ammo/equipment with the same selection. Heal near max and at full health. Throw against a wall and near multiple colliders on one enemy: damage must apply once per enemy. Pause freezes fuse and reload clocks.

Limitations: no inventory screen, loot or save-to-disk persistence. Equipment packs are selectable bundles, not an inventory grid.

Suggested commit: `feat(loadout): add pregame selection medkits and throwable grenades`

## Phase 13 — Five waves

Created `Waves/WaveDefinition.cs`, `WaveManager.cs`, `UI/GameStatusPresenter.cs`, `Editor/Outbreak/Phase13Setup.cs` and five native wave data assets. Updated ZombieMap's live HUD bindings.

| Wave | Title | Hunter | Runner | Ambusher | Elite |
|---|---|---:|---:|---:|---:|
| 1 | OUTBREAK | 8 | 0 | 0 | 0 |
| 2 | RUNNERS | 8 | 5 | 0 | 0 |
| 3 | THE HUNT | 8 | 5 | 3 | 0 |
| 4 | THE SIEGE | 12 | 5 | 4 | 0 |
| 5 | THE COMMANDER | 10 | 5 | 3 | 1 |

Setup: **Zombie Outbreak > Phase 13 - Five waves**. Initial preparation 6 s, spawn interval 0.7 s, intermission 7 s. Entrances are serialized markers, checked for NavMesh projection, occupancy and 8 m player distance. WaveManager tracks its own living set through one death subscription per enemy. HUD Remaining includes queued and living enemies. Completion requires the spawn queue to finish and the living set to empty. Player death stops the sequence; final completion calls GameUIController.ShowVictory.

Between waves, each carried weapon receives two reserve magazines, making all primary choices sustainable without adding a loot/inventory system. The HUD shows the supply announcement. Equipment counts and E prompt are event-driven; the only presenter Update expires announcements.

Test: kill the first enemy while others are still queued; the next wave must not start. Verify exact counts, six spawn entrances, pause/fuse behavior, one Elite in wave 5, Victory after its final death, and scene restart resetting all runtime counts.

Limitations: blocked/missing entrances abort with an explicit diagnostic after ten attempts; they never silently skip required enemies. Human balance testing remains necessary.

Suggested commit: `feat(waves): implement five-wave spawning and event-driven HUD`

## Phase 14 — Agent integration

Added `Editor/Outbreak/Phase14Checks.cs`. Updated EnemyWorld to retain the actual shot payload (including its original noise radius), EnemyPerception to remember observations, and Hunter to search the initial spawn/last known position rather than tracking an unseen moving target continuously.

Shared registry and tactical reservations are scene-scoped. All agents read the same player/weapon snapshot and use shared damage, path movement and timed melee. Dead actors release reservations, stop decisions/combat and leave the live registry. Player death stops all registered agents. Decisions are staggered/throttled: Hunter 0.15 s plus 1.5 s path replanning, Runner 0.4 s, Ambusher 0.35 s, Elite 0.45 s; perception 0.2 s. No runtime scene-wide enemy searches.

Native Play Mode check covers all four moving agents, low health, weapon switching, unreachable target, exclusive reservations, door interaction, medkit capping and exactly-once death cleanup. Run headlessly with `-executeMethod Phase14Checks.RunBatch` without `-quit`; the harness owns process exit and never saves runtime edits.

Manual stress test: hold a doorway with many agents, move crates into a route, retreat through alternate exits, and watch decisions with the later debug overlay. Capture Unity Profiler CPU/Physics evidence on the team's target machine; no frame-rate claim is made from headless checks.

Suggested commit: `fix(ai): integrate four agents with shared navigation and lifecycle events`

## Phase 15 — Graphics and effects

Created `Environment/FlickeringLight.cs`, `Core/CombatEffects.cs`, `Editor/Outbreak/Phase15Setup.cs`; procedural surface textures/materials, six bounded particle prefabs, lamps, street markings, vehicles, window panels, signs and debris. Updated weapon muzzle hooks, grenade effect hook and ZombieMap's lighting and navigation bake.

Night uses a cool directional moon, warm unshadowed street pools, interior lamps, a flickering emergency lamp and linear distance fog. Ambient fill keeps navigation readable. Concrete, asphalt, brick, timber, metal and dirty window materials are separated. Only the moon casts real-time shadows; shadow distance is 40 m, pixel light count four, particles have bounded counts. The hit/death/impact effect manager pools 16 instances per effect. Environment smoke is limited to 60 particles.

Setup: **Zombie Outbreak > Phase 15 - Night district and effects**. Moving props remain outside permanent navigation geometry; the builder rebakes and checks all marker routes after adding vehicles. Read `TEAM_MODEL_HANDOFF.md` for the two required team-authored models. No external model or texture asset was invented or attributed to a student.

Test: check readable silhouettes against the street and interior walls, verify muzzle/impact/hit/death/explosion particles, orbit the shoulder camera near props and doors, and inspect materials for pink shaders. Review point-light overlap and particle counts in Unity Profiler/Frame Debugger. Static flags are set on fixed dressing; baked occlusion/lightmaps require manual baking after final team art stabilizes.

Limitations: original procedural placeholders are not final graphics or team Blender/Maya evidence. No claim of baked occlusion, global illumination bake, final audio or measured target-device FPS.

Suggested commit: `feat(graphics): add readable industrial night lighting and combat effects`

## Phase 16 — Animation integration

Updated `Phase2HumanoidFactory` to author distinct zombie idle/walk/run/attack/hit/death and Elite heavy poses; added `Editor/Outbreak/Phase16Setup.cs` and seven native Humanoid clips. Updated EnemyCombat duration, EnemyAnimationController gait speed, PlayerAnimatorDriver damage/reload hooks and PlayerWeaponController reload timing.

Setup: **Zombie Outbreak > Phase 16 - Synchronize animations**. Enemy locomotion blends at 0/1.5/3.5 m/s with GaitScale above 3.5. Attacks use a 1 s source clip, scaled to `windup / 0.6`; damage is gated at normalized time 0.6 and cannot happen twice if an imported clip adds AnimationImpact. Heavy duration scales by 1.4. Death is terminal and suppresses attack/hit triggers. Player reload uses `ReloadSpeed = 1.2 / weapon.reloadTime`, while authoritative ammo remains in WeaponAmmoState. Weapon switching cancels the action layer. Player death finishes using unscaled Animator time behind the paused Game Over panel.

Test: compare attack arm extension with health loss, leave range during windup, verify recovery from Hit, run Runner faster than Hunter without root-motion drift, check all four weapon reload durations and cancellation, and kill an attacking agent. Inspect transitions for a terminal death state with no exits.

Limitations: jointed procedural Humanoid visuals are placeholders. Final skinned models and hand IK require the supplied team rig; no fragile rigging package dependency was added. Later Animation Rigging can constrain the support hand to a weapon grip marker and use a multi-aim chest/arm rig, without moving decision logic into Animator.

Suggested commit: `feat(animation): synchronize humanoid attacks reloads and terminal death`

## Phase 17 — Optimization and regression fixes

Created native Editor test harnesses `Phase17Checks`, `Phase17MechanicsChecks`, `Phase17StressChecks`, capture helper `OutbreakCapture`, and final integration builder `Phase17Setup`. Updated the existing four-weapon Play Mode fixture to isolate itself from the new two-slot loadout and waves without saving those temporary edits.

Fixed two scene-generation issues found by actual Play Mode runs: equipment components were not marked dirty before changing scenes, and menu catalog references could be unloaded while creating scenes. Phase12Setup now saves the gameplay scene explicitly and repairs catalog references in existing menu scenes. Added clear diagnostics for missing presenter/menu fields. Other integration changes: dead agents disable their NavMeshAgent, optional debug shot rays are off, practice targets are inactive, barrels have cylinder visuals, terminal/pause panels return to MainMenu, and MainMenu has Quit. The camera collision mask is World only so a nearby zombie does not force a sudden close-up.

Verified: full scene flow with exact 77 spawns; Victory; restart; player death; health/ammo/weapon/crosshair HUD; medkit and grenade counts; all four weapons/pellets; ammo conservation; cover and saturated ray buffers; timed melee and range escape; idempotent impact; grenade multi-collider damage and wall occlusion; barricade break; camera collision. A 21-agent concurrent run moved all 21, observed a moving player and weapon switch, and reported no runtime errors.

Optimization: no runtime enemy scans, explicit event lifetimes, shared graph/registry, staggered perception and decisions, rate-limited path requests, bounded particle pools and a single shadowed key light. New C# source was formatted with the installed compiler's syntax tools and checked to preserve every code token. Final build compilation rechecks it.

Manual procedure: use the input and balance checklist in PLAY_AND_SETUP.md. Headless/accelerated tests cannot replace unaided human survival and final target-device profiling. No fabricated FPS claim is made.

Suggested commit: `fix(integration): repair menu references and verify complete game flow`

## Phase 18 — Assessment and handoff

Created `UI/AIDebugPresenter.cs`, `Editor/Outbreak/Phase18Setup.cs`, `ASSESSMENT_GUIDE.md`, `PLAY_AND_SETUP.md`, `TEAM_MODEL_HANDOFF.md`, `VERIFICATION_REPORT.md` and current full-source/index documents. The optional F3 display reads the selected/nearest actor's sensors, action and algorithm-specific scores. It is off by default, refreshes at 4 Hz only when visible, and can be disabled entirely on the HUD component. Four clear member folders remain separate from Shared.

Native validation checks scene order, missing scripts, four prefab compositions, enemy motions, player reload parameter, supported material shaders and marker reachability. A local 60-search A* benchmark records mean/p95/max and failures in Logs/NavigationBenchmark.txt. Build Windows Player produces the complete runnable folder under Builds/Windows. Build results and launch outcome are summarized in VERIFICATION_REPORT.md.

Manual assessment work: create and integrate the team's original zombie and barricade in Blender/Maya, retain source/WIP evidence, perform human balance/input/visual review and record each member's actual code contribution. Documentation distinguishes generated placeholders and automated tests from this outstanding human evidence.

Suggested commit: `docs(assessment): add AI inspection model handoff and verification evidence`

### Additional sparse atmosphere completion

`Phase15Atmosphere` and `TransientLight` add bounded fire, embers and warehouse dust; broken-wall and grime details; grenade smoke and a short unshadowed flash. The completed map was rebaked and marker reachability passed again. These complete the requested Phase 15 effects without a new gameplay system.

## Team model integration (10 October 2026)

Added `Assets/Scripts/Environment/TeamArtCatalog.cs`, `Assets/Editor/Outbreak/TeamArtProjectSetup.cs` and `Assets/Editor/Outbreak/TeamArtIntegration.cs`. The Editor setup creates `Assets/Data/TeamArtCatalog.asset` plus the zombie and barricade source/export/texture folders. The validation menu checks Humanoid avatar, mesh/material readiness and visual-only component constraints. The apply menu swaps supplied models into the shared enemy visual and scene barricade visuals, retaining common gameplay components and rebuilding navigation.

No team-authored model assets have been supplied in this workspace. The catalog is empty and greybox remains in use. Record real author/source details and WIP evidence; do not mark the authorship flag without team confirmation. See `TEAM_MODEL_HANDOFF.md`.

## Imported character and gun replacement (12 October 2026)

Replaced the live procedural mannequins with a Microsoft Rocketbox skinned human and Pixelhouse rigged zombie, and replaced all four gun visuals with the CC0 M4A1, Grease Gun, Shotgun and Makarov. Existing gameplay roots and four distinct AI implementations remain. The earlier statement that greybox visuals remain is historical; the empty team-authorship catalog is still accurate.

Imported Humanoid locomotion, repaired the legacy zombie Avatar scale/facing, created masked carry/finger/fire/reload poses, added camera-pitch torso motion and retained support-hand IK. Native weapon material conversion connects albedo, normal and compressed metallic/smoothness textures. Cosmetic bolt/slide/pump/magazine motion uses existing firing/reload hooks. No inventory/wave/AI system was replaced.

Current setup and limitations: REALISTIC_CHARACTER_ART.md. Third-party sources, modifications and licenses: THIRD_PARTY_CHARACTER_ASSETS.md. Student Blender/Maya modeling evidence remains a separate handoff requirement. Build and visual-review results are recorded separately in VERIFICATION_REPORT.md; no gameplay suite was run for this art request.
