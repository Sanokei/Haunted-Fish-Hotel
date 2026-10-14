# Device-aware input prompt checks

## Verification levels

These are three different checks. Passing one does not imply the others passed.

1. `RunMono.sh` compiles the production `Assets/Scripts/InputPrompts/*.cs` files with deliberately small input and presentation boundary doubles, then executes the production resolver, device-event policy, local runtime-map registry, Image presenter, and TMP presenter. This is deterministic source-behavior coverage, **not native Unity input, real controller hardware, imported sprites, rendered UI, or play-mode lifecycle evidence**.
2. `CheckUnityApi.sh` compiles those same production files and `Assets/Editor/InputPromptNativeValidation.cs` against installed **real Unity, Input System, UI, and TextMeshPro assemblies**. It has no application boundary doubles. This checks actual API compatibility; it does not launch or import a Unity project. By default it uses package DLLs supplied in the installed Editor template cache. Pass the target project's `Library/ScriptAssemblies` directory as argument 2 to check that project's actual resolved package DLLs instead.
3. `InputPromptNativeValidation.Run` is an explicitly invoked native Editor validator. It creates synthetic devices using the real Input System, sends full state/delta events, runs real input updates and binding changes, and exercises production views plus imported authored prefab slots. Its source has been compiled, but **the native method has not run in this environment**. Physical gamepad input and visual appearance still require separate acceptance even after a successful synthetic native run.

## Repeat the executable checks

```sh
bash Tools/InputPromptTests/RunMono.sh /path/to/Unity/Editor/Data
bash Tools/InputPromptTests/CheckUnityApi.sh /path/to/Unity/Editor/Data
# Optional, use project-resolved InputSystem/TMP/UI package assemblies:
bash Tools/InputPromptTests/CheckUnityApi.sh /path/to/Unity/Editor/Data /path/to/project/Library/ScriptAssemblies
```

The scripts compile into a temporary directory, delete it on exit, return nonzero for a failure, and never launch Unity. Expected serialized-field `CS0649` warnings arise because Unity populates Inspector fields.

Recorded on 2026-10-13 against Unity Editor 6000.3.6f1:

- **147 production-source assertions passed, zero failed.**
- **Real API compilation passed**, including the native validator, with only expected Inspector-field warnings.
- **Native Editor execution: not run.** The environment has an existing blocked IPC/EPERM launch restriction. No retry or bypass was attempted.

The asset importer/manifest verifier in this directory has a separate owner and scope. Its import/provenance results are not implied by the source or API checks above.

## Executable source coverage

- Keyboard/mouse, XInput/Xbox, DualShock, DualSense, and unknown/generic classification. A plain Gamepad never becomes Xbox by assumption.
- Keyboard ↔ controller, DualShock ↔ DualSense, and generic device switches. Presses, trigger threshold, D-pad, meaningful mouse movement and scroll count as activity.
- Idle stick drift, slow meaningful tilt, held stick noise after a keyboard switch, two simultaneously held sticks, large directional changes, and neutral rearming.
- Release-only input, held trigger noise, mouse jitter, unsupported/non-state/disabled-device events do not steal ownership.
- Disconnect, re-add, disable, fallback to a recently used available device, and reconnect without unsolicited ownership changes.
- Single shared input/action/device subscriptions, view listener removal, subsystem reset, re-enable, and a session owner that remembers activity while every prompt is hidden.
- Physical binding paths, keyboard-layout display names, Xbox A versus PlayStation Cross, West/X versus Square, generic positional labels, and family-specific bindings.
- Action/map/asset masks, per-binding map device restrictions (including Mouse-only versus Keyboard), disabled/removed restricted devices, invalid control names versus valid uncatalogued F13, disabled/empty/malformed bindings, missing actions, mouse-only/keyboard-only truthfulness, and explicit fixed paths.
- Complete WASD and modifier composites, rebinding one part, disabled parts, stable physical ordering, and full text when there are too few authored slots.
- Runtime cloned action-map registration, immediate refresh, idempotence, replacement ownership, stale-owner unregister protection, fallback to authored maps, and clone rebinding.
- Image sprite refresh and stale-slot clearing; TMP inline sprite replacement and safe text fallback; native-looking adapter events named `BoundControlsChanged`; missing artwork; and refusal to use Xbox artwork for an unknown device.

`UnityDoubles.cs` models only the API surfaces used by these sources. Its layout inheritance, binding masks, state storage, callback order, sprite names, and UI properties are deliberately simplified. In particular it does not implement actual control processors, Unity's binary state layouts, action resolution, player loop, import pipeline, rendering, object lifetime, or OS input. Tests that depend on these belong in the native validator, not in stronger claims about this harness.

## Authored native validation

Use an idle disposable copy of the project after all assets and packages have imported. Open an empty, saved scene outside Prefab Mode, with no prompt listeners active. Avoid interacting with physical controls during this synchronous synthetic-input run.

Run `Tools > Haunted Hotel > Validate native input prompts (isolated)`, or invoke `InputPromptNativeValidation.Run` through Unity's existing `-executeMethod` workflow on a machine where native Editor launch is allowed.

The method:

- Uses `InputSystem.AddDevice` with real Keyboard, Mouse, XInputController, DualShockGamepad, DualShock4GamepadHID, DualSenseGamepadHID and generic Gamepad layouts.
- Sends `QueueStateEvent` keyboard/gamepad/mouse/DualSense reports, actual control-state events for DualShock4 HID, and `QueueDeltaStateEvent` stick updates; each is processed with `InputSystem.Update`.
- Checks native callback-driven Image changes, stick processors and noise policy, release-only events, device removal/re-add, `ApplyBindingOverride` and `BoundControlsChanged`, complete/disabled composites, binding masks, restricted devices, and an actual interactive keyboard rebind.
- Loads and clones the actual imported `HotelMultiplayerActions` asset, checks `Ghost/Dispose` has controller North with no competing Ghost action on that button, checks Xbox Y / PlayStation Triangle prompts, and verifies a native North event performs Dispose once while release does not. This checks the input action; held/possessed gameplay disposal behavior is a separate gameplay check.
- Registers a real runtime action map, then scans imported Resource prefabs containing authored `InputGlyphPrompt` or `InputPromptSpriteText` components. Loads only those into isolated prefab contents without saving. Checks binding references, every Image slot and exact TMP token output for each device family after real device events.
- Verifies listener removal and session-owned tracking while all views are hidden, then reopens a presenter and checks the remembered device.
- Restores the previous Input System settings, destroys only temporary objects, removes only created test devices, unregisters the temporary map, resets temporary tracker state, unloads isolated prefab contents without saving, and writes a detailed report to the OS temporary directory as `HauntedFish-InputPrompts-NativeValidation.txt`.

A failed or unexecuted native validator is not a pass. The method does not fabricate a success report on compile. Its report is generated only by native execution.

## Remaining physical and visual acceptance

On a native machine, verify a keyboard/mouse, Xbox-style pad, DualShock/DualSense pad, and unknown generic pad through representative lobby/intro, round-guide, active HUD and boss views. Switch while UI is open and while hidden; rebind a simple action and composite; disconnect/reconnect; ensure prompts fit their authored slots and remain legible at supported aspect ratios. Confirm generic prompts show positions rather than Xbox letters. This physical/render acceptance has not been performed by the source suite or compilation check.

## Shared presentation and integration

The existing Unity Input System remains the input library; no additional input framework or paid asset is installed. `InputPromptDevices` observes meaningful device events once for the session (including periods when all UI is hidden). `InputPromptResolver` resolves effective bindings, masks, device restrictions and complete composites. `InputPromptActions` accepts explicit local runtime-map registration so cloned/rebound gameplay actions override authored source maps without scene searches.

`InputGlyphPrompt` fills serialized Image slots and a text fallback. `InputPromptSpriteText` replaces authored `{input:Game/Jump}` tokens in an existing TMP object using the explicitly assigned sprite asset. Fixed controls use tokens such as `{input:<Mouse>/leftButton}`. Neither presenter creates UI, enables action maps, polls an Update loop, or assumes that a generic controller uses Xbox labels.

Serialized integration covers both single-page ready guides, contextual Ghost HUD, stairwell challenge entrance, pause-menu instructions, theatre exit hint and lobby-introduction skip label. Controller Discard is the real `Ghost/Dispose` binding `<Gamepad>/buttonNorth` (Xbox Y, PlayStation Triangle), with Q retained. It feeds the existing held/possessed disposal handler; no other Ghost action uses North. Pointer-only spotlight/placement/click-trap and keyboard-only theatre/intro controls continue to show an honest device requirement on a gamepad.

Artwork comes from the official [Kenney Input Prompts](https://kenney.nl/assets/input-prompts) 1.5A package under CC0. `Assets/Art/InputPrompts/License.txt`, `SOURCE.txt` and `glyph-manifest.json` retain source URLs and pinned hashes. The 182 PNGs are copied byte-for-byte; the TMP atlas only places their unchanged pixels into cells. `import_glyphs.py --verify` checks all mappings and every atlas cell.

Additional current checks:

```sh
python Tools/InputPromptTests/import_glyphs.py --verify
python Tools/InputPromptTests/verify_authored_prompts.py
python Tools/ReadyUpTests/verify_layout.py
```

- 640 serialized glyph/catalog/presenter/action checks pass across 19 presenters.
- The guide worker's refreshed suite passes 682 source/layout checks, including Y/Triangle Discard proofs.
- `CheckUnityApi.sh` additionally compiles the actual `MemberMenuTutorial`, `HotelAudioRouting` and `TutorialRoleContent` consumer sources against real engine assemblies, without application doubles.
- `HotelPlayerMovement` and its movement/trap collaborators also passed a scoped real-engine API compile using the pre-existing project-service boundary doubles in the local trap-review harness. That check is not a full gameplay assembly compile.
- `BossArenaCoordinator` and `GhostPlacementController` have source/reference checks, but their complete gameplay/network assembly has not been compiled here. The migrated entrance `_Prompt` member is `TMP_Text`, matching its authored TMP component; the Ghost status `_Label` remains `UnityEngine.UI.Text` and its separate glyph presenter references authored TMP text.
- Unity project import, Mirage weaving, native execution, physical devices and rendered UI remain unverified because the existing native Editor IPC restriction prevents launch. No bypass was attempted.
