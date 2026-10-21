# Imported people, zombies and guns

## Applied replacement

ZombieMap and the existing player/enemy/weapon prefabs now use a textured, skinned military human, a textured rigged zombie and four detailed gun models. The abandoned city and the four AI implementations remain in the project.

- Human movement uses imported Humanoid idle, walk, run, sprint, strafe, jump and falling animation, with camera-relative blend trees.
- Carry, finger grip, firing recoil and reload poses are authored against the imported human's actual arm lengths. An arm mask overlays the carry pose; the action layer overrides it during reload/hit. Carry fades out during sprint, air movement and death.
- While aiming, the torso follows camera pitch before the gun and support hand are aligned. The player root continues to face the camera through the existing controller.
- Zombie walk, attack and death use the source animation. Idle is a stationary breathing variation; run combines imported locomotion with the zombie arm posture. Hunter, Runner, Ambusher and Elite retain separate brains; they share the zombie mesh, with scale/tint differences.
- Weapons use albedo, normal and metallic/smoothness textures. Muzzles and grip points fit the imported geometry. Bolt/slide/pump movement and magazine motion are cosmetic; the existing controller owns ammo, damage, reload completion, audio and firing noise.

These are realistic-style free game assets. The zombie is an older low-polygon model. This is not a photorealistic character pack, and reload poses are reusable approximations rather than separately captured reloads for each firearm.

## Files and hierarchy

```text
Player [existing movement, aiming, health, weapons, animation driver]
  CameraTarget / AimTarget / GroundCheck
  SurvivorVisual [Animator, ShooterBodyPose, SupportHandGrip]
    ... RightHand
      WeaponSocket
        Active weapon [WeaponView, WeaponMechanism]
          ImportedWeaponVisual [source mesh parts]
          Muzzle [existing flash/audio hooks]
          SupportHand
Enemy root [existing brain, movement, health, combat]
  Visual [Animator, skinned zombie, local yaw -90 degrees]
```

| Location | Purpose |
|---|---|
| `Assets/Editor/Art/ImportedCharacterSetup.cs` | Unity-native scene/prefab replacement, Humanoid import, legacy zombie Avatar repair, material assignment |
| `Assets/Editor/Art/ImportedCharacterMotion.cs` | Imported clips, existing blend trees, carry mask and override controllers |
| `Assets/Editor/Art/ImportedShooterPoses.cs` | Calibrated carry, fire, reload and finger muscle curves |
| `Assets/Editor/Art/ImportedWeaponSetup.cs` | Model fit, PBR conversion and cosmetic moving-part references |
| `Assets/Scripts/Player/ShooterBodyPose.cs` | Carry blending and aim pitch |
| `Assets/Scripts/Player/SupportHandGrip.cs` | Left-arm support IK after weapon aiming |
| `Assets/Scripts/Weapons/WeaponMechanism.cs` | Cosmetic gun-part movement |
| `Assets/Art/ImportedCharacters/` | Source human, zombie, animation files and character materials |
| `Assets/Art/ImportedWeapons/CC0/` | Source gun meshes, PBR maps and native Standard materials |
| `Assets/Animations/ImportedMotion/` | Retargeted clips and controllers |

## Inspector and authoring

The project remains on Unity 6000.5.3f1, Built-in rendering and the existing Input/Animator packages. No paid assets or new runtime packages are required.

- Use **Dead city > Art > Apply imported people and zombies** to reapply the character import and poses, then **Apply imported weapon models** for the four guns. These authoring commands overwrite generated art bindings; preserve custom art edits first.
- The older character art menu now delegates to these imported-asset commands when the real human model is present.
- Player Animator: `Survivor.overrideController`, root motion off. Existing Speed, Horizontal, Vertical, IsAiming, IsGrounded, IsSprinting and action parameters are retained.
- Zombie Animator: per-type override of `Infected.controller`, root motion off. Visual yaw is -90 degrees because this source Avatar faces +X. Leave gameplay/collider roots facing +Z.
- Character texture import maximum: 2048 pixels; normal maps imported as normals. Gun materials use Standard, normal strength 0.8 and smoothness map scale 0.85. Metallic/smoothness textures are DXT5-compressed native assets.
- `ShooterBodyPose`: Blend Speed 8. Camera pitch is limited to 45 degrees in either direction and applies 75% to the chest.
- `SupportHandGrip`: Maximum Weight 0.95, Blend Seconds 0.16. It releases during reload, air movement, sprint and death; it clamps targets to arm reach.
- `WeaponMechanism`: travel 0.018 m for rifle/SMG bolt, 0.025 m pistol slide, 0.065 m shotgun pump. It resets on disable/reload cancellation.
- Shooting still uses camera aim point plus muzzle raycast. Gun meshes contain no gameplay colliders; damage remains in the existing weapon controller.

## Manual review procedure

1. Start MainMenu, select a loadout and enter ZombieMap. Inspect the skinned survivor and textured zombies.
2. Walk/run/sprint, move backward and strafe while aiming, jump and land. Observe the transitions and any visible foot sliding.
3. Equip Rifle, SMG, Shotgun and Pistol. Aim above/below the horizon and near cover; check both hands, muzzle position, flash and existing camera/muzzle obstruction behavior.
4. Fire, reload, cancel reload by switching, and pause/resume. Watch the magazine, slide and pump; confirm the HUD still reflects the existing ammo events.
5. Observe all four AI types moving, attacking and dying; confirm their visual forward agrees with their movement. Existing damage timing is unchanged.

No gameplay test suite was run for this art request. Preview images use temporary studio lighting and sampled animation poses. See `VERIFICATION_REPORT.md` for the build result and remaining review limits.

## Common fixes

- **Stretched zombie:** rerun the imported character command. The old FBX's auto-generated skeleton uses source units; the importer command explicitly rebuilds its Avatar skeleton from Unity-converted transforms.
- **Sideways zombie:** restore only Visual's -90 degree local yaw, not the AI root.
- **Pink/blank gun:** use the generated Standard materials and retain the texture files with their `.meta` files; this project is not URP/HDRP.
- **Floating support hand:** adjust SupportHand in the weapon prefab within the arm's reachable distance. Reauthor the carry pose for major grip changes.
- **T-pose after custom changes:** retain the model Avatar and `Survivor.overrideController`; do not assign the old primitive Avatar to the new human.
- **Slide/magazine stuck:** ensure WeaponMechanism references are on the same root as WeaponView. Both reset on disable/cancel.

Selective pre-import backup: `Builds/Backups/BeforeImportedCharacters.zip`. Full source code is also collected in `CURRENT_SOURCE.md`. Credits/licenses: `THIRD_PARTY_CHARACTER_ASSETS.md`.

## Suggested commits

- `feat(art): replace mannequins with licensed human and zombie models`
- `feat(animation): retarget locomotion and calibrate weapon handling`
- `feat(weapons): integrate four textured gun models and moving parts`
- `docs(art): record free asset licenses and integration guidance`
