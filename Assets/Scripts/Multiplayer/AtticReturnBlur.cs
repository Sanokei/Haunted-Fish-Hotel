using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // Ink toggles an authored effect object around a cinematic move. Direction/strength
    // live in its saved material; this component has no camera or gameplay subscriptions.
    public sealed class AtticReturnBlur : MonoBehaviour
    {
        [SerializeField] Graphic[] _Graphics;
        [SerializeField] Material _Material;
        Material[] _Previous;
        Graphic[] _Applied;
        void OnEnable()
        {
            if (!_Material || _Graphics == null || _Previous != null) return;
            _Applied = (Graphic[])_Graphics.Clone();
            _Previous = new Material[_Graphics.Length];
            for (int i = 0; i < _Applied.Length; i++)
                if (_Applied[i]) { _Previous[i] = _Applied[i].material; _Applied[i].material = _Material; }
        }
        void OnDisable()
        {
            if (_Previous == null) return;
            for (int i = 0; i < _Applied.Length; i++)
                if (_Applied[i]) _Applied[i].material = _Previous[i];
            _Previous = null;
            _Applied = null;
        }
    }
}
