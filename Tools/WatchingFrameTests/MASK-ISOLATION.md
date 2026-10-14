# Watching-frame mask isolation audit

## Finding and correction

The previous presentation clipped the red pupil to the original eye outline, but the separate eye plane had no picture-frame-opening mask. Eyes could extend over the frame's interior edge, and frame/eye were separate draws. The revised authored asset has one renderer and one compositing pass per frame. The entire eye is clipped by a texture belonging to that frame family, while a separate derived backing guarantees grey beneath translucent inner paint.

Every frame has its own renderer-local gaze and closure property block. Materials and source textures are never modified during play. No shared stencil reference, SpriteMask, UI mask or global shader properties are used. The shader's final alpha and clipping depend only on that draw's textures and UV coordinates, so unrelated ghost material state cannot open the frame mask. Normal foreground sprites can still visually cover a frame.

## Ghost/death paths reviewed

- Death switches the local player into spectator/ghost control. `GameMouseSpotlight` disables the fish flashlight and publishes inactive beam state. Reopening after death is intentional, but previously exposed the unbounded eye art.
- `DeathEmergence.prefab` uses its own high-sorting camera-space Canvas and ordinary UI Image materials. `AuthoredGhostEmergence` animates its own canvas and anchor; it does not write frame material properties.
- `GhostTrapRange.shader`, `GhostTrapCursor.shader` and `GhostPlacementGrid.shader` use ordinary alpha blending without stencil operations. Their presenter owns separate property blocks.
- QuickOutline's stencil sample is confined to its example scene. The active PC/Mobile renderer assets do not enable stencil override.
- The portable guide render uses an already composited hallway panorama and alpha-composites ghost/soul sprites afterward. It has no live stencil buffer and cannot modify a frame's baked contents. Fresh panoramas must use the revised full-frame compositor, not the earlier separate-eye planes.

No evidence established a shared ghost-to-frame stencil/material mutation in the inspected production sources. The demonstrated defect was missing frame-opening clipping; the single-pass correction also removes between-layer sorting opportunities. Native rendering is still required to evaluate any platform-specific effect.

## Native review when Unity is available

1. Open Lobby and Game; verify all original frame families, both variants, grey interiors and the authored approximately33% eye selection. Check a tall oval and narrow rectangular frame: eye height is retained and its sides are clipped, without inner-edge wall-colored seams.
2. Move the local camera in both axes; check the pupil stays within the original eye mask and all eye/lid pixels remain inside the frame opening.
3. Aim the actual fish flashlight at an eye. Confirm top and bottom lids converge, the moving pupil does not squash, and beam-off smoothly reopens. Repeat behind a solid collider and with triggers, and in ghost/boss/disabled-light views.
4. Trigger player death and ghost emergence in front of open and closed eyes. Confirm the overlay merely covers the background, no other frame changes mask or pupil position, and disabled fish light reopens affected eyes.
5. Repeat with trap cursor/range/grid and foreground player sprites. Inspect partially transparent original frame edges for correct blending.
6. Disable/re-enable the gallery; verify scheduler teardown/restart, centered pupils and cleared closure. Profile the common 20 Hz controller and additional-light shader variants on target platforms.
