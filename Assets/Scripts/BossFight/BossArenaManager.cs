using System;
using UnityEngine;

namespace HauntedFish.BossFight
{
    [Serializable]
    public sealed class BossArenaSnapshot
    {
        public uint Epoch, Revision, FishId, GhostId, ReactionEvent;
        public bool ReactionFishScored, ReactionFinal, Serving;
        public float ReactionRemaining;
        public bool Active, ResultIssued, WinnerFish, Cancelled, ParticipantsReady;
        public int FishScore, GhostScore;
        public Vector3 PuckPosition, PuckVelocity, FishPaddlePosition, GhostPaddlePosition;
    }

    public sealed class BossArenaManager : MonoBehaviour
    {
        [SerializeField]
        Rigidbody _Puck, _FishPaddle, _GhostPaddle;
        [SerializeField] BossCharacterPresentation _Reactions;
        [SerializeField]
        Transform _FishSpawn, _GhostSpawn;
        [SerializeField]
        Camera _Camera;
        [SerializeField]
        TextMesh _Score;
        [SerializeField]
        Vector3 _Centre = new Vector3(1000, 1.6f, 0);
        [SerializeField]
        Vector2 _HalfSize = new Vector2(3, 5);
        [SerializeField, Min(.1f)]
        float _PaddleSpeed = 7, _PuckSpeedLimit = 18, _ServeSpeed = 4;
        [SerializeField, Min(0)]
        float _ServeDelay = 1;
        [SerializeField, Min(.01f)]
        float _SnapshotInterval = .05f, _InputTimeout = .3f;
        [SerializeField, Min(.05f)]
        float _PaddleRadius = .48f;
        readonly BossMatch _Match = new BossMatch();
        Vector2 _FishAxis, _GhostAxis;
        float _FishInputTime, _GhostInputTime, _NextSnapshot, _NextServe;
        bool _Authority, _Serving, _WinnerFish, _Cancelled, _Observed, _Ready;
        uint _Revision, _ObservedEpoch, _ObservedRevision;
        BossArenaSnapshot _View;
        uint _ReactionEvent;
        bool _ReactionFishScored, _ReactionFinal, _CompletionPending;
        float _ReactionUntil;
        BossMatchResult _PendingResult;
        public event Action<BossMatchResult> Completed;
        public event Action<string> SnapshotChanged;
        public Rigidbody Puck => _Puck;
        public Camera Camera => _Camera;
        public Vector3 FishSpawn => _FishSpawn.position;
        public Vector3 GhostSpawn => _GhostSpawn.position;
        public Bounds Bounds => new Bounds(_Centre, new Vector3(_HalfSize.x * 2, 2, _HalfSize.y * 2));
        public bool Active => _Authority ? _Match.Active || _CompletionPending : _View != null && _View.Active;
        public uint FishId => _Authority ? _Match.FishId : _View?.FishId ?? 0;
        public uint GhostId => _Authority ? _Match.GhostId : _View?.GhostId ?? 0;
        public uint MatchEpoch => _Authority ? _Match.Epoch : _View?.Epoch ?? 0;
        public bool ParticipantsReady => _Authority ? _Ready : _View != null && _View.ParticipantsReady;
        public string Snapshot => _View == null ? string.Empty : JsonUtility.ToJson(_View);

        void Awake()
        {
            _Match.Completed += OnCompleted;
            SetAuthority(false);
            SetLocalParticipant(0);
        }

        public void SetAuthority(bool authority)
        {
            _Authority = authority;
            _Puck.isKinematic = !authority || !_Match.Active;
            _FishPaddle.isKinematic = _GhostPaddle.isKinematic = true;
            _Puck.detectCollisions = _FishPaddle.detectCollisions = _GhostPaddle.detectCollisions = authority;
            _FishAxis = _GhostAxis = Vector2.zero;
        }

        public void SetLocalParticipant(uint id)
        {
            SetLocalViewer(id, id == FishId || id == GhostId);
        }

        // Presentation only. Watching never admits an actor or grants paddle authority.
        // The coordinator owns local hallway membership and restores its camera on exit.
        public void SetLocalViewer(uint localId, bool watch)
        {
            if (_Camera)
                _Camera.gameObject.SetActive(localId != 0 && watch);
            if (!watch && _Reactions) _Reactions.ResetPose();
        }

        public bool TryBegin(uint fishId, uint ghostId)
        {
            if (!_Authority || _CompletionPending || !_Match.TryBegin(fishId, ghostId))
                return false;
            _WinnerFish = _Cancelled = _Ready = false;
            _ReactionEvent = 0; _ReactionUntil = 0;
            if (_Reactions) _Reactions.ResetPose();
            _FishAxis = _GhostAxis = Vector2.zero;
            _FishPaddle.position = _Centre + new Vector3(0, .1f, -3.6f);
            _GhostPaddle.position = _Centre + new Vector3(0, .1f, 3.6f);
            ScheduleServe();
            Publish();
            return true;
        }

        public bool ApplyInput(uint id, Vector2 axis)
        {
            if (!_Authority || !_Ready || !_Match.Active || !float.IsFinite(axis.x) || !float.IsFinite(axis.y))
                return false;
            axis = Vector2.ClampMagnitude(axis, 1);
            if (id == _Match.FishId)
            {
                _FishAxis = axis;
                _FishInputTime = Time.unscaledTime;
                return true;
            }

            if (id == _Match.GhostId)
            {
                _GhostAxis = axis;
                _GhostInputTime = Time.unscaledTime;
                return true;
            }

            return false;
        }

        public void SetParticipantsReady(bool ready)
        {
            if (!_Authority || !_Match.Active || _Ready == ready)
                return;
            _Ready = ready;
            _FishAxis = _GhostAxis = Vector2.zero;
            ScheduleServe();
            Publish();
        }

        public bool Cancel(uint id)
        {
            if (!_Authority || id != FishId && id != GhostId) return false;
            if (!_CompletionPending) return _Match.Cancel(id);
            _CompletionPending = false; _ReactionUntil = 0; _Cancelled = true;
            if (_Reactions) _Reactions.ResetPose();
            Publish(); Completed?.Invoke(new BossMatchResult(_Match.Epoch, FishId, GhostId, false, true));
            return true;
        }
        public bool RegisterGoal(bool fishScored)
        {
            if (!_Authority || !_Ready || _Serving || !_Match.Active) return false;
            _ReactionEvent++; _ReactionFishScored = fishScored;
            _ReactionFinal = fishScored || _Match.GhostScore + 1 >= 2;
            _ReactionUntil = Time.unscaledTime + (_Reactions ? _Reactions.ReactionDuration(_ReactionFinal) : 0);
            if (!_Match.Score(_Match.Epoch, fishScored)) return false;
            if (_Match.Active)
                ScheduleServe();
            Publish();
            return true;
        }

        void ScheduleServe()
        {
            if (!_Puck.isKinematic)
            {
                _Puck.linearVelocity = Vector3.zero;
                _Puck.angularVelocity = Vector3.zero;
            }

            _Puck.isKinematic = true;
            _Puck.position = _Centre + Vector3.up * .2f;
            _NextServe = Mathf.Max(Time.unscaledTime + _ServeDelay, _ReactionUntil);
            _Serving = true;
        }

        void Update()
        {
            if (!_Authority || !_CompletionPending) return;
            if (Time.unscaledTime >= _ReactionUntil)
            {
                _CompletionPending = false; Publish(); Completed?.Invoke(_PendingResult);
            }
            else if (Time.unscaledTime >= _NextSnapshot)
            {
                _NextSnapshot = Time.unscaledTime + _SnapshotInterval; Publish();
            }
        }
        void FixedUpdate()
        {
            if (!_Authority || !_Ready || !_Match.Active)
                return;
            float now = Time.unscaledTime;
            MovePaddle(_FishPaddle, now - _FishInputTime <= _InputTimeout ? _FishAxis : Vector2.zero, false);
            MovePaddle(_GhostPaddle, now - _GhostInputTime <= _InputTimeout ? _GhostAxis : Vector2.zero, true);
            if (_Serving && now >= _NextServe)
            {
                _Serving = false;
                _Puck.isKinematic = false;
                _Puck.linearVelocity = new Vector3(UnityEngine.Random.Range(-.25f, .25f), 0, _Match.GhostScore == 0 ? 1 : -1).normalized * _ServeSpeed;
            }

            if (!_Serving)
            {
                Vector3 v = _Puck.linearVelocity;
                v.y = 0;
                _Puck.linearVelocity = Vector3.ClampMagnitude(v, _PuckSpeedLimit);
                if (Mathf.Abs(_Puck.position.x - _Centre.x) > _HalfSize.x + 2 || Mathf.Abs(_Puck.position.z - _Centre.z) > _HalfSize.y + 2)
                    ScheduleServe();
            }

            if (now >= _NextSnapshot)
            {
                _NextSnapshot = now + _SnapshotInterval;
                Publish();
            }
        }

        void MovePaddle(Rigidbody body, Vector2 axis, bool ghost)
        {
            Vector3 p = body.position + new Vector3(axis.x, 0, axis.y) * (_PaddleSpeed * Time.fixedDeltaTime);
            p.x = Mathf.Clamp(p.x, _Centre.x - _HalfSize.x + _PaddleRadius, _Centre.x + _HalfSize.x - _PaddleRadius);
            p.z = Mathf.Clamp(p.z, _Centre.z + (ghost ? .55f : -_HalfSize.y + _PaddleRadius), _Centre.z + (ghost ? _HalfSize.y - _PaddleRadius : -.55f));
            p.y = _Centre.y + .1f;
            body.MovePosition(p);
        }

        void OnCompleted(BossMatchResult result)
        {
            _Serving = _Ready = false;
            if (_Puck)
            {
                if (!_Puck.isKinematic) _Puck.linearVelocity = Vector3.zero;
                _Puck.isKinematic = true;
            }
            _WinnerFish = result.WinnerFish;
            _Cancelled = result.Cancelled;
            _FishAxis = _GhostAxis = Vector2.zero;
            _PendingResult = result;
            _CompletionPending = !result.Cancelled && _Reactions && Time.unscaledTime < _ReactionUntil;
            if (result.Cancelled) _ReactionUntil = 0;
            Publish();
            if (!_CompletionPending) Completed?.Invoke(result);
        }

        void Publish()
        {
            if (!_Authority)
                return;
            _View = new BossArenaSnapshot
            {
                Epoch = _Match.Epoch,
                Revision = ++_Revision,
                FishId = _Match.FishId,
                GhostId = _Match.GhostId,
                Active = _Match.Active || _CompletionPending,
                ReactionEvent = _ReactionEvent, ReactionFishScored = _ReactionFishScored, ReactionFinal = _ReactionFinal,
                ReactionRemaining = Mathf.Max(0, _ReactionUntil - Time.unscaledTime), Serving = _Serving,
                ResultIssued = _Match.ResultIssued,
                WinnerFish = _WinnerFish,
                Cancelled = _Cancelled,
                ParticipantsReady = _Ready,
                FishScore = _Match.FishScore,
                GhostScore = _Match.GhostScore,
                PuckPosition = _Puck ? _Puck.position : _View?.PuckPosition ?? _Centre,
                PuckVelocity = _Puck && !_Puck.isKinematic ? _Puck.linearVelocity : Vector3.zero,
                FishPaddlePosition = _FishPaddle ? _FishPaddle.position : _View?.FishPaddlePosition ?? _Centre,
                GhostPaddlePosition = _GhostPaddle ? _GhostPaddle.position : _View?.GhostPaddlePosition ?? _Centre
            };
            PresentScore();
            SnapshotChanged?.Invoke(JsonUtility.ToJson(_View));
        }

        public bool ApplySnapshot(string json)
        {
            if (_Authority || string.IsNullOrEmpty(json) || json.Length > 4096)
                return false;
            BossArenaSnapshot s;
            try
            {
                s = JsonUtility.FromJson<BossArenaSnapshot>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (s == null || !float.IsFinite(s.ReactionRemaining) || s.ReactionRemaining < 0 || s.ReactionRemaining > 10 || !Finite(s.PuckPosition) || !Finite(s.PuckVelocity) || !Finite(s.FishPaddlePosition) || !Finite(s.GhostPaddlePosition))
                return false;
            if (_Observed && (s.Epoch < _ObservedEpoch || (s.Epoch == _ObservedEpoch && s.Revision <= _ObservedRevision)))
                return false;
            _Observed = true;
            _ObservedEpoch = s.Epoch;
            _ObservedRevision = s.Revision;
            _View = s;
            _Puck.position = s.PuckPosition;
            _FishPaddle.position = s.FishPaddlePosition;
            _GhostPaddle.position = s.GhostPaddlePosition;
            PresentScore();
            return true;
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        void PresentScore()
        {
            if (_Reactions && _View != null) _Reactions.Present(_View);
            if (!_Score || _View == null)
                return;
            _Score.text = _View.ResultIssued ? (_View.Cancelled ? "Match cancelled" : _View.WinnerFish ? "Fish wins!" : "Ghost wins") : "FISH " + _View.FishScore + " / 1     GHOST " + _View.GhostScore + " / 2";
        }

        void OnDisable()
        {
            if (_Authority && Active) Cancel(_Match.FishId);
            SetLocalParticipant(0);
        }
    }
}
