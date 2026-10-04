using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Monologue.Dialogue;
using Monologue.Character;
using System.Linq;
namespace HauntedFish.Multiplayer
{
    // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
    [RequireComponent(typeof(Canvas))]
    // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
    public sealed class WorldDialogueCanvas : MonoBehaviour
    {
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public Vector3 Offset = new Vector3(3.3f,4.2f,0);
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public Vector2 Size = new Vector2(900,440);
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public float WorldScale = .009f;
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public bool AdaptLegacyLayout = true;
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public Transform Follow;
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public bool FaceCamera = true;
        Vector3 _Anchor;
        Canvas _Canvas;
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public static WorldDialogueCanvas Place(Component panel, Vector3 point)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!panel) return null;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var canvas=panel.GetComponentInParent<Canvas>(true);
            // Report a missing authored dependency instead of adding objects or components at runtime.
            if (!canvas) {Debug.LogError("Panel requires an authored world Canvas.",panel);return null;}
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var placement=canvas.GetComponent<WorldDialogueCanvas>();
            // Report a missing authored dependency instead of adding objects or components at runtime.
            if (!placement) {Debug.LogError("Panel requires an authored WorldDialogueCanvas.",panel);return null;}
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            placement._Anchor=point;
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            placement.Follow=null;
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            placement.Configure();
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            return placement;
        }
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        public void Configure()
        {
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            _Canvas=GetComponent<Canvas>();
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            _Canvas.renderMode=RenderMode.WorldSpace;
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            _Canvas.worldCamera=Camera.main;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var rect=GetComponent<RectTransform>();
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            rect.pivot=new Vector2(.5f,.5f);
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            rect.sizeDelta=Size;
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            rect.localScale=Vector3.one*WorldScale;
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var scaler=GetComponent<CanvasScaler>();
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (scaler) { scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize; scaler.dynamicPixelsPerUnit=10; }
            // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
            var raycaster=GetComponent<GraphicRaycaster>();
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (raycaster) raycaster.ignoreReversedGraphics=false;
            // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
            LateUpdate();
        }
        // Resolve the authored dependencies early; the scene and prefab data determine what exists.
        void Awake() { Configure(); }
        // Apply the story system's existing authored effects through the selected private or shared authority path.
        void OnEnable() { StoryFunctions.OnSpeakerEvent += FollowSpeaker; }
        // Apply the story system's existing authored effects through the selected private or shared authority path.
        void OnDisable() { StoryFunctions.OnSpeakerEvent -= FollowSpeaker; }
        // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
        void FollowSpeaker(string speaker)
        {
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (speaker == "player")
            {
                // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
                var player=FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue && SharedDialogue.Instance ?
                    // Ownership restricts this path to the local player, so a remote avatar cannot take local input or camera focus.
                    p.NetId==SharedDialogue.Instance.SpeakerPlayerId : p.IsRelevantPlayer);
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (player) Follow=player.transform;
            }
            else
            {
                // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
                var character=FindObjectsByType<DefaultCharacter>(FindObjectsSortMode.None).FirstOrDefault(c => c.CharacterTag==speaker);
                // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                if (character) Follow=character.transform;
            }
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void LateUpdate()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!_Canvas) return;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (!_Canvas.worldCamera) _Canvas.worldCamera=Camera.main;
            // The authored world Canvas follows the relevant speaker and uses the active scene camera for rendering and pointer raycasts.
            transform.position=(Follow ? Follow.position : _Anchor)+Offset;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (FaceCamera && _Canvas.worldCamera) transform.rotation=_Canvas.worldCamera.transform.rotation;
        }
    }
}

