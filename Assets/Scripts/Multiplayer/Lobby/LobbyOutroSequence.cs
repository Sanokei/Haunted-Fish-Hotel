using System;
using System.Collections;
using System.Collections.Generic;
using Monologue.Dialogue;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Local lobby outro: crash dolly into the winner, a short celebration, the Win/Defeat card,
    // then a jib back while the credits roll. Leaving through the front door plays the credits alone.
    public sealed class LobbyOutroSequence : MonoBehaviour
    {
        [Header("Compiled Ink")]
        [SerializeField] TextAsset _GhostWinInk;
        [SerializeField] TextAsset _GhostDefeatInk;
        [SerializeField] TextAsset _FishWinInk;
        [SerializeField] TextAsset _FishDefeatInk;
        [SerializeField] TextAsset _CreditsInk;
        [Tooltip("Credits source. '# ' heading, '- ' small print, '@handle' gets the Instagram mark, '[logo] Name' shows Resources/CreditsLogos/Name, '//' is a note.")]
        [SerializeField] TextAsset _CreditsText;
        [Header("World")]
        [SerializeField] CinemachineCamera _Camera;
        [SerializeField] HotelFishSprite _Fish;
        [SerializeField] SpriteRenderer _Ghost;
        [SerializeField] Transform _WinnerMark, _CrashStart, _CrashEnd, _JibEnd;
        [SerializeField, Range(5, 90)] float _CrashFieldOfView = 28;
        [Header("Overlay")]
        [SerializeField] CanvasGroup _Overlay;
        [SerializeField] Image _Fade;
        [SerializeField] TMP_Text _Title;
        [SerializeField] RectTransform _CreditsViewport, _CreditsContent;
        [Tooltip("Inactive template rows cloned for each credits line.")]
        [SerializeField] TMP_Text _HeadingTemplate, _LineTemplate, _SmallTemplate;
        [SerializeField] Color _IconColor = Color.white;
        [SerializeField] IntroductionSkipCircle _SkipCircle;
        [SerializeField, Min(.1f)] float _SkipHoldSeconds = 1;
        [SerializeField, Range(0, 1)] float _CreditsDim = .72f;

        const string CreditsSeenKey = "HauntedFish.CreditsSeen.v1";
        // Same rule as the lobby introduction: editor and development builds always replay.
        static bool RememberCredits => !Application.isEditor && !Debug.isDebugBuild;
        static Sprite s_InstagramMark;

        public bool Playing { get; private set; }
        public event Action PlayingChanged;
        public bool CreditsPending => _CreditsInk && !(RememberCredits && PlayerPrefs.GetInt(CreditsSeenKey, 0) == 1);

        InputAction _Skip;
        float _SkipHeld;
        Coroutine _Routine;
        bool _CreditsAllowed, _Active, _CreditsBuilt, _Cheering;
        Transform _Actor;
        Vector3 _ActorHome, _ActorScale;
        float _CheerStart;
        Transform _Follow, _LookAt;
        CinemachineSplineDolly _Dolly;
        bool _DollyEnabled;
        Vector3 _CameraPosition;
        Quaternion _CameraRotation;
        float _CameraFieldOfView;
        readonly List<SpriteRenderer> _Hidden = new List<SpriteRenderer>();
        readonly List<bool> _HiddenState = new List<bool>();

        void Awake()
        {
            _Skip = new InputAction("Skip lobby outro", InputActionType.Button, "<Keyboard>/space");
            ResetOverlay();
        }
        void OnEnable() => _Skip.Enable();
        void OnDisable() { _Skip.Disable(); Stop(false); }
        void OnDestroy() => _Skip.Dispose();

        TextAsset InkFor(LobbyOutroKind kind) => kind switch
        {
            LobbyOutroKind.GhostWin => _GhostWinInk,
            LobbyOutroKind.GhostDefeat => _GhostDefeatInk,
            LobbyOutroKind.FishWin => _FishWinInk,
            LobbyOutroKind.FishDefeat => _FishDefeatInk,
            _ => null,
        };
        // Blacks out the lobby while a returning match result waits for its first shot.
        public void CoverScreen(bool cover)
        {
            if (Playing) return;
            if (!cover) { ResetOverlay(); return; }
            if (_Overlay) _Overlay.alpha = 1;
            if (_Fade) _Fade.color = Color.black;
        }
        public bool PlayOutcome(LobbyOutroKind kind) => Play(InkFor(kind));
        // Leaving the lobby shows the credits only while they are still pending.
        public bool PlayCredits() => CreditsPending && Play(_CreditsInk);

        bool Play(TextAsset ink)
        {
            if (Playing || !ink || !isActiveAndEnabled) return false;
            try
            {
                var steps = StoryFunctions.ReadAnimationSequence(ink.text);
                foreach (var step in steps)
                    if (!Supports(step)) throw new InvalidOperationException("Unsupported lobby outro step: " + step.Kind + " " + step.Target);
                _CreditsAllowed = CreditsPending;
                SetPlaying(true);
                _Routine = StartCoroutine(Run(steps));
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("Unable to play lobby outro: " + error.Message, this);
                Stop(false);
                return false;
            }
        }

        public bool Supports(StorySequenceStep step) => step.Kind switch
        {
            StorySequenceStepKind.Wait => true,
            StorySequenceStepKind.FlashFrames => _Fade,
            StorySequenceStepKind.OutroTitle => _Title,
            StorySequenceStepKind.RollCredits => _Camera && _JibEnd && _CreditsContent && _LineTemplate,
            StorySequenceStepKind.LobbyShot => _Camera && _WinnerMark && _CrashStart && _CrashEnd &&
                (step.Target == "winner_fish" && _Fish || step.Target == "winner_ghost" && _Ghost ||
                 step.Target == "crash_dolly" || step.Target == "cheer"),
            _ => false,
        };

        IEnumerator Run(IReadOnlyList<StorySequenceStep> steps)
        {
            yield return null;
            var completed = false;
            try
            {
                foreach (var step in steps)
                {
                    if (step.Kind == StorySequenceStepKind.Wait) { yield return new WaitForSecondsRealtime(step.Seconds); continue; }
                    yield return PlayStep(step);
                }
                completed = true;
            }
            finally { Finish(completed); }
        }

        IEnumerator PlayStep(StorySequenceStep step)
        {
            switch (step.Kind)
            {
                case StorySequenceStepKind.FlashFrames:
                    _Overlay.alpha = 1;
                    _Fade.color = Color.white;
                    for (int i = 0; i < step.Count; i++) yield return null;
                    _Fade.color = Color.clear;
                    break;
                case StorySequenceStepKind.OutroTitle:
                    yield return ShowTitle(step.Variant, step.Seconds);
                    break;
                case StorySequenceStepKind.RollCredits:
                    yield return RollCredits(step.Seconds);
                    break;
                case StorySequenceStepKind.LobbyShot when step.Target == "crash_dolly":
                    yield return CrashDolly(step.Seconds);
                    break;
                case StorySequenceStepKind.LobbyShot when step.Target == "cheer":
                    _Cheering = true; _CheerStart = Time.unscaledTime;
                    yield return new WaitForSecondsRealtime(step.Seconds);
                    break;
                case StorySequenceStepKind.LobbyShot:
                    yield return BeginWorld(step.Target == "winner_ghost");
                    yield return new WaitForSecondsRealtime(step.Seconds);
                    break;
            }
        }

        IEnumerator BeginWorld(bool ghost)
        {
            float deadline = Time.unscaledTime + 60;
            while (!HotelPlayer.LocalPlayer && Time.unscaledTime < deadline) yield return null;
            TakeCamera();
            HidePlayers();
            if (_Fish) _Fish.gameObject.SetActive(!ghost);
            if (_Ghost) _Ghost.gameObject.SetActive(ghost);
            if (!ghost)
            {
                var local = HotelPlayer.LocalPlayer;
                var visual = local ? local.GetComponentInChildren<HotelFishSprite>(true) : null;
                if (visual) _Fish.CopyAppearanceFrom(visual);
                foreach (var collider in _Fish.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
            _Actor = ghost ? _Ghost.transform : _Fish.transform;
            _Actor.position = _ActorHome = _WinnerMark.position;
            _ActorScale = _Actor.localScale;
            _Cheering = false;
            _Camera.transform.SetPositionAndRotation(_CrashStart.position, _CrashStart.rotation);
            SetFieldOfView(_CameraFieldOfView);
            _Camera.PreviousStateIsValid = false;
            if (_Fade) _Fade.color = Color.clear;
        }

        // Accelerates the whole way in, slams to a stop, then a short overshoot settles on the close-up.
        IEnumerator CrashDolly(float seconds)
        {
            var camera = _Camera.transform;
            Vector3 from = camera.position, to = _CrashEnd.position;
            Quaternion fromRotation = camera.rotation, toRotation = _CrashEnd.rotation;
            float fromFov = _Camera.Lens.FieldOfView;
            float push = seconds * .82f, settle = seconds - push;
            var overshoot = (to - from).normalized * .12f;
            yield return Tween(push, t =>
            {
                float e = t * t * t;
                camera.SetPositionAndRotation(Vector3.LerpUnclamped(from, to + overshoot, e), Quaternion.Slerp(fromRotation, toRotation, e));
                SetFieldOfView(Mathf.Lerp(fromFov, _CrashFieldOfView, e));
            });
            yield return Tween(settle, t =>
            {
                float e = 1 - (1 - t) * (1 - t);
                camera.position = Vector3.Lerp(to + overshoot, to, e);
            });
        }

        IEnumerator ShowTitle(string text, float seconds)
        {
            _Overlay.alpha = 1;
            _Title.text = text;
            _Title.gameObject.SetActive(true);
            // Sit the card at the top so it never covers the winner's face.
            _Title.verticalAlignment = VerticalAlignmentOptions.Top;
            _Title.margin = new Vector4(0, 40, 0, 0);
            // A dark outline keeps the card readable over the bright close-up; TMP only has its material once active.
            _Title.outlineWidth = .16f;
            _Title.outlineColor = new Color32(20, 12, 10, 255);
            var rect = _Title.rectTransform;
            float punch = Mathf.Min(.35f, seconds);
            yield return Tween(punch, t =>
            {
                float e = 1 - Mathf.Pow(1 - t, 3);
                rect.localScale = Vector3.one * Mathf.Lerp(1.6f, 1, e);
                _Title.alpha = t;
            });
            yield return new WaitForSecondsRealtime(Mathf.Max(0, seconds - punch));
        }

        IEnumerator RollCredits(float seconds)
        {
            TakeCamera();
            var camera = _Camera.transform;
            Vector3 from = camera.position;
            Quaternion fromRotation = camera.rotation;
            float fromFov = _Camera.Lens.FieldOfView, toFov = _CameraFieldOfView;
            // Without credits still pending, the jib alone closes the outro.
            bool credits = _CreditsAllowed;
            float total = credits ? seconds : Mathf.Min(seconds, 4);
            _Overlay.alpha = 1;
            float roll = 0, viewport = 0;
            if (credits)
            {
                BuildCredits();
                _CreditsViewport.gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_CreditsContent);
                viewport = _CreditsViewport.rect.height;
                roll = _CreditsContent.rect.height + viewport;
            }
            yield return Tween(total, t =>
            {
                float e = t * t * (3 - 2 * t);
                camera.SetPositionAndRotation(Vector3.Lerp(from, _JibEnd.position, e), Quaternion.Slerp(fromRotation, _JibEnd.rotation, e));
                SetFieldOfView(Mathf.Lerp(fromFov, toFov, e));
                if (_Title.gameObject.activeSelf) _Title.alpha = 1 - Mathf.Clamp01(t * total / .8f);
                // Dim the lobby under the roll, then fade fully to black for the hand-back.
                float dim = credits ? _CreditsDim * Mathf.Clamp01(t * 6) : 0;
                float black = Mathf.Clamp01((t - .92f) / .08f);
                _Fade.color = new Color(0, 0, 0, Mathf.Max(dim, black));
                if (credits) _CreditsContent.anchoredPosition = new Vector2(0, -viewport + roll * t);
            });
            if (credits) MarkCreditsSeen();
        }

        void Update()
        {
            if (!Playing) { _SkipHeld = 0; return; }
            _SkipHeld = _Skip.IsPressed() ? _SkipHeld + Time.unscaledDeltaTime : 0;
            float progress = Mathf.Clamp01(_SkipHeld / Mathf.Max(.1f, _SkipHoldSeconds));
            if (_SkipCircle) { _SkipCircle.gameObject.SetActive(progress > 0); _SkipCircle.Progress = progress; }
            // A held skip counts as having seen the credits, like the introduction.
            if (progress >= 1) { if (_CreditsAllowed) MarkCreditsSeen(); Stop(true); }
        }
        static void MarkCreditsSeen()
        {
            if (RememberCredits) { PlayerPrefs.SetInt(CreditsSeenKey, 1); PlayerPrefs.Save(); }
        }

        void LateUpdate()
        {
            if (!_Actor || !_Actor.gameObject.activeSelf) return;
            var camera = _Camera ? _Camera.transform : null;
            float t = Time.unscaledTime - _CheerStart;
            if (_Actor == _Fish.transform)
            {
                // Three quick hops with a squash on each landing.
                float hop = _Cheering && t < 1.8f ? Mathf.Abs(Mathf.Sin(t * Mathf.PI * 1.65f)) : 0;
                _Actor.position = _ActorHome + Vector3.up * hop * .32f;
                float squash = _Cheering && hop < .2f ? 1 - (.2f - hop) * .6f : 1;
                _Actor.localScale = Vector3.Scale(_ActorScale, new Vector3(2 - squash, squash, 1));
                _Fish.Present(false, _Cheering, _Cheering);
            }
            else
            {
                // The ghost bobs in place and twirls once when it celebrates.
                float bob = Mathf.Sin(Time.unscaledTime * 2.4f) * .06f;
                float twirl = _Cheering ? Mathf.SmoothStep(0, 360, Mathf.Clamp01(t / 1.1f)) : 0;
                _Actor.position = _ActorHome + Vector3.up * (bob + (_Cheering ? Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI) * .25f : 0));
                if (camera) _Actor.rotation = Quaternion.LookRotation(camera.forward, Vector3.up) * Quaternion.Euler(0, twirl, 0);
            }
        }

        void BuildCredits()
        {
            if (_CreditsBuilt || !_CreditsText) return;
            _CreditsBuilt = true;
            foreach (var raw in _CreditsText.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("//")) continue;
                if (line.StartsWith("[logo] ")) { AddLogo(line.Substring(7).Trim()); continue; }
                var template = line.StartsWith("# ") ? _HeadingTemplate : line.StartsWith("- ") ? _SmallTemplate : _LineTemplate;
                if (!template) template = _LineTemplate;
                var text = line.StartsWith("# ") || line.StartsWith("- ") ? line.Substring(2) : line;
                AddRow(template, text);
            }
        }
        // A row is a centered run of text pieces; each '@handle' gets the Instagram mark in front of it.
        void AddRow(TMP_Text template, string text)
        {
            var row = new GameObject("Credits row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(_CreditsContent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = template.fontSize * .25f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (string.IsNullOrWhiteSpace(text)) { AddText(row.transform, template, " "); return; }
            var words = text.Split(' ');
            var run = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                if (word.Length > 1 && word[0] == '@')
                {
                    if (run.Length > 0) { AddText(row.transform, template, run.ToString().TrimEnd()); run.Clear(); }
                    AddIcon(row.transform, template.fontSize);
                    AddText(row.transform, template, word);
                }
                else run.Append(word).Append(' ');
            }
            if (run.Length > 0) AddText(row.transform, template, run.ToString().TrimEnd());
        }
        static void AddText(Transform row, TMP_Text template, string text)
        {
            var copy = Instantiate(template, row);
            copy.gameObject.SetActive(true);
            copy.text = text;
            copy.textWrappingMode = TextWrappingModes.NoWrap;
        }
        void AddIcon(Transform row, float size)
        {
            var icon = new GameObject("Instagram", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            icon.transform.SetParent(row, false);
            var image = icon.GetComponent<Image>();
            image.sprite = InstagramMark(); image.color = _IconColor; image.raycastTarget = false;
            var element = icon.GetComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = size * .82f;
        }
        // A centered logo row, sized off the body text so it scales with the roll.
        void AddLogo(string name)
        {
            var texture = Resources.Load<Texture2D>("CreditsLogos/" + name);
            if (!texture) { Debug.LogWarning("Missing credits logo: " + name, this); return; }
            var row = new GameObject("Credits logo " + name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            row.transform.SetParent(_CreditsContent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var logo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            logo.transform.SetParent(row.transform, false);
            var image = logo.GetComponent<Image>();
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            image.preserveAspect = true; image.raycastTarget = false;
            var element = logo.GetComponent<LayoutElement>();
            element.preferredHeight = _LineTemplate.fontSize * 1.8f;
            element.preferredWidth = element.preferredHeight * texture.width / texture.height;
        }
        // The outline camera mark: rounded square, lens ring and flash dot, drawn once with soft edges.
        static Sprite InstagramMark()
        {
            if (s_InstagramMark) return s_InstagramMark;
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Instagram mark" };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + .5f) / n - .5f, (y + .5f) / n - .5f);
                // Signed distances in unit space; strokes are .075 thick.
                var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(.42f - .13f, .42f - .13f);
                float box = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - .13f;
                float frame = Mathf.Abs(box + .0375f) - .0375f;
                float lens = Mathf.Abs(p.magnitude - .17f) - .0375f;
                float dot = (p - new Vector2(.215f, .215f)).magnitude - .04f;
                float d = Mathf.Min(frame, Mathf.Min(lens, dot));
                byte a = (byte)(Mathf.Clamp01(.5f - d * n) * 255);
                pixels[y * n + x] = new Color32(255, 255, 255, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            s_InstagramMark = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
            return s_InstagramMark;
        }

        void TakeCamera()
        {
            if (_Active) return;
            _Active = true;
            _CameraPosition = _Camera.transform.position;
            _CameraRotation = _Camera.transform.rotation;
            _CameraFieldOfView = _Camera.Lens.FieldOfView;
            _Follow = _Camera.Follow; _LookAt = _Camera.LookAt;
            _Dolly = _Camera.GetComponent<CinemachineSplineDolly>();
            if (_Dolly) { _DollyEnabled = _Dolly.enabled; _Dolly.enabled = false; }
            _Camera.Follow = null; _Camera.LookAt = null;
        }
        void HidePlayers()
        {
            if (_Hidden.Count > 0) return;
            foreach (var player in HotelPlayer.ActivePlayers)
            {
                if (!player) continue;
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    _Hidden.Add(renderer); _HiddenState.Add(renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            }
        }

        void SetFieldOfView(float fov) { var lens = _Camera.Lens; lens.FieldOfView = fov; _Camera.Lens = lens; }
        void SetPlaying(bool playing)
        {
            if (Playing == playing) return;
            Playing = playing;
            PlayingChanged?.Invoke();
        }
        void Stop(bool skipped)
        {
            var routine = _Routine;
            _Routine = null;
            if (routine != null) StopCoroutine(routine);
            Finish(skipped);
        }
        void Finish(bool completed)
        {
            _Routine = null;
            _SkipHeld = 0;
            _Cheering = false;
            ResetOverlay();
            if (_Actor) _Actor.localScale = _ActorScale;
            if (_Fish) _Fish.gameObject.SetActive(false);
            if (_Ghost) _Ghost.gameObject.SetActive(false);
            _Actor = null;
            for (int i = 0; i < _Hidden.Count; i++) if (_Hidden[i]) _Hidden[i].forceRenderingOff = _HiddenState[i];
            _Hidden.Clear(); _HiddenState.Clear();
            if (_Active && _Camera)
            {
                _Camera.transform.SetPositionAndRotation(_CameraPosition, _CameraRotation);
                SetFieldOfView(_CameraFieldOfView);
                var player = HotelPlayer.LocalPlayer;
                _Camera.Follow = player ? player.transform : _Follow;
                _Camera.LookAt = _LookAt;
                if (_Dolly) _Dolly.enabled = _DollyEnabled;
                _Camera.PreviousStateIsValid = false;
            }
            _Active = false;
            SetPlaying(false);
        }
        void ResetOverlay()
        {
            if (_Overlay) _Overlay.alpha = 0;
            if (_Fade) _Fade.color = Color.clear;
            if (_Title) { _Title.text = ""; _Title.gameObject.SetActive(false); }
            if (_CreditsViewport) _CreditsViewport.gameObject.SetActive(false);
            if (_SkipCircle) { _SkipCircle.Progress = 0; _SkipCircle.gameObject.SetActive(false); }
        }

        static IEnumerator Tween(float seconds, Action<float> apply)
        {
            if (seconds <= 0) { apply(1); yield break; }
            for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            apply(1);
        }
    }
}
