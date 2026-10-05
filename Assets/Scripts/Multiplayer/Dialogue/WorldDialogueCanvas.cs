using UnityEngine;
using UnityEngine.UI;
using Monologue.Dialogue;
using Monologue.Character;
using System.Linq;

namespace HauntedFish.Multiplayer
{
    [RequireComponent(typeof(Canvas))]
    public sealed class WorldDialogueCanvas : MonoBehaviour
    {
        public Vector3 Offset = new Vector3(3.3f, 4.2f, 0);
        public Vector2 Size = new Vector2(900, 440);
        public float WorldScale = .009f;
        public bool AdaptLegacyLayout = true;
        public Transform Follow;
        public bool FaceCamera = true;
        Vector3 _Anchor;
        Canvas _Canvas;

        public static WorldDialogueCanvas Place(Component panel, Vector3 point)
        {
            if (!panel)
                return null;
            var canvas = panel.GetComponentInParent<Canvas>(true);
            if (!canvas)
            {
                Debug.LogError("Panel requires an authored world Canvas.", panel);
                return null;
            }

            var placement = canvas.GetComponent<WorldDialogueCanvas>();
            if (!placement)
            {
                Debug.LogError("Panel requires an authored WorldDialogueCanvas.", panel);
                return null;
            }

            placement._Anchor = point;
            placement.Follow = null;
            placement.Configure();
            return placement;
        }

        public void Configure()
        {
            _Canvas = GetComponent<Canvas>();
            _Canvas.renderMode = RenderMode.WorldSpace;
            _Canvas.worldCamera = Camera.main;
            var rect = GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Size;
            rect.localScale = Vector3.one * WorldScale;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 10;
            }

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster)
                raycaster.ignoreReversedGraphics = false;
            LateUpdate();
        }

        void Awake()
        {
            Configure();
        }

        void OnEnable()
        {
            StoryFunctions.OnSpeakerEvent += FollowSpeaker;
        }

        void OnDisable()
        {
            StoryFunctions.OnSpeakerEvent -= FollowSpeaker;
        }

        void FollowSpeaker(string speaker)
        {
            if (speaker == "player")
            {
                var player = FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None)
                    .FirstOrDefault(IsSpeakingPlayer);
                if (player)
                    Follow = player.transform;
            }
            else
            {
                var character = FindObjectsByType<DefaultCharacter>(FindObjectsSortMode.None).FirstOrDefault(c => c.CharacterTag == speaker);
                if (character)
                    Follow = character.transform;
            }
        }

        bool IsSpeakingPlayer(HotelPlayer player)
        {
            if (DialogueManager.Instance && DialogueManager.Instance.IsSharedDialogue && SharedDialogue.Instance)
                return player.NetId == SharedDialogue.Instance.SpeakerPlayerId;

            return player.IsRelevantPlayer;
        }

        void LateUpdate()
        {
            if (!_Canvas)
                return;
            if (!_Canvas.worldCamera)
                _Canvas.worldCamera = Camera.main;
            transform.position = (Follow ? Follow.position : _Anchor) + Offset;
            if (FaceCamera && _Canvas.worldCamera)
                transform.rotation = _Canvas.worldCamera.transform.rotation;
        }
    }
}
