using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Monologue.Dialogue;
using System.Collections.Generic;

namespace HauntedFish.Multiplayer
{
    // Every round uses the authored cinematic and a deliberate ready-up page; no saved seen flag.
    public sealed class GhostSelectionPresentation : MonoBehaviour
    {
        [SerializeField] GameObject _Root;
        [SerializeField] RectTransform _Wheel, _Pointer;
        [SerializeField] GhostSelectionWheel _Graphic;
        [SerializeField] Text _Title, _Caption, _PlayerLabel;
        [SerializeField] TextAsset _RoundInk;
        [SerializeField] StoryUI _AtticPrefab;
        [SerializeField] Vector2 _ReturnPosition = Vector2.zero;
        readonly List<Image> _Streaks = new List<Image>();
        RoundHowToPage _HowTo;
        AtticTransitionAssets _Assets;
        [SerializeField, Min(0)] float _HallwayLookSeconds = GameplayPresentationPolicy.DefaultHallwayLookSeconds;
        readonly HallwayFishStaging<HallwayFishVisual> _HallwayFish = new HallwayFishStaging<HallwayFishVisual>();
        RectTransform _HallwayFishRoot;
        float _HallwayFishStarted;
        sealed class HallwayFishVisual
        {
            public RectTransform Root;
            public readonly List<RectTransform> Fins = new List<RectTransform>();
            public readonly List<Quaternion> FinRest = new List<Quaternion>();
        }
        void AddHallwayFish(uint[] roster)
        {
            EndHallwayFish();
            var wall = _Attic.Find("hallway_wall");
            var floor = _Attic.Find("hallway_floor");
            var hotel = floor ? floor.parent as RectTransform : null;
            if (!wall || !floor || !hotel || wall.parent != hotel)
                throw new InvalidOperationException("Bind the cutscene's ground-floor wall and beam for hallway fish staging.");
            _Assets = _Attic.GetComponent<AtticTransitionAssets>();
            if (!_Assets || !_Assets.FishRoot || !_Assets.FishPrefab) throw new InvalidOperationException("Bind authored hallway fish assets.");
            _HallwayFishRoot = _Assets.FishRoot;
            _HallwayFishRoot.gameObject.SetActive(true);
            float floorTop = hotel.InverseTransformPoint(floor.TransformPoint(new Vector3(0, floor.rect.yMax, 0))).y;
            float wallCenter = hotel.InverseTransformPoint(wall.TransformPoint(wall.rect.center)).x;
            _HallwayFishStarted = Time.unscaledTime;
            if (roster == null || roster.Length == 0 || _Assets.IntroFishWorldX == null || _Assets.IntroFishWorldX.Length != 4 || _Assets.IntroFish == null || _Assets.IntroFish.Length != 4)
                throw new InvalidOperationException("Author exactly four starting-area intro fish slots.");
            // Slot identity belongs only to this cutscene ensemble. Cycle actual fish
            // appearance sources when fewer than four players participate.
            _HallwayFish.Begin(new uint[] { 0, 1, 2, 3 }, slot => CreateHallwayFish(roster[(int)slot % roster.Length], floorTop, wallCenter, wall.rect.width, wall.rect.yMax - (floorTop - wall.anchoredPosition.y), (int)slot), visual =>
            {
                if (!visual.Root) return;
                visual.Root.gameObject.SetActive(false);
                // Saved intro figures remain in their prefab; only visibility changes.
            });
        }
        HallwayFishVisual CreateHallwayFish(uint id, float floorTop, float wallCenter, float wallWidth, float artworkClearance, int introSlot)
        {
            HotelPlayer player = null;
            foreach (var member in HotelPlayer.ActivePlayers)
                if (member && member.NetId == id) { player = member; break; }
            var scene = GameSceneController.Current;
            if (!player || !scene) return null;
            var source = player.GetComponentInChildren<HotelFishSprite>(true);
            if (!source) return null;
            var sourceScale = source.transform.lossyScale;
            if (!GameplayPresentationPolicy.Finite(sourceScale.x) || !GameplayPresentationPolicy.Finite(sourceScale.y) ||
                !GameplayPresentationPolicy.Finite(sourceScale.z) || Mathf.Abs(sourceScale.x) < .001f ||
                Mathf.Abs(sourceScale.y) < .001f || Mathf.Abs(sourceScale.z) < .001f) return null;
            var renderers = source.GetComponentsInChildren<SpriteRenderer>(true);
            Array.Sort(renderers, (a, b) => (a ? a.sortingOrder : int.MinValue).CompareTo(b ? b.sortingOrder : int.MinValue));
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.sprite) continue;
                var center = source.transform.InverseTransformPoint(renderer.transform.TransformPoint(renderer.sprite.bounds.center));
                var scale = renderer.transform.lossyScale;
                var size = Vector3.Scale(renderer.sprite.bounds.size, new Vector3(scale.x / sourceScale.x, scale.y / sourceScale.y, 1));
                var piece = new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0));
                if (!initialized) { bounds = piece; initialized = true; } else bounds.Encapsulate(piece);
            }
            if (!initialized || bounds.size.y <= .001f) return null;
            var body = player.GetComponent<CharacterController>();
            float playerHeight = body ? body.height * Mathf.Abs(player.transform.lossyScale.y) : bounds.size.y * Mathf.Abs(sourceScale.y);
            float clearance = scene.HallwayCeilingY - scene.HallwayBounds.max.y;
            float height = artworkClearance * Mathf.Clamp(_Assets.IntroFishHeightFraction, .1f, .8f);
            float fit = height / bounds.size.y;
            float width = bounds.size.x * fit;
            var visual = new HallwayFishVisual();
            var artwork = _Assets.IntroFish[introSlot];
            if (!artwork) throw new InvalidOperationException("Bind four saved intro fish figures.");
            artwork.gameObject.SetActive(true);
            visual.Root = artwork.Root;
            visual.Root.name = "Intro ensemble fish " + (introSlot + 1) + " - appearance " + id;
            visual.Root.anchorMin = visual.Root.anchorMax = new Vector2(.5f, .5f);
            visual.Root.pivot = new Vector2(.5f, 0);
            visual.Root.sizeDelta = new Vector2(width, height);
            var hallway = scene.HallwayBounds;
            // Authored starting-area slot; actor transforms and network roster remain unchanged.
            visual.Root.anchoredPosition = new Vector2(wallCenter + GameplayPresentationPolicy.HallwayFishX(
                _Assets.IntroFishWorldX[introSlot], hallway.min.x, hallway.max.x, wallWidth, width * .5f), floorTop);
            var shadow = artwork.Shadow;
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(.5f, 0);
            shadow.rectTransform.anchoredPosition = new Vector2(0, 2);
            shadow.rectTransform.sizeDelta = new Vector2(width * .65f, 8);
            var shadowColor = HotelPalette.Night; shadowColor.a = .28f; shadow.color = shadowColor;
            int pieceIndex = 0;
            foreach (var piece in artwork.Pieces) piece.gameObject.SetActive(false);
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.sprite) continue;
                if (pieceIndex >= artwork.Pieces.Length) { artwork.gameObject.SetActive(false); throw new InvalidOperationException("Author more fish artwork slots for this player."); }
                var image = artwork.Pieces[pieceIndex++];
                image.gameObject.SetActive(true);
                image.name = renderer.name;
                image.sprite = renderer.sprite; image.color = renderer.color;
                var center = source.transform.InverseTransformPoint(renderer.transform.TransformPoint(renderer.sprite.bounds.center));
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0);
                image.rectTransform.anchoredPosition = new Vector2(center.x - bounds.center.x, center.y - bounds.min.y) * fit;
                var scale = renderer.transform.lossyScale;
                var size = renderer.sprite.bounds.size;
                image.rectTransform.sizeDelta = new Vector2(Mathf.Abs(size.x * scale.x / sourceScale.x), Mathf.Abs(size.y * scale.y / sourceScale.y)) * fit;
                image.rectTransform.localScale = new Vector3(Mathf.Sign(scale.x / sourceScale.x) * (renderer.flipX ? -1 : 1),
                    Mathf.Sign(scale.y / sourceScale.y) * (renderer.flipY ? -1 : 1), 1);
                var right = source.transform.InverseTransformDirection(renderer.transform.right);
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);
                if (renderer.name.IndexOf("fin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visual.Fins.Add(image.rectTransform);
                    visual.FinRest.Add(image.rectTransform.localRotation);
                }
            }
            return visual;
        }
        void AnimateHallwayFish()
        {
            float elapsed = Mathf.Max(0, Time.unscaledTime - _HallwayFishStarted);
            foreach (var item in _HallwayFish.Visuals)
            {
                var visual = item.Value;
                if (!visual.Root) continue;
                visual.Root.localScale = new Vector3(GameplayPresentationPolicy.HallwayFishFacesLeft(elapsed, item.Key) ? -1 : 1, 1, 1);
                visual.Root.localRotation = Quaternion.Euler(0, 0, GameplayPresentationPolicy.HallwayFishLean(elapsed, item.Key));
                for (int i = 0; i < visual.Fins.Count; i++)
                    if (visual.Fins[i]) visual.Fins[i].localRotation = visual.FinRest[i] * Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 3.4f + item.Key % 7) * 3);
            }
        }
        void EndHallwayFish()
        {
            _HallwayFish.End();
            if (_HallwayFishRoot)
            {
                _HallwayFishRoot.gameObject.SetActive(false);

                _HallwayFishRoot = null;
            }
        }
        void CloseAttic()
        {
            EndHallwayFish();
            _Streaks.Clear();
            if (_Attic) { _Attic.Close(); Destroy(_Attic.gameObject); _Attic = null; }
            _AtticCanvas = null;
        }
        IEnumerator Descend(StorySequenceStep step)
        {
            // The authored ghost and hotel artwork share the hotel's moving RectTransform.
            var ghost = _Attic.Find("ghost");
            var hotel = ghost ? ghost.parent as RectTransform : null;
            if (!hotel) throw new InvalidOperationException("The attic ghost must belong to the authored hotel stack.");
            var startPosition = hotel.anchoredPosition;
            if (!_Assets || !_Assets.StreakRoot || _Assets.Streaks == null) throw new InvalidOperationException("Bind authored upward streaks.");
            var effects = _Assets.StreakRoot;
            effects.gameObject.SetActive(true);
            _Streaks.Clear();
            _Streaks.AddRange(_Assets.Streaks);
            float elapsed = 0;
            while (elapsed < step.Seconds)
            {
                float t = step.Seconds > 0 ? Mathf.Clamp01(elapsed / step.Seconds) : 1;
                // Ease in, with maximum speed at the cut. Never ease out or hold the bottom pose.
                hotel.anchoredPosition = Vector2.LerpUnclamped(startPosition, _ReturnPosition, GameplayPresentationPolicy.DescentProgress(elapsed, step.Seconds, step.X));
                var size = effects.rect.size;
                for (int i = 0; i < _Streaks.Count; i++)
                {
                    var streak = _Streaks[i];
                    float phase = Mathf.Repeat(i * .381966f + t * 1.7f, 1);
                    streak.rectTransform.anchoredPosition = new Vector2((i * .618034f % 1 - .5f) * size.x, (phase - .5f) * (size.y + 440));
                    streak.rectTransform.sizeDelta = new Vector2(2 + i % 3, (80 + i % 5 * 35) * (.4f + t));
                    var color = HotelPalette.Light; color.a = .08f + t * .2f;
                    streak.color = color;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            // Remove the transition on the same update that completes the accelerating travel.
            CloseAttic();
        }
        StoryUI _Attic;
        Canvas _WheelCanvas, _AtticCanvas;
        void Awake() { if (_Root) _WheelCanvas = _Root.GetComponent<Canvas>(); }
        void LateUpdate()
        {
            AnimateHallwayFish();
            bool watching = HotelPlayer.LocalPlayer && HotelPlayer.LocalPlayer.BossWatching;
            if (_WheelCanvas) _WheelCanvas.enabled = !watching;
            if (_Attic && (!_AtticCanvas || _AtticCanvas.gameObject != _Attic.gameObject)) _AtticCanvas = _Attic.GetComponent<Canvas>();
            if (_AtticCanvas) _AtticCanvas.enabled = !watching;
            if (_HowTo) _HowTo.SetVisible(!watching);
        }
        readonly System.Collections.Generic.List<Text> _Labels = new System.Collections.Generic.List<Text>();
        public void Play(Texture2D arrow, uint[] roster, uint ghost, uint local, Action finished, Action ready)
        {
            Close();
            if (!_AtticPrefab) throw new InvalidOperationException("Assign the authored attic cutscene prefab.");
            var settings = _AtticPrefab.GetComponent<RoundIntroductionSettings>();
            if (!_RoundInk && (!settings || !settings.Sequence))
                throw new InvalidOperationException("Assign the compiled round Ink sequence on the cutscene prefab.");
            if (roster == null || roster.Length == 0 || Array.IndexOf(roster, ghost) < 0)
                throw new ArgumentException("The authoritative ghost must belong to the round roster.");
            StartCoroutine(Animate(arrow, roster, ghost, local, finished, ready));
        }

        IEnumerator Animate(Texture2D arrow, uint[] roster, uint ghost, uint local, Action finished, Action ready)
        {
            _Root.SetActive(true);
            _Graphic.Count = roster.Length;
            _Graphic.SetVerticesDirty();
            foreach (var label in _Labels) if (label) Destroy(label.gameObject);
            _Labels.Clear();
            _Title.text = "WHO WILL HAUNT THE HOTEL?";
            for (int i = 0; i < roster.Length; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2 / roster.Length;
                var label = Instantiate(_PlayerLabel, _Wheel, false);
                label.gameObject.SetActive(true);
                label.text = "Player " + roster[i] + (roster[i] == local ? " (you)" : "");
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 158;
                label.color = GhostSelectionWheel.SectorColor(i).grayscale > .48f ? HotelPalette.Night : HotelPalette.Light;
                _Labels.Add(label);
            }
            int selected = Array.IndexOf(roster, ghost);
            var settings = _AtticPrefab.GetComponent<RoundIntroductionSettings>();
            var sequence = _RoundInk ? _RoundInk : settings.Sequence;
            float timingScale = settings ? settings.TimingScale : 1;
            if (float.IsNaN(timingScale) || float.IsInfinity(timingScale) || timingScale <= 0)
                throw new InvalidOperationException("Round timing scale must be finite and positive.");
            var steps = new List<StorySequenceStep>();
            float totalSeconds = 0;
            foreach (var step in StoryFunctions.ReadAnimationSequence(sequence.text))
            {
                float seconds = step.Target == "wheel_spin" ? step.Seconds : step.Seconds * timingScale;
                steps.Add(new StorySequenceStep(step.Kind, step.Target, step.Variant, seconds,
                    step.X, step.Y, step.Count, step.Visible, step.Parent));
                totalSeconds += seconds * (step.Kind == StorySequenceStepKind.Knock ? step.Count : 1);
                if (step.Target == "hotel_bottom" && step.Kind == StorySequenceStepKind.DollyTo) break;
            }
            if (totalSeconds < GameRoundGate.MinimumDuration)
                throw new InvalidOperationException("Round sequence must meet the server's minimum presentation duration.");
            _AtticPrefab.ValidateSequence(new List<StorySequenceStep>(FilterAtticSteps(steps)));
            _Pointer.localRotation = Quaternion.identity;
            _Graphic.Selected = -1;
            _Graphic.SelectionPulse = 0;
            _Caption.text = "Choosing the ghost...";
            foreach (var step in steps)
            {
                if (step.Target == "wheel_spin" && step.Kind == StorySequenceStepKind.DollyTo)
                {
                    float destination = -(1800 + (selected + .5f) * 360 / roster.Length);
                    float start = Time.unscaledTime;
                    while (Time.unscaledTime - start < step.Seconds)
                    {
                        float t = Mathf.Clamp01((Time.unscaledTime - start) / step.Seconds);
                        _Pointer.localRotation = Quaternion.Euler(0, 0, destination * (1 - Mathf.Pow(1 - t, 4)));
                        yield return null;
                    }
                    _Pointer.localRotation = Quaternion.Euler(0, 0, destination);
                    _Graphic.Selected = selected;
                    _Graphic.SetVerticesDirty();
                    _Title.text = "THE GHOST HAS BEEN CHOSEN";
                    _Caption.text = ghost == local ? "You are the ghost" : "Player " + ghost + " is the ghost - You are a fish";
                }
                else if (step.Target == "selected_slice" && step.Kind == StorySequenceStepKind.Knock)
                {
                    for (int pulse = 0; pulse < step.Count; pulse++)
                    {
                        float start = Time.unscaledTime;
                        while (Time.unscaledTime - start < step.Seconds)
                        {
                            float t = Mathf.Clamp01((Time.unscaledTime - start) / step.Seconds);
                            float amount = Mathf.Sin(t * Mathf.PI);
                            _Graphic.SelectionPulse = amount;
                            _Graphic.SetVerticesDirty();
                            _Labels[selected].rectTransform.localScale = Vector3.one * (1 + amount * .15f);
                            yield return null;
                        }
                    }
                    _Graphic.SelectionPulse = 0;
                    _Graphic.SetVerticesDirty();
                    _Labels[selected].rectTransform.localScale = Vector3.one;
                }
                else if (step.Target == "attic_cutscene" && step.Kind == StorySequenceStepKind.SetVisible)
                {
                    _Attic = Instantiate(_AtticPrefab, transform, false);
                    _Attic.Prepare();
                    AddHallwayFish(roster);
                    _Root.SetActive(false);
                    float hallwayLook = GameplayPresentationPolicy.Finite(_HallwayLookSeconds) ? Mathf.Max(0, _HallwayLookSeconds) : GameplayPresentationPolicy.DefaultHallwayLookSeconds;
                    if (hallwayLook > 0) yield return new WaitForSecondsRealtime(hallwayLook);
                }
                else if (_Attic && step.Target == "hotel_bottom" && step.Kind == StorySequenceStepKind.DollyTo)
                {
                    yield return Descend(step);
                    break;
                }
                else if (_Attic) yield return _Attic.Play(step);
                else if (step.Kind == StorySequenceStepKind.Wait) yield return new WaitForSecondsRealtime(step.Seconds);
                else throw new InvalidOperationException("Unbound round operation: " + step.Target);
            }
            var assets = _AtticPrefab.GetComponent<AtticTransitionAssets>();
            var howToPrefab = assets ? (local == ghost ? assets.GhostHowToPrefab : assets.FishHowToPrefab) : null;
            if (!howToPrefab) throw new InvalidOperationException("Bind both authored role how-to pages.");
            finished();
            _HowTo = Instantiate(howToPrefab, transform, false);
            _HowTo.Begin(local, ghost, ready);
            // Keep the page visible until replicated all-ready/setup state closes presentation.
            while (_HowTo) yield return null;
        }

        static IEnumerable<StorySequenceStep> FilterAtticSteps(IReadOnlyList<StorySequenceStep> steps)
        {
            foreach (var step in steps)
                if (step.Target != "wheel_spin" && step.Target != "selected_slice" && step.Target != "attic_cutscene") yield return step;
        }

        void CloseHowTo()
        {
            if (!_HowTo) return;
            _HowTo.Cancel(); _HowTo.gameObject.SetActive(false); Destroy(_HowTo.gameObject); _HowTo = null;
        }
        public void Close()
        {
            StopAllCoroutines();
            CloseHowTo();
            CloseAttic();
            if (_Pointer) _Pointer.localRotation = Quaternion.identity;
            if (_Graphic) { _Graphic.Selected = -1; _Graphic.SelectionPulse = 0; _Graphic.SetVerticesDirty(); }
            if (_Root) _Root.SetActive(false);
            foreach (var label in _Labels) if (label) Destroy(label.gameObject);
            _Labels.Clear();
        }
        void OnDisable() => Close();
    }
}
