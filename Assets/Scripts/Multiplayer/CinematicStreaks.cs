using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // An authored, Ink-toggled overlay. Observes cinematic travel; never moves a camera.
    public sealed class CinematicStreaks : MonoBehaviour
    {
        [SerializeField] RectTransform _Motion, _Viewport;
        [SerializeField] Vector2 _MotionYRange = new Vector2(-2160, 0);
        [SerializeField] Image[] _Streaks;

        void OnEnable() => LateUpdate();
        void LateUpdate()
        {
            if (!_Motion || !_Viewport || _Streaks == null) return;
            float t = Mathf.InverseLerp(_MotionYRange.x, _MotionYRange.y, _Motion.anchoredPosition.y);
            var size = _Viewport.rect.size;
            for (int i = 0; i < _Streaks.Length; i++)
            {
                var streak = _Streaks[i];
                if (!streak) continue;
                float phase = Mathf.Repeat(i * .381966f + t * 1.7f, 1);
                streak.rectTransform.anchoredPosition = new Vector2((i * .618034f % 1 - .5f) * size.x, (phase - .5f) * (size.y + 440));
                streak.rectTransform.sizeDelta = new Vector2(2 + i % 3, (80 + i % 5 * 35) * (.4f + t));
                var color = HotelPalette.Light;
                color.a = .08f + t * .2f;
                streak.color = color;
            }
        }
    }
}
