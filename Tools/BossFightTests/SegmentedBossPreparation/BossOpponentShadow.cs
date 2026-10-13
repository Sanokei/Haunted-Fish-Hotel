using UnityEngine;
namespace HauntedFish.BossFight
{
    // Reuses the attic template's ONE authored shadow renderer. The shared storm
    // owns strikes, sprite copying and light timing; this adapter owns wall placement.
    public sealed class BossOpponentShadow : MonoBehaviour
    {
        [SerializeField] LightningEffectManager _Lightning;
        [SerializeField] BossSegmentedRig _GhostRig;
        [SerializeField] Light _RoomPulse;
        [SerializeField] SpriteRenderer _Shadow,_FishOpponent,_GhostOpponent;
        [SerializeField] Transform _FishWallAnchor,_GhostWallAnchor;
        [SerializeField] float _PeakIntensity=1.35f,_Enlargement=2.3f;
        [SerializeField,Range(0,1)] float _Opacity=.8f;
        BossViewerSide _Side;
        bool _Watching;
        SpriteRenderer Opponent=>_Side==BossViewerSide.Ghost?_FishOpponent:_GhostRig?_GhostRig.ShadowSource:_GhostOpponent;
        public void SetViewer(BossViewerSide side,bool watching)
        {
            _Side=side;_Watching=watching;
            if(_Lightning)_Lightning.SetOpponentRenderer(watching?Opponent:null);
            if(!watching&&_Shadow)_Shadow.enabled=false;
        }
        void LateUpdate()
        {
            if(!_Watching||!_Shadow||!_Shadow.enabled||!Opponent)return;
            var anchor=_Side==BossViewerSide.Ghost?_FishWallAnchor:_GhostWallAnchor;
            if(anchor)_Shadow.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
            _Shadow.transform.localScale=Opponent.transform.lossyScale*_Enlargement;
            float envelope=_RoomPulse&&_RoomPulse.enabled?Mathf.Clamp01(_RoomPulse.intensity/Mathf.Max(.01f,_PeakIntensity)):0;
            _Shadow.color=new Color(0,0,0,envelope*_Opacity);
        }
        void OnDisable(){_Watching=false;if(_Shadow)_Shadow.enabled=false;}
    }
}