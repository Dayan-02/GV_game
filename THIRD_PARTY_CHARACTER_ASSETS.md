# Character, animation and weapon asset credits

Imported on 12 October 2026. These are free third-party assets, not original student modeling work. Keep these credits and the supplied licenses with redistributed project files and builds.

## Active human survivor — P2 (14 October 2026)

- **Survivalist**, by **Slayver**, selected by the user as P2.
- Source: https://www.cgtrader.com/free-3d-models/character/man/survivalist-f5c207a4-36d6-49d1-9ec8-c19d2ab562a4
- Publisher download page: https://www.cgtrader.com/items/2645135/download-page
- Listing license: **Royalty Free License (no AI)**. This is a licensed third-party asset, not MIT/CC0 or original student work. Retain the publisher attribution and consult the listing's license before redistributing source assets.
- Downloaded files: `Military_Survivalist.unitypackage`, `Basic Demo Animations.zip`, and `SK_Military_Survivalist.fbx`. Archive hashes and imported file hashes are recorded in `Assets/Art/ImportedCharacters/Survivalist/SourceRecords.json`.
- Imported publisher meshes, textures, materials and four source clothing prefabs under `Assets/Art/ImportedCharacters/Survivalist/Vendor`. Demo scripts, demo scenes and renderer settings were excluded.
- Integration: corrected humanoid pelvis mapping; supplied idle/walk/run/jump/death clips; existing licensed Unity sprint/fall/strafe clips; recalibrated cadence; fitted carry/fire/reload poses; finger binding correction; hand attachment and ground-aware foot placement. The live player uses one assembled clothing variant.
- Original archives and pre-change backups remain local under `Previews/` and `Backups/`, excluded from Git.

## Previous human survivor (retained source)

- **Military_Male_01**, Microsoft Rocketbox, copyright Microsoft and the contributors listed in its license.
- Source: https://github.com/microsoft/Microsoft-Rocketbox
- License: **MIT**; full notice retained at `Assets/Art/ImportedCharacters/Rocketbox/LICENSE.md`.
- Changes: Unity Humanoid import, material connections, carry/weapon attachment, retargeted animation and support-hand alignment. Original FBX and texture bytes are retained.

## Zombie

- **Zombie**, by **Pixelhouse**.
- Source: https://opengameart.org/content/zombie
- Original archive: https://opengameart.org/sites/default/files/zombie.zip
- License: **Creative Commons Attribution 3.0 Unported** — https://creativecommons.org/licenses/by/3.0/ ; legal text: https://creativecommons.org/licenses/by/3.0/legalcode
- The author's supplied `read first.txt` is retained in `Assets/Art/ImportedCharacters/PixelhouseZombie/`.
- Changes: scale and Humanoid skeleton conversion, facing correction, tinted materials, four visual variants, retargeted run/idle blends and gameplay animation timing. One shared source mesh is used by the four distinct AI types. Attribution does not imply endorsement by Pixelhouse.

## Human locomotion

- **Standard Assets Characters**, Unity Technologies and its licensors.
- Source: https://github.com/Unity-Technologies/Standard-Assets-Characters
- License: **Unity Companion License**; full supplied notice retained at `Assets/Art/ImportedCharacters/UnityMotion/LICENSE.md`.
- Use these assets in Unity-based projects under that license; they are not CC0/public-domain assets.
- Imported animation files: idle, forward walk/run/sprint, strafe idle/right/forward, falling and standing jump. Changes: retargeting, root-motion baking, mirrored left strafe, reversed backward walk and upper-body carry/action overlays.

## Four guns

- **Free CC0 Guns & Explosives Pack**, by **3dmodelscc0 / nisu**.
- Original publisher and license declaration: https://3dmodelscc0.itch.io/free-cc0-guns-explosives-pack
- License: **CC0 1.0** — https://creativecommons.org/publicdomain/zero/1.0/
- Original FBX archive downloaded through this public mirror after the original download stalled: https://github.com/BoQsc/cc0-guns-explosives-pack-glb/releases/download/0/Guns.Explosives.rar
- Mirror project: https://github.com/BoQsc/cc0-guns-explosives-pack-glb
- Included: M4A1, Grease Gun, Shotgun and Makarov. Original meshes and PBR textures retained under `Assets/Art/ImportedWeapons/CC0/`.
- Changes: material conversion to Unity Standard, DirectX normal-map green-channel conversion where applicable, combined metallic/smoothness maps, grip/muzzle/support-hand placement, pistol magazine seating and cosmetic moving parts.

## Integrity and assessment

The latest city graphics pass adds Poly Haven CC0 covered_car, fern_02, shrub_02 and kloofendal_overcast_puresky. Original author/source records and the license notice are retained under `Assets/Art/Realism/PolyHaven`; package hashes and local import receipt are under `Assets/Art/Realism`. See [REALISTIC_GRAPHICS_UPDATE.md](REALISTIC_GRAPHICS_UPDATE.md) for usage and provenance. These assets are free third-party work.

`Assets/Art/ImportedCharacters/SourceRecords.json` records pinned repository URLs and SHA-256 hashes for the human and locomotion sources. `Assets/Art/FreeAssetSources.json` records the zombie and weapon source hashes and archive provenance.

Previous environment asset credits remain in `ABANDONED_CITY_ART.md`. The separate university requirement for team-created zombie/barricade models and modeling evidence remains in `TEAM_MODEL_HANDOFF.md`; importing these free models does not satisfy that authorship requirement.
