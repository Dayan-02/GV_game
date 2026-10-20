# Opening cinematic

Added 19 October 2026. A ten-second, 1280x720, 24 fps MP4 plays before MainMenu. It contains two camera moves rendered from the existing enhanced city, muted colour grading, letterboxing, fades, and an original synthesized ambient sound bed. Unity overlays the game title and skip prompt.

## Flow

Opening -> MainMenu -> Loadout -> ZombieMap

The saved Unity scene list points specifically to `Assets/Scenes/EnhancedCity/ZombieMap.unity`. The original map has been deleted. `ConfigureSceneFlow` persists this mapping for Editor Play and builds; relying only on a build-time substitution previously caused the Editor menu to load the earlier map.

Space, Enter, Escape or left click skips the opening. Menu returns and restarts keep their existing destinations, so the video does not replay after a death or a return from Loadout. Loading failures fall through to MainMenu after a bounded wait. Audio and fades use unscaled time.

## Files

- `Assets/Scenes/Opening.unity`: first enabled build scene.
- `Assets/Scripts/UI/OpeningVideoController.cs`: preparation, playback, title fade, skip, cleanup and menu transition.
- `Assets/StreamingAssets/Opening/OutbreakIntro.mp4`: local H.264/AAC video; included unchanged in Windows builds.
- `Assets/Editor/Art/OpeningVideoSetup.cs`: scene authoring, offline city capture and Windows build entry points.
- `ArtSource/OpeningVideo/encode_opening.py`: reproducible encoding and original audio generation.
- `ArtSource/OpeningVideo/OriginalAmbient.wav`: original synthesized audio source.

Requires Unity's built-in Video module, added to `Packages/manifest.json`. The game uses its existing legacy input configuration. The VideoPlayer writes into a released-on-exit RenderTexture; no additional online service or downloaded video is required.

## Editing

Change title text in Opening Canvas > Title reveal. Change `videoFile` and `nextScene` on Opening Video only when replacing the video or menu. Keep the video aspect ratio at 16:9 or update the RawImage AspectRatioFitter. The current path is intended for the project's Windows standalone target.

The actual city scene is never saved during capture. To regenerate, run `OpeningVideoSetup.CaptureAndInstall` through Unity, then run `encode_opening.py` with NumPy and imageio-ffmpeg installed. Rendered frames and the local encoder installation are ignored by Git. Keep the MP4 and its `.meta` file in version control.

## Build and review

`OpeningVideoSetup.BuildWindows` configures the current scene flow and builds it. Build output is `Builds/Windows/ZombieOutbreak.exe`; keep the complete Windows folder together. The separate older ZIP has been deleted.

Native compilation, render production and video encoding are checked during authoring. No automated gameplay tests or manual playback/skip playthrough were run for this change.

The Windows build completed successfully with zero errors and one build warning. The MP4 is present in the built player's StreamingAssets/Opening directory. Build receipt: `Logs/OpeningVideoBuild.txt`.

Suggested commit: `feat(ui): add skippable city opening video before main menu`
