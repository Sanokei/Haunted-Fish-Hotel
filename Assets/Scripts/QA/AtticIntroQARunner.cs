using System.Collections;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine;

namespace HauntedFish.QA
{
    // QA only: plays the authored attic Ink through the cutscene prefab at a fixed 0.1s per frame and
    // renders each frame offscreen, so the capture is identical whether or not the editor has focus.
    public sealed class AtticIntroQARunner : MonoBehaviour
    {
        [SerializeField] StoryUI _Prefab;
        [SerializeField] float _FrameInterval = .1f;
        [SerializeField] Vector2Int _Resolution = new Vector2Int(1280, 720);
        [SerializeField] string _OutputFolder = "QA/AtticIntro";

        string _Folder, _Label = "start";
        int _Frame;
        bool _Waiting, _WaitCaptured;
        Camera _Camera;
        RenderTexture _Target;
        Texture2D _Read;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Time.captureDeltaTime = _FrameInterval;
            _Folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), _OutputFolder);
            System.IO.Directory.CreateDirectory(_Folder);
            foreach (var old in System.IO.Directory.GetFiles(_Folder, "*.png")) System.IO.File.Delete(old);
            _Target = new RenderTexture(_Resolution.x, _Resolution.y, 24);
            _Read = new Texture2D(_Resolution.x, _Resolution.y, TextureFormat.RGB24, false);
            _Camera = Camera.main ? Camera.main : FindAnyObjectByType<Camera>();
            _Camera.targetTexture = _Target;
            _Camera.enabled = false;

            var settings = _Prefab.GetComponent<RoundIntroductionSettings>();
            float scale = settings ? settings.TimingScale : 1;
            var attic = Instantiate(_Prefab, transform, false);
            var canvas = attic.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _Camera;
            canvas.planeDistance = 1;
            attic.Prepare();
            foreach (var step in StoryFunctions.ReadAnimationSequence(settings.Sequence.text))
            {
                if (step.Target == "wheel_spin" || step.Target == "selected_slice" || step.Target == "attic_cutscene") continue;
                _Label = step.Kind + "-" + step.Target + "-" + step.Variant + "-" + step.Seconds;
                // Realtime waits hold a still image; keep one frame of each instead of hundreds.
                _Waiting = step.Kind == StorySequenceStepKind.Wait; _WaitCaptured = false;
                Debug.Log($"[AtticQA] frame {_Frame}: {_Label}");
                yield return attic.Play(new StorySequenceStep(step.Kind, step.Target, step.Variant, step.Seconds * scale,
                    step.X, step.Y, step.Count, step.Visible, step.Parent, step.Easing));
            }
            _Waiting = false; _Label = "end";
            yield return null; yield return null;
            Time.captureDeltaTime = 0;
            Debug.Log($"[AtticQA] finished, {_Frame} frames in {_Folder}");
            enabled = false;
        }

        void LateUpdate()
        {
            if (!_Camera || (_Waiting && _WaitCaptured)) return;
            _WaitCaptured = _Waiting;
            Canvas.ForceUpdateCanvases();
            _Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _Target;
            _Read.ReadPixels(new Rect(0, 0, _Resolution.x, _Resolution.y), 0, 0);
            RenderTexture.active = previous;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_Folder, $"{_Frame++:000}_{Time.time:00.0}s_{_Label}.png"), _Read.EncodeToPNG());
        }
    }
}
