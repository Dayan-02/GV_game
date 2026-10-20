# Distinct infected types and improved attacks — 16 October 2026

The four existing enemy prefabs now have different silhouettes, clothing and attack clips. The FPS player, city layout, wave composition and four AI decision systems are retained.

| Type | Visual identity | Normal attack |
| --- | --- | --- |
| Hunter | Ochre work shirt, weathered work helmet, reflective strips and chest pocket | Asymmetric right-hand rake with a torso wind-up |
| Runner | Narrower body, light sleeveless top, bare head and soiled shoulder strap | Fast-looking double-handed reach with a forward lean |
| Ambusher | Dark green hood, ragged shoulder cape and narrow silhouette | Left-led diagonal claw rake from a hunched pose |
| Elite | Larger, wider body, damaged helmet, chest plate, shoulder armour, belt pouches and shin guards | Heavy arm strike; the existing heavy-attack decision uses a separate two-handed overhead slam |

These are derived mesh/outfit variants of the existing licensed zombie, using the same compatible Humanoid Avatar. They are not four newly purchased/scanned character models. Existing source-asset credits still apply. No downloads, paid assets or new packages were added.

## Preview

- [Four-type lineup](<D:/GV project/Previews/EnemyIdentity/Lineup.png>)
- [Attack animation video](<D:/GV project/Previews/EnemyIdentity/AttackPreview.mp4>)

The lineup and 24 attack frames were rendered with Unity from the saved prefabs. Left to right: Hunter, Runner, Ambusher, Elite. The video displays one normalized attack cycle over two seconds for easier inspection. These are authoring previews, with gameplay and navigation disabled. They do not demonstrate live hit registration, pursuit, terrain contact or battle balance.

## Attack timing correction

Previously every type used the same **2.5667-second GroundedAttack** clip. EnemyAnimationController set AttackSpeed to **1 / combat duration**, which only produces the intended timing for a one-second clip. Consequently the visible action could outlast the combat clock.

The new clips have distinct anticipation, contact, follow-through and recovery poses. Contact is authored at **0.56 normalized time**. EnemyCombat computes duration as **windup / contactNormalizedTime**; EnemyAnimationController computes playback speed as **actual clip length / duration**. An explicit short crossfade starts the chosen action at its beginning. The exit transition starts at 0.92 with a 0.06-second blend back to movement.

The existing normal wind-up remains **0.5 seconds** for Hunter, Runner and Ambusher and **0.7 seconds** for Elite. Their normal action durations are approximately **0.893 / 1.25 seconds**, respectively. Heavy attacks retain the existing 1.4× wind-up/duration multiplier and 1.8× damage multiplier. Damage amounts, range and cooldown values were retained.

An attack now tracks the target during the first **0.38 normalized time**, then commits to its facing. Impact remains a single idempotent damage gate with range and line-of-sight checks. Added frontal hit arc: **110°**, or **125° for Elite**. Moving behind the enemy during the committed strike can make it miss. This changes dodge opportunities and needs a combat balance review.

Navigation still owns world movement; root motion remains disabled. Full Humanoid pose curves are recorded at 60 Hz, with foot targets held during authoring and root height calibrated against the actual variant skin. This does not guarantee perfect contact on every slope. The attacks are procedurally authored on the existing rig, not newly captured actor performances.

## Files and setup

| Path | Purpose |
| --- | --- |
| [EnemyIdentitySetup.cs](<D:/GV project/Assets/Editor/Art/EnemyIdentitySetup.cs>) | Rebuilds the derived body meshes, fitted skinned clothing, accessories, materials, eight action clips, prefab references and attack exit transitions through Unity APIs. |
| [EnemyIdentityPreview.cs](<D:/GV project/Assets/Editor/Art/EnemyIdentityPreview.cs>) | Small isolated studio lineup and attack capture; restores lighting and removes temporary objects afterward. |
| [EnemyAnimationController.cs](<D:/GV project/Assets/Scripts/AI/Shared/EnemyAnimationController.cs>) | Uses the explicit normal/heavy clip references to synchronize playback with combat duration. |
| [EnemyCombat.cs](<D:/GV project/Assets/Scripts/AI/Shared/EnemyCombat.cs>) | Contact fraction, committed facing and frontal impact check. |

Generated assets are under **D:/GV project/Assets/Art/EnemyIdentity/**, with Meshes, Materials and Animations folders. Modified prefabs: **D:/GV project/Assets/Prefabs/Enemies/{EnemyBase,Hunter,Runner,Ambusher,Elite}.prefab**. Existing controllers and overrides under **D:/GV project/Assets/Animations/InfectedLocomotion/** reference the new clips.

On each enemy, EnemyAnimationController has **Attack Clip** and **Heavy Attack Clip** assigned. EnemyCombat's **Contact Normalized Time** is 0.56. Keep the animation contact point and this value matched when replacing a clip. The four AI scripts remain responsible for deciding when to attack; no AI types were merged or removed.

To regenerate in Edit Mode: **Zombie Outbreak → Enemies → Apply distinct identities and attacks**. The saved assets are already configured. Restart Play Mode to replace enemies spawned before the asset update. Scene spawning references the same four prefab paths.

## Review and common issues

1. Start the enhanced map or the rebuilt Windows game. Confirm Hunter workwear, Runner light top, Ambusher hood/cape and Elite armour are distinguishable at gameplay distance.
2. Approach each type, watch its wind-up and contact, then move away or around it. Verify damage happens once, requires range and an unobstructed frontal strike, and stops after death.
3. Inspect the Elite's overhead attack separately. Check recovery into pursuit and attacks while the player crouches or moves near a wall.
4. Review hood/cape and armour intersections across movement, hit and death poses, then complete a normal wave play-through. These previews cover idle and normal attack poses; all possible terrain and animation combinations have not been reviewed.

If enemies still look identical, stop and restart the run, and use **Assets/Scenes/EnhancedCity/ZombieMap.unity**. Historical art-generation commands can overwrite newer prefab/controller references. If a future attack feels delayed, inspect its actual clip length and contact fraction rather than globally changing Animator speed. If damage occurs behind an enemy, preserve the new hit-arc check when modifying combat.

The first authoring preview exposed clothing clipping; fitted skinned torso clothing replaced the initial rigid torso shells. New cloth and headgear meshes include back-face normals and tangents for their surface maps. Materials reuse the existing authored fabric/metal surface maps; they remain prototype art.

## Delivery

Build result: **D:/GV project/Logs/EnemyIdentityBuildSummary.txt**. Run **D:/GV project/Builds/Windows/ZombieOutbreak.exe** with its accompanying files, or extract **D:/GV project/Builds/ZombieOutbreak-Windows.zip**.

Windows x64 build succeeded with **0 errors, 1 warning**, **483,804,492 bytes**, in **35.610 seconds**. The warning disables the optional Pipeline connection in the standalone player.

Backup before this update: **D:/GV project/Backups/BeforeEnemyIdentity-2026-10-16/Enemies.zip**. No automated gameplay suite was added or run. Hands-on four-type combat review and target-device profiling remain.

Suggested Git commits:

- `feat: distinguish four infected types with fitted outfits and silhouettes`
- `fix: synchronize zombie attack clips with combat contact timing`
- `docs: add infected identity and attack previews and review notes`

Commit generated Assets with their .meta files, scripts and documentation. Logs, Previews, Backups and Builds are local ignored artifacts. Complete current C# code is in [CURRENT_SOURCE.md](<D:/GV project/CURRENT_SOURCE.md>).
