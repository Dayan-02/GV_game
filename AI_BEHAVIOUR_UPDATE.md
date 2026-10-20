# Enemy AI behaviour update — 17 October 2026

## Four distinct roles

| Type | Preserved algorithm | New behaviour |
| --- | --- | --- |
| Hunter | Explicit A* over the navigation grid | Replans on changed evidence, changed target position or failed movement; investigates the last known location and searches up to four reachable nearby positions. |
| Runner | Weighted utility scores with hysteresis | Chooses reachable flank routes on either side, exploits a visible reload, commits to short rushes, recovers after a strike or missed rush, and limits disengagement duration. |
| Ambusher | State machine with tactical position scoring | Scores concealed or rear positions using observed facing/velocity and actual path length, reserves its location, waits for an observable opportunity, turns toward the target during a short warning, and relocates when exposed. |
| Elite | Survival hierarchy above utility scoring | Checks attack feasibility, commits to actions, reserves cover, rallies allies who already see the player, retreats briefly when hurt at low health, and has longer recovery after heavy strikes. |

## Shared rules

- Sight uses a 130-degree horizontal field of view, the existing 45 m maximum range, obstacles and a 2.4 m close-awareness radius. Close awareness still respects walls.
- A sighting or audible event creates 16 seconds of memory. Once sight is lost, target position stays at the last evidence location. Hidden weapon, reload, firing and health facts are not supplied to the brains.
- A bounded 32-event hearing buffer retains recent gunshots, sprint steps and grenade explosions. Each enemy consumes each event once. Hearing gives an investigation location, not visual confirmation of the player.
- Sprint noise defaults to 12 m every 0.65 seconds while actually moving. Gunshots retain their weapon-specific noise radii. Grenades report noise at seven times their damage radius.
- Enemies investigate the authored initial search area before their first detection; this is not a live player tracker. After losing contact, they search locally and eventually idle/scan.
- At most two enemies can be in a committed attack at once. Other nearby enemies use distributed approach positions while a slot or their cooldown is unavailable. Slots are released on completion, cancellation, removal, disable and death. This limits simultaneous attacks, not the total number of nearby enemies.
- Navigation waits for pending paths, reports incomplete/off-mesh routes, detects lack of progress for two seconds, attempts up to two local sidesteps, then rejects that destination briefly. Tactical scoring uses complete paths and path length.
- Existing measured locomotion cadence and contact-timed attack clips remain in use. Enemies face the target before starting a strike. During a strike, turning stops before impact; damage is range/arc/obstacle checked and can occur only once. Heavy attacks get a longer cooldown. No new animation assets were added.

## Inspector tuning

The new serialized fields have code defaults, so existing prefabs/scenes use the improved logic without rebuilding the city or replacing references.

| Component | Setting | Default / existing prefab values |
| --- | --- | --- |
| EnemyWorld | Maximum Attackers / Waiting Radius | 2 / 3.2 m |
| EnemyWorld | Sprint Noise Radius / Interval | 12 m / 0.65 s |
| EnemyPerception | Field Of View / Close Awareness | 130 degrees / 2.4 m |
| EnemyPerception | Memory Seconds / Eye Height | 16 s / 1.5 m |
| EnemyMovement | Stuck Seconds / Local Retry Limit | 2 s / 2 retries |
| RunnerBrain | Commitment / Rush / Recovery | 0.8 s / 2.4 s / 1.2 s |
| AmbusherBrain | Warning / Opportunity Range | 0.6 s / 7 m |
| EliteBrain | Commitment / Heavy Cooldown | 1.3 s / existing 6 s |
| HunterBrain | Replan Interval | Existing 1.5 s, with urgent replans limited to one per 0.45 s |

Existing per-prefab approach speeds remain: Hunter 1.25, Runner caution 1.5, Ambusher travel 1.5 and Elite advance 1.45 m/s. Existing rush speeds remain 4–4.8 m/s. Avoid raising them without checking stride calibration.

Tactical points must be reachable MapMarker objects with kind Cover or Ambush. No spawn markers are used as cover. Places in front of an observed player require physical concealment for an ambush.

## F3 inspection

Aim at an enemy and press F3. The inspector now includes:

- Current intent and knowledge state; remaining memory.
- Observed reload and Elite rally state.
- Attack phase, remaining cooldown and slot ownership.
- Active attack count and the global limit.
- Navigation status, retry count and Elite reserved cover.
- Existing A* expansion/cost, utility scores and Ambusher point scores.

## Code locations

- Assets/Scripts/AI/Shared/EnemyWorld.cs — hearing history, formation positions, reservations, attack limits and rallies.
- Assets/Scripts/AI/Shared/EnemyPerception.cs — field of view, observations, hearing and finite memory.
- Assets/Scripts/AI/Shared/EnemyActor.cs — shared investigation/search and approach helpers.
- Assets/Scripts/AI/Shared/EnemyMovement.cs — path length, progress monitoring and bounded retries.
- Assets/Scripts/AI/Shared/EnemyCombat.cs — feasible attacks, shared slots, pause-safe commitment and heavy recovery.
- Assets/Scripts/AI/Hunter/HunterBrain.cs — event-sensitive A* pursuit.
- Assets/Scripts/AI/Runner/RunnerBrain.cs — utility choices, scored routes and rush recovery.
- Assets/Scripts/AI/Ambusher/AmbusherBrain.cs — opportunity-based ambush state machine.
- Assets/Scripts/AI/Elite/EliteBrain.cs — hierarchical tactics, cover and rally.
- Assets/Scripts/Inventory/GrenadeProjectile.cs — explosion noise event.
- Assets/Scripts/UI/AIDebugPresenter.cs — expanded inspection.
- Assets/Editor/AIBehaviorValidation.cs — isolated Play Mode regression scenarios.

## Validation and human review

Run **Zombie Outbreak > AI > Run isolated behavior validation** outside Play Mode. It uses a temporary scene with a real flat NavMesh and the actual four enemy prefabs, then restores the original scene. Results are written to Logs/AIBehaviorValidation.txt. This is deliberately separate from the authored level.

The checks cover sensory knowledge, memory expiry, multiple hearing events, wall-blocked strikes, early/duplicate impact rejection, cooldowns, group attack limits, reservation cleanup, configurable Runner attack range, real Runner movement/recovery, A* routing, blocked movement and a mixed 20-enemy encounter.

The implementation passed 30 isolated checks. An additional 15-second city integration check passed six assertions with a peak of 24 living enemies and two committed attackers. It checked actual city navigation, FPS weapon initialization and mixed enemy execution without runtime exceptions. Reports: Logs/AIBehaviorValidation.txt and Logs/AICityValidation.txt. This was not a complete five-wave balance run.

Windows build: succeeded with zero errors and one optional Pipeline warning (483,818,492 bytes; 43.935 seconds). The updated executable is Builds/Windows/ZombieOutbreak.exe; the complete distribution is Builds/ZombieOutbreak-Windows.zip.

For a human review in the enhanced ZombieMap:

1. Let a Hunter see you, move behind cover, then quietly change position. It should investigate the old location.
2. Sprint or fire nearby while out of view. Enemies should investigate the sound location.
3. Let a Runner approach, dodge its rush and use the recovery opening. Reload visibly and compare its response.
4. Approach an Ambusher from different directions. Its waiting opportunity depends on sight, proximity and your facing/reload; a timed-out hide causes relocation.
5. Fight an Elite near cover. A wall should prevent a heavy attack, and its heavy strike should leave a longer recovery.
6. Observe a mixed group with F3. The committed attack count must stay at or below two.
7. Review attack telegraphs, turning, footsteps, camera feel and all five waves on the target computer.

Automated logic checks do not establish natural-looking animation, balanced difficulty or target-device frame rate. Those remain human review items.

## Suggested commits

- feat(ai): add observed target memory and bounded hearing history
- fix(ai): gate attacks and recover from failed navigation
- feat(ai): differentiate hunter runner ambusher and elite decisions
- feat(ai): coordinate group attack slots cover and rallies
- test(ai): add isolated behavior regression scenarios and tuning guide

Backup: Backups/BeforeAIBehavior-2026-10-17/AI.zip. The grenade event addition is also visible in the current source snapshot.
