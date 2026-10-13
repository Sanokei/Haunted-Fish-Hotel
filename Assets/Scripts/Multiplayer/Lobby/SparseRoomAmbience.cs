using System;
using Monologue.Dialogue;
using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Source hierarchy, clips and gains are authored in a prefab. No runtime widgets/objects.
    public sealed class SparseRoomAmbience : MonoBehaviour
    {
        [SerializeField] AudioSource _QuietRoomSource, _AirSource, _AccentSource;
        [SerializeField] AudioClip _QuietRoomLoop, _DistantAirLoop, _HollowSwirl, _SoftTimber;
        [SerializeField,Range(0,1)] float _RoomGain=1, _AirGain=.60f, _SwirlGain=.55f, _TimberGain=.45f, _BusGain=.8f;
        [SerializeField] float _DialogueDuckDb=-4.5f;
        [SerializeField,Min(.01f)] float _DuckAttackSeconds=.3f, _DuckReleaseSeconds=1.2f, _FadeInSeconds=1;
        [SerializeField] bool _StartOnEnable=true;
        readonly SparseAmbienceSchedule _Schedule=new SparseAmbienceSchedule();
        bool _Subscribed, _Dialogue, _CinematicDuck;
        float _Envelope,_Duck=1,_DuckFrom=1,_DuckTarget=1,_DuckElapsed,_DuckDuration,_AccentGain;
        public bool Running=>_Schedule.Running;
        public bool CinematicDucked=>_CinematicDuck;
        public bool ValidateBindings(out string error)
        {
            error=null;
            if(!_QuietRoomSource||!_AirSource||!_AccentSource||_QuietRoomSource==_AirSource||
                _QuietRoomSource==_AccentSource||_AirSource==_AccentSource)
            {error="Assign three distinct prefab-authored ambience sources.";return false;}
            if(!_QuietRoomLoop||!_DistantAirLoop||!_HollowSwirl||!_SoftTimber)
            {error="Assign the four verified licensed ambience stems.";return false;}
            return true;
        }
        public void BeginAmbience()
        {
            if(Running)return;
            if(!ValidateBindings(out var error)){Debug.LogError(error,this);return;}
            if(!_Subscribed){DialogueManager.OnDialogueStartEvent+=DialogueStarted;DialogueManager.OnDialogueEndEvent+=DialogueEnded;_Subscribed=true;}
            _Dialogue=DialogueManager.Instance&&DialogueManager.Instance.ActiveDialoguePanel;
            _Duck=_DuckFrom=_DuckTarget=(_Dialogue||_CinematicDuck)?Mathf.Pow(10,_DialogueDuckDb/20):1;
            _DuckElapsed=0;_Envelope=0;
            Configure(_QuietRoomSource,true);Configure(_AirSource,true);Configure(_AccentSource,false);
            _QuietRoomSource.clip=_QuietRoomLoop;_AirSource.clip=_DistantAirLoop;
            _QuietRoomSource.Play();_AirSource.Play();
            _Schedule.Begin(Time.unscaledTime,(min,max)=>UnityEngine.Random.Range((float)min,(float)max));
        }
        static void Configure(AudioSource source,bool loop)
        {source.Stop();source.playOnAwake=false;source.loop=loop;source.pitch=1;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;}
        void DialogueStarted(){_Dialogue=true;DuckChanged();}
        void DialogueEnded(){_Dialogue=false;DuckChanged();}
        public void SetCinematicDuck(bool active){_CinematicDuck=active;DuckChanged();}
        void DuckChanged()
        {
            float target=(_Dialogue||_CinematicDuck)?Mathf.Pow(10,_DialogueDuckDb/20):1;
            if(Mathf.Approximately(target,_DuckTarget))return;
            _DuckFrom=_Duck;_DuckTarget=target;_DuckElapsed=0;
            _DuckDuration=target<_Duck?_DuckAttackSeconds:_DuckReleaseSeconds;
        }
        void Update()
        {
            if(!Running)return;
            _DuckElapsed+=Time.unscaledDeltaTime;
            _Duck=Mathf.Lerp(_DuckFrom,_DuckTarget,Mathf.Clamp01(_DuckElapsed/Mathf.Max(.01f,_DuckDuration)));
            _Envelope=Mathf.MoveTowards(_Envelope,1,Time.unscaledDeltaTime/Mathf.Max(.01f,_FadeInSeconds));
            float bus=_BusGain*_Duck*_Envelope;
            _QuietRoomSource.volume=bus*_RoomGain;_AirSource.volume=bus*_AirGain;
            var accent=_Schedule.Take(Time.unscaledTime,_AccentSource.isPlaying,_HollowSwirl.length,_SoftTimber.length);
            if(accent!=SparseAmbienceSchedule.Accent.None)
            {
                bool swirl=accent==SparseAmbienceSchedule.Accent.Swirl;
                _AccentSource.clip=swirl?_HollowSwirl:_SoftTimber;
                _AccentGain=swirl?_SwirlGain:_TimberGain;
                _AccentSource.volume=bus*_AccentGain;_AccentSource.Play();
            }
            _AccentSource.volume=bus*_AccentGain;
        }
        public void StopAmbience()
        {
            _Schedule.Stop();
            if(_Subscribed){DialogueManager.OnDialogueStartEvent-=DialogueStarted;DialogueManager.OnDialogueEndEvent-=DialogueEnded;_Subscribed=false;}
            foreach(var source in new[]{_QuietRoomSource,_AirSource,_AccentSource})if(source){source.Stop();source.volume=0;}
            _Envelope=_AccentGain=0;_CinematicDuck=_Dialogue=false;_Duck=_DuckFrom=_DuckTarget=1;_DuckElapsed=0;
        }
        void OnEnable(){if(_StartOnEnable)BeginAmbience();}
        void OnDisable()=>StopAmbience();
        void OnDestroy()=>StopAmbience();
    }
}
