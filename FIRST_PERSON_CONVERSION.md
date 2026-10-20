# First-person conversion — 16 October 2026

The current game is now first person. Open **D:/GV project/Assets/Scenes/MainMenu.unity**, press Play, choose a loadout and enter the district. The enhanced gameplay scene is **D:/GV project/Assets/Scenes/EnhancedCity/ZombieMap.unity**.

## Controls

| Action | Control |
| --- | --- |
| Walk / strafe / back up | WASD |
| Look and face direction | Mouse |
| Run | Left Shift + movement |
| Sprint, with weapon lowered | Left Ctrl + forward movement |
| Crouch / stand | C; standing requires clearance |
| Jump | Space |
| Hip fire | Left mouse; hold for automatic weapons |
| Aim down sights / zoom | Hold right mouse |
| Reload | R |
| Primary / pistol | 1 / 2 or mouse wheel |
| Interact / medkit / grenade | E / Q / G |
| Pause | Escape |

## Changes and complete source

Complete current C# code is in [CURRENT_SOURCE.md](<D:/GV project/CURRENT_SOURCE.md>); the authoritative files are:

| File | Responsibility |
| --- | --- |
| [ThirdPersonCameraController.cs](<D:/GV project/Assets/Scripts/Player/ThirdPersonCameraController.cs>) | Eye position follows the CharacterController capsule. Shared mouse look, recoil and pause handling; smooth 75° to 55° aim zoom. The existing class name preserves scene references. |
| [ThirdPersonPlayerController.cs](<D:/GV project/Assets/Scripts/Player/ThirdPersonPlayerController.cs>) | First-person facing follows mouse yaw. WASD remains camera relative; A/D strafe. Existing acceleration, gravity and jump remain. |
| [FirstPersonWeaponRig.cs](<D:/GV project/Assets/Scripts/Player/FirstPersonWeaponRig.cs>) | Rifle/SMG/Shotgun/Pistol view positions, aim blending, small weapon sway/bob, recoil, reload/equip motion and sprint/near-cover lowering. Hides the body from view while keeping its shadow. |
| [FirstPersonArms.cs](<D:/GV project/Assets/Scripts/Player/FirstPersonArms.cs>) | Two-bone arm IK follows the weapon grip and existing SupportHand point. Retains the Survivalist hand/finger pose. No second Animator writes to these arm bones. |
| [PlayerWeaponController.cs](<D:/GV project/Assets/Scripts/Weapons/PlayerWeaponController.cs>) | Allows hip fire, increases hip spread, and retains camera-to-aim-point / muzzle-to-target hit detection, cover blocking, ammo, reload, switching and AI noise events. |
| [PlayerHudPresenter.cs](<D:/GV project/Assets/Scripts/UI/PlayerHudPresenter.cs>) | Shows the crosshair for hip fire and aiming; hides it during reload, pause and death. Existing event subscriptions still drive HUD updates. |
| [PlayerEquipment.cs](<D:/GV project/Assets/Scripts/Player/PlayerEquipment.cs>) | Grenades originate at the FPS eye position, including crouch. Resolves scene camera once on initialization. |
| [PlayerInteractor.cs](<D:/GV project/Assets/Scripts/Player/PlayerInteractor.cs>) | Resolves the scene camera when the new player prefab is instantiated. Existing interaction behavior is retained. |
| [FirstPersonSetup.cs](<D:/GV project/Assets/Editor/Art/FirstPersonSetup.cs>) | Native Unity asset/scene authoring utility. Menu: Zombie Outbreak → First Person → Apply to Enhanced City. The saved scene is already configured. |

## Saved hierarchy

```text
Player [CharacterController, movement, aim, health, weapons, FirstPersonWeaponRig, ...]
  CameraTarget / AimTarget / GroundCheck
  SurvivalistVisual [existing Animator and corrected locomotion]
    ... original hand WeaponSocket [retained]
  FirstPersonView [follows eye camera after movement]
    WeaponSocket
      Selected primary / Pistol [runtime instances]
    FirstPersonArms [FirstPersonArms]
      Skeleton
      FPS_HANDS [active]
        SK_Miliary_FPS_Arms_Gloves1
        SK_Miliary_Military_FPS_Shirt3
    WeaponCamera [Camera; no AudioListener]
Main Camera [Camera, AudioListener, ThirdPersonCameraController, existing city effects]
GameUI [existing HUD and modal panels]
```

Prefab files: **D:/GV project/Assets/Prefabs/Player/FirstPersonArms.prefab** and **D:/GV project/Assets/Prefabs/Player/PlayerFirstPerson.prefab**. The latter resolves the scene Main Camera at runtime. A new scene also needs the Main Camera's firstPerson flag and culling configuration below, plus its normal HUD/AI references.

## Inspector settings

- Main Camera: First Person enabled; FOV **75**, aim FOV **55**, sensitivity **2.5**, starting pitch **0**, pitch limits **−85 / +85**, eye inset **0.14 m**, near plane **0.04 m**. Camera culling excludes layer **30: FirstPerson**.
- WeaponCamera: Depth clear flags, depth one above Main Camera, near/far **0.01 / 3 m**, culling only layer 30, HDR off, occlusion culling off. Runtime copies the main camera FOV, aspect and viewport. It renders arms and guns over the world; world hit detection still prevents shooting through cover.
- FirstPersonWeaponRig: world camera, weapon camera, view root, socket, arms and body renderer references are assigned. Pose response **14**, sway **0.45**, bob **0.004 m**. Bob affects the weapon only; camera height comes from the capsule rather than animated bones.
- FirstPersonArms: upper arm, forearm, hand and wrist orientation assigned for both sides. Arms local offset **(0, −1.68, 0.12)**. FPS_HANDS must remain active. Gloves and sleeve meshes use the existing character materials.
- PlayerStanceController: crouched capsule height **1.2 m**, standing height **1.8 m**. The existing obstruction checks prevent standing into ceilings.
- Body SupportHandGrip is disabled in this scene, since the separate FPS arm rig owns the visible hands. Existing world body animations still produce the character shadow.

No new packages or downloads were added. This uses the existing Built-in Render Pipeline, legacy Input Manager, uGUI and Survivalist assets. Camera composition follows Unity's [Camera clear flags](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-clearFlags.html); body visibility uses [ShadowsOnly](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.ShadowCastingMode.ShadowsOnly.html).

## Preview and review limits

- Windows x64 build succeeded with **0 errors and 1 warning**; see **D:/GV project/Logs/FirstPersonBuildSummary.txt**. Run **D:/GV project/Builds/Windows/ZombieOutbreak.exe** locally or extract the entire **D:/GV project/Builds/ZombieOutbreak-Windows.zip** on another Windows PC.
- [Game View with the rifle, arms and HUD](<D:/GV project/Previews/FirstPerson/FirstPerson-Hip.png>) is captured from Play Mode.
- [Rifle aiming pose](<D:/GV project/Previews/FirstPerson/Aim-Pose.png>) is an authoring still with the aim offset and arm grips applied, not a recorded mouse-input sequence.
- Existing locomotion clips, city layout, removed bridge, enemy types, loadout, waves and menus are retained. The original **Assets/Scenes/ZombieMap.unity** remains the earlier third-person scene; use the enhanced scene for FPS.
- Hand motion during reload is procedural and uses the existing weapon mechanism. This conversion does not add a new motion-captured reload library.
- Rifle presentation was visually reviewed. A hands-on pass across all four weapons, terrain, movement, combat and performance remains to be done. No automated gameplay test suite was added or run.

## Hands-on testing procedure

1. Start through MainMenu → Prepare Loadout → Enter the District. Confirm eye-level view, arms, gun, centered crosshair and the current primary plus pistol.
2. Walk, run, strafe and back up while looking around. The camera should follow the capsule smoothly with no animated head bob. Jump, crouch under cover and try to stand where blocked.
3. Fire without aiming, then hold RMB. Confirm zoom and sight transition; fire/reload/switch and inspect ammo changes. Repeat with Rifle, SMG and Shotgun loadouts and the Pistol. Release the trigger after switching.
4. Approach a wall: the gun should lower, and shots must not damage a target through the wall. Check near corners while aiming and crouching.
5. Throw a grenade while crouched, use a medkit and interact with a door. Pause/resume, then restart after death. Confirm camera/cursor/HUD state.

## Common issues

- **Third-person view still appears:** open the enhanced ZombieMap, or start with the newly rebuilt Windows game. The earlier map and historical Phase 1–3 generators remain for reference.
- **Gun visible but no arms:** keep FPS_HANDS and the two selected FPS mesh objects active. WeaponCamera must render FirstPerson layer 30; Main Camera must exclude it.
- **Face/body blocks the camera:** keep FirstPersonWeaponRig enabled and its original body renderer references assigned. The FPS arm renderer references must not be included in that list.
- **Weapons or hands shake:** do not attach FirstPersonView to the animated head or hand bones. Keep the FPS arms free of an Animator and leave the world-body support-hand IK disabled in the FPS scene.
- **Pink materials:** use the project's current Built-in pipeline; these materials were not converted to URP/HDRP.
- **Input unavailable:** focus Game View and resume after focus-loss pause. Editing scripts during Play Mode can invalidate transient weapon state; stop and restart Play Mode after recompilation.

## Git

Commit scripts, both new prefabs with their .meta files, enhanced scene, TagManager and docs. Builds, Logs, Previews and Backups are ignored. Suggested commits:

1. `feat: convert enhanced player camera and movement to first person`
2. `feat: add first person arms weapon presentation and hip fire`
3. `docs: document FPS controls setup and review steps`

Pre-conversion backup: **D:/GV project/Backups/BeforeFirstPerson-2026-10-16/PlayerAndScene.zip**.
