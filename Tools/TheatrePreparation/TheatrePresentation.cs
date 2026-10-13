using System;
using System.Collections;
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
        [SerializeField] VideoPlayer _Video;
        [SerializeField] VideoClip _FishVideo,_GhostVideo;
        [SerializeField] TMP_Text _Status,_Countdown;
        [SerializeField] Image _CountdownRing;
        [SerializeField] GameObject _CountdownRoot;
        [SerializeField] Image _FishTabBackground,_GhostTabBackground;
        [SerializeField] TMP_Text _FishTabLabel,_GhostTabLabel;
        bool _Returning;
        [SerializeField] float _MinX=-9,_MaxX=9,_ExitX=-8.5f,_Speed=3;
        HotelPlayer _Owner; HauntedHotelMultiplayer _Session; Coroutine _Playback;
        Vector3 _WalkerOrigin; bool _CursorVisible;CursorLockMode _CursorLock; bool _Open,_ExitSent,_ResumeVideo,_QueuedGhost; int _ClipIndex;
        public event Action ExitRequested;
        public Camera View=>_Camera;
        public bool ValidateAuthoredBindings(out string error)
        {
            error=null;
            if(!_Camera||!_Walker||!_OwnSilhouette||!_Video||!_FishVideo||!_GhostVideo||!_Status||!_Countdown||!_CountdownRing||!_CountdownRoot){error="Assign authored theatre camera, silhouette, both videos and playback/countdown UI.";return false;}
            if(_Video.renderMode!=VideoRenderMode.RenderTexture||!_Video.targetTexture){error="Assign authored video RenderTexture.";return false;}
            if(_MinX>=_ExitX||_ExitX>=_MaxX){error="Exit must lie within authored horizontal lane.";return false;}
            return true;
        }
        void Awake(){_WalkerOrigin=_Walker?_Walker.localPosition:Vector3.zero;}
        void OnEnable(){if(_Video){_Video.errorReceived+=OnError;_Video.loopPointReached+=OnEnd;}}
        void OnDisable(){Close();if(_Video){_Video.errorReceived-=OnError;_Video.loopPointReached-=OnEnd;}}
        public void Open(HotelPlayer owner,HauntedHotelMultiplayer session)
        {
            if(!owner||owner!=HotelPlayer.LocalPlayer||!owner.IsRelevantPlayer)throw new InvalidOperationException("Theatre presentation requires the owned local player.");
            if(!ValidateAuthoredBindings(out string error))throw new InvalidOperationException(error);
            Close();_CursorVisible=Cursor.visible;_CursorLock=Cursor.lockState;_Owner=owner;_Session=session;_Open=true;_ExitSent=false;
            _Walker.localPosition=_WalkerOrigin;
            // Reuse that player's actual art in the authored proxy, without changing the network visual.
            foreach(var sprite in owner.GetComponentsInChildren<SpriteRenderer>(true))
                if(sprite.sprite&&sprite.name.IndexOf("Body",StringComparison.OrdinalIgnoreCase)>=0){_OwnSilhouette.sprite=sprite.sprite;break;}
            _OwnSilhouette.color=Color.black;PlayFish();
        }
        public void Close()
        {
            if(_Open){bool menu=_Session&&_Session.MemberMenuOpen;Cursor.visible=menu||_CursorVisible;Cursor.lockState=menu?CursorLockMode.None:_CursorLock;}
            _Returning=false;_Open=false;_Owner=null;_Session=null;_ResumeVideo=_QueuedGhost=false;
            if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}
            if(_Video)_Video.Stop();if(_CountdownRoot)_CountdownRoot.SetActive(false);
        }
        void Update()
        {
            if(!_Open||_Returning)return;
            if(!_Owner||_Owner!=HotelPlayer.LocalPlayer||!_Owner.IsRelevantPlayer){RequestExit();return;}
            bool blocked=(_Session&&(_Session.MemberMenuOpen||_Session.IntroductionPlaying||_Session.Transitioning))||_Owner.Movement.SharedDialogueLocked;
            if(blocked){if(_Video.isPlaying){_ResumeVideo=true;_Video.Pause();}return;}
            if(_ResumeVideo){_ResumeVideo=false;_Video.Play();}if(_QueuedGhost){_QueuedGhost=false;PlayGhost();}
            var keys=Keyboard.current;float axis=keys==null?0:(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0);
            var point=_Walker.localPosition;point.x=TheatreVisit.Move(point.x,axis,Time.unscaledDeltaTime,_MinX,_MaxX,_Speed);
            point.y=_WalkerOrigin.y;point.z=_WalkerOrigin.z;_Walker.localPosition=point;
            if(axis!=0)_OwnSilhouette.flipX=axis<0;
            if(point.x<=_ExitX)RequestExit();
        }
        void LateUpdate(){if(_Open){Cursor.visible=true;Cursor.lockState=CursorLockMode.None;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);}}
        void RequestExit(){if(_ExitSent)return;_ExitSent=true;ExitRequested?.Invoke();}
        public void PlayFish()=>Play(0);
        public void PlayGhost()=>Play(1);
        void Play(int index)
        {
            if(!_Open||_Returning||(_Session&&_Session.MemberMenuOpen))return;
            if(_Playback!=null)StopCoroutine(_Playback);
            _ClipIndex=index;ApplySelectedTab();_Playback=StartCoroutine(PrepareAndPlay(index==0?_FishVideo:_GhostVideo));
        }
        public void ShowReturnPending()
        {
            _Returning=true;if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}
            _Video.Pause();_CountdownRoot.SetActive(false);_Status.text="Returning to Lobby...";
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
            _Video.Stop();_Video.clip=clip;_Video.isLooping=false;_Video.Prepare();_Status.text=_ClipIndex==0?"PLAYER TUTORIAL":"GHOST TUTORIAL";
            float began=Time.realtimeSinceStartup;
            while(!_Video.isPrepared&&Time.realtimeSinceStartup-began<15)yield return null;
            if(!_Video.isPrepared){_Status.text="Video could not prepare. Choose Player or Ghost to retry.";_Playback=null;yield break;}
            _CountdownRoot.SetActive(true);float elapsed=0;
            while(elapsed<3)
            {
                if(!_Session||!_Session.MemberMenuOpen)elapsed+=Time.unscaledDeltaTime;
                _Countdown.text=TheatreVisit.Countdown(elapsed).ToString();_CountdownRing.fillAmount=1-(elapsed%1);yield return null;
            }
            _CountdownRoot.SetActive(false);_Video.Play();_Playback=null;
        }
        void OnEnd(VideoPlayer video){if(_Open&&_ClipIndex==0){if(_Session&&_Session.MemberMenuOpen)_QueuedGhost=true;else PlayGhost();}else if(_Open)_Status.text="Both tutorials complete. Replay either, or walk left to exit.";}
        void OnError(VideoPlayer video,string message){if(_Playback!=null){StopCoroutine(_Playback);_Playback=null;}_CountdownRoot.SetActive(false);_Status.text="Video playback failed. Choose a tutorial to retry.";Debug.LogError("Theatre video: "+message,this);}
    }
}