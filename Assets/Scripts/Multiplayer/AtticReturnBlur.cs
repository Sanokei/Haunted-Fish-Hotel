using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // Ink toggles this authored object only for the fast downward camera pan.
    public sealed class AtticReturnBlur : MonoBehaviour
    {
        [SerializeField] Graphic[] _Graphics;
        [SerializeField] Material _Material;
        Material[] _Previous;
        void OnEnable()
        {
            _Previous = new Material[_Graphics.Length];
            for (int i = 0; i < _Graphics.Length; i++)
                if (_Graphics[i]) { _Previous[i] = _Graphics[i].material; _Graphics[i].material = _Material; }
        }
        void OnDisable()
        {
            if (_Previous == null) return;
            for (int i = 0; i < _Graphics.Length; i++)
                if (_Graphics[i]) _Graphics[i].material = _Previous[i];
            _Previous = null;
        }
    }
}
