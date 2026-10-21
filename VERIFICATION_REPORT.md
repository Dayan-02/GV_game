# Verification report

## Enemy decision improvements — 17 October 2026

- Implemented finite observation-based perception, field of view, hearing history, sprint/explosion noise, investigation/search, movement failure recovery, two simultaneous attack slots, distributed waiting positions, exclusive cover and Elite rallies.
- Preserved A* Hunter, utility Runner, tactical-state-machine Ambusher and hierarchical/utility Elite. Added bounded rush/recovery, opportunity-driven ambushes, action commitment, valid heavy-attack selection and attack-facing checks.
- Unity C# compilation completed with zero errors. Older art-preview code still emits deprecated-API warnings; the new AI validator uses the current lookup API. Isolated Play Mode validation passed 30 assertions using the real enemy prefabs and a temporary NavMesh. Evidence: Logs/AIBehaviorValidation.txt.
- Windows x64 build succeeded: **0 errors, 1 warning, 483,818,492 bytes, 43.935 seconds**. Evidence: Logs/AIBehaviorBuildSummary.txt. The warning concerns the optional Pipeline connection in standalone builds.
- City integration passed six checks over 15 gameplay seconds; peak living enemies 24, peak simultaneous committed attackers two. FPS weapon initialization and navigation worked; no runtime exceptions were recorded during the check. Evidence: Logs/AICityValidation.txt.
- Existing attack clips and measured locomotion cadence are retained. Natural-looking animation, human difficulty, a complete five-wave play-through and target-device frame rate remain review items. This is not a claim of completed human balance testing.
- Full settings, script ownership and suggested commits: AI_BEHAVIOUR_UPDATE.md. Backup: Backups/BeforeAIBehavior-2026-10-17/AI.zip.

## Distinct enemy types and attacks — 16 October 2026

- Saved distinct derived body meshes, fitted skinned outfits and accessories for Hunter, Runner, Ambusher and Elite, plus the Hunter-compatible EnemyBase. Separate normal/heavy clips are assigned through each existing override controller. Assets: Assets/Art/EnemyIdentity. Full details: ENEMY_IDENTITY_ATTACK_UPDATE.md.
- Corrected attack playback from 1/duration to actual clip length/duration; the previous shared clip was 2.5667 seconds. New clips have contact at normalized 0.56. Added committed strike facing and a frontal damage arc; range, wind-up, damage and cooldown values were retained. These dodge changes require human balance review.
- Native Unity studio lineup and 24 normal attack samples rendered and reviewed; corrected initial clothing intersections with fitted skinned torso meshes. Preview video: Previews/EnemyIdentity/AttackPreview.mp4. This is a posed authoring preview at half speed, not live pursuit/combat evidence. Heavy, death, hit and all terrain combinations were not visually reviewed.
- Windows x64 build succeeded: **0 errors, 1 warning, 483,804,492 bytes, 35.610 seconds**. Evidence: Logs/EnemyIdentityBuildSummary.txt. The warning concerns the optional Pipeline connection in standalone players. No automated gameplay test suite was added or run.
- FPS player and city layout remain. The prefabs reuse the existing licensed zombie with new derived geometry/outfits, not four newly acquired character packs. Backup: Backups/BeforeEnemyIdentity-2026-10-16/Enemies.zip.

## First-person conversion — 16 October 2026

- Saved the enhanced ZombieMap with capsule-based eye positioning, mouse-facing movement, hip fire/aim zoom, separate FPS arms and weapon camera, crouch eye height, procedural weapon presentation and event-driven crosshair behavior. Existing city layout and game systems retained. Complete settings and review procedure: FIRST_PERSON_CONVERSION.md.
- Reviewed Play Mode Game View with the rifle, dedicated Survivalist gloves/sleeves and HUD. Reviewed a separately posed rifle aiming still. Images are under Previews/FirstPerson. These stills do not establish keyboard/mouse feel or complete four-weapon combat behavior.
- Windows x64 build succeeded: **0 errors, 1 warning, 480,222,508 bytes, 40.460 seconds**. Evidence: Logs/FirstPersonBuildSummary.txt. The build warning concerns the optional Pipeline connection in standalone builds.
- C# compilation passed. A clean Play Mode session initialized the rifle, two active FPS arm meshes and both cameras with complementary layer masks; the subsequent Editor log tail contained no new exceptions. Script reimport during an earlier preview session invalidated transient weapon state and produced null-reference messages in that session; restart Play Mode after editing scripts. No automated gameplay tests were added or run.
- The original map SHA-256 remains BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431. Backup: Backups/BeforeFirstPerson-2026-10-16/PlayerAndScene.zip.
- Hands-on movement, all four weapons, near-cover combat, reload/switch transitions and target-device performance still need review. Reload hand motion is procedural; no new motion-captured reload asset was added.

## Overhead bridge removal — 15 October 2026

- Removed the pictured service bridge group and its 51 descendants from the saved enhanced ZombieMap. Removed its creation code and two bridge-only combined mesh assets. The adjacent facade group and existing original map remain.
- Bridge colliders: 0. Existing NavMeshSurface collects PhysicsColliders, so its navigation source is unchanged. The build's existing navigation validation completed.
- Windows x64 build succeeded with 0 errors, 1 warning, 480,186,460 bytes. See `Logs/BridgeRemovalBuildSummary.txt` and `BRIDGE_REMOVAL.md`.
- Scene and authoring source were backed up at `Backups/BeforeBridgeRemoval-2026-10-15/BridgeAndScene.zip`.

## Player shaking correction — 15 October 2026

- Live before-observation: 946 frames / 10.0065 seconds, stable root Y=0.0800 m, grounded throughout; lateral hip range 0.6167 m. Captured from the user's actual walk/run input.
- Corrected private player source imports to share Unity's original defaultmale_rigAvatar. Preserved P2 target model and all gameplay architecture. Run right-foot twist normalized muscle range changes from roughly -4.20..+0.19 to -0.374..-0.021.
- Removed linear root XZ travel before retargeting; preserved native muscle and foot-goal tracks. Final sampled run lateral hip span 0.0122 m; walk 0.0445 m; sprint 0.0479 m. Actual source knees flex through about 69 degrees in walking and 124–130 degrees in running.
- Current-Animator three-angle preview completed 240 frames / 8 seconds, reviewed side/front/rear. Gameplay scripts/physics disabled in this authoring preview. It does not prove all runtime terrain behavior. The user subsequently completed the live walk/run review and confirmed it looks smooth. The after-recorder produced zero frames; after-values are authoring measurements, not runtime telemetry.
- Initial import/render attempts hit native graphics crashes. Main editor reopened with Direct3D 11; isolated preview completed after closing the main city editor (exit 0).
- Windows x64 build succeeded: 0 errors, 1 warning, 480,276,492 bytes, 16.112152 seconds. Updated Windows build and ZIP. Evidence: Logs/PlayerJitterBuildSummary.txt, Logs/PlayerJitterBuildEditor.log, Logs/WalkRunCorrection.txt.
- Full source/settings/details: WALK_RUN_ANIMATION_FIX.md. Original scene SHA-256 unchanged. No automated gameplay suite added or run.

## Earlier walk/run attempt — superseded after user reported shaking

- Preserved the existing player, motor, camera and Animator. Replaced only exploration walk/run/sprint overrides with separate, target-rig clips. Removed the 25% walking mixture from running and the 39 mm running-flight clamp. Sprint no longer repeats the modified jog at 1.756×.
- Calibrated stance from backward travel of the supporting boot. Existing motor speeds remain 1.6/3.6/6 m/s; clip multipliers are 1.0396/1.0998/1.2967. Walk/run/sprint phase offsets 0/0.05/0.008333. Direction damping 0.08 s; Speed follows the already-smoothed actual displacement without extra lag.
- Native current-Animator visual preview: 240 frames, 8 seconds, side/rear views over a moving ground grid. `Previews/WalkRunRepair-2026-10-15/CorrectedPlayerMovement.mp4`. This is an authoring preview with scripted speed progression, not keyboard/physics/terrain gameplay verification.
- Initial main-Editor preview render crashed after 125 frames. The correction was already saved. The complete preview subsequently rendered successfully in the isolated CharacterReview project (exit 0).
- Windows x64 build succeeded with zero errors. See `Logs/WalkRunBuildSummary.txt` and `Logs/WalkRunBuildEditor.log` for the current report.
- Original ZombieMap SHA-256 remains BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431. No new test suite or manual gameplay pass was run. Sharp turns, curbs and slopes need hands-on review. Complete corrected scripts and exact settings are in `WALK_RUN_ANIMATION_FIX.md`; current source snapshot contains 123 C# files.
## Infected movement update — 14 October 2026

- Found that the earlier grounding repair had assigned survivor idle/walk/run to every zombie. Re-encoded the licensed native zombie walk against the corrected Avatar and authored 12 infected idle/shamble/chase clips with four controllers/overrides. Added independent starting phases; preserved attack/hit/death timing and NavMesh ownership of world translation. Details: `INFECTED_MOVEMENT_UPDATE.md`.
- Slower approach speeds now fit the short shamble: Hunter **1.25 m/s**, Runner caution **1.5**, Ambusher travel **1.5**, Elite advance **1.45**. Fast rush speeds remain **4–4.8 m/s**. This changes approach balance; no balance play-through was performed.
- Full Humanoid pose/body recording, explicit root-height settings and calibration against each actual prefab mesh resolved the sinking visible in initial authored poses, including the differently scaled Elite. Final native Editor sheets: **48 frames, 192 sampled poses**. Lowest skin vertex above the flat studio floor was **+3.8 to +25.0 mm** for shamble and **+1.0 to +62.7 mm** for chase; airborne chase phases account for some positive clearance. These are authoring pose measurements, not runtime terrain/contact assertions. Evidence: `Logs/InfectedPreviewBounds.txt` and `Previews/InfectedMotion/`.
- Per-variant calibrated stride speeds recorded in `Logs/InfectedMotionAuthoring.txt`; no global Animator speed change affects combat clips. Native previews reviewed after the final correction.
- **Windows x64 build succeeded:** zero errors, one warning, **480,208,204 bytes**, **26.539 seconds**. Evidence: `Logs/InfectedMovementBuild.txt` and `Logs/InfectedMovementBuildWarnings.txt`. The warning disables the optional Pipeline Editor connection in standalone builds.
- **122 C# files** in the updated source snapshot/index. No new asset download, character mesh replacement, player movement edit or environment layout change. Original map SHA-256 remains `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`.
- No automated gameplay tests, human play-through, runtime transition/terrain replay or frame-rate benchmark were run. The motion sheets sample clips in place and do not demonstrate NavMesh travel, turn contact, stopping or complete wave balance. Unity remains in Edit Mode on the enhanced map.

## Realistic city graphics update — 14 October 2026

- Imported CC0 Poly Haven covered vehicles, ferns, shrubs and overcast HDR sky. Applied Linear lighting, filmic HDR camera presentation, dirty window glass, flat worn road paint, improved character surface settings and baked local reflections. See `REALISTIC_GRAPHICS_UPDATE.md`.
- **Layout preservation:** 6,796 existing environment transforms, 126 collider definitions and the NavMesh reference retained. Added 138 scanned foliage renderers; obsolete grass/puddle renderers hidden. Evidence: `Logs/RealismLayout.txt`. Original scene SHA-256 is still `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`.
- All 31 downloaded asset files matched provider MD5 and byte count; the closed 33-file source package matched its SHA-256 roster before import. Source/author/license records and receipt are under `Assets/Art/Realism`. Canonical game-dev CLI unavailable; the existing Unity importer was used.
- Native Unity Editor camera renders reviewed after final materials/lighting/reflection updates: `Previews/Realism/RealismAfter.png` and `RealismStreet.png`. These are scene stills, not gameplay/performance measurements. Native shader diagnostics contain no messages: `Logs/RealismShaderMessages.txt`.
- **Windows x64 build succeeded:** zero errors, one warning, **477,735,804 bytes**, **66.226 seconds**. Evidence: `Logs/WindowsBuildSummary.txt` and `Logs/RealismBuildWarnings.txt`. The standard Build Windows Player menu now delegates to the enhanced-city build helper.
- Unity is left with `Assets/Scenes/EnhancedCity/ZombieMap.unity` open in Edit Mode. Earlier Unity sessions had been displaying the original `Assets/Scenes/ZombieMap.unity`, which omits this upgrade.
- **120 C# files** are recorded in the current source snapshot/index. No new gameplay tests, manual controls play-through, full-wave run or target-device FPS benchmark was performed for this graphics pass. Historical movement/gameplay results below predate this pass. Existing meshes and simple architectural geometry remain limits on realism.

## Supplied-video movement and environment update — 14 October 2026

- Reviewed the user's local 2-minute Last of Us Part I combat-tips video, including street traversal, crouching and overgrown outdoor areas. Applied a reference-inspired pass using existing licensed assets; this is not AAA model or motion-capture parity. Implementation and controls: `STEALTH_REFERENCE_UPDATE.md`.
- Added C-toggle crouch, safe capsule resizing, a five-point crouch animation tree, low-ready weapon carry, stance camera height and muzzle obstruction height. Revised acceleration/braking and turning speed. Added ferns, fuller ivy, leaf litter and damp-road detail.
- **Layout preservation passed:** **6,762 existing transforms**, **126 collider definitions**, collider count and NavMesh reference unchanged. Added **85,160 foliage triangles** before culling. Evidence: `Logs/StealthReferenceLayout.txt`. Original scene SHA-256 remains `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`.
- **Synthetic crouch replay passed:** 73 captured frames across standing, crouch idle/forward, aimed left/back/right, standing transition, walk/run, crouched shooting and reload. Verified fixed capsule base, 1.55 m crouched height, standing obstruction/clearance, and rejection of stance changes while paused/dead. Lower-boot clearance **-19.3 to +38.8 mm**. Evidence: `Logs/StealthBakedSoleReview.log`, `Logs/StealthRuntimeReview.txt`, `Previews/StealthReference/Movement.gif`.
- The replay initially exposed **89.9 mm** penetration during transitions. Generated muscle clips reported unusable imported IK goals. Evaluated ankle positions plus serialized boot-sole samples reduced the error. Samples avoid per-frame mesh baking or readable mesh imports in the player build. Remaining small contact errors are recorded above.
- **Standing locomotion regression passed:** walk/run/sprint, aimed forward/left/back and stopping. Lower-boot clearance **-38.9 to +38.9 mm**. Evidence: `Logs/StealthStandingRegression.log`, `Logs/PlayerRuntimeGait.txt`.
- **All 21 gameplay integration assertions passed:** grounding/collision, camera/weapon setup, shot damage/ammo, reload/cancel, switching, and live complete routes/movement for Hunter, Runner, Ambusher and Elite. Evidence: `Logs/StealthGameplayRegression.log`, `Logs/ReferenceCitySmoke.txt`.
- Native character poses, runtime movement frames and environment captures were reviewed. The motion preview is a synthetic replay sampled at six frames per second; it is not a performance capture. It precedes the final puddle tint/foliage UV adjustment. No human keyboard/mouse play-through, full-wave play-through or target-machine performance benchmark was performed.
- **Final Windows x64 build succeeded:** zero errors, one warning, **413,372,476 bytes**, **13.592 seconds**. The remaining warning disables the optional Unity Pipeline editor connection in the standalone player; there are no shader warnings in this final build. Evidence: `Logs/StealthReferenceFinalBuild.log`, `Logs/WindowsBuildSummary.txt`.
- **118 C# files** are included in `CURRENT_SOURCE.md` and `SOURCE_INDEX.md`. No paid assets, additional Unity packages, proprietary game models or reference video footage were added to the build.

## Reclaimed city presentation — 14 October 2026

- Implemented a Last of Us Part I inspired material/lighting/overgrowth pass with worn survivor clothing, infected surface variants and restrained torso inertia/breathing. Existing models and grounded locomotion are retained; this is not AAA asset or animation parity. See `RECLAIMED_CITY_PRESENTATION.md`.
- **Layout preservation passed:** all **6,739** pre-existing environment transforms and all **126** serialized collider definitions remained unchanged; NavMesh data reference and collider count are unchanged. Existing routes validated. Evidence: `Logs/ReclaimedCityPresentation.txt`.
- Original `Assets/Scenes/ZombieMap.unity` bytes remain unchanged (SHA-256 `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`). New materials and foliage live under `Assets/Art/ReclaimedCity` and are assigned to the enhanced scene.
- Native before/after, street and character renders reviewed. Shader compilation passed. Environment count: **705 enabled MeshRenderers**, including **22 new foliage chunks**, **71,268 additional foliage triangles** before culling. These are asset counts, not performance measurements.
- Play Mode locomotion replay **passed**, with new body motion, materials, weapon layers and foot IK active. **56 captured frames**; lower-boot clearance **-27.0 to +38.8 mm**, including transitions. This remains a synthetic velocity/aim replay, not a human controls test; small foot contact errors remain. Evidence: `Logs/ReclaimedCityMotionReview.log`, `Logs/PlayerRuntimeGait.txt`, `Previews/ReclaimedCity/Movement.gif`.
- Post-update gameplay integration **passed all 21 assertions**: grounding/collision, camera/weapon references, shooting/damage/ammo, reload/cancel, weapon switching and complete live paths for Hunter, Runner, Ambusher and Elite. Evidence: `Logs/ReclaimedCityGameplayReview.log`, `Logs/ReferenceCitySmoke.txt`.
- Windows x64 build **succeeded: zero errors, one warning**, **405,560,268 bytes**, **41.373 seconds**. Evidence: `Logs/ReclaimedCityBuild.log`, `Logs/WindowsBuildSummary.txt`.
- Complete editable handoff now contains **113 C# files** in `CURRENT_SOURCE.md` and `SOURCE_INDEX.md`. No paid assets, packages or proprietary game files were added.
- Human controls, every jump/curb/sharp-turn case and target-machine frame rate remain manual review items. Screen-space occlusion and added foliage increase rendering work. Earlier attempts encountered an unresponsive open editor; a normal shutdown and fresh Direct3D 11 session completed previews, checks and build.

## Player locomotion repair after video review — 14 October 2026

- Isolated supplied P2 locomotion, without foot IK/action layers, reproduced the bad contact: original run's lower boot **-146 mm to +145 mm** relative to its ground plane. Corrected rig mapping was retained; the supplied clips and standing offset required changes.
- Generated separate PlayerGrounded clips/controller with target-mesh sole calibration, a restrained phase-aligned jog, consistent sprint cadence, corrected visual skin offset and contact-aware terrain IK. Original motion assets retained.
- Final isolated clips: idle/walk contact approximately **4 mm**, jog lower boot **3.7–38.9 mm**. Reviewed native side-view renders and controller poses.
- Final runtime animation/physics replay **passed** across walk/run/sprint, aim forward/left/back and stopping, with actual animation driver, weapon layers and foot IK enabled. Runtime captures visually reviewed. Overall lower-boot clearance **-36.6 to +41.3 mm**, including transitions; small blend/contact error remains. Evidence: `Logs/PlayerRuntimeGait.txt` and `Logs/PlayerGaitRuntimeBatch.log`.
- The replay uses synthetic velocity/aim inputs, not human keyboard/mouse interaction. It does not establish flawless contact on every curb, sharp turn or jump. Further human movement review remains appropriate.
- Post-repair gameplay integration **passed all 21 assertions**, covering grounding, collisions, camera/weapon initialization, shooting/damage/ammo, reload and weapon switching, plus live navigation for all four enemy types. Evidence: `Logs/PlayerRepairIntegration.log` and `Logs/ReferenceCitySmoke.txt`.
- Windows x64 build **succeeded: zero errors, one warning**, **423,290,108 bytes**, **20.996 seconds**. Evidence: `Logs/PlayerLocomotionBuild.log` and `Logs/WindowsBuildSummary.txt`. Implementation/settings: `PLAYER_LOCOMOTION_REPAIR.md`.

## Reference city enhancement — 14 October 2026

- Enhanced copy: `Assets/Scenes/EnhancedCity/ZombieMap.unity`. Original scene hash and backup hash match: `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`. Existing gameplay scripts and original material assets retained.
- Native before/after renders reviewed from the same comparison camera. Added weathered facades, fire escapes, shutters, roof details, street props/vehicles, overcast sky, neutral fog and softer lighting. See `REFERENCE_CITY_UPDATE.md` for remaining differences from the reference.
- Rebuilt navigation and validated complete routes from all existing markers. No missing scene scripts; existing gameplay references validated.
- Final isolated Play Mode run **passed all 21 assertions**: street grounding, collision-controller movement, central route clearance, camera/weapon references, camera-to-muzzle shooting and damage, ammo consumption, reload start/cancel, weapon switching, and live complete paths/movement for Hunter, Runner, Ambusher and Elite prefabs. Evidence: `Logs/ReferenceCitySmoke.txt` and `ReferenceCityBatch2.log`.
- Earlier interactive attempts timed out at startup. An isolated null-graphics-device run exposed a native crash in realtime reflection-probe rendering. The enhanced scene now uses a static sky cubemap; the final isolated run completed and exited normally.
- Scene geometry inspection: 683 enabled district MeshRenderers, 126 colliders, 282,504 triangles before culling. Static material/chunk combining, existing compressed maps and distant-detail LOD culling applied. These counts do not establish target-machine FPS or standalone draw calls.
- Keyboard/mouse input feel, full wave play-through, corner cases and demonstration-machine performance remain manual review items. Tests used APIs/physics/agent navigation, not human controls.
- Updated Windows x64 build **succeeded: zero errors, one warning**, **421,658,428 bytes**, **30.767 seconds**. Evidence: `Logs/ReferenceCityBuild.log` and `Logs/WindowsBuildSummary.txt`.

## Player leg / zombie gait repair — 14 October 2026

- Corrected player foot IK rotation misuse; constrained ground correction and added knee hints.
- Rebuilt zombie Avatar reference orientation, separated walk/chase states, calibrated cadence against actual NavMesh speed, re-encoded native attack/death poses and removed prototype root-level boot/knee/equipment meshes.
- Inspected Unity animation render sequences and a live ZombieMap camera capture. Live moving Hunters used GroundedWalk at approximately 1.343 playback for 2.25 m/s movement. Idle Hunters used GroundedIdle. All five enemy prefabs contain one Animator and one skin, without the old sole/knee meshes.
- Windows x64 build succeeded: **zero errors, one warning**, **413,286,668 bytes**, **15.145 seconds**. Build record: `Logs/WindowsBuildSummary.txt`.
- No automated tests, standalone play-through or full manual gameplay suite were run. Sharp turns, terrain contact and all aim/movement combinations remain manual review items. See `LEG_AND_GAIT_REPAIR.md`.
- `Previews/LegRepair/Corrected-Gaits.gif` is a sampled studio animation preview; `Previews/LegRepair/Gameplay.png` is a native scene-camera capture without overlay HUD.

## Selected P2 Survivalist update — 14 October 2026

- Applied the user-selected Slayver Survivalist to ZombieMap and the player prefab using Unity Editor APIs. Corrected the supplied root/pelvis mapping and separated full-body aiming idle from the masked weapon-carry pose.
- Added movement acceleration/braking, damped rotation, calibrated animation playback and limited terrain foot placement. Corrected finger animation binding names.
- Measured animation stance travel: walk **1.600 m/s**, run **3.600 m/s**, sprint **6.000 m/s**, four aim directions **1.250 m/s**, matching configured movement targets. These are offline animation measurements, not an end-to-end foot-sliding test.
- Reviewed native Unity character/weapon renders and sampled movement frames. `Previews/P2Player/P2-Weapons.png` and `P2-Movement.gif` use temporary studio lighting and sampled clips, not gameplay recording.
- Windows x64 build succeeded with **zero errors** and **one Pipeline configuration warning**. Pipeline is disabled in the player build because no runtime config is present. Latest build details are in `Logs/WindowsBuildSummary.txt`.
- No automated tests or manual gameplay play-through were run for this update. In-game movement feel, terrain contacts, extreme aim angles and weapon-specific reload poses remain manual review items. See `P2_PLAYER_UPDATE.md`.

Earlier results below are historical.

## Imported human, zombie and gun update — 12 October 2026

- Replaced the live procedural figures with licensed skinned models and imported locomotion. All four existing weapon prefabs now use textured CC0 gun models. See REALISTIC_CHARACTER_ART.md and THIRD_PARTY_CHARACTER_ASSETS.md.
- Inspected native Unity art renders and sampled animation poses. Repaired the legacy zombie Avatar's source-unit mismatch, corrected visual forward, calibrated the gun socket against the evaluated retargeted pose, and fitted material/muzzle/grip references. The animation contact sheet uses CPU-baked skin snapshots so repeated offscreen renders show each current pose.
- Final Windows x64 build **succeeded: zero errors, one warning**, **247,066,636 bytes** reported by Unity; build time **14.066 seconds**. The warning states that Pipeline is disabled in Player builds because no runtime config exists. Evidence: Logs/ImportedArtBuild.log and Logs/WindowsBuildSummary.txt.
- **No gameplay suite, automated gameplay tests, standalone startup run or manual play-through was performed for this art update.** Historical gameplay results below predate this replacement. Full in-game grip/contact, movement feel, balance and target-machine performance still require human review.
- Current art previews: Logs/Captures/Imported_Character_Lineup.png, Imported_Weapon_Lineup.png, Armed_Characters.png and Imported_Movement.gif. These use temporary studio lighting and animation sampling; they are not gameplay screenshots.
- Current handoff contains **100 C# scripts**. The four AI implementations remain distinct, while their current zombie visuals share one source mesh. Imported assets do not establish student modeling authorship.
- Updated Windows archive: **116937906 bytes**, SHA-256: **25F6351783556A51F77D0527F87D06FEB274A8A24CB9CAA339BB408C995FC547**. Credits and source license notices are included alongside the executable.

Comprehensive gameplay verification: 6 October 2026. Supplemental Editor and build validation: 10 October 2026. Editor: Unity 6000.5.3f1, Windows x64.

## Latest character/weapon art delivery — 12 October 2026

- Native art application completed and saved ZombieMap, weapon prefabs, enemy variants, texture maps, material assets and Humanoid clips. Existing clip GUIDs were preserved.
- Reviewed native character/weapon lineup renders and an aimed player pose. Sampled walk, run and sprint into a 48-frame animation preview. Reduced repetitive surface patterns and added bevelled weapon sections after the first render review.
- Windows build **succeeded: zero errors, one warning**, **152,124,864 bytes**, **27.455 seconds**. The warning states that the local Pipeline connection is disabled in the Player. Build evidence: `Logs/CharacterArtBuild.log` and `Logs/WindowsBuildSummary.txt`.
- No new automated gameplay tests or standalone startup run were performed for this request. Earlier startup and full-flow results below predate these changes. Runtime grip placement across all view angles and human movement feel still need review.
- Art previews: `Logs/Captures/Character_Lineup.png`, `Weapon_Lineup.png`, `Character_Aim.png`, `Movement_Preview.gif`. They show temporary preview lighting/cameras and sampled animation poses; they are not all unmodified gameplay screenshots.
- Source handoff now contains **94 complete C# files**. See CHARACTER_WEAPON_ART.md for settings, asset provenance, manual checks and model limitations.
- Updated archive: `Builds/ZombieOutbreak-Windows.zip`, **73,461,202 bytes**. SHA-256: `7DA1914DAAF316969866403E4A09B5D24EB3BF381FA98387BD4CBAA4405399B1`.

## Latest abandoned-city art verification — 10 October 2026

- Native scene generation and existing Phase 5/18 structural/navigation validation passed after the art update. The new surface shader returned no Unity shader diagnostic messages.
- Inspected native camera renders before/after the change and in Play Mode. Corrected excessive streetlight brightness, black puddle surfaces, duplicated FBX variants, imported model orientation and disconnected generated limb silhouettes.
- A bounded Play Mode preview reached wave one with eight spawned enemies and a functioning HUD. This is not a repetition of the complete earlier five-wave gameplay suite.
- Final Windows x64 build **succeeded: zero errors, one warning**, 145,231,352 bytes reported by Unity. Incremental build time: 2.473 seconds. The warning explicitly states that Pipeline is disabled in Player builds because no runtime configuration was supplied; that Editor-only connection is not needed by the game.
- The first build succeeded but included a live-command timeout diagnostic. Rebuilding with the supported long-running script command removed that error; the results above refer to this final rerun.
- A fresh hidden batch startup initialized Direct3D 12 and PhysX, remained alive for 12 seconds, and was then stopped. No managed exceptions appeared in that startup log. It does not establish interactive playability or performance on other devices.
- Evidence: `Logs/CityArtBuild.log`, `Logs/WindowsBuildSummary.txt`, `Logs/WindowsCityStartup.log` and `Logs/Captures/City_Gameplay.png`. The gameplay capture uses a temporary camera composition restored immediately afterward.
- Source handoff updated to **89 complete C# files**. Asset provenance and art settings are in ABANDONED_CITY_ART.md. Realistic character/weapon models and original team modeling evidence remain outstanding.
- Refreshed archive: `Builds/ZombieOutbreak-Windows.zip`, **70,577,575 bytes**, 164 entries including the executable. SHA-256: `6B404B5BDFFE695706F38E254F015AAA156427D5F1BD994AF791A33815FD7E55`.

## Earlier gameplay checks

| Check | Result | Evidence |
|---|---|---|
| Phases 5–13 compilation and native setup | Passed | Logs/Phase5.log through Phase13.log; later menu-reference repair included |
| Phase 14 four-agent integration | Passed | Logs/Phase14.log |
| Night graphics / atmosphere / marker reachability | Passed | Logs/Phase15.log, Phase15Atmosphere.log |
| Animation generation and controller integration | Passed | Logs/Phase16.log |
| Full MainMenu → Loadout → ZombieMap → five waves → Victory | Passed | Logs/Phase17.log |
| Exact enemy distribution | Passed, 77 total | 46 Hunter, 20 Runner, 10 Ambusher, 1 Elite |
| Restart and Game Over | Passed | Logs/Phase17.log |
| Health, ammo, switching, aim and equipment HUD events | Passed | Logs/Phase17.log |
| Four live weapons / shotgun pellets / reload cancellation | Passed | Logs/Phase17Weapons.log |
| Ammo conservation, cover, self exclusion, query saturation, spread | Passed | Logs/Phase17Setup.log |
| Melee timing, range escape and duplicate impact prevention | Passed | Logs/Phase17Mechanics.log |
| Grenade enemy damage, duplicate colliders and wall occlusion | Passed | Logs/Phase17Mechanics.log |
| Barricade destruction and camera collision | Passed | Logs/Phase17Mechanics.log |
| Concurrent 21-agent simulation | Passed; all 21 moved and observed weapon switch | Logs/Phase17Stress.log |
| Structural validation and A* microbenchmark | Passed | Logs/Phase18.log, NavigationBenchmark.txt |
| Windows player build | Final build passed: 0 errors, 0 warnings | Logs/WindowsBuild.log, WindowsBuildSummary.txt |

The first full-flow attempt caught missing menu catalog references; these were fixed and the flow passed on rerun. An earlier integration attempt caught unsaved equipment components; these were fixed before proceeding. Only passing reruns support the results above.

## Delivery checks before the city art update

- **Latest Windows x64 build:** succeeded, **0 errors, 0 warnings**, 96,962,860 bytes reported by Unity. Build time 22.718 seconds. Evidence: `Logs/WindowsBuildTeamArt.log` and `Logs/WindowsBuildSummary.txt`.
- **Executable:** `Builds/Windows/DeadCity.exe`. The complete Windows folder is also packaged as `Builds/ZombieOutbreak-Windows.zip`.
- **Packaged startup checks:** the executable initialized Direct3D 12 and PhysX and remained responsive for 259 seconds in the earlier check. After this update, a fresh hidden launch remained alive for 12 seconds and was then closed. These confirm startup, not a full standalone input-driven playthrough.
- **Final post-build scene flow:** passed again in native Unity Play Mode using the final scenes and code. Evidence: `Logs/Phase17Final.log`. Includes exact counts, HUD events, Victory, restart and Game Over.
- **Latest A* microbenchmark:** 60 searches, zero failures; **1.034 ms mean, 1.873 ms p95, 7.331 ms maximum**. This is a local Editor microbenchmark, not FPS.
- **Source handoff:** 84 complete C# files indexed in SOURCE_INDEX.md and included verbatim in CURRENT_SOURCE.md. All runtime scripts remain editable in their normal Assets folders.

## Earlier visual review

Native camera/Canvas captures were inspected for MainMenu, Loadout, gameplay, aim HUD and the optional AI display. Captures include widescreen and 4:3 variants. Files are under Logs/Captures. The art is visibly a procedural prototype; the handoff does not describe it as completed team-authored graphics.

## Limits of this evidence

- The flow harness uses accelerated spawning/time and test damage to exercise every count and transition. It is not an unaided human five-wave win.
- The 21-agent check exercises navigation and shared state for a bounded period. It is not a frame-rate benchmark or a long soak test.
- The path microbenchmark measures explicit A* searches in the Editor, not whole-game rendering performance.
- Native keyboard/mouse feel, every display/device configuration, final audio/art quality and gameplay balance require human review.
- Team Blender/Maya models and authorship evidence remain outstanding; follow TEAM_MODEL_HANDOFF.md.
- Logs/Captures/Builds are ignored by Git. Source, native assets, metadata, settings, data and these reports are included in the workspace.

## Supplemental model workflow verification (10 October 2026)

- Phase18Setup compiled and passed structural validation with the empty TeamArtCatalog asset. It created the catalog at `Assets/Data/TeamArtCatalog.asset` and the six Source/Export/Textures folders under `Assets/Models/Team/`.
- Windows build after the Editor changes passed with zero errors and zero warnings. Runtime behavior remains the previously verified build; the current executable passed a fresh 12-second startup smoke check.
- The complete archive was refreshed at `Builds/ZombieOutbreak-Windows.zip` (37,136,258 bytes). SHA-256: `DBF969E521FCA1627ACBB941F9093A9612A375869081265BEC35DED70EF9E840`.
- No original team zombie or barricade models were present, so the model validation/apply actions have not been exercised with actual art. The models and human balance/device review remain outstanding.


