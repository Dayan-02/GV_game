# One current playable version

Cleanup completed 20 October 2026 at the project owner's request.

## Keep and use

- Startup: `Assets/Scenes/Opening.unity`
- Menu: `Assets/Scenes/MainMenu.unity`
- Loadout: `Assets/Scenes/Loadout.unity`
- Gameplay: `Assets/Scenes/EnhancedCity/ZombieMap.unity`
- Standalone game: `Builds/Windows/DeadCity.exe`, with its entire containing folder.

## Removed

- `Assets/Scenes/ZombieMap.unity` and its meta file.
- `Assets/Scenes/ArtBackups`, including the earlier city scene and meta files.
- The outdated `Builds/ZombieOutbreak-Windows.zip`.
- `Builds/Backups`, containing older Windows game archives.
- Sixteen previous game-code/prefab/scene backup folders under `Backups` (the Before* and CustomEnemyNativeImport-* folders). Exact paths are in `Logs/RemovedOldGameVersions.txt`.

The asset source archive `Backups/RealismAssetPackage`, Blender authoring files, asset-review projects and visual preview evidence are retained as production resources. They are not selectable game versions. Historical development guides may describe removed paths; this guide and PLAY_AND_SETUP describe current startup.

## Prevention

- Shared Editor scene references use the single enhanced city path.
- Phase 1-4 prototype generation refuses to overwrite the finished project.
- The old art pass no longer creates backup scenes inside Assets.
- Scene setup and build entry points use the same saved four-scene flow.
- `CurrentGameScenePolicy` rejects incorrect build lists and obsolete gameplay scene paths during a build.

No gameplay assets, model sources or Git history were purged. No gameplay tests were run for this cleanup. The current Windows executable already contains the opening video and enhanced city; this cleanup changes Editor setup and removes superseded local copies.
