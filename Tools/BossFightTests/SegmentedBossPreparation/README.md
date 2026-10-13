# Segmented boss preparation checkpoint

SAFE FOR OTHER OWNERS TO IMPORT THE SHARED PROJECT. No Assets source, scene, prefab, PNG or import metadata was changed by this preparation. No Unity launch/import/Play/batch was initiated. All edits are outside Assets in this directory; no live boss file is mid-edit.

Inspected all 25 source PNGs in Assets/Art/Boss through a pixel contact sheet. V1 has no supplied full reference. V1 = BOSS-v1-neutral-head + v1v2 arm-left/right-1..4. V2 = BOSS-v2-angry-head + same eight grey arms. V3 = BOSS-v3-god-head + god arm-left/right-1..4. Full body PNGs are references and nonrendering silhouette sources, not normal visible contestant art. V1's black silhouette reuses the v2 full outline; V1 visible head remains neutral.

assembly-reference-preview.png compares reconstructed source-pixel composites with supplied v2/v3 full references. Alpha IoU is98.9%/98.6%. This is offline composition evidence, not a Unity scene render. sprite-mapping.json records exact source files, cropped common1714px alignment height, arm-base pivots and world coordinates at512pixels/unit. Native import crops extended numbered-arm tails below the supplied full-reference baseline, without modifying PNG files. Layer order left2,4,3,1; right4,3,1,2; head in front.

Prepared production files (NOT yet published):
- BossGhostLifeStage.cs -> Assets/Scripts/BossFight/BossGhostLifeStage.cs
- BossSegmentedRig.cs -> Assets/Scripts/BossFight/BossSegmentedRig.cs
- BossCharacterPresentation.cs -> existing boss-owned script: life stage, all-segment local visibility, head/arm reactions replacing ghost whole-body Ink spin; fish animation unchanged.
- BossOpponentShadow.cs -> existing boss-owned adapter: selects the one nonrendering full-reference source; shared strike/audio controller untouched.
- BossArenaManager.cs -> existing boss-owned manager: practice life art follows local practice score; physics/input/match rules unchanged.

Prepared manual native helpers (NOT yet published): BossSegmentedAuthoring.cs, BossSegmentedNativeValidation.cs; updates to BossArenaValidation.cs and BossPresentationNativeValidation.cs. No autoruns. Authored target BossSegmentedOpponent.prefab will contain3 variants, each1 head/8 tentacles, plus1 never-camera-rendered shadow source. Only1 variant is active. Native authoring nests it under the existing ghost reaction pivot in BossFightArena.prefab, preserves all physical bodies/colliders and existing scene prefab identity, removes only the old ghost SpriteRenderer/leaf when safe. Game/Lobby/common player/ghost files are excluded.

Compilation: prepared runtime, modern native Sprite Editor authoring helper and native validators all compile against installed Unity6000.3.6f1 assemblies. LifeStageTests.csproj passed1302 match/life progression checks. Current live code remains the previously validated boss version. current-source-hashes.json guards8 current files against overwriting later edits before publication.

After parent grants boss native ownership: recheck manifest/current changes, publish staged code and manual helpers, import sprites and author native prefabs, update dedicated serialized-structure test from BossInputAndSceneTests.cs, then render all3 stages in Fish/Spectator landscape+portrait and retain Ghost-side own-segment invisibility checks. Verify9 visible parts/1 active variant, no duplicate full-source render, full board/goal visibility, board-pixel occlusion baselines and the existing near-fish positive control; test shared storm shadows for every stage; rerun physical3-versus1 scoring/reactions/cancellation/practice and pointer/menu gates. Native acceptance remains pending; do not claim authored prefabs saved yet.