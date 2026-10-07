using UnityEngine;
namespace HauntedFish.Multiplayer
{
    // Activates only the scene-authored offline preview; network scene entry retains its persistent players.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameEditorPreview : MonoBehaviour
    {
        [SerializeField] GameObject _Player;
        void Awake()
        {
            // Mirage excludes NotEditable objects from scene spawning, including inactive Editor previews.
            if (_Player) _Player.hideFlags = HideFlags.NotEditable;
        }
        void Start()
        {
            if (_Player) _Player.SetActive(Application.isEditor && (!GameSceneController.Current || !GameSceneController.Current.Session) &&
                HotelPlayer.ActivePlayers.Count == 0);
        }
        void Update()
        {
            if (_Player && _Player.activeSelf && GameSceneController.Current && GameSceneController.Current.Session) _Player.SetActive(false);
        }
        void OnDisable() { if (_Player) _Player.SetActive(false); }
    }
}
