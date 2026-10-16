using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Thin white bar at the left edge: appears while the local fish sprints or recovers, then fades away.
    public sealed class SprintStaminaBar : MonoBehaviour
    {
        [SerializeField] Image _Fill;
        [SerializeField] CanvasGroup _Group;
        [SerializeField, Min(0)] float _FadeSeconds = .35f;
        [SerializeField] Color _Ready = new Color(1, 1, 1, .9f), _Winded = new Color(1, 1, 1, .35f);

        void LateUpdate()
        {
            var player = HotelPlayer.LocalPlayer;
            var movement = player ? player.Movement : null;
            bool show = movement && movement.Stamina < .999f && player.ControlMode == HotelControlMode.Fish;
            if (_Fill && movement)
            {
                _Fill.fillAmount = movement.Stamina;
                _Fill.color = movement.Winded ? _Winded : _Ready;
            }
            if (_Group)
                _Group.alpha = Mathf.MoveTowards(_Group.alpha, show ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.01f, _FadeSeconds));
        }
    }
}
