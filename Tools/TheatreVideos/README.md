# Silent-film tutorial guides

`Assets/Dialogue/TutorialRecording/*Recording.ink` owns supported recording captions and timed production demonstrations. Cards are full-screen intertitles; baked caption/footer regions are cropped from the following actual gameplay shot and the remaining image is letterboxed, never stretched. There are no repeated corner role labels in the finished guide. The sole opening titles are `Guide to all Fish` and `Guide to the Ghost`. The death intertitle is spelled `Wowza` and belongs after a real lethal demonstration, never the nonlethal dining cart.

## Current state and pending work

`recording-plan.json` is an authored **pending-capture plan**, not footage evidence. Both uploaded art ZIPs were received and CRC-verified. Production and tutorial trap prefabs now contain the new dining cart, angler and separated chandelier art. The existing `TutorialRecording.unity` has a linked stage instance and inherits the updated `TutorialRecordingStage.prefab`: three linked production actors (two fish and a ghost), scoped haunting, real piranha/death bindings, four trap definitions and a wide trap shot. Native Unity import/capture is still blocked in this cloud environment. No MP4 was generated, replaced or marked current.

Executable Ink and the director now cover nonlethal dining cart impact/recovery, a complete stationary-cart running jump, current danger-band animation, separated chandelier sway/drop, the angler light, a two-fish lethal traversal with slowdown, and actual authored soul effects. All effects are observed from the production world/actor paths, never played or synthesized by the recorder. The role/death copy follows health-to-spectator behavior; there is no spook-driven role swap.

`TutorialSetup`, caption waits and camera transitions are cut from the finished guide. Their real time is included in source offsets; only `TutorialDemo` intervals become gameplay footage. Every pre-trim demonstration retains its original order and duration. The jump setup plans against actual enabled solid parts, the actor capsule and production motor values; the final observation still requires an actual contact-free full traversal. Linked actors inherit the intentionally launched running-jump air reach without any recording overrides. Normal ground speed, vertical jump and gravity are not changed by this fixture.

`TutorialRecordingAuthoring.Trap` preserves the production `GhostTrapAreaPresentation`, BOSS-cursor endpoints, animated Fish-ver-ghost fill and separated/light references. `author_recording_fixture.py` synchronizes the actual serialized tutorial trap copies/stage without running Unity, retaining existing GUIDs. Neither path adds runtime UI/object construction. The checked-in authored assets require **native import and visual/physics verification**, not replacement by a runtime-generated scene. Keep the entire danger band inside the final cropped gameplay viewport.

Offline input and damage contexts are explicit and scoped: no networked actor can use the injected input/damage fixture, each capture gets a unique round key, and status/readiness/round/version/room checks remain active. Production defaults remain unchanged without a fixture binding. Stop, failure, disable and completion release bindings and disable the recording actors.

The existing `Evidence/manifest.json` and review PNGs describe older archived recordings only. Those clips predate this refresh, and the archived Ghost footage ends during the chandelier drop. They must not be reused as evidence of new trap behavior or silently padded with new cards.

## Capture and build contract

After a successful native import and stage inspection, use the existing authored recording scene to capture both roles through the complete timed sequence with real Unity Recorder frames and log the production observations in `required_capture_checks` from the plan. For a successful native check, the observation file must contain the exact line `PASS <check-id>`. Review the actual footage before recording a pass.

The native recording batch exports `Recordings/TutorialEvidence/native-captures.json` only after the completed real captures satisfy all required observations. It does not import raw footage over the shipped silent guides. The manifest has `schema_version: 1`, `plan_revision` matching the plan and a `captures` object keyed by `Fish` and `Ghost`. Each role requires:

- `completed: true` and `native_scene: "Assets/Scenes/TutorialRecording.unity"`
- `source`: real native MP4 path, absolute or relative to the manifest
- `source_sha256`: SHA-256 of those actual video bytes
- `ink_sha256`: SHA-256 of the exact role Ink source used for that capture
- `observations`: native observation log path, absolute or relative to the manifest
- `observations_sha256`: SHA-256 of that log

Do not write a success manifest before these observations exist. `capture_contract.py` rejects missing checks, missing future chapters, duplicate opening titles, stale Ink, mismatched source/log bytes and incomplete capture claims. FFprobe then rejects footage shorter than the complete Ink sequence. Hashes establish identity, not visual correctness; native observation and human frame/playback review remain required.

Build from verified captures:

```
python Tools/TheatreVideos/build_intertitles.py --capture-manifest /path/to/native-captures.json
```

Requires Pillow, FFmpeg and FFprobe. `--ffmpeg` and `--ffprobe` override local tools; defaults use PATH. The card renderer uses the original checked-in Haunted Intertitle font for headings and body, with its provenance record and editable outline source. Its static TextMeshPro asset, real SDF atlas, material and reproducible CPU builder are documented in `../Fonts/TEXTMESHPRO.md`. There is no mixed-font fallback. Existing MP4 paths and `.meta` GUIDs are preserved. Both outputs are fully decoded before replacing either shipped MP4. Intertitles allow approximately 2.7 words per second plus one second, with a 3.5-second minimum. Theatre playback, readability, looping and production guide metadata must be verified in Unity after import.

## Static card direction and pacing

The original Haunted Intertitle font and approved reference establish the visual direction: warm ivory text, subtly textured charcoal, authored connected botanical/fan corners, straight engraved double rules and smaller matching instruction flourishes. `intertitle_cards.py` uses the original outlines, while `intertitle_borders.py` authors the connected vector ornaments without copying image pixels. The opening title is centered at the reference-like scale with a dominant main line. The preview's old death spelling is corrected to `Wowza`. Opening cards are title-only; role names never repeat in a corner.

Generate **static QA images only** and a source-duration estimate:

```
python Tools/TheatreVideos/preview_cards.py
```

The four new samples and `CardPreviews/HauntedIntertitle-v1/HauntedIntertitle-static-card-QA.png` are typography/layout previews, not gameplay screenshots or capture evidence. Earlier Smythe previews are preserved as historical artifacts. All 26 current cards are rendered and bounds-checked before the new samples are written. `CardPreviews/HauntedIntertitle-v1/estimated-timings.json` records the exact original-font and Ink hashes and estimates:

- Fish: **11 cards, 51.1 seconds reading + 34.6 seconds demonstrations = 1:25.7** (previous draft: 17 cards, 2:31.1).
- Ghost: **15 cards, 68.7 seconds reading + 51.0 seconds demonstrations = 1:59.7** (previous draft: 22 cards, 3:30.5).

The 2.7-words-per-second reading allowance plus one second and 3.5-second minimum are unchanged. Savings come from shorter prose, merged repeated instructions and cutting camera travel, not speeding text or demonstrations. Required mechanics, capture check IDs and all demonstration durations are retained. Fish's `Run`, `Knocked Back`, `Stay Alive`, `Watch The Ceiling`, `Watch Its Light` and `Still Watching` cards are folded into adjacent cards. Ghost's `Fly`, `Carry It To The Hallway`, `The Safe Hallway`, `A Brief Stun`, `Unpossess`, `Falling Chandelier` and `Know Your Trap` cards are folded into their controls/trap/death cards. The required `Swinging Chandelier`, `Dining cart`, `Read The Danger Bands`, `Angler` and `Wowza` headings remain.

These are source estimates. Actual native capture, montage rhythm, frame timing, gameplay visibility and theatre readability still require review.

## Source checks

```
python -m unittest discover -s Tools/TheatreVideos -p 'test_*.py' -v
python Tools/TheatreVideos/verify_recording_fixture.py
```

`CompileGuideInk.cs` builds against the vendored Ink compiler/runtime without Unity. Run from the repository root to compile and execute both guides and verify serialized JSON parity. Pass `--compile` only when updating the compiled JSON. For the bundled Unity Mono toolchain:

```
"$MONO/bin/mono" "$MONO/lib/mono/4.5/csc.exe" -nologo -out:/tmp/CompileGuideInk.exe Tools/TheatreVideos/CompileGuideInk.cs $(find Assets/Plugins/Ink/InkLibs/InkCompiler Assets/Plugins/Ink/InkLibs/InkRuntime -name '*.cs')
"$MONO/bin/mono" /tmp/CompileGuideInk.exe
```

These checks establish source/contract correctness only. They do not constitute native gameplay, authored-art, capture or two-client verification.
