// STAGED ONLY: manual publication and native authoring require the next exclusive Lobby window.
using UnityEngine;
namespace HauntedFish.Multiplayer {
 public sealed class LobbyFireplaceFlicker : MonoBehaviour {
  [SerializeField] Light _Hearth, _Bounce;
  [SerializeField] AudioSource _Crackle;
  [SerializeField,Range(0, .3f)] float _HearthVariation=.18f;
  [SerializeField,Range(0, .15f)] float _BounceVariation=.06f;
  [SerializeField] float _Seed=29.87f;
  float _HearthIntensity,_BounceIntensity,_Volume,_EnabledAt;bool _OwnsState;
  void OnEnable(){if(!Application.isPlaying||!_Hearth||!_Bounce)return;_HearthIntensity=_Hearth.intensity;_BounceIntensity=_Bounce.intensity;_EnabledAt=Time.unscaledTime;_OwnsState=true;if(_Crackle){_Volume=_Crackle.volume;if(_Crackle.clip){_Crackle.volume=0;_Crackle.Play();}}}
  void Update(){if(!_OwnsState)return;float t=Time.unscaledTime;float n=(Mathf.PerlinNoise(_Seed,t*1.3f)*.65f+Mathf.PerlinNoise(_Seed+11,t*2.7f)*.35f-.5f)*2;_Hearth.intensity=_HearthIntensity*(1+n*_HearthVariation);_Bounce.intensity=_BounceIntensity*(1+n*_BounceVariation);if(_Crackle&&_Crackle.isPlaying)_Crackle.volume=_Volume*Mathf.Clamp01((t-_EnabledAt)/.75f);}
  void OnDisable(){if(!_OwnsState)return;if(_Hearth)_Hearth.intensity=_HearthIntensity;if(_Bounce)_Bounce.intensity=_BounceIntensity;if(_Crackle){_Crackle.Stop();_Crackle.volume=_Volume;}_OwnsState=false;}
 }
}
