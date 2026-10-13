# Lobby introduction art and rain

The existing `LobbyIntroductionUI.prefab` now references the five supplied Lobby-Intro PNGs and the existing `HALLWAY-Door-full.png`. The PNG bytes are unchanged; `test_lobby_intro_art.py` records their SHA-256 values. Texture metadata preserves alpha, imports each as a single sprite and disables unneeded physics outlines.

All imagery and weather layers are authored in the prefab. There are no new runtime constructors, material instances, components or scene searches. Existing StoryUI element tags, panel layouts, world-shot ownership, audio references, held-space/click skip and the independent full-viewport white flash remain in place. Ink now explicitly hides the previous exterior and letter-reading reveal at the start, so replay cannot retain the last reveal's visibility. StoryUI remembers each element's optional contact-shadow rest pose alongside its target; completion, skip and disable restore both before replay.

## Weather

`LobbyIntroWeather.shader` uses normal alpha blending and the UI stencil/rectangle clipping contracts. Its authored materials supply:

- A low-contrast blue-black cloud field and a noisy wet path with sparse fading ripple rings.
- Three rain depths with independently authored density, speed, width, length, slant and opacity. The opening uses the far/near materials; the gate shot uses all three.
- Per-droplet taper, soft derivative-aware edges, randomized brightness/length, breakup along the glint and selective visibility near the gate lamps/open sky. This avoids opaque, uniformly white line primitives.
- Low-opacity warm lamp scatter. Letter artwork uses its own source sprites and the standard UI material.

The weather uses the shader's normal elapsed-time input. Disabling the existing Storm/Exterior/content objects stops rendering it; there is no separate coroutine or subscription to leak on skip, replay, scene exit or cancellation.

Visual references supplied during research: [NVIDIA rain rendering](https://developer.download.nvidia.com/SDK/10.5/direct3d/Source/rain/doc/RainSDKWhitePaper.pdf), [Eric Gagon rain study](https://tooger.artstation.com/projects/3Enlr2), and [Vishal Ranga storm](https://vishalranga.artstation.com/projects/eaDLQJ). They informed depth, translucency, wind and light-dependent visibility. No reference artwork was imported.

## Reusable Ink camera animation

`ParallaxTo(rigTag, poseTag, seconds, easing)` is available both to compiled local sequences and ordinary `StoryFunctions` events. For example:

```ink
EXTERNAL ParallaxTo(rigTag, poseTag, seconds, easing)
~ ParallaxTo("exterior", "wide", 0.0, "linear")
~ FlashFrames(2)
~ ParallaxTo("exterior", "gate_arrival", 5.0, "smoother")
```

Author `_ParallaxRigs` on `StoryUI`. Each rig has a unique tag, a projection origin in canvas units, sibling layer targets with positive depths, and named poses with `Camera` X/Y translation and `Push`. Positive camera X pans right; positive Y cranes up. Negative Y cranes down and therefore lifts the image layers. Positive push approaches the layers, magnifying nearby layers more strongly. Coordinates are reference-canvas units at depth 1. Targets must have the same parent and anchor frame (and matching pivots when stretched); a target cannot belong to multiple rigs. No camera, layer, UI, material or GameObject is constructed at playback time.

The pose is absolute relative to the authored rest composition. The layer projection is `origin + ((restPosition - origin) - camera / depth) * depth / (depth - push)`. Current camera position/push interpolate together, so a pose can combine horizontal pan, vertical crane, push-in or pull-back. Repeating a pose does not accumulate scale or offset, and a zero-duration pose makes an immediate cut. Supported easing names are `linear`, `smooth`, `smoother`, `ease_in`, and `ease_out`. `smoother` eases velocity and acceleration at both ends. The legacy `DollyTo` layout operation remains compatible with other stories.

The lobby uses eight existing layers, from sky at depth 12 through hotel at 3.5, foliage at 1.9, gate at 1.5 and near rain at 1.3. Its five-second arrival moves the camera down 140 units while pushing in 0.4 depth units. The wide pose is applied before the two white frames. Sky and rain have authored 1600×900 overscan; regression checks evaluate their coverage and the ground's lower edge at 101 points along the camera move. The existing image assets, rain materials, lamps, flash, world handoff and audio bindings stay intact.

`StoryAnimationMath` contains the deterministic projection and easing rules. `StoryParallaxRig` contains authoring data. `StoryParallaxPlayer` owns per-UI camera/rest state and a cancellation version; `StoryUI` only routes the step through its existing ordered coroutine and closes the player on teardown. Close, skip, world handoff and disable restore all rig layer transforms. Stale iterators cannot reapply a cancelled camera, including after a new replay has begun. Configuration validation rejects missing/duplicate tags and targets, inconsistent coordinate spaces, nonfinite camera data, invalid timing/easing, and push values that reach a layer's camera plane.

## Supplied envelope and handwritten letter

`Assets/Art/LobbyIntro/Letter-source.png` archives the byte-identical 2048×1271 `IMG_0101.PNG` attachment. The user explicitly approved lossless pixel cropping/background masking after two image-generation extraction trials were rejected for altering the original artwork. Neither generated trial is used in the project.

The two production sprites are `Assets/Art/LobbyIntro/letter.png` (501×327) and `letter_read.png` (517×746). Their crop boxes in source coordinates are `(382,189)-(883,516)` and `(1213,146)-(1730,892)`, with a top-left origin and exclusive right/bottom edges. `letter_asset_provenance.json` records exact source/output hashes, drawn-pixel counts, alpha counts and boundaries.

Every RGB byte in each crop is identical to its source. Every nonwhite drawn pixel stays opaque, including all handwritten words, punctuation, the two seals, the small character and the heart. A one-pixel binary closing seals tiny gaps in the drawn outline for a temporary mask only; filling that outline retains opaque white paper and removes only exterior canvas. No RGB resampling, denoising, replacement text, invented content or redrawing occurs. `segment_letter.py` reproduces this offline with Pillow, NumPy and SciPy.

Both sprites bind to the existing `letter` and `letter_read` Image components in `LobbyIntroductionUI.prefab`. They use default UI material, white tint, preserved aspect ratio, alpha, stencil clipping and no raycast. The reading rect is 330×470: its existing 1.4× fullscreen expansion fits the 720-high canvas with a margin. The envelope keeps its existing floor transform, under-door move and contact-shadow binding. Both door beats still use the original `HALLWAY-Door-full.png`. No Ink, camera API, weather, audio, flash, skip, lifecycle or object-creation changes were needed.

## Verification

```
python3 -m unittest discover -s Tools/LobbyIntroArt -p 'test_*.py' -v
python3 Tools/LobbyIntroArt/segment_letter.py  # optional deterministic re-segmentation
python3 -m unittest discover -s Tools/ScenePresentationTests -p 'test_*.py' -v
python3 Tools/ScenePresentationTests/verify_scene_assets.py --asset Assets/Resources/LobbyIntroductionUI.prefab
dotnet run --project Tools/IntroductionTests/IntroductionTests.csproj
```

The introduction suite compiles and executes the vendored Ink source, checks serialized JSON parity, and exercises the production StoryUI tween/rest/skip-related contracts with test doubles. A Mono compile of that same standalone suite can use the compiler shipped with Unity without starting the Editor.

Native Unity import, shader compilation, actual appearance/performance, frame-accurate flashes and interactive skip/replay must still be checked in an available Editor. The current cloud Editor cannot initialize its required Unix socket; no workaround was attempted. The optional Blender source previews show the actual supplied PNGs and authored prefab poses. They explicitly approximate weather shader rendering and are **not Unity captures**.

`author_lobby_intro.py` is an offline, idempotent authoring helper for this migration, not runtime code. It preserves existing prefab identities and the two new letter sprite bindings. Run it from this repository; `--source-dir` optionally reimports the original five PNGs. `segment_letter.py --source /path/to/IMG_0101.PNG` archives and segments the approved source; its hash guard rejects a different image. Inspect changes and rerun checks after any reauthoring.
