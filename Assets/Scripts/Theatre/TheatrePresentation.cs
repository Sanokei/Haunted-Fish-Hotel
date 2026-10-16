using System;
using System.Collections;
using System.Collections.Generic;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;
namespace HauntedFish.Theatre
{
    // All actors, screen, playback UI and input presentation are scene-authored.
    public sealed class TheatrePresentation : MonoBehaviour
    {
        [SerializeField] Camera _Camera;
        [SerializeField] Transform _Walker;
        [SerializeField] SpriteRenderer _OwnSilhouette;
        [SerializeField] SpriteRenderer[] _ConnectedSilhouettes;
        Vector3 _CameraOrigin;float _SilhouetteHeight;
        public int VisibleRealPlayerSilhouetteCount=>_Open?((_OwnSilhouette&&_OwnSilhouette.enabled&&_OwnSilhouette.sprite)?1:0)+ConnectedVisible():0;
        int ConnectedVisible(){int count=0;if(_ConnectedSilhouettes!=null)foreach(var r in _ConnectedSilhouettes)if(r&&r.enabled)count++;return count;}
        [SerializeField] VideoPlayer _Video;
        [SerializeField] VideoClip _FishVideo,_GhostVideo;
        [SerializeField] TMP_Text _Status,_Countdown;
        [SerializeField] Image _CountdownRing;
        [SerializeField] GameObject _CountdownRoot;
        [SerializeField] Image _FishTabBackground,_GhostTabBackground;
        [SerializeField] TMP_Text _FishTabLabel,_GhostTabLabel;
        bool _Returning;
        // Theatre owns the soundscape while open: lobby radio, fire and storm are muted, not stopped, so they resume in place.
        [SerializeField] AudioSource _ProjectorAudio,_MusicAudio;
        readonly List<AudioSource> _MutedOutside=new List<AudioSource>();float _NextMuteScan;
        [SerializeField] float _MinX=-9,_MaxX=9,_ExitX=-8.5f,_Speed=3;
        HotelPlayer _Owner; HauntedHotelMultiplayer _Session; Coroutine _Playback;
        Vector3 _WalkerOrigin; bool _CursorVisible;CursorLockMode _CursorLock; bool _Open,_ExitSent,_ResumeVideo; int _ClipIndex,_QueuedRole=-1;
        public event Action ExitRequested;
        public Camera View=>_Camera;
        public bool ValidateAuthoredBindings(out string error)
        {
            error=null;
            if(!_Camera||!_Walker||!_OwnSilhouette||!_Video||!_FishVideo||!_GhostVideo){error="Assign authored theatre camera, silhouette, both role videos and RenderTexture.";return false;}
            if(_Video.renderMode!=VideoRenderMode.RenderTexture||!_Video.targetTexture){error="Assign authored video RenderTexture.";return false;}
            if(_MinX>=_ExitX||_ExitX>=_MaxX){error="Exit must lie within authored horizontal lane.";return false;}
            return true;
        }
        void Awake(){_WalkerOrigin=_Walker?_Walker.localPosition:Vector3.zero;_CameraOrigin=_Camera?_Camera.transform.localPosition:Vector3.zero;_SilhouetteHeight=_OwnSilhouette?_OwnSilhouette.bounds.size.y:4.2f;}
        void OnEnable(){if(_Video){_Video.errorReceived+=OnError;_Video.loopPointReached+=OnEnd;}}
        void OnDisable(){Close();if(_Video){_Video.errorReceived-=OnError;_Video.loopPointReached-=OnEnd;}}
        public void Open(HotelPlayer owner,HauntedHotelMultiplayer session)
        {
            if(!owner||owner!=HotelPlayer.LocalPlayer||!owner.IsRelevantPlayer)throw new InvalidOperationException("Theatre presentation requires the owned local player.");
            if(!ValidateAuthoredBindings(out string error))throw new InvalidOperationException(error);
            Close();_CursorVisible=Cursor.visible;_CursorLock=Cursor.lockState;_Owner=owner;_Session=session;_Open=true;_ExitSent=false;
            _Walker.localPosition=_WalkerOrigin;
            // Reuse that player's actual art in the authored proxy, without changing the network visual.
            BindRealArt(_OwnSilhouette,owner);_OwnSilhouette.enabled=true;RefreshConnectedSilhouettes();PlayFish();
            MuteOutside();if(_ProjectorAudio){_ProjectorAudio.loop=true;_ProjectorAudio.Play();}if(_MusicAudio){HotelAudioRouting.RouteMusic(_MusicAudio);_MusicAudio.loop=true;_MusicAudio.Play();}
        }
        public void Close()
        {
            if(_Open){bool menu=_Session&&_Session.MemberMenuOpen;Cursor.visible=menu||_CursorVisible;Cursor.lockState=menu?CursorLockMode.None:_CursorLock;}
            _Returning=false;_Open=false;_Owner=null;_Session=null;_ResumeVideo=false;_QueuedRole=-1;
            if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}
            if(_ProjectorAudio)_ProjectorAudio.Stop();if(_MusicAudio)_MusicAudio.Stop();RestoreOutside();
            if(_Video)_Video.Stop();if(_CountdownRoot)_CountdownRoot.SetActive(false);if(_OwnSilhouette)_OwnSilhouette.enabled=false;if(_ConnectedSilhouettes!=null)foreach(var r in _ConnectedSilhouettes)if(r)r.enabled=false;if(_Camera)_Camera.transform.localPosition=_CameraOrigin;
        }
        void Update()
        {
            if(!_Open||_Returning)return;
            if(!_Owner||_Owner!=HotelPlayer.LocalPlayer||!_Owner.IsRelevantPlayer){RequestExit();return;}
            bool blocked=(_Session&&(_Session.MemberMenuOpen||_Session.IntroductionPlaying||_Session.Transitioning))||_Owner.Movement.SharedDialogueLocked;
            if(blocked){if(_Video.isPlaying){_ResumeVideo=true;_Video.Pause();}return;}
            if(_ResumeVideo){_ResumeVideo=false;_Video.Play();}if(_QueuedRole>=0){int next=_QueuedRole;_QueuedRole=-1;Play(next);}
            var keys=Keyboard.current;float axis=keys==null?0:(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0);
            var point=_Walker.localPosition;point.x=TheatreVisit.Move(point.x,axis,Time.unscaledDeltaTime,_MinX,_MaxX,_Speed);
            point.y=_WalkerOrigin.y;point.z=_WalkerOrigin.z;_Walker.localPosition=point;
            if(axis!=0)_OwnSilhouette.flipX=axis<0;
            if(point.x<=_ExitX)RequestExit();
        }
        void MuteOutside()
        {
            _NextMuteScan=Time.unscaledTime+.5f;
            foreach(var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(source&&source.gameObject.scene!=gameObject.scene&&!source.mute&&!source.GetComponentInParent<MemberMenuTutorial>(true)&&!_MutedOutside.Contains(source)){source.mute=true;_MutedOutside.Add(source);}
        }
        void RestoreOutside(){foreach(var source in _MutedOutside)if(source)source.mute=false;_MutedOutside.Clear();}
        void LateUpdate(){if(_Open){if(Time.unscaledTime>=_NextMuteScan)MuteOutside();RefreshConnectedSilhouettes();if(!_Camera.orthographic){var p=_CameraOrigin;p.x+=Mathf.Clamp(_Walker.localPosition.x-_WalkerOrigin.x,-7.5f,7.5f);_Camera.transform.localPosition=p;}Cursor.visible=_Session&&_Session.MemberMenuOpen;Cursor.lockState=CursorLockMode.None;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);}}

        void BindRealArt(SpriteRenderer target,HotelPlayer player){target.sprite=null;foreach(var source in player.GetComponentsInChildren<SpriteRenderer>(true))if(source.sprite&&source.name.IndexOf("Body",StringComparison.OrdinalIgnoreCase)>=0){target.sprite=source.sprite;target.flipX=source.flipX;break;}target.color=Color.black;if(target.sprite){float height=target.sprite.bounds.size.y*Mathf.Abs(target.transform.lossyScale.y);if(height>.001f)target.transform.localScale*=_SilhouetteHeight/height;}}
        void RefreshConnectedSilhouettes(){if(_ConnectedSilhouettes==null)return;int slot=0;if(_Session)foreach(var player in HotelPlayer.ActivePlayers){if(!player||player==_Owner||!player.isActiveAndEnabled||!player.Networked||!player.Identity.IsSpawned)continue;if(slot>=_ConnectedSilhouettes.Length)break;var r=_ConnectedSilhouettes[slot++];if(!r)continue;BindRealArt(r,player);r.enabled=r.sprite;}
            for(;slot<_ConnectedSilhouettes.Length;slot++){var r=_ConnectedSilhouettes[slot];if(r){r.enabled=false;r.sprite=null;}}}
        void RequestExit(){if(_ExitSent)return;_ExitSent=true;ExitRequested?.Invoke();}
        public void PlayFish()=>Play(0);
        public void PlayGhost()=>Play(1);
        void Play(int index)
        {
            if(!_Open||_Returning||(_Session&&_Session.MemberMenuOpen))return;
            if(_Playback!=null)StopCoroutine(_Playback);
            _ClipIndex=index;_Playback=StartCoroutine(PrepareAndPlay(index==0?_FishVideo:_GhostVideo));
        }
        public void ShowReturnPending()
        {
            _Returning=true;if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}
            _Video.Pause();if(_CountdownRoot)_CountdownRoot.SetActive(false);
        }
        void ApplySelectedTab()
        {
            var selected=new Color(.12f,.42f,.45f,.98f);var idle=new Color(.08f,.1f,.12f,.95f);
            if(_FishTabBackground)_FishTabBackground.color=_ClipIndex==0?selected:idle;
            if(_GhostTabBackground)_GhostTabBackground.color=_ClipIndex==1?selected:idle;
            if(_FishTabLabel)_FishTabLabel.fontStyle=_ClipIndex==0?FontStyles.Bold:FontStyles.Normal;
            if(_GhostTabLabel)_GhostTabLabel.fontStyle=_ClipIndex==1?FontStyles.Bold:FontStyles.Normal;
        }
        IEnumerator PrepareAndPlay(VideoClip clip)
        {
            _Video.Stop();_Video.clip=clip;_Video.isLooping=false;_Video.Prepare();
            float began=Time.realtimeSinceStartup;
            while(!_Video.isPrepared&&Time.realtimeSinceStartup-began<15)yield return null;
            if(!_Video.isPrepared){Debug.LogWarning("Cinema film could not prepare; use ESC How To instructions.",this);_Playback=null;yield break;}
            // Cinema starts directly after decoding; no countdown or visible controls.
            if(_CountdownRoot)_CountdownRoot.SetActive(false);_Video.Play();_Playback=null;
        }
        void OnEnd(VideoPlayer video)
        {
            if(!_Open||_Returning)return;int next=1-_ClipIndex;
            if(_Session&&_Session.MemberMenuOpen)_QueuedRole=next;else Play(next);
        }
        void OnError(VideoPlayer video,string message){if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}if(_CountdownRoot)_CountdownRoot.SetActive(false);Debug.LogError("Theatre video: "+message,this);}
    }
}