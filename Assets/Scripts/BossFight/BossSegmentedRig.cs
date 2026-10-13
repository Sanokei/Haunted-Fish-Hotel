using System;
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
    // All 27 visible segments and the nonrendering silhouette source are authored.
    // This component only selects variants and poses their existing transforms.
    public sealed class BossSegmentedRig : MonoBehaviour
    {
        [SerializeField] BossGhostVariant[] _Variants;
        [SerializeField] SpriteRenderer _ShadowSource;
        [SerializeField] Sprite[] _ShadowSprites;
        [SerializeField] Renderer[] _AllArtwork;
        [SerializeField] float _IdleArmDegrees=8.5f,_HeadBob=.045f,_IdleReachDegrees=12f;
        struct Pose
        {
            public Transform Transform;public Vector3 Position,Scale;public Quaternion Rotation;
            public void Restore(){Transform.localPosition=Position;Transform.localRotation=Rotation;Transform.localScale=Scale;}
        }
        Pose[][] _Poses;
        bool _Initialized,_Visible=true,_FishScored,_Final;
        int _Index=-1;
        float _ReactionStart,_ReactionEnd,_ReactionLength;
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
            _Poses=new Pose[3][];
            for(int v=0;v<3;v++)
            {
                _Poses[v]=new Pose[9];
                for(int j=0;j<9;j++)
                {
                    var t=j==0?_Variants[v].Head:_Variants[v].Arms[j-1];
                    _Poses[v][j]=new Pose{Transform=t,Position=t.localPosition,Rotation=t.localRotation,Scale=t.localScale};
                }
            }
            _Initialized=true;_ShadowSource.forceRenderingOff=true;
        }
        void Awake(){Initialize();SetLifeStage(0);}
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
        public void PlayReaction(bool fishScored,bool final,float remaining,float fullLength)
        {
            Initialize();_FishScored=fishScored;_Final=final;
            _ReactionLength=Mathf.Max(.05f,fullLength);remaining=Mathf.Clamp(remaining,0,_ReactionLength);
            _ReactionStart=Time.unscaledTime-(_ReactionLength-remaining);_ReactionEnd=Time.unscaledTime+remaining;
        }
        void LateUpdate(){if(_Initialized&&_Index>=0)EvaluatePose(Time.unscaledTime);}
        void EvaluatePose(float now)
        {
            var poses=_Poses[_Index];float stage=_Index+1;
            float progress=Mathf.Clamp01((now-_ReactionStart)/Mathf.Max(.05f,_ReactionLength));
            float pulse=now<_ReactionEnd?Mathf.Sin(progress*Mathf.PI):0;
            for(int j=0;j<poses.Length;j++)
            {
                var pose=poses[j];float frequency=j==0?.5f+.08f*stage:.18f+.075f*((j*7)%5);float phase=now*frequency+j*.77f;
                if(j==0)
                {
                    float shudder=_FishScored?Mathf.Sin(progress*Mathf.PI*8)*(_Final?7:4):Mathf.Sin(progress*Mathf.PI)*-4;
                    pose.Transform.localPosition=pose.Position+Vector3.up*(_HeadBob*Mathf.Sin(phase)+pulse*(_FishScored?-.06f:.10f));
                    pose.Transform.localRotation=pose.Rotation*Quaternion.Euler(0,0,shudder*pulse);
                }
                else
                {
                    float side=j<=4?-1:1;
                    float reaction=_FishScored?side*(_Final?18:10):side*-16;
                    float reach=Mathf.Clamp01((Mathf.Sin(now*(.22f+.025f*j)+j*1.91f)-.82f)/.18f);
                    reach=Mathf.SmoothStep(0,1,reach);
                    pose.Transform.localRotation=pose.Rotation*Quaternion.Euler(0,0,_IdleArmDegrees*Mathf.Sin(phase)+side*_IdleReachDegrees*reach+reaction*pulse);
                }
            }
        }
        public void ResetPose()
        {
            _ReactionStart=_ReactionEnd=_ReactionLength=0;
            if(_Initialized)foreach(var variant in _Poses)foreach(var pose in variant)pose.Restore();
        }
        void OnDisable(){ResetPose();if(_ShadowSource)_ShadowSource.enabled=false;}
        void OnEnable(){if(_Initialized)ApplyVisibility();}
    }
}