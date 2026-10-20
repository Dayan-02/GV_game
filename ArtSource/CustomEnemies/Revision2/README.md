# Custom enemies — city and animation refinement

This is an iteration of the four original AI-assisted Blender prototypes. The first version is preserved in the parent folder. These candidates do not replace the live enemy prefabs.

## Changes

- Continuous upper-body skin across shoulders and elbows, with blended joint weights.
- Continuous trousers across the seat, hips and knees; covered leg surfaces removed to reduce intersections.
- Small cloth compression folds and less exaggerated torn hems.
- Original 1024-pixel base colour, tangent normal, roughness and metallic/smoothness maps. Fine surface detail is now exportable rather than a Blender-only bump effect.
- Hunter workwear, lean Runner, hooded Ambusher and armoured Elite identities retained.

## Files

- `Blender/`: editable sources. All four characters are retained in separate collections.
- `Exports/`: neutral-pose FBX and GLB files with the shared humanoid skeleton.
- `Textures/`: original procedural maps, packed into the Blender sources.
- `build_enemies.py`, `surface_and_deformation.py`: the modelling recipe.
- `export_preview_meshes.py`: lightweight, external-texture FBX copies for the separate Unity preview.

The isolated Unity project is `D:/GV project/Previews/CustomEnemyMotion`. Its editor setup creates Humanoid Avatars, uses copies of the game's current per-type motion clips, adjusts body height for the new soles, and measures stride speed on the new proportions. Navigation remains the intended owner of movement; the clips do not move the gameplay root.

## Review boundaries

The candidates are still stylized procedural characters. Cloth folds and PBR maps do not make them photorealistic. Retopology around the face/hands and further silhouette work would be needed for close-up cinematic quality.

The first dense draft had a shirt-remeshing defect and was superseded. A subsequent Blender render ran out of memory after saving the repaired source and exports. The isolated Unity preview also encountered importer crashes; lightweight model copies with external textures were created for the retry. Only completed output and the accompanying final report should be treated as review evidence.

## Completed review output

The final isolated Unity run completed all **270 frames at 30 fps** across idle, walk, chase, attack and heavy-strike sections. All four imported Avatars are valid Humanoids with 52 mapped bones. The lightweight Unity FBX copies use consistent export units; use the candidate prefabs in the preview project for this reviewed setup.

`Previews/Repaired_Lineup.png` is the final Blender still. Earlier renders are retained only under `Evidence/SupersededRenders`. The Unity video is `D:/GV project/Previews/CustomEnemyMotion/ReviewOutput/CustomEnemies_MotionReview.mp4`; its motion report records per-model corrections and measured stride speeds. This is an isolated authoring preview, not a gameplay recording or a promise of flawless foot contact on uneven terrain.

Final source mesh triangle counts: Hunter 59,538; Runner 33,428; Ambusher 36,406; Elite 44,614. Every source vertex has a bone weight. Material counts and these mesh costs still require optimization before use in large crowds.

Visual review found visibly bending knees and articulated arms, but also stylized faces, bulky shoe shapes, shoulder/garment contours and abrupt trouser folds. These remaining issues are reasons to continue art refinement before replacing the live enemies. City-level lighting, animation transitions, turning, slopes and live combat timing have not been signed off.

No live city geometry, player animation, AI, enemy prefab or gameplay controller is changed by this iteration. The source motion remains subject to the existing game's animation licence records. Model and procedural texture authorship remains **AI-assisted procedural modelling by Codex in Blender**, not manual student modelling.
