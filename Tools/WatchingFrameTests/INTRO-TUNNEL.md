# Intro-only continuous upper-gallery extension

`Assets/Prefabs/WatchingFrames/IntroTunnel/IntroWatchingFrameTunnel.prefab` retains its stable prefab identity and inactive default, but now extends the real Lobby vertically. It is not a small shaft over the reception desk. There are no wall/gallery visibility swaps or mid-shot geometry changes.

## Exact room continuity

The root is identity at world origin. The authoring tool reads the existing left/right/back wall transforms and thickness from `Lobby.unity`, derives their actual inward faces, and extends those same planes from their upper edge at approximately **Y21.89985 to Y80**. The left/right planes retain the room's original tapered plan. Their intersections with the back inner plane at **Z≈8.85** and new front plane at **Z−2.95** define the four-sided perimeter.

Each lower seam reuses the exact `HotelLobbyColor6.mat` asset. Four separately sorted transparent black overlays start at zero opacity through Y25, then smoothly darken upward with `1 - exp(-max(0, worldY - 25) × 0.10)`. They render behind frame quads, not over their contents. The final cap at Y80 is opaque black. All extension surfaces face inward.

The extension has **553 original filled/unfilled frames, 182 eyes and 371 empty picture slots**, using all 25 original frame families. Frames cover all four sides from Y22.08 through Y61.08; the remaining height is the empty black throat. Quads are irregularly clustered and varied in rotation/size. Their exact #202020 base interiors, height-first eye fit, frame-opening masks, original pupil/outline and gaze projection remain shared with the ordinary galleries.

One serialized shared controller owns the 182 selected eyes. The 371 empty slots receive static shading once on enable and never run gaze/flashlight sampling. Each frame's constant `PresentationGain = exp(-max(0, worldY - 25) × 0.10)` fades the composite into the distant darkness. No new per-frame MonoBehaviour, runtime object, camera, mesh, material or UI is constructed. Ordinary gallery presentation gain remains 1.

## Lifecycle and camera

`LobbyIntroductionWorldSequence.Prepare` activates the extension before the first world view. It remains continuously active through every world beat and the ending, restoring its prior state on Close/skip/disable/error. Existing Lobby walls and all ordinary gallery frames remain visible throughout. No occluder bindings or timed shaft thresholds are used.

The linked Lobby instance is 8141014101, stripped root GameObject 8141014102. `_Viewer` binds output-camera transform 330585546. The final authored camera is **(0, 8.5, 3.2), pitch −89°, vertical FOV24**. Its four edge rays meet the four actual extended wall planes above the original rim; its center ray reaches the deep black cap. The source camera owner retains the entry/reception path and timing.

## Source reconstruction and checks

`intro-tunnel-placement.json` schema3 records every frame transform, mask/material reference and gain. Surface records distinguish `matched_wall`, `height_darkness` and `black_cap`, with world plane points/normals, vertical ranges, original renderer/transform references and exact footprint endpoints. An offline renderer must preserve the original room and gallery, reuse the matching material at the seam, and implement the complementary height fade; rendering every new surface as a black plane would be incorrect.

Run `python Tools/WatchingFrameTests/verify_intro_frame_tunnel.py --require-scene` for reference/hierarchy integrity, dormant root, inward culling, bounds, original eye aspect, exact sampled colour, actual plane/material/upper-edge continuity, all four endpoint rays, and no visibility swaps. `author_intro_frame_tunnel.py --geometry-only` can regenerate geometry/manifests without touching material/shader assets while the lighting owner updates them.

Native Unity import, HLSL compilation, rendered lighting/Cinemachine behavior and performance remain unverified because Editor startup is blocked. Blender diagnostics are source approximations. Frame-source tests and real Unity API compilation remain applicable.
