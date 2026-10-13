using UnityEngine;

namespace HauntedFish.Multiplayer
{
    // Fits the existing 1280x720 page inside the display safe area, without rebuilding its hierarchy.
    public sealed class RoundHowToSafeArea : MonoBehaviour
    {
        [SerializeField] RectTransform _Viewport;
        [SerializeField] RectTransform _SafeArea;
        [SerializeField] RectTransform _Page;
        Rect _LastArea;
        Vector2 _LastSize;
        int _Width, _Height;
        bool _Applied;
        public bool HasBindings => _Viewport && _SafeArea && _Page;

        void OnEnable()
        {
            _Applied = false;
            Canvas.willRenderCanvases += Refresh;
            Refresh();
        }
        void OnDisable() => Canvas.willRenderCanvases -= Refresh;

        void Refresh()
        {
            int width = Screen.width, height = Screen.height;
            if (!HasBindings || width <= 0 || height <= 0) return;
            Rect area = Screen.safeArea;
            Vector2 size = _Viewport.rect.size;
            if (size.x <= 0 || size.y <= 0 || area.width <= 0 || area.height <= 0) return;
            if (_Applied && width == _Width && height == _Height && area == _LastArea && size == _LastSize) return;
            _Applied = true; _Width = width; _Height = height; _LastArea = area; _LastSize = size;
            _SafeArea.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / width), Mathf.Clamp01(area.yMin / height));
            _SafeArea.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / width), Mathf.Clamp01(area.yMax / height));
            _SafeArea.offsetMin = _SafeArea.offsetMax = Vector2.zero;
            Vector2 available = Vector2.Scale(size, _SafeArea.anchorMax - _SafeArea.anchorMin);
            Vector2 pageSize = _Page.sizeDelta;
            if (pageSize.x <= 0 || pageSize.y <= 0) return;
            float scale = Mathf.Min(1, Mathf.Min(available.x / pageSize.x, available.y / pageSize.y));
            _Page.localScale = Vector3.one * scale;
        }
    }
}
