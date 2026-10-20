# Reference city enhancement

Open `Assets/Scenes/EnhancedCity/ZombieMap.unity`. This is the existing level with additive environment work. The original `Assets/Scenes/ZombieMap.unity` is retained unchanged, with a byte-identical backup in `Backups/BeforeReferenceCity/`.

## Inspection and reuse

Unity 6000.5.3f1; Built-in renderer; AI Navigation 2.0.15. Existing gameplay uses the third-person CharacterController, camera orbit/aim system, weapon controller, health, waves, interactions and four separate zombie brains. Player spawn stays at the existing main-street marker. Existing warehouse, clinic/office and maintenance interiors, roads, colliders, doors, physics objects and tactical markers remain.

Reused photographic Poly Haven brick, cracked concrete, asphalt, paving, wood and rust maps, plus scanned bin/bag/barrel/hydrant props. These existing assets and their license/source records are retained. No new downloaded art or additional Unity package is required.

## Implemented environment work

- Warehouse upper tenement, clinic façade details and foreground apartment upper levels, retaining interior access.
- Window frames/sills, boarded windows, masonry courses/pilasters, fire escapes, shutters, awnings, roof vents and antennae.
- Skyline masonry detailing, dead trees, damaged railings, dumpsters, rubble, dust/seepage patches and asphalt fractures. The raised service bridge was removed from the enhanced scene on 15 October 2026 at the user's request.
- Three additional abandoned cars reuse existing mesh art with door seams, grille, mirrors and plates. Cars/dumpsters have simple collision; central passage remains available.
- Faded road paint and separate weathered material copies under `Assets/Art/ReferenceCity/Materials`. Original shared material assets remain unchanged.
- Overcast procedural sky, gray distance fog, soft lower-contrast sunlight, neutral ambient lighting and restrained existing camera grading. Render pipeline and project-wide quality settings remain unchanged.
- Replaced the enhanced scene's realtime startup reflection capture with a reusable static overcast cubemap. This avoids a Unity null-graphics-device probe-render crash encountered during batch validation and reduces startup work. The cubemap approximates sky/ground lighting rather than detailed mirrored buildings.
- Static decorative geometry combined by material/chunk; source objects retained with their redundant renderers disabled. Small distant masonry details have screen-size culling LODs. Existing compressed texture maps and instanced materials are reused.
- Rebuilt NavMesh stored separately from the original scene’s navigation data. All existing spawn/tactical markers must retain complete paths to the player before saving.
- Build scene list points at the enhanced copy. Its scene name is still `ZombieMap`, preserving existing menu and restart flows.

## Reusable tools and assets

Menu: **Tools > Dead Zone > Enhance Existing City**. Run from the original scene to create a safe copy, or open the enhanced copy. An existing enhancement root is retained without generating duplicates. Adjust authored objects directly after the first pass.

Authoring code: `Assets/Editor/Art/ExistingCityEnhancer.cs`. Shader sources: `Assets/Shaders/OvercastCitySky.shader` and `CityWearDecal.shader`. Prefabs: `Assets/Art/ReferenceCity/Prefabs/` includes abandoned vehicle, dumpster, dead tree and warehouse upper tenement. Materials, meshes, wear mask and navigation data are in the same art folder.

Scene additions: `OutbreakDistrict/ReferenceCityEnhancement`, containing `Facade additions` and `Street decay`. Existing art stays under `OutbreakDistrict/HorrorCity`. New decorative fine detail has no physics; substantial additions have simple World-layer colliders.

## Comparison and limits

`Previews/ReferenceCity/Before.png` and `After.png` use the same native Unity comparison camera and an evaluated player pose. They are scene renders; the camera is temporarily staged and the overlay HUD is omitted.

The result follows the reference’s aged urban façades, fire escapes, muted masonry, cloudy light and depth haze. Remaining differences include the original wider street layout, existing arena cover and crosswalks, simpler vehicle shapes, less dense rubble, repeated window modules and less detailed distant architecture. This is not a claim of photographic equality to the reference or AAA asset quality.

## Validation and manual review

Build/scene and bounded Play Mode results are recorded in `VERIFICATION_REPORT.md` and `Logs/ReferenceCitySmoke.txt`. The bounded test checks grounding, physical movement, camera initialization, aimed shooting/ammo/damage, reload start/cancel, weapon switching and agent paths for all four enemy types. It uses gameplay APIs and physics, not simulated keyboard/mouse input or a full human play-through.

The final isolated run passed all 21 assertions. The original scene's SHA-256 remains `BC1D62E75E604F47164C248A4C1F09E50D8952B122BD3C029824FFEDADB31431`. The enhanced district has 683 enabled mesh renderers and 282,504 triangles before view culling; these are scene counts, not draw-call or FPS measurements. Raw editor viewport counters also include editor rendering and do not establish standalone performance.

For manual review, start through MainMenu/Loadout, move/aim/shoot around the new cars, enter all three existing interiors, jump over curbs, interact with doors and destructible barricades, and observe each enemy type navigating the street. Check readability and frame rate on the university demonstration computer. Editor rendering counters are not a target-machine performance guarantee.

Suggested commits:

- `feat: enhance existing city scene with weathered facades and street dressing`
- `feat: add overcast city sky and muted atmospheric lighting`
- `perf: batch static city details and preserve navigable routes`
