using UnityEngine;
using UnityEngine.UI;
using Monologue.Dialogue;
using Monologue.Character;
using System.Linq;
using TMPro;

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
        Panel _Panel;
        TMP_Text _Speech;
        float _Letters;
        int _LetterCount;
        [SerializeField] AudioClip _BubbleVoice;
        [SerializeField] AudioSource _Voice;
        [Min(1)] public float LettersPerSecond = 32;

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
            canvas.gameObject.SetActive(true);
            placement.Configure();
            return placement;
        }

        public void Configure()
        {
            _Canvas = GetComponent<Canvas>();
            _Canvas.renderMode = RenderMode.WorldSpace;
            _Canvas.worldCamera = HotelViewCamera.Current;
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
            ConfigureSpeech();
            LateUpdate();
        }

        void ConfigureSpeech()
        {
            if (_Panel) return;
            _Panel = GetComponentInChildren<Panel>(true);
            if (!_Panel || !_Panel.SpeechText) return;
            _Speech = _Panel.SpeechText;
            // Legacy screen panels have masks and depth offsets which clip world text.
            foreach (var mask in GetComponentsInChildren<Mask>(true)) mask.enabled = false;
            foreach (var mask in GetComponentsInChildren<RectMask2D>(true)) mask.enabled = false;
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (!image.GetComponentInParent<Button>()) image.enabled = false;
            var rect = _Speech.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-24, 0); rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var position = rect.localPosition; position.z = -.01f; rect.localPosition = position;
            _Speech.alignment = TextAlignmentOptions.Center;
            _Speech.enableAutoSizing = true; _Speech.fontSizeMin = 24; _Speech.fontSizeMax = 36;
            _Speech.overflowMode = TextOverflowModes.Overflow;
            _Speech.textWrappingMode = TextWrappingModes.Normal;
            _Speech.raycastTarget = false;
            _Speech.outlineWidth = .15f;
            _Panel.TextChanged += BeginTyping;
            _Panel.RevealTyping = RevealText;
            if (_BubbleVoice && _Voice)
            {
                _Voice.clip = _BubbleVoice;
                HotelAudioRouting.RouteEffects(_Voice);
            }
            else if (_BubbleVoice) Debug.LogError("Assign the authored speaker AudioSource.", this);
        }

        void BeginTyping(string text)
        {
            _Letters = 0;
            _Speech.ForceMeshUpdate(true);
            _LetterCount = _Speech.textInfo.characterCount;
            _Speech.maxVisibleCharacters = 0;
            if (_Voice && _LetterCount > 0) _Voice.Play();
        }

        bool RevealText()
        {
            if (!_Speech || _Speech.maxVisibleCharacters >= _LetterCount) return false;
            _Letters = _LetterCount; _Speech.maxVisibleCharacters = _LetterCount;
            if (_Voice) _Voice.Stop();
            return true;
        }

        void Update()
        {
            if (!_Speech || _Speech.maxVisibleCharacters >= _LetterCount) return;
            _Letters += Time.unscaledDeltaTime * LettersPerSecond;
            _Speech.maxVisibleCharacters = Mathf.Min(_LetterCount, Mathf.FloorToInt(_Letters));
            if (_Speech.maxVisibleCharacters >= _LetterCount && _Voice) _Voice.Stop();
        }

        void OnDestroy()
        {
            if (_Panel) { _Panel.TextChanged -= BeginTyping; _Panel.RevealTyping = null; }
        }

        void Awake()
        {
            Configure();
        }

        void OnEnable()
        {
            StoryFunctions.OnSpeakerEvent += FollowSpeaker;
            HotelViewCamera.Changed += BindCamera;
            BindCamera(HotelViewCamera.Current);
        }

        void OnDisable()
        {
            if (_Voice) _Voice.Stop();
            StoryFunctions.OnSpeakerEvent -= FollowSpeaker;
            HotelViewCamera.Changed -= BindCamera;
            if(_Canvas)_Canvas.worldCamera=null;
        }

        void BindCamera(Camera camera){if(_Canvas)_Canvas.worldCamera=camera;}

        void FollowSpeaker(string speaker)
        {
            if (speaker == "player")
            {
                HotelPlayer player=null;
                for(int i=0;i<HotelPlayer.ActivePlayers.Count;i++)if(IsSpeakingPlayer(HotelPlayer.ActivePlayers[i])){player=HotelPlayer.ActivePlayers[i];break;}
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
            if (_Canvas.worldCamera != HotelViewCamera.Current)
                _Canvas.worldCamera = HotelViewCamera.Current;
            transform.position = (Follow ? Follow.position : _Anchor) + Offset;
            if (FaceCamera && _Canvas.worldCamera)
            {
                var camera = _Canvas.worldCamera;
                var anchor = Follow ? Follow.position : _Anchor;

                float depth = Mathf.Max(camera.nearClipPlane + .5f, Vector3.Dot(anchor - camera.transform.position, camera.transform.forward) - .2f);
                float height = camera.orthographic ? camera.orthographicSize * 2 : 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                float scale = Mathf.Min(WorldScale, height * .32f / Size.y, height * camera.aspect * .75f / Size.x);
                transform.localScale = Vector3.one * scale;
                var viewport = camera.WorldToViewportPoint(anchor);
                float halfWidth = Size.x * scale / (height * camera.aspect) * .5f;
                float halfHeight = Size.y * scale / height * .5f;
                if (_Speech && _Panel && !_Panel.HasChoices)
                    halfHeight = Mathf.Min(halfHeight, Mathf.Max(.035f, _Speech.preferredHeight * scale / height * .5f));
                viewport.x = Mathf.Clamp(viewport.x, halfWidth + .03f, .97f - halfWidth);
                viewport.y = Mathf.Clamp(viewport.y - .37f, halfHeight + .03f, .97f - halfHeight);
                viewport.z = depth;
                transform.position = camera.ViewportToWorldPoint(viewport);
                transform.rotation = _Canvas.worldCamera.transform.rotation;
            }
        }
    }
}

