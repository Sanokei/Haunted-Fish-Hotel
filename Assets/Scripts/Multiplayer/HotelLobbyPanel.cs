using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HauntedFish.Multiplayer
{
    // The authored player HUD exposes only invitations; server addresses and host/client controls stay out of gameplay.
    public sealed class HotelLobbyPanel : MonoBehaviour
    {
        // A six-character invitation is the only editable networking value shown to players.
        [SerializeField] TMP_InputField Code;
        // Display the room code and a concise connection state without revealing technical endpoint details.
        [SerializeField] TMP_Text Status;
        // Sharing and joining act on serialized widgets; regular game actions remain in a separate authored menu.
        [UnityEngine.Serialization.FormerlySerializedAs("Create")]
        [SerializeField] Button Share;
        // Preserve the other established widget references while migrating old saved Create bindings to Copy code.
        [SerializeField] Button Join;

        HotelLobby _CurrentLobby; 

        // Bind existing scene controls to the persistent session rather than assembling UI at runtime.
        void Awake()
        {
            // Copying an invitation is local clipboard behavior, not an external message or automatic sharing action.
            Share.onClick.AddListener(()=>GUIUtility.systemCopyBuffer=HotelLobby.Instance.Code);
            // A connected player can enter another invitation; HotelLobby serializes cleanup and replacement admission.
            Join.onClick.AddListener(()=>HotelLobby.Instance.JoinLobby(Code.text));

            _CurrentLobby=HotelLobby.Instance;
        }
        // Keep the compact invitation controls synchronized with admission and the current game scene.
        void Update()
        {
            // Helper initializes the persistent session before Lobby becomes playable.
            if(!_CurrentLobby)return;
            // A room invitation is useful only once its code exists; no URL or IP address is shown here.
            bool connected=!string.IsNullOrEmpty(_CurrentLobby.Code),transition=_CurrentLobby.Transitioning;
            // Keep service error details internal while telling players whether a room is available.
            Status.text=connected?"Room code: "+_CurrentLobby.Code:(transition?"Connecting...":"Unable to connect.");
            // Room changes and invitation editing wait for the preceding scene/socket transition to finish.
            Code.interactable=Join.interactable=!transition;
            // Prevent empty or malformed invitations from becoming a destructive room change.
            Join.interactable &= Code.text.Trim().Length==6;
            // An invitation can be copied only when the current room is known.
            Share.interactable=connected&&!transition;
            // Typing an invitation must not simultaneously move the local avatar.
            HotelLobby.SetInputFocused(Code.isFocused);
        }
        // Clear focus when scene travel destroys this authored HUD so movement resumes in its replacement scene.
        void OnDisable(){HotelLobby.SetInputFocused(false);}
    }
}
