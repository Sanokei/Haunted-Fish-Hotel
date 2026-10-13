# Trap artwork authoring evidence — 2026-10-13

`source-layout-preview-not-unity.png` was rendered from `../../PreviewArtwork.svg` using Inkscape. It is a code-authored reference-layout document which displays the original PNG assets unchanged, with a diagrammatic 3-degree chandelier swing. It is **not a Unity capture** and establishes no runtime rendering, animation, physics, RPC delivery or native import result. The companion HTML is another source-layout view.

`source-checks.txt` records the production deterministic rule suite and source/serialization/pixel-analysis guard. `source-hashes.txt` identifies the authored assets and presentation adapter at that check. Later edits require rechecking the relevant evidence.

The original uploaded PNG byte hashes are separately preserved in `../../TrapArtworkSources.json`. All six source files were copied unchanged. Chandelier pieces are Unity sprite rectangles, not new edited rasters. The cord's black pixels were inspected, and are visible against the neutral preview background.

Native Editor launch remains blocked by this executor's Unix-socket/IPC restriction. A Chromium attempt to render the HTML also stopped at `socket() failed: Operation not permitted`. No bypass was attempted; the local vector-document renderer produced the clearly labelled preview instead.

Required native checks remain documented in the parent README: import/compile/weave, real camera and UI-to-world projection, light and exclusive alternate-image transitions, fixed canopy through sway/drop/reset, actual collision/jump behavior with the new shapes, and two-client effect/snapshot delivery. Old native shopping-cart evidence is intentionally stale after the new silhouette.
