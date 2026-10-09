# Haunted Fish Hotel code architecture

Keep game rules and operation state in ordinary C# objects. Unity components own scene references, input, rendering and physics; network adapters own Mirage and wire protocols. Dependencies flow from those adapters into the rules through explicit constructors and small contracts.

This direction follows Unity's [separation of data, logic and presentation](https://learn.unity.com/course/design-patterns/tutorial/build-a-modular-codebase-with-mvc-and-mvp-programming-patterns). The boundaries below are specific to this game. A new framework or dependency injection package is unnecessary for these constructors.

## Session ownership

`HauntedHotelMultiplayer` is the authored composition root. It retains all serialized dependency fields and public UI entry points, binds loaded scene services, and creates the following collaborators once in `Awake`.

| Owner | Responsibility | Dependencies |
| --- | --- | --- |
| `HotelSessionLifecycle` | Create, join, leave, admission, retries, cancellation and the active room heartbeat | Session ports and room DTOs; no Unity or Mirage types |
| `HotelNetworkSession` | Mirage peers, admission services and replicated character lifetime | Authored network dependencies; implements `IHotelSessionNetwork` |
| `UnityHotelSessionTransport` | Configurable socket preparation and relay endpoint | Current socket and directory URL |
| `UnityHotelSessionDirectory` | Typed room operations converted to JSON and HTTP | Directory client and current authorization token |
| `HotelHostMigration` | Prepared transport/lease and reliable handoff | Coroutine owner, network adapter and lifecycle |
| `IHotelScene` implementations | Scene entry, exit and spawn positions | Injected session facade |

`IHotelSessionRuntime` supplies coroutine scheduling and unscaled time. `IHotelSessionScene` supplies the scene and presentation effects used by connection policy. The component implements these ports explicitly, so the lifecycle can be exercised with a controlled clock and typed fake endpoints.

The lifecycle increments its operation version on cancellation. Late create/join/heartbeat callbacks cannot complete an old operation. Notifications and scene callbacks can synchronously cancel an operation, so the lifecycle checks its version again before performing the next effect. A connection becomes ready only after admission and a live local character. Closing deletes a room only when this client owns its directory key. The current room heartbeat and the prepared host heartbeat have separate owners. Disposal stops each owner's routines and detaches its events.

Keep room changes inside the lifecycle. Keep prepared socket adoption inside the migration adapter. UI components should call the facade's public methods; they should not call directory HTTP endpoints or construct network peers.

## Dialogue ownership

`DialogueStorySession` owns executable Ink progression, pending input lines and global-variable subscriptions. `DialogueManager` owns function bindings, input/UI lifecycle and shared-dialogue routing. Remote snapshots update presentation without creating an executable story session.

`Variables` synchronizes only variable names declared in the shared globals store. Story-specific variables retain their authored defaults. Starting and stopping a story subscription is idempotent; replacing or disabling a manager releases the old story and function bindings.

Each `Panel` subscribes to its own option instances. Retired options are detached and deactivated immediately before Unity's deferred destruction. Compatibility static events remain, but they are not a substitute for checking option ownership at a consumer.

The manager checks that a clicked row belongs to its current open panel before routing it. Choice-only Ink knots remain open until a choice is made. Disabling or destroying an open manager closes its input panel and emits one end event after presentation/shared state become inactive; cleanup bypasses RPC routing.

## Trap supply ownership

`ConveyorSchedule` owns delay, burst/gap timing, pause/stop suppression, seed-once decisions and spawn cadence. It returns a step describing the active interval and overdue spawn offsets. `WeightedSelection` maps a supplied random sample to a weighted family. Neither helper depends on Unity.

`TrapManager` owns authority/scope checks, catalogs, package identities and revisions, package objects, path updates and snapshot publication. It applies the schedule's decisions to the existing Unity objects. Unity supplies randomness only on the authority. Preserve move-before-spawn ordering, the 64-attempt backlog cap and monotonic IDs/revisions when changing this path.

## Player command ownership

`HotelPlayer` retains Mirage RPC signatures, ownership enforcement, SyncVars and placement replies. It forwards game requests through `IHotelGameCommands` and boss requests through `IHotelBossCommands`. It does not discover a scene singleton to execute those commands.

`GameSceneController` creates one `GamePlayerCommands` adapter per activation from its authored world and trap manager. It binds both existing and newly registered players and releases them on departure or scene exit. The adapter retains round/version validation, placement proximity checks and trap-input range checks; the world and supply manager retain authoritative simulation checks. Missing bindings reject placement with one negative reply and ignore commands without replies.

`BossArenaCoordinator` implements the boss endpoint and binds/unbinds it with its own enable/disable lifecycle. Player removal clears both endpoints. Unbind operations compare endpoint identity so an old owner cannot detach a newer binding. These are ordinary per-player references, not a service locator or global event bus. Existing `Current` properties remain for presentation/editor consumers; this change removes their use from player commands.

## Extending the project

For a new behavior, identify the state owner first. Put deterministic rules in a plain C# collaborator and inject the inputs and effects it actually needs. Keep physics queries and rendering in Unity adapters. Keep owned RPC signatures and authorization checks at the network boundary, then call a scoped gameplay operation. Subscribe and unsubscribe in the same owner; release a previous collaborator before replacing it.

Preserve MonoBehaviour class/file identities, script metadata GUIDs, serialized field names and public UnityEvent targets during refactors. Treat scene/prefab migration as a separate, verified change. Do not modify generated Unity `.csproj` files as the project definition.

The next useful boundaries are:

1. Separate round orchestration from `GameSceneController` presentation and movement constraints. Keep the new player command binding at scene entry/exit.
2. Extract placement snapshot validation and identity/revision rules from `GhostPlacementWorld`; retain actual colliders, sweep simulation and trap objects in the Unity adapter.
3. Inject actor/snapshot publication into trap systems. They still read `HotelPlayer.ActivePlayers` and write replicated avatar strings directly.
4. Narrow scene interfaces after those command and publication boundaries exist. Add assembly definitions only when dependencies are acyclic and serialized/build references can be validated.

Avoid converting every method to an interface, replacing all events with a global event bus, or moving runtime session state into shared ScriptableObject assets. Use authored data assets where shared editable configuration actually helps; keep per-session state owned by the session.

## Verification

The independent suites compile production source directly and use no added packages:

```powershell
dotnet run --project Tools/SessionLifecycleTests/SessionLifecycleTests.csproj
dotnet run --project Tools/DialogueTests/DialogueTests.csproj
dotnet run --project Tools/TrapSupplyTests/TrapSupplyTests.csproj
dotnet run --project Tools/MultiplayerTests/MultiplayerTests.csproj
dotnet run --project Tools/RoundIntroductionTests/RoundIntroductionTests.csproj
dotnet run --project Tools/BossFightTests/BossFightTests.csproj
```

The first three suites cover lifecycle/cancellation policy, actual vendored Ink progression and UI ownership, and conveyor timing/selection. `Tools/DialogueTests/RunUnity.ps1 -ValidationProject '<existing Temp project>'` exercises actual prefab options, text-input submission and Unity disable/destroy scheduling. Native compilation, Mirage weaving, authored gameplay references and real physics use the existing isolated Unity runners under `Tools/PerformanceTests` and `Tools/RoundIntroductionTests`. Their README files explain the disposable project requirements. Recorded results and the corresponding source hashes are under `Tools/ArchitectureTests/Evidence/2026-10-08`.

Deterministic endpoints and a solo host do not establish two-client delivery, latency behavior or reconnect/host handoff across real peers. Those remain separate integration checks.

The player-command refactor adds native host RPC routing assertions to `AuditGuardrails` and scene/player disable/re-enable and stale-owner assertions to `AuditLifecycleRunner`. Run the existing PerformanceTests runner in Host and Lifecycle modes against an idle disposable full project. Standalone suites do not compile `HotelPlayer` or prove Mirage weaving; native results are required for that boundary.
