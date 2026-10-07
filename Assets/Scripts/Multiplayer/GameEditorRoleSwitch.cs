using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class GameEditorRoleSwitch : MonoBehaviour
    {
        [SerializeField] GameObject _Root;
        [SerializeField] Text _Label;
        [SerializeField] Button _Button;
        void Awake() => _Button.onClick.AddListener(Switch);
        void Switch() { if (HotelPlayer.LocalPlayer) HotelPlayer.LocalPlayer.SwitchEditorRole(); }
        void Update()
        {
            var player = HotelPlayer.LocalPlayer;
            bool ready = Application.isEditor && player && player.RoundReleased && player.ControlsReady;
            _Root.SetActive(ready);
            _Button.interactable = ready;
            _Label.text = player && player.ControlMode == HotelControlMode.Ghost ? "Switch to Player" : "Switch to Ghost";
        }
        void OnDisable() { if (_Root) _Root.SetActive(false); }
    }
}
