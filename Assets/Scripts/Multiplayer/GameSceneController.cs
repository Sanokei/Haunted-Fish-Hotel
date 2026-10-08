using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Multiplayer
{
    // Owns Game's lifecycle and installs its behavior on persistent and newly enabled players.
    public sealed class GameSceneController : MonoBehaviour, IHotelScene, IGameMovementEnvironment
    {
        [SerializeField]
        Vector3 _SpawnOrigin = new Vector3(-10, 1.2f, 0);
        [SerializeField]
        float _SpawnSpacing = 2;
        [SerializeField, Min(0)] float _GhostSetupSeconds = 10;
        [SerializeField, Min(.1f)] float _GhostFlightSpeed = 8;
        [SerializeField] BoxCollider _HallwayFloor, _HallwayCeiling;
        [SerializeField] Text _SetupCaption;
        bool _SetupStarted;
        float _SetupDeadline;
        int _DisplayedSetupSecond = -1;
        public float GhostFlightSpeed => float.IsNaN(_GhostFlightSpeed) || float.IsInfinity(_GhostFlightSpeed) ? 8 : Mathf.Max(.1f, _GhostFlightSpeed);
        float GhostSetupSeconds => float.IsNaN(_GhostSetupSeconds) || float.IsInfinity(_GhostSetupSeconds) ? 10 : Mathf.Max(0, _GhostSetupSeconds);
        public float HallwayCeilingY => _HallwayCeiling ? _HallwayCeiling.bounds.min.y : 4.11f;
        public Bounds HallwayBounds => _HallwayFloor ? _HallwayFloor.bounds : new Bounds(new Vector3(0, -.5f, 0), new Vector3(30, 1, 2));
        public Vector3 ClampGhost(Vector3 point) => new Vector3(Mathf.Clamp(point.x, HallwayBounds.min.x, HallwayBounds.max.x), Mathf.Clamp(point.y, HallwayBounds.max.y + .5f, _MouseBounds.yMax), 0);
        public Vector3 ClampFish(HotelPlayerMovement movement, Vector3 point)
        {
            var body = movement.BodyController;
            var scale = body.transform.lossyScale;
            var center = Vector3.Scale(body.center, scale);
            float radius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, body.height * Mathf.Abs(scale.y) * .5f);
            var floor = HallwayBounds;
            float top = HallwayCeilingY;
            point.x = Mathf.Clamp(point.x, floor.min.x + radius - center.x, floor.max.x - radius - center.x);
            point.y = Mathf.Clamp(point.y, floor.max.y + halfHeight - center.y, top - halfHeight - center.y);
            point.z = -center.z;
            return point;
        }
        [SerializeField]
        Rect _MouseBounds = new Rect(-15, -2, 30, 18);
        [SerializeField]
        float _MouseLightDepth = -3;
        [SerializeField]
        GameCameraOwner _Camera;
        [SerializeField]
        GameMouseSpotlight _Spotlight;
        readonly HashSet<GameSideScrollMotor> _Motors = new HashSet<GameSideScrollMotor>();
        bool _Entered, _Chosen, _Watching, _Watched, _Released;
        int _WatchedVersion;
        HauntedHotelMultiplayer _Session;
        readonly HashSet<HotelPlayer> _Players = new HashSet<HotelPlayer>();
        readonly GameRoundGate _Gate = new GameRoundGate();
        [SerializeField]
        Texture2D _SelectionArrow;
        [SerializeField]
        GhostSelectionPresentation _Selection;
        [SerializeField]
        GhostPlacementWorld _PlacementWorld;
        [SerializeField]
        GhostPlacementController _GhostControls;
        [SerializeField]
        GameEditorRoleSwitch _EditorSwitch;
        [SerializeField]
        TrapManager _Traps;
        public static GameSceneController Current { get; private set; }
        public GhostPlacementWorld PlacementWorld => _PlacementWorld;
        public TrapManager Traps => _Traps;
        [SerializeField] GameHauntingController _Haunting;
        public GameHauntingController Haunting => _Haunting;
        public HauntedHotelMultiplayer Session => _Session;

        readonly List<HotelPlayer> _OrderedPlayers = new List<HotelPlayer>();
        bool _RosterDirty;
        static readonly System.Comparison<HotelPlayer> ByPlayerId = (a, b) => a.NetId.CompareTo(b.NetId);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCurrent() => Current = null;
        void Update()
        {
            if (_RosterDirty)
            {
                _OrderedPlayers.Clear();
                foreach (var member in _Players)
                    if (member)
                        _OrderedPlayers.Add(member);
                _OrderedPlayers.Sort(ByPlayerId);
                _RosterDirty = false;
            }

            // NetIds can be assigned after OnEnable during network spawning.
            for (int i = 1; i < _OrderedPlayers.Count; i++)
                if (_OrderedPlayers[i - 1].NetId > _OrderedPlayers[i].NetId)
                {
                    _OrderedPlayers.Sort(ByPlayerId);
                    break;
                }

            var players = _OrderedPlayers;
            if (players.Count == 0)
                return;
            bool authority = true, missingRoster = false, ready = true;
            int maximumVersion = 0;
            HotelPlayer current = null;
            foreach (var member in players)
            {
                authority &= !member.Networked || member.IsServer;
                missingRoster |= string.IsNullOrEmpty(member.RoundRoster);
                ready &= member.ControlsReady && (!member.Networked || member.Identity.Owner == null || member.Identity.Owner.SceneIsReady);
                maximumVersion = Mathf.Max(maximumVersion, member.RoundVersion);
                if (!current && !string.IsNullOrEmpty(member.RoundStateKey))
                    current = member;
            }

            if (_Chosen && authority && missingRoster)
            {
                if (_Released || _SetupStarted)
                {
                    if (current)
                    {
                        string joinedRoster = string.Join(",", players.Select(p => p.NetId.ToString()));
                        foreach (var member in players)
                        {
                            if (string.IsNullOrEmpty(member.RoundRoster))
                            {
                                member.ClearRoundInventory();
                                member.GhostId = current.GhostId;
                                member.RoundStateKey = current.RoundStateKey;
                                member.RoundVersion = current.RoundVersion;
                                member.RoundReleased = false;
                                member.GhostSetupRemaining = Mathf.Max(0, _SetupDeadline - Time.unscaledTime);
                                _Gate.Join(member.NetId, current.RoundVersion, Time.unscaledTime);
                            }

                            member.RoundRoster = joinedRoster;
                        }
                    }
                }
                else
                {
                    _Chosen = false;
                    foreach (var member in players)
                    {
                        member.RoundReleased = false;
                        member.ControlledCube = -1;
                    }
                }
            }

            if (!_Chosen && authority && (_Session == null || players.Count == _Session.PlayerCount) && ready)
            {
                _Chosen = true;
                _Released = _SetupStarted = false;
                var ghost = players[Random.Range(0, players.Count)].NetId;
                var roster = string.Join(",", players.Select(p => p.NetId.ToString()));
                int version = maximumVersion + 1;
                string key = System.Guid.NewGuid().ToString("N");
                _PlacementWorld.BeginRound(players[0].InventoryScope, key, version);
                _Gate.Begin(players.Select(p => p.NetId), version, Time.unscaledTime);
                foreach (var player in players)
                {
                    player.ClearRoundInventory();
                    player.RoundStateKey = key;
                    player.RoundReleased = false;
                    player.ResetEditorRole();
                    player.ControlledCube = -1;
                    player.GhostId = ghost;
                    player.RoundRoster = roster;
                    player.RoundVersion = version;
                    if (player.NetId == ghost)
                        player.PlacedObjectsJson = _PlacementWorld.Snapshot;
                }
            }

            var local = HotelPlayer.LocalPlayer;
            if (local && !string.IsNullOrEmpty(local.RoundStateKey))
                _PlacementWorld.BeginRound(local.InventoryScope, local.RoundStateKey, local.RoundVersion);
            if (local && (string.IsNullOrEmpty(local.RoundRoster) || local.RoundVersion != _WatchedVersion) && (_Watching || _Watched))
            {
                if (_Selection)
                    _Selection.Close();
                _Watching = _Watched = false;
            }

            if (!_Watching && !_Watched && local && !string.IsNullOrEmpty(local.RoundRoster))
            {
                _Watching = true;
                _WatchedVersion = local.RoundVersion;
                _Selection.Play(_SelectionArrow, local.RoundRoster.Split(',').Select(uint.Parse).ToArray(), local.GhostId, local.NetId, () =>
                {
                    _Watched = true;
                    if (local)
                    {
                        if (local.Networked)
                            local.FinishGhostSelection(_WatchedVersion);
                        else
                            Acknowledge(local, _WatchedVersion);
                    }
                });
            }

            if (local && local.GhostSetupReady && _Watching)
            {
                if (_Selection)
                    _Selection.Close();
                _Watching = false;
            }
            if (authority && _SetupStarted && !_Released)
            {
                float remaining = Mathf.Max(0, _SetupDeadline - Time.unscaledTime);
                foreach (var member in players) member.GhostSetupRemaining = remaining;
                if (remaining <= 0)
                {
                    _Released = true;
                    foreach (var member in players)
                        if (member.RoundIntroComplete) member.RoundReleased = true;
                }
            }
            if (_SetupCaption)
            {
                bool show = local && local.RoundIntroComplete && !local.RoundReleased;
                _SetupCaption.gameObject.SetActive(show);
                int seconds = show ? Mathf.CeilToInt(local.GhostSetupRemaining) : -1;
                if (seconds != _DisplayedSetupSecond)
                {
                    _DisplayedSetupSecond = seconds;
                    _SetupCaption.text = "GHOST SETUP  " + seconds + "s  -  Fish wait for the haunting";
                }
            }
        }

        public void Acknowledge(HotelPlayer player, int version)
        {
            if (!_Chosen || !_Players.Contains(player) || !_Gate.Finish(player.NetId, version, Time.unscaledTime))
                return;
            if (_Released || _SetupStarted)
            {
                player.RoundIntroComplete = true;
                player.RoundReleased = _Released;
                player.GhostSetupRemaining = Mathf.Max(0, _SetupDeadline - Time.unscaledTime);
            }
            else
                ReleaseIfFinished();
        }

        void ReleaseIfFinished()
        {
            if (!_Chosen || _Released || _SetupStarted || _Players.Count == 0 || !_Gate.Complete)
                return;
            _SetupStarted = true;
            _SetupDeadline = Time.unscaledTime + GhostSetupSeconds;
            foreach (var player in _Players)
                if (player)
                {
                    player.RoundIntroComplete = true;
                    player.GhostSetupRemaining = GhostSetupSeconds;
                }
        }

        void OnEnable() => Activate();
        void OnDisable() => Exit();
        public void Enter(HauntedHotelMultiplayer session)
        {
            _Session = session;
            Activate();
        }

        void Activate()
        {
            if (_Entered || !isActiveAndEnabled)
                return;
            _Entered = true;
            Current = this;
            if (_EditorSwitch)
                _EditorSwitch.enabled = Application.isEditor;
            if (_PlacementWorld)
            {
                _PlacementWorld.enabled = true;
                _PlacementWorld.BindControls(_GhostControls);
            }

            if (_GhostControls)
            {
                _GhostControls.enabled = true;
                _GhostControls.Configure(_Camera, this);
            }

            HotelPlayer.PlayerEnabled += AttachPlayer;
            HotelPlayer.PlayerDisabled += DetachPlayer;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
                AttachPlayer(HotelPlayer.ActivePlayers[i]);
            if (_Camera)
                _Camera.enabled = true;
            if (_Spotlight)
                _Spotlight.enabled = true;
        }

        public void Exit()
        {
            if (!_Entered)
                return;
            _Entered = false;
            if (Current == this)
                Current = null;
            HotelPlayer.PlayerEnabled -= AttachPlayer;
            HotelPlayer.PlayerDisabled -= DetachPlayer;
            foreach (var motor in _Motors)
                if (motor)
                    motor.Unbind(this);
            _Motors.Clear();
            _Players.Clear();
            _OrderedPlayers.Clear();
            _RosterDirty = true;
            _Chosen = _Watching = _Watched = _Released = _SetupStarted = false;
            if (_SetupCaption) _SetupCaption.gameObject.SetActive(false);
            if (_Selection)
                _Selection.Close();
            if (_GhostControls)
                _GhostControls.enabled = false;
            if (_EditorSwitch)
                _EditorSwitch.enabled = false;
            if (_PlacementWorld)
                _PlacementWorld.enabled = false;
            if (_Camera)
                _Camera.enabled = false;
            if (_Spotlight)
                _Spotlight.enabled = false;
        }

        void AttachPlayer(HotelPlayer player)
        {
            if (!player.isActiveAndEnabled || !player.TryGetComponent<GameSideScrollMotor>(out var motor))
                return;
            if (!_Players.Add(player))
                return;
            _RosterDirty = true;
            if (!player.Networked || player.IsServer)
            {
                player.RoundReleased = false;
                player.RoundRoster = "";
                player.ClearRoundInventory();
            }

            if (_Chosen && !_Released && !_SetupStarted && (!player.Networked || player.IsServer))
            {
                // New participants join through the same mandatory round presentation.
                _Chosen = false;
                foreach (var member in _Players)
                    if (member)
                    {
                        member.RoundReleased = false;
                        member.RoundRoster = "";
                    }
            }

            _Motors.Add(motor);
            motor.Bind(this);
        }

        void DetachPlayer(HotelPlayer player)
        {
            if (player == HotelPlayer.LocalPlayer)
            {
                if (_Selection)
                    _Selection.Close();
                _Watching = _Watched = false;
                _WatchedVersion = 0;
            }

            if (!player.TryGetComponent<GameSideScrollMotor>(out var motor))
                return;
            bool lostGhost = _Chosen && player.NetId == player.GhostId;
            _Players.Remove(player);
            _RosterDirty = true;
            if (lostGhost && (!player.Networked || player.IsServer))
            {
                _Chosen = false;
                foreach (var remaining in _Players)
                    if (remaining)
                    {
                        remaining.RoundReleased = false;
                        remaining.RoundRoster = "";
                    }
            }

            _Gate.Remove(player.NetId);
            ReleaseIfFinished();
            _Motors.Remove(motor);
            motor.Unbind(this);
        }

        public Vector3 SpawnPosition(int index) => _SpawnOrigin + Vector3.right * (index * _SpawnSpacing);
        // Both local input and authoritative server input use the same scene-owned constraint.
        public Vector3 ClampMouse(Vector3 point) => new Vector3(Mathf.Clamp(point.x, _MouseBounds.xMin, _MouseBounds.xMax), Mathf.Clamp(point.y, _MouseBounds.yMin, _MouseBounds.yMax), _MouseLightDepth);
    }
}
