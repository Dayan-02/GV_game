# Unity integration — 20 October 2026

## Installed assets

- Updated `Assets/Art/CustomEnemies/Models/{Hunter,Runner,Ambusher,Elite}_Animated.fbx`.
- Added twelve textures under `Assets/Art/CustomEnemies/Textures/Sculpt4/`.
- Added four Standard materials under `Assets/Art/CustomEnemies/Materials/`,
  named `<Kind>_Sculpt4_PBR_Atlas`.
- Updated the material overrides on EnemyBase, Hunter, Runner, Ambusher and Elite.
- Added `Assets/Editor/Art/SculptedEnemyImport.cs` for repeatable integration.
- Updated the earlier importer to recognize the new material names.

The exporter makes `AtlasUV` the sole FBX UV channel, so it becomes Unity UV0.
The Blender review A-pose is cleared before exporting. An action slot is assigned
explicitly so Blender exports all existing animation actions.

The colour texture imports as sRGB at 4096 pixels. Normal maps import as normal
maps at 2048 pixels. Metallic/smoothness maps import as linear textures at 2048
pixels, with metallic in red and `1 - roughness` in alpha. Mipmaps and high-quality
texture compression are enabled.

## Import result

Unity 6000.5.3f1 completed the import successfully:

| Enemy | Animation clips | Skin bones | Unity vertices after seam splitting |
| --- | ---: | ---: | ---: |
| Hunter | 9 | 52 | 68,390 |
| Runner | 9 | 52 | 47,422 |
| Ambusher | 9 | 52 | 43,652 |
| Elite | 10 | 52 | 40,854 |

All four existing model GUIDs were retained. The AI source files and animation
controller files recorded before import have unchanged SHA-256 hashes.
No scene layout, AI behaviour or animation-controller transitions were edited.

Source/export/copy hashes and protected-file hashes are recorded in
`UnityExports/export-receipt.json` and `UnityExports/import-receipt.json`.
These are native workflow receipts, not Game Development Studio CLI validation.

Previous models, metadata and prefabs are backed up outside Unity's Assets folder:
`D:/GV project/Backups/Sculpt4Import-20261020-200541`.

## Windows game

Rebuilt `D:/GV project/Builds/Windows/ZombieOutbreak.exe` with its accompanying data.
Build result: **Succeeded, 0 errors, 1 warning**.

The warning reports that the optional Unity Pipeline runtime configuration is
absent, so its runtime connection is disabled in the player. This does not report
an enemy model, material or animation import failure.

The opening video and existing Opening → MainMenu → Loadout → EnhancedCity scene
flow are retained. A gameplay session was not run.

Logs:

- `D:/GV project/Logs/SculptedEnemyImport.txt`
- `D:/GV project/Logs/Sculpt4UnityImport.log`
- `D:/GV project/Logs/Sculpt4WindowsBuild.log`
- `D:/GV project/Logs/OpeningVideoBuild.txt`

The existing art limitations documented in README.md still apply; this import
does not constitute a gameplay or visual-quality sign-off.
