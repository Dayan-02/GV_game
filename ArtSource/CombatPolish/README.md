# Combat presentation correction

## Observed in the supplied recording

The muzzle and hit effects appear as bright yellow square tiles. The existing
particle material has no opacity texture, so the complete particle quad is
visible. Enemy hits also invoke both an environment impact at the raycast contact
and a second enemy effect at a fixed height above the enemy root.

## Changes

- `SoftCombatParticle.shader`: explicit transparent blending and an analytic,
  feathered opacity mask. The quad boundary is always transparent. Flame and
  dust/contact shapes share one small shader; no bitmap download is required.
- `CombatPresentationSetup.cs`: updates the existing four combat effect prefabs
  in place, retaining their GUIDs and root ParticleSystem references.
- Muzzle: one short additive flare and a faint separate smoke puff.
- Environment contact: small muted dust, directed away from the impact normal.
- Enemy contact: small dark-red soft particles at the actual bullet contact;
  no yellow environment burst on enemies.
- Death: a small low-opacity dust puff near the feet.
- `WeaponView.cs`: replays the flash for each shot while previously emitted smoke
  finishes independently.
- `EnemyAnimationController.cs`: rate-limits cosmetic hit reactions and does not
  queue them while another hit reaction or transition is active. Damage is still
  applied on every hit.

Existing hit detection, weapon damage, ammo, AI decisions and movement clips
are retained. Impact placement follows the existing physics collider contact.

## Applying the asset changes

Outside Play Mode, run **Tools → Dead Zone → Apply soft combat effects**.
This invokes `CombatPresentationSetup.Apply`. Code and shader changes also need
Unity's normal compilation/import.

The original scripts and effect prefabs were saved under:
`D:/GV project/Backups/CombatPresentation-20261020-210737`.

Import completion is recorded in `Logs/CombatPresentationImport.txt`.
The latest Windows build result is recorded in `Logs/OpeningVideoBuild.txt`.
No automated tests or gameplay session are part of this correction.

## Applied build

Applied in the open Unity 6000.5.3f1 editor on 2026-10-20.
The Windows build completed at 21:20 local time: **Succeeded, 0 errors, 1 warning**.
The updated game is `D:/GV project/Builds/Windows/ZombieOutbreak.exe`.
Gameplay appearance still needs an in-game review; build success is not visual
verification.
