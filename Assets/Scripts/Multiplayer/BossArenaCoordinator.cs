using System;
using System.Collections;
using System.Collections.Generic;
using HauntedFish.BossFight;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    [Serializable]
    public sealed class HotelBossSnapshot
    {
        public string RoomScope, RoundKey, Arena;
        public int RoundVersion;
    }

    // Participant-only additive arena transport. The arena owns physics/scoring;
    // persistent hotel actors retain their network identity, room, round and inventory.
    public sealed class BossArenaCoordinator : MonoBehaviour
    {
        [SerializeField] BoxCollider _Entrance;
        [SerializeField] Text _Prompt;
        [SerializeField] GhostPlacementWorld _World;
        [SerializeField] GameCameraOwner _GameCamera;
        [SerializeField] string _SceneName = "BossFight";
        [SerializeField, Min(0)] float _ReturnGhostSeconds = 3;
        readonly Dictionary<HotelPlayer, Vector3> _Watchers = new Dictionary<HotelPlayer, Vector3>();
        float _ReturnDeadline;
        string _ReturnScope, _ReturnKey;
        int _ReturnVersion;
        bool _EditorPreview;
        AsyncOperation _LoadOperation;
        BossArenaManager _Arena;
        HotelPlayer _Fish, _Ghost, _PendingFish;
        Camera _HallwayCamera;
        HotelViewCamera _HallwayBinding;
        bool _Loading, _CameraInArena, _AuthorityOwned, _MovedToArena;
        readonly HashSet<uint> _Ready = new HashSet<uint>();
        uint _SentReadyEpoch;
        float _NextInput, _ResultUntil;
        int _ResultSeen;
        string _Applied, _ScopeKey, _PresentationRoundKey;
        public static BossArenaCoordinator Current { get; private set; }
        public BossArenaManager Arena => _Arena;
        public bool EntranceContains(HotelPlayer player)
        {
            if (!_Entrance || !_Entrance.enabled || !_Entrance.gameObject.activeInHierarchy || !player || !player.BodyController) return false;
            var body = player.BodyController;
            var scale = body.transform.lossyScale;
            float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius * 2, body.height * Mathf.Abs(scale.y));
            // Observer CharacterControllers are intentionally disabled; their native
            // bounds are empty. The authored capsule dimensions still define proximity.
            var bounds = new Bounds(body.transform.TransformPoint(body.center), new Vector3(radius * 2, height, radius * 2));
            return _Entrance.bounds.Intersects(bounds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
        void OnEnable()
        {
            Current = this;
            _Loading = _LoadOperation != null && !_LoadOperation.isDone;
            if (!_Loading) _LoadOperation = null;
            if (_Arena)
            {
                _AuthorityOwned = Authority; _Arena.SetAuthority(_AuthorityOwned);
                _Arena.Completed -= Completed; _Arena.Completed += Completed;
                _Arena.SnapshotChanged -= Publish; _Arena.SnapshotChanged += Publish;
            }
            SceneManager.sceneLoaded += Loaded;
            HotelPlayer.PlayerDisabled += Departed;
            var scene = SceneManager.GetSceneByName(_SceneName);
            if (scene.isLoaded) BindArena(scene);
        }
        bool Authority
        {
            get
            {
                var players = HotelPlayer.ActivePlayers;
                for (int i = 0; i < players.Count; i++)
                    if (!players[i].Networked || players[i].IsServer) return true;
                return false;
            }
        }
        public bool TryEnter(HotelPlayer fish, string key, int version)
        {
            if (!Authority || !fish || fish.Networked && !fish.IsServer || !fish.ControlsReady || !fish.RoundReleased ||
                fish.InBossFight || fish.BossHallwayLocked || Time.unscaledTime < _ReturnDeadline || fish.ControlMode != HotelControlMode.Fish || fish.RoundStateKey != key ||
                fish.RoundVersion != version || !_World || _World.RoundKey != key || _World.RoomScope != fish.InventoryScope ||
                !EntranceContains(fish) || _PendingFish || _Arena && _Arena.Active) return false;
            var ghost = FindPlayer(fish.GhostId);
            if (!ghost || !ghost.ControlsReady || ghost.InBossFight || ghost.ControlMode != HotelControlMode.Ghost ||
                ghost.RoundStateKey != key || ghost.InventoryScope != fish.InventoryScope) return false;
            _PendingFish = fish;
            if (_Arena) BeginPending();
            else if (!_Loading) StartCoroutine(LoadArena());
            return true;
        }
        static HotelPlayer FindPlayer(uint id)
        {
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++) if (players[i].NetId == id) return players[i];
            return null;
        }
        IEnumerator LoadArena()
        {
            _Loading = true;
            if (!Application.CanStreamedLevelBeLoaded(_SceneName))
            {
                Debug.LogError("Add the authored BossFight scene to build settings.", this);
                _PendingFish = null; _Loading = false; yield break;
            }
            _LoadOperation = SceneManager.LoadSceneAsync(_SceneName, LoadSceneMode.Additive);
            yield return _LoadOperation;
            _LoadOperation = null; _Loading = false;
        }
        void Loaded(Scene scene, LoadSceneMode mode) { if (scene.name == _SceneName) { _LoadOperation = null; _Loading = false; BindArena(scene); } }
        void BindArena(Scene scene)
        {
            if (_Arena) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                _Arena = root.GetComponentInChildren<BossArenaManager>(true);
                if (_Arena) break;
            }
            if (!_Arena) { Debug.LogError("Authored BossFight scene is missing its arena manager.", this); _PendingFish = null; return; }
            _AuthorityOwned = Authority;
            _Arena.SetAuthority(_AuthorityOwned);
            _Arena.Completed += Completed;
            _Arena.SnapshotChanged += Publish;
            if (_PendingFish) BeginPending();
        }
        void BeginPending()
        {
            var fish = _PendingFish; _PendingFish = null;
            if (!fish || !fish.ControlsReady || !fish.RoundReleased || fish.ControlMode != HotelControlMode.Fish || !EntranceContains(fish)) return;
            var ghost = FindPlayer(fish.GhostId);
            if (!ghost || !ghost.ControlsReady || ghost.InBossFight || ghost.ControlMode != HotelControlMode.Ghost || ghost.RoundStateKey != fish.RoundStateKey || ghost.RoundVersion != fish.RoundVersion || ghost.InventoryScope != fish.InventoryScope) return;
            _Fish = fish; _Ghost = ghost;
            fish.BossReturnPosition = fish.transform.position;
            ghost.BossReturnPosition = _World.Controls && ghost.IsRelevantPlayer ? _World.Controls.FlightPosition : ghost.GhostFlightReady ? ghost.GhostFlightPosition : ghost.transform.position;
            _ScopeKey = fish.RoundStateKey;
            _AuthorityOwned = true; _MovedToArena = false; _Ready.Clear();
            _Arena.SetAuthority(true);
            if (!_Arena.TryBegin(fish.NetId, ghost.NetId)) { _Fish = _Ghost = null; return; }
            _World.SuspendGhost(ghost);
            fish.ResetSceneMotion(); ghost.ResetSceneMotion();
            fish.InBossFight = ghost.InBossFight = true;
            fish.BossMatchEpoch = ghost.BossMatchEpoch = _Arena.MatchEpoch;
            if (!fish.Networked || fish.Identity.Owner == null || fish.IsRelevantPlayer) _Ready.Add(fish.NetId);
            if (!ghost.Networked || ghost.Identity.Owner == null || ghost.IsRelevantPlayer) _Ready.Add(ghost.NetId);
            WatchPlayers();
            BeginWhenReady();
            Publish(_Arena.Snapshot);
        }
        public bool AcceptReady(HotelPlayer player, string key, int version, uint epoch)
        {
            if (!_AuthorityOwned || !_Arena || !_Arena.Active || !player || !player.InBossFight ||
                player.Networked && !player.IsServer || key != _ScopeKey || player.RoundStateKey != key ||
                player.RoundVersion != version || epoch != _Arena.MatchEpoch || player != _Fish && player != _Ghost) return false;
            _Ready.Add(player.NetId); BeginWhenReady(); return true;
        }
        void BeginWhenReady()
        {
            if (_MovedToArena || !_Arena || !_Fish || !_Ghost || !_Ready.Contains(_Fish.NetId) || !_Ready.Contains(_Ghost.NetId)) return;
            _MovedToArena = true;
            _Fish.Teleport(_Arena.FishSpawn); _Ghost.Teleport(_Arena.GhostSpawn);
            _Arena.SetParticipantsReady(true);
        }
        public bool AcceptInput(HotelPlayer player, Vector2 axis, string key, int version, uint epoch)
        {
            return Authority && _Arena && _Arena.Active && player && player.InBossFight &&
                (!player.Networked || player.IsServer) && player.RoundStateKey == key && player.RoundVersion == version &&
                key == _ScopeKey && epoch == _Arena.MatchEpoch && float.IsFinite(axis.x) && float.IsFinite(axis.y) &&
                _Arena.ApplyInput(player.NetId, Vector2.ClampMagnitude(axis, 1));
        }
        void Publish(string state)
        {
            if (!Authority || !_Fish) return;
            var json = JsonUtility.ToJson(new HotelBossSnapshot { RoomScope = _Fish.InventoryScope, RoundKey = _Fish.RoundStateKey, RoundVersion = _Fish.RoundVersion, Arena = state });
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
                if (players[i].InventoryScope == _Fish.InventoryScope && players[i].RoundStateKey == _Fish.RoundStateKey && players[i].RoundVersion == _Fish.RoundVersion)
                    players[i].BossArenaJson = json;
        }
        bool SameRound(HotelPlayer player, string scope, string key, int version) => player && player.InventoryScope == scope && player.RoundStateKey == key && player.RoundVersion == version;
        void WatchPlayers()
        {
            if (!_AuthorityOwned || !_Arena || !_Arena.Active || !_Fish) return;
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var actor = players[i];
                if (!SameRound(actor, _Fish.InventoryScope, _Fish.RoundStateKey, _Fish.RoundVersion) || _Watchers.ContainsKey(actor)) continue;
                var point = actor == _Fish || actor == _Ghost ? actor.BossReturnPosition : actor.transform.position;
                _Watchers.Add(actor, point); actor.BossReturnPosition = point;
                actor.BossWatching = true; actor.BossReturnLocked = false; actor.BossReturnSetupRemaining = 0;
                actor.ResetSceneMotion();
                actor.BossArenaJson = _Fish.BossArenaJson;
            }
        }
        void TickReturnGate()
        {
            if (!Authority || string.IsNullOrEmpty(_ReturnKey)) return;
            float remaining = Mathf.Max(0, _ReturnDeadline - Time.unscaledTime);
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var actor = players[i];
                if (!SameRound(actor, _ReturnScope, _ReturnKey, _ReturnVersion)) continue;
                actor.BossReturnSetupRemaining = remaining;
                actor.BossReturnLocked = remaining > 0 && actor.ControlMode != HotelControlMode.Ghost;
            }
            if (remaining <= 0) _ReturnKey = null;
        }
        void Completed(BossMatchResult result)
        {
            if (!_Fish || !_Arena || result.Epoch != _Arena.MatchEpoch) return;
            bool loss = !result.WinnerFish && !result.Cancelled;
            _ReturnScope = _Fish.InventoryScope; _ReturnKey = _Fish.RoundStateKey; _ReturnVersion = _Fish.RoundVersion;
            _ReturnDeadline = loss ? Time.unscaledTime + (float.IsFinite(_ReturnGhostSeconds) ? Mathf.Max(0, _ReturnGhostSeconds) : 3) : Time.unscaledTime;
            _Fish.BossWon = result.WinnerFish && !result.Cancelled;
            _Fish.BossResultVersion++;
            foreach (var pair in _Watchers)
            {
                var actor = pair.Key;
                if (!actor) continue;
                actor.BossWatching = false;
                actor.BossReturnPosition = loss && actor == _Fish && GameSceneController.Current ? GameSceneController.Current.SpawnPosition(0) : pair.Value;
                actor.BossReturnLocked = loss && actor != _Ghost;
                ReturnActor(actor, actor == _Ghost);
            }
            _Watchers.Clear(); _Fish = _Ghost = null;
            TickReturnGate();
        }
#if UNITY_EDITOR
        public void EditorGoToBoss()
        {
            WatchPlayers(); TickReturnGate();
            var local = HotelPlayer.LocalPlayer;
            if (!local || !local.RoundReleased || local.BossReturnLocked) return;
            if (_EditorPreview)
            {
                _EditorPreview = false; local.BossWatching = false; local.ResetSceneMotion(); SetCamera(0); return;
            }
            if (_Arena && _Arena.Active) return;
            if (local.ControlMode == HotelControlMode.Fish && FindPlayer(local.GhostId) && FindPlayer(local.GhostId) != local)
            {
                var old = local.transform.position;
                local.Teleport(_Entrance.bounds.center - local.BodyController.center);
                if (TryEnter(local, local.RoundStateKey, local.RoundVersion)) return;
                local.Teleport(old);
            }
            // A development preview never manufactures a ghost or a scored match.
            // A normal two-player challenge still uses the production entrance/RPC.
            _EditorPreview = true; local.BossWatching = true; local.ResetSceneMotion();
            if (!_Arena && !_Loading) StartCoroutine(LoadArena());
        }
#endif
        void ReturnActor(HotelPlayer player, bool ghost)
        {
            player.InBossFight = false;
            player.ResetSceneMotion();
            player.Teleport(player.BossReturnPosition);
            if (ghost && _World) _World.RestoreFlight(player, player.BossReturnPosition);
        }
        void Departed(HotelPlayer player)
        {
            if (_PendingFish == player) _PendingFish = null;
            _Watchers.Remove(player);
            if (_Arena && _AuthorityOwned && _Arena.Active) _Arena.Cancel(player.NetId);
        }
        void Update()
        {
            WatchPlayers(); TickReturnGate();
            var local = HotelPlayer.LocalPlayer;
            if (!local || !local.ControlsReady) { SetCamera(0); if (_Prompt) _Prompt.gameObject.SetActive(false); return; }
            if (_PresentationRoundKey != local.RoundStateKey)
            {
                _PresentationRoundKey = local.RoundStateKey;
                _ResultSeen = local.BossResultVersion; _ResultUntil = 0;
                _SentReadyEpoch = 0; _Applied = null;
            }
            if (!Authority && !string.IsNullOrEmpty(local.BossArenaJson) && _Applied != local.BossArenaJson)
            {
                HotelBossSnapshot snapshot;
                try { snapshot = JsonUtility.FromJson<HotelBossSnapshot>(local.BossArenaJson); } catch (ArgumentException) { snapshot = null; }
                if (snapshot != null && snapshot.RoomScope == local.InventoryScope && snapshot.RoundKey == local.RoundStateKey && snapshot.RoundVersion == local.RoundVersion)
                {
                    if (!_Arena && !_Loading) StartCoroutine(LoadArena());
                    if (_Arena && _Arena.ApplySnapshot(snapshot.Arena)) _Applied = local.BossArenaJson;
                }
            }
            if (local.InBossFight && _Arena && local.BossMatchEpoch == _Arena.MatchEpoch && _SentReadyEpoch != _Arena.MatchEpoch)
            {
                _SentReadyEpoch = _Arena.MatchEpoch;
                if (local.Networked) local.ReadyBossScene(local.RoundStateKey, local.RoundVersion, _Arena.MatchEpoch);
                else AcceptReady(local, local.RoundStateKey, local.RoundVersion, _Arena.MatchEpoch);
            }
            SetCamera(local.BossWatching && _Arena ? (local.Networked ? local.NetId : 1u) : 0);
            if (local.InBossFight && _Arena && Time.unscaledTime >= _NextInput)
            {
                _NextInput = Time.unscaledTime + .05f;
                var axis = local.InputBlocked ? Vector2.zero : local.Movement.BossAxis;
                if (local.Networked) local.SendBossInput(axis, local.RoundStateKey, local.RoundVersion, _Arena.MatchEpoch);
                else AcceptInput(local, axis, local.RoundStateKey, local.RoundVersion, _Arena.MatchEpoch);
            }
            bool entrance = local.RoundReleased && !local.InBossFight && !local.BossHallwayLocked && local.BossReturnSetupRemaining <= 0 && local.ControlMode == HotelControlMode.Fish && EntranceContains(local);
            if (entrance && !local.InputBlocked && local.Movement.InteractPressed)
            {
                if (local.Networked) local.RequestBossFight(local.RoundStateKey, local.RoundVersion);
                else TryEnter(local, local.RoundStateKey, local.RoundVersion);
            }
            if (local.BossResultVersion != _ResultSeen) { _ResultSeen = local.BossResultVersion; _ResultUntil = Time.unscaledTime + 3; }
            if (_Prompt)
            {
                bool result = Time.unscaledTime < _ResultUntil;
                bool waiting = !_EditorPreview && local.BossWatching && (!_Arena || !_Arena.ParticipantsReady);
                _Prompt.gameObject.SetActive(_EditorPreview || entrance || result || waiting || local.BossReturnSetupRemaining > 0);
                _Prompt.text = _EditorPreview ? "Editor arena preview: two real players are required to score." : local.BossReturnSetupRemaining > 0 ? "Ghost head start: " + Mathf.CeilToInt(local.BossReturnSetupRemaining) + "s" : waiting ? "Entering the ghost challenge..." : result ? (local.BossWon ? "You won the ghost's challenge!" : "Returned to the hallway.") : "E / X: upstairs to the ghost challenge";
            }
        }
        void SetCamera(uint localId)
        {
            bool inArena = localId != 0 && _Arena;
            if (_CameraInArena == inArena) return;
            _CameraInArena = inArena;
            if (inArena)
            {
                _HallwayCamera = HotelViewCamera.Current;
                _HallwayBinding = _HallwayCamera ? _HallwayCamera.GetComponent<HotelViewCamera>() : null;
                _Arena.SetLocalViewer(localId, true);
                if (_HallwayCamera) _HallwayCamera.enabled = false;
                if (_HallwayBinding) _HallwayBinding.enabled = false;
            }
            else
            {
                if (_HallwayCamera) _HallwayCamera.enabled = true;
                if (_HallwayBinding) _HallwayBinding.enabled = true;
                if (_Arena) _Arena.SetLocalViewer(0, false);
                if (_GameCamera) _GameCamera.ResetFollow();
                _HallwayCamera = null; _HallwayBinding = null;
            }
        }
        void OnDisable()
        {
            SceneManager.sceneLoaded -= Loaded;
            HotelPlayer.PlayerDisabled -= Departed;
            if (_Arena)
            {
                if (_AuthorityOwned && _Arena.Active) _Arena.Cancel(_Arena.FishId);
                _Arena.Completed -= Completed; _Arena.SnapshotChanged -= Publish;
            }
            if (_AuthorityOwned)
            {
                if (_Fish) ReturnActor(_Fish, false);
                if (_Ghost) ReturnActor(_Ghost, true);
            }
            foreach (var pair in _Watchers) if (pair.Key) { pair.Key.BossWatching = false; pair.Key.BossReturnPosition = pair.Value; ReturnActor(pair.Key, pair.Key.ControlMode == HotelControlMode.Ghost); }
            _Watchers.Clear();
            var actors = HotelPlayer.ActivePlayers;
            for (int i = 0; i < actors.Count; i++) if (SameRound(actors[i], _ReturnScope, _ReturnKey, _ReturnVersion) || _EditorPreview && actors[i] == HotelPlayer.LocalPlayer) { actors[i].BossWatching = false; actors[i].BossReturnLocked = false; actors[i].BossReturnSetupRemaining = 0; }
            SetCamera(0); _PendingFish = null; _Fish = _Ghost = null; _ReturnKey = null; _ReturnDeadline = 0; _EditorPreview = false;
            StopAllCoroutines(); _Loading = false;
            if (Current == this) Current = null;
        }
    }
}
