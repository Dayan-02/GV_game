# Team model drop location

Copy original team-authored Blender/Maya source, FBX exports, textures and optional debris into this folder with each member's real name and WIP evidence. Suggested folders:

```text
Team/Zombie/Source/
Team/Zombie/Export/
Team/Zombie/Textures/
Team/Barricade/Source/
Team/Barricade/Export/
Team/Barricade/Textures/
Team/Barricade/Debris/
```

This folder currently contains no model files. It deliberately does not claim that the existing greybox or future generated scaffolding is team-authored.

When the FBX prefabs arrive, assign them to `Assets/Data/TeamArtCatalog.asset`. Use **Zombie Outbreak > Team Art > Validate supplied models**, inspect the author/source record, then **Apply validated models to game**. Details and constraints are in the root `TEAM_MODEL_HANDOFF.md`.
