# Boss life-head HUD

The current BossFight scene uses `Assets/Resources/BossFight/SceneObjects/BossUI.prefab` and a scene-owned `BossHudPresentation`. Both the active scene references and the older `BossFightArena.prefab` fixture are authored and validated here.

## Presentation

- Top-left safe-area inset is 32 × 24 canvas units at the existing 1920 × 1080 reference resolution, CanvasScaler match 0.5.
- `Lives:` is authored in HauntedIntertitle TMP, size 44 in an 88-unit label box; heads start at x100. The one fish or three ghost icons are authored Images, 88 × 88 with a 10-unit gap.
- The row shows the opponent’s lives: fish viewers see 3 → 2 → 1 → 0 neutral boss heads from `FishScore`; ghost viewers see 1 → 0 fish heads from `GhostScore`. Depleted heads disappear. The label remains through the terminal reaction until the viewer leaves.
- Spectators have no opponent life row; their existing overview camera/board presentation is unchanged. Missing snapshots, cancellation, leaving the view, and disabled presentation hide the row.
- Practice uses the same score-driven row with no practice label. The old bottom label, hint, and backing remain inactive.
- Authoritative publication, accepted remote snapshots, role changes, and editor practice already call `RefreshViewer`, so no new gameplay polling is added. Existing revision/epoch rejection prevents stale snapshot presentation.
- `BossHudSafeArea` subscribes to the canvas render callback and only changes anchors when screen dimensions/safe area change. It owns no per-frame gameplay loop and creates no UI objects. HUD order remains 20, below MemberMenu 32767; all new graphics have raycast targeting disabled.

## Artwork

`Assets/Art/Boss/Player-fish-life-head.png` was extracted from the inspected `Assets/Art/HotelFish/FISH-right.png` using built-in image generation, with actual alpha. It preserves the original right-facing bellhop design, painted grain, red/yellow hat, blue face, eye and orange cheek, and removes the body.

`Assets/Art/Boss/Boss-neutral-life-head.png` is a separate HUD-only extraction from the inspected `BOSS-v1-neutral-head.png`. It preserves the neutral eyes, spotted gray mantle, chalky white outline and small bellhop hat, with transparent padding.

The user’s corrected direction is applied to BOTH heads: the lower cut is CONVEX and bulges downward, with the center LOWER than the sides. The fish’s lower opaque edge is y914 at x376, y1123 at x689, and y949 at x1003. The boss’s is y1100 at x376, y1165 at x627, and y1103 at x877 (image coordinates). These are read-only alpha measurements.

Both HUD images use independent single-sprite assets. The original player PNG, all segmented boss PNGs, and the original neutral boss sprite metadata remain unchanged. No Python raster editing was used. Previous upward-scooped fish artwork and the temporary neutral sprite rectangle are superseded and no longer referenced.

## Executed verification

Run `RunMono.sh /path/to/Editor/Data` with an existing Unity installation. This compiles the actual production rules, presenter and safe-area component against explicit lightweight Unity doubles, executes them, and runs the read-only serialized/icon/source validator. It does not start Unity.

The checks cover authoritative 3/1 goal thresholds, stale and terminal duplicate goals, repeated matches, repeated role/state transitions, every intermediate head count, final zero lives, practice resets, spectators, missing snapshots, cancellation, disable/re-enable, notched portrait safe areas, menu layering and passive graphics. Serialized checks resolve local scene/prefab IDs and scene-to-prefab stripped references.

`Evidence/managed-source-results.txt` records the managed and source checks. `Evidence/unity-api-compile.txt` records compiling the new production files against Unity 6000.3.6f1 Core/UI and TextMeshPro assemblies; a minimal snapshot declaration was used for this scoped compile. Compiler warnings are serialized fields populated by Unity and retained compatibility references.

The opt-in native presentation validator's old numeric-text expectations have been updated to check the authored head images and hidden legacy text. That existing runner still loads the legacy arena fixture, not the active decomposed BossFight scene, and has not been run for this revision. Native Unity launch is blocked by the already-diagnosed environment EPERM; no attempt was made to bypass it. Native import, full-project compile/Mirage weaving, actual UI rendering/input and two-client delivery remain unverified. Source-rendered previews are not native Unity screenshots.
