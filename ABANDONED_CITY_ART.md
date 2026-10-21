# Abandoned-city horror art pass

## What changed

ZombieMap now has photographic asphalt, brick, cracked concrete, corrugated metal, timber and paving materials; a warehouse roof and structural details; boarded clinic and utilities buildings; a surrounding skyline; wrecked cars; scanned bins, bags, barrels and hydrants; drains, kerbs, litter, cables and wet road patches. Cool ambient light, warmer street lamps, distance fog and restrained camera grading establish the horror direction while retaining visibility.

The existing generated humanoids have muted clothing, connected rounded body shapes, head details and survivor equipment. These remain procedural characters, not production-quality realistic humans or original student modeling work. The weapon art also remains a prototype. See TEAM_MODEL_HANDOFF.md for importing the team's Blender/Maya models.

## Open and adjust

1. Open `Assets/Scenes/ZombieMap.unity` in Unity 6000.5.3f1. Press Play for the following third-person camera; the stationary Edit Mode camera is not the gameplay pose.
2. Environment additions are under `OutbreakDistrict/HorrorCity`, split into road details, architecture, background blocks and street furniture. Existing physics props retain their gameplay roots and colliders.
3. Reusable materials are under `Assets/Art/AbandonedCity/Materials`. The **Outbreak/Weathered Surface** shader supports world projection, texture scale, roughness, normal strength, metallic and wetness. Scanned props use the same shader with UV mapping.
4. `Main Camera/CityCameraAtmosphere` controls exposure, saturation, contrast, vignette and subtle bloom. The explicit shader reference is required in builds. Disable the component to compare the ungraded image.
5. Fog and ambient light are scene Lighting settings. `StreetPool` spotlights control road light pools. The low-resolution reflection probe supplies approximate reflections, not accurate mirror reflections for every puddle.

The project stays on the Built-in Render Pipeline. No URP or Cinemachine migration is required. The Pipeline package is an Editor connection aid, not a gameplay dependency.

## Authoring and regeneration

- `Assets/Editor/Art/AbandonedCityArtPass.cs`: applies scene art, lighting, refinements and existing structural/navigation validation.
- `CityArtAssets.cs`: imports maps and assigns shared materials; preserves imported model axis corrections.
- `CityArtGeometry.cs`: decorative mesh, facade and car helpers.
- `CityCharacterArt.cs`: clothes the existing generated rigs without replacing bones, Avatar, Animator or sockets.
- `Assets/Scripts/Environment/CityCameraAtmosphere.cs`: camera grading and small bloom buffers.
- `Assets/Shaders/WeatheredSurface.shader` and `CityAtmosphere.shader`: surface rendering and camera effect.

Use **Dead city > Art > Apply abandoned city art pass** in Edit Mode. It preserves an existing HorrorCity root rather than duplicating it, but resets authored lighting and shared material defaults. Save manual art experiments separately before running it. The initial scene layout is retained in `Assets/Scenes/ArtBackups/ZombieMap_BeforeCityArt.unity`; this is a scene backup, not a complete snapshot of shared prefabs/materials. Do not include it in Build Settings.

New decorative details have no collision. Existing walls, cover, doors, props, navigation and AI routes remain authoritative. If moving structures or adding substantial obstacles, add appropriate World-layer colliders and rebake/validate navigation. Keep decorative kerbs low and doorway openings clear.

## External asset provenance

Ten resources were downloaded from [Poly Haven](https://polyhaven.com) under [CC0](https://polyhaven.com/license). Exact source pages, authors, download URLs, resolutions and MD5 checksums are recorded in `Assets/Art/AbandonedCity/PolyHaven/SourceRecords.json`.

| Surface sets | Scanned models |
|---|---|
| asphalt_02 | trashbag |
| brick_wall_001 | Barrel_01 |
| cracked_concrete_wall | fire_hydrant |
| rusty_corrugated_iron | metal_trash_can |
| wood_planks_grey | |
| concrete_pavement | |

`Tools/FetchCityAssets.ps1` retrieves those specific resources through the provider API, verifies checksums and preserves valid existing downloads. No paid assets or external executable code were imported. Keep downloaded assets and their `.meta` files in source control. These resources must not be presented as student-authored models or textures.

## Review checklist

- Inspect main street, clinic entrances and warehouse interior in Play Mode.
- Walk, sprint, jump, aim, switch weapons and shoot from cover; compare the original controls.
- Check enemies navigate entrances and retain their animations; verify barrels still respond to physics and barricades to damage.
- Confirm imported bins/barrels stand upright, materials are not pink and wet road patches blend with the asphalt.
- Review dark areas and HUD readability on the target laptop. Measure actual performance there before claiming an FPS target.
- Keep the whole Windows build folder together when distributing the executable.

Suggested commit: `feat(graphics): add weathered abandoned-city environment and horror lighting`
