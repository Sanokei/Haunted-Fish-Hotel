# Single-page round ready guide

The existing `FishRoundHowTo.prefab` and `GhostRoundHowTo.prefab` retain their asset GUIDs and role-specific references from `AtticRoundCutscene`. Each now contains one authored Canvas, one fixed guide page and one Ready button. The page has a role heading, objective, controls, current trap rules, participant counter and setup rule. There are no videos, page navigation or runtime-created UI elements in this guide. Theatre assets, title cards and playback remain separate and unchanged.

## Serialized layout proof

`Evidence/serialized-layout.json` is derived from the actual prefab objects and imported TTF files. It records file IDs, sizes, copy, measured line widths, conservative full-font line-height budgets, and safe-area bounds for 720p, 1080p, 16:10, 4:3, ultrawide, an inset landscape display and portrait. The accompanying Fish/Ghost PNGs are reproducible source previews drawn from those rectangles and fonts, visibly labeled as source-only. This is source/metric evidence, not a Unity screenshot or a runtime rendering pass.

- Both canvases use Expand scaling with a 1280 × 720 reference.
- Object 210 is the normalized display-safe container. Object 220 holds the fixed authored page.
- The page uses the existing Modak heading font. Body copy references the real imported Liberation Sans asset instead of the old built-in-font placeholder.
- All content and live labels have independent authored rectangles. Text does not auto-shrink. Controls and rules are 24 reference pixels; the page scales uniformly only when needed to fit the safe display.
- `RoundHowToSafeArea` listens for canvas rendering and only changes existing anchors/scale when display geometry changes. Disable removes its callback.

## Readiness behavior

`RoundHowToPage` keeps the original `Begin`, `Cancel`, `SetVisible` and `Showing` API. Its input/status methods are small; deterministic display policy lives in `RoundHowToState`. The 1.5-second reading beat starts only while the guide is visible, the local cinematic has been acknowledged, and no menu blocks input.

A deliberate click, Enter, Space or gamepad South submits once. Hidden/menu-blocked input is discarded rather than queued. The same page then displays `READY SENT`, followed by `YOU ARE READY`. The separate group status shows only the replicated `N / N PLAYERS READY` count, without repeating the button instruction. The presentation owner closes it only when the host releases the cinematic/ready barrier. Cancel, disable and replay remove the old listener and callback; a replay restores the visible guide and fresh read beat.

`GameReadyGate`, RPC ownership checks and `GameSceneController.ReleaseIfFinished` are unchanged. Both every-participant cinematic completion and every-participant deliberate readiness remain required before the shared setup deadline. Late arrivals and disconnects update the existing host-owned roster gate. No local page changes authoritative ready counts or countdown time.

## Repeat the checks

```sh
bash Tools/ReadyUpTests/RunMono.sh /path/to/Unity/Editor/Data
bash Tools/ReadyUpTests/CheckUnityApi.sh /path/to/Unity/Editor/Data
python3 Tools/ReadyUpTests/verify_layout.py --report Tools/ReadyUpTests/Evidence/serialized-layout.json
python3 Tools/ReadyUpTests/render_source_previews.py --output Tools/ReadyUpTests/Evidence
MONO_ROOT=/path/to/Unity/Editor/Data/MonoBleedingEdge bash Tools/RoundIntroductionTests/run-mono.sh
```

`verify_layout.py` uses the existing source parser plus PyYAML and Pillow. It checks the real prefab hierarchy, font files, non-overlapping text bounds, safe-area geometry, current input bindings/trap settings and all-ready release guard. `RunMono.sh` compiles actual production gate/presenter/safe-area code with narrow Unity/input doubles. `CheckUnityApi.sh` also compiles the changed production scripts against installed real Unity/UI/InputSystem assemblies, with only the HotelPlayer boundary doubled. It does not launch the Editor.

Recorded 2026-10-13 results:

- 1,892 explicit readiness, role, stale-scope, ownership, late-join, disconnect and replay checks passed.
- 58 actual page-adapter checks passed, including early/hidden/menu input, repeated clicks, per-frame SetVisible(true) after a click, pending acknowledgement, keyboard/gamepad, counters, disable and replay.
- 49 actual safe-area-adapter checks passed, including geometry changes, insets, unchanged-frame stability, disable/re-enable and transient zero-size screens.
- 266 serialized-layout/font/current-rule/dynamic-label checks passed.
- Real Unity/UI/InputSystem API compilation passed. Only the expected serialized-field assignment warnings remain.
- The full standalone round-introduction runner passed with the new display-policy source included.

Native Unity launch is blocked by the environment's existing EPERM restriction. No bypass was attempted. Native import, real UI rendering/input, play-mode lifecycle, Mirage weaving and two-client delivery/synchronized countdown remain unverified here. The migrated `GameTutorialPlaybackBatch.Run` is available for an explicit `--game-tutorial-playback` command in an idle disposable Unity project; it now validates imported single-page guides rather than the removed ready-page video playback. Theatre's native playback validation remains independent.
