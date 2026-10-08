# BossFight arena assets and checks

The scene is an additive arena at world X=1000, using the delivered native Blender FBX. Only the matched fish and ghost control paddles; hallway spectators can watch through its camera. Existing multiplayer identity, hallway return and input ownership remain with the hotel coordinator.

Assets:
- `Assets/Scenes/BossFight.unity`: actual serialized PrettyObject section labels, room walls outside its playable interior, authored table geometry, two paddles, cue-ball Rigidbody, split end rails and real trigger goals, manager, spectator spawn markers and initially inactive participant camera with HotelViewCamera.
- `Assets/Resources/BossFight/FeltHockeyTable.prefab`: reusable serialized table with editable dimensions and materials.
- `Assets/Art/BossFight/BossFight_FeltHockey.fbx`: delivered Y-up/Z-length static table, 69 imported mesh renderers in both scene and reusable prefab.
- `Assets/Art/BossFight/EmbeddedTextures`: extracted felt color, felt normal and walnut color textures. Eight `BF_*.mat` assets remap FBX materials to installed URP Unlit, preserving the hotel's flat visual treatment. Felt and walnut use embedded base colors; the normal map is retained for editing but unused by Unlit.
- Historical `FeltHockeyTable.obj` and `.mtl` remain as fallback source; temporary table renderers are disabled.
- `Assets/Art/BossFight/*.mat`: installed URP Unlit colors, using the hotel's flat artwork style. `AirHockey.physicsMaterial`: frictionless restitution.
- `Assets/Scripts/BossFight/BossMatch.cs`, `BossArenaManager.cs`, `BossGoal.cs`: independent rules, cached/injected physical arena and trigger endpoints.

Integration contract, namespace HauntedFish.BossFight:
- Assign authority once with `SetAuthority(bool)`.
- `TryBegin(uint fishId, uint ghostId)` admits two distinct players, rejects a busy arena, resets scores and advances MatchEpoch.
- `ParticipantsReady` starts false. `SetParticipantsReady(true)` releases a fresh serve delay once; duplicate readiness does not restart the delay. Inputs and physical scoring are blocked until admission completes.
- `ApplyInput(uint id, Vector2 axis)` validates finite axes, clamps magnitude and permits only an admitted participant. Coordinates control the XZ paddle in that participant's own half; stale input stops after 0.3 seconds.
- `Completed` supplies BossMatchResult (Epoch/FishId/GhostId/WinnerFish/Cancelled) exactly once. Fish wins at one goal; ghost wins at two. `Cancel(uint departedId)` handles either departure; spectators cannot cancel.
- `SnapshotChanged` emits JSON at 20 Hz; `Snapshot` returns current JSON. An observer applies monotonically newer epoch/revision snapshots with `ApplySnapshot(string)`, disables Rigidbody collision/simulation and never scores.
- `SetLocalParticipant(uint id)` preserves its existing participant-only camera contract. `SetLocalViewer(uint localId, bool watch)` exposes camera-only viewing to nonzero local hallway identities; it never changes participants, authority, readiness, scoring or input permission. The coordinator handles spectator membership, additive loading and hallway camera restoration. `Camera`, `FishSpawn`, `GhostSpawn`, `Bounds`, `Active`, `FishId`, `GhostId`, `MatchEpoch` are available to the transport/travel bridge.
- Preserve each actor's ownership identity and return position in the integration layer. Do not globally travel hallway players to this additive arena.

Verification:
- `dotnet run --project Tools/BossFightTests/BossFightTests.csproj`: 1502 independent rules checks, 100 repeated fish-win, ghost-win and cancellation cycles; busy/invalid identities, stale goals, duplicate completion and re-entry.
- `Run.ps1 -ValidationProject <existing disposable full production copy>`: latest PASS 49; historical initial PASS 24 checks in Unity 6000.3.6f1, isolated compile and Mirage weaving/import, real native Rigidbody serve and both trigger-goal thresholds, observer reconstruction, repeated/cancelled flow, rendered camera screenshot. Inspect the saved log/result rather than assuming launch success.

Limitations:
The original Library reference image was resolved but its supported download returned HTTP403; its pixels were not inspected, so reference matching is not claimed. Native Blender authoring happened on the cloud computer. The user-confirmed local FBX now supplies the final table; no desktop Blender installation was needed. Independent arena checks do not establish two-client networking success.

AuthorSerializedAssets.py writes the serialized files directly and indexed model data; it does not create scene GameObjects in a Unity Editor or at runtime. Existing scene and prefab identities are retained on regeneration. Do not regenerate after manually editing these new assets.

Final evidence: Evidence/2026-10-08/arena.png was visually inspected. Both paddle character badges, cue ball, goal trays and score are visible; no pink materials remain. BossScorePresentation and the authored ScoreText shader/material provide URP-compatible native TextMesh rendering without runtime scene construction. The final run exited successfully with no BossFight script or shader errors. Source Editor PID13188 stayed open. The isolated project has been released to the integration owner.

The original 24-check arena evidence is historical. The five spectator API checks were subsequently run as part of the latest 49-check native suite, alongside the final imported scene and reaction checks.


## Final imported model and reaction verification

`Evidence/2026-10-08-imported-reactions` contains 49 passed live native arena checks, physical cue-ball/trigger fish1/ghost2 goals, actual camera captures during winning bounce and frustration spin, full production compile/Mirage weave log and source hashes. Checks include repeated events/scores, observer snapshot reconstruction, duplicate revision rejection, component/view interruption, baseline restoration, pending terminal-goal cancellation and no delayed second result. The existing cue ball and all physical references are retained.

The separate 15-check `BossModelVerify.cs` inspection establishes 69 delivered FBX renderers and eight URP remaps, extracted base-color usage, Y-up/Z-length 6x10m felt at top Y1.6, two 2m end openings, 18 original table BoxColliders, matching prefab visuals and collider-free authored character proxies. Its first static render predates final framing; final live arena/shared-flow captures are the presentation evidence. Inspect it in an isolated full project; it compiles the four reaction Ink sources there and does not construct scene GameObjects.

Standing fish/ghost proxies use existing `Assets/Art/HotelFish/FISH-right.png` and `Assets/Art/player_ghost.png`, behind opposite table ends. Their actual matched IDs come from authoritative snapshots; real hallway fish art hides during participation and restores on return. Fish/ghost sprite orientation faces the table and preserves depth/occlusion. The front outward wall was moved farther outward to avoid obscuring the near player; paddle badge figures are disabled. Inspector references, poses, materials and timing remain editable.

Reaction sources: `Assets/Dialogue/BossHappy.ink`, `BossFrustrated.ink`, `BossWin.ink`, `BossLose.ink`, with corresponding compiled JSON. Happy uses bounce; frustration uses vertical movement then spin; terminal sequences use win/lose variations. They run through the existing StoryFunctions/StoryUI sequence integration, with one shared epoch/event per score and remaining duration. Input/physics remain authoritative and result return occurs once after the terminal reaction, or immediately on cancellation.

The user-authorized local FBX resolves the earlier Library download blocker. No desktop Blender installation was required. The cloud-created source .blend/ZIP was not imported on this desktop. Two admitted clients and network latency/reconnect delivery remain unverified.
