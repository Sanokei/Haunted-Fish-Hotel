using UnityEngine;
using UnityEngine.InputSystem;
namespace HauntedFish.Multiplayer
{
    // CharacterController movement retains the linked foundation's speed, gravity, spin and CanMove gate.
    // Only the server calls connected physics; observers receive positions and render the authored light.
    public sealed class GameSideScrollMotor : MonoBehaviour
    {
        // Twelve metres/second reaches the authored platform spacing under the foundation's gravity.
        public float JumpSpeed=12;
        // The marker exists only in the separately authored side-scroll scene, keeping Lobby controls distinct.
        public bool Active => GameSceneDefinition.Current;
        // The server stores the clamped owner-requested light point; HotelPlayer publishes it as a SyncVar.
        public Vector3 LampPosition { get; private set; }
        // Cache components already present on the authored network prefab.
        HotelPlayer _Player;
        // Client controllers are disabled by HotelPlayer; this reference is used only for server/offline physics.
        CharacterController _Controller;
        // Explicit prefab references prevent hidden runtime creation of the light or its visual marker.
        [SerializeField] Light _Lamp;
        // The small authored marker follows the same world point as its point light.
        [SerializeField] MeshRenderer _Marker;
        // Axis is bounded movement intent, vertical is server velocity, lastCommand expires stale input.
        float _Axis,_Vertical,_LastCommand;
        // Latch jump edges until simulation; wasActive detects entering/leaving Game for visual setup.
        bool _PendingJump,_WasActive;
        // Keep the last valid pointer-plane intersection if the current ray cannot hit that plane.
        Vector3 _LocalMouse;
        // Resolve the serialized prefab components before Mirage begins its spawn lifecycle.
        void Awake()
        {
            // Read existing components; missing authoring is an error, never an instruction to add objects.
            _Player=GetComponent<HotelPlayer>(); _Controller=GetComponent<CharacterController>();
            // Both references are assigned in HotelNetworkPlayer.prefab.
            if (!_Lamp || !_Marker) Debug.LogError("Assign the authored mouse-light child in the player prefab.",this);
            // Lobby starts with Game-only lighting hidden; the Game marker enables it after scene readiness.
            if (_Lamp) _Lamp.gameObject.SetActive(false);
        }
        // Clear old movement at scene handoffs without replacing the persistent network character.
        public void ResetMotion()
        {
            // A scene change must not retain a held axis or vertical velocity from its previous movement mode.
            _Axis=_Vertical=0;
            // Discard a jump pressed during loading rather than executing it in a new scene.
            _PendingJump=false;
            // Start a fresh stale-command deadline for this scene.
            _LastCommand=Time.unscaledTime;
            // Until a valid mouse ray exists, use a nearby point rather than an unrelated screen coordinate.
            _LocalMouse=transform.position+Vector3.right;
        }
        // Only HotelPlayer's local owner reads these inputs; remote characters never read local devices.
        public void ReadOwnedInput(bool canMove,out float horizontal,out bool jump,out Vector3 mouse)
        {
            // Explicit neutral commands stop movement while dialogue, text focus or CanMove blocks control.
            horizontal=0;jump=false;
            // The authored Game map supplies Vector2 movement; this XY motor uses its horizontal X component.
            if(canMove&&_Player.GameMove!=null)horizontal=_Player.GameMove.ReadValue<Vector2>().x;
            // Preserve a button edge for HotelPlayer to queue until the next throttled network command.
            jump=canMove&&_Player.GameJump!=null&&_Player.GameJump.WasPressedThisFrame();
            // The New Input System pointer uses the current authored Game camera, not a Lobby camera override.
            if (Mouse.current!=null && Camera.main)
            {
                // Convert the pointer pixel into a camera ray independently of the avatar's world position.
                var ray=Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                // Gameplay is on Z=0; intersect that plane instead of guessing a perspective depth.
                if (new Plane(Vector3.forward,Vector3.zero).Raycast(ray,out var distance)) _LocalMouse=ray.GetPoint(distance);
            }
            // This client clamp improves local intent, but the server clamps again before accepting it.
            mouse=GameSceneDefinition.Current.ClampMouse(_LocalMouse);
        }
        // ServerRpc validation in HotelPlayer reaches this method only for the owning ready connection.
        public void Accept(float horizontal,bool jump,Vector3 mouse)
        {
            // Disabled, loading or retired avatars discard commands rather than replaying them after reactivation.
            if(!Active || !_Player || !_Player.SimulationReady){ResetMotion();return;}
            // Bound horizontal intent so a peer cannot request a faster movement magnitude.
            _Axis=Mathf.Clamp(horizontal,-1,1);
            // Latch an edge until server physics consumes it, even if several commands arrive in one frame.
            _PendingJump|=jump;
            // Real-time expiry stops motion when an owner stops sending commands or loses its connection.
            _LastCommand=Time.unscaledTime;
            // Keep the authoritative mouse point inside this scene's authored bounds and fixed lighting plane.
            LampPosition=GameSceneDefinition.Current.ClampMouse(mouse);
        }
        // Connected HotelPlayer calls this only on the authoritative server, with Unity's frame delta.
        public void Simulate(float delta)
        {
            // enabled alone remains true on an inactive GameObject. Require the actual authoritative spawn/scene/controller lifecycle.
            if (!Active || !_Player || !_Player.SimulationReady) { ResetMotion();return; }
            // A missing command stream cannot leave movement or an old jump running indefinitely.
            if (Time.unscaledTime-_LastCommand>.3f) {_Axis=0;_PendingJump=false;}
            // Grounding determines whether the buffered jump may start; there is no extra airborne jump.
            if (_Controller.isGrounded)
            {
                // A small downward velocity keeps the controller in contact with the collision surface.
                if (_Vertical<0) _Vertical=-2;
                // Server-side CanMove still decides whether an owner-requested jump is allowed.
                if (_PendingJump && _Player.CanMove) _Vertical=JumpSpeed;
            }
            // Consume the jump edge once and integrate the foundation's gravity on the server.
            _PendingJump=false;_Vertical-=_Player.Gravity*delta;
            // Collision handling, horizontal speed and vertical motion remain server-owned.
            if(!_Player.TryMoveAuthoritatively(new Vector3(_Player.CanMove?_Axis*_Player.WalkingSpeed:0,_Vertical,0)*delta))
            // Stop a buffered command if the controller became unavailable while its movement was being calculated.
            {ResetMotion();return;}
            // Enforce the side-scroll plane after collision resolution rather than allowing depth drift.
            var point=transform.position;point.z=0;transform.position=point;
            // Rotate the authored Facing child only when a horizontal direction is actually requested.
            if (Mathf.Abs(_Axis)>.01f && _Player.Spin)
                // HotelPlayer synchronizes this resulting rotation for remote fish presentation.
                _Player.Spin.rotation=Quaternion.Slerp(_Player.Spin.rotation,Quaternion.Euler(0,_Axis>0?90:-90,0),_Player.SpinSpeed*delta);
        }
        // HotelPlayer's synchronized walking flag drives the hub controller on every observing peer.
        public bool Walking => _Player.SimulationReady && Mathf.Abs(_Axis)>.01f && _Player.CanMove;
        // Every peer displays the same SyncVar point; only the owning peer supplies the mouse input.
        public void ShowLamp(Vector3 worldPosition)
        {
            // Hide Game lighting in Lobby and during connected scene loads.
            bool active=Active && _Player.ControlsReady;
            // Toggle the authored child without constructing, destroying or replacing any light object.
            if (_Lamp.gameObject.activeSelf!=active) _Lamp.gameObject.SetActive(active);
            // Scene-entry setup runs only when Game is ready to display this existing character.
            if (active)
            {
                // Configure once per Game entry, preserving the light across network updates.
                if (!_WasActive)
                {
                    // Discard motion from Lobby before rendering the new side-scroll scene.
                    ResetMotion();
                    // Read optional materials from the authored Game scene rather than inventing renderer assets.
                    var definition=GameSceneDefinition.Current;
                    // An explicit scene material may replace the authored marker's existing material.
                    if (definition.LampMaterial) _Marker.sharedMaterial=definition.LampMaterial;
                    // A deterministic NetId hue gives this owner's light the same color on every peer.
                    _Lamp.color=Color.HSVToRGB((_Player.NetId*.23f)%1,.45f,1);
                    // The visual fish is an authored nested rig; an optional scene override targets that renderer.
                    var renderer=_Player.PlayerAnimator?_Player.PlayerAnimator.GetComponentInChildren<SkinnedMeshRenderer>():null;
                    // Preserve the original fish material unless a Game-specific replacement was explicitly assigned.
                    if (renderer && definition.PlayerMaterial) renderer.sharedMaterial=definition.PlayerMaterial;
                }
                // Apply the synchronized point in world space so it is independent of avatar movement.
                _Lamp.transform.position=worldPosition;
            }
            // Remember the entry state so leaving/re-entering Game restores the intended initialization behavior.
            _WasActive=active;
        }
    }
}
