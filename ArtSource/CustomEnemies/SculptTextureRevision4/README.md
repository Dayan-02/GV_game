# Enemy sculpt and texture revision 4

Four edited Blender characters: Hunter, Runner, Ambusher and Elite.
Open `Blender/Four_Enemies_Sculpt4_Review.blend` for the combined review.

## What changed

- Reshaped brows, eye sockets, cheekbones, hollow cheeks and mouth area.
- Added shaped eye surfaces and thin eyelids.
- Broadened the Elite's jaw.
- Replaced the Hunter and Runner's thick hair locks with short, finer mesh strands.
- Added geometric clothing folds around knees, hems, waists and elbows.
- Rebuilt footwear with shaped uppers, ankle openings, thinner soles, seams,
  crossed laces and shallow tread.
- Replaced large flat Elite armour plates with curved plates, rims and rivets.
- Added higher-resolution facial colour sources, skin pores, fabric detail and
  roughness variation.
- Created a packed, non-overlapping `AtlasUV` layout and baked colour (4096×4096),
  roughness, metallic and tangent normal (2048×2048) textures.

## Files and editing

For each character:

- `Blender/<Name>_Sculpt4.blend`: rigged model with baked texture material.
- `Blender/<Name>_Sculpt4_EditableMaterials.blend`: the same refined geometry
  with the editable projection and procedural material graphs used for baking.
- `Textures/<Name>_BaseColor_4K.png`: colour atlas, sRGB.
- `Textures/<Name>_Roughness_2K.png`: roughness, linear/non-colour.
- `Textures/<Name>_Metallic_2K.png`: metallic, linear/non-colour.
- `Textures/<Name>_Normal_2K.png`: tangent-space normal, linear/non-colour.
- `Sources/<Name>_Face.png`: generated face colour source, **not a model render**.
- `Previews/`: actual native Blender renders.

Images are also packed into the Blender files. The models open in an A-pose.
Select a rig and select its saved action in the Action Editor to inspect motion.
The existing Idle, Walk, Chase, turns, Attack, Hit, Stagger and Death actions are
retained; Elite also retains HeavyAttack.

The refined geometry is the editable sculpt surface. No separate high/low-poly
retopology pair or Multires sculpt stack is claimed. Normal maps capture material
microdetail on the refined meshes; they are not high-to-low mesh normal bakes.

## Scope and evidence

Earlier Blender files remain unchanged. On 20 October 2026, this revision was
imported into Unity at the user's request and included in the Windows build.
The existing four `Assets/Art/CustomEnemies/Models/*_Animated.fbx` files were
updated in place, retaining their GUIDs. Enemy prefab material overrides now use
the new atlas materials. See `UNITY_IMPORT.md` for the integration record.

`Evidence/review.json` records source hashes, topology counts, bone-rest comparison,
skin-weight checks, packed images and sampled deformations in existing actions.
Walk and Attack preview images show actual posed geometry. These samples do not
prove that every animation frame or every gameplay interaction is free of clipping.

## Provenance and remaining limits

AI-assisted procedural mesh editing in native Blender, based on the project's
existing custom meshes and rigs. Face references were generated with the built-in
image tool. No downloaded character meshes or paid asset services were used.
Prompts are recorded in `Sources/provenance.json`.

The Hunter and Ambusher use a less graphic facial treatment after the image
service rejected their initial outputs. Runner and Elite retain stronger infected
facial detail.

This is a further art refinement, not a claim of finished cinematic-quality models.
Clothing colours still inherit some painted lighting and imperfect projections
from the earlier reference sheets. Baking makes those materials portable; it does
not turn the source imagery into fully neutral scanned albedo. Close-up seam
painting, finer hand and ear anatomy, dedicated hair cards, facial rigging and
Unity lighting/motion review remain possible later improvements.

## Reproduction

Run `refine_and_bake.py -- <Name>` with Blender's background Python runner for
each character, then run `review_and_package.py`. The first script reads only
Revision3 models and this folder's face sources and writes this revision.
The review script reads the saved results and creates the combined review file.
