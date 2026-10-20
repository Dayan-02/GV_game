# Phase 1: Third-Person Player and Camera

This is the historical Phase 1 guide. The current project includes Phase 3 shooting; use [PHASE3_GUIDE.md](<D:/GV project/PHASE3_GUIDE.md>) for current setup and status. Rebuilding Phase 1 replaces the gameplay scene.

Phase 1 originally added a camera-relative humanoid greybox controller, orbit camera, basic health component, and an interaction contract. Later phases extend these scripts.

## Unity project and packages

- Project version: `6000.5.3f1`. During Phase 3, a working Editor was found at `D:/unity/6000.5.3f1/Editor/Unity.exe`; native assets were generated and project checks passed.
- Required packages: none beyond Unity's built-in 3D, physics, and editor modules.
- Cinemachine: not installed in this clean project. The camera is a small custom orbit rig with collision handling, so this Phase 1 setup has no package dependency. Cinemachine can be added later if the team chooses to replace the camera implementation.
- Input: the scripts use Unity's built-in `UnityEngine.Input` API. In **Edit > Project Settings > Player > Other Settings > Active Input Handling**, choose **Input Manager (Old)** or **Both** if the project has been switched to Input System only.

## Included scripts

Each link opens the complete C# source for that script.

| Full path | Purpose |
| --- | --- |
| `D:\GV project\Assets\Scripts\Player\ThirdPersonPlayerController.cs` | Reads WASD, maps it to the camera's horizontal forward/right axes, handles walk/run/sprint, jump, gravity, ground probing, and smooth facing. |
| `D:\GV project\Assets\Scripts\Player\ThirdPersonCameraController.cs` | Mouse orbit, smooth follow, configurable sensitivity/distance, collision sphere cast, cursor lock, and the moving aim target. |
| `D:\GV project\Assets\Scripts\Player\PlayerHealth.cs` | Max/current health, `TakeDamage`, `Heal`, `Die`, `HealthChanged`, and `Died` events. Healing does not revive a dead player. |
| `D:\GV project\Assets\Scripts\Core\IInteractable.cs` | Small interface contract for future world objects. It contains no door, pickup, or interaction implementation. |
| `D:\GV project\Assets\Editor\Phase1SceneBuilder.cs` | Unity Editor menu tool that creates the `ZombieMap` greybox scene, greybox materials, player prefab, and build-scene entry. |

## Create the scene

1. Open `D:\GV project` in Unity Hub with Editor `6000.5.3f1`.
2. Wait for the initial asset import and C# compilation to finish.
3. In the Editor, select **Tools > Phase 1 > Build ZombieMap Greybox**.
4. If Unity asks to save an open modified scene, save it or cancel and return after saving.
5. The builder creates `Assets/Scenes/ZombieMap.unity`, `Assets/Prefabs/Player/Player.prefab`, and greybox materials under `Assets/Materials/Greybox/`. It also adds `ZombieMap` to the build scene list and sets Force Text serialization plus Visible Meta Files for Git collaboration.
6. Open `Assets/Scenes/ZombieMap.unity` if it is not already active, then press Play.

The editor script creates a simple primitive humanoid placeholder from capsules and a sphere. It has no rig, Animator, final model, or final graphics.

## Controls

| Input | Action |
| --- | --- |
| `W A S D` | Move relative to the camera |
| Hold `Left Shift` | Run |
| Hold `Left Control` | Sprint (takes precedence if both speed keys are held) |
| `Space` | Jump |
| Mouse movement | Orbit the camera while the cursor is locked |
| `Escape` | Unlock and show the cursor |
| Left mouse click | Relock the cursor while the Game view is focused |

Walking is the default speed. Values are editable on the Player Controller component.

## Scene and GameObject setup

The builder wires references automatically. If setting up the scene by hand, use the following component arrangement:

- **Player** (root): `CharacterController`, `ThirdPersonPlayerController`, and `PlayerHealth`.
- **Humanoid_Greybox** (child): body meshes only; keep colliders off the visual body parts.
- **CameraTarget** (child of Player): at the upper torso, approximately local `(0, 1.48, 0)`.
- **AimTarget** (child of CameraTarget): a placeholder target moved to the camera's forward ray by `ThirdPersonCameraController`.
- **WeaponSocket** (child of Player): approximately local `(0.34, 1.12, 0.22)`, ready for a later held object.
- **GroundCheck** (child of Player): approximately local `(0, 0.08, 0)`.
- **Main Camera** (scene root): `Camera`, `AudioListener`, and `ThirdPersonCameraController`; assign CameraTarget and AimTarget.
- **Environment** (scene root): greybox ground and blocks, with ordinary colliders for grounding and camera obstruction.
- **Directional Light** (scene root).

For hand-built setups, assign the Main Camera transform and GroundCheck to `ThirdPersonPlayerController`; assign CameraTarget and AimTarget to `ThirdPersonCameraController`. Keep the player on **Ignore Raycast** (layer 2), or exclude the player layer from the ground and camera collision masks. The builder uses layer 2 so the player does not obstruct its own camera sphere cast or ground probe.

## Inspector values

### Player / CharacterController

- Height `1.8`, radius `0.32`, center `(0, 0.9, 0)`.
- Step Offset `0.3`, Slope Limit `45`, Skin Width `0.08`.
- The controller's `Ground Layers` mask defaults to Unity's default raycast layers, which omit Ignore Raycast.

### Player / ThirdPersonPlayerController

- Walk `2.2`, Run `4.2`, Sprint `6.5` metres/second.
- Rotation `720` degrees/second.
- Jump Height `1.25` metres; Gravity `-20` metres/second squared.
- Run key `Left Shift`; Sprint key `Left Control`; Jump key `Space`.
- Ground Probe Radius `0.2`; Ground Check at local Y `0.08`.

### Main Camera / ThirdPersonCameraController

- Follow Distance `4.5`; Look-at Height Offset `0.05`.
- Starting Pitch `12`; pitch limits `-25` to `65` degrees.
- Sensitivity `2.5`; Position Smooth Time `0.08`; Rotation Sharpness `18`.
- Aim Target Distance `100`.
- Collision Radius `0.2`; Collision Padding `0.05`; Collision Layers default to Unity's default raycast layers.
- Lock Cursor At Start enabled; Invert Mouse Y disabled.
- Camera field of view `60`, near clip `0.1`, far clip `500`.

### Player / PlayerHealth

- Max Health `100`. Current Health starts at Max Health in `Awake` and is read-only outside the component.
- Subscribe to `HealthChanged(float current, float maximum)` to receive health updates; `Died` fires once when the player dies.

## Testing steps

1. Build `ZombieMap` using the menu action above, then enter Play mode.
2. Walk in all four directions and diagonally. The character should move relative to the camera, face the travel direction smoothly, and stop at the greybox blocks.
3. Hold Shift and Control separately while moving; verify the three speeds differ. Press Space while grounded and verify the player rises, falls, and can jump again after landing.
4. Move the mouse to orbit. Move close to cover and verify the camera shortens its distance instead of passing through the block. Press Escape and click the Game view to test cursor unlock/relock.
5. Verify the aim target tracks the camera center by selecting `Player/CameraTarget/AimTarget` during Play mode and watching its world position update.
6. To exercise health without adding a permanent gameplay feature, use a temporary MonoBehaviour on Player that subscribes to `HealthChanged`, calls `TakeDamage(25)`, `Heal(10)`, then `TakeDamage(100)`, and checks for `CurrentHealth` values `75`, `85`, and `0` with `IsDead == true`. Remove the temporary caller after the check. Calling Heal after death must not revive the player.
7. Confirm the scripts compile. `IInteractable` intentionally has no concrete implementation in Phase 1.

## Common errors and fixes

- **`InvalidOperationException` about Input:** set Active Input Handling to Input Manager (Old) or Both, then restart the Editor if prompted.
- **Player does not move relative to the view:** make sure Player Controller's Camera Transform references Main Camera.
- **Player never grounds or cannot jump again:** confirm GroundCheck is just above the floor, the floor has a collider, and Ground Layers include the floor's layer while excluding the player.
- **Camera clips into blocks:** make sure blocks have colliders and their layer is included in Collision Layers. Keep the player excluded from that mask.
- **Mouse does not rotate camera:** focus the Game view, ensure the cursor is locked, and check that Mouse X and Mouse Y axes exist in the legacy Input Manager. Escape unlocks; left click relocks.
- **Scene builder menu is missing:** let Unity finish compiling and check Console for errors. The builder file must remain under `Assets/Editor/`.
- **Material appears magenta:** use the project's active render-pipeline shader in the generated materials; the builder tries Standard first, then URP/Lit.
- **Duplicate scene objects after a manual setup:** the menu action builds a new single scene. Save the current scene first and run it once for a clean setup.

## Expected Unity hierarchy

```text
ZombieMap
├── Directional Light
├── Environment
│   ├── Ground_Greybox
│   ├── Cover_West
│   ├── Cover_East
│   ├── Cover_North
│   ├── LowBarrier_West
│   ├── LowBarrier_East
│   └── Boundary_* (four greybox perimeter blocks)
├── Player
│   ├── Humanoid_Greybox
│   │   ├── Torso
│   │   ├── Head
│   │   ├── LeftArm
│   │   ├── RightArm
│   │   ├── LeftLeg
│   │   └── RightLeg
│   ├── CameraTarget
│   │   └── AimTarget
│   ├── WeaponSocket
│   └── GroundCheck
└── Main Camera
```

## Phase 1 checklist

- [x] Unity `Assets` folder layout for scenes, scripts, prefabs, models, materials, textures, animations, and audio.
- [x] Modular script folders reserved for Core, Player, Weapons, Inventory, AI, Waves, Environment, and UI.
- [x] Editor builder for a gameplay scene named `ZombieMap` and a Player prefab.
- [x] Humanoid greybox player with WASD, camera-relative walk/run/sprint, jump, gravity, ground probe, and smooth rotation.
- [x] Third-person orbit camera with mouse rotation, smoothing, collision handling, sensitivity, and configurable follow distance.
- [x] CameraTarget, AimTarget, WeaponSocket, and GroundCheck transforms.
- [x] PlayerHealth with max/current health, damage, healing, death, and change/death events.
- [x] IInteractable interface for future world objects.
- [x] Unity Git ignore and text/line-ending settings for team collaboration.
- [ ] Open the project in Unity and run **Tools > Phase 1 > Build ZombieMap Greybox** to generate the scene and prefab assets.

## Suggested Git commit messages

- `chore: scaffold Unity project folders and Git settings`
- `feat(player): add camera-relative third-person controller`
- `feat(camera): add smooth orbit camera with collision`
- `feat(player): add health events and interaction contract`
- `feat(scene): add ZombieMap greybox scene builder`
