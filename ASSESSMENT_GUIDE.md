# Assessment guide — Graphics & Visualization / Intelligent Systems

## What is implemented

The prototype is a 3D third-person, over-the-shoulder Unity shooter with humanoid animation, modular weapons, an event-driven HUD, a navigable industrial district, interactions, four distinct agent algorithms, pre-game loadout/equipment, five data-driven waves, pause, Game Over and Victory.

The runtime source is under `Assets/Scripts`; Editor authoring/check tools are under `Assets/Editor/Outbreak`. Full source files are included in the project. `DEVELOPMENT_STATUS.md` records each phase's setup, changes, limits and suggested commits.

## Four distinguishable agent contributions

| Owner | Files / primary method | Algorithm | Observable decision |
|---|---|---|---|
| Member 1: Hunter | AI/Hunter/HunterNavigationGraph.FindPath; HunterBrain | Informed A* over sampled NavMesh graph | Follows waypoints, remembers sensed position, throttles replanning, stops/retries incomplete routes |
| Member 2: Runner | AI/Runner/RunnerBrain.Score | Normalized utility scores with weights and hysteresis | Chases weak players, respects a close shotgun, flanks or disengages under threat |
| Member 3: Ambusher | AI/Ambusher/AmbusherBrain.ChoosePoint | Tactical position evaluation + state sequence | Chooses concealed/flanking/interception points, reserves one, waits briefly then strikes |
| Member 4: Elite | AI/Elite/EliteBrain.Score and Update | Priority hierarchy above utility | Critical health triggers bounded retreat; otherwise selects heavy attack, push, spacing or reposition |

Shared health, NavMesh movement, combat, perception, animation and registry code is in **AI/Shared**, not duplicated into each member's folder. This folder layout describes intended ownership; it is not a claim that any particular student has already written or reviewed the generated code.

### Member 1 viva: A*

- Nodes are sampled on the NavMesh in a 2 m grid. Eight-neighbour edges are usable only when a NavMesh ray says the segment is clear.
- `g(n)` is accumulated Euclidean edge length. `h(n)` is Euclidean distance to the goal. `f(n)=g(n)+h(n)` determines the next open node.
- Euclidean h does not overestimate the geometric path cost, so it is admissible for this graph. This says nothing about optimality on every possible continuous route outside the sampled graph.
- Parent pointers reconstruct the path. NavMeshAgent follows those waypoints and performs local avoidance; the explicit A* implementation remains visible and explainable.
- Bounded expansion, staggered replanning and dynamic edge validation limit work. Limitations: coarse passages, linear open-list scan and an expansion ceiling can reject a route.
- Show Inspector path gizmos, expanded-node count and path cost. Explain a blocked/unreachable case rather than claiming the algorithm always finds a path.

### Member 2 viva: utility AI

- Sensors produce normalized threat, weakness and distance values. The score function is separately callable and checked with known sensor inputs.
- Actions: Chase, Attack, Caution, Flank, Disengage. Weights change priorities; hysteresis and a minimum decision interval reduce rapid switching.
- With full player health and a close shotgun, caution/flank utility rises. With low player health, chase utility rises and caution falls. Low own health increases disengage utility.
- Candidate movement targets must have complete routes. The fallback stops or approaches cautiously when a flank point is unreachable.
- Explain utility as a hand-authored decision system, not machine learning.

### Member 3 viva: tactical evaluation

- Authored MapMarker transforms encode useful locations, not decisions.
- Reject unreachable and occupied points. Score remaining points by relative angle, concealment, travel distance, preferred player distance and predicted player travel.
- Default weights: flank 0.30, concealment 0.25, own travel 0.15, interception 0.15, proximity 0.15.
- Reserve the winning point in a scene-scoped registry. Death/disable releases it so another agent can use it.
- The state sequence Observe → Evaluate → Move → Wait → Strike → Reposition gives the score practical meaning. Wait duration is bounded so the final enemy cannot hide indefinitely.
- Limitations: linear short-term motion prediction and hand-authored tactical points.

### Member 4 viva: hierarchy and utility

- The hierarchy first rejects dead/busy actors and handles critical-health retreat. Utility selection handles the remaining valid behavior choices.
- Utility considers range, player weakness, shotgun threat, firing, nearby allies, route availability and heavy-attack cooldown.
- A heavy attack has longer windup and greater damage; the shared impact gate checks actual range/cover at the moment of damage.
- Retreat lasts three seconds with a 12-second cooldown. This prevents an endless survival loop.
- Nearby allies influence a push decision. This version does not implement explicit subordinate orders or claim a learned strategy.

## Demonstration plan

1. Start from MainMenu. Change primary and equipment; show the correct weapon and counts in ZombieMap.
2. Show camera-relative movement, shoulder aiming, weapon switching, ammo/reload UI, cover blocking, medkit and grenade.
3. Press **F3** to enable the optional AI Inspector. Aim at an agent, otherwise the nearest living actor is selected. Show its sensors, decision and algorithm-specific values. Disable with F3 for the final play demonstration.
4. Use the Editor Scene view to inspect MapMarker locations, navigation and the Hunter's optional Draw Path gizmos.
5. Demonstrate a close shotgun versus an injured player for Runner; two Ambushers selecting exclusive points; Elite retreat/heavy attack and ally-sensitive push.
6. Show all five wave assets with exact counts, the spawn queue plus live registry, a death callback, Victory and restart.
7. Show the original team models, native Blender/Maya files, UV/material work and WIP evidence. This item remains manual until those files are supplied.

## Graphics evidence map

| Criterion | Existing evidence | Remaining team work |
|---|---|---|
| 3D third person and humanoid motion | Camera, rig, Animator controllers, shoulder aiming | Human input/animation-quality review with final rig |
| Environment and navigation | Warehouse, office, maintenance building, streets, alley, cover, barricades and marker-based routes | Final artistic composition and detail |
| Lighting | Moon, warm street/interior lights, emergency flicker, readable ambient fill | Target-device visual approval |
| Materials | Procedural concrete/asphalt/brick/wood/metal/window/grime treatments | Original textures/material refinement as needed |
| Atmosphere / VFX | Fog, bounded smoke/dust/fire/embers, muzzle/impact/hit/death, grenade explosion/smoke/flash | Human particle/audio presentation review |
| Two team custom models | Integration architecture and written handoff | **Original zombie and barricade authoring, native files, screenshots and named attribution** |
| Performance | Throttled sensors/decisions, shared graph, registries, bounded particles, one shadowed key | Profiler/Frame Debugger evidence on submission hardware |

## Verification evidence and its limits

- Native Unity compilation and per-phase scene/asset checks completed through the implementation stages.
- Native Play Mode tests cover four live weapon categories and shotgun pellets, ammo/reload conservation, muzzle/camera cover handling, shared enemy lifecycle, utility score relationships, reservations, weapon/health sensing, timed melee, grenade multi-collider damage/occlusion, barricades and camera collision.
- The complete-flow harness navigates real scenes and completes all **77** required spawns, verifies category counts, triggers Victory, restarts cleanly and verifies Game Over. It accelerates time/spawning and applies test damage; it is not an unaided human survival playthrough.
- A 60-search A* microbenchmark in the local Editor recorded **0.795 ms mean, 1.595 ms p95, 2.993 ms max, zero route failures** before the final sparse atmosphere addition. These measure path searches, not whole-game frame rate.
- Logs and visual captures are in the ignored `Logs` directory. Keep selected screenshots/measurements in the team's submission evidence folder with context and date.
- Windows build and launch results are recorded in `VERIFICATION_REPORT.md` after completion.

## Team contribution record

| Member | Reviewed/modified files | Design reasoning | Test/video evidence | Commit(s) |
|---|---|---|---|---|
| Member 1 | To fill after actual work | A* | Pending human evidence | Pending |
| Member 2 | To fill after actual work | Utility AI | Pending human evidence | Pending |
| Member 3 | To fill after actual work | Tactical evaluation | Pending human evidence | Pending |
| Member 4 | To fill after actual work | Hierarchy + utility | Pending human evidence | Pending |

Do not fabricate authorship, commits, WIP screenshots or modeling provenance. Each member should be able to explain and modify their algorithm and identify shared versus individual work.

## Suggested Git sequence

Commit `Assets` including `.meta` files, `Packages`, `ProjectSettings` and docs. Exclude Library, Temp, Logs and Builds. Use separate changes per feature or member's actual contribution. Do not regenerate/delete `.meta` files to fix merge conflicts; resolve references and scene changes deliberately.

```text
feat(environment): build zombie map combat greybox
feat(environment): add doors physics props and destructible barricades
feat(ai): add shared enemy perception movement combat and health
feat(ai-hunter): implement explicit A-star hunter navigation
feat(ai-runner): add utility-based runner decisions
feat(ai-ambusher): add tactical position scoring and reservations
feat(ai-elite): implement adaptive commander decision hierarchy
feat(loadout): add pregame selection medkits and throwable grenades
feat(waves): implement five-wave spawning and event-driven HUD
fix(ai): integrate four agents with shared navigation and lifecycle events
feat(graphics): add readable industrial night lighting and combat effects
feat(animation): synchronize humanoid attacks reloads and terminal death
fix(integration): repair menu references and verify complete game flow
docs(assessment): add AI inspection model handoff and verification evidence
```

No Git commits were created automatically. The repository's existing uncommitted work remains available for team review.
