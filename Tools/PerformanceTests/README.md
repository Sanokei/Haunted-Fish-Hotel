# Unity code audit verification

Saved changes provide scene-authored camera output bindings, lifecycle-managed actor registration including Mirage unspawn/start, roster sorting on membership changes, cached trap definitions/components, change-only HUD captions and package highlights, complete reusable physics query buffers, and transition-based UI ownership. Remaining discovery is tied to initialization, scene events, clicks or actual collision hits; event-driven catalog/snapshot LINQ remains.

Installed versions verified locally: Unity 6000.3.6f1, Mirage 159.0.0, Cinemachine 3.1.7, Input System 1.18.0. No dependencies were installed/upgraded. Unity 6.3 [documents Camera.main](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera-main.html) as a lookup in a cached set of tagged objects, with small overhead comparable to GetComponent. It is not literally GetComponent or a full scene search. No measured frame-time improvement is claimed.

Run against an existing disposable production-project copy under the personal Temp directory with cached packages:

```powershell
./Tools/PerformanceTests/Run.ps1 -ValidationProject '<existing Temp project>' -Mode Host
./Tools/PerformanceTests/Run.ps1 -ValidationProject '<existing Temp project>' -Mode Direct
./Tools/PerformanceTests/Run.ps1 -ValidationProject '<existing Temp project>' -Mode Lifecycle
./Tools/PerformanceTests/Run.ps1 -ValidationProject '<existing Temp project>' -Mode Lobby
```

The runner rejects non-Temp destinations and copies already open in an Editor. It copies current scripts, test observers, changed scenes/prefab into the disposable copy and launches a hidden Editor. The copy must already contain production Resources, art, Packages and ProjectSettings; the runner neither creates a new project nor installs dependencies. Test-created objects exist only in test fixtures. Production camera bindings/cutscene/trap assets are serialized assets. Original Editor PID 13188 was preserved.

Evidence: [Evidence/2026-10-07](Evidence/2026-10-07), including logs, rendered frames, exact source hashes and verification.json.

| Verification | Result |
| --- | --- |
| Full production Unity compile and Mirage weaving | Passed in final isolated runs |
| Actual solo-host Helper to Lobby to Game flow | 74 gameplay checks passed |
| Final host camera/UI/authority/catalog/reset/grid guardrails | 37 checks passed |
| Direct authored Game entry, earlier audit checkpoint | 77 gameplay / 27 audit checks passed |
| Actual preview unload/reload/unload, real CharacterControllers, occupied reset and registry teardown | 14 checks passed |
| Actual authored Lobby loading/Ink skip, normal completion, connection ordering, retry, interruption and raycasts | 65 checks passed |
| Standalone round barrier / compiled Ink | 2,098 checks passed |
| Existing multiplayer rules | 127 checks passed |
| Visual review | Actual Game attic ghost, downward-return blur and two independent carts rendered and inspected |

The direct checkpoint predates the final disabled-world request guard. Final host/lifecycle logs compile current production source. No failing production test remains in recorded final runs. Fresh independent review is pending parent relay.

Allocation measurement is INCONCLUSIVE. GC.GetAllocatedBytesForCurrentThread returned zero even for a known-allocation positive control with 1,000 retained 1 KiB byte arrays. Profiler.GetMonoUsedSizeLong was noisy, including negative deltas across collections. These zero/heap readings do not establish allocation-free behavior. No allocation savings, GC reduction or speedup is claimed. A target-build Profiler capture remains necessary to quantify effect.

Remote authority was tested with a real Mirage server-spawned nonlocal ghost: finite/scoped axes, clamping, bounded integration, forged package coordinates, and unspawn of a still-active avatar. This is not a second admitted client. Two admitted clients, remote reconnect and latency reconciliation remain unverified after an earlier real second-client admission timeout. Server settings/deployment were untouched. Native Lobby visual confirmation remains unverified: batch native screenshots did not appear and a hidden GUI attempt stalled; converted-canvas images are not presented as native proof. Lobby lifecycle/raycast tests passed.

The requested unslop skill was absent from checkout/user locations and the skill catalog; the audit continued with current source inspection and independent read-only reviews. Intermittent desktop execution disconnects recovered and files were inspected before mutations were retried.

| Reviewer finding | Disposition and evidence |
| --- | --- |
| Offline preview leaks across travel | Network avatars persist; offline preview stays scene-local. Actual unload destroys preview/actions/registry entry and repeated load creates exactly one. |
| Cart sweeps/pushes penetrate fish or carts | Swept clipping, full leading face, complete reusable buffers, controller preflight, leading-first pushes and actual applied displacement constrain motion. Real pinned/two-fish/large-step and adjacent-cart escape tests pass. |
| World disable strands ownership or reuses IDs | Disable publishes a higher empty revision and releases ownership; same-round identity/counters remain monotonic. Delayed IDs/snapshots and fresh disabled-world requests reject. |
| Same-family replacement changes live inventory | Definitions/artwork are retained for live held/placed families throughout the round. Observer reconstruction retains the old prefab; next round adopts replacement. |
| Paused shortened conveyor emits invalid distances | Reconcile path edits independently of run/pause state; final snapshot accepts as replica. |
| Reset teleports into occupied origin | Exclude own colliders and check other colliders plus actual fish bounds. Fish and another physical object's occupied origins reject. |
| Client coordinates establish interaction authority | Remote sends finite scoped bounded flight axes; server integrates flight and validates proximity using that position. Forged/stale requests reject in installed Mirage fixture. |
| Invalid grid step hangs generation | Finite positive settings and bounded integer line counts; zero, negative, NaN, infinity and tiny spacing terminate with bounded mesh. |
| Unused setup APIs and extension candidates | Removed verified no-op world Configure and unused art plumbing. IGhostTrap contract preserved; no broader interface redesign. Supplied assets use Slide/Movement, Pulse/MouseClick, Fall/Space. |

The chandelier armed height/fall distance changed from 3.4 to 2.5 units after a randomized real conveyor run exposed intersection with the actual hallway ceiling. The wheel still uses Unity Random.Range; the old static source assertion was updated to the list roster's Count property.

Feature paths retained: Assets/Resources/AtticRoundCutscene.prefab; Assets/Dialogue/GhostRoundIntroduction.ink and .json; actual art Assets/Art/attic.png and Assets/Art/ghost.png. Output bindings are authored on existing camera objects in Assets/Scenes/Game.unity and Assets/Scenes/Lobby.unity. Existing automatic scene flow exercises these hookups; no extra Inspector hookup is outstanding.

Exact audit changes, excluding unrelated user changes and earlier feature work:

- Assets/Scenes/Game.unity
- Assets/Scenes/Lobby.unity
- Assets/Resources/Traps/FallingChandelierTrap.prefab
- Assets/Scripts/Multiplayer/HotelViewCamera.cs.meta
- Tools/RoundIntroductionTests/Program.cs
- Tools/RoundIntroductionTests/ActualGameFlowRunner.cs
- Assets/Scripts/Multiplayer/GameCameraOwner.cs
- Assets/Scripts/Multiplayer/GameEditorPreview.cs
- Assets/Scripts/Multiplayer/GameSceneController.cs
- Assets/Scripts/Multiplayer/GameSideScrollMotor.cs
- Assets/Scripts/Multiplayer/GameUIEventOwnership.cs
- Assets/Scripts/Multiplayer/GhostPlacementController.cs
- Assets/Scripts/Multiplayer/GhostPlacementGrid.cs
- Assets/Scripts/Multiplayer/GhostPlacementWorld.cs
- Assets/Scripts/Multiplayer/GhostPossessionEffects.cs
- Assets/Scripts/Multiplayer/GhostTentaclePresentation.cs
- Assets/Scripts/Multiplayer/GhostTrap.cs
- Assets/Scripts/Multiplayer/GhostTrapAreaPresentation.cs
- Assets/Scripts/Multiplayer/HotelFishSprite.cs
- Assets/Scripts/Multiplayer/HotelPlayer.cs
- Assets/Scripts/Multiplayer/HotelPlayerMovement.cs
- Assets/Scripts/Multiplayer/HotelViewCamera.cs
- Assets/Scripts/Multiplayer/PossessionSmoke.cs
- Assets/Scripts/Multiplayer/TrapManager.cs
- Assets/Scripts/Multiplayer/Dialogue/WorldDialogueCanvas.cs
- Tools/PerformanceTests/ActualGameSceneValidation.cs
- Tools/PerformanceTests/AuditGuardrails.cs
- Tools/PerformanceTests/AuditLifecycleRunner.cs
- Tools/PerformanceTests/Run.ps1
- Tools/PerformanceTests/README.md
- Tools/PerformanceTests/Evidence/2026-10-07/*
