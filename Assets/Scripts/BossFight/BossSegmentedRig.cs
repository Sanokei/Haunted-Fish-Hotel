using System;
using System.Collections;
using UnityEngine;
namespace HauntedFish.BossFight
{
    [Serializable] public sealed class BossGhostVariant
    {
        public GameObject Root;
        public Transform Head;
        public Transform[] Arms;
        public Renderer[] Artwork;
    }
    // All 27 visible segments and the nonrendering silhouette source are authored, and so is every movement: each
    // variant root carries an Animator whose controller holds Idle, Juggle, Panic, Frustrated, Gloat, Flinch, Defeat
    // and Triumph clips that swing each tentacle about its own shoulder. This component only picks variants and
    // selects states; it has no per-frame code. A variant without an Animator simply holds its authored rest pose.
    public sealed class BossSegmentedRig : MonoBehaviour
    {
        [SerializeField] BossGhostVariant[] _Variants;
        [SerializeField] SpriteRenderer _ShadowSource;
        [SerializeField] Sprite[] _ShadowSprites;
        [SerializeField] Renderer[] _AllArtwork;
        [SerializeField,Range(0,1)] float _PanicHeat=.65f;
        static readonly int Speed=Animator.StringToHash("Speed");
        static readonly int Idle=Animator.StringToHash("Idle"),Juggle=Animator.StringToHash("Juggle"),Panic=Animator.StringToHash("Panic");
        static readonly int Frustrated=Animator.StringToHash("Frustrated"),Gloat=Animator.StringToHash("Gloat"),Flinch=Animator.StringToHash("Flinch");
        static readonly int Defeat=Animator.StringToHash("Defeat"),Triumph=Animator.StringToHash("Triumph");
        struct Pose
        {
            public Transform Transform;public Vector3 Position,Scale;public Quaternion Rotation;
            public void Restore(){Transform.localPosition=Position;Transform.localRotation=Rotation;Transform.localScale=Scale;}
        }
        Pose[][] _Poses;Animator[] _Animators;
        bool _Initialized,_Visible=true;
        int _Index=-1;
        float _ReactionEnd;Coroutine _Panic;
        public int VariantIndex=>_Index;
        public Renderer[] AllArtwork=>_AllArtwork;
        public Renderer[] ActiveArtwork=>_Index>=0?_Variants[_Index].Artwork:Array.Empty<Renderer>();
        public SpriteRenderer ShadowSource=>_ShadowSource;
        public bool ReactionPlaying=>Time.unscaledTime<_ReactionEnd;
        public bool HasAnimatedPose
        {
            get
            {
                if(!_Initialized||_Index<0)return false;
                foreach(var pose in _Poses[_Index])if(Quaternion.Angle(pose.Transform.localRotation,pose.Rotation)>.05f||Vector3.Distance(pose.Transform.localPosition,pose.Position)>.0001f)return true;
                return false;
            }
        }
        public bool ValidateAuthoredBindings(out string error)
        {
            error=null;
            if(_Variants==null||_Variants.Length!=3||!_ShadowSource||_ShadowSprites==null||_ShadowSprites.Length!=3||_AllArtwork==null||_AllArtwork.Length!=27)
            {error="Author three variants, 27 visible segments and three silhouette references.";return false;}
            for(int i=0;i<3;i++)
            {
                var variant=_Variants[i];
                if(variant==null||!variant.Root||!variant.Head||variant.Arms==null||variant.Arms.Length!=8||variant.Artwork==null||variant.Artwork.Length!=9||!_ShadowSprites[i])
                {error="Incomplete authored boss variant "+i;return false;}
                foreach(var arm in variant.Arms)if(!arm){error="Missing authored arm pivot.";return false;}
                foreach(var artwork in variant.Artwork)if(!artwork){error="Missing authored segment renderer.";return false;}
            }
            return true;
        }
        void Initialize()
        {
            if(_Initialized)return;
            if(!ValidateAuthoredBindings(out string error))throw new InvalidOperationException(error);
            _Poses=new Pose[3][];_Animators=new Animator[3];
            for(int v=0;v<3;v++)
            {
                _Poses[v]=new Pose[9];
                for(int j=0;j<9;j++)
                {
                    var t=j==0?_Variants[v].Head:_Variants[v].Arms[j-1];
                    _Poses[v][j]=new Pose{Transform=t,Position=t.localPosition,Rotation=t.localRotation,Scale=t.localScale};
                }
                _Animators[v]=_Variants[v].Root.GetComponent<Animator>();
            }
            _Initialized=true;_ShadowSource.forceRenderingOff=true;
        }
        void Awake(){Initialize();SetLifeStage(0);}
        Animator Current=>_Index>=0?_Animators[_Index]:null;
        // Authored clip length by state name, so one-shot reactions can be stretched to the time the arena allots.
        float ClipSeconds(string state)
        {
            var animator=Current;if(!animator||!animator.runtimeAnimatorController)return 0;
            foreach(var clip in animator.runtimeAnimatorController.animationClips)if(clip&&clip.name.EndsWith("_"+state))return clip.length;
            return 0;
        }
        public void SetLifeStage(int fishGoals)
        {
            Initialize();int next=BossGhostLifeStage.VariantIndex(fishGoals);
            if(_Index==next)return;
            ResetPose();_Index=next;
            for(int i=0;i<3;i++)_Variants[i].Root.SetActive(i==_Index);
            _ShadowSource.sprite=_ShadowSprites[_Index];ApplyVisibility();
        }
        public void SetVisible(bool visible){Initialize();_Visible=visible;ApplyVisibility();}
        void ApplyVisibility()
        {
            foreach(var renderer in _AllArtwork)renderer.enabled=_Visible;
            _ShadowSource.enabled=_Visible;_ShadowSource.forceRenderingOff=true;
        }
        void Select(int state,float normalizedStart,float speed)
        {
            var animator=Current;if(!animator||!animator.isActiveAndEnabled)return;
            animator.SetFloat(Speed,speed);animator.Play(state,0,Mathf.Clamp01(normalizedStart));
        }
        // fishScored: the boss was scored on (it rages); otherwise the ghost scored (it gloats). final: the match-deciding goal.
        public void PlayReaction(bool fishScored,bool final,float remaining,float fullLength)
        {
            Initialize();StopPanic();
            fullLength=Mathf.Max(.05f,fullLength);remaining=Mathf.Clamp(remaining,0,fullLength);
            string name=final?(fishScored?"Defeat":"Triumph"):(fishScored?"Frustrated":"Gloat");
            int state=final?(fishScored?Defeat:Triumph):(fishScored?Frustrated:Gloat);
            float clip=ClipSeconds(name);
            // Stretch the authored clip over the reaction window; resume mid-clip if a snapshot arrived late.
            Select(state,1-remaining/fullLength,clip>0?Mathf.Clamp(clip/fullLength,.4f,4f):1);
            _ReactionEnd=Time.unscaledTime+remaining;
        }
        // The cue bomb is lit: juggle it, then panic once the fuse is hot. A single wait schedules the change.
        public void PresentFuse(bool armed,float heat,float fuseSeconds)
        {
            Initialize();StopPanic();
            if(ReactionPlaying)return;
            if(!armed){Select(Idle,0,1);return;}
            if(heat>=_PanicHeat){Select(Panic,0,1);return;}
            Select(Juggle,0,1);
            if(isActiveAndEnabled&&fuseSeconds>0)_Panic=StartCoroutine(PanicAfter((_PanicHeat-heat)*fuseSeconds));
        }
        IEnumerator PanicAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _Panic=null;
            if(!ReactionPlaying)Select(Panic,0,1);
        }
        void StopPanic(){if(_Panic!=null)StopCoroutine(_Panic);_Panic=null;}
        // The bomb blew up on a side: the boss ducks if it was its own, and gloats if it was the fish's.
        public void PresentBlast(bool onBossSide)
        {
            Initialize();StopPanic();
            float clip=ClipSeconds(onBossSide?"Flinch":"Gloat");
            Select(onBossSide?Flinch:Gloat,0,1);
            _ReactionEnd=Time.unscaledTime+(clip>0?clip:1);
        }
        public void ResetPose()
        {
            StopPanic();_ReactionEnd=0;
            if(!_Initialized)return;
            foreach(var variant in _Poses)foreach(var pose in variant)pose.Restore();
            Select(Idle,0,1);
        }
        void OnDisable(){ResetPose();if(_ShadowSource)_ShadowSource.enabled=false;}
        void OnEnable(){if(_Initialized)ApplyVisibility();}
    }
}
