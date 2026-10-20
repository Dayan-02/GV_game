# Shared source rig for player locomotion

The existing UnityMotion FBXs were previously imported with a separate automatically generated Avatar for each posed animation file. This produced extreme right-ankle muscle values. These private player copies use **Copy From Other Avatar**, referencing the original `defaultmale_rigAvatar`.

Source: Unity Technologies, Standard Assets Characters, revision `8f185aa3c782d824976653dd6a6476312daad241`.

- [Original reference rig](https://github.com/Unity-Technologies/Standard-Assets-Characters/blob/8f185aa3c782d824976653dd6a6476312daad241/Assets/_Standard%20Assets/Characters/Animation/Male/Rig/defaultmale_rig.fbx)
- [Original rig import settings](https://github.com/Unity-Technologies/Standard-Assets-Characters/blob/8f185aa3c782d824976653dd6a6476312daad241/Assets/_Standard%20Assets/Characters/Animation/Male/Rig/defaultmale_rig.fbx.meta)
- Original downloaded metadata SHA-256: `0CA3AADD91C57FD52A2C49A45174DD89A33AE3F5812D4247E5CCF33A44EFB8F2` (Unity upgrades its serialization when importing).
- FBX hashes and provenance: `SourceRecords.json`.
- License: the accompanying `LICENSE.md`, Unity Companion License for Unity-dependent projects.

The source rig is used to interpret motion, not as the visible character. P2 Survivalist remains the player model and target Avatar. Existing shared enemy source files remain untouched.

Rebuild the player clips with `Tools > Dead Zone > Correct existing player walk and run`, with the enhanced ZombieMap scene open and Play Mode stopped. `PlayerSourceMotionImport` restores these import settings; `PlayerInPlaceMotion` removes the horizontal travel and fits boot contact without replacing the original knee, ankle or foot-goal tracks. Commit this folder and its `.meta` files together.
