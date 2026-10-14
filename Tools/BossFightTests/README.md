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

## 2026-10-10 mouse and practice fix

BossArenaCoordinator now routes pointer position through the boss camera to the owned paddle's half of the table, using the existing scoped axis command and speed-limited Rigidbody movement. Pointer control begins when the mouse moves or clicks; WASD retains priority. Member/settings input focus sends zero paddle axes. The cue ball is never positioned by pointer input.

The solo editor button now starts local practice with a restrained training ghost paddle and physical serves. Practice never creates network participants, publishes snapshots, awards a result, or changes round/inventory state. Its puck freezes during blocked input and resumes afterward. Real incoming challenges take priority over practice. The normal two-player match retains fish-one/ghost-two scoring and existing return behavior.

BossFight.unity now has one standing fish sprite beneath the previously empty reaction pivot and a smaller, centered ghost sprite. Existing disabled paddle badges stay disabled. Added BossArenaValidation cases cover practice identity/result isolation, serve/pause/exit, and one artwork per side. BossIntegrationRunner adds pointer projection, spectator/invalid input denial, non-teleporting puck, synthetic native mouse movement and menu focus regression assertions.

Verification: 1,502 managed match-rule checks passed. Current scripts plus both changed Unity regression runners compiled against the Unity 6000.3.6f1 references; player configuration also compiled. A serialized scene check resolved all local references across 348 unique objects and verified one standing sprite per side. These source/static checks do not establish live native gameplay, final camera framing, or two-client network behavior; the new Unity regression assertions have not been executed in a native editor. No second editor or project copy was launched.

### Executed input/scene coverage and safe editor follow-up

The production `BossPaddleInputPolicy` is now shared by camera-pointer calculation and both production/practice menu gates. `dotnet run --project Tools/BossFightTests/BossFightTests.csproj` executes 1,502 match-rule checks plus 2,556 input-policy/serialized-scene checks. Executed coverage includes participant/spectator ownership, active/ready gates, same menu/dialogue focus gate used by gameplay, pause/invalid time, both player halves and radius insets, bounded speed and near-target convergence, nonfinite inputs, and actual scene ID/artwork bindings. Exact stdout is in `Evidence/2026-10-10-input-policy/managed-results.txt`.

The existing native BossArenaValidation/BossIntegrationRunner entry points open scenes and enter Play mode, so they were not invoked against the user's open editor. There is no callable Unity test-runner/native UI bridge in this execution environment. `Assets/Editor/BossIsolatedEditValidation.cs` provides an opt-in guarded Edit-mode alternative following the existing InitializeOnLoad/delayCall tooling pattern. It creates only hidden owned objects in a disposable preview scene, checks actual Camera ray projection and Rigidbody practice serve/pause/restore state, closes/destroys its objects, and verifies user scene/dirty flags/selection are unchanged. It does not simulate physics or run live mouse/menu/spectator gameplay. It refuses Play mode, compilation and updating. A once-only Temp request is installed; it awaits the editor's natural import. The explicit menu item is `Tools/Haunted Hotel/Validate isolated boss input and practice`. Result path: `%TEMP%/HauntedFishBossIsolatedChecks-20261010.txt`. No result had appeared at the final check, so its native assertions remain unexecuted.

Menu integration contract: `HauntedHotelMultiplayer.SetMemberMenuOpen(bool)` sets `Session.SetInputFocused(open || _DeskInputFocused || IntroductionPlaying)`; `HotelPlayer.InputBlocked` reads that focus. Both boss paths call `BossPaddleInputPolicy.InputAllowed(local.InputBlocked, Time.timeScale)`. No new cross-owner API is introduced. Practice starts from the existing `GameEditorRoleSwitch` boss development button via `BossArenaCoordinator.EditorGoToBoss()` when no valid real fish/ghost pair is available; it starts `BossArenaManager.BeginEditorPractice()` immediately or after additive scene bind. Returning with the same button stops it. Real two-player admission stays on `TryEnter`/`BeginPending` and preempts practice.

## Current handoff: 2026-10-10 own-end presentation

Current production rules supersede older evidence below/above: fish scores three goals to win, ghost scores one to win. Managed regression run passed 1702 rule checks and 2798 input/serialized-scene/framing checks. Production source and both native authoring helpers compile against Unity 6000.3.6f1 assemblies. Earlier 17 isolated native checks cover physical practice/input gating, not the newly authored perspective presentation.

Scene integration is pending. Run Tools > Haunted Hotel > Author boss prefab from saved scene (isolated), then Integrate validated boss arena prefab, while BossFight is not loaded and Unity is idle. The helpers serialize through Unity, preserve active user scenes and dirty flags, and refuse stale saved-scene hashes. They produce request/result evidence in TEMP: HauntedFishBossPerspectiveAuthoring-20261010 and HauntedFishBossPrefabSceneIntegration-20261010. Both request files are installed; no result files yet. Do not claim new camera/HUD/storm gameplay or screenshots verified until these complete and the native play regressions run.

BossStormPresentation binds shared StormLightningAttic/StormAudioAttic prefabs and existing StartAmbient/StopAmbient/SetOpponentRenderer APIs. BossOpponentShadow only positions/scales the single template shadow and mirrors the shared room-light envelope; it adds no strike timer. It suspends active Game-scene storms during boss watch and restores them on exit. The Game scene owner should avoid restarting Game storm while the boss owns the watch view.

### Native integration completed
Unity completed 23 native prefab-authoring checks and 19 scene-integration checks. Assets/Resources/BossFight/BossFightArena.prefab is saved; BossFight.unity now contains its connected instance, with three perspective rigs and internal manager/view/HUD bindings. Reports are preserved beside managed-results.txt. Managed post-integration run passed1702 rule checks and3162 input/serialized-prefab/framing checks; parser accepts Unity-native stripped nested-prefab references. This supersedes the pending integration checkpoint above. New native gameplay/render/lifecycle testing remains pending.

Cleanup may archive the completed Assets/Editor/BossPrefabAuthoring.cs, BossPrefabSceneIntegration.cs and BossIsolatedEditValidation.cs, with paired .meta files; retain Tools/BossFightTests/BossArenaValidation.cs and Tools/BossIntegrationTests/BossIntegrationRunner.cs for pending native gameplay regression.

### Pending native presentation and restored Play session
Retain Assets/Editor/BossPresentationNativeValidation.cs and Assets/Editor/BossArenaValidation.cs (plus paired .meta) until their queued runs finish. The first loads saved prefab contents in a preview, checks native projections/pointer/visibility/life HUD/one shared storm lifecycle and renders landscape/portrait views for fish, ghost and spectator. It never saves the prefab or alters user scenes. It explicitly labels Edit-mode results separately from actual Play physics.

If the isolated run passes and the Play request exists, the second runner refuses any dirty scene, captures clean scene setup/selection, runs the physical boss scene in the connected editor, exits Play without closing Unity, and restores original scene setup/selection. It includes both end viewpoints/pointer checks and storm/audio shutdown on return; actual three/one goal triggers, reaction completion/cancellation and editor practice. End-to-end coordinator tests in Tools/BossIntegrationTests/BossIntegrationRunner.cs are not scheduled: that historical runner still requires safe connected-editor lifecycle adaptation before execution. No second editor was launched.

Queued result prefixes in TEMP: HauntedFishBossPresentationNative-20261010, HauntedFishBossArenaPlayNative-20261010. Evidence folders:2026-10-10-native-presentation and2026-10-10-native-gameplay. Latest checkpoint: source compiles; requests installed; native result/render files not yet available. Requires connected editor import/idle refresh.

### Native results inspected
The native isolated presentation result passed104 checks and six native rendered fish/ghost/spectator views (1280x720 and720x1280). Own character hidden, visible far opponent, both-end pointer direction, board/goal corner projection, life HUD, one attic strike/audio owner, shared shadow selection and direct StartAmbient/StopAmbient restoration passed. All six pixels inspected: contestant views show full playable board, local avatar hidden, far opponent. Fish opponent is cropped at top in ghost landscape; portrait hints wrap tightly. Spectator near fish is strongly foreshortened and overlaps near goal visually: viewport containment checks do not test occlusion; further spectator framing refinement is warranted.

Actual Play run passed32 initial checks including editor physical practice, both contestant ends/input, local visibility, spectator denial, and attic/audio stop on return. It timed out at stage1 waiting for normal serve; full physical win/loss/reaction suite therefore did NOT pass. See results.txt and partial-checks.txt in2026-10-10-native-gameplay. Scoped runner correction: standalone Boss scene lacks the production HauntedHotelMultiplayer bootstrap which sets Application.runInBackground=true, while project default is0. Test now sets it during armed Play and restores original value, with detailed timeout diagnostics. Background focus is the likely cause, not proven without rerun. No Play rerun or editor scene change initiated while Lobby owner is verifying.

### Spectator goal obstruction fix saved
BossCharacterPresentation.SetViewer now hides near fish artwork for Spectator as well as Fish view; far ghost remains visible. Ghost contestant view still displays fish opponent, Fish view still displays ghost opponent. Only authored artwork Renderer visibility changes: no actor/paddle/puck/goal GameObject, identity, collider or ownership is removed.

New native acceptance is queued under TEMP HauntedFishBossPresentationOcclusionNative-20261010. It compares landscape/portrait near-goal pixels with an all-artwork-hidden unobstructed baseline (RGB tolerance2, at most0.1percent differing ROI pixels), then deliberately enables the old near-fish artwork as a positive control that must create a detectable goal-region difference. Evidence will save both baselines and positive-control images in2026-10-10-native-presentation-occlusion. No Play chained from this read-only preview check.

Corrected standalone Play request uses TEMP HauntedFishBossArenaPlayNative-20261010-rerun, with an explicit .hold file while Lobby final refresh is pending. Runner refuses Play while .hold exists and requires passing new occlusion report plus clean user scenes. Root can release hold and invoke the menu only after Lobby verification is complete. Old failure evidence preserved. No current editor scene modified or Play transition initiated.

### Final explicit native acceptance:2026-10-11
Owned single-editor native batch passed110 isolated authored presentation/render/lifecycle checks and62 actual Play arena checks. Physical trigger fish3/ghost1, four exactly-once results, reactions/cancellation, practice serving, both-end pointer/visibility and storm/audio return passed. The corrected background-execution fixture resolves the prior stage1 timeout. Test-helper scene-state serialization was corrected to explicit DTO fields after an initial restoration failure; final native run verifies clean scene setup/selection and background setting restored. Normal startup remains manual-only; Resume functions only continue explicitly armed runs, batch bridge requires batchmode plus--boss-native-validation.

Spectator near-goal ROI changed0pixels relative to unobstructed baseline at both1280x720 and720x1280. Deliberately restoring old near fish changes6905 landscape pixels (tolerance10) and12487 portrait pixels (tolerance12): positive-control checks prove the old occlusion is detected. Updated spectator render inspected and near goal is clear. Evidence under2026-10-11-native-batch, including final-native.log, presentation-results.txt, physics-results.txt, preservation-results.txt. All31 saved Lobby/radio/grade/storm asset hashes unchanged. Batch completed successfully and no Unity editor remains; editor ownership is free for sequential jump validation.

Limitations: standalone native arena tests do not prove full network/coordinator Game transition/input-device integration, human audio audition or browser playback. Standalone Boss scene has no AudioListener because normal Game owns it; audible acceptance is not claimed. No production shared/Lobby/radio/grade files edited.

### Full-reference boss silhouette and dark void (2026-10-13)

- Visual direction follows the original `Assets/Art/Boss/BOSS-v2-angry-full.png` and `BOSS-v3-god-full.png`: wide, tall arm fans around the central head. Original raster art is unchanged.
- Fish camera pitch is 12 degrees. `RequiredOffsetDistance` fits the full playable board, goal/tray envelope and central head markers with a four-percent edge margin. Outer arm tips may crop. Ghost camera fitting is unchanged.
- The head's lower artwork edge is physically occluded by the original table geometry. No added wooden masking divider remains. Arms use the authored `BossArmVoidFade` material, with source-local root RGB/alpha feathering with the shared physical-art lighting path; no runtime materials or objects are created.
- Room-wall and existing floor renderers are disabled; the existing floor collider remains active. The unused `BossVoidBlack` material is retained as an authored asset. Camera backgrounds are black. Existing floor/wall/gameplay colliders and rigidbodies are unchanged. Superseded added backboard/ceiling geometry was removed.
- Head bob is 0.02 local units and its down-recoil is 0.02; this small presentation-only reduction keeps the eyes above the rail during reactions. Arm motion and gameplay are unchanged.
- An authored 85%-black, non-raycasting Lives backing has eight-pixel padding and draws beneath the existing label/icons, keeping moving pale arm tips from competing with the HUD.
- Obsolete editor-practice keyboard/control score copy is cleared; the internal standalone editor harness remains available.
- Run `bash Tools/BossFightTests/RunFramingMono.sh <Unity Editor/Data>`: 11,743 assertions exercise production camera math, seven authored target aspect ratios, random point sets, invalid inputs, bounds and minimal retreat. This does not launch Unity or validate GPU rendering.
- Scene previews reconstruct authored geometry, sprites, transforms, material/shader equations, camera fit and light settings in Blender CPU. URP shader import/GPU behavior, lighting response, TMP rasterization and native live animation remain unverified. Label the images source previews, never Unity screenshots. Prior divider/ceiling occlusion evidence is superseded by this void composition.
