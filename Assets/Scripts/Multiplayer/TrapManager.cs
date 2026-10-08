using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HauntedFish.Multiplayer
{
    [Serializable]
    public struct ConveyorPackageState
    {
        public int Id;
        public string Family;
        public float Distance;
    }

    [Serializable]
    public sealed class ConveyorSnapshot
    {
        public string RoomScope, RoundKey;
        public int RoundVersion, Revision;
        public bool Running, Paused;
        public float Speed;
        public Vector3 PathStart, PathEnd;
        public List<ConveyorPackageState> Packages = new List<ConveyorPackageState>();
    }

    // Catalog injection, weighted supply and round-scoped replication belong to the scene authority.
    [DefaultExecutionOrder(-800)]
    public sealed class TrapManager : MonoBehaviour
    {
        [SerializeField]
        GhostPlacementWorld _World;
        [SerializeField]
        GhostPlacementController _GhostControls;
        [SerializeField]
        GhostTrapSupply _PackagePrefab;
        [SerializeField]
        TrapDefinition[] _Traps = Array.Empty<TrapDefinition>();
        [SerializeField]
        ConveyorRunPolicy _RunPolicy = ConveyorRunPolicy.AfterRoundRelease;
        [SerializeField, Min(0)]
        float _StartDelay;
        [SerializeField, Min(.05f)]
        float _SpawnInterval = 4;
        [SerializeField, Min(0)]
        float _Speed = 2;
        [SerializeField, Min(.05f)]
        float _RunDuration = 12;
        [SerializeField, Min(0)]
        float _PauseBetweenRuns = 8;
        [SerializeField, Range(0, 64)]
        int _InitialPackages = 3;
        [SerializeField, Range(1, 64)]
        int _MaxPackages = 8;
        [SerializeField]
        BoxCollider _HallwayFloor;
        [SerializeField]
        Vector3 _PathStart = new Vector3(-15, 6.5f, 0), _PathEnd = new Vector3(15, 6.5f, 0);
        [SerializeField, Min(.1f)]
        float _PickupRadius = 2;
        readonly Dictionary<string, TrapDefinition> _Catalog = new Dictionary<string, TrapDefinition>();
        readonly Dictionary<int, GhostTrapSupply> _Packages = new Dictionary<int, GhostTrapSupply>();
        ConveyorSnapshot _State = new ConveyorSnapshot();
        HotelPlayer _Authority;
        int _NextId, _Revision;
        string _CounterKey, _CounterScope;
        readonly ConveyorSchedule _Schedule = new ConveyorSchedule();
        bool _Dirty;
        float _PublishDue, _ReceivedAt;
        string _Applied;
        GhostTrapSupply _Highlighted;
        bool _RefreshCatalog;
        void OnValidate() => _RefreshCatalog = true;
        Vector3 EffectiveStart => _HallwayFloor ? new Vector3(_HallwayFloor.bounds.min.x, _PathStart.y, 0) : _PathStart;
        Vector3 EffectiveEnd => _HallwayFloor ? new Vector3(_HallwayFloor.bounds.max.x, _PathEnd.y, 0) : _PathEnd;
        public bool Running => _State.Running;
        public bool Paused => _State.Paused;
        public int PackageCount => _Packages.Count;
        public string Snapshot => JsonUtility.ToJson(_State);
        public float Speed { get => _Speed; set => _Speed = SafeNonnegative(value); }

        public float SpawnInterval
        {
            get => _SpawnInterval;
            set
            {
                _SpawnInterval = SafeInterval(value);
                _Schedule.ClampSpawnDue(_SpawnInterval);
            }
        }

        public ConveyorRunPolicy RunPolicy { get => _RunPolicy; set => _RunPolicy = value; }

        public GhostTrapSupply Package(int id) => _Packages.TryGetValue(id, out var item) ? item : null;
        public TrapDefinition Definition(string family) => family != null && _Catalog.TryGetValue(family, out var item) ? item : null;
        void Awake() => InjectCatalog();
        public void Inject(GhostPlacementWorld world, GhostPlacementController controls, GhostTrapSupply packagePrefab, IEnumerable<TrapDefinition> traps)
        {
            _World = world;
            _GhostControls = controls;
            _PackagePrefab = packagePrefab;
            ConfigureDefinitions(traps);
        }

        public void ConfigureDefinitions(IEnumerable<TrapDefinition> traps)
        {
            _Traps = (traps ?? Array.Empty<TrapDefinition>()).Where(t => t != null).Select(t => new TrapDefinition { Prefab = t.Prefab, DisplayArt = t.DisplayArt, Weight = t.Weight }).ToArray();
            InjectCatalog();
        }

        void InjectCatalog()
        {
            var previous = new Dictionary<string, TrapDefinition>(_Catalog);
            if (_World)
                _World.InjectDefinitions((_Traps ?? Array.Empty<TrapDefinition>()).Where(t => t != null && t.Valid).Select(t => t.Prefab));
            _Catalog.Clear();
            foreach (var item in _Traps ?? Array.Empty<TrapDefinition>())
            {
                if (item == null || !item.Valid || _Catalog.ContainsKey(item.Family))
                    continue;
                var effective = _World ? _World.Definition(item.Family) : item.Prefab;
                if (effective && effective != item.Prefab)
                {
                    var artwork = previous.TryGetValue(item.Family, out var old) && old.Prefab == effective ? old.DisplayArt : effective.Icon;
                    _Catalog.Add(item.Family, new TrapDefinition { Prefab = effective, DisplayArt = artwork, Weight = item.Weight });
                }
                else
                    _Catalog.Add(item.Family, item);
            }

            if (_GhostControls)
                _GhostControls.InjectSupplies(this);
            for (int i = _State.Packages.Count - 1; i >= 0; i--)
                if (Definition(_State.Packages[i].Family) == null)
                {
                    RemovePackage(_State.Packages[i].Id);
                    _State.Packages.RemoveAt(i);
                }

            foreach (var item in _Packages.Values)
                if (item)
                    item.Configure(item.PackageId, Definition(item.FamilyTag));
            if (_Highlighted)
                _Highlighted.Highlight(true);
            Publish(true);
        }

        // Unity supplies randomness only on the authority; selection is independent of scene objects.
        public TrapDefinition SelectWeighted(double sample) => WeightedSelection.Select(_Catalog.Values, entry => entry.Weight, sample);

        bool ScopedAuthority(HotelPlayer player) => _World && player &&
            (!player.Networked || player.IsServer) && player.InventoryScope == _World.RoomScope &&
            player.RoundStateKey == _World.RoundKey && player.RoundVersion == _World.RoundVersion &&
            !string.IsNullOrEmpty(_World.RoundKey);
        bool CanAuthor(HotelPlayer player) => isActiveAndEnabled && _World && _World.isActiveAndEnabled && player && player.ControlsReady && ScopedAuthority(player);
        public void BeginRound(string scope, string key, int version)
        {
            if (_State.RoomScope == scope && _State.RoundKey == key && _State.RoundVersion == version)
                return;
            ResetRound();
            if (_CounterKey != key || _CounterScope != scope)
            {
                _NextId = 0;
                _Revision = 0;
                _CounterKey = key;
                _CounterScope = scope;
            }

            _State.RoomScope = scope;
            _State.RoundKey = key;
            _State.RoundVersion = version;
            _State.Revision = _Revision;
            _State.PathStart = Finite(EffectiveStart) ? EffectiveStart : new Vector3(-15, 6.5f, 0);
            _State.PathEnd = Finite(EffectiveEnd) ? EffectiveEnd : new Vector3(15, 6.5f, 0);
            _State.Speed = SafeNonnegative(_Speed);
            InjectCatalog();
        }

        public void ResetRound()
        {
            foreach (var package in _Packages.Values)
                if (package)
                {
                    package.gameObject.SetActive(false);
                    Destroy(package.gameObject);
                }

            _Highlighted = null;
            _Packages.Clear();
            _State = new ConveyorSnapshot();
            _Authority = null;
            _Schedule.Reset();
            _Dirty = false;
            _PublishDue = 0;
            _Applied = null;
        }

        public bool StartConveyor()
        {
            if (!CanAuthor(_Authority) || !_Authority.GhostSetupReady)
                return false;
            if (_State.Running && !_State.Paused)
                return true;
            _Schedule.AllowAutomaticStarts();
            StartRun();
            Publish(true);
            return true;
        }

        public bool StopConveyor()
        {
            if (!CanAuthor(_Authority))
                return false;
            _Schedule.SuppressAutomaticStarts();
            _State.Running = false;
            _State.Paused = false;
            _Dirty = true;
            Publish(true);
            return true;
        }

        public bool PauseConveyor()
        {
            if (!CanAuthor(_Authority))
                return false;
            _State.Paused = true;
            _Dirty = true;
            Publish(true);
            return true;
        }

        public bool ResumeConveyor()
        {
            if (!CanAuthor(_Authority))
                return false;
            _State.Paused = false;
            _Dirty = true;
            Publish(true);
            return true;
        }

        [ContextMenu("Conveyor/Start")]
        void StartFromInspector() => StartConveyor();
        [ContextMenu("Conveyor/Stop")]
        void StopFromInspector() => StopConveyor();
        [ContextMenu("Conveyor/Pause")]
        void PauseFromInspector() => PauseConveyor();
        [ContextMenu("Conveyor/Resume")]
        void ResumeFromInspector() => ResumeConveyor();
        void StartRun() => ApplyRunStart(_Schedule.Start(_State.Running, _State.Paused, _SpawnInterval));

        void ApplyRunStart(ConveyorRunStart start)
        {
            if (!start.Started)
                return;
            _State.Running = true;
            _State.Paused = false;
            _Dirty = true;
            if (start.SeedInitialPackages)
            {
                int count = Mathf.Clamp(_InitialPackages, 0, Mathf.Clamp(_MaxPackages, 1, 64));
                for (int i = 0; i < count; i++)
                    Spawn(i * PathLength / count);
            }
        }

        float PathLength => Vector3.Distance(_State.PathStart, _State.PathEnd);

        bool Spawn(float distance)
        {
            if (!_PackagePrefab || _State.Packages.Count >= Mathf.Clamp(_MaxPackages, 1, 64) || !Finite(PathLength) || PathLength < .001f)
                return false;
            var definition = SelectWeighted(UnityEngine.Random.value);
            if (definition == null)
                return false;
            var record = new ConveyorPackageState
            {
                Id = _NextId++,
                Family = definition.Family,
                Distance = distance
            };
            _State.Packages.Add(record);
            CreatePackage(record);
            _Dirty = true;
            return true;
        }

        void CreatePackage(ConveyorPackageState record)
        {
            var package = Instantiate(_PackagePrefab, transform, false);
            package.name = "Package " + record.Id + " - " + record.Family;
            package.Configure(record.Id, Definition(record.Family));
            package.gameObject.SetActive(true);
            _Packages.Add(record.Id, package);
            package.transform.position = Position(record.Distance);
        }

        Vector3 Position(float distance) => Vector3.Lerp(_State.PathStart, _State.PathEnd, PathLength > .001f ? distance / PathLength : 0);
        public void Simulate(HotelPlayer authority, float seconds)
        {
            if (!CanAuthor(authority) || !Finite(seconds) || seconds < 0)
                return;
            BeginRound(_World.RoomScope, _World.RoundKey, _World.RoundVersion);
            _Authority = authority;
            if (!authority.GhostSetupReady)
            {
                Publish();
                return;
            }

            if (!Finite(EffectiveStart) || !Finite(EffectiveEnd))
                return;
            _Dirty |= _State.Speed != SafeNonnegative(_Speed) || _State.PathStart != EffectiveStart || _State.PathEnd != EffectiveEnd;
            _State.Speed = SafeNonnegative(_Speed);
            _State.PathStart = EffectiveStart;
            _State.PathEnd = EffectiveEnd;
            // Path edits apply while paused/stopped too; publish only distances
            // inside the edited path, so observers can accept the next snapshot.
            for (int i = _State.Packages.Count - 1; i >= 0; i--)
                if (_State.Packages[i].Distance >= PathLength)
                {
                    RemovePackage(_State.Packages[i].Id);
                    _State.Packages.RemoveAt(i);
                }

            var step = _Schedule.Advance(_State.Running, _State.Paused, seconds,
                new ConveyorTiming(_RunPolicy, _StartDelay, _SpawnInterval, _RunDuration, _PauseBetweenRuns));
            ApplyRunStart(step.Start);
            if (step.AdvancePackages)
            {
                for (int i = _State.Packages.Count - 1; i >= 0; i--)
                {
                    var record = _State.Packages[i];
                    record.Distance += _State.Speed * step.ActiveSeconds;
                    if (record.Distance >= PathLength)
                    {
                        RemovePackage(record.Id);
                        _State.Packages.RemoveAt(i);
                    }
                    else
                        _State.Packages[i] = record;
                }

                foreach (float elapsed in step.Spawns)
                {
                    float distance = elapsed * _State.Speed;
                    if (distance < PathLength)
                        Spawn(distance);
                }

                _Dirty = true;
            }

            _State.Running = step.Running;

            RenderPackages(false);
            Publish();
        }

        void RemovePackage(int id)
        {
            if (_Highlighted && _Highlighted.PackageId == id)
                _Highlighted = null;
            if (_Packages.TryGetValue(id, out var package))
            {
                package.gameObject.SetActive(false);
                Destroy(package.gameObject);
                _Packages.Remove(id);
            }

            _Dirty = true;
        }

        public bool TryTake(HotelPlayer player, int packageId, Vector3 position, string key, int version)
        {
            if (!CanAuthor(player) || !_World.RequestMatches(player, key, version) || key != _State.RoundKey || version != _State.RoundVersion || !Finite(position) || !_World.InteractionPosition(player, position, out var verified))
                return false;
            int index = _State.Packages.FindIndex(p => p.Id == packageId);
            if (index < 0)
                return false;
            var record = _State.Packages[index];
            if (Vector3.Distance(verified, Position(record.Distance)) > Mathf.Max(.1f, SafeNonnegative(_PickupRadius)) || !_World.Hold(player, record.Family, Position(record.Distance)))
                return false;
            RemovePackage(packageId);
            _State.Packages.RemoveAt(index);
            _Authority = player;
            Publish(true);
            return true;
        }

        public GhostTrapSupply ClosestPackage(Vector3 point)
        {
            GhostTrapSupply closest = null;
            float distance = float.MaxValue;
            foreach (var item in _Packages.Values)
                if (item)
                {
                    float d = (item.transform.position - point).sqrMagnitude;
                    if (d < distance)
                    {
                        distance = d;
                        closest = item;
                    }
                }

            return closest;
        }

        public void HighlightPackage(GhostTrapSupply package)
        {
            if (_Highlighted == package)
                return;
            if (_Highlighted)
                _Highlighted.Highlight(false);
            _Highlighted = package;
            if (_Highlighted)
                _Highlighted.Highlight(true);
        }

        public bool InPickupRange(Vector3 point, GhostTrapSupply package) => package && Vector3.Distance(point, package.transform.position) <= Mathf.Max(.1f, SafeNonnegative(_PickupRadius));
        void Publish(bool force = false, bool teardown = false)
        {
            if (!(teardown ? ScopedAuthority(_Authority) && _State.RoomScope == _World.RoomScope && _State.RoundKey == _World.RoundKey && _State.RoundVersion == _World.RoundVersion : CanAuthor(_Authority)) || (!_Dirty && !force) || (!force && Time.unscaledTime < _PublishDue))
                return;
            _State.Revision = ++_Revision;
            _Dirty = false;
            _PublishDue = Time.unscaledTime + .1f;
            var json = Snapshot;
            for (int i = 0; i < HotelPlayer.ActivePlayers.Count; i++)
            {
                var member = HotelPlayer.ActivePlayers[i];
                if (teardown ? ScopedAuthority(member) : CanAuthor(member))
                    member.ConveyorJson = json;
            }

            _Applied = json;
        }

        public bool ApplySnapshot(string json)
        {
            ConveyorSnapshot next;
            try
            {
                next = JsonUtility.FromJson<ConveyorSnapshot>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (next == null || next.Packages == null || next.Packages.Count > 64 || string.IsNullOrEmpty(_State.RoundKey) || next.RoomScope != _State.RoomScope || next.RoundKey != _State.RoundKey || next.RoundVersion != _State.RoundVersion || next.Revision < _State.Revision || !Finite(next.Speed) || next.Speed < 0 || !Finite(next.PathStart) || !Finite(next.PathEnd))
                return false;
            float length = Vector3.Distance(next.PathStart, next.PathEnd);
            if (!Finite(length))
                return false;
            var ids = new HashSet<int>();
            foreach (var record in next.Packages)
                if (record.Id < 0 || !ids.Add(record.Id) || Definition(record.Family) == null || !Finite(record.Distance) || record.Distance < 0 || record.Distance > length || (_Packages.TryGetValue(record.Id, out var existing) && existing.FamilyTag != record.Family))
                    return false;
            if (!_PackagePrefab && next.Packages.Count > 0)
                return false;
            foreach (var id in _Packages.Keys.Where(id => !ids.Contains(id)).ToArray())
                RemovePackage(id);
            _State = next;
            _Revision = Math.Max(_Revision, next.Revision);
            _NextId = Math.Max(_NextId, next.Packages.Count == 0 ? 0 : next.Packages.Max(p => p.Id) + 1);
            foreach (var record in next.Packages)
                if (!_Packages.ContainsKey(record.Id))
                    CreatePackage(record);
            _ReceivedAt = Time.unscaledTime;
            RenderPackages(false);
            return true;
        }

        void RenderPackages(bool predict)
        {
            float extra = predict && Running && !Paused ? Mathf.Min(.15f, Time.unscaledTime - _ReceivedAt) * _State.Speed : 0;
            foreach (var record in _State.Packages)
                if (_Packages.TryGetValue(record.Id, out var package))
                    package.transform.position = Position(record.Distance + extra);
        }

        void Update()
        {
            if (_RefreshCatalog)
            {
                _RefreshCatalog = false;
                InjectCatalog();
            }

            if (!_World || string.IsNullOrEmpty(_World.RoundKey))
            {
                if (!string.IsNullOrEmpty(_State.RoundKey))
                    ResetRound();
                return;
            }

            BeginRound(_World.RoomScope, _World.RoundKey, _World.RoundVersion);
            var players = HotelPlayer.ActivePlayers;
            HotelPlayer authority = null;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!CanAuthor(player))
                    continue;
                if (!authority)
                    authority = player;
                if (player.NetId == player.GhostId)
                {
                    authority = player;
                    break;
                }
            }

            if (authority)
                Simulate(authority, Time.unscaledDeltaTime);
            else
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.ControlsReady && player.InventoryScope == _State.RoomScope && player.RoundStateKey == _State.RoundKey && !string.IsNullOrEmpty(player.ConveyorJson) && player.ConveyorJson != _Applied)
                        if (ApplySnapshot(player.ConveyorJson))
                            _Applied = player.ConveyorJson;
                }

                RenderPackages(true);
            }
        }

        void OnDisable()
        {
            _State.Packages.Clear();
            _State.Running = false;
            _State.Paused = false;
            _Dirty = true;
            Publish(true, true);
            ResetRound();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static float SafeNonnegative(float value) => ConveyorSchedule.SafeNonnegative(value);
        static float SafeInterval(float value) => ConveyorSchedule.SafeInterval(value);
    }
}
