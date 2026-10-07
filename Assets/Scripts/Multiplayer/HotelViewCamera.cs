using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    // One authored output-camera binding per hotel scene. Cinemachine still selects
    // and follows this client's owned player; presentations never search camera tags.
    [DefaultExecutionOrder(-1100)]
    [RequireComponent(typeof(Camera))]
    public sealed class HotelViewCamera : MonoBehaviour
    {
        [SerializeField]
        Camera _Output;
        static readonly List<HotelViewCamera> _Views = new List<HotelViewCamera>();
        static Camera _Current;
        bool _WasAvailable;
        public static Camera Current => _Current && _Current.isActiveAndEnabled ? _Current : null;

        public static event Action<Camera> Changed;
        public Camera Output => _Output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _Views.Clear();
            _Current = null;
            Changed = null;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void Awake()
        {
            if (!_Output)
                _Output = GetComponent<Camera>();
        }

        void OnEnable()
        {
            if (_Views.Count == 0)
            {
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }

            _Views.Add(this);
            _WasAvailable = Available;
            SelectView();
        }

        void OnDisable()
        {
            _Views.Remove(this);
            SelectView();
            if (_Views.Count == 0)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
        }

        bool Available => isActiveAndEnabled && _Output && _Output.isActiveAndEnabled;

        void Update()
        {
            // A camera can be disabled independently of its scene binding. Observe
            // that transition once here rather than retrying discovery in consumers.
            bool available = Available;
            if (_WasAvailable == available)
                return;
            _WasAvailable = available;
            SelectView();
        }

        public void BindOutput(Camera output)
        {
            if (ReferenceEquals(_Output, output))
                return;
            _Output = output;
            _WasAvailable = Available;
            if (isActiveAndEnabled)
                SelectView();
        }

        static void OnActiveSceneChanged(Scene previous, Scene current) => SelectView();
        static void OnSceneUnloaded(Scene scene) => SelectView();
        static void SelectView()
        {
            Camera selected = null;
            var activeScene = SceneManager.GetActiveScene();
            // Prefer the active scene; a newly loaded hotel scene can publish its
            // camera before scene travel makes it active. Unloading removes it.
            for (int i = _Views.Count - 1; i >= 0; --i)
            {
                var view = _Views[i];
                if (!view || !view.Available)
                    continue;
                if (!selected)
                    selected = view._Output;
                if (view.gameObject.scene == activeScene)
                {
                    selected = view._Output;
                    break;
                }
            }

            if (ReferenceEquals(_Current, selected))
                return;
            _Current = selected;
            Changed?.Invoke(Current);
        }
    }
}
