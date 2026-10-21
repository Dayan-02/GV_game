# Realistic city graphics update

14 October 2026 · Unity 6000.5.3f1 · Built-in renderer

## Open the updated game

The current city is **Assets/Scenes/EnhancedCity/ZombieMap.unity**. This is the scene left open in Unity. The similarly named **Assets/Scenes/ZombieMap.unity** is the original map and does not contain this presentation pass. The Windows player uses the enhanced scene.

Run **Builds/Windows/DeadCity.exe**, keeping its complete folder together, or open the enhanced scene in Unity and press Play. For the full menu/loadout flow, open Assets/Scenes/MainMenu.unity.

## Changes

- Linear lighting, a photographic overcast HDR sky, stronger architectural shadows and restrained atmospheric depth.
- Filmic highlight roll-off, subtle bloom, edge antialiasing and contact occlusion. HUD rendering remains separate from scene grading.
- Free scanned covered vehicles fitted inside the existing parking footprints; scanned barrel skins exposed in place of the old primitive surfaces.
- 138 fern/shrub renderers with photographic colour, opacity, normal, roughness and occlusion maps. Distant plants cull through LODGroups. Angular stand-in curb grass is hidden.
- Weathered brick/concrete/asphalt materials, dampness on the road surface and worn paint flattened onto the road. Disconnected opaque puddle polygons are hidden.
- Revised survivor/infected surface roughness and dirt. Character meshes, movement/foot placement, weapon sockets and four enemy behaviors are retained.
- Baked local reflections, avoiding a realtime cubemap capture every frame.

The environment upgrade preserves all 6,796 existing environment transforms, 126 collider definitions and the NavMesh data reference. It adds visual foliage and replaces vehicle skins without moving buildings, roads, cover, spawn points or gameplay routes. The original scene is retained. Rendering/material changes are confined to the enhanced scene's art, except project-wide Linear color space and quality settings.

## Main editable files

- Assets/Editor/Art/RealisticCityUpgrade.cs — native Unity authoring, lighting refinement, reflections and still captures.
- Assets/Scripts/Environment/CinematicCityCamera.cs — camera presentation controls.
- Assets/Shaders/CinematicCity.shader — HDR bloom, haze, tone mapping and edge smoothing.
- Assets/Shaders/ScannedFoliage.shader — double-sided masked plant surfaces and restrained wind.
- Assets/Shaders/WornRoadPaint.shader — worn paint on the retained roadway.
- Assets/Shaders/DirtyCityGlass.shader — dusty glass with rain streaks and variable roughness.
- Assets/Art/Realism/Materials — new vehicle, plant and sky materials.
- Assets/Art/Realism/Reflections — baked city reflection cubemap.
- Assets/Art/Realism/PolyHaven — original free asset files and source notices.

`Apply` is a one-time authoring operation and refuses to run again after installation. `Refine` updates the existing lighting/materials; `BakeReflections` regenerates the local reflection texture. Do not run old phase generation commands over this scene. Changes to road height require adjusting the road-paint material's SurfaceY (currently 0.033 m).

## Inspector settings

Main Camera: HDR enabled; 4× project MSAA; CinematicCityCamera enabled after CityContactShadows. The previous CityCameraAtmosphere grade is disabled to avoid applying two grades. Exposure -0.15 EV, saturation 0.92, contrast 1.05, haze density 0.0028, maximum haze 0.18. Directional light intensity 1.1, shadow strength 0.82; ambient intensity 0.45. Sky exposure 0.42. Reflection probe is Custom with a baked 256-pixel HDR texture.

Plant texture colour maps are sRGB. Roughness, opacity and occlusion are linear; OpenGL normals use Unity's Normal Map import. Colour/opacity maps are 2K and most other maps 1K. No paid asset or new render pipeline package is required.

## Provenance

All four newly imported sources are CC0 from Poly Haven:

- [Covered car](https://polyhaven.com/a/covered_car)
- [Fern 02](https://polyhaven.com/a/fern_02)
- [Shrub 02](https://polyhaven.com/a/shrub_02)
- [Kloofendal overcast pure sky](https://polyhaven.com/a/kloofendal_overcast_puresky)

[Publisher license](https://polyhaven.com/license). Authors and source URLs are retained in Assets/Art/Realism/PolyHaven/SourceRecords.json. Every downloaded asset file matched provider size and MD5; the complete 33-file package has a SHA-256 roster. The game-dev canonical package CLI was unavailable; this is a local verified staging/import workflow, not a canonical CLI receipt. Imported art is credited third-party work, not original student modeling.

## Evidence and limits

Native Unity stills: Previews/Realism/RealismAfter.png and RealismStreet.png. These are Editor camera renders, not AI concept images or a measured gameplay recording. The character in the first image is sampled in Edit Mode; foot IK is evaluated during gameplay.

See VERIFICATION_REPORT.md and Logs/WindowsBuildSummary.txt for the current build. No new gameplay tests, human play-through or target-device FPS benchmark were run for this graphics pass. New vegetation and image effects add rendering cost. Existing simple facade geometry and current character meshes remain visible limitations; this update does not reproduce The Last of Us's authored AAA assets or animation production.

## Suggested commits

- `feat(art): add credited scanned city props and foliage`
- `feat(rendering): add linear HDR city lighting and filmic camera pass`
- `fix(art): refine road paint reflections and facade materials`
- `docs: record graphics setup asset sources and native previews`
