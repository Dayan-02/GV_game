# Reclaimed city presentation

This pass uses The Last of Us Part I as an art-direction reference: worn survivor materials, layered urban decay, restrained overgrowth and warmer daylight against cool distance haze. The linked YouTube video could not be played in this environment. The game was identified from indexed references, confirmed by the user, and the [official PlayStation product page](https://www.playstation.com/en-us/games/the-last-of-us-part-i/) was consulted. This is an inspired presentation pass using the project's licensed assets, not a reproduction of that game's models, animation library or production quality.

## Open and play

Gameplay scene: `Assets/Scenes/EnhancedCity/ZombieMap.unity`.
Windows executable: `Builds/Windows/DeadCity.exe` (keep the whole Windows folder together).

## Environment

- Photographic brick, concrete, asphalt and metal maps gain spatially varied moss, rain staining and dampness through `Assets/Shaders/ReclaimedSurface.shader`.
- Deterministic mesh foliage adds individual folded leaves, weeds at the curb, ivy on existing facades and sparse regrowth on existing trees. Foliage has small wind displacement, no collision, and chunk-level distance culling.
- Existing architectural geometry, streets, rooms, entrances, cover, spawns, tactical markers and navigation remain in place.
- Warmer directional light, cooler ambient fill and reduced distance haze increase separation. Half-resolution contact occlusion adds depth around intersections of surfaces. It is a screen-space approximation and does not replace baked global illumination.
- Camera grading stays restrained; the exploration camera sits closer to the player's shoulder. The existing camera collision solve and aim targets remain active.

## Characters and animation

- New material copies use the player's existing color, normal, occlusion and roughness maps. Roughness sampling compensates for source texture color-space settings without changing vendor import settings.
- Cloth gets subdued color and embedded dust. Skin uses less dirt and weaker normal intensity. Four infected material profiles retain the existing Hunter, Runner, Ambusher and Elite roles, with distinct mottling and tints.
- `Assets/Scripts/Player/CharacterMotionPolish.cs` adds small torso responses to acceleration and turns, plus breathing. It runs after the Animator and before weapon/body and support-hand corrections. It does not transform the pelvis, legs or character root.
- Jump, attack, reload and death suspend the added torso effects. Aiming reduces them to protect gun alignment. Pause and disable paths avoid accumulating rotation.
- Grounded player clips, calibrated enemy cadence, humanoid Avatars, weapon sockets and combat timing are retained. No new motion-capture library is included; these changes add weight to existing locomotion rather than introducing vaulting, motion matching or authored turn-in-place clips.

## Editable files

- `Assets/Editor/Art/ReclaimedCityPresentation.cs`: one-time authoring pass, native Unity previews and layout checks.
- `Assets/Art/ReclaimedCity/Materials`: cloned environment and character materials; source materials remain available.
- `Assets/Art/ReclaimedCity/Meshes`: generated foliage meshes.
- `Assets/Shaders/SurvivorSurface.shader`: UV-anchored dust/infection and roughness-aware character lighting.
- `Assets/Shaders/LivingFoliage.shader`: vertex-colored folded leaves with restrained wind.
- `Assets/Scripts/Environment/CityContactShadows.cs` and matching shader: optional contact occlusion. Disable the component on Main Camera if the demonstration computer needs a cheaper render.

The authoring menu rejects a second installation rather than duplicating vegetation. Adjust the installed objects and materials directly. Re-running earlier full city/player setup tools may replace current presentation settings.

## Preservation and evidence

Backup before this pass: `Backups/BeforeReclaimedCity/SceneCharacters.zip`.
Native before/after and character renders: `Previews/ReclaimedCity/`.
Native Play Mode movement replay: `Previews/ReclaimedCity/Movement.gif` (synthetic inputs; 56 captured frames).
Layout comparison: `Logs/ReclaimedCityPresentation.txt`.

The authoring check compares every existing environment transform, serialized collider definition and the NavMesh data reference before saving. New foliage has no colliders. Native previews are staged scene renders; they are not a human play-through. Runtime checks and build results are recorded separately in `VERIFICATION_REPORT.md`.

## Manual review

1. Start through MainMenu and Loadout. Walk, run, sprint, stop and reverse direction.
2. Aim and strafe, fire each weapon, reload, switch weapons and check support-hand placement.
3. Enter each existing room and use doors/barricades. Check the same routes are available.
4. Observe all four enemy types chasing, attacking and dying; examine clothing and skin under sun and shade.
5. Inspect curb transitions and sharp turns for remaining foot contact error. The previous gait fix still has small blending errors; this art pass does not claim full foot locking.
6. Check vegetation culling, dark edges and frame rate on the university demonstration computer. The new shading and foliage add rendering work; no target-machine performance guarantee is implied.

## Provenance

Existing Survivalist, Pixelhouse infected, Unity motion and Poly Haven assets retain the licenses documented in `THIRD_PARTY_CHARACTER_ASSETS.md` and the existing environment records. Foliage geometry and the new shaders are authored for this project. No new paid assets or third-party game files were acquired.

Suggested commits:

- `feat: add reclaimed city materials foliage and natural daylight`
- `feat: improve survivor and infected surface shading`
- `feat: add restrained character breathing and torso inertia`
- `test: verify presentation preserves existing map collision and navigation`
