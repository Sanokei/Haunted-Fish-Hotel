# Authored watching-frame galleries

Original source artwork and its provenance tests are in `Assets/Art/Frames` and `Tools/FrameArt`. All 25 frame families and both filled/unfilled variants are retained without resampling or repainting the original PNGs.

## Authored production content

- `LobbyWatchingFrames.prefab`: **365 frames and 121 eyes** (201 filled, 164 empty). Three dense, irregular clusters wrap the original upper walls. Randomized neighbor growth, unequal sizes and varied rotations replace rows/grid packing; the existing model and upper-wall bounds are respected.
- `HallwayWatchingFrames.prefab`: **eight frames and three eyes**, four filled/four empty, in sparse gaps between existing doors and sconces.
- `Lobby.unity` and `Game.unity` contain linked authored instances with explicit local output-camera references.
- `TutorialRecordingStage.prefab` contains the same linked hallway gallery under its existing Hallway root, bound to its output camera and director. Its authored fish-light action supplies the same beam parameters through `SetFlashlightBeam`.
- Every frame has one authored MeshFilter/quad and one MeshRenderer. Only eye-bearing frames have an opening-center AimPoint. Empty slots receive their fixed presentation gain once on enable and never enter gaze or flashlight sampling. There are no runtime-created scene objects, meshes, materials, masks or UI.

## About one third eyes; other slots remain empty

The latest policy supersedes the earlier every-frame-eye layout. **121/365 Lobby (33.15%), 3/8 hallway (37.5%, the sensible small-count rounding), and 182/553 extension (32.91%)** have eyes. Overall this is 306 eyes and 620 empty frame slots. Lobby wall quotas are left 45/right 45/back 31; extension quotas are left 48/right 47/back 30/front 57. Hallway eyes are at X−13.55, 5.50 and 44.76.

Selection uses balanced largest-remainder wall quotas followed by SHA256 ranking of stable scene/wall/frame identities with seed 52. It is independent of layout randomness and serialization order, never an every-third sequence. Original filled/unfilled borders, transforms, opening masks and eye-fit rectangles are preserved.

Materials explicitly set `_HasEye` to 1 or 0. Empty materials set `_HasEye=0`, `_HasPicture=0`, a null `_PictureTex` and default `_PictureRect=(.5,.5,1,1)`. This suppresses both pupil and outline while retaining the dark-grey backing and full lighting/shadow silhouette. A later image should use a per-frame material/property override rather than enabling a shared empty material for unrelated matching frames.

`WatchingFrameGallery._Eyes` contains only selected eyes. `_StaticFrames` applies empty slots' depth gain once in OnEnable; those slots have no gaze anchor, beam test or scheduled property updates. The guide stage inherits the same three-eye hallway prefab; its X 5.5 beam demonstration remains selected.

## Isolated grey opening and height-first eye fit

Each draw owns the full frame: original border, grey interior and watching eye. `WatchingEye.shader` samples the original filled/unfilled texture, this family's opening and backing textures, and the three aligned original eye textures. It uses no stencil buffer, SpriteMask, shared mask ID, screen grab or global shader mutation. One frame cannot expose another frame's eye through mask state.

`build_frame_opening_masks.py` derives the largest closed opening from each original empty frame's alpha, including enclosed specks. A separate grey backing extends four source pixels underneath partially transparent inner paint, bounded inside the original outer silhouette. This avoids wall-colored seams while retaining the painted inner edge. The source PNGs are never edited.

The original 1654×853 eye canvas is fitted to 97% of the opening height first. Width follows that aspect ratio without a width cap. Wide portions of a tall frame's eye are intentionally cut off by the exact frame opening. The entire pupil/outline/closed-lid result is clipped after composition. The pupil also retains the original eye-interior mask, and the original outline stays above it.

The serialized `_InteriorColor` Vector is neutral **#202020 sRGB**, or **0.014443843596092545 linear RGB** in all three channels. This exactly matches the user's uploaded 159×71 swatch: all 11,289 pixels are RGBA(32,32,32,255). The unmodified reference and its checksum/sampling evidence are in `References/frame-interior-dark-grey-swatch.png` and `interior-colour-reference.json`. It is explicitly linear to avoid automatic Unity Color conversion ambiguity; source artwork is untouched.

The final alpha is `frame.a + backing * (1 - frame.a)`. Original paint, the dark-grey backing, and optional eye/picture content are composited inside each frame's local opening. `_HasEye=0` removes both pupil and outline. Physical lighting and shadows affect the frame, backing, picture and outline; the pupil bypasses illumination only. The entire composite then receives alpha-correct fog, the greater of world-height visibility and the nearby-art visibility floor, and finally the authored presentation gain. This retains the exact source swatch as the backing material input while allowing environmental darkness. Transparent queue, premultiplied blending and ZWrite Off preserve foreground-player compositing and original transparent edges.

## Gaze and flashlight ownership

`WatchingFrameGallery` is one scene-scoped owner, scheduled at 20 Hz. Serialized eye entries are data, not individual components. It samples the explicitly bound local camera, skips unchanged property writes and updates only renderer-local property blocks. It cancels its scheduler and resets pupils/closure on disable. Each authored AimPoint keeps gaze and beam tests centered on its irregular opening.

`WatchingFrameGaze` provides deterministic bounded projection. Camera direction in the eye's plane determines pupil travel. The travel ellipse only limits displacement; actual pupil clipping uses the original hand-painted eye-interior texture.

Only the hallway gallery reacts to flashlight. In `Game.unity`, existing `GameMouseSpotlight` component **24000033** explicitly references gallery controller **8141014012**, which uses output Camera **24000026**. This is the actual local fish cursor flashlight; disabled network-avatar Light components are not treated as active beams. The exact center, radius, softness, aspect and active state rendered by that source are published to the gallery. An eye closes within the beam's half-light edge, only while that flashlight is active. A camera-to-opening physics ray rejects intervening colliders and ignores triggers. Trigger-only or non-collider visual geometry cannot occlude this source-level test.

The common scheduler advances closure even when the camera is stationary. Upper and lower original outline lines converge over 0.16 seconds; the pupil remains unsquashed and is clipped by the shrinking original-eye mask. At the center they become an anti-aliased stroke colored from the original outline. Beam-off reopens over 0.24 seconds, including interrupted transitions. Death/ghost/boss and source-disable states publish inactive beam state, so reopening is expected. The same behavior is bound in the recording stage through `TutorialRecordingDirector.WatchingFrames`.

See `MASK-ISOLATION.md` for the ghost-layer audit and native review checklist.

## Reproduction and checks

Run from the repository root:

- `python Tools/FrameArt/test_original_frames.py`
- `python Tools/WatchingFrameTests/build_frame_opening_masks.py`
- `python Tools/WatchingFrameTests/author_frame_galleries.py`
- `python Tools/WatchingFrameTests/verify_authored_galleries.py`
- `bash Tools/WatchingFrameTests/Run.sh /path/to/Unity/Editor/Data`
- `python Tools/ScenePresentationTests/verify_scene_assets.py --asset Assets/Resources/LobbyIntroductionUI.prefab`

Both authoring scripts are offline tools. They emit serialized assets and deterministic manifests, never gameplay construction paths. `gallery-placement.json` schema 2 provides frame/world transforms, original texture paths, per-frame opening/backing masks, height-first eye UV rectangles and eye-center AimPoints for source-preview reproduction. The scene patcher replaces only this tool's instance blocks; unrelated scene changes are preserved.

`source-validation.json` records 9,776 focused checks. `unity-api-and-gaze-tests.txt` records 11,020 pure gaze/beam/closure checks, controller compilation against actual installed Unity API assemblies, and actual GameMouseSpotlight/gallery compilation against real Unity/UI/Input APIs. Only the unrelated network-player boundary is doubled in the latter compilation. Serialized-field CS0649 warnings are expected outside the Editor. Aggregate scene results are in `aggregate-scene-validation.txt` and may change when other owners finish unrelated assets.

## Intro-only tunnel

The ending also has an authored inactive 553-frame, 182-eye vertical continuation of the real upper room, with matching lower wall planes/materials, inward-only surfaces and a black vanishing throat. See `INTRO-TUNNEL.md` and `intro-tunnel-placement.json` for geometry, per-eye cinematic depth gain, camera placement and preparation-to-close activation/restoration without hiding the ordinary room. Ordinary Lobby and hallway eyes explicitly retain gain 1 and their original two-sided frame presentation.

## Verification limits

Source checks establish hierarchy/reference integrity, original artwork provenance, texture-local masking, deterministic authoring, fit/layout constraints, bounded gaze math and C# API compatibility. They do not establish native shader compilation, import, rendering, physics, multiplayer or performance. Native Unity startup is blocked by licensing/socket initialization in this environment. Blender visual proofs are source approximations, not Unity captures. Their preview lighting/fog is not a claim of identical URP lighting.

## 3D artwork shadows and authored empty slots

The later world-lighting pass separates the pupil contribution from all lit surfaces and adds a silhouette-clipped depth-only shadow pass. Original frame, outline, grey backing and future picture surfaces now receive actual main/additional shadows; pupils remain unlit. Lobby non-pupil art shares the upper-wall height-darkness curve. `_HasEye=0` leaves an empty grey frame; `_PictureTex`, `_HasPicture`, `_PictureRect` provide the future masked picture binding. See `Tools/WorldLightingTests/README.md` for current formulas, authored light budget and verification limits. Earlier formula descriptions above describe the pre-lighting baseline.


## Independently published ordinary galleries (14 October 2026)

Run `python3 Tools/WatchingFrameTests/verify_authored_galleries.py --ordinary-scenes-only` for the Lobby and hallway integration. The default full suite also requires the separately authored tutorial recording fixture, which is not included in this snapshot. Dormant intro-tunnel resources are retained without activating the unfinished intro integration.
