using UnityEngine;
namespace HauntedFish.BossFight
{
    // All cameras/focus/actor bindings are authored in BossFightArena.prefab.
    public sealed class BossArenaView : MonoBehaviour
    {
        [SerializeField] Camera _Fish,_Ghost,_Spectator;
        [SerializeField] Transform _Focus;
        [SerializeField] BossSegmentedRig _GhostFraming;
        [SerializeField] Transform _DefenseFraming;
        [SerializeField] BossCharacterPresentation _Characters;
        [SerializeField] BossOpponentShadow _StormShadows;
        [SerializeField] BossStormPresentation _Storm;
        [SerializeField] Vector3 _FrameSize=new Vector3(7.3f,.7f,11.6f);
        [SerializeField] float _FrameCentreY=-.05f;
        [SerializeField] Vector3 _SpectatorFrameSize=new Vector3(9,6,15.6f);
        [SerializeField] float _SpectatorFrameCentreY=1.6f;
        [SerializeField] Renderer[] _FishNearWalls,_GhostNearWalls;
        BossViewerSide _Side;
        bool _Watching;
        public Camera CurrentCamera=>_Side==BossViewerSide.Fish?_Fish:_Side==BossViewerSide.Ghost?_Ghost:_Spectator;
        public BossViewerSide Side=>_Side;
        public void Select(BossViewerSide side,bool watch)
        {
            _Side=side;_Watching=watch;
            if(_Fish) _Fish.gameObject.SetActive(watch&&side==BossViewerSide.Fish);
            if(_Ghost) _Ghost.gameObject.SetActive(watch&&side==BossViewerSide.Ghost);
            if(_Spectator) _Spectator.gameObject.SetActive(watch&&side==BossViewerSide.Spectator);
            if(_Characters)_Characters.SetViewer(watch?side:BossViewerSide.Spectator);
            if(_StormShadows)_StormShadows.SetViewer(side,watch);
            if(_Storm)_Storm.SetWatching(watch);
            if(_FishNearWalls!=null)foreach(var wall in _FishNearWalls)if(wall)wall.enabled=!watch||side==BossViewerSide.Ghost;
            if(_GhostNearWalls!=null)foreach(var wall in _GhostNearWalls)if(wall)wall.enabled=!watch||side!=BossViewerSide.Ghost;
            Fit();
        }
        void LateUpdate(){if(_Watching)Fit();}
        public void Fit()
        {
            var camera=CurrentCamera;if(!camera||!_Focus)return;
            if(camera.pixelWidth>0&&camera.pixelHeight>0)camera.aspect=camera.pixelWidth/(float)camera.pixelHeight;
            float pitch=Mathf.Asin(Mathf.Clamp(-camera.transform.forward.y,-1,1))*Mathf.Rad2Deg;
            var size=_Side==BossViewerSide.Spectator?_SpectatorFrameSize:_FrameSize;
            float centreY=_Side==BossViewerSide.Spectator?_SpectatorFrameCentreY:_FrameCentreY;
            if(BossPerspectiveFraming.RequiredDistance(camera.fieldOfView,camera.aspect,camera.nearClipPlane,size.x*.5f,
                centreY-size.y*.5f,centreY+size.y*.5f,size.z*.5f,pitch,out float distance))
            {
                if(_Side!=BossViewerSide.Ghost&&_GhostFraming)
                {
                    foreach(var renderer in _GhostFraming.ActiveArtwork)
                    {
                        if(!renderer)continue;var bounds=renderer.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var relative=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z)-_Focus.position;
                            if(BossPerspectiveFraming.ProjectedPointDistance(Vector3.Dot(relative,camera.transform.right),Vector3.Dot(relative,camera.transform.up),Vector3.Dot(relative,camera.transform.forward),camera.fieldOfView,camera.aspect,camera.nearClipPlane,out float opponentDistance))distance=Mathf.Max(distance,opponentDistance);
                        }
                    }
                }
                if(_DefenseFraming)foreach(var renderer in _DefenseFraming.GetComponentsInChildren<Renderer>())
                {
                    if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;var bounds=renderer.bounds;
                    for(int corner=0;corner<8;corner++){var p=new Vector3((corner&1)==0?bounds.min.x:bounds.max.x,(corner&2)==0?bounds.min.y:bounds.max.y,(corner&4)==0?bounds.min.z:bounds.max.z)-_Focus.position;
                        if(BossPerspectiveFraming.ProjectedPointDistance(Vector3.Dot(p,camera.transform.right),Vector3.Dot(p,camera.transform.up),Vector3.Dot(p,camera.transform.forward),camera.fieldOfView,camera.aspect,camera.nearClipPlane,out float rackDistance))distance=Mathf.Max(distance,rackDistance);}
                }
                camera.transform.position=_Focus.position-camera.transform.forward*distance;
                camera.farClipPlane=Mathf.Max(100,distance+size.magnitude+5);
            }
        }
    }
}