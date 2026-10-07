using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HauntedFish.Multiplayer
{
    [DefaultExecutionOrder(-900)]
    public sealed class GameUIEventOwnership : MonoBehaviour
    {
        [SerializeField] EventSystem _Events;
        [SerializeField] InputSystemUIInputModule _Input;
        EventSystem _PersistentEvents;
        InputSystemUIInputModule _PersistentInput;
        void Awake() => Refresh();
        void Update() => Refresh();
        void Refresh()
        {
            if (!_Events || !_Input) return;
            // EventSystem already tracks its current enabled provider. Cache a
            // persistent provider on transition rather than scanning all systems.
            var current = EventSystem.current;
            if (current && current != _Events && current != _PersistentEvents &&
                current.gameObject.scene.name == "DontDestroyOnLoad")
            {
                _PersistentEvents = current;
                _PersistentInput = current.GetComponent<InputSystemUIInputModule>();
            }
            bool persistent = _PersistentEvents && _PersistentEvents.isActiveAndEnabled &&
                _PersistentInput && _PersistentInput.isActiveAndEnabled;
            if (_Input.enabled == persistent) _Input.enabled = !persistent;
            if (_Events.enabled == persistent) _Events.enabled = !persistent;
        }
    }
}
