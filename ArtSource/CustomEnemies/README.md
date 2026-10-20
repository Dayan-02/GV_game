# Original custom enemy prototypes

Four original, AI-assisted models created procedurally in Blender 5.2 for Zombie Outbreak. No existing player/zombie mesh, downloaded character or third-party texture was used as an input.

These are editable model prototypes with UVs, generated textures, a shared 52-bone humanoid skeleton and initial skin weights. They have **not** replaced any Unity character.

| Enemy | Design | Editable source |
| --- | --- | --- |
| Hunter | Work shirt, hardhat, reflective strips, utility pouch | Blender/Hunter.blend |
| Runner | Narrow frame, asymmetric ripped shirt, diagonal strap, torn trouser cuff | Blender/Runner.blend |
| Ambusher | Dark canvas hood, ragged mantle, concealed silhouette | Blender/Ambusher.blend |
| Elite | Broader body, helmet, breastplate, shoulder and shin armour | Blender/Elite.blend |

Open **Blender/Four_Enemies_Review.blend** to inspect the four posed characters together. Individual files include the full editable collection set, with the selected character visible. Textures are packed into the Blender files.

## Files

- Blender/: editable source scenes and studio lighting.
- Exports/: each model as FBX and GLB in its neutral T pose. FBX embeds/copies image textures; GLB embeds its texture images.
- Textures/: generated 512 × 512 colour maps. Roughness and metalness are material values. Fine bump is a Blender preview effect and is not a baked game normal map.
- Previews/: actual Blender front/rear renders and the four-character lineup.
- Evidence/mesh_report.json: source mesh counts, UVs, bones and unweighted-vertex counts.
- Evidence/export_validation.json: independent re-import and structural checks on the actual FBX/GLB files.
- Evidence/sha256.json: source/export/render hashes.
- build_enemies.py: editable modelling recipe. Running it overwrites this asset set.
- validate_exports.py: independent export inspection.

## Review limits and next steps

Independent Blender re-import passed for all eight exports (four FBX and four GLB files). Each imported model retained its 52 bones, normalized skin weights, UVs and texture images. Static knee/arm bends produced finite, bounded geometry. Source geometry is approximately 28,154–31,282 triangles per character, with 9–11 material slots.

Front/rear renders and the lineup were inspected. Initial exposed waist/kneecap intersections, excessive skin blotching, high shoulder caps and rear calf intersections were revised. Segmented garment/joint contours and stylized facial anatomy remain visible; these are not a photorealism or production deformation sign-off.

The models are original stylized prototypes, not scanned or photorealistic characters. Their geometry has separate overlapping clothing/body surfaces and is not a production retopology pass. A shared skeleton does not by itself guarantee correct Unity Humanoid mapping or natural animation.

Before applying them to the game:

1. Review the silhouette, face and clothing in the previews.
2. In Blender, refine anatomy and garment intersections. Check shoulder, hip, elbow and knee deformation under the game's intended poses.
3. In an isolated Unity import, map the Humanoid Avatar, retarget the existing motion clips and tune feet, root height and collider scale.
4. Check lighting/materials in the game's Built-in renderer. Consolidate materials or bake atlases if draw calls become a problem.
5. Replace only the reviewed visual child in each enemy prefab, retaining the existing AI, combat and navigation components.

No walking, running or attack clips are supplied by this modelling step. The static preview poses and numerical bend checks do not establish animation quality.

## Authorship and assignment evidence

Authorship: **AI-assisted procedural modelling by Codex using Blender**, requested by the project owner. Do not label these files as manually modelled by a student. Keep this record and the actual contribution history. Whether these satisfy a team-authored modelling criterion depends on the lecturer's rules for AI-assisted work.

User request: “okay just make coustom models for enimies”.

Design brief: four original infected humanoid enemy models, distinct workwear / lean runner / hooded stalker / armoured heavyweight identities, editable source, neutral-pose exports, humanoid skeleton, texture maps and real mesh previews.

Exclusions: no vendor mesh inputs, no paid generation service, no Unity prefab replacement, no invented student authorship and no claim of finished locomotion animation.
