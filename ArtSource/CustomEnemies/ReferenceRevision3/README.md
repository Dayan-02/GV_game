# Reference-directed enemy model revision

This folder contains editable Blender revisions of the project's original enemy
models. The modelling source derives from `../Revision2/build_enemies.py`; the
skeletons and animation actions come from `../BlenderAnimationPass/Blender/`.
The image references are in `../ConceptTurnarounds-20261020/`.

## Scope and provenance

These are AI-assisted procedural modelling revisions, with AI-generated concept
art used as visual reference. They must not be described as manually sculpted
student work. No downloaded character meshes or paid generation services are used.

The files preserve the original skeleton names and rest transforms and retain
each character's saved actions. They open in a neutral A-pose for inspecting the
new surfaces. Select the rig and choose an action in the Action Editor to play
the existing animations.

The hidden reference collection contains the packed four-view concept image.
Enable its viewport visibility to inspect it alongside the character.

## Changes by character

| Character | Reference-directed changes |
| --- | --- |
| Hunter | Removed hardhat and reflective strips; added matted hair, longer sleeves, collar, buttons, pocket flaps and weathered workwear colours. |
| Runner | Removed the diagonal harness and sleeve remnant; added athletic shorts, exposed lower legs, side seams and matted hair. |
| Ambusher | Extended the coat silhouette, added long sleeves, torn hanging hems, hood edging, rope belt and diagonal strap; darkened the cloth. |
| Elite | Removed helmet and floating shoulder plates; added integrated shoulder/arm volume, back carrier, webbing, cargo pockets and gloves; changed trousers to industrial orange. |

All four receive revised facial colour placement, shoulder joins, footwear and
local hand UVs. The earlier standalone eye and tooth inserts were removed to
avoid duplicating the expression already present in the reference colours.

## Surface workflow

The `Guide_Front`, `Guide_Back`, `Guide_Left`, and `Guide_Right` UV layers are
reference projections. The current colour material primarily blends front and
back samples. Small details use separate procedural materials.

These colour projections retain some lighting from the concept art and can have
seams or occlusion artefacts. They are look-development materials, not final
seamless PBR texture atlases. A final sculpt and retopology, UV cleanup, and
albedo/normal/roughness baking are separate production steps. The pictures are
design targets; the generated meshes do not reproduce every visible detail.

## Files

- `revise_models.py`: reproducible geometry, materials, rig preservation, saving,
  and actual Blender preview rendering.
- `finish_hands.py`: final hand-UV correction applied to the saved revision.
- `review_models.py`: reopens saved files, checks weights and sampled deformation,
  and generates the combined review.
- `Blender/*_ReferenceRevision3.blend`: revised individual characters.
- `Blender/Four_Enemies_ReferenceRevision3.blend`: all four models in one editable
  review scene.
- `Previews/`: renders of the actual edited meshes, not generated concept images.
- `Evidence/*.json`: source/output hashes, topology counts, weights, action
  inventory, and skeleton comparison.

Earlier source files and Unity assets are not overwritten by this workflow.

Validation covers unchanged rest-bone transforms, preserved action inventory,
normalized skin weights, finite mesh positions in five sampled poses each from
Walk, Chase and Attack, and unchanged earlier source-file hashes. It does not
certify every animation frame, seamless UVs, mesh intersections or Unity import.
