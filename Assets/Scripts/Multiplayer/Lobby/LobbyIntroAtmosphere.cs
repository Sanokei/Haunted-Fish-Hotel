using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HauntedFish.Multiplayer
{
    // Uses an authored prefab/Volume; creates no objects or profiles at runtime.
    public sealed class LobbyIntroAtmosphere : MonoBehaviour
    {
        [SerializeField] Volume _Volume;
        [SerializeField] Camera _Camera;
        float _Weight;
        bool _PostProcessing, _Active;
        UniversalAdditionalCameraData _Data;
        public void Begin()
        {
            if (_Active || !_Volume || !_Camera) return;
            _Active = true;
            _Weight = _Volume.weight;
            _Data = _Camera.GetComponent<UniversalAdditionalCameraData>();
            if (_Data) { _PostProcessing = _Data.renderPostProcessing; _Data.renderPostProcessing = true; }
            _Volume.weight = 1;
        }
        public void End()
        {
            if (!_Active) return;
            if (_Volume) _Volume.weight = _Weight;
            if (_Data) _Data.renderPostProcessing = _PostProcessing;
            _Active = false; _Data = null;
        }
        void OnDisable() => End();
    }
}
