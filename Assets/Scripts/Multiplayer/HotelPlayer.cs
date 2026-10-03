using Mirage;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
namespace HauntedFish.Multiplayer
{
    // Fish-Out-Of-Water MovementSideScroll foundation: CharacterController, two axes,
    // walking/spin speeds, gravity, CanMove, and speaker animation.
    [RequireComponent(typeof(NetworkIdentity), typeof(CharacterController))]
    public sealed class HotelPlayer : NetworkBehaviour
    {
        public bool Networked = true;
        public float WalkingSpeed = 4.5f, SpinSpeed = 12f, Gravity = 25f;
        public bool CanMove = true;
        public Transform Spin;
        public Animator PlayerAnimator;
        [SyncVar] Vector3 position;
        [SyncVar] Quaternion facing = Quaternion.identity;
        [SyncVar] bool walking, talking;
        CharacterController controller;
        Vector2 input;
        float lastInput, nextSend, verticalSpeed;
        bool Connected => Networked && Identity.IsSpawned;
        public bool IsRelevantPlayer => !Connected || IsLocalPlayer;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!Spin) Spin = transform;
            Identity.OnStartServer.AddListener(() => { position=transform.position; facing=Spin.rotation; });
            Identity.OnStartClient.AddListener(() => {
                if (!IsServer) { controller.enabled = false; transform.position = position; Spin.rotation = facing; }
            });
        }
        void OnEnable()
        {
            StoryFunctions.OnSpeakerEvent += Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent += StopTalking;
        }
        void OnDisable()
        {
            StoryFunctions.OnSpeakerEvent -= Speaker;
            DialogueManager.OnDialogueTryingToContinueEvent -= StopTalking;
        }
        void Speaker(string speaker)
        {
            if (!IsRelevantPlayer) return;
            if (Connected) SetTalking(speaker == "player"); else talking = speaker == "player";
        }
        void StopTalking() { if (IsRelevantPlayer) { if (Connected) SetTalking(false); else talking = false; } }
        [ServerRpc] void SetTalking(bool value) { talking = value; }
        void Update()
        {
            if (IsRelevantPlayer)
            {
                var keyboard = Keyboard.current;
                Vector2 value = Vector2.zero;
                if (CanMove && keyboard != null && !HotelLobby.InputFocused)
                {
                    value.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                              (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                    value.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                              (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                }
                if (DialogueManager.Instance && DialogueManager.Instance.ActiveDialoguePanel) value = Vector2.zero;
                value = Vector2.ClampMagnitude(value, 1);
                if (!Connected) { input = value; Simulate(Time.deltaTime); }
                else if (Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + .05f;
                    SetInput(value);
                }
            }
            if (Connected && IsServer)
            {
                if (Time.unscaledTime - lastInput > .3f || (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue)) input = Vector2.zero;
                Simulate(Time.deltaTime);
                position = transform.position;
                facing = Spin.rotation;
            }
            else if (Connected)
            {
                transform.position = Vector3.Lerp(transform.position, position, 1 - Mathf.Exp(-18 * Time.deltaTime));
                Spin.rotation = Quaternion.Slerp(Spin.rotation, facing, 1 - Mathf.Exp(-18 * Time.deltaTime));
            }
            if (PlayerAnimator)
            {
                PlayerAnimator.SetBool("walking", walking);
                PlayerAnimator.SetBool("talking", talking);
            }
        }
        [ServerRpc] void SetInput(Vector2 value)
        {
            if (!float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y))
            { input = Vector2.ClampMagnitude(value, 1); lastInput = Time.unscaledTime; }
        }
        void Simulate(float delta)
        {
            if (!controller.enabled) return;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed -= Gravity * delta;
            // Same side-scroll plane as the foundation, with a stable movement basis.
            var direction = Vector3.forward * input.x + Vector3.right * -input.y;
            controller.Move((direction * (CanMove ? WalkingSpeed : 0) + Vector3.up * verticalSpeed) * delta);
            walking = direction.sqrMagnitude > .001f && CanMove;
            if (walking)
                Spin.rotation = Quaternion.Slerp(Spin.rotation,
                    Quaternion.Euler(0, Mathf.Atan2(-input.y, input.x) * Mathf.Rad2Deg, 0), delta * SpinSpeed);
        }
        void LateUpdate()
        {
            if (!IsRelevantPlayer || !Camera.main) return;
            var camera = Camera.main.transform;
            camera.position = transform.position + new Vector3(8, 8, -10);
            camera.LookAt(transform.position + Vector3.up);
        }
        [ServerRpc] public void RequestDialogue(int action, string story, Vector3 anchor, int choice, int revision, string inputKey, string inputValue)
        {
            if (SharedDialogue.Instance) SharedDialogue.Instance.HandleRequest(this, action, story, anchor, choice, revision, inputKey, inputValue);
        }
    }
}


