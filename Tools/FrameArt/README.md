# Original painted frames and watching-eye art

The user supplied these four original PNGs on 2026-10-14. They are retained
byte-for-byte in `Assets/Art/Frames/Originals`.

- `Frames-Empty-00.png`, 2048 × 857
- `Frames-Filled-00.png`, 2048 × 857
- `HALLWAY-Frame-Eye-outline.png`, 2048 × 1714
- `HALLWAY-Frame-Eye.png`, 2048 × 1714 (the red pupil)

The separate new sconce source is imported by the sconce authoring workflow;
this tool does not own its art, lights, prefabs, or scenes.

## Imported assets

`Assets/Art/Frames/Segmented Frames` contains 25 matched pairs:
`Frame-Filled-01.png` through `25`, and `Frame-Empty-01.png` through `25`.
The numeric order is by the top edge of each complete component on the sheet.
All 25 components were visually checked as complete frames, including both
small square frames and the slender vertical frame.

Every retained RGBA pixel is copied exactly from the corresponding original,
without resizing, colour adjustment, alpha thresholding, or replacement art.
The rectangle around each complete frame includes four pixels of padding.
Some frame rectangles overlap on the original sheet (notably the small gold
diamond and upright oval). Pixels owned by a neighbouring complete frame are
excluded using nearest-main-component ownership. The entire main component of
each intended frame is preserved. Tiny disconnected sheet-edge marks outside
the crop rectangles remain preserved in the full originals.

`frame-segmentation.json` records original Library IDs and hashes, per-frame
source bounding boxes, crop sizes, source pixel checks, crop hashes, Unity
GUIDs, and exact opening bounds/centroids derived from the empty frame alpha.
Coordinates are top-left image coordinates; right/bottom edges are exclusive.
The opening bounds are a bounding box of the true irregular opening, not a
claim that every pixel of that rectangle is inside the opening. Fit the eye
conservatively for diamonds/ovals and visually review the result.

Unity metadata uses full-rectangle single sprites, 100 pixels per unit,
centered pivots, bilinear filtering, no mipmaps, clamp addressing, and no
texture compression. Maximum import size is 4096. Existing GUIDs are preserved
on reruns; new asset GUIDs are deterministic.

## Eye alignment and mask

All three runtime assets in `Assets/Art/Frames/Eye` share the same 1654 × 853
canvas, cropped from source rectangle `[206, 431, 1860, 1284]`. Their UVs and
pivot align without independent rescaling or centering:

- `HALLWAY-Frame-Eye-outline.png`: original painted stroke, exact source pixels
- `HALLWAY-Frame-Eye.png`: original red pupil, exact source pixels
- `HALLWAY-Frame-Eye-interior-mask.png`: derived white alpha mask of the largest
  closed transparent region strictly inside the original outline

The mask has 635,448 opaque interior pixels and excludes the original stroke.
It is a geometric mask derived from the supplied outline, not new eye art.
Draw the translated original pupil clipped by this fixed mask, then draw the
unchanged outline over it. Do not move or independently recenter the mask.

## Reproduce and verify

Requires existing Python packages Pillow, NumPy, and SciPy. No network or image
model is used. From repository root:

    python3 Tools/FrameArt/segment_original_frames.py --source Assets/Art/Frames/Originals --qa /tmp/frame-art-qa
    python3 Tools/FrameArt/test_original_frames.py

The QA contact sheet contains every matched pair and the eye looking center,
left, and right. It is a review image only; preview downscaling never changes
the game assets. This workflow writes only its `Assets/Art/Frames` subtree and
its manifest/QA artifact. It does not modify scenes, prefabs, or game scripts.

The tests verify all source hashes, 50 exact retained-pixel copies, complete
main-frame preservation, absence of foreign main components, matched crop
geometry, imported settings and GUID uniqueness, and eye mask alignment and
clipping at three gaze positions. These are source-level/import-metadata
checks. Native Unity import and in-scene appearance still require Unity QA.
