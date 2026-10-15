# Lobby introduction: foggy city, tunnel shot, and offline preview

## What changed in the intro
* **Fogged block city.** Three transparent layers (`Assets/Art/LobbyIntro/City/City{Far,Mid,Near}.png`) sit between the sky and
  the driveway in the existing `exterior` parallax rig at depths 9, 6.5 and 4.6. They are rendered in Blender from plain
  primitive blocks with facade textures and real per-pixel fog (distance fog plus a ground-fog bank that clears with height,
  so only tower tops ghost through). Heavy fog also fades opacity, so the night sky shows through instead of flat cut-outs.
  No runtime objects: they are ordinary authored UI layers, so the existing rig, skip and replay handling apply.
* **Crane and tilt into the frame tunnel.** `LobbyShot("tunnel", 6.0)` now ends the Ink. `LobbyIntroductionWorldSequence`
  raises the camera from the reception pose to the authored tunnel pose (0, 8.5, 3.2, pitch -89, 24 degree lens) on two
  authored curves (`_TunnelDolly`, `_TunnelTilt`; the tilt starts 20% later). `Lobby.unity` now installs the intro tunnel
  prefab (viewer bound to the output camera), the tunnel camera marker and the curves. The tunnel turns on in `Prepare`
  (`BeginEndingVisibility`) and is restored on close, skip or disable, so nothing pops in mid-shot.

## Texture honesty
The building facades are **procedural placeholders**, not photographs: this sandbox could not reach any photo site
(upload.wikimedia.org, ambientcg.com, polyhaven.com, pexels, unsplash were all denied). To use real photos, add
`photo_*.jpg` to `city/facades/` and run `python city/render_city_layers.py`; photos are preferred automatically.

## Preview (offline, approximate)
`compose_exterior.py` replays the gate-arrival crane with the real sprites, the project's own parallax maths and numpy ports of
the sky, driveway, rain and lamp shaders. `render_lobby_preview.py` rebuilds the lobby in Blender from the FBX and the tunnel
manifest (553 frames, eyes, depth fade) and flies the authored camera path. `assemble_preview.py` joins them
(`Preview/lobby-intro-preview.mp4`). Unity lighting, shaders and fish animation are not reproduced; nothing here is native
Unity verification.
