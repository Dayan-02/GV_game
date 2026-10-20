# Custom Blender enemy integration

## Current status

Installed on 18 October 2026 in Unity 6000.5.3f1. **Hunter, Runner, Ambusher, Elite and EnemyBase now use the custom Blender visuals and animation controllers.** All 37 animation takes imported successfully. Inspection of the saved prefabs confirmed one valid rig per prefab, assigned animation drivers/controllers, textured materials and disabled root motion. Unity reported no script compilation failure. A gameplay playthrough has not been run in this import pass.

Native import receipt: `Logs/CustomBlenderEnemyImport.txt`. Saved-reference inspection: `Logs/CustomEnemySavedReferences.txt`. The pre-import native prefab backup is `Backups/CustomEnemyNativeImport-20261018-171108`.

## Included files

- `Assets/Art/CustomEnemies/Models`: four animated FBX files, exported from the latest `ArtSource/CustomEnemies/BlenderAnimationPass/Blender/*_Animated.blend` sources.
- `Assets/Art/CustomEnemies/Textures`: original custom PBR maps from Revision2.
- `Assets/Editor/Art/CustomBlenderEnemySetup.cs`: native Unity importer, material/controller authoring and enemy prefab visual replacement.
- `ArtSource/CustomEnemies/BlenderAnimationPass/export_unity.py`: reproducible FBX export without embedded duplicate images. Original Blender files are unchanged.

The initial backup is `Backups/BeforeCustomBlenderEnemies-20261018-161525`. It contains the enemy prefabs and the previous animation driver. The native utility also backs up all five prefabs before running.

## Reapply after editing Blender sources

1. Open the existing `D:/GV project` project in Unity Hub/Unity Editor and leave Play Mode.
2. Allow the assets and editor scripts to import. Resolve any Console compile errors before proceeding.
3. Choose **Tools > Dead Zone > Import custom Blender enemies**.
4. Wait for the `Custom Blender enemy integration` Console message and `Logs/CustomBlenderEnemyImport.txt`.
5. The existing Hunter, Runner, Ambusher, Elite and EnemyBase prefab GUIDs are retained, so the current wave/spawn references continue to use them.

Before reapplying, rerun `export_unity.py` in Blender and copy its `UnityExports/*.fbx` outputs into `Assets/Art/CustomEnemies/Models`. Keep the existing Unity `.meta` files so references remain stable.

## Integration choices

- The enemy rigs use Generic animation to preserve their exact Blender bone transforms. The player Animator is unaffected.
- Navigation owns translation and rotation. Apply Root Motion is disabled; no runtime foot-IK overlay is added.
- Idle/walk and chase use the existing speed-driven animation interface, with corrected stride rates accounting for baked frame duration and Elite scale.
- Attack/Heavy states use the existing combat clock and 0.56 contact timing. Hit and Death are wired into the existing health events.
- All 37 takes are imported. TurnLeft, TurnRight and Stagger are retained for later use; they are not automatically connected to AI. Turn clips need an explicit root-orientation handoff to avoid doubling navigation rotation.
- No AI decision rules, player controls, city layout or wave definitions are changed. The only runtime edit allows initial locomotion phase variation on valid Generic avatars as well as Humanoid avatars.
- No standalone Windows game rebuild has been made during this preparation.

These remain AI-assisted procedural models/animations, not student-manually-authored or motion-capture assets. Uneven-ground foot placement, clothing intersections and final gameplay motion quality require review after native import.
