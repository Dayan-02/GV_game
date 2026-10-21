> Historical procedural art pass. The live project now uses imported skinned people/zombies and detailed free gun models. See [REALISTIC_CHARACTER_ART.md](REALISTIC_CHARACTER_ART.md) for the current implementation.

# Character, weapon and movement art — 12 October 2026

## Applied in ZombieMap

- Player, Hunter, Runner, Ambusher and Elite have canvas clothing, denim trousers, leather boots/gloves, skin materials and equipment details. Enemy clothing colours remain distinguishable. Clothing uses tapered meshes rather than the previous rounded capsule silhouettes.
- Rifle, SMG, Shotgun and Pistol have separate detailed visual assemblies, including bevelled receivers, cylindrical barrels, trigger guards, sights, ejection ports and furniture. The shotgun uses walnut furniture, the rifle tan polymer, and the SMG/pistol dark polymer. Muzzle transforms, ammo, damage, firing and audio remain connected to the existing weapon system.
- Seven deterministic 512×512 surface families supply albedo, normal and metallic/smoothness maps. These are procedural project assets, not photographic scans. All maps and materials can be edited or replaced independently.
- Revised Humanoid clips cover idle breathing, walk/backward/strafe, run, sprint, jump, fall, aim movement, fire, reload, hit and death. Movement includes hip sway, knee lift, foot roll and torso counterrotation. These remain generated in-place clips, not motion capture.
- Each enemy uses an Animator Override Controller for its idle/walk/run: Hunter has an uneven stride; Runner leans and swings more; Ambusher crouches; Elite uses a shorter, heavier gait. Shared attack timings and decision algorithms remain in their existing systems.
- `SupportHandGrip` follows the active weapon's SupportHand target after weapon aiming. It blends out for reload, air movement, sprint and death. It adjusts the left arm only; it does not move the root, right hand or muzzle and does not deal damage. Final finger gripping and a fully skinned production character remain future art work.

## File map

| File | Responsibility |
|---|---|
| `Assets/Editor/Art/CharacterEquipmentArtPass.cs` | Main authoring command; applies character appearance, weapon art and clips |
| `Assets/Editor/Art/CharacterSurfaceLibrary.cs` | Reproducible texture generation/import and shared Standard materials |
| `Assets/Editor/Art/WeaponEquipmentArt.cs` | Builds distinct weapon visuals and support-hand markers |
| `Assets/Editor/Art/CharacterMotionAuthoring.cs` | Samples motion into existing clips, preserves asset GUIDs, creates enemy gait overrides |
| `Assets/Scripts/Player/SupportHandGrip.cs` | Optional runtime support arm alignment |
| `Assets/Scripts/Weapons/WeaponView.cs` | Exposes the serialized SupportHand marker |
| `Assets/Scripts/Weapons/PlayerWeaponController.cs` | Exposes the active weapon view for presentation |
| `Assets/Editor/Phase2/Phase2HumanoidFactory.cs` | Supports an optional pose author callback; existing callers retain their old behavior |

Textures/materials/meshes: `Assets/Art/CharactersAndWeapons/`.
Player clips: `Assets/Animations/Phase2/Clips/`.
Shared enemy action clips: `Assets/Animations/Enemies/Clips/`.
Enemy gait overrides: `Assets/Animations/CharacterMotion/`.

## Hierarchy and Inspector

```text
Player
  Humanoid_Phase2 [Animator, SupportHandGrip]
    Hips
      SurfaceBelt
      ... Chest
        SurfaceEquipment
        ... RightHand
          WeaponSocket
            Active weapon [WeaponView]
              Muzzle [existing flash/audio hooks]
              SupportHand
              DetailedWeaponArt
```

- On `SupportHandGrip`, Maximum Weight defaults to **0.95**, Blend Seconds **0.16**. Disable the component to use the authored arm animation alone. Bone and player references are cached once in Awake, without per-frame scene searches.
- Adjust **SupportHand** in each weapon prefab to place the left palm under the foregrip. The solver clamps to arm reach. Moving this target does not change shot origin or hit detection.
- Material albedo controls tint; normal strength controls small surface detail. Leather, clothing and skin use nonmetal smoothness maps. Steel uses its metal map. Normal and smoothness imports are linear; albedo is sRGB; mipmaps and anisotropic filtering are enabled.
- Player Animator retains its directional blend trees and action layer. Reload remains a 1.2-second source clip scaled by ReloadSpeed. Enemy impact poses retain the shared 60%-of-attack impact timing.
- All scripts use the existing Built-in pipeline, Humanoid Animator and Legacy Input setup. No new packages, external accounts or paid assets are required.

## Authoring safely

Run **Dead city > Art > Apply character textures weapons and motion** in Edit Mode. This command overwrites the generated appearance and clips, so preserve custom edits before rerunning. It skips character models without the generated GreyboxMesh parts. Run it after the earlier city/clothing setup commands if you use those historical tools again.

The prior character/weapon prefabs, animations, scene and city materials were archived at `Builds/Backups/BeforeCharacterTextures.zip` before applying this update. This is a selective backup, not a complete project archive. Existing `.meta` files on the live assets are retained.

Texture generation is deterministic. `SurfaceRevision2.txt` records the current map generation; remove that marker only when deliberately regenerating the complete surface family. Keep maps and `.meta` files in Git. The new content is generated project art and must not be represented as student-authored Blender/Maya work.

## Review and limitations

Native Unity renders were inspected for the character lineup, four weapon prefabs, an aimed player pose, and a walk/run/sprint sequence. Captures are under `Logs/Captures`: `Character_Lineup.png`, `Weapon_Lineup.png`, `Character_Aim.png`, and `Movement_Preview.gif`. Preview cameras and sampled poses are temporary; they do not change the saved gameplay camera.

No automated gameplay test suite was added or run for this request. The manual review still needed is movement feel, grip placement while looking steeply up/down, weapon switching/reload at different speeds, and foot sliding on slopes. These rigid generated character meshes have no individual finger bones or final facial art. The changes improve the existing prototype rather than supplying a realistic sculpted/skinned character.

Suggested commits:

```text
feat(art): texture characters and add distinct weapon visuals
feat(animation): refine humanoid locomotion and enemy gait variants
feat(player): add weapon support-hand alignment
docs(art): record character materials motion and limitations
```

