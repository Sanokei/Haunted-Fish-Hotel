# Hallway end-to-start intro shot

The saved `AtticRoundCutscene.prefab` reuses its current hallway strip and four saved starting-area fish. The actual `HALLWAY-Attic-Door-Open-panel.png` is an authored Image at the Game scene's end coordinate, X51.8, with its original aspect ratio and a four-world-unit height. No player/network transforms or gameplay cameras change.

`GhostRoundIntroduction.ink` now owns the complete sequence: original seven-second wheel/pulse, starting-fish look, attic ascent and ghost reveal, accelerating descent, white cut, 0.7-second end-door hold, 0.85-second end-to-start horizontal pan, then a one-second unblurred fish hold. The manager reads all steps and preserves their easing. It closes the cinematic and acknowledges intro completion only after the final hold. Existing all-client intro/explicit-ready/setup/countdown ownership is unchanged.

- Motion: existing `ParallaxTo` / `StoryParallaxPlayer`, one serialized `hotel_camera` rig and four absolute poses. All poses have zero push, preserving fish scale.
- Blur: `AtticReturnBlur`, saved vertical and horizontal material instances, separately toggled by Ink. Both lists bind the actual artwork Images, including hallway panels/decor/fish and the new door. Material restoration is idempotent and survives changed bindings. The shader uses screen-space derivatives for its 13-tap directional kernel without changing geometry or UV scale, avoiding the old large-panel shrink.
- Streaks: `CinematicStreaks` observes only the authored vertical strip displacement. It never moves a camera. Ink enables/disables its saved overlay.
- Flash: saved white Image, controlled through existing `SetVisible` and `FlashFrames` glue.

## Executed checks

Run these from the repo root:

    MONO_ROOT=/path/to/Editor/Data/MonoBleedingEdge bash Tools/RoundIntroductionTests/run-mono.sh
    MONO_ROOT=/path/to/Editor/Data/MonoBleedingEdge bash Tools/HallwayPanTests/run-mono.sh
    UNITY_EDITOR_DATA=/path/to/Editor/Data bash Tools/HallwayPanTests/compile-engine.sh
    python3 Tools/HallwayPanTests/validate_authored.py

The playback suite executes production StoryUI/ParallaxPlayer/blur logic against explicit engine doubles at 24/30/60/144 FPS, including interrupted camera stages, stale iterators, repeated rounds and material restore. The compile check uses real installed Unity engine/UI assemblies with project/network boundary doubles. The Python checks inspect serialized references, hierarchy, geometry, directions, fish slots/scale and actual sprite bindings. None is a native Unity runtime or GPU-rendering pass.

To recompile only this Ink JSON: run the round suite with `--compile-round`. The restored .NET project also supports `dotnet run --project Tools/RoundIntroductionTests` where .NET 9 is installed.

## Source-layout proof and native follow-up

    python3 Tools/HallwayPanTests/validate_authored.py --proof-dir /path/to/proof-output

Generated proof images are explicitly labeled source-layout composites, not Unity captures. They show the saved door/end, pan midpoint, and starting frame using source sprites. The fish are representative player-prefab body/fin art at the unchanged staging dimensions. Live network appearance, native mesh bounds, lighting and the GPU blur shader are not simulated.

Native Unity launch remains blocked in this cloud environment by the previously established EPERM restriction; it was not bypassed. On an available native editor, run `Tools > Haunted Fish > Validate authored hallway intro pan`, then play the actual Game scene. Verify the full end-door shot and directional blur at paused/realtime scale, close/disable during each effect, repeat round, all four fish, and both roles reaching their guide only after landing. Verify two clients independently finish intro, then explicitly ready before setup/countdown. The menu validates actual imports/bindings/shader compilation; it does not substitute for those runtime/render/network checks.
