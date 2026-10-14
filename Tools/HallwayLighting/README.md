# Authored three-bulb hallway sconces

The user's uploaded `HALLWAY-Sconce.png` is copied **byte-for-byte** into the existing `Assets/Art/Hallway/HALLWAY-sconce.png` asset. Its import GUID is preserved. There are no added pixels, painted light pools, replacement artwork, glow planes, cookies or texture emissions. The original three blue bulbs determine the three point-light centers.

## Authored sources

- `Assets/Resources/HallwaySconce.prefab`: one original-art SpriteRenderer, three serialized, enabled Unity `Light` components.
- `Assets/Resources/GameHallway.prefab`: twenty linked fixtures at the existing hallway positions. Its saved parent/child hierarchy provides sixty real lights without runtime object construction.
- `Assets/Scenes/Game.unity` and `Assets/Resources/Tutorials/TutorialRecordingStage.prefab` already link that same hallway; the recording scene links the stage. Both therefore inherit the same fixture and light sources.
- `Assets/Art/Hallway/HallwaySpriteLit.shader` and `.mat`: the original hallway sprite material was unlit. The new alpha-preserving URP 3D forward/clustered shader reads actual Unity punctual lights, so the authored bulbs can illuminate the wall, table, fishbowl and sconce artwork rather than exist as invisible components.
- `lighting-manifest.json`: exact source identity, bulb centers, energy/color/range, twenty fixture transforms, material response and inherited consumer paths. Source renders must derive from this, remain labeled approximations, and never be called native Unity lighting evidence.

The original width is now 1173 pixels (previous image 1172). Its full 1714-pixel canvas and 100-pixels-per-unit centered import remain intact. The fixture artwork child uses scale 0.162; normalized fixture roots preserve the original saved world artwork scale and position. The old large transparent canvas is intentional, and bulb registration is calculated against the original canvas, not the opaque bounding box.

Each point light uses RGB `(0.38, 0.68, 1.0)`, intensity `0.22`, range `2.15`, no color-temperature tint and a 0.22-unit offset toward the viewer. Light sources are kept in front of the wall at z=2. They are shadowless to avoid sixty realtime point-shadow cubemaps. Short ranges, the existing PC Forward+ light clusters, and original-art base illumination keep this inexpensive and retain the blue/purple hallway. Final energy, bloom and readability need actual camera review. The later world-lighting pass adds alpha-shaped artwork casters/receivers and one inherited directional shadow key; the sixty sconce point lights remain shadowless. See `Tools/WorldLightingTests/README.md`.

The material preserves original sprite alpha/tint with base illumination 0.52 plus a bounded two-sided Lambert contribution from actual Unity light data. The shader includes Forward+ clustered and ordinary Forward paths. Mobile retains its existing four-light-per-object setting; complete coverage on its large wall sprites needs device verification or a separately approved asset partitioning/quality adjustment. No global renderer or quality setting was changed.

## Scope inventory

The direct previous-sconce sprite consumers were exactly `GameHallway.prefab` (20 world renderers) and `AtticRoundCutscene.prefab` (74 screen-space-overlay Images). Game and recording stage share the world prefab, not independent copied images.

All 74 cutscene Images inherit the new original artwork by the unchanged GUID. Screen-space-overlay UI does **not** receive 3D Unity point lights. No dummy lights or costly 222-light overlay bridge was added. An economical future solution is a single authored world-space hallway/camera render target reused by the four cutscene strips, rather than replicating per-image world lights or painting glow into the art. That would change cutscene rendering and requires a separately scoped native review.

The hallway trap warning v5 sources, colliders, door positions, movement and scene gameplay bindings are not modified here.

## Verification

```
python Tools/HallwayLighting/verify_sconces.py
bash Tools/HallwayLighting/check_unity_api.sh /path/to/Unity/Editor/Data
python Tools/TheatreVideos/verify_recording_fixture.py
python Tools/HallwayPanTests/validate_authored.py
```

`verify_sconces.py` checks serialized references, exactly three enabled real lights, distinct pixel-aligned centers, unmodified original art hash/alpha bounds, fixed intensity/range/shadow settings, twenty inherited fixtures and actual light-reactive material bindings. `verification.json` records the result and limits.

`check_unity_api.sh` compiles the editor validator against actual Unity 6000 engine/editor assemblies, without gameplay or engine doubles. It verifies C# API compatibility only; it does not import/compile the GPU shader or run a scene.

After a successful native Unity import, run `Tools > Haunted Fish > Validate authored hallway sconces`, then inspect Game and tutorial cameras in motion. The validator checks resolved prefab inheritance, actual `Light`/SpriteRenderer objects, import dimensions and shader import errors. Render, lighting, mobile performance and guide playback still require native verification. **No native import/capture pass is claimed by these source checks.**
