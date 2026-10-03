using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // The authored player HUD exposes only invitations; server addresses and host/client controls stay out of gameplay.
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        // A six-character invitation is the only editable networking value shown to players.
        public TMP_InputField Code;
        // Display the room code and a concise connection state without revealing technical endpoint details.
        public TMP_Text Status;
        // Sharing and joining act on serialized widgets; regular game actions remain in a separate authored menu.
        [UnityEngine.Serialization.FormerlySerializedAs("Create")]
        public Button Share;
        // Preserve the other established widget references while migrating old saved Create bindings to Copy code.
        public Button Join,Leave,StartGame,ReturnLobby;
        // Bind existing scene controls to the persistent session rather than assembling UI at runtime.
        void Awake()
        {
            // Copying an invitation is local clipboard behavior, not an external message or automatic sharing action.
            Share.onClick.AddListener(()=>GUIUtility.systemCopyBuffer=HotelLobby.Instance.Code);
            // A connected player can enter another invitation; HotelLobby serializes cleanup and replacement admission.
            Join.onClick.AddListener(()=>HotelLobby.Instance.JoinLobby(Code.text));
            // The ordinary menu returns a guest to their own automatically created private room.
            Leave.onClick.AddListener(()=>HotelLobby.Instance.StartPrivateRoom());
            // Scene travel preserves the same Mirage session and remains an authoritative host decision.
            StartGame.onClick.AddListener(()=>HotelLobby.Instance.SceneTravel.GoToGame());
            // Returning to Lobby uses the same reliable scene readiness handshake.
            ReturnLobby.onClick.AddListener(()=>HotelLobby.Instance.SceneTravel.ReturnToLobby());
        }
        // Keep the compact invitation controls synchronized with admission and the current game scene.
        void Update()
        {
            // Helper initializes the persistent session before Lobby becomes playable.
            var lobby=HotelLobby.Instance;if(!lobby)return;
            // A room invitation is useful only once its code exists; no URL or IP address is shown here.
            bool connected=!string.IsNullOrEmpty(lobby.Code),transition=lobby.Transitioning;
            // Keep service error details internal while telling players whether a room is available.
            Status.text=connected?"Room code: "+lobby.Code:(transition?"Connecting...":"Unable to connect.");
            // Room changes and invitation editing wait for the preceding scene/socket transition to finish.
            Code.interactable=Join.interactable=!transition;
            // Prevent empty or malformed invitations from becoming a destructive room change.
            Join.interactable &= Code.text.Trim().Length==6;
            // An invitation can be copied only when the current room is known.
            Share.interactable=connected&&!transition;
            // Only a guest needs to leave a shared room for private play; hosts already own their room.
            Leave.gameObject.SetActive(connected&&!lobby.IsHost);
            // A regular game action enters Game from Lobby; it does not advertise transport/server terminology.
            StartGame.gameObject.SetActive(connected&&lobby.IsHost&&lobby.SceneTravel.TargetScene=="Lobby");
            // The return action belongs to Game and is absent from Lobby's menu.
            ReturnLobby.gameObject.SetActive(connected&&lobby.IsHost&&lobby.SceneTravel.TargetScene=="Game");
            // Disable game actions during loading or invitation switching, even if the last code is still visible.
            Leave.interactable=StartGame.interactable=ReturnLobby.interactable=!transition;
            // Typing an invitation must not simultaneously move the local avatar.
            HotelLobby.SetInputFocused(Code.isFocused);
        }
        // Clear focus when scene travel destroys this authored HUD so movement resumes in its replacement scene.
        void OnDisable(){HotelLobby.SetInputFocused(false);}
    }
}
