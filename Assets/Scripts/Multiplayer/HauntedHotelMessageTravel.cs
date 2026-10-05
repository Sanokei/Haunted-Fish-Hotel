using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HauntedFish.Multiplayer
{
    public struct HotelTravel
    {
        public int Version;
        public string Scene;
    }

    public struct HotelTravelReady
    {
        public int Version;
        public string Scene;
    }

    public sealed class HotelTravelCallbacks
    {
        public readonly Action<string> ReportStatus;
        public readonly Func<string> RoomCode;
        public readonly Action BeforeLoad;
        public readonly Action ResetMotion;
        public readonly Action PositionCharacters;
        public Func<bool> CanStartGame;

        public HotelTravelCallbacks(Action<string> reportStatus, Func<string> roomCode,
            Action beforeLoad, Action resetMotion, Action positionCharacters)
        {
            ReportStatus = reportStatus;
            RoomCode = roomCode;
            BeforeLoad = beforeLoad;
            ResetMotion = resetMotion;
            PositionCharacters = positionCharacters;
        }
    }

    // Owns scene-message ordering and the peer readiness barrier.
    public sealed class HauntedHotelMessageTravel : MonoBehaviour
    {
        [SerializeField] NetworkServer _Server;
        [SerializeField] NetworkClient _Client;
        [SerializeField] ServerObjectManager _ServerObjects;
        [SerializeField] ClientObjectManager _ClientObjects;

        const float _ReadinessTimeout = 30f;
        readonly Dictionary<INetworkPlayer, float> _Waiting = new Dictionary<INetworkPlayer, float>();
        readonly List<INetworkPlayer> _Expired = new List<INetworkPlayer>();
        HotelSessionContext _Session;
        HotelTravelCallbacks _Callbacks;
        int _Version;
        bool _ServerLoaded;

        public bool Loading { get; private set; }
        public string TargetScene { get; private set; } = "Lobby";
        public bool CanTravel => _Server && _Server.Active && !Loading && _Waiting.Count == 0;
        public event Action StateChanged;

        public void Configure(HotelSessionContext context, HotelTravelCallbacks lifecycle)
        {
            Unconfigure();
            _Session = context;
            _Callbacks = lifecycle;
            _Server.Started.AddListener(RegisterServerMessages);
            _Client.Started.AddListener(RegisterClientMessages);
            _Server.Disconnected.AddListener(OnPeerDisconnected);
        }

        public void Unconfigure()
        {
            StopAllCoroutines();
            if (_Server)
            {
                _Server.Started.RemoveListener(RegisterServerMessages);
                _Server.Disconnected.RemoveListener(OnPeerDisconnected);
            }
            if (_Client)
                _Client.Started.RemoveListener(RegisterClientMessages);
            _Waiting.Clear();
            SetLoading(false);
            _Session = null;
            _Callbacks = null;
        }

        void RegisterServerMessages()
        {
            _Server.MessageHandler.RegisterHandler<HotelTravelReady>(OnPeerReady, allowUnauthenticated: false);
        }

        void RegisterClientMessages()
        {
            _Client.MessageHandler.RegisterHandler<HotelTravel>(OnTravelRequested, allowUnauthenticated: false);
        }

        public void GoToGame()
        {
            if (CanTravel && TargetScene == "Lobby" && (_Callbacks.CanStartGame == null || _Callbacks.CanStartGame()))
                BeginTravel("Game");
        }

        public void ReturnToLobby()
        {
            if (CanTravel && TargetScene == "Game")
                BeginTravel("Lobby");
        }

        void BeginTravel(string scene)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                _Callbacks.ReportStatus("Add " + scene + " to the build scene list.");
                return;
            }

            _Callbacks.BeforeLoad();
            ++_Version;
            TargetScene = scene;
            _ServerLoaded = false;
            _Waiting.Clear();
            SetLoading(true);
            foreach (var player in _Server.AuthenticatedPlayers)
                AwaitReadiness(player);
            _Server.SendToAll(CurrentTravelMessage(), authenticatedOnly: true, excludeLocalPlayer: true);
            StartCoroutine(LoadScene(scene, true));
        }

        void DestroyPreviousSceneObjects()
        {
            foreach (var identity in _Server.World.SpawnedIdentities.ToArray())
            {
                if (identity && identity.gameObject.scene.name != "DontDestroyOnLoad")
                    _ServerObjects.Destroy(identity);
            }
        }

        HotelTravel CurrentTravelMessage()
        {
            return new HotelTravel { Version = _Version, Scene = TargetScene };
        }

        void AwaitReadiness(INetworkPlayer player)
        {
            player.SceneIsReady = false;
            _Waiting[player] = Time.unscaledTime + _ReadinessTimeout;
        }

        public void Admit(INetworkPlayer player)
        {
            if (TargetScene == "Lobby" && !Loading)
            {
                player.SceneIsReady = true;
                return;
            }
            AwaitReadiness(player);
            if (player != _Server.LocalPlayer)
                player.Send(CurrentTravelMessage());
        }

        void OnTravelRequested(INetworkPlayer sender, HotelTravel message)
        {
            if (_Server.Active || message.Version <= _Version)
                return;
            if (message.Scene != "Game" && message.Scene != "Lobby")
                return;
            if (!Application.CanStreamedLevelBeLoaded(message.Scene))
            {
                _Callbacks.ReportStatus("Scene missing from this build: " + message.Scene);
                _Client.Disconnect();
                return;
            }

            _Version = message.Version;
            TargetScene = message.Scene;
            SetLoading(true);
            StartCoroutine(LoadScene(message.Scene, false));
        }

        IEnumerator LoadScene(string scene, bool host)
        {
            _Callbacks.ReportStatus("Loading " + scene + "...");
            if (host)
                DestroyPreviousSceneObjects();
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            _ClientObjects.PrepareToSpawnSceneObjects();
            _Callbacks.ResetMotion();
            if (host)
                CompleteHostLoad();
            else
                _Client.Send(new HotelTravelReady { Version = _Version, Scene = scene });
            SetLoading(false);
            _Callbacks.ReportStatus("Connected - " + scene + ".");
        }

        void CompleteHostLoad()
        {
            _ServerLoaded = true;
            _Callbacks.PositionCharacters();
            if (_Server.LocalPlayer != null)
            {
                _Server.LocalPlayer.SceneIsReady = true;
                _Waiting.Remove(_Server.LocalPlayer);
            }
            _ServerObjects.SpawnSceneObjects();
            foreach (var player in _Server.AuthenticatedPlayers)
            {
                if (player.SceneIsReady)
                    _ServerObjects.SpawnVisibleObjects(player);
            }
        }

        void OnPeerReady(INetworkPlayer player, HotelTravelReady message)
        {
            if (!_Waiting.ContainsKey(player) || message.Version != _Version || message.Scene != TargetScene)
                return;
            _Waiting.Remove(player);
            player.SceneIsReady = true;
            if (_ServerLoaded)
                _ServerObjects.SpawnVisibleObjects(player);
        }

        void OnPeerDisconnected(INetworkPlayer player) => _Waiting.Remove(player);

        void Update()
        {
            if (!_Server || !_Server.Active)
                return;
            _Expired.Clear();
            foreach (var entry in _Waiting)
            {
                if (Time.unscaledTime <= entry.Value)
                    continue;
                _Expired.Add(entry.Key);
            }
            foreach (var player in _Expired)
                if (_Waiting.Remove(player)) player.Disconnect();
        }

        void SetLoading(bool loading)
        {
            Loading = loading;
            if (_Session != null)
                _Session.Loading = loading;
            StateChanged?.Invoke();
        }

        public void SessionEnded()
        {
            StopAllCoroutines();
            _Waiting.Clear();
            _Version = 0;
            TargetScene = "Lobby";
            _ServerLoaded = false;
            SetLoading(false);
            if (SceneManager.GetActiveScene().name == "Game" && Application.CanStreamedLevelBeLoaded("Lobby"))
                StartCoroutine(ReturnOffline());
        }

        IEnumerator ReturnOffline()
        {
            SetLoading(true);
            yield return SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Single);
            SetLoading(false);
        }

        void OnDestroy() => Unconfigure();
    }
}
