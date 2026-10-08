using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    public sealed class GameEditorRoleSwitch : MonoBehaviour
    {
        [SerializeField] GameObject _Root;
        [SerializeField] Text _Label;
        [SerializeField] Button _Button;
        [SerializeField] Button _BossButton;
        [SerializeField] Text _BossLabel;
        void Awake()
        {
            if (_Button) _Button.onClick.AddListener(Switch);
#if UNITY_EDITOR
            if (_BossButton) _BossButton.onClick.AddListener(() => { if (BossArenaCoordinator.Current) BossArenaCoordinator.Current.EditorGoToBoss(); });
#endif
        }
        void Switch() { if (HotelPlayer.LocalPlayer) HotelPlayer.LocalPlayer.SwitchEditorRole(); }
        void Update()
        {
            var player = HotelPlayer.LocalPlayer;
            if (!_Root) return; // EditorOnly hierarchy is stripped from player builds.
            bool ready = Application.isEditor && player && player.RoundReleased && player.ControlsReady;
            _Root.SetActive(ready);
            _Button.interactable = ready && !player.InBossFight && !player.BossHallwayLocked;
            if (_BossButton) _BossButton.interactable = ready && !player.InBossFight && !player.BossReturnLocked && (!BossArenaCoordinator.Current || !BossArenaCoordinator.Current.Arena || !BossArenaCoordinator.Current.Arena.Active);
            if (_BossLabel) _BossLabel.text = player && player.BossWatching ? "Return to hallway (dev)" : "Go to BossFight (dev)";
            _Label.text = player && player.ControlMode == HotelControlMode.Ghost ? "Switch to Player" : "Switch to Ghost";
        }
        void OnDisable() { if (_Root) _Root.SetActive(false); }
    }
}
