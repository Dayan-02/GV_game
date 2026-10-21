# Weapon handling and enemy death update — 20 October 2026

## Source

User recording: `20261020-1553-43.9461402.mp4`.
Frames extracted under `HandlingReview/` cover the full 76-second recording.

## Weapon handling

- Independent critically damped translation and pitch springs replace the
  linear recoil ramp. Rifle, SMG, shotgun and pistol have different impulses.
- Weapon changes start in the lowered equip pose and ease into the ready pose.
- Aiming eases out during reload/sprint, then returns to the requested aim.
- Reload timing has distinct reach, withdrawal, insertion and return phases.
  The support hand follows the actual magazine position rather than an
  unrelated point below the weapon.
- Execution order is weapon pose (160), firing (200), moving mechanism (250),
  and arm IK (350). The support hand follows the pump's current position.
- Arm reach retains a small elbow bend to avoid straight-arm singularities.
- Shooting, ammunition, damage and reload completion remain owned by the
  existing weapon controller.

## Enemy death

`author_deaths.py` creates forward, backward and side collapse actions for each
of Hunter, Runner, Ambusher and Elite. The editable copies retain the previous
actions and the sculpted mesh. Original Blender source files are not modified.

Each take includes an initial reaction, loss of knee support, fall, contact and
settling phase. Torso clearance is established before solving the limbs to the
floor, avoiding whole-body elevation driven by folded feet. The final pose is
held. Elite takes are longer to convey its greater weight.

`CombatHandlingDeathSetup.Apply` imports animation-only FBXs with Preserve
Hierarchy enabled so their armature paths match the existing models. It also
supports copying curves into `.anim` files with the armature prefix restored
as a fallback if an importer collapses the root. This
preserves the existing mesh, material, skeleton and locomotion references.
Only death states/transitions and their two parameters are changed.

The runtime selects one of three variants with a small playback-rate variation.
Death prevents late attack/hit transitions. Corpses remain for nine seconds,
including the collapse. These are authored animations, not physics ragdolls;
they do not conform every limb to stairs or nearby props.

## Files

- Editable sources: `DeathRevision/Blender/*_DeathRevision.blend`
- Animation exports: `DeathRevision/Exports/*_Deaths.fbx`
- Blender pose renders: `DeathRevision/Previews/`
- Source hashes and action inventory: `DeathRevision/Evidence/authoring.json`
- Unity clips: `Assets/Art/CustomEnemies/DeathAnimations/`
- Import receipt: `Logs/CombatHandlingDeathImport.txt`
- Build result: `Logs/OpeningVideoBuild.txt`
- Backups: `Backups/HandlingDeath-20261020/`

No automated tests or gameplay session were requested or run. Blender renders
are asset review evidence; they do not establish the in-game visual result.

## Completion

All twelve takes were imported and bound successfully. Preserve Hierarchy
allowed direct FBX clip references; no expanded `.anim` copies are needed.
The final Windows build completed on 20 October 2026 at 22:01 Sri Lanka time:
**Succeeded, 0 errors, 1 warning**. The warning is the existing optional Pipeline
runtime configuration warning; the game's runtime does not require that editor
connection. The current Opening scene was saved when Unity requested it.

Executable: `D:/GV project/Builds/Windows/ZombieOutbreak.exe`.
Pose sheet: `DeathRevision/Previews/DeathVariants.png`.
