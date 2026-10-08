using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    [Serializable]
    public struct GhostCubePlacement
    {
        public int Id;
        public uint OwnerId;
        public Vector3 Position, Origin;
        public float Rotation, Progress;
        public string FamilyTag;
        public int Phase, Activations;
    }

    [Serializable]
    public sealed class GhostCubeSnapshot
    {
        public string RoomScope, RoundKey;
        public int RoundVersion, Revision;
        public List<GhostCubePlacement> Cubes = new List<GhostCubePlacement>();
    }

    // The authority owns inventory. Observers reconstruct authored prefab assets only within their current round.
    public sealed class GhostPlacementWorld : MonoBehaviour
    {
        public const float InteractionTolerance = .75f;
        [SerializeField]
        GhostTrap[] _TrapPrefabs = Array.Empty<GhostTrap>();
        [SerializeField]
        Transform _TrapContainer;
        [Tooltip("Authored trigger marking the starting area. Trap footprints and cart travel cannot enter it.")]
        [SerializeField] BoxCollider _GraceZone;
        [Tooltip("Maximum horizontal placement reach for a hallway ceiling trap; vertical flight within the hallway does not require floor alignment.")]
        [SerializeField, Min(.1f)] float _HallwayPlacementReach = 4;
        public Bounds GraceBounds => _GraceZone ? _GraceZone.bounds : default;
        public bool HasGraceZone => _GraceZone && _GraceZone.enabled && _GraceZone.gameObject.activeInHierarchy;
        public bool FootprintAllowed(Vector3 center, Vector3 half)
        {
            if (!HasGraceZone) return true;
            var safe = GraceBounds;
            return center.x - half.x >= safe.max.x - .0001f || center.x + half.x <= safe.min.x + .0001f ||
                center.z - half.z >= safe.max.z || center.z + half.z <= safe.min.z;
        }
        public Vector2 TravelLimits(Vector3 origin, Vector3 half, Vector3 offset, float travel)
        {
            var hallway = GameSceneController.Current ? GameSceneController.Current.HallwayBounds : new Bounds(new Vector3(0, -.5f, 0), new Vector3(30, 1, 2));
            float left = hallway.min.x + half.x - offset.x, right = hallway.max.x - half.x - offset.x;
            if (HasGraceZone && origin.z + offset.z + half.z > GraceBounds.min.z && origin.z + offset.z - half.z < GraceBounds.max.z)
            {
                if (origin.x + offset.x >= GraceBounds.max.x) left = Mathf.Max(left, GraceBounds.max.x + half.x - offset.x);
                else right = Mathf.Min(right, GraceBounds.min.x - half.x - offset.x);
            }
            return new Vector2(Mathf.Max(left, origin.x - travel), Mathf.Min(right, origin.x + travel));
        }
        GhostCubeSnapshot _State = new GhostCubeSnapshot();
        readonly Dictionary<int, GhostTrap> _Objects = new Dictionary<int, GhostTrap>();
        string _Applied = "";
        int _NextId, _Revision;
        string _CounterScope, _CounterKey;
        int _CounterVersion;
        GhostTrap[] _RequestedPrefabs;
        readonly Dictionary<string, GhostTrap> _LockedDefinitions = new Dictionary<string, GhostTrap>();
        bool _Dirty;
        float _NextPlacement, _NextPublish;
        public string Snapshot => JsonUtility.ToJson(_State);
        public string RoomScope => _State.RoomScope;
        public string RoundKey => _State.RoundKey;
        public int RoundVersion => _State.RoundVersion;
        public int Count => _Objects.Count;

        readonly Dictionary<string, GhostTrap> _Definitions = new Dictionary<string, GhostTrap>();
        GhostTrap[] _BoundPrefabs;
        public GhostPlacementController Controls { get; private set; }

        public void BindControls(GhostPlacementController controls) => Controls = controls;
        void OnValidate() => _BoundPrefabs = null;
        public GhostTrap Definition(string family)
        {
            if (!ReferenceEquals(_BoundPrefabs, _TrapPrefabs))
            {
                _Definitions.Clear();
                foreach (var prefab in _TrapPrefabs)
                    if (prefab && !string.IsNullOrEmpty(prefab.FamilyTag) && !_Definitions.ContainsKey(prefab.FamilyTag))
                        _Definitions.Add(prefab.FamilyTag, prefab);
                _BoundPrefabs = _TrapPrefabs;
            }

            return family != null && _Definitions.TryGetValue(family, out var definition) ? definition : null;
        }

        public GhostTrap Trap(int id) => _Objects.TryGetValue(id, out var trap) ? trap : null;
        public string DefaultFamily => _TrapPrefabs.Length > 0 && _TrapPrefabs[0] ? _TrapPrefabs[0].FamilyTag : "";

        public void InjectDefinitions(IEnumerable<GhostTrap> prefabs)
        {
            _RequestedPrefabs = (prefabs ?? Array.Empty<GhostTrap>()).Where(p => p).ToArray();
            // A family ID cannot acquire a different prefab while live inventory
            // uses it. Preserve that identity through this round on every peer.
            var retained = new List<GhostTrap>();
            foreach (var trap in _Objects.Values)
                if (trap && Definition(trap.FamilyTag))
                    retained.Add(Definition(trap.FamilyTag));
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var player = HotelPlayer.ActivePlayers[i];
                if (player.InventoryScope == _State.RoomScope && player.RoundStateKey == _State.RoundKey &&
                    player.RoundVersion == _State.RoundVersion && !string.IsNullOrEmpty(player.HeldTrapFamily) && Definition(player.HeldTrapFamily))
                    retained.Add(Definition(player.HeldTrapFamily));
            }

            foreach (var old in retained)
            {
                var replacement = Array.Find(_RequestedPrefabs, p => p.FamilyTag == old.FamilyTag);
                if (replacement != old)
                    _LockedDefinitions[old.FamilyTag] = old;
            }

            _TrapPrefabs = _LockedDefinitions.Values.Concat(retained).Concat(_RequestedPrefabs).Where(p => p).GroupBy(p => p.FamilyTag).Select(g => g.First()).ToArray();
        }

        public void BeginRound(string roomScope, string key, int version)
        {
            if (_State.RoomScope == roomScope && _State.RoundKey == key && _State.RoundVersion == version)
                return;
            ResetRound();
            if (_CounterScope != roomScope || _CounterKey != key || _CounterVersion != version)
            {
                _CounterScope = roomScope;
                _CounterKey = key;
                _CounterVersion = version;
                _NextId = _Revision = 0;
                _LockedDefinitions.Clear();
                if (_RequestedPrefabs != null)
                    _TrapPrefabs = _RequestedPrefabs;
            }

            _State.RoomScope = roomScope;
            _State.RoundKey = key;
            _State.RoundVersion = version;
            _State.Revision = _Revision;
        }

        public void ResetRound()
        {
            foreach (var trap in _Objects.Values)
                if (trap)
                {
                    trap.StopInput();
                    trap.gameObject.SetActive(false);
                    Destroy(trap.gameObject);
                }

            _Objects.Clear();
            _Flights.Clear();
            // Only the scoped authority creates a deletion revision. An observer
            // tears down local visuals without outrunning an unchanged server snapshot.
            bool authoritative = false;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var player = HotelPlayer.ActivePlayers[i];
                if ((!player.Networked || player.IsServer) && player.InventoryScope == _State.RoomScope &&
                    player.RoundStateKey == _State.RoundKey && player.RoundVersion == _State.RoundVersion)
                    authoritative = true;
            }
            _State.Cubes.Clear();
            if (authoritative) _State.Revision = ++_Revision;
            var json = Snapshot;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var player = HotelPlayer.ActivePlayers[i];
                if ((player.Networked && !player.IsServer) || player.InventoryScope != _State.RoomScope || player.RoundStateKey != _State.RoundKey || player.RoundVersion != _State.RoundVersion)
                    continue;
                player.ControlledCube = -1;
                player.HeldTrapFamily = "";
                player.GhostFlightReady = false;
                player.PlacedObjectsJson = json;
            }

            _Applied = authoritative ? json : null;
            _Dirty = false;
            _NextPlacement = _NextPublish = 0;
        }

        void OnEnable() => HotelPlayer.PlayerDisabled += ForgetFlight;
        void ForgetFlight(HotelPlayer player) => _Flights.Remove(player);
        void OnDisable()
        {
            HotelPlayer.PlayerDisabled -= ForgetFlight;
            ResetRound();
        }

        public bool PlacementPoint(Vector3 requested, out Vector3 grounded) => PlacementPoint(requested, DefaultFamily, out grounded);
        public bool PlacementPoint(Vector3 requested, string family, out Vector3 grounded)
        {
            var prefab = Definition(family);
            float height = prefab ? prefab.BodyHalfSize.y : .5f;
            var hallway = GameSceneController.Current ? GameSceneController.Current.HallwayBounds : new Bounds(new Vector3(0, -.5f, 0), new Vector3(30, 1, 2));
            var offset = prefab ? prefab.ColliderOffset : Vector3.zero;
            grounded = new Vector3(requested.x, hallway.max.y + height - offset.y, -offset.z);
            if (!prefab || !Finite(requested.x) || !Finite(requested.y) || !Finite(requested.z)) return false;
            var center = grounded + prefab.ArmedOffset + offset;
            var half = prefab.BodyHalfSize;
            float ceiling = GameSceneController.Current ? GameSceneController.Current.HallwayCeilingY : 4.11f;
            return center.x - half.x >= hallway.min.x && center.x + half.x <= hallway.max.x &&
                center.y + half.y <= ceiling + .0001f && FootprintAllowed(center, half) &&
                !FishOccupies(center, prefab.HalfExtents) &&
                !Physics.CheckBox(center, prefab.HalfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        public static bool FishOccupies(Vector3 center, Vector3 halfExtents)
        {
            // CharacterController contacts may not appear in an overlap query in
            // the same frame as Move. Check the registered authority bodies too.
            var area = new Bounds(center, halfExtents * 2);
            var players = HotelPlayer.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var body = player.BodyController;
                if (player.ControlsReady && player.ControlMode == HotelControlMode.Fish && body && body.enabled &&
                    body.gameObject.activeInHierarchy && area.Intersects(body.bounds)) return true;
            }
            return false;
        }
        public static Vector2 TravelBounds(Vector3 origin) => new Vector2(Mathf.Max(-14.5f, origin.x - 2), Mathf.Min(14.5f, origin.x + 2));
        public Vector3 CubePosition(int id) => Trap(id) ? Trap(id).Position : Vector3.zero;
        public int NearbyCube(Vector3 point) => NearbyCube(point, null);
        public int NearbyCube(Vector3 point, HotelPlayer owner)
        {
            int closest = -1;
            float distance = 2;
            foreach (var item in _Objects)
            {
                if (!item.Value || (owner && item.Value.CaptureState().OwnerId != owner.NetId))
                    continue;
                float candidate = Vector3.Distance(point, item.Value.Position);
                if (candidate < distance)
                {
                    closest = item.Key;
                    distance = candidate;
                }
            }

            return closest;
        }

        sealed class FlightMotion
        {
            public Vector3 Position;
            public Vector2 Axis;
            public float LastInput;
        }

        readonly Dictionary<HotelPlayer, FlightMotion> _Flights = new Dictionary<HotelPlayer, FlightMotion>();
        public bool AcceptFlightInput(HotelPlayer player, Vector2 axis, string key, int version)
        {
            if (!RequestMatches(player, key, version) || !Finite(axis.x) || !Finite(axis.y))
                return false;
            var motion = Flight(player);
            motion.Axis = Vector2.ClampMagnitude(axis, 1);
            motion.LastInput = Time.unscaledTime;
            return true;
        }

        FlightMotion Flight(HotelPlayer player)
        {
            if (!_Flights.TryGetValue(player, out var motion))
            {
                var scene = GameSceneController.Current;
                motion = new FlightMotion
                {
                    Position = scene ? scene.SpawnPosition(0) + Vector3.up * 1.5f : new Vector3(0, 2, 0)
                };
                _Flights.Add(player, motion);
            }

            return motion;
        }

        public bool InteractionPosition(HotelPlayer player, Vector3 requested, out Vector3 position)
        {
            position = Vector3.zero;
            if (!CanControl(player) || !Finite(requested.x) || !Finite(requested.y) || !Finite(requested.z))
                return false;
            // The host's local presentation is trusted. A remote owner supplies only
            // bounded flight input; its RPC coordinate never establishes proximity.
            if (!player.Networked || player.IsRelevantPlayer)
            {
                position = Controls ? Controls.FlightPosition : requested;
            }
            else
                position = Flight(player).Position;
            return Vector3.Distance(requested, position) <= InteractionTolerance;
        }

        public bool PlacementNearFlight(HotelPlayer player, Vector3 grounded, string family)
        {
            var definition = Definition(family);
            if (!definition)
                return false;
            Vector3 position = (!player.Networked || player.IsRelevantPlayer) && Controls ? Controls.FlightPosition : Flight(player).Position;
            if (!Finite(grounded.x) || !Finite(grounded.y) || !Finite(grounded.z)) return false;
            if (definition.PlacementMode == TrapPlacementMode.HallwayCeiling)
            {
                var scene = GameSceneController.Current;
                float floor = scene ? scene.HallwayBounds.max.y : 0, ceiling = scene ? scene.HallwayCeilingY : 4.11f;
                return Mathf.Abs(grounded.x - position.x) <= Mathf.Max(.1f, _HallwayPlacementReach) &&
                    Mathf.Abs(position.z) <= InteractionTolerance && position.y >= floor - InteractionTolerance && position.y <= ceiling + InteractionTolerance;
            }
            return Mathf.Abs(grounded.x - position.x) <= InteractionTolerance && Mathf.Abs(grounded.z - position.z) <= .01f &&
                Mathf.Abs(position.y - (grounded.y + definition.ColliderOffset.y)) <= InteractionTolerance;
        }

        void SimulateFlight(HotelPlayer player)
        {
            if (!player.Networked || player.IsRelevantPlayer)
                return;
            var motion = Flight(player);
            var trap = Trap(player.ControlledCube);
            if (trap)
                motion.Position = trap.Position;
            else if (Time.unscaledTime - motion.LastInput <= .3f)
                motion.Position += new Vector3(motion.Axis.x, motion.Axis.y, 0) * ((GameSceneController.Current ? GameSceneController.Current.GhostFlightSpeed : 8) * Time.unscaledDeltaTime);
            motion.Position = GameSceneController.Current ? GameSceneController.Current.ClampGhost(motion.Position) : new Vector3(Mathf.Clamp(motion.Position.x, -15, 15), Mathf.Clamp(motion.Position.y, .5f, 16), 0);
            player.GhostFlightPosition = motion.Position;
            player.GhostFlightReady = true;
        }

        static bool Authority(HotelPlayer player) => player && (!player.Networked || player.IsServer) && player.ControlsReady && !player.InBossFight && !player.BossHallwayLocked && player.GhostSetupReady && player.ControlMode == HotelControlMode.Ghost;
        bool CanControl(HotelPlayer player) => isActiveAndEnabled && Authority(player) && player.InventoryScope == _State.RoomScope && player.RoundStateKey == _State.RoundKey && player.RoundVersion == _State.RoundVersion && !string.IsNullOrEmpty(_State.RoundKey);
        public bool RequestMatches(HotelPlayer player, string key, int version) => CanControl(player) && key == _State.RoundKey && version == _State.RoundVersion;
        public bool Hold(HotelPlayer player, string family, Vector3 position)
        {
            if (!CanControl(player) || !Definition(family) || player.ControlledCube >= 0 || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
                return false;
            if (!string.IsNullOrEmpty(player.HeldTrapFamily))
                return false;
            player.HeldTrapFamily = family;
            EmitPossession(player, position);
            return true;
        }

        public bool Dispose(HotelPlayer player, int id)
        {
            if (!CanControl(player))
                return false;
            if (id == -1)
            {
                if (player.ControlledCube >= 0 || string.IsNullOrEmpty(player.HeldTrapFamily))
                    return false;
                player.HeldTrapFamily = "";
                return true;
            }

            var trap = Trap(id);
            if (player.ControlledCube != id || !trap || trap.CaptureState().OwnerId != player.NetId)
                return false;
            EmitPossession(player, trap.Position);
            trap.StopInput();
            trap.gameObject.SetActive(false);
            Destroy(trap.gameObject);
            _Objects.Remove(id);
            _State.Cubes.RemoveAll(p => p.Id == id);
            player.ControlledCube = -1;
            player.HeldTrapFamily = "";
            Changed(player);
            return true;
        }

        public void Possess(HotelPlayer player, int id, Vector3 ghostPosition)
        {
            if (!CanControl(player))
                return;
            if (id == -1)
            {
                var previous = Trap(player.ControlledCube);
                if (previous)
                {
                    previous.StopInput();
                    var motion = Flight(player);
                    motion.Position = previous.Position;
                    motion.Axis = Vector2.zero;
                }

                player.ControlledCube = -1;
                return;
            }

            var trap = Trap(id);
            if (!trap || trap.CaptureState().OwnerId != player.NetId || !string.IsNullOrEmpty(player.HeldTrapFamily) || !Finite(ghostPosition.x) || !Finite(ghostPosition.y) || !Finite(ghostPosition.z) || (!InteractionPosition(player, ghostPosition, out var verified) || Vector3.Distance(verified, trap.Position) > 2))
                return;
            if (player.ControlledCube == id)
                return;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var other = HotelPlayer.ActivePlayers[i];
                if (other != player && other.ControlledCube == id)
                    return;
            }

            if (Trap(player.ControlledCube))
                Trap(player.ControlledCube).StopInput();
            player.ControlledCube = id;
            trap.StopInput();
            EmitPossession(player, trap.Position);
        }

        void EmitPossession(HotelPlayer player, Vector3 position)
        {
            player.PossessionEffectPosition = position;
            player.PossessionEffectVersion++;
        }

        public void AcceptTrapInput(HotelPlayer player, int id, float axis) => AcceptTrapAction(player, id, new TrapInput(TrapInputKind.Move, axis));
        public bool AcceptTrapAction(HotelPlayer player, int id, TrapInput command)
        {
            var trap = Trap(id);
            if (!CanControl(player) || player.ControlledCube != id || !trap || trap.CaptureState().OwnerId != player.NetId || !trap.AcceptInput(command, Time.unscaledTime))
                return false;
            int index = _State.Cubes.FindIndex(p => p.Id == id);
            _State.Cubes[index] = trap.CaptureState();
            _Dirty = true;
            Publish(player);
            return true;
        }

        public bool TryPlace(HotelPlayer player, Vector3 position, float rotation) => Finite(rotation) && TryPlace(player, position, DefaultFamily);
        public bool TryPlace(HotelPlayer player, Vector3 position, string family)
        {
            if (!CanControl(player) || Time.unscaledTime < _NextPlacement || _State.Cubes.Count >= 64 || player.ControlledCube >= 0 || player.HeldTrapFamily != family || !PlacementPoint(position, family, out var grounded) || Mathf.Abs(position.y - grounded.y) > .01f || Mathf.Abs(position.z) > .01f)
                return false;
            var prefab = Definition(family);
            var record = new GhostCubePlacement
            {
                Id = _NextId++,
                OwnerId = player.NetId,
                Position = grounded + prefab.ArmedOffset,
                Origin = grounded,
                FamilyTag = family
            };
            _State.Cubes.Add(record);
            var trap = Instantiate(prefab, _TrapContainer ? _TrapContainer : transform, false);
            trap.name = prefab.DisplayName + " " + record.Id;
            trap.gameObject.SetActive(true);
            trap.Initialize(this, record, record.Id);
            _Objects.Add(record.Id, trap);
            player.HeldTrapFamily = "";
            _NextPlacement = Time.unscaledTime + .2f;
            Changed(player);
            return true;
        }

        internal void SuspendGhost(HotelPlayer player)
        {
            if (!CanControl(player)) return;
            if (Trap(player.ControlledCube)) Trap(player.ControlledCube).StopInput();
            player.ControlledCube = -1;
            if (_Flights.TryGetValue(player, out var motion)) motion.Axis = Vector2.zero;
        }
        internal void RestoreFlight(HotelPlayer player, Vector3 position)
        {
            if (!player || player.Networked && !player.IsServer) return;
            _Flights[player] = new FlightMotion { Position = position, Axis = Vector2.zero, LastInput = Time.unscaledTime };
            player.GhostFlightPosition = position;
            player.GhostFlightReady = true;
        }
        internal void TransferGhost(HotelPlayer previous, HotelPlayer next, Vector3 flightPosition)
        {
            previous.ControlledCube = next.ControlledCube = -1;
            previous.HeldTrapFamily = next.HeldTrapFamily = "";
            foreach (var trap in _Objects.Values)
            {
                trap.StopInput();
                var state = trap.CaptureState();
                if (state.OwnerId != previous.NetId) continue;
                state.OwnerId = next.NetId;
                trap.ApplyState(state);
                int index = _State.Cubes.FindIndex(item => item.Id == state.Id);
                _State.Cubes[index] = state;
            }
            _Flights.Remove(previous);
            _Flights[next] = new FlightMotion { Position = flightPosition, Axis = Vector2.zero, LastInput = Time.unscaledTime };
            previous.GhostFlightReady = false;
            next.GhostFlightPosition = flightPosition;
            next.GhostFlightReady = true;
            Changed(next);
        }
        void Changed(HotelPlayer player)
        {
            _Dirty = true;
            _NextPublish = 0;
            Publish(player);
        }

        void Publish(HotelPlayer player)
        {
            if (!_Dirty || Time.unscaledTime < _NextPublish)
                return;
            _Dirty = false;
            _State.Revision = ++_Revision;
            _NextPublish = Time.unscaledTime + .05f;
            var json = Snapshot;
            player.PlacedObjectsJson = json;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var other = HotelPlayer.ActivePlayers[i];
                if (other != player && CanControl(other))
                    other.PlacedObjectsJson = json;
            }

            _Applied = json;
        }

        void Update()
        {
            var players = HotelPlayer.ActivePlayers;
            HotelPlayer authority = null;
            for (int i = 0; i < players.Count; i++)
                if (CanControl(players[i]))
                {
                    authority = players[i];
                    break;
                }

            if (authority)
            {
                for (int i = 0; i < players.Count; i++)
                    if (CanControl(players[i]))
                        SimulateFlight(players[i]);
                foreach (var item in _Objects)
                {
                    var trap = item.Value;
                    var before = trap.Position;
                    if (trap.Simulate(Time.deltaTime, Time.unscaledTime))
                    {
                        int index = _State.Cubes.FindIndex(p => p.Id == item.Key);
                        _State.Cubes[index] = trap.CaptureState();
                        _Dirty = true;
                    }
                    if (GameSceneController.Current && GameSceneController.Current.Haunting)
                        GameSceneController.Current.Haunting.ApplyTrapSpook(trap, before, Time.deltaTime);
                }

                Publish(authority);
            }
            else
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.ControlsReady && player.ControlMode == HotelControlMode.Ghost && player.InventoryScope == _State.RoomScope && player.RoundStateKey == _State.RoundKey && !string.IsNullOrEmpty(player.PlacedObjectsJson) && _Applied != player.PlacedObjectsJson)
                        if (ApplySnapshot(player.PlacedObjectsJson))
                            _Applied = player.PlacedObjectsJson;
                }
        }

        public bool ApplySnapshot(string json)
        {
            GhostCubeSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<GhostCubeSnapshot>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (snapshot == null || snapshot.Cubes == null || snapshot.Cubes.Count > 64 || snapshot.RoomScope != _State.RoomScope || snapshot.RoundKey != _State.RoundKey || snapshot.RoundVersion != _State.RoundVersion || snapshot.Revision < _State.Revision || string.IsNullOrEmpty(_State.RoundKey))
                return false;
            var ids = new HashSet<int>();
            foreach (var record in snapshot.Cubes)
                if (record.Id < 0 || !ids.Add(record.Id) || !Definition(record.FamilyTag) || !Finite(record.Position.x) || !Finite(record.Position.y) || !Finite(record.Position.z) || !Finite(record.Origin.x) || !Finite(record.Origin.y) || !Finite(record.Origin.z) || !Finite(record.Progress) || !Finite(record.Rotation) || record.Progress < 0 || record.Progress > 1 || record.Phase < 0 || record.Phase > 2 || (_Objects.TryGetValue(record.Id, out var existing) && existing.FamilyTag != record.FamilyTag))
                    return false;
            foreach (var id in _Objects.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                _Objects[id].gameObject.SetActive(false);
                Destroy(_Objects[id].gameObject);
                _Objects.Remove(id);
            }

            foreach (var record in snapshot.Cubes)
            {
                var prefab = Definition(record.FamilyTag);
                if (_Objects.TryGetValue(record.Id, out var trap))
                {
                    if (trap.FamilyTag != record.FamilyTag)
                        return false;
                    trap.ApplyState(record);
                }
                else
                {
                    trap = Instantiate(prefab, _TrapContainer ? _TrapContainer : transform, false);
                    trap.name = prefab.DisplayName + " " + record.Id;
                    trap.gameObject.SetActive(true);
                    trap.Initialize(this, record, record.Id);
                    _Objects.Add(record.Id, trap);
                }
            }

            _State = snapshot;
            _Revision = Math.Max(_Revision, snapshot.Revision);
            _NextId = Math.Max(_NextId, snapshot.Cubes.Count == 0 ? 0 : snapshot.Cubes.Max(p => p.Id) + 1);
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
