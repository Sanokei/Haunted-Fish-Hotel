# Single-page round ready guide

The existing `FishRoundHowTo.prefab` and `GhostRoundHowTo.prefab` retain their asset GUIDs and role-specific references from `AtticRoundCutscene`. Each now contains one authored Canvas, one fixed guide page and one Ready button. The page has a role heading, objective, controls, current trap rules, participant counter and setup rule. There are no videos, page navigation or runtime-created UI elements in this guide. Theatre playback remains separate. Its authored caption/content sources now say Dining cart; existing MP4s remain unchanged until a real replacement capture.

## Serialized layout proof

`Evidence/serialized-layout.json` is derived from the actual prefab objects and imported TTF files. It records file IDs, sizes, copy, measured line widths, conservative full-font line-height budgets, and safe-area bounds for 720p, 1080p, 16:10, 4:3, ultrawide and an inset landscape display. The accompanying Fish/Ghost PNGs are reproducible source previews drawn from those rectangles and fonts, visibly labeled as source-only. This is source/metric evidence, not a Unity screenshot or a runtime rendering pass.

- Both canvases use Expand scaling with a 1280 × 720 reference.
- Object 210 is the normalized display-safe container. Object 220 holds the fixed authored page.
- The page uses only two coherent families: unchanged Modak role/section headings and the approved Haunted Intertitle body face. Short key actions, trap names and lethal warnings use that same face’s authored Bold style. Most copy stays regular; no unrelated font was added. The source proof approximates the short bold emphasis, not native Unity font rasterization.
- All content and live labels have independent authored rectangles. Text does not auto-shrink. Controls are 28 reference pixels and rules 27; the page scales uniformly only when needed to fit the safe display.
- `RoundHowToSafeArea` listens for canvas rendering and only changes existing anchors/scale when display geometry changes. Disable removes its callback.

## Active-device controls

Each short action row owns existing glyph Image slots and an empty fallback Text slot, bound to the shared `InputGlyphPrompt` catalog/resolver. Keyboard composites have separately authored key slots. The runtime changes sprites/visibility in place when input family or binding changes; it never constructs UI. Only the current PC, Xbox or PlayStation glyph family is shown. Discard uses the actual Ghost/Dispose binding: Q on PC, Y on Xbox and Triangle on PlayStation. Pointer aiming is still mouse-only, so that row explicitly says Mouse required on a controller. Unknown/unbound controls never invent buttons. The ready action has its own Enter/South glyph.

The current previews are named `*-guide-pc-1280x720-source.png`, `*-guide-xbox-1280x720-source.png`, `*-guide-playstation-1280x720-source.png`, and `*-guide-playstation-1024x768-source.png`. Generic `fish-guide-source.png`/`ghost-guide-source.png` aliases now contain the PC version. Counts stay at authored 0/0; optional populated examples explicitly label their counts illustrative.

`author_guides.py` reproducibly writes the serialized objects while preserving the existing prefab/script identities. It requires the shared catalog/art to be present. `guide_source.py` reads those actual objects, fonts, action bindings and glyph sprites for source tests/previews.

## Readiness behavior

`RoundHowToPage` keeps the original `Begin`, `Cancel`, `SetVisible` and `Showing` API. Its input/status methods are small; deterministic display policy lives in `RoundHowToState`. The 1.5-second reading beat starts only while the guide is visible, the local cinematic has been acknowledged, and no menu blocks input.

A deliberate click, Enter, Space or gamepad South submits once. Hidden/menu-blocked input is discarded rather than queued. The same page then displays `READY SENT`, followed by `YOU ARE READY`. The separate group status shows only the replicated `N / N PLAYERS READY` count, without repeating the button instruction. The presentation owner closes it only when the host releases the cinematic/ready barrier. Cancel, disable and replay remove the old listener and callback; a replay restores the visible guide and fresh read beat.

`GameReadyGate`, RPC ownership checks and `GameSceneController.ReleaseIfFinished` are unchanged. Both every-participant cinematic completion and every-participant deliberate readiness remain required before the shared setup deadline. Late arrivals and disconnects update the existing host-owned roster gate. No local page changes authoritative ready counts or countdown time.

## Repeat the checks

```sh
bash Tools/ReadyUpTests/RunMono.sh /path/to/Unity/Editor/Data
bash Tools/ReadyUpTests/CheckUnityApi.sh /path/to/Unity/Editor/Data
python3 Tools/ReadyUpTests/verify_layout.py --report Tools/ReadyUpTests/Evidence/serialized-layout.json
python3 Tools/ReadyUpTests/render_source_previews.py --output Tools/ReadyUpTests/Evidence --family PC
python3 Tools/ReadyUpTests/render_source_previews.py --output Tools/ReadyUpTests/Evidence --family Xbox
python3 Tools/ReadyUpTests/render_source_previews.py --output Tools/ReadyUpTests/Evidence --family PlayStation
python3 Tools/ReadyUpTests/render_source_previews.py --output Tools/ReadyUpTests/Evidence --family PlayStation --width 1024 --height 768
MONO_ROOT=/path/to/Unity/Editor/Data/MonoBleedingEdge bash Tools/RoundIntroductionTests/run-mono.sh
```

`verify_layout.py` uses the existing source parser plus PyYAML and Pillow. It checks the real prefab hierarchy, font files, non-overlapping text bounds, safe-area geometry, current input bindings/trap settings and all-ready release guard. `RunMono.sh` compiles actual production gate/presenter/safe-area code with narrow Unity/input doubles. `CheckUnityApi.sh` also compiles the changed production scripts against installed real Unity/UI/InputSystem assemblies, with only the HotelPlayer boundary doubled. It does not launch the Editor.

Recorded 2026-10-13 results:

- 1,892 explicit readiness, role, stale-scope, ownership, late-join, disconnect and replay checks passed.
- 58 actual page-adapter checks passed, including early/hidden/menu input, repeated clicks, per-frame SetVisible(true) after a click, pending acknowledgement, keyboard/gamepad, counters, disable and replay.
- 49 actual safe-area-adapter checks passed, including geometry changes, insets, unchanged-frame stability, disable/re-enable and transient zero-size screens.
- 682 serialized-layout/font/active-device/dynamic-label checks passed.
- Real Unity/UI/InputSystem API compilation passed. Only the expected serialized-field assignment warnings remain.
- The full standalone round-introduction runner passed with the new display-policy source included.

Native Unity launch is blocked by the environment's existing EPERM restriction. No bypass was attempted. Native import, real UI rendering/input, play-mode lifecycle, Mirage weaving and two-client delivery/synchronized countdown remain unverified here. The migrated `GameTutorialPlaybackBatch.Run` is available for an explicit `--game-tutorial-playback` command in an idle disposable Unity project; it now validates imported single-page guides rather than the removed ready-page video playback. Theatre's native playback validation remains independent.
