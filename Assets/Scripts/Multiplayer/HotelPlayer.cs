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
        InputActionAsset ownedActions;
        InputActionMap lobbyMap,gameMap;
        InputAction lobbyMove;
        public InputAction GameMove {get;private set;}
        public InputAction GameJump {get;private set;}
        // The active map is exposed for validation of exclusive scene controls.
        public string ActiveInputMap => gameMap!=null&&gameMap.enabled?"Game":lobbyMap!=null&&lobbyMap.enabled?"Lobby":"";
        HotelSessionContext session;
        public void Configure(HotelSessionContext context) { session = context; }
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
        [SyncVar] Vector3 position;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Quaternion facing = Quaternion.identity;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] bool walking, talking;
        // Mirage serializes this authoritative server value to observers; clients use the received state for presentation.
        [SyncVar] Vector3 mouseLight;
        GameSideScrollMotor gameMotor;
        // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
        bool queuedJump;
        // The scene-travel loading gate keeps input from acting against a partially loaded scene.
        public bool ControlsReady => isActiveAndEnabled && gameObject.activeInHierarchy &&
            // Networked avatars must be spawned peers; a stopped identity is not an offline player.
            (!Networked || (Identity.IsSpawned && (IsServer || IsClient))) &&
            // Scene handoffs gate both input maps until the current authored scene is ready.
            (session == null ? !Networked : !session.Loading);
        // Only a ready server or explicitly offline avatar may run collision physics; clients keep their controllers disabled.
        public bool SimulationReady => ControlsReady && controller && controller.enabled && controller.gameObject.activeInHierarchy &&
            // A valid collision volume distinguishes an initialized native collider from a logically enabled detached controller.
            Time.frameCount>resumeMovementAfterFrame && controller.bounds.size.sqrMagnitude>0.000001f &&
            // The server also waits for this owner's readiness acknowledgement before moving its persistent character.
            (!Networked || (IsServer && (Identity.Owner==null || Identity.Owner.SceneIsReady)));
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool GameInputActive => IsRelevantPlayer && ControlsReady && gameMotor && gameMotor.Active;
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool LobbyInputActive => IsRelevantPlayer && ControlsReady && (!gameMotor || !gameMotor.Active);
        // This controller belongs to the authored player prefab; the server uses it to preserve the movement foundation's collision behavior.
        CharacterController controller;
        // Native collision state must settle after activation/teleport before either motor can move this controller.
        int resumeMovementAfterFrame;
        Vector2 input;
        // Expire movement intent after a short interval so connection loss cannot leave an avatar moving forever.
        float lastInput, nextSend, verticalSpeed;
        // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
        bool Connected => Networked && Identity.IsSpawned;
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        public bool IsRelevantPlayer => !Networked || (Identity.IsSpawned && IsLocalPlayer);
        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake()
        {
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            // InputActionAssets are data assets; cloning one does not create scene objects.
            if(InputActions) {
                ownedActions=Instantiate(InputActions);
                lobbyMap=ownedActions.FindActionMap("Lobby",true);gameMap=ownedActions.FindActionMap("Game",true);
                lobbyMove=lobbyMap.FindAction("Move",true);GameMove=gameMap.FindAction("Move",true);GameJump=gameMap.FindAction("Jump",true);
            }
            controller = GetComponent<CharacterController>();
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            gameMotor=GetComponent<GameSideScrollMotor>();
            // Report a missing authored dependency instead of adding objects or components at runtime.
            if (!gameMotor) Debug.LogError("The authored player prefab requires GameSideScrollMotor.",this);
            // Keep the established session or spawned network identity alive across connected Lobby/Game travel.
            DontDestroyOnLoad(transform.root.gameObject);
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (!Spin) Spin = transform;
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartServer.AddListener(() => { position=transform.position; facing=Spin.rotation; ResetSceneMotion();SettleController(); });
            // Stop events clear held movement and owned actions before Mirage releases or reuses the character identity.
            Identity.OnStopServer.AddListener(StopSimulation);
            // Client teardown must not promote a disabled observer controller to offline simulation.
            Identity.OnStopClient.AddListener(StopSimulation);
            // Keep this small operation on the existing component so callers share one state transition.
            Identity.OnStartClient.AddListener(() => {
                // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
                if (!IsServer) { controller.enabled = false; transform.position = position; Spin.rotation = facing; }
            });
        }
        // Retiring a network identity releases input and motion without re-enabling an intentionally disabled controller.
        void StopSimulation()
        {
            // Each avatar owns this private action asset, so stopping it cannot disable another player's actions.
            if(ownedActions)ownedActions.Disable();
            // Remove buffered axes/jumps/velocity before any later scene or spawn lifecycle resumes.
            ResetSceneMotion();walking=false;
        }
        // Subscribe while this existing component is active so presentation reacts to the current story or scene.
        void OnEnable()
        {
            // Reactivation does not grant authority or enable the controller; it only delays physics until native state settles.
            resumeMovementAfterFrame=Time.frameCount+1;
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent += Speaker;
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            DialogueManager.OnDialogueTryingToContinueEvent += StopTalking;
        }
        // Unsubscribe on deactivation to prevent stale listeners from acting on a later scene/session.
        void OnDisable()
        {
            // Disabled avatars release their private input maps immediately.
            if(ownedActions)ownedActions.Disable();
            // Apply the story system's existing authored effects through the selected private or shared authority path.
            StoryFunctions.OnSpeakerEvent -= Speaker;
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            DialogueManager.OnDialogueTryingToContinueEvent -= StopTalking;
        }
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        // Disposing only our private action asset leaves the authored shared asset untouched.
        void OnDestroy() {if(ownedActions)Destroy(ownedActions);}
        void Speaker(string speaker)
        {
            // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
            if (!IsRelevantPlayer) return;
            // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
            if (Connected) SetTalking(speaker == "player"); else talking = speaker == "player";
        }
        // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
        void StopTalking() { if (IsRelevantPlayer) { if (Connected) SetTalking(false); else talking = false; } }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] void SetTalking(bool value) { talking = value; }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        
        void Update()
        {
            // Only the local owner enables gameplay actions. Travel disables both maps until ready.
            bool lobbyEnabled=LobbyInputActive,gameEnabled=GameInputActive;
            // A stopped identity or loading scene cannot keep an old command alive until a future spawn.
            if(!ControlsReady){ResetSceneMotion();walking=false;}
            if(lobbyMap!=null) {if(lobbyEnabled)lobbyMap.Enable();else lobbyMap.Disable();}
            if(gameMap!=null) {if(gameEnabled)gameMap.Enable();else gameMap.Disable();}
            // Lobby controls are mutually exclusive with the side-scroll controls and belong only to the local character.
            if (LobbyInputActive)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                // Read the authored Lobby map; text focus and movement locks suppress its command.
                Vector2 value=CanMove&&ControlsReady&&!(session != null && session.InputFocused)&&lobbyMove!=null?lobbyMove.ReadValue<Vector2>():Vector2.zero;
                if (DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel) value = Vector2.zero;
                // Clamp the requested value to the allowed range before it reaches authoritative simulation or world-space presentation.
                value = Vector2.ClampMagnitude(value, 1);
                // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
                if (!Connected) { input = value; Simulate(Time.deltaTime); }
                // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                else if (Time.unscaledTime >= nextSend)
                {
                    // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                    nextSend = Time.unscaledTime + .05f;
                    // The owner sends bounded movement intent rather than a position, leaving collision and movement authority on the server.
                    SetInput(value);
                }
            }
            // Game controls run only for the owned character while the Game scene is active and ready.
            if (GameInputActive)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                bool canMove=CanMove && !(session != null && session.InputFocused) && !(DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel);
                // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
                gameMotor.ReadOwnedInput(canMove,out var horizontal,out var jump,out var mouse);
                // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
                queuedJump|=jump;
                // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
                if (!Connected) {gameMotor.Accept(horizontal,queuedJump,mouse);queuedJump=false;gameMotor.Simulate(Time.deltaTime);mouseLight=gameMotor.LampPosition;}
                // Send or accept the owner's horizontal, jump and mouse command; server validation decides whether it affects the game.
                else if (Time.unscaledTime>=nextSend) {nextSend=Time.unscaledTime+.05f;SetGameInput(horizontal,queuedJump,mouse);queuedJump=false;}
            }
            // Only the authoritative server changes shared simulation; remote clients consume the resulting state.
            if (Connected && IsServer)
            {
                // Use real session time so UI pauses or time-scale changes cannot defeat networking deadlines.
                if (Time.unscaledTime - lastInput > .3f || (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)) input = Vector2.zero;
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (gameMotor.Active)
                {
                    // Readiness prevents movement or visibility updates while that connection is loading a different scene.
                    if (!SimulationReady) gameMotor.ResetMotion();
                    // The light is a serialized child of the network player; only its mouse-derived world position is synchronized.
                    if(SimulationReady)gameMotor.Simulate(Time.deltaTime);mouseLight=gameMotor.LampPosition;walking=gameMotor.Walking;
                }
                // The scene-travel loading gate keeps input from acting against a partially loaded scene.
                else if (ControlsReady) Simulate(Time.deltaTime);
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                position = transform.position;
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                facing = Spin.rotation;
            }
            // Distinguish a spawned network identity from local/offline operation before choosing input and simulation behavior.
            else if (Connected)
            {
                // Interpolate toward the received authoritative position to hide network update steps on observing clients.
                transform.position = Vector3.Lerp(transform.position, position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                // Smooth visible facing between authoritative rotations without giving the observer simulation authority.
                Spin.rotation = Quaternion.Slerp(Spin.rotation, facing, 1 - Mathf.Exp(-18 * Time.deltaTime));
            }
            // The light is a serialized child of the network player; only its mouse-derived world position is synchronized.
            gameMotor.ShowLamp(mouseLight);
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (PlayerAnimator)
            {
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                PlayerAnimator.SetBool("walking", walking);
                // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                PlayerAnimator.SetBool("talking", talking);
            }
        }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] void SetInput(Vector2 value)
        {
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            if (!ControlsReady || gameMotor.Active || (Identity.Owner!=null && !Identity.Owner.SceneIsReady)) return;
            // Reject non-finite network values so malformed input cannot corrupt shared positions or physics.
            if (!float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y))
            // Clamp the requested value to the allowed range before it reaches authoritative simulation or world-space presentation.
            { input = Vector2.ClampMagnitude(value, 1); lastInput = Time.unscaledTime; }
        }
        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] public void SetGameInput(float horizontal,bool jump,Vector3 mouse)
        {
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            if (!ControlsReady || !gameMotor.Active || (Identity.Owner!=null && !Identity.Owner.SceneIsReady)) return;
            // Reject non-finite network values so malformed input cannot corrupt shared positions or physics.
            if (float.IsNaN(horizontal) || float.IsInfinity(horizontal) || float.IsNaN(mouse.x) || float.IsNaN(mouse.y) || float.IsInfinity(mouse.x) || float.IsInfinity(mouse.y)) return;
            // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
            gameMotor.Accept(horizontal,jump,mouse);
        }
        // Keep a one-frame jump press until the next network send so throttling cannot lose the button edge.
        public void ResetSceneMotion() {input=Vector2.zero;verticalSpeed=0;queuedJump=false;gameMotor.ResetMotion();}
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        public void Teleport(Vector3 point)
        {
            // Disable client collision simulation while interpolating server state; enable it only for authoritative movement.
            var enabled=controller.enabled;controller.enabled=false;transform.position=point;controller.enabled=enabled;
            // Treat the mouse as a world-plane point, clamp it on the server, and publish it for remote light presentation.
            position=point;ResetSceneMotion();mouseLight=point+Vector3.right;SettleController();
        }
        // Synchronize an existing enabled collider after an authorized teleport; intentional client/scene disables remain intact.
        void SettleController()
        {
            // Require a complete later frame even when Unity reports the component logically enabled immediately.
            resumeMovementAfterFrame=Time.frameCount+1;
            // Flush moved transforms into physics only at lifecycle boundaries, never enable a disabled controller here.
            if(controller && controller.enabled && controller.gameObject.activeInHierarchy)Physics.SyncTransforms();
        }
        // Both Lobby and Game pass through one immediate authority/native-state check before calling Unity's Move API.
        public bool TryMoveAuthoritatively(Vector3 displacement)
        {
            // Recheck at the actual Move boundary, after each motor has calculated its displacement.
            if(!SimulationReady)return false;
            // Reject collapsed transform scales; enabled colliders on zero-scale actors have no usable native capsule.
            var scale=controller.transform.lossyScale;
            // Preserve intentionally hidden/inactive actors rather than re-enabling or resizing them automatically.
            if(Mathf.Abs(scale.x)<.0001f || Mathf.Abs(scale.y)<.0001f || Mathf.Abs(scale.z)<.0001f)return false;
            // The sole Move call remains on an active, spawned authoritative collider with a valid volume.
            controller.Move(displacement);
            // Callers update walking/facing only when collision simulation actually ran.
            return true;
        }
        // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
        void Simulate(float delta)
        {
            // Disable client collision simulation while interpolating server state; enable it only for authoritative movement.
            if (!SimulationReady) { input=Vector2.zero;verticalSpeed=0;walking=false;return; }
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            // Use the movement foundation's gravity setting for authoritative vertical motion.
            verticalSpeed -= Gravity * delta;
            // Same side-scroll plane as the foundation, with a stable movement basis.
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            var direction = Vector3.forward * input.x + Vector3.right * -input.y;
            // Integrate authoritative velocity through Unity collision handling instead of trusting client-provided transforms.
            if(!TryMoveAuthoritatively((direction * (CanMove ? WalkingSpeed : 0) + Vector3.up * verticalSpeed) * delta))
            // A lifecycle change discards intent instead of carrying an old axis or falling velocity into reactivation.
            {input=Vector2.zero;verticalSpeed=0;walking=false;return;}
            // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
            walking = direction.sqrMagnitude > .001f && CanMove;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (walking)
                // Smooth visible facing between authoritative rotations without giving the observer simulation authority.
                Spin.rotation = Quaternion.Slerp(Spin.rotation,
                    // The owning client reads input, the server simulates the CharacterController, and SyncVars carry the resulting state to observers.
                    Quaternion.Euler(0, Mathf.Atan2(-input.y, input.x) * Mathf.Rad2Deg, 0), delta * SpinSpeed);
        }

        // Mirage weaves this call into an owner-authorized client-to-server message; the server still validates the requested values.
        [ServerRpc] public void RequestDialogue(int action, string story, Vector3 anchor, int choice, int revision, string inputKey, string inputValue)
        {
            // Require the currently displayed story revision so duplicated or stale clicks cannot advance the shared story twice.
            if (SharedDialogue.Instance) SharedDialogue.Instance.HandleRequest(this, action, story, anchor, choice, revision, inputKey, inputValue);
        }
    }
}


