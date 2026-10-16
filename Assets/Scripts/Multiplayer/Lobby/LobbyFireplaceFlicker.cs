using UnityEngine;
namespace HauntedFish.Multiplayer {
 // Fire light: layered noise drives intensity, colour temperature and a small positional sway, so the
 // room pulses and the shadows crawl like a real flame rather than breathing evenly.
 public sealed class LobbyFireplaceFlicker : MonoBehaviour {
  [SerializeField] Light _Hearth, _Bounce;
  [SerializeField] AudioSource _Crackle;
  // The introduction has authored audio of its own; the hearth stays silent while it plays.
  public static bool CrackleMuted;
  [SerializeField,Range(0, .6f)] float _HearthVariation=.4f;
  [SerializeField,Range(0, .3f)] float _BounceVariation=.18f;
  [SerializeField] Color _EmberColor=new Color(1f,.32f,.06f), _FlameColor=new Color(1f,.62f,.2f);
  [SerializeField,Min(0)] float _Sway=.05f;
  [SerializeField] float _Seed=29.87f;
  float _HearthIntensity,_BounceIntensity,_Volume,_EnabledAt;Color _HearthColor,_BounceColor;Vector3 _HearthPosition;bool _OwnsState;
  void OnEnable(){if(!Application.isPlaying||!_Hearth||!_Bounce)return;_HearthIntensity=_Hearth.intensity;_BounceIntensity=_Bounce.intensity;_HearthColor=_Hearth.color;_BounceColor=_Bounce.color;_HearthPosition=_Hearth.transform.localPosition;_EnabledAt=Time.unscaledTime;_OwnsState=true;if(_Crackle){_Volume=_Crackle.volume;if(_Crackle.clip){_Crackle.volume=0;_Crackle.Play();}}}
  void Update(){
   if(!_OwnsState)return;float t=Time.unscaledTime;
   // Slow swell, mid flutter and a quick crackle; sharp dips read as the flame guttering.
   float slow=Mathf.PerlinNoise(_Seed,t*.9f),mid=Mathf.PerlinNoise(_Seed+11,t*3.1f),fast=Mathf.PerlinNoise(_Seed+23,t*9f);
   float n=(slow*.5f+mid*.35f+fast*.15f-.5f)*2;
   _Hearth.intensity=_HearthIntensity*(1+n*_HearthVariation);
   _Bounce.intensity=_BounceIntensity*(1+n*_BounceVariation);
   float heat=Mathf.Clamp01(n*.5f+.5f);
   var fire=Color.Lerp(_EmberColor,_FlameColor,heat);
   _Hearth.color=fire;_Bounce.color=Color.Lerp(_BounceColor,fire,.5f);
   _Hearth.transform.localPosition=_HearthPosition+new Vector3(Mathf.PerlinNoise(_Seed+37,t*2.3f)-.5f,(Mathf.PerlinNoise(_Seed+53,t*3.7f)-.5f)*.6f,Mathf.PerlinNoise(_Seed+71,t*2.9f)-.5f)*(2*_Sway);
   if(_Crackle&&_Crackle.isPlaying)_Crackle.volume=CrackleMuted?0:_Volume*Mathf.Clamp01((t-_EnabledAt)/.75f);
  }
  void OnDisable(){if(!_OwnsState)return;if(_Hearth){_Hearth.intensity=_HearthIntensity;_Hearth.color=_HearthColor;_Hearth.transform.localPosition=_HearthPosition;}if(_Bounce){_Bounce.intensity=_BounceIntensity;_Bounce.color=_BounceColor;}if(_Crackle){_Crackle.Stop();_Crackle.volume=_Volume;}_OwnsState=false;}
 }
}
