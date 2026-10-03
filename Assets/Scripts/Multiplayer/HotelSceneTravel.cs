using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace HauntedFish.Multiplayer
{
    // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
    public struct HotelTravel { public int Version; public string Scene; }
    // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
    public struct HotelTravelReady { public int Version; public string Scene; }
    // Mirage v159 uses player readiness and object managers; no deprecated sample scene manager.
    // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
    public sealed class HotelSceneTravel : MonoBehaviour
    {
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public bool Loading { get; private set; }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public string TargetScene { get; private set; }="Lobby";
        NetworkServer server;NetworkClient client;ServerObjectManager serverObjects;ClientObjectManager clientObjects;HotelLobby lobby;
        int version;bool serverLoaded;
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        readonly Dictionary<INetworkPlayer,float> waiting=new Dictionary<INetworkPlayer,float>();
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public void Configure(HotelLobby owner,NetworkServer host,NetworkClient peer,ServerObjectManager hostObjects,ClientObjectManager peerObjects)
        {
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            lobby=owner;server=host;client=peer;serverObjects=hostObjects;clientObjects=peerObjects;
            // Register the exact message type on this peer; the handler applies only the session protocol it understands.
            server.Started.AddListener(()=>server.MessageHandler.RegisterHandler<HotelTravelReady>(Ready,allowUnauthenticated:false));
            // Register the exact message type on this peer; the handler applies only the session protocol it understands.
            client.Started.AddListener(()=>client.MessageHandler.RegisterHandler<HotelTravel>((_,message)=>Receive(message),allowUnauthenticated:false));
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            server.Disconnected.AddListener(player=>waiting.Remove(player));
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public void GoToGame() { if (server.Active && !Loading && TargetScene=="Lobby") Begin("Game"); }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public void ReturnToLobby() { if (server.Active && !Loading && TargetScene=="Game") Begin("Lobby"); }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        void Begin(string scene)
        {
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            if (!Application.CanStreamedLevelBeLoaded(scene)) {lobby.SetStatus("Add "+scene+" to the build scene list.");return;}
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (SharedDialogue.Instance && Monologue.Dialogue.DialogueManager.Instance && Monologue.Dialogue.DialogueManager.Instance.IsSharedDialogue)
                // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
                Monologue.Dialogue.DialogueManager.Instance.ExitDialogMode();
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            ++version;TargetScene=scene;serverLoaded=false;Loading=true;waiting.Clear();
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            foreach (var player in server.AuthenticatedPlayers) {player.SceneIsReady=false;waiting[player]=Time.unscaledTime+30;}
            // Scene-owned objects belong to the old scene. Persistent player/coordinator identities survive.
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            foreach (var identity in server.World.SpawnedIdentities.ToArray())
                // Keep the established session or spawned network identity alive across connected Lobby/Game travel.
                if (identity && identity.gameObject.scene.name!="DontDestroyOnLoad") serverObjects.Destroy(identity);
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            server.SendToAll(new HotelTravel {Version=version,Scene=scene},authenticatedOnly:true,excludeLocalPlayer:true);
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            StartCoroutine(Load(scene,true));
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public void Admit(INetworkPlayer player)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (TargetScene=="Lobby" && !Loading) return;
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            player.SceneIsReady=false;waiting[player]=Time.unscaledTime+30;
            // Send this protocol payload through the established Mirage connection rather than creating a separate gameplay session.
            player.Send(new HotelTravel {Version=version,Scene=TargetScene});
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        void Receive(HotelTravel message)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (server.Active || (message.Scene!="Game" && message.Scene!="Lobby") || message.Version<=version) return;
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            if (!Application.CanStreamedLevelBeLoaded(message.Scene)) {lobby.SetStatus("Scene missing from this build: "+message.Scene);client.Disconnect();return;}
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            version=message.Version;TargetScene=message.Scene;Loading=true;
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            StartCoroutine(Load(message.Scene,false));
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        IEnumerator Load(string scene,bool host)
        {
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            lobby.SetStatus("Loading "+scene+" with the connected room...");
            // Load the authored scene asset while keeping the existing session; readiness messages release the movement gate afterwards.
            yield return SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            clientObjects.PrepareToSpawnSceneObjects();
            // Look up objects already present in the loaded scenes; ownership and scene checks select the appropriate one.
            foreach (var player in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None)) player.ResetSceneMotion();
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (host)
            {
                // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
                serverLoaded=true;
                // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
                var index=0;
                // Transport authentication alone is not room admission; the room code and join key are checked separately.
                foreach (var player in server.AuthenticatedPlayers)
                    // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
                    if (player.HasCharacter)
                    {
                        // Read a component already serialized on this object; do not create a runtime replacement for missing authoring.
                        var character=player.Identity.GetComponent<HotelPlayer>();
                        // Ask Mirage to replicate this authoritative object and its initial state to connected observers.
                        if (character) character.Teleport(GameSceneDefinition.Current?GameSceneDefinition.Current.Spawn(index):new Vector3(index*2.2f,1.1f,2));
                        ++index;
                    }
                // Readiness prevents movement or visibility updates while that connection is loading a different scene.
                if (server.LocalPlayer!=null) {server.LocalPlayer.SceneIsReady=true;waiting.Remove(server.LocalPlayer);}
                // Readiness prevents movement or visibility updates while that connection is loading a different scene.
                foreach (var player in server.AuthenticatedPlayers) if (player.SceneIsReady) serverObjects.SpawnVisibleObjects(player);
                // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
                serverObjects.SpawnSceneObjects();
            }
            // Send this protocol payload through the established Mirage connection rather than creating a separate gameplay session.
            else client.Send(new HotelTravelReady {Version=version,Scene=scene});
            // Expose the session result to the authored UI so connection failures and current room state remain visible.
            Loading=false;lobby.SetStatus("Connected to "+lobby.Code+" — "+scene+".");
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        void Ready(INetworkPlayer player,HotelTravelReady message)
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!waiting.ContainsKey(player) || message.Version!=version || message.Scene!=TargetScene) return;
            // Readiness prevents movement or visibility updates while that connection is loading a different scene.
            waiting.Remove(player);player.SceneIsReady=true;
            // Refresh network visibility after readiness, allowing this peer to receive the objects in its loaded scene.
            if (serverLoaded) serverObjects.SpawnVisibleObjects(player);
        }
        // Service local presentation and authoritative simulation each frame, with ownership/readiness checks inside the path.
        void Update()
        {
            // Leave this path once the result is known; guards keep an invalid or irrelevant peer from changing shared state.
            if (!server || !server.Active) return;
            // End this peer connection through Mirage so its normal identity and observer cleanup runs.
            foreach (var entry in waiting.ToArray()) if (Time.unscaledTime>entry.Value) {waiting.Remove(entry.Key);entry.Key.Disconnect();}
        }
        // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
        public void SessionEnded()
        {
            // Scene messages travel over the existing reliable Mirage connection. Loading temporarily gates input and spawning until each peer reports readiness.
            StopAllCoroutines();waiting.Clear();Loading=false;version=0;TargetScene="Lobby";serverLoaded=false;
            // Check the current session or presentation state before continuing; this path must not run against an invalid dependency.
            if (SceneManager.GetActiveScene().name=="Game" && Application.CanStreamedLevelBeLoaded("Lobby")) StartCoroutine(ReturnOffline());
        }
        // Load the authored scene asset while keeping the existing session; readiness messages release the movement gate afterwards.
        IEnumerator ReturnOffline() {Loading=true;yield return SceneManager.LoadSceneAsync("Lobby",LoadSceneMode.Single);Loading=false;}
    }
}
