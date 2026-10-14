# Stable visual publication, 14 October 2026

This snapshot combines the reviewed boss life HUD, camera/depth/void presentation, physical-art lighting, ordinary Lobby and hallway frame galleries, hallway sconces and gold-trim lobby runner. Upper-gallery shaft resources remain dormant; the original intro UI, Ink and runtime remain in place.

The capsule V8 PNGs are an explicitly requested work-in-progress checkpoint. Their proportions, perspective and cart direction remain under revision; they are not final approved store artwork. The paired exports and original-art provenance are retained for continued editing.

Shared gameplay and tutorial prefabs were reconstructed from main 0d2a497 with only lighting-owned SpriteRenderer material and shadow fields. Lobby was reconstructed from that same baseline with the approved lighting, ordinary gallery, runner and dark backstop. Unfinished intro/city/door and guide motion/filter/recoil changes are excluded.

Run the ordinary gallery suite with `--ordinary-scenes-only`. Its default full mode still checks the separate guide-recording fixture; that fixture is intentionally absent here and its eight checks were not run. The other ordinary-gallery contracts remain active.

The adjacent logs record tests executed against this selected snapshot. The source hash manifest identifies the selected asset/code bytes. Earlier owner freeze manifests describe the integrated authoring workspace and are provenance, not a claim that unfinished integrations shipped.

Native Unity import, shader/GPU rendering, Play mode and connected multiplayer are unverified. The available editor cannot launch because its IPC initialization is blocked. Managed tests and real Unity API compilation do not substitute for native validation. Existing external package GUIDs remain outside source-only verification.
