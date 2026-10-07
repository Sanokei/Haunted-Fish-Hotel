using UnityEngine;
using UnityEngine.Events;

namespace HauntedFish.Multiplayer
{
    public sealed class GhostTrap : MonoBehaviour, IGhostTrap
    {
        [SerializeField]
        string _FamilyTag = "cart", _DisplayName = "Shopping cart";
        [SerializeField]
        TrapInputMode _InputMode;
        [SerializeField]
        TrapBehaviour _Behaviour;
        [SerializeField]
        Sprite _Icon;
        [SerializeField]
        BoxCollider _Collider;
        [SerializeField]
        GhostTrapAreaPresentation _Presentation;
        [SerializeField]
        Vector3 _ArmedOffset;
        [SerializeField]
        Vector2 _BackgroundSize = new Vector2(5, 1.5f), _BackgroundOffset = new Vector2(0, .25f);
        [SerializeField]
        Vector2 _ObjectSize = new Vector2(1.25f, 1);
        [SerializeField, Min(.01f)]
        float _MoveSpeed = 5, _TravelHalfWidth = 2, _ActivationSeconds = .35f;
        [SerializeField, Min(.01f)]
        float _FallDistance = 3.4f, _FallSeconds = .7f, _ResetDelay = 2;
        [SerializeField]
        bool _AutoReset;
        [Tooltip("Invoked on the authority only. Assign established gameplay effects here.")]
        [SerializeField]
        UnityEvent _Activated = new UnityEvent(), _Landed = new UnityEvent();
        GhostCubePlacement _State;
        float _Axis, _LastInput, _Elapsed;
        RaycastHit[] _Hits = new RaycastHit[8];
        Collider[] _Overlaps = new Collider[8];
        RaycastHit[] _PushHits = new RaycastHit[8];
        readonly System.Collections.Generic.List<HotelPlayer> _PushPlayers = new System.Collections.Generic.List<HotelPlayer>(4);
        const float PushSeparation = .12f;
        const float ContactMargin = .01f;
        public GhostPlacementWorld World { get; private set; }
        public int Index { get; private set; }
        public string FamilyTag => _FamilyTag;
        public string DisplayName => _DisplayName;
        public TrapInputMode InputMode => _InputMode;
        public Sprite Icon => _Icon;
        public Vector3 ArmedOffset => _ArmedOffset;
        public Vector3 Position => transform.position;
        public Vector2 BackgroundSize => _BackgroundSize;
        public Vector2 BackgroundOffset => _BackgroundOffset;
        public Vector2 ObjectSize => _ObjectSize;
        public Vector3 BodyHalfSize => _Collider ? Vector3.Scale(_Collider.size, transform.lossyScale) * .5f : Vector3.one * .5f;
        public Vector3 HalfExtents => BodyHalfSize * .96f;
        public TrapPhase Phase => (TrapPhase)_State.Phase;
        public Vector2 Limits => new Vector2(Mathf.Max(-15 + BodyHalfSize.x, _State.Origin.x - _TravelHalfWidth), Mathf.Min(15 - BodyHalfSize.x, _State.Origin.x + _TravelHalfWidth));
        public string ControlHint => _InputMode == TrapInputMode.Movement ? "A/D / stick: move" : _InputMode == TrapInputMode.MouseClick ? "Mouse click: activate - R: reset" : "Space / A: activate - R: reset";

        public void Initialize(GhostPlacementWorld world, GhostCubePlacement state, int index)
        {
            World = world;
            Index = index;
            _Axis = 0;
            _Elapsed = 0;
            ApplyState(state);
            if (_Presentation)
                _Presentation.ConfigureTrap(this);
        }

        public GhostCubePlacement CaptureState() => _State;
        public void ApplyState(GhostCubePlacement state)
        {
            _State = state;
            transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0, state.Rotation, 0));
        }

        public void StopInput()
        {
            _Axis = 0;
        }

        public bool AcceptInput(TrapInput input, float now)
        {
            if (input.Kind == TrapInputKind.Reset)
            {
                if (!ResetPointClear())
                    return false;
                _Axis = 0;
                _Elapsed = 0;
                _State.Phase = (int)TrapPhase.Armed;
                _State.Progress = 0;
                _State.Position = _State.Origin + _ArmedOffset;
                ApplyState(_State);
                Physics.SyncTransforms();
                return true;
            }

            if (_InputMode == TrapInputMode.Movement)
            {
                if (input.Kind != TrapInputKind.Move || !Finite(input.Axis))
                    return false;
                _Axis = Mathf.Clamp(input.Axis, -1, 1);
                _LastInput = now;
                return true;
            }

            var expected = _InputMode == TrapInputMode.MouseClick ? TrapInputKind.Click : TrapInputKind.Activate;
            if (input.Kind != expected || Phase != TrapPhase.Armed)
                return false;
            if (expected == TrapInputKind.Click && (!Finite(input.Point.x) || !Finite(input.Point.y) || !Finite(input.Point.z) || !_Collider || !_Collider.bounds.Contains(input.Point)))
                return false;
            _State.Phase = (int)TrapPhase.Active;
            _State.Progress = 0;
            _Elapsed = 0;
            _State.Activations++;
            _Activated.Invoke();
            return true;
        }

        public bool Simulate(float seconds, float now)
        {
            if (seconds <= 0)
                return false;
            if (_Behaviour == TrapBehaviour.Slide)
            {
                if (now - _LastInput > .3f)
                    _Axis = 0;
                var limits = Limits;
                float next = Mathf.Clamp(Position.x + _Axis * _MoveSpeed * seconds, limits.x, limits.y);
                return MovePhysical(Vector3.right * (next - Position.x), true);
            }

            if (Phase == TrapPhase.Active)
            {
                _Elapsed += seconds;
                float duration = _Behaviour == TrapBehaviour.Fall ? _FallSeconds : _ActivationSeconds;
                _State.Progress = Mathf.Clamp01(_Elapsed / Mathf.Max(.01f, duration));
                if (_Behaviour == TrapBehaviour.Fall)
                {
                    var target = _State.Origin + _ArmedOffset + Vector3.down * (_FallDistance * _State.Progress * _State.Progress);
                    bool blocked = !MovePhysical(target - Position, false) && (target - Position).sqrMagnitude > .00001f;
                    if (_State.Progress >= 1 || blocked)
                    {
                        _State.Phase = (int)TrapPhase.Latched;
                        _Elapsed = 0;
                        _Landed.Invoke();
                    }
                }
                else if (_State.Progress >= 1)
                {
                    _State.Phase = (int)TrapPhase.Armed;
                    _State.Progress = 0;
                }

                return true;
            }

            if (Phase == TrapPhase.Latched && _AutoReset)
            {
                _Elapsed += seconds;
                if (_Elapsed >= _ResetDelay)
                    return AcceptInput(new TrapInput(TrapInputKind.Reset), now);
            }

            return false;
        }

        bool ResetPointClear()
        {
            Physics.SyncTransforms();
            int count;
            var target = _State.Origin + _ArmedOffset;
            var rotation = Matrix4x4.Rotate(transform.rotation);
            var half = HalfExtents;
            var extent = new Vector3(Mathf.Abs(rotation.m00)*half.x+Mathf.Abs(rotation.m01)*half.y+Mathf.Abs(rotation.m02)*half.z,
                Mathf.Abs(rotation.m10)*half.x+Mathf.Abs(rotation.m11)*half.y+Mathf.Abs(rotation.m12)*half.z,
                Mathf.Abs(rotation.m20)*half.x+Mathf.Abs(rotation.m21)*half.y+Mathf.Abs(rotation.m22)*half.z);
            if (GhostPlacementWorld.FishOccupies(target,extent)) return false;
            while ((count = Physics.OverlapBoxNonAlloc(target, HalfExtents, _Overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore)) == _Overlaps.Length)
                System.Array.Resize(ref _Overlaps, _Overlaps.Length * 2);
            for (int i = 0; i < count; i++)
                if (!_Overlaps[i].transform.IsChildOf(transform))
                    return false;
            return true;
        }

        bool MovePhysical(Vector3 delta, bool pushFish)
        {
            if (delta.sqrMagnitude < .00000001f)
                return false;
            float distance = delta.magnitude;
            var direction = delta / distance;
            // Preserve the full leading face along motion. Shrinking every axis
            // allowed the visual/collision body to cross a neighboring cart.
            var sweepHalf = HalfExtents;
            if (Mathf.Abs(direction.x) > .5f) sweepHalf.x = BodyHalfSize.x;
            if (Mathf.Abs(direction.y) > .5f) sweepHalf.y = BodyHalfSize.y;
            if (Mathf.Abs(direction.z) > .5f) sweepHalf.z = BodyHalfSize.z;
            int hitCount;
            while ((hitCount = Physics.BoxCastNonAlloc(Position, sweepHalf, direction, _Hits, transform.rotation, distance, ~0, QueryTriggerInteraction.Ignore)) == _Hits.Length)
                System.Array.Resize(ref _Hits, _Hits.Length * 2);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _Hits[i];
                if (hit.collider.transform.IsChildOf(transform))
                    continue;
                var fish = hit.collider.GetComponentInParent<HotelPlayer>();
                if (pushFish && fish && fish.ControlsReady && fish.ControlMode == HotelControlMode.Fish && fish.Movement && fish.Movement.SimulationReady)
                    continue;
                distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - ContactMargin));
            }

            if (distance <= ContactMargin)
                return false;
            if (pushFish)
                distance = PushFish(direction, distance);
            if (distance <= ContactMargin)
                return false;
            _State.Position += direction * distance;
            transform.position = _State.Position;
            Physics.SyncTransforms();
            return true;
        }

        float PushFish(Vector3 direction, float distance)
        {
            _PushPlayers.Clear();
            float sign = Mathf.Sign(direction.x);
            var cartBounds = _Collider ? _Collider.bounds : new Bounds(Position, HalfExtents * 2);
            float back = sign > 0 ? cartBounds.min.x : -cartBounds.max.x;
            float front = (sign > 0 ? cartBounds.max.x : -cartBounds.min.x) + distance + PushSeparation;
            var players = HotelPlayer.ActivePlayers;
            // Include swept contacts and contiguous fish, not only bodies already
            // inside the destination box. Four lobby slots keep this set small.
            bool added;
            do
            {
                added = false;
                for (int i = 0; i < players.Count; i++)
                {
                    var fish = players[i];
                    if (!fish.ControlsReady || fish.ControlMode != HotelControlMode.Fish || !fish.Movement.SimulationReady || _PushPlayers.Contains(fish))
                        continue;
                    var bounds = fish.BodyController.bounds;
                    if (bounds.max.y < cartBounds.min.y || bounds.min.y > cartBounds.max.y || bounds.max.z < cartBounds.min.z || bounds.min.z > cartBounds.max.z)
                        continue;
                    float near = sign > 0 ? bounds.min.x : -bounds.max.x;
                    float far = sign > 0 ? bounds.max.x : -bounds.min.x;
                    if (near > front || far < back)
                        continue;
                    _PushPlayers.Add(fish);
                    front = Mathf.Max(front, far + distance + PushSeparation);
                    added = true;
                }
            }
            while (added);
            for (int i = 0; i < _PushPlayers.Count; i++)
            {
                var fish = _PushPlayers[i];
                float allowed = FishPushDistance(fish, direction, distance + PushSeparation);
                distance = Mathf.Min(distance, Mathf.Max(0, allowed - PushSeparation));
            }

            if (distance <= ContactMargin)
                return 0;
            // Move leading bodies first, so their controllers don't pin the fish
            // behind them. A small insertion sort needs no per-move comparator.
            for (int i = 1; i < _PushPlayers.Count; i++)
            {
                var fish = _PushPlayers[i];
                int j = i - 1;
                while (j >= 0 && _PushPlayers[j].transform.position.x * sign < fish.transform.position.x * sign)
                {
                    _PushPlayers[j + 1] = _PushPlayers[j];
                    j--;
                }

                _PushPlayers[j + 1] = fish;
            }

            for (int i = 0; i < _PushPlayers.Count; i++)
            {
                var fish = _PushPlayers[i];
                if (!fish.Movement.TryMove(direction * (distance + PushSeparation), out var applied))
                    return 0;
                distance = Mathf.Min(distance, Mathf.Max(0, Vector3.Dot(applied, direction) - PushSeparation));
            }

            return distance;
        }

        float FishPushDistance(HotelPlayer fish, Vector3 direction, float requested)
        {
            var controller = fish.BodyController;
            var scale = controller.transform.lossyScale;
            var center = controller.transform.TransformPoint(controller.center);
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = controller.height * Mathf.Abs(scale.y);
            var offset = controller.transform.up * Mathf.Max(0, height * .5f - radius);
            // Inset for the CharacterController skin, including floor contact.
            float queryRadius = Mathf.Max(.001f, radius - controller.skinWidth - ContactMargin);
            int count;
            while ((count = Physics.CapsuleCastNonAlloc(center - offset, center + offset, queryRadius, direction, _PushHits, requested, ~0, QueryTriggerInteraction.Ignore)) == _PushHits.Length)
                System.Array.Resize(ref _PushHits, _PushHits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var collider = _PushHits[i].collider;
                if (collider.transform.IsChildOf(transform) || collider.transform.IsChildOf(fish.transform))
                    continue;
                var other = collider.GetComponentInParent<HotelPlayer>();
                if (other && _PushPlayers.Contains(other))
                    continue;
                requested = Mathf.Min(requested, Mathf.Max(0, _PushHits[i].distance - ContactMargin));
            }

            return requested;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
