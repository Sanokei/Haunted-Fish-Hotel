using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace HauntedFish.Multiplayer
{
 // This UI operates serialized controls. Button callbacks request session actions; they never create scene objects.
 public sealed class HotelLobbyPanel : MonoBehaviour
 {
  // Use a normalized six-character room code to resolve the directory entry for this particular host.
  public TMP_InputField Directory,Address,Code;
  // Expose the session result to the authored UI so connection failures and current room state remain visible.
  public TMP_Text Status;
  // This UI operates serialized controls. Button callbacks request session actions; they never create scene objects.
  public Button Create,Join,Leave,StartGame,ReturnLobby;
  // Resolve the authored dependencies early; the scene and prefab data determine what exists.
  void Awake() {
   // Bind this authored button to a session action; the callback operates existing components and scene references.
   Create.onClick.AddListener(()=>HotelLobby.Instance.CreateLobby());
   // Bind this authored button to a session action; the callback operates existing components and scene references.
   Join.onClick.AddListener(()=>HotelLobby.Instance.JoinLobby(Code.text));
   // Bind this authored button to a session action; the callback operates existing components and scene references.
   Leave.onClick.AddListener(()=>HotelLobby.Instance.LeaveLobby());
   // Bind this authored button to a session action; the callback operates existing components and scene references.
   StartGame.onClick.AddListener(()=>HotelLobby.Instance.SceneTravel.GoToGame());
   // Bind this authored button to a session action; the callback operates existing components and scene references.
   ReturnLobby.onClick.AddListener(()=>HotelLobby.Instance.SceneTravel.ReturnToLobby());
   // Apply the edited endpoint only when text editing finishes, keeping setup values explicit in the UI.
   Directory.onEndEdit.AddListener(value=>HotelLobby.Instance.DirectoryUrl=value);
   // Apply the edited endpoint only when text editing finishes, keeping setup values explicit in the UI.
   Address.onEndEdit.AddListener(value=>HotelLobby.Instance.AdvertisedAddress=value);
  }
  // All peers must query the same room directory; it advertises endpoints and invitations rather than relaying game traffic.
  void Start() { if(HotelLobby.Instance) {Directory.text=HotelLobby.Instance.DirectoryUrl;Address.text=HotelLobby.Instance.AdvertisedAddress;} }
  // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
  void Update() {
   // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
   var lobby=HotelLobby.Instance;if(!lobby)return;
   // Expose the session result to the authored UI so connection failures and current room state remain visible.
   Status.text=lobby.Status+"\nLobby code: "+lobby.Code;
   // Use a normalized six-character room code to resolve the directory entry for this particular host.
   bool connected=!string.IsNullOrEmpty(lobby.Code),free=!connected&&!lobby.Busy;
   // Enable this authored control only in the session state where its action is meaningful.
   Create.interactable=Join.interactable=Directory.interactable=Address.interactable=Code.interactable=free;
   // Enable this authored control only in the session state where its action is meaningful.
   Leave.interactable=connected||lobby.Busy;
   // Enable this authored control only in the session state where its action is meaningful.
   StartGame.interactable=lobby.IsHost&&connected&&!lobby.SceneTravel.Loading&&lobby.SceneTravel.TargetScene=="Lobby";
   // Enable this authored control only in the session state where its action is meaningful.
   ReturnLobby.interactable=lobby.IsHost&&connected&&!lobby.SceneTravel.Loading&&lobby.SceneTravel.TargetScene=="Game";
   // Text entry in these serialized fields suppresses movement input while the keyboard edits lobby settings.
   HotelLobby.SetInputFocused(Directory.isFocused||Address.isFocused||Code.isFocused);
  }
 }
}
