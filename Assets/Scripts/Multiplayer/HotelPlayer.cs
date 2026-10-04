using Mirage;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HauntedFish.Multiplayer
{
    // Fish-Out-Of-Water MovementSideScroll foundation: CharacterController, two axes,
    // walking/spin speeds, gravity, CanMove, and speaker animation.
    // This controller belongs to the authored player prefab; the server uses it to preserve the movement foundation's collision behavior.
    [RequireComponent(typeof(NetworkIdentity), typeof(CharacterController))]
    // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
    public sealed class HotelPlayer : NetworkBehaviour
    {
        // This opt-in setting decides whether the object participates in shared network authority or existing local behavior.
        // A private clone prevents one avatar from enabling another avatar's action maps.
        public InputActionAsset InputActions;
        InputActionAsset _OwnedActions;
        InputActionMap _LobbyMap,_GameMap;
        InputAction _LobbyMove;
        public InputAction GameMove {get;private set;}
        public InputAction GameJump {get;private set;}
        // The active map is exposed for validation of exclusive scene controls.
        public string ActiveInputMap => _GameMap!=null&&_GameMap.enabled?"Game":_LobbyMap!=null&&_LobbyMap.enabled?"Lobby":"";
        HotelSessionContext _Session;
        public void Configure(HotelSessionContext context) { _Session = context; }
        public bool Networked = true;
        // Use the movement foundation's gravity setting for authoritative vertical motion.
        public float WalkingSpeed = 4.5f, SpinSpeed = 12f, Gravity = 25f;
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        public bool CanMove = true;
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        public Transform Spin;
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        public Animator PlayerAnimator;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Vector3 _Position;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Quaternion _Facing = Quaternion.identity;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] bool _Walking, _Talking;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Vector3 _MouseLight;
        GameSideScrollMotor _GameMotor;
        // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
        bool _QueuedJump;
        // The scene-travel loading gate keeps input from acting against a partially loaded scene.
        public bool ControlsReady => isActiveAndEnabled && gameObject.activeInHierarchy &&
            // Networked avatars must be spawned peers; a stopped identity is not an offline player.
            (!Networked || (Identity.IsSpawned && (IsServer || IsClient))) &&
            // Scene handoffs gate both input maps until the current authored scene is ready.
            (_Session == null ? !Networked : _Session.Connected && !_Session.Loading);
        // Only a ready server or explicitly offline avatar may run collision physics; clients keep their controllers disabled.
        public bool SimulationReady => ControlsReady && _Controller && _Controller.enabled && _Controller.gameObject.activeInHierarchy &&
            // A valid collision volume distinguishes an initialized native collider from a logically enabled detached controller.
            Time.frameCount>_ResumeMovementAfterFrame && _Controller.bounds.size.sqrMagnitude>0.000001f &&
            // The server also waits for this owner's readiness acknowledgement before moving its persistent character.
            (!Networked || (IsServer && (Identity.Owner==null || Identity.Owner.SceneIsReady)));
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool GameInputActive => IsRelevantPlayer && ControlsReady && _GameMotor && _GameMotor.Active;
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool LobbyInputActive => IsRelevantPlayer && ControlsReady && (!_GameMotor || !_GameMotor.Active);
        // This controller belongs to the authored player prefab; the server uses it to preserve the movement foundation's collision behavior.
        CharacterController _Controller;
        // Native collision state must settle after activation/teleport before either motor can move this controller.
        int _ResumeMovementAfterFrame;
        Vector2 _Input;
        // Expire movement intent after a short interval so connection loss cannot leave an avatar moving forever.
        float _LastInput, _NextSend, _VerticalSpeed;
        // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
        bool _Connected => Networked && Identity.IsSpawned;
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool IsRelevantPlayer => !Networked || (Identity.IsSpawned && IsLocalPlayer);
        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake()
        {
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            // InputActionAssets are data assets; cloning one does not create scene objects.
            if(InputActions) {
                _OwnedActions=Instantiate(InputActions);
                _LobbyMap=_OwnedActions.FindActionMap("Lobby",true);_GameMap=_OwnedActions.FindActionMap("Game",true);
                _LobbyMove=_LobbyMap.FindAction("Move",true);GameMove=_GameMap.FindAction("Move",true);GameJump=_GameMap.FindAction("Jump",true);
            }
            _Controller = GetComponent<CharacterController>();
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            _GameMotor=GetComponent<GameSideScrollMotor>();
            // Report a missing authored dependency instead of adding objects or components at runtime.
            if (!_GameMotor) Debug.LogError("The authored player prefab requires GameSideScrollMotor.",this);
            // Keep the established session or spawned network identity alive across connected Lobby/Game travel.
            DontDestroyOnLoad(transform.root.gameObject);
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (!Spin) Spin = transform;
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartServer.AddListener(() => { _Position=transform.position; _Facing=Spin.rotation; ResetSceneMotion();SettleController(); });
            // Stop events clear held movement and owned actions before Mirage releases or reuses the character identity.
            Identity.OnStopServer.AddListener(StopSimulation);
            // Client teardown must not promote a disabled observer controller to offline simulation.
            Identity.OnStopClient.AddListener(StopSimulation);
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartClient.AddListener(() => {
                // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
                if (!IsServer) { _Controller.enabled = false; transform.position = _Position; Spin.rotation = _Facing; }
            });
        }
        // Retiring a network identity releases input and motion without re-enabling an intentionally disabled controller.
        void StopSimulation()
        {
            // Each avatar owns this private action asset, so stopping it cannot disable another player's actions.
            if(_OwnedActions)_OwnedActions.Disable();
            // Remove buffered axes/jumps/velocity before any later scene or spawn lifecycle resumes.
            ResetSceneMotion();_Walking=false;
        }
        // Subscribe while this existing component is active so presentation reacts to the current story or scene.
        void OnEnable()
        {
            // Reactivation does not grant authority or enable the controller; it only delays physics until native state settles.
            _ResumeMovementAfterFrame=Time.frameCount+1;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent += Speaker;
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            DialogueManager.OnDialogueTryingToContinueEvent += StopTalking;
        }
        // Unsubscribe on deactivation to prevent stale listeners from acting on a later scene/session.
        void OnDisable()
        {
            // Disabled avatars release their private input maps immediately.
            if(_OwnedActions)_OwnedActions.Disable();
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent -= Speaker;
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            DialogueManager.OnDialogueTryingToContinueEvent -= StopTalking;
        }
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        // Disposing only our private action asset leaves the authored shared asset untouched.
        void OnDestroy() {if(_OwnedActions)Destroy(_OwnedActions);}
        void Speaker(string speaker)
        {
            // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
            if (!IsRelevantPlayer) return;
            // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
            if (_Connected) SetTalking(speaker == "player"); else _Talking = speaker == "player";
        }
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        void StopTalking() { if (IsRelevantPlayer) { if (_Connected) SetTalking(false); else _Talking = false; } }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] void SetTalking(bool value) { _Talking = value; }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        
        void Update()
        {
            // Only the local owner enables gameplay actions. Travel disables both maps until ready.
            bool lobbyEnabled=LobbyInputActive,gameEnabled=GameInputActive;
            // A stopped identity or loading scene cannot keep an old command alive until a future spawn.
            if(!ControlsReady){ResetSceneMotion();_Walking=false;}
            if(_LobbyMap!=null) {if(lobbyEnabled)_LobbyMap.Enable();else _LobbyMap.Disable();}
            if(_GameMap!=null) {if(gameEnabled)_GameMap.Enable();else _GameMap.Disable();}
            // Lobby controls are mutually exclusive with the side-scroll controls and belong only to the local character.
            if (LobbyInputActive)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                // Read the authored Lobby map; text focus and movement locks suppress its command.
                Vector2 value=CanMove&&ControlsReady&&!(_Session != null && _Session.InputFocused)&&_LobbyMove!=null?_LobbyMove.ReadValue<Vector2>():Vector2.zero;
                if (DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel) value = Vector2.zero;
                // Clamp the requested value to the allowed range before it reaches authoritative simulation or world-space presentation.
                value = Vector2.ClampMagnitude(value, 1);
                // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
                if (!_Connected) { _Input = value; Simulate(Time.deltaTime); }
                // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                else if (Time.unscaledTime >= _NextSend)
                {
                    // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                    _NextSend = Time.unscaledTime + .05f;
                    // The owner sends bounded movement intent rather than a position, leaving collision and movement authority on the server.
                    SetInput(value);
                }
            }
            // Game controls run only for the owned character while the Game scene is active and ready.
            if (GameInputActive)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                bool canMove=CanMove && !(_Session != null && _Session.InputFocused) && !(DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel);
                // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
                _GameMotor.ReadOwnedInput(canMove,out var horizontal,out var jump,out var mouse);
                // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
                _QueuedJump|=jump;
                // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
                if (!_Connected) {_GameMotor.Accept(horizontal,_QueuedJump,mouse);_QueuedJump=false;_GameMotor.Simulate(Time.deltaTime);_MouseLight=_GameMotor.LampPosition;}
                // Send or accept the owner's horizontal, jump and mouse command; server validation decides whether it affects the game.
                else if (Time.unscaledTime>=_NextSend) {_NextSend=Time.unscaledTime+.05f;SetGameInput(horizontal,_QueuedJump,mouse);_QueuedJump=false;}
            }
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (_Connected && IsServer)
            {
                // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                if (Time.unscaledTime - _LastInput > .3f || (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)) _Input = Vector2.zero;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (_GameMotor.Active)
                {
                    // Readiness prevents movement or visibility updates while that connection is loading a different scene.
                    if (!SimulationReady) _GameMotor.ResetMotion();
                    // The light is a serialized child of the network player; only its mouse-derived world position is synchronized.
                    if(SimulationReady)_GameMotor.Simulate(Time.deltaTime);_MouseLight=_GameMotor.LampPosition;_Walking=_GameMotor.Walking;
                }
                // The scene-travel loading gate keeps input from acting against a partially loaded scene.
                else if (ControlsReady) Simulate(Time.deltaTime);
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                _Position = transform.position;
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                _Facing = Spin.rotation;
            }
            // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
            else if (_Connected)
            {
                // Interpolate toward the received authoritative position to hide network update steps on observing clients.
                transform.position = Vector3.Lerp(transform.position, _Position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                // Smooth visible facing between authoritative rotations without giving the observer simulation authority.
                Spin.rotation = Quaternion.Slerp(Spin.rotation, _Facing, 1 - Mathf.Exp(-18 * Time.deltaTime));
            }
            // The light is a serialized child of the network player; only its mouse-derived world position is synchronized.
            _GameMotor.ShowLamp(_MouseLight);
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (PlayerAnimator)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                PlayerAnimator.SetBool("walking", _Walking);
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                PlayerAnimator.SetBool("talking", _Talking);
            }
        }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] void SetInput(Vector2 value)
        {
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            if (!ControlsReady || _GameMotor.Active || (Identity.Owner!=null && !Identity.Owner.SceneIsReady)) return;
            // Reject non-finite network values so malformed input cannot corrupt shared positions or physics.
            if (!float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y))
            // Clamp the requested value to the allowed range before it reaches authoritative simulation or world-space presentation.
            { _Input = Vector2.ClampMagnitude(value, 1); _LastInput = Time.unscaledTime; }
        }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] public void SetGameInput(float horizontal,bool jump,Vector3 mouse)
        {
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            if (!ControlsReady || !_GameMotor.Active || (Identity.Owner!=null && !Identity.Owner.SceneIsReady)) return;
            // Reject non-finite network values so malformed input cannot corrupt shared positions or physics.
            if (float.IsNaN(horizontal) || float.IsInfinity(horizontal) || float.IsNaN(mouse.x) || float.IsNaN(mouse.y) || float.IsInfinity(mouse.x) || float.IsInfinity(mouse.y)) return;
            // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
            _GameMotor.Accept(horizontal,jump,mouse);
        }
        // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
        public void ResetSceneMotion() {_Input=Vector2.zero;_VerticalSpeed=0;_QueuedJump=false;_GameMotor.ResetMotion();}
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        public void Teleport(Vector3 point)
        {
            // Disable client collision simulation while interpolating server state; enable it only for authoritative movement.
            var enabled=_Controller.enabled;_Controller.enabled=false;transform.position=point;_Controller.enabled=enabled;
            // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
            _Position=point;ResetSceneMotion();_MouseLight=point+Vector3.right;SettleController();
        }
        // Synchronize an existing enabled collider after an authorized teleport; intentional client/scene disables remain intact.
        void SettleController()
        {
            // Require a complete later frame even when Unity reports the component logically enabled immediately.
            _ResumeMovementAfterFrame=Time.frameCount+1;
            // Flush moved transforms into physics only at lifecycle boundaries, never enable a disabled controller here.
            if(_Controller && _Controller.enabled && _Controller.gameObject.activeInHierarchy)Physics.SyncTransforms();
        }
        // Both Lobby and Game pass through one immediate authority/native-state check before calling Unity's Move API.
        public bool TryMoveAuthoritatively(Vector3 displacement)
        {
            // Recheck at the actual Move boundary, after each motor has calculated its displacement.
            if(!SimulationReady)return false;
            // Reject collapsed transform scales; enabled colliders on zero-scale actors have no usable native capsule.
            var scale=_Controller.transform.lossyScale;
            // Preserve intentionally hidden/inactive actors rather than re-enabling or resizing them automatically.
            if(Mathf.Abs(scale.x)<.0001f || Mathf.Abs(scale.y)<.0001f || Mathf.Abs(scale.z)<.0001f)return false;
            // The sole Move call remains on an active, spawned authoritative collider with a valid volume.
            _Controller.Move(displacement);
            // Callers update walking/facing only when collision simulation actually ran.
            return true;
        }
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        void Simulate(float delta)
        {
            // Disable client collision simulation while interpolating server state; enable it only for authoritative movement.
            if (!SimulationReady) { _Input=Vector2.zero;_VerticalSpeed=0;_Walking=false;return; }
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (_Controller.isGrounded && _VerticalSpeed < 0) _VerticalSpeed = -2;
            // Use the movement foundation's gravity setting for authoritative vertical motion.
            _VerticalSpeed -= Gravity * delta;
            // Same side-scroll plane as the foundation, with a stable movement basis.
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            var direction = Vector3.forward * _Input.x + Vector3.right * -_Input.y;
            // Integrate authoritative velocity through Unity collision handling instead of trusting client-provided transforms.
            if(!TryMoveAuthoritatively((direction * (CanMove ? WalkingSpeed : 0) + Vector3.up * _VerticalSpeed) * delta))
            // A lifecycle change discards intent instead of carrying an old axis or falling velocity into reactivation.
            {_Input=Vector2.zero;_VerticalSpeed=0;_Walking=false;return;}
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            _Walking = direction.sqrMagnitude > .001f && CanMove;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (_Walking)
                // Smooth visible facing between authoritative rotations without giving the observer simulation authority.
                Spin.rotation = Quaternion.Slerp(Spin.rotation,
                    // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                    Quaternion.Euler(0, Mathf.Atan2(-_Input.y, _Input.x) * Mathf.Rad2Deg, 0), delta * SpinSpeed);
        }

        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] public void RequestDialogue(int action, string story, Vector3 anchor, int choice, int revision, string inputKey, string inputValue)
        {
            // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
            if (SharedDialogue.Instance) SharedDialogue.Instance.HandleRequest(this, action, story, anchor, choice, revision, inputKey, inputValue);
        }
    }
}


