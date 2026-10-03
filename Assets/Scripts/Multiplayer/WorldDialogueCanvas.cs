using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Monologue.Dialogue;
using Monologue.Character;
using System.Linq;
namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(Canvas))]
    public sealed class WorldDialogueCanvas : MonoBehaviour
    {
        public Vector3 Offset = new Vector3(3.3f,4.2f,0);
        public Vector2 Size = new Vector2(900,440);
        public float WorldScale = .009f;
        public bool AdaptLegacyLayout = true;
        public Transform Follow;
        public bool FaceCamera = true;
        Vector3 anchor;
        Canvas canvas;
        public static WorldDialogueCanvas Place(Component panel, Vector3 point)
        {
            if (!panel) return null;
            var canvas=panel.GetComponentInParent<Canvas>(true);
            if (!canvas) canvas=panel.gameObject.AddComponent<Canvas>();
            var placement=canvas.GetComponent<WorldDialogueCanvas>();
            if (!placement) placement=canvas.gameObject.AddComponent<WorldDialogueCanvas>();
            placement.anchor=point;
            placement.Follow=null;
            placement.Configure();
            if (placement.AdaptLegacyLayout && panel is Panel dialogue) dialogue.ConfigureWorldLayout();
            if (placement.AdaptLegacyLayout && panel is Monologue.StoryInput.TextFieldPanel input) input.ConfigureWorldLayout();
            return placement;
        }
        public void Configure()
        {
            canvas=GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=Camera.main;
            var rect=GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=Size;
            rect.localScale=Vector3.one*WorldScale;
            var scaler=GetComponent<CanvasScaler>();
            if (scaler) { scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize; scaler.dynamicPixelsPerUnit=10; }
            var raycaster=GetComponent<GraphicRaycaster>();
            if (!raycaster) raycaster=gameObject.AddComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics=false;
            if (!EventSystem.current)
            {
                var events=new GameObject("World UI Events",typeof(EventSystem));
                if (Application.isPlaying) DontDestroyOnLoad(events);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            else if (!EventSystem.current.GetComponent<InputSystemUIInputModule>())
            {
                var old=EventSystem.current.GetComponent<StandaloneInputModule>();
                if (old) old.enabled=false;
                EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            LateUpdate();
        }
        void Awake() { Configure(); }
        void OnEnable() { StoryFunctions.OnSpeakerEvent += FollowSpeaker; }
        void OnDisable() { StoryFunctions.OnSpeakerEvent -= FollowSpeaker; }
        void FollowSpeaker(string speaker)
        {
            if (speaker == "player")
            {
                var player=FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue && SharedDialogue.Instance ?
                    p.NetId==SharedDialogue.Instance.SpeakerPlayerId : p.IsRelevantPlayer);
                if (player) Follow=player.transform;
            }
            else
            {
                var character=FindObjectsByType<DefaultCharacter>(FindObjectsSortMode.None).FirstOrDefault(c => c.CharacterTag==speaker);
                if (character) Follow=character.transform;
            }
        }
        void LateUpdate()
        {
            if (!canvas) return;
            if (!canvas.worldCamera) canvas.worldCamera=Camera.main;
            transform.position=(Follow ? Follow.position : anchor)+Offset;
            if (FaceCamera && canvas.worldCamera) transform.rotation=canvas.worldCamera.transform.rotation;
        }
    }
}

