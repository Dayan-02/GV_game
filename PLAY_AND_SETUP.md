# Dead city — run and setup guide

## Start the game

1. Open this project with **Unity 6000.5.3f1**. Let Unity restore packages and finish importing.
2. Open `Assets/Scenes/Opening.unity` and press Play for the 10-second opening cinematic. Space, Enter, Escape or a left click skips to the main menu. Open `Assets/Scenes/MainMenu.unity` directly when developing without the intro.
3. Select **Prepare Loadout**, choose Rifle, SMG or Shotgun, choose an equipment pack, then select **Enter the District**.
4. Survive five waves. Wave five contains the Elite. Pause, Game Over and Victory have restart and Main Menu controls.

A generated Windows player, when built, is `Builds/Windows/DeadCity.exe`. Keep the entire Windows folder together; the EXE needs its Data folder and Unity runtime files. Builds are deliberately excluded from Git.

## Controls

Enemy behaviour and tuning: see [AI behaviour update](AI_BEHAVIOUR_UPDATE.md). F3 now shows target memory, navigation failures, cooldowns, attack slots and Elite cover/rally information.

| Action | Control |
|---|---|
| Move / walk | WASD |
| Run | Left Shift + movement |
| Sprint | Left Ctrl + movement |
| Jump | Space |
| Crouch / stand | C; standing requires overhead clearance |
| Look | Mouse |
| Aim down sights / zoom | Hold right mouse |
| Fire / hip fire | Left mouse; hold for Rifle/SMG |
| Reload | R |
| Primary / Pistol | 1 / 2 or mouse wheel |
| Interact with door | E, looking at it within 3.2 m |
| Medkit | Q |
| Grenade | G |
| Pause / resume | Escape |
| Optional AI viva display | F3; aim at an enemy to inspect it |

Click the Game view if input is not focused. Losing application focus pauses the run. Resume through the panel or Escape. The cursor is available in menus and locked during gameplay.

Enemies now have distinct outfits and silhouettes plus separate contact-timed attack clips; see [ENEMY_IDENTITY_ATTACK_UPDATE.md](ENEMY_IDENTITY_ATTACK_UPDATE.md). Their existing infected locomotion and approach/rush profiles are described in [INFECTED_MOVEMENT_UPDATE.md](INFECTED_MOVEMENT_UPDATE.md).

## Packages and renderer

- Unity UI `com.unity.ugui` **2.5.0**, including TextMeshPro.
- AI Navigation `com.unity.ai.navigation` **2.0.15**.
- Built-in animation, audio, physics, particles and AI modules are resolved by Package Manager.
- Built-in Render Pipeline with Standard and custom weathered-surface materials. This project has not been migrated to URP.
- Unity Pipeline `com.unity.pipeline` **0.8.0-exp.1** supplies the local Editor connection used for authoring.
- Legacy Input Manager is used by the existing controller and StandaloneInputModule. Keep Active Input Handling set to **Input Manager (Old)** or **Both**.
- Cinemachine is not required: the existing camera controller now uses capsule-based first-person eye positioning and smooth aim zoom. A second camera draws the FPS arms and gun. See [first-person setup](FIRST_PERSON_CONVERSION.md).
- Animation Rigging is optional future work; it is not needed to compile or run this version.

No package or Editor installation is required if these already match the installed project. Do not run the old Phase 1–3 generation commands over the finished scenes: they are retained as historical authoring tools and some rebuild earlier scene layouts. The enhanced scene now supports crouched movement and aiming; see [the supplied-video update](STEALTH_REFERENCE_UPDATE.md).

## Abandoned-city visuals

Open **Assets/Scenes/EnhancedCity/ZombieMap.unity** for the current graphics. Earlier playable maps and build archives have been removed; see VERSION_CLEANUP.md. See [REALISTIC_GRAPHICS_UPDATE.md](REALISTIC_GRAPHICS_UPDATE.md) for scanned plants/vehicles, Linear HDR lighting, materials, settings and preview limits. [ABANDONED_CITY_ART.md](ABANDONED_CITY_ART.md) records the earlier environment pass; [P2_PLAYER_UPDATE.md](P2_PLAYER_UPDATE.md) records the imported survivor and [THIRD_PARTY_CHARACTER_ASSETS.md](THIRD_PARTY_CHARACTER_ASSETS.md) contains character/weapon credits. The project mixes imported scanned/model assets with simple authored facade geometry.

## Scene hierarchy

```text
MainMenu / Loadout
  Menu
    LoadoutMenuController
    MenuCanvas [Canvas, CanvasScaler, GraphicRaycaster]
      Content [title, description, selection, buttons]
  MenuCamera [Camera, AudioListener]
  EventSystem [StandaloneInputModule]
  LoadoutSession [runtime persistent object]

ZombieMap
  Player [CharacterController, PlayerHealth, movement, aim, animation,
          weapons, loadout applier, equipment, interaction, physics push]
    CameraTarget
    AimTarget
    GroundCheck
    Humanoid visual [Animator]
      ... RightHand
        WeaponSocket [original retained socket]
    FirstPersonView
      WeaponSocket
        Selected primary / Pistol [runtime WeaponView instances]
      FirstPersonArms [dedicated gloves and sleeves, arm IK]
      WeaponCamera [FirstPerson layer only]
  Main Camera [ThirdPersonCameraController, firstPerson enabled]
  Exploration Camera / Aim Camera [legacy pose settings]
  OutbreakDistrict [NavMeshSurface]
    Streets / Buildings / Props / Boundaries
    Doors / PhysicsObjects / Barricades
    SpawnPoints / AmbushPoints / Lighting
    ArtPass / AtmosphereDetails
  EnemySystems [EnemyWorld, HunterNavigationGraph, WaveManager]
  CombatEffects [bounded particle pools]
  GameUI [GameUIController]
    HUDCanvas [PlayerHudPresenter, GameStatusPresenter, AIDebugPresenter]
      Crosshair / Health / Weapon / ReloadingIndicator
      EquipmentPlaceholders [live equipment counts]
      WavePlaceholders [live wave and enemy counts]
      WaveAnnouncement / InteractionPrompt / AIDebugPanel [off initially]
    ModalCanvas [GamePanelsPresenter]
      PausePanel / GameOverPanel / VictoryPanel
  EventSystem
  Phase1Environment_Archived [inactive, retained for reference]
  Hunter / Runner / Ambusher / Elite [spawned at runtime]
```

The FPS arms use the existing Survivalist skeleton and dedicated glove/sleeve meshes. FirstPersonView follows the capsule camera and contains the active weapon socket. The world body retains its Animator and casts shadows. See [FIRST_PERSON_CONVERSION.md](FIRST_PERSON_CONVERSION.md) for the new prefabs, layer and camera settings. The archived environment and practice targets are inactive.

## Inspector references and tuning

### Player and loadout

`PlayerLoadoutApplier.catalog` -> `Assets/Data/LoadoutCatalog.asset`. It runs before weapon Awake and applies exactly primary + Pistol. Do not edit WeaponData to store runtime ammo. `LoadoutSession` persists the selected catalog indices; it deliberately does not persist health/ammo between runs.

`PlayerEquipment`: PlayerHealth, main Camera and Grenade prefab are assigned; heal amount 45, throw speed 14 m/s. Medic pack = 3 medkits + 1 grenade; Assault pack = 1 medkit + 3 grenades. Full health does not consume a medkit. Grenades do not harm the player in this version.

### World and enemies

World layer = 8, Enemies = 9, Player = Ignore Raycast (2). NavMeshSurface collects **World colliders in its children**. Dynamic objects use NavMeshModifier Ignore From Build + carving NavMeshObstacle. Enemy roots have NavMeshAgent + CapsuleCollider and no dynamic Rigidbody.

EnemyWorld has explicit PlayerHealth, PlayerWeaponController, player movement and tactical-marker arrays. Each spawned EnemyActor receives `Initialize(world)` from WaveManager. Use the four enemy prefabs, not EnemyBase alone, when adding a decision-making actor.

Shared perception samples every 0.2 s. Line of sight uses the World mask, so other zombies do not hide the player from perception. Weapon hit rays still collide with enemies and cover. Actual shot events retain the category/noise radius at the time of firing.

### UI and events

Both gameplay canvases use Screen Space Overlay, **Scale With Screen Size**, reference **1920 × 1080**, match **0.5**. Health anchors bottom-left, weapon bottom-right, equipment bottom-centre, crosshair centre, waves top-right. Modal overlays stretch to all edges with centred cards. One EventSystem per scene. Text/decoration raycasts are off; modal blocking backgrounds and buttons receive raycasts.

- PlayerHudPresenter subscribes to health, aim, weapon, ammo, reload and screen events.
- GameStatusPresenter subscribes to waves, equipment and interaction prompts.
- GamePanelsPresenter owns button listeners and listens to screen-state changes.
- OnEnable subscribes and reads an initial snapshot; OnDisable unsubscribes. Start refreshes again after initial weapon creation.
- There are no constant scene searches in HUD updates. Only announcement expiry and the optional, throttled debug display need Update.
- GameUIController owns pause time/audio and cursor state. Pausing freezes scaled reload, grenade and wave clocks. Enemy decisions stop after player death; the player's terminal death animation can finish with unscaled animation time.

### Wave assets

`Assets/Data/Waves/Wave1.asset` through `Wave5.asset` contain the exact 8 / 13 / 16 / 21 / 19 totals. Initial delay 6 s, intermission 7 s, per-enemy spawn interval 0.7 s. Remaining = queued + living enemies. Two reserve magazines per carried weapon are supplied between waves. Edit the data assets to tune pacing while retaining the assessment counts.

## Common bugs and fixes

| Symptom | Check / fix |
|---|---|
| Pink materials | Use the project's Built-in renderer; converting to URP requires deliberate material conversion. |
| Missing TMP font | Import TMP Essentials; the builder expects LiberationSans SDF under Assets/TextMesh Pro/Resources. |
| No input | Focus Game view, Resume, and check legacy input handling and cursor state. |
| Missing menu catalog | Assign LoadoutCatalog on both menu controllers; Phase12Setup now repairs existing menu references. |
| No enemies / spawn warning | Check six entrances, NavMeshSurface bake, World layer, occupancy and distance from player. Do not place entrances inside props. |
| Agent off NavMesh | Bake first, check agent type, height and radius, and sample spawn positions. |
| A* rejects a route | Inspect grid bounds/2 m spacing, dynamic carving and usable passage width. It intentionally rejects incomplete paths. |
| Door opens visually but route remains blocked | Put the moving NavMeshObstacle on the leaf, not the hinge; exclude the leaf from the permanent bake. |
| Rigidbodies fight navigation | Remove dynamic Rigidbody from agents; it belongs on props and grenades. |
| Shooting near cover appears blocked | Intended: the offset camera can see a point the muzzle cannot reach. Check muzzle clearance and body-to-muzzle cover tests. |
| Ammo or HUD stale | Confirm presenter references, event subscription lifecycle and runtime snapshots; do not add per-frame Find calls. |
| Double grenade damage | All enemy colliders must share one parent EnemyHealth; the grenade deduplicates that component. |
| Attack damages through wall | Ensure cover colliders are World layer; shared combat rechecks range/LOS at impact. |
| Sliding or T pose | Valid Humanoid Avatar, assigned controller, root motion off, locomotion thresholds and GaitScale. |
| Reload ends too soon | Use ReloadSpeed parameter and weapon reload duration; do not let clip events transfer ammo independently. |
| Wave never ends | Check queued count and living registry; destroy enemies through EnemyHealth so death callbacks run. Bypassing health with arbitrary Destroy is unsupported. |
| UI clicks fire a shot | Resume/switch uses trigger release arming and camera cursor-lock frame suppression. Keep those gates when changing input. |

## Final manual checks

Automated checks establish mechanics and scene flow, not human combat balance or every input/rendering device. Before submission:

- Walk/run/sprint/jump, backward aim and strafe on a keyboard/mouse in the actual build.
- Fight every weapon choice, hold full-auto, click semi-auto, reload with low reserve, switch during reload and shoot from near cover.
- Push props, open/close doors, destroy barricades and use both equipment packs.
- Play the five waves without automated damage or speed changes; adjust balance only after recording feedback.
- Review 16:9 and 4:3 HUD, camera collision, night/interior readability and particle visibility.
- Record a Unity Profiler/Frame Debugger capture on the submission machine. Static batching is enabled for fixed art; bake occlusion/lightmaps only after final models are placed.
- Complete the two original models and evidence in `TEAM_MODEL_HANDOFF.md`.
- Record each member's actual contribution and viva evidence in `ASSESSMENT_GUIDE.md`.

## Current imported art

The live player is a skinned military human, and the four AI categories use a rigged zombie model with distinct tints/scales. Weapons use detailed free models and PBR textures. See REALISTIC_CHARACTER_ART.md for the current Animator, grip, prefab and material settings, plus a manual visual-review procedure. THIRD_PARTY_CHARACTER_ASSETS.md contains the source credits and license information. Older generated-character instructions describe the earlier placeholder pass.
