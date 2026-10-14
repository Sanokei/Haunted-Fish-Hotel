using UnityEngine;

namespace HauntedFish.BossFight
{
    // Only adjusts the authored safe-area container when display geometry changes.
    // It never builds UI, changes menu ordering, or consumes pointer input.
    public sealed class BossHudSafeArea : MonoBehaviour
    {
        [SerializeField] RectTransform _Content;
        Rect _LastArea;
        int _Width, _Height;
        bool _Applied;

        void OnEnable()
        {
            _Applied=false;
            Canvas.willRenderCanvases+=Refresh;
            Refresh();
        }
        void OnDisable(){Canvas.willRenderCanvases-=Refresh;}
        void Refresh()
        {
            int width=Screen.width,height=Screen.height;
            if(!_Content||width<=0||height<=0)return;
            Rect area=Screen.safeArea;
            if(_Applied&&width==_Width&&height==_Height&&area==_LastArea)return;
            _Applied=true;_Width=width;_Height=height;_LastArea=area;
            _Content.anchorMin=new Vector2(Mathf.Clamp01(area.xMin/width),Mathf.Clamp01(area.yMin/height));
            _Content.anchorMax=new Vector2(Mathf.Clamp01(area.xMax/width),Mathf.Clamp01(area.yMax/height));
            _Content.offsetMin=_Content.offsetMax=Vector2.zero;
        }
    }
}
