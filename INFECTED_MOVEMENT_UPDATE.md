# Infected movement update

14 October 2026

## Why the zombies looked human

The previous grounding repair assigned the survivor's idle, walk and run clips to all four enemies. It reduced sliding, but also gave the zombies ordinary upright posture and reciprocal jogging arm swings. Elite and Ambusher also entered the shared running state during normal travel.

## Changes

- Re-encoded the already licensed Pixelhouse zombie walk onto the corrected forward-facing zombie Avatar. The raw vendor animation is not assigned directly to the repaired rig.
- Authored separate idle, shamble and chase clips for Hunter, Runner, Ambusher and Elite. These use hunched torsos, uneven shoulders, head tilt, relaxed bent wrists and asymmetric reaching/dragging arms. Chase retains the existing running leg foundation with new infected upper-body motion.
- Recorded the full Humanoid pose and its body coordinates together, then refined the lowest sole height on the actual mesh. This avoids changing the torso while leaving an incompatible body reference frame underneath it.
- Recalibrated animation playback against each clip's measured backward foot travel and existing visual scale. NavMeshAgent remains responsible for world translation; root motion remains disabled.
- Added independent starting phases so newly spawned enemies do not move in lockstep.
- Disabled the shared survivor-style torso-polish component on enemy rigs; the infected motion is now authored in their clips.
- Existing attack, heavy attack, hit and death overrides and damage clocks are retained.

## Four movement styles

| Enemy | Travel | Rush |
| --- | --- | --- |
| Hunter | Hunched, asymmetric shamble at 1.25 m/s | Its normal pursuit remains the shamble |
| Runner | Uneven guarded movement at 1.5 m/s | Forward-reaching infected chase; existing 4.8 m/s speed |
| Ambusher | More bent stalking posture at 1.5 m/s | Existing 4.4 m/s strike speed |
| Elite | Heavy shoulder-led movement at 1.45 m/s | Existing 4 m/s push speed |

**Balance impact:** normal approach speeds are slower than before (Hunter 2.25, Runner caution 2.5, Ambusher travel 3.1, Elite advance 2.8 m/s). This is intentional: the short zombie stride otherwise required excessively fast playback and exceeded the cadence limit. Fast rush speeds, AI decisions, health, damage, wave composition and the city layout are unchanged. Human play-through is still needed to judge the easier approach timing.

## Files and editing

- `Assets/Animations/InfectedLocomotion/` — 12 variant clips, the retargeted shamble source, four controllers and four override controllers.
- `Assets/Editor/Art/InfectedLocomotionSetup.cs` — repeatable native Unity authoring and stride/contact calibration.
- `Assets/Editor/Art/GroundedZombieActions.cs` — shared re-encoding helper now accepts an optional output folder.
- `Assets/Scripts/AI/Shared/EnemyAnimationController.cs` — speed matching and initial phase variation.
- `Assets/Editor/Art/InfectedMotionPreview.cs` — isolated Editor animation sheets.
- `Assets/Prefabs/Enemies/` — current controllers, cadence references and travel speeds assigned to EnemyBase and all four variants.

Keep state names `Locomotion` and `Chase`; the runtime animation driver uses these to select the appropriate stride reference. Locomotion-to-Chase thresholds are 2.7 m/s for Hunter/Runner, 3.35 for Ambusher and 3.2 for Elite, with 0.25 m/s hysteresis. Blend duration is 0.16 seconds. Do not run the historical LegAndGaitRepair tool over this version: it assigns the old shared human locomotion again.

No additional asset download or package is needed. Existing Pixelhouse and Unity motion licenses continue to apply; see THIRD_PARTY_CHARACTER_ASSETS.md. Previous enemy assets and prefabs are backed up at Backups/BeforeInfectedMotion/EnemyAnimation.zip.

## Preview and limits

`Previews/InfectedMotion/` contains native Unity animation sheets and motion previews. Left to right: Hunter, Runner, Ambusher, Elite. Each sheet samples a complete normalized clip cycle while the actor stays in place. It demonstrates the authored posture, not runtime world travel, transitions or measured performance.

Authoring measurements are in Logs/InfectedMotionAuthoring.txt and Logs/InfectedPreviewBounds.txt. See VERIFICATION_REPORT.md for the build and review record. No automated gameplay tests or human controls/full-wave run were performed for this change. Sharp turns, steps, collisions and balance still need review in actual play.

## Suggested commits

- `fix(animation): replace human zombie walks with infected locomotion`
- `feat(animation): distinguish four infected movement styles`
- `fix(ai): match shamble travel speeds and desynchronize animation starts`
- `docs: record infected motion setup and native previews`
