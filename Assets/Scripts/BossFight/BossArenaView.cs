using UnityEngine;
namespace HauntedFish.BossFight
{
    // Camera, focus, actor and presentation-occluder bindings are authored in the arena scene/prefabs.
    public sealed class BossArenaView : MonoBehaviour
    {
        [SerializeField] Camera _Fish,_Ghost,_Spectator;
        [SerializeField] Transform _Focus;
        [SerializeField] BossSegmentedRig _GhostFraming;
        [SerializeField] Transform _DefenseFraming;
        [SerializeField] Transform[] _AuthoredFrameMarkers;
        [SerializeField] Transform[] _FishViewFrameMarkers;
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
        float[] _FishFrameRight,_FishFrameUp,_FishFrameDepth;
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
            if(_Side==BossViewerSide.Fish&&FitFish(camera,size,centreY))return;
            if(BossPerspectiveFraming.RequiredDistance(camera.fieldOfView,camera.aspect,camera.nearClipPlane,size.x*.5f,
                centreY-size.y*.5f,centreY+size.y*.5f,size.z*.5f,pitch,out float distance))
            {
                // Fixed authored board/tray and central-head boundaries; outer tentacles may crop.
                // Preview objects and transparent sprite canvas bounds never change the camera.
                FitMarkers(camera, _AuthoredFrameMarkers, ref distance);
                // The local ghost is hidden in its own view. Only its opponent and spectators
                // reserve only the central head envelope; sprawling arm tips may leave the frame.
                if (_Side != BossViewerSide.Ghost)
                {
                    FitMarkers(camera, _FishViewFrameMarkers, ref distance);
                }
                camera.transform.position=_Focus.position-camera.transform.forward*distance;
                camera.farClipPlane=Mathf.Max(100,distance+size.magnitude+5);
            }
        }
        bool FitFish(Camera camera,Vector3 size,float centreY)
        {
            int count=8+(_AuthoredFrameMarkers?.Length??0)+(_FishViewFrameMarkers?.Length??0);
            if(_FishFrameRight==null||_FishFrameRight.Length!=count)
            {_FishFrameRight=new float[count];_FishFrameUp=new float[count];_FishFrameDepth=new float[count];}
            int used=0;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                AddFishFramePoint(camera,new Vector3(x*size.x*.5f,centreY+y*size.y*.5f,z*size.z*.5f),ref used);
            AddFishFrameMarkers(camera,_AuthoredFrameMarkers,ref used);
            AddFishFrameMarkers(camera,_FishViewFrameMarkers,ref used);
            if(!BossPerspectiveFraming.RequiredOffsetDistance(camera.fieldOfView,camera.aspect,camera.nearClipPlane,
                _FishFrameRight,_FishFrameUp,_FishFrameDepth,used,out float distance,out float offset))return false;
            camera.transform.position=_Focus.position+camera.transform.up*offset-camera.transform.forward*distance;
            camera.farClipPlane=Mathf.Max(100,distance+size.magnitude+15);
            return true;
        }
        void AddFishFrameMarkers(Camera camera,Transform[] markers,ref int used)
        {
            if(markers==null)return;
            foreach(var marker in markers)if(marker)AddFishFramePoint(camera,marker.position-_Focus.position,ref used);
        }
        void AddFishFramePoint(Camera camera,Vector3 point,ref int used)
        {
            _FishFrameRight[used]=Vector3.Dot(point,camera.transform.right);
            _FishFrameUp[used]=Vector3.Dot(point,camera.transform.up);
            _FishFrameDepth[used]=Vector3.Dot(point,camera.transform.forward);used++;
        }
        void FitMarkers(Camera camera, Transform[] markers, ref float distance)
        {
            if (markers == null) return;
            foreach (var marker in markers)
            {
                if (!marker) continue;
                var point = marker.position - _Focus.position;
                if (BossPerspectiveFraming.ProjectedPointDistance(
                    Vector3.Dot(point, camera.transform.right),
                    Vector3.Dot(point, camera.transform.up),
                    Vector3.Dot(point, camera.transform.forward),
                    camera.fieldOfView, camera.aspect, camera.nearClipPlane,
                    out float framedDistance))
                {
                    distance = Mathf.Max(distance, framedDistance);
                }
            }
        }
    }
}
