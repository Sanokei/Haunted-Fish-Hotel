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
        public BossDefenseSnapshot[] Defenses;
    }

    public sealed class BossArenaManager : MonoBehaviour
    {
        [SerializeField]
        Rigidbody _Puck, _FishPaddle, _GhostPaddle;
        [SerializeField] BossCharacterPresentation _Reactions;
        [SerializeField] BossArenaView _Views;
        [SerializeField] BossHudPresentation _Hud;
        [SerializeField] BossDefenseBoard _Defenses;
        [SerializeField] BossDragInput _DragInput;
        uint _PracticeDefenseEpoch;
        uint _LocalViewer;
        bool _LocalWatching;
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
        bool _EditorPractice, _PracticePaused;
        Vector2 _PracticeAxis,_PracticeGhostAxis; bool _PracticeGhostManual; BossViewerSide _PracticeViewerSide=BossViewerSide.Fish;
        Vector3 _PracticeVelocity;
        int _PracticeFishScore,_PracticeGhostScore;
        bool _PracticeRoundEnded,_PracticeWinnerFish;
        uint _Revision, _ObservedEpoch, _ObservedRevision;
        BossArenaSnapshot _View;
        uint _ReactionEvent;
        bool _ReactionFishScored, _ReactionFinal, _CompletionPending;
        float _ReactionUntil;
        BossMatchResult _PendingResult;
        public event Action<BossMatchResult> Completed;
        public event Action<string> SnapshotChanged;
        public Rigidbody Puck => _Puck;
        public BossDefenseBoard Defenses=>_Defenses;
        public BossDragInput DragInput=>_DragInput;
        public bool Serving=>_Serving;
        public Camera Camera => _Views ? _Views.CurrentCamera : _Camera;
        public Vector3 FishSpawn => _FishSpawn.position;
        public Vector3 GhostSpawn => _GhostSpawn.position;
        public Bounds Bounds => new Bounds(_Centre, new Vector3(_HalfSize.x * 2, 2, _HalfSize.y * 2));
        public bool Active => _Authority ? _Match.Active || _CompletionPending : _View != null && _View.Active;
        public uint FishId => _EditorPractice ? 0 : _Authority ? _Match.FishId : _View?.FishId ?? 0;
        public uint GhostId => _EditorPractice ? 0 : _Authority ? _Match.GhostId : _View?.GhostId ?? 0;
        public uint MatchEpoch => _Authority ? _Match.Epoch : _View?.Epoch ?? 0;
        public bool ParticipantsReady => _Authority ? _Ready : _View != null && _View.ParticipantsReady;
        public string Snapshot => _View == null ? string.Empty : JsonUtility.ToJson(_View);

        void Awake()
        {
            _Match.Completed += OnCompleted;
            if(_Defenses)_Defenses.Changed+=Publish;
            SetAuthority(false);
            SetLocalParticipant(0);
        }

        public void SetAuthority(bool authority)
        {
            _Authority = authority;
            if(_Defenses)_Defenses.SetAuthority(authority);
            _Puck.isKinematic = !authority || !_Match.Active;
            _FishPaddle.isKinematic = _GhostPaddle.isKinematic = true;
            _Puck.detectCollisions=authority; _FishPaddle.detectCollisions=_GhostPaddle.detectCollisions=true;
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
            _LocalViewer=localId;_LocalWatching=localId!=0&&watch;
            RefreshViewer();
            if (!watch && _Reactions) _Reactions.ResetPose();
        }
        BossViewerSide ViewerSide=>_EditorPractice?_PracticeViewerSide:_LocalViewer!=0&&_LocalViewer==FishId?BossViewerSide.Fish:
            _LocalViewer!=0&&_LocalViewer==GhostId?BossViewerSide.Ghost:BossViewerSide.Spectator;
        void RefreshViewer()
        {
            if(_EditorPractice&&_Reactions)_Reactions.SetGhostLifeStage(_PracticeFishScore);
            if(_Views)_Views.Select(ViewerSide,_LocalWatching);
            else if(_Camera)_Camera.gameObject.SetActive(_LocalWatching);
            if(_Hud)_Hud.Present(_EditorPractice?new BossArenaSnapshot{FishScore=_PracticeFishScore,GhostScore=_PracticeGhostScore,ResultIssued=_PracticeRoundEnded,WinnerFish=_PracticeWinnerFish}:_View,ViewerSide,_LocalWatching,_EditorPractice);
        }

        public bool TryBegin(uint fishId, uint ghostId)
        {
            if (_EditorPractice) return false;
            if (!_Authority || _CompletionPending || !_Match.TryBegin(fishId, ghostId))
                return false;
            if(_Defenses){_Defenses.Begin(_Match.Epoch,fishId,ghostId);_Defenses.SetPlayable(true,false);}
            if(_DragInput)_DragInput.Cancel();
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

        // Pointer steers the owned paddle; only authority advances its Rigidbody.
        // The puck remains a dynamic body moved by paddle/rail collisions.
        public Vector2 PointerAxis(uint id, Vector2 screenPosition, float inputInterval)
        {
            if (!BossPaddleInputPolicy.OwnsPaddle(id, FishId, GhostId, Active, ParticipantsReady)) return Vector2.zero;
            return ProjectPointer(screenPosition, inputInterval, id == GhostId);
        }
        Vector2 ProjectPointer(Vector2 screenPosition, float inputInterval, bool ghost)
        {
            if(!TryPointerTarget(screenPosition,out Vector3 target))return Vector2.zero;
            return AxisToTarget((ghost?_GhostPaddle:_FishPaddle).position,target,inputInterval,ghost);
        }

        public bool TryPointerTarget(Vector2 screenPosition,out Vector3 target)
        {
            target=default;var camera=Camera;if(_Views)_Views.Fit();
            if(!camera||!float.IsFinite(screenPosition.x)||!float.IsFinite(screenPosition.y)||!camera.pixelRect.Contains(screenPosition))return false;
            var ray=camera.ScreenPointToRay(screenPosition);var plane=new Plane(Vector3.up,_Centre+Vector3.up*.1f);
            if(!plane.Raycast(ray,out float distance))return false;target=ray.GetPoint(distance);return Finite(target);
        }
        public Vector2 AxisToTarget(Vector3 position,Vector3 target,float interval,bool ghost)
        {
            return BossPaddleInputPolicy.TryAxis(target.x,target.z,position.x,position.z,_Centre.x,_Centre.z,_HalfSize.x,_HalfSize.y,_PaddleRadius,_PaddleSpeed,interval,ghost,out float x,out float z)?new Vector2(x,z):Vector2.zero;
        }
#if UNITY_EDITOR
        // Local practice has no participant IDs, snapshots, goals or match results.
        public bool EditorPracticing => _EditorPractice;
        public void BeginEditorPractice()
        {
            if (Active || _EditorPractice) return;
            _EditorPractice = true; _PracticePaused = false; _PracticeAxis = _PracticeGhostAxis = Vector2.zero;_PracticeGhostManual=false;_PracticeViewerSide=BossViewerSide.Fish;
            if(_Defenses){_Defenses.SetAuthority(true);_Defenses.Begin(++_PracticeDefenseEpoch,1,2,true);_Defenses.SetPlayable(true,false);}
            _PracticeFishScore=_PracticeGhostScore=0;_PracticeRoundEnded=false;
            _FishPaddle.isKinematic = _GhostPaddle.isKinematic = true;
            _Puck.detectCollisions = _FishPaddle.detectCollisions = _GhostPaddle.detectCollisions = true;
            _FishPaddle.position = _Centre + new Vector3(0, .1f, -3.6f);
            _GhostPaddle.position = _Centre + new Vector3(0, .1f, 3.6f);
            ScheduleServe();
            if (_Score) _Score.text = "PRACTICE - Mouse / WASD\nTraining ghost - no match results";
            RefreshViewer();
        }
        public Vector2 EditorPointerAxis(Vector2 screenPosition) => _EditorPractice ? ProjectPointer(screenPosition, .05f, false) : Vector2.zero;
        public void SetEditorPracticeInput(Vector2 axis, bool paused)
        {
            if (_EditorPractice && _PracticePaused != paused && !_Serving)
            {
                if (paused) { _PracticeVelocity = _Puck.linearVelocity; _Puck.isKinematic = true; }
                else { _Puck.isKinematic = false; _Puck.linearVelocity = _PracticeVelocity; }
            }
            _PracticePaused = paused;
            _PracticeAxis = !paused && float.IsFinite(axis.x) && float.IsFinite(axis.y) ? Vector2.ClampMagnitude(axis, 1) : Vector2.zero;
        }
        public void SetEditorPracticeGhostInput(Vector2 axis){if(!_EditorPractice)return;_PracticeGhostManual=true;_PracticeGhostAxis=!_PracticePaused&&float.IsFinite(axis.x)&&float.IsFinite(axis.y)?Vector2.ClampMagnitude(axis,1):Vector2.zero;}
        public void SetEditorPracticeViewer(BossViewerSide side){if(!_EditorPractice)return;_PracticeViewerSide=side;RefreshViewer();}
        public void StopEditorPractice()
        {
            if (!_EditorPractice) return;
            _EditorPractice = false; _PracticeAxis = Vector2.zero; if(_Defenses)_Defenses.SetPlayable(false,false);if(_DragInput)_DragInput.Cancel();
            if (!_Puck.isKinematic) _Puck.linearVelocity = Vector3.zero;
            SetAuthority(_Authority);
            if (_Reactions) _Reactions.ResetPose();
        }
#endif
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
            if (_EditorPractice)
            {
                if(!_Serving)
                {
                    if(fishScored)_PracticeFishScore++;else _PracticeGhostScore++;
                    _PracticeRoundEnded=_PracticeFishScore>=BossMatch.FishGoalsToWin||_PracticeGhostScore>=BossMatch.GhostGoalsToWin;
                    _PracticeWinnerFish=fishScored;ScheduleServe();
                    if(_PracticeRoundEnded)_NextServe=Mathf.Max(_NextServe,Time.unscaledTime+2);
                    RefreshViewer();
                }
                return false;
            }
            if (!_Authority || !_Ready || _Serving || !_Match.Active) return false;
            _ReactionEvent++; _ReactionFishScored = fishScored;
            _ReactionFinal = fishScored ? _Match.FishScore+1 >= BossMatch.FishGoalsToWin : _Match.GhostScore+1 >= BossMatch.GhostGoalsToWin;
            _ReactionUntil = Time.unscaledTime + (_Reactions ? _Reactions.ReactionDuration(_ReactionFinal) : 0);
            if (!_Match.Score(_Match.Epoch, fishScored)) return false;
            if (_Match.Active)
                ScheduleServe();
            Publish();
            return true;
        }

        void ScheduleServe()
        {
            if(_Defenses){_Defenses.ServeStarted();_Defenses.SetPlayable(_EditorPractice||_Match.Active,false);}
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
            if(_Defenses)_Defenses.SetPlayable(_EditorPractice||_Match.Active,_EditorPractice?!_PracticePaused&&!_Serving:_Ready&&!_Serving&&_Match.Active);
            if (_EditorPractice)
            {
                if (_PracticePaused) return;
                MovePaddle(_FishPaddle, _PracticeAxis, false);
                // A restrained local training paddle follows the puck in its own half.
                var delta = new Vector2(_Puck.position.x - _GhostPaddle.position.x, _Centre.z + 3.6f - _GhostPaddle.position.z);
                MovePaddle(_GhostPaddle,_PracticeGhostManual?_PracticeGhostAxis:Vector2.ClampMagnitude(delta,.55f),true);
                if (_Serving && Time.unscaledTime >= _NextServe)
                {
                    if(_PracticeRoundEnded){_PracticeFishScore=_PracticeGhostScore=0;_PracticeRoundEnded=false;RefreshViewer();}
                    _Serving = false; _Puck.isKinematic = false;
                    _Puck.linearVelocity = new Vector3(.15f, 0, -1).normalized * _ServeSpeed;
                }
                if (!_Serving)
                {
                    var velocity = _Puck.linearVelocity; velocity.y = 0;
                    _Puck.linearVelocity = Vector3.ClampMagnitude(velocity, _PuckSpeedLimit);
                    if (Mathf.Abs(_Puck.position.x - _Centre.x) > _HalfSize.x + 2 || Mathf.Abs(_Puck.position.z - _Centre.z) > _HalfSize.y + 2) ScheduleServe();
                }
                return;
            }
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
            if(_Defenses)p=_Defenses.ClampStriker(body.position,p,_PaddleRadius);
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
            if(_Defenses)_Defenses.SetPlayable(false,false);if(_DragInput)_DragInput.Cancel();
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
                GhostPaddlePosition = _GhostPaddle ? _GhostPaddle.position : _View?.GhostPaddlePosition ?? _Centre,
                Defenses = _Defenses ? _Defenses.Snapshot() : null
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
            if(_Defenses&&!_Defenses.ValidateSnapshot(s.Epoch,s.FishId,s.GhostId,s.Defenses))return false;
            if (_Observed && (s.Epoch < _ObservedEpoch || (s.Epoch == _ObservedEpoch && s.Revision <= _ObservedRevision)))
                return false;
            _Observed = true;
            _ObservedEpoch = s.Epoch;
            _ObservedRevision = s.Revision;
            _View = s;
            _Puck.position = s.PuckPosition;
            _FishPaddle.position = s.FishPaddlePosition;
            _GhostPaddle.position = s.GhostPaddlePosition;
            if(_Defenses)_Defenses.ApplySnapshot(s.Epoch,s.FishId,s.GhostId,s.Defenses,s.Active,s.ParticipantsReady&&!s.Serving);
            PresentScore();
            return true;
        }

        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        void PresentScore()
        {
            if (_Reactions && _View != null) _Reactions.Present(_View);
            RefreshViewer();
            if (!_Score || _View == null)
                return;
            _Score.text = _View.ResultIssued ? (_View.Cancelled ? "Match cancelled" : _View.WinnerFish ? "Fish wins!" : "Ghost wins") : "FISH LIFE " + Mathf.Max(0,BossMatch.GhostGoalsToWin-_View.GhostScore) + "     GHOST LIVES " + Mathf.Max(0,BossMatch.FishGoalsToWin-_View.FishScore);
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            StopEditorPractice();
#endif
            if (_Authority && Active) Cancel(_Match.FishId);
            SetLocalParticipant(0);
        }
    }
}
