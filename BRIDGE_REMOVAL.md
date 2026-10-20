# Overhead bridge removal — 15 October 2026

Removed the service bridge above the street in the supplied screenshot from `Assets/Scenes/EnhancedCity/ZombieMap.unity`. Its group contained the span, railings, handrails and two combined visible meshes. The scene was saved with the surrounding facade additions in place.

The bridge had no colliders, and the existing `NavMeshSurface` uses `PhysicsColliders` as its geometry source. Its navigation data asset remains assigned. The two combined mesh assets were deleted after confirming that the enhanced scene was their only reference.

Removed the bridge creation block from `Assets/Editor/Art/ExistingCityEnhancer.cs` so the art pass cannot recreate it. The original `Assets/Scenes/ZombieMap.unity` is unchanged.

Backup: `Backups/BeforeBridgeRemoval-2026-10-15/BridgeAndScene.zip`.

Verification: `Logs/BridgeRemoval.txt`; the saved enhanced scene contains no `Distant service bridge` hierarchy or bridge renderers. The Windows build result is recorded in `Logs/BridgeRemovalBuildSummary.txt`.
