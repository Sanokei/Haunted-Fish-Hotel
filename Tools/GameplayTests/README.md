# Gameplay and Lobby interaction verification

Implemented in the current Unity/Mirage flow without replacing the multiplayer rewrite or changing server configuration.

- `Assets/Scenes/Game.unity`: ten-second authoritative ghost setup after the existing random wheel/attic intro. Fish input/collision simulation remains blocked until the original deadline; late arrivals inherit the phase and acknowledge their own intro. Serialized countdown, spook meter, hallway floor/ceiling references and conveyor floor bounds.
- `Assets/Resources/HotelNetworkPlayer.prefab`: editable Lobby walk 6, Game walk 1.8/run 4.5. Shift is Game-only, with shared run state and faster fin cadence. Game W/S bindings removed; authored capsule extents constrain hallway travel/teleport.
- `Assets/Scripts/Multiplayer/GameSceneController.cs`: shared ghost flight speed 8 for prediction/server, above fish run; editable duration/speed.
- `Assets/Resources/Traps/ShoppingCartTrap.prefab` and `ClickTrap.prefab`: measured opaque art rectangle from `shopping card.png`; cart foot anchors to the actual projected collider floor and rotates/scales with projected world axes.
- `Assets/Scripts/Multiplayer/GameHauntingController.cs`: exposed spook threshold 100, contact rate 50/second, swap cooldown 1 second. Moving/active trap contact accumulates authoritative fish spook. The spooked fish becomes the real ghost player; the previous ghost takes its body position/facing as a fish. Network identities/input owners remain stable, controlled/held state releases, and existing trap ownership transfers.
- `Assets/Resources/GhostEmergence.prefab`, `Assets/Dialogue/GhostEmergence.ink` and `.json`: reusable authored `Assets/Art/player_ghost.png` rise/clear through existing StoryUI/Ink layouts.
- Q disposal reuses `Assets/Resources/PossessionSmoke.prefab` and `Assets/Dialogue/GhostPossessionSmoke.ink/.json`, with one synchronized event at the removed object's position and timed Ink cleanup.
- `Assets/Scenes/Lobby.unity`: faster walk without sprint; existing desk cursor delegates through `Assets/Scripts/Multiplayer/Lobby/LobbyCursorManager.cs`. Grandmafish is the existing `Great Grandfish`/`greatgrandfish` scene hierarchy. Its camera-facing front, range, height, target collider, question cursor and dialogue references are serialized. Press/release interaction respects owned local input, UI/dialogue blocking and physical occlusion.
- `Assets/Dialogue/GrandmafishInspect.ink/.json`: new editable local item dialogue, relevant-player audience.

The reference's actual [CursorManager](https://github.com/Sanokei/Fish-Out-Of-Water/blob/c0188dcd1968f92b0a0e899fd35ed8f6a9b139ac/Assets/Scripts/CursorManager.cs) and [TutorialItemsDialogue](https://github.com/Sanokei/Fish-Out-Of-Water/blob/c0188dcd1968f92b0a0e899fd35ed8f6a9b139ac/Assets/Scripts/Tutorial/TutorialItemsDialogue.cs) were read at the verified commit. Their camera-ray/blocker and mouse-release/dialogue pattern was adapted to cached camera ownership, New Input System and current audience APIs. Existing local `Assets/Art/Cursors/mark_question.png` was reused.

## Reproduce

Use an existing disposable production copy with cached packages under personal Temp:

```powershell
./Tools/GameplayTests/Run.ps1 -ValidationProject '<existing Temp project>'
```

The runner rejects a non-Temp destination or open validation Editor. It preserves the user's Editor, copies current authorized assets/scripts into the existing disposable copy, and runs native Helper → Lobby → Game → Lobby. Test-created actors are fixtures only. No package install or server changes.

## Saved checkpoint

[Evidence/2026-10-07](Evidence/2026-10-07) contains the full production compile/Mirage weave log, 48 passed real host checks, source hashes and actual Unity camera renders. Checks cover native cursor ray and mouse release/local Ink, proximity/front rejection, setup/late server actor/no timer reset, native held-Shift run/release, W/S exclusion, body-extents teleport bounds, conveyor ends, disposal Ink, real moving-cart spook, real role/body/trap-owner swap, duplicate prevention, emergence cleanup and actual Game-to-Lobby speed/input restoration.

The temporary peer is a real spawned Mirage server actor with no second admitted client. Two-client delivery, reconnect and network latency remain unverified. Camera.Render frames are actual Unity renders, rather than an interactive Editor preview. Grounding was inspected under camera roll/zoom. Native Lobby screenshots were unavailable; positive interaction used the actual camera and input devices. Later BossFight integration has a separate evidence set and may change these hashed sources; this checkpoint remains unchanged.

Generated .NET test outputs were left intact after automatic approval review rejected a reset because it might discard user edits. The earlier audit evidence remains separate in `Tools/PerformanceTests/Evidence/2026-10-07`.

Final integration regression: `Evidence/2026-10-08` preserves a fresh PASS 48 run with BossFight integration present, including native local cursor/Ink, setup, movement/run, grounding, Q disposal, spook handoff/emergence and Game-to-Lobby restoration. The fixture accepts the current multiplayer implementation's automatic all-ready travel or its leader Start action, preserving production code.

Final shared-view/grace/stair regression: `Evidence/2026-10-08-final` records PASS 48 with current production scripts and authored Game/Lobby assets, full native compile/weave log, source hashes and actual renders. The earlier dated checkpoints remain intact.

Imported-table/reaction regression: `Evidence/2026-10-08-imported-reactions` contains a fresh PASS 48 on the final production scripts, including participant visual hiding and the shared Ink Spin extension. Native source hashes and complete compile/weave log are retained.
