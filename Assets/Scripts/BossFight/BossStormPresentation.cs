using System.Collections.Generic;
using HauntedFish.Multiplayer;
using UnityEngine;
namespace HauntedFish.BossFight
{
    public sealed class BossStormPresentation : MonoBehaviour
    {
        [SerializeField] LightningEffectManager _Lightning;
        [SerializeField] LobbyStormAudio _Audio;
        [SerializeField] GameObject _LightningRoot,_AudioRoot;
        [SerializeField] GameObject[] _StageLights;
        readonly List<LightningEffectManager> _SuspendedGameLights=new List<LightningEffectManager>();
        bool _Watching;
        void Awake() => SetStageLights(false);
        void SetStageLights(bool active)
        {
            if (_StageLights == null) return;
            foreach (var light in _StageLights)
            {
                if (light) light.SetActive(active);
            }
        }
        public void SetWatching(bool watching)
        {
            if(!Application.isPlaying||_Watching==watching)return;
            if(watching)
            {
                if(!_Lightning||!_Audio||!_LightningRoot||!_AudioRoot)
                {Debug.LogError("Boss attic storm has incomplete authored lifecycle bindings.",this);return;}
                if(!_Lightning.ValidateAuthoredBindings(out string error))
                {Debug.LogError("Boss attic storm binding validation failed: "+error,this);return;}
            }
            _Watching=watching;
            SetStageLights(watching);
            if(watching)
            {
                _SuspendedGameLights.Clear();
                foreach(var light in FindObjectsByType<LightningEffectManager>(FindObjectsSortMode.None))
                    if(light!=_Lightning&&light.gameObject.scene.name=="Game"&&light.AmbientActive){_SuspendedGameLights.Add(light);light.StopAmbient();}
                foreach(var audio in FindObjectsByType<LobbyStormAudio>(FindObjectsSortMode.None))
                    if(audio!=_Audio&&audio.gameObject.scene.name=="Game")audio.StopAudio();
                if(_LightningRoot)_LightningRoot.SetActive(true);
                if(_AudioRoot)_AudioRoot.SetActive(true);
                if(_Lightning)_Lightning.StartAmbient();
            }
            else
            {
                if(_Lightning)_Lightning.StopAmbient();if(_Audio)_Audio.StopAudio();
                if(_AudioRoot)_AudioRoot.SetActive(false);if(_LightningRoot)_LightningRoot.SetActive(false);
                foreach(var light in _SuspendedGameLights)if(light&&light.isActiveAndEnabled)light.StartAmbient();
                _SuspendedGameLights.Clear();
            }
        }
        void OnDisable()
        {
            if (_Watching) SetWatching(false);
            SetStageLights(false);
        }
    }
}
