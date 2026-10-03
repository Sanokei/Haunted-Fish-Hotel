# Multiplayer setup and verification

This integration targets Haunted Fish Hotel, Unity 6000.3.6f1, and Mirage v159.0.0.
The Fish-Out-Of-Water MovementSideScroll script is the movement reference.
Authored scene contents are preserved. Helper is first in the build scene list and
owns injected dialogue/input dependencies; Lobby owns multiplayer and gameplay.
Missing Helper references are filled from copies of the existing prefabs at runtime.
Authored references take precedence. Opening Lobby directly loads Helper when needed.

## Run locally or on a LAN
1. Restore packages in Unity. Packages/manifest.json pins Mirage's v159.0.0 Git tag
   and adds OpenUPM for UniTask. Package restore, actual Mirage compilation and
   Unity networking weaving have succeeded on this machine.
2. Start the directory from the project root:
   `python Tools/LobbyServer/lobby_server.py`
   For other computers on the same LAN, run with `--bind 0.0.0.0` and use the
   directory machine's LAN address in the game's directory URL.
3. Open Helper and press Play; its existing handoff enters Lobby. The runtime lobby panel supplies Create lobby,
   six-character code entry, Join lobby, and Leave. Run a second standalone build
   on the same machine or LAN and use the same directory URL.
4. Create a lobby. Its displayed code resolves to the advertised host address
   and UDP port 7777. The advertised address must be reachable by every client.
5. Join by code. Invalid, unknown/expired, full, unavailable, admission failure,
   connection timeout, and lost-host cases display a message. The server checks
   the room's invitation key and total capacity before spawning a player.
6. Move using WASD/arrows. Only the owning player reads input; bounded input goes
   to the server, which moves CharacterController and synchronizes position,
   facing, walking, and talking. Remote copies interpolate and disable their
   CharacterController. Idle input times out. Disconnect destroys the character.

The default runtime player is a visible capsule. A test ground is added only if
Lobby has no colliders. Assign HotelLobby.PlayerPrefab for the game's finished
player; include NetworkIdentity, CharacterController and HotelPlayer, assign Spin
and optionally the existing Animator. Register other spawnable prefabs in
AdditionalPrefabs. Runtime hashes identify the default player and dialogue service.

The directory has a 30-second lease, refreshed every 5 seconds; it removes rooms
after host crashes. It is an in-memory development service: restarting it expires
all codes. The host's owner credential is never returned by code lookup.
The Mirage server remains the final capacity/admission authority, including races
between multiple joins.

## Networked world objects
Add NetworkIdentity and NetworkWorldObject to a scene object or registered prefab.
Networked is a component-level setting, not a separate game mode.
Existing scripts that mutate it should check NetworkWorldObject.CanSimulate.
MovableCharacter now does that and uses network despawning where applicable.
Server-only systems spawn using ServerObjectManager.Spawn and destroy using its
Destroy method. Set Visual to a child object for synchronized visibility; do not
disable the entire network identity. Components without a spawned identity retain
local behavior.

## Existing Ink dialogue, shared or private
EnterDialogueMode and TriggerDialogue expose Audience:
- RelevantPlayer: existing local Ink story behavior, with world-space presentation.
  Starting/advancing it does not send a shared dialogue request or publish state.
- Everyone: an admitted player requests a scene-authored shared source. The server
  checks story catalog, authored source, interaction distance and revision, executes
  the existing DialogueManager, and publishes Ink state/text/choices/input/anchor.
  Shared continuation, choice, and text input go through the owned player's RPC.
  Concurrent requests with an old revision cannot skip the next line.
  Clients render server state without running Ink external functions again.
  Late joiners receive the current shared state.

Existing StoryFunctions speaker, camera, move, emoji and animation events are
forwarded only during shared dialogue. Replicated world-object movement runs on
the server; local-only cutscene objects can keep Networked disabled.
A shared conversation occupies the existing one-panel manager; starting another
private/shared conversation while it is active is refused. Multiple simultaneous
independent conversations would require additional panel instances.

WorldDialogueCanvas configures the existing dialogue/input Canvas as WorldSpace,
assigns Camera.main and GraphicRaycaster, ensures the Input System UI module,
uses configurable size/scale/offset, and follows the relevant speaker. Shared
player speech follows the same initiating network player on every client.
TriggerDialogue retains existing 2D hooks and also accepts 3D trigger volumes.
For clients whose CharacterController is disabled, the local player's pose is
checked against the authored volume. A visual cue is optional and does not disable
interaction. Actual authored trigger placement still needs Play-mode QA.

Choice instances inherit local UI coordinates/scale and rebuild their layout;
pointer clicks use the canvas event camera supplied by GraphicRaycaster.
No replacement dialogue system is introduced.

The Helper initializer supplies missing panel/input references and globals without
replacing authored managers. It retains those dependencies and the UI event system
when Helper hands off to Lobby. The existing completion counters accept all
completed dependencies, and the scene loader trims serialized destination quotes
and changes scene once. No authored Helper or Lobby scene file was overwritten. Use Tools > Haunted Fish Hotel > Configure World Dialogue to
prepare existing open-scene references without saving over a scene automatically.
Use Refresh Multiplayer Assets after adding/changing compiled Ink JSON so every
build uses the same catalog. Shared Ink ChangeScene still needs a game-specific
Mirage NetworkSceneManager flow; arbitrary local SceneManager.LoadScene is not a
supported synchronized scene transition.

## Lean GUI intro skeleton
IntroPanelAnimation uses the installed Lean GUI and Lean Transition APIs.
Assign Lightning (CanvasGroup), KnockPanel.Target, DeliveredItemPanel.Target,
LetterPanel.Target, SlidingItem, ForegroundGate.Target, HotelBackground.Target,
and optional ArrivalRoot/ArrivalWindow. Wire RevealLightning/HideLightning,
PushKnock/PushDeliveredItem/PushLetter, SlideItem, Dolly, Arrive and ResetPanels
through Lean GUI events. Inspector destinations, scales, durations and easing
remain assignable; durations default to zero and no automatic sequence is imposed.
The generic PushPanel binding is retained for existing Inspector hookups.
The three named panels and downward delivery slide follow the cloud worker's
storyboard inspection. Foreground gate and background hotel move independently;
arrival represents the lobby handoff. No character animations were invented.
Local storyboard materialization is blocked by the official Library transfer
helper using os.setxattr, which is unavailable on Windows. Cloud visual inspection
supplied the bindings; desktop artwork assignment and visual preview remain pending.

## Checks completed and still required
- 8 Python tests pass, including real HTTP create/resolve/close, six-character
  validation, concurrent unique codes, host authorization, full/reopen and expiry.
- Current game scripts compile against installed Unity, Input System, Ink, Lean
  and the actual Mirage 159 DLLs. Unity generated SyncVar serializers and player
  RPC methods; a development Windows player build succeeds.
- Separate real host/client processes pass lobby admission/spawning, owner-only
  input, server movement synchronized back to the client, stationary other player,
  disabled client CharacterController, and remote world-object authority checks.
- Private Ink dialogue remains absent on the host. Shared text/choices, duplicate
  continuation revision protection, world-space Canvas/event camera, selected
  branch, shared exit, and remote character cleanup pass runtime assertions.
- Disconnect cleanup is deferred until Mirage finishes its callbacks, preventing
  synchronous re-entry into ClientObjectManager cleanup. Rejoin receives the
  current shared input prompt without retaining stale players.
- The expanded regression uses the real authored Helper scene, starts there, and
  reaches Lobby through its existing handoff. Separate host/client players both
  exit 0 after rejoin, late-join shared input prompt, submission/continuation,
  server global update, host-loss cleanup, and joining a restarted host.
- Shared input UI snapshot callbacks never advance client Ink. A last line held
  behind InputText is displayed before ending, and disconnect closes shared UI.
  Preserve the project's format:name tag convention for text shown immediately
  after InputText; submission updates globals and the running Ink variable for
  later branches. Synchronous Ink interpolation evaluated before submission does
  not retroactively reformat an already generated line.
- Final runtime logs: Codex task workspace verification/runtime-results-1791045324/
  host.log and client.log; successful build log: verification/
  unity-runtime-build-remote-input.log. The verification project is isolated from
  the game's open Unity editor and uses test Lobby content plus the real Helper.
- Still require visual Play-mode QA of pointer raycasts and choice layout, finished
  player/intro artwork bindings, shared camera/move/emoji/animation cues, and
  testing under latency and on another LAN computer. Headless assertions do not
  prove visual placement or user-facing presentation.

## Internet hosting decision
Nothing was deployed or pushed. Mirage UDP does not supply room discovery,
encryption, relaying or NAT punching. This directory provides real code lookup
for a reachable host; it does not make residential hosts internet reachable.
Internet play still requires an agreed public lobby-directory host with HTTPS
and either reachable/forwarded game-server UDP or an approved relay/NAT-capable
socket (such as Steam/EOS) with its account setup. No paid service was selected.

