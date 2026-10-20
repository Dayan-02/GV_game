# Zombie Outbreak

Unity **6000.5.3f1** · 3D first-person survival shooter · Phases 1–18 prototype

Open `Assets/Scenes/Opening.unity` and press Play for the new 10-second opening video, or open `Assets/Scenes/MainMenu.unity` to go directly to the menu. Space, Enter, Escape or a click skips the intro. The updated Windows game is `Builds/Windows/ZombieOutbreak.exe`; see [opening video setup](OPENING_VIDEO.md).

Latest AI update: [four enemy roles, finite perception, group attack limits and navigation recovery](AI_BEHAVIOUR_UPDATE.md). Includes Inspector tuning, F3 diagnostics and isolated behaviour checks.

Enemy art update: [four distinct infected outfits and contact-timed attacks](ENEMY_IDENTITY_ATTACK_UPDATE.md). Hunter workwear, a lean Runner, a hooded Ambusher and an armoured Elite have separate silhouettes and attack clips.

Latest update: [first-person camera, arms, hip fire and aiming](FIRST_PERSON_CONVERSION.md). Use mouse look, WASD movement, left-click fire and right-click aim. The current map and Windows build use FPS. Earlier playable maps have been removed.

The only gameplay level is `Assets/Scenes/EnhancedCity/ZombieMap.unity`. The overhead service bridge has been removed from this level and the Windows build; see [bridge removal](BRIDGE_REMOVAL.md). Editor Play and builds use the same scene list. See [version cleanup](VERSION_CLEANUP.md).

Latest player animation correction: [fixed sideways shaking, shared source rig and natural knee motion](WALK_RUN_ANIMATION_FIX.md).

Previous infected movement update: [four infected locomotion styles, corrected sole height and slower shamble approach speeds](INFECTED_MOVEMENT_UPDATE.md). Player crouch/low-ready controls from [the supplied-video update](STEALTH_REFERENCE_UPDATE.md) remain; press **C** to toggle crouch.

Latest visual update: [scanned plants/vehicles, linear HDR lighting and weathered city materials](REALISTIC_GRAPHICS_UPDATE.md). Open **Assets/Scenes/EnhancedCity/ZombieMap.unity** to see it. The existing map layout, collision and navigation are preserved.

The current Windows build includes the opening cinematic, enhanced city and custom Blender enemy models. Copy the complete `Builds/Windows` folder to another Windows computer and run `ZombieOutbreak.exe`; keep its accompanying files together. The outdated Windows ZIP has been removed.

- [Controls, setup, hierarchy and troubleshooting](PLAY_AND_SETUP.md)
- [Per-phase implementation and suggested commits](DEVELOPMENT_STATUS.md)
- [Verification results and test limits](VERIFICATION_REPORT.md)
- [Four-agent algorithms and viva guide](ASSESSMENT_GUIDE.md)
- [Required team model handoff](TEAM_MODEL_HANDOFF.md)
- [Abandoned-city art, settings and asset sources](ABANDONED_CITY_ART.md)
- [Latest reference-city enhancement](REFERENCE_CITY_UPDATE.md)
- [Imported characters, weapons and movement setup](REALISTIC_CHARACTER_ART.md)
- [Selected P2 Survivalist and smooth movement update](P2_PLAYER_UPDATE.md)
- [Free asset credits and licenses](THIRD_PARTY_CHARACTER_ASSETS.md)
- [Team model import folders](Assets/Models/Team/README.md)
- [Source index](SOURCE_INDEX.md)
- [Complete current C# source snapshot](CURRENT_SOURCE.md)

The earlier PHASE1/2/3 guides and source snapshots are historical. The current scripts and guides above include later integration changes.

## Scope and remaining work

Implemented: first-person movement/camera/arms, modular Rifle/SMG/Shotgun/Pistol, hip fire and aiming, animation, event-driven HUD, interactive district, A* Hunter, utility Runner, tactical Ambusher, adaptive Elite, loadout, medkits/grenades, exact five waves, Victory/Game Over/pause, a weathered abandoned-city art pass and debug/evidence tooling.

**Before assessment submission:** complete the team's original zombie and barricade models in Blender/Maya, retain WIP/authorship evidence, and perform human input, balance and target-device performance checks. Imported free models are credited to their original authors and do not satisfy the team modeling authorship requirement.

## Git collaboration

Commit Assets **with .meta files**, Packages, ProjectSettings and docs. Do not commit Library, Temp, Logs or Builds. AI ownership folders are `Assets/Scripts/AI/{Hunter,Runner,Ambusher,Elite}`, with shared mechanics in `Shared`. Preserve actual authorship; no commits have been made on behalf of team members.


