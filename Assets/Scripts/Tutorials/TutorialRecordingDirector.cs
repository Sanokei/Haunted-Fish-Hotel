using System;
using System.Collections;
using System.Collections.Generic;
using HauntedFish.Multiplayer;
using HauntedFish.BossFight;
using Ink.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace HauntedFish.Tutorials
{
    [Serializable]
    public sealed class TutorialShot
    {
        public string Name;
        public Transform Pose;
        public float FieldOfView = 46;
        public bool Orthographic = true;
        public float Size = 3.8f;
    }

    // Isolated filming adapter. Authored scene references are mandatory. Gameplay commands use
    // the production motor, supply, placement, possession, trap physics and boss simulation.
    // Only gameplay spawning through those existing systems occurs during filming.
    [DefaultExecutionOrder(2000)]
    public sealed class TutorialRecordingDirector : MonoBehaviour, IGameMovementEnvironment, ITrapDamageContext, IHotelOfflineInputSource
    {
        public Camera Output;
        public UnityEngine.Video.VideoPlayer Review;
        public RenderTexture ReviewTarget;
        public RawImage ReviewImage;
        public GameObject ReviewRoot;
        public Text Heading, Caption, Progress;
        public Image Spotlight;
        public HotelPlayer Fish, OtherFish, Ghost;
        public GameHauntingController Haunting;
        public GameObject FishStunEffect;
        public Animation FishStunAnimation;
        public Transform GhostFlight, CarriedPreview;
        public SpriteRenderer GhostArt, CarryArt;
        public GhostPlacementWorld World;
        public TrapManager Supplies;
        public GhostTrap CartDefinition, ChandelierDefinition, ClickDefinition, AnglerDefinition;
        public GameObject Hallway, ArenaRoot;
        public BossArenaManager Arena;
        public TutorialRoleContent FishContent, GhostContent;
        public TutorialShot[] Shots;
        public AnimationCurve CameraEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public bool Running { get; private set; }
        public bool Completed { get; private set; }
        public string Failure { get; private set; }
        public TutorialRole Role { get; private set; }
        public float Elapsed { get; private set; }
        public bool Capturing { get; private set; }
        public void SetCapturing(bool value) => Capturing = value;
        public int RenderedFrames { get; private set; }
        public readonly List<string> Observations = new List<string>();
        Coroutine routine;
        Material spotlightMaterial;
        float fishAxis;
        bool fishRun, fishJump;
        Vector3 lamp;
        int controlled = -1;
        GhostTrap activeTrap;
        float trapAxis;
        Vector3 initialFish;
        Vector3 initialPuck;
        float initialPuckDistance;
        int chapter, framesAtStart;
        bool boss;
        string demoAction = "", recordingKey;
        float jumpLaunchX;
        bool jumpPrepared, jumpIssued, jumpContact;
        float demoStarted, beforeHealth, recoveredX, entryX, previousTrapX, slowSpeed, clearSpeed, patternStart;
        int beforeDeath, otherBeforeDeath, lightPhase;
        bool sawStun, sawPiranha, sawRecovery, sawFirstSoul, sawSecondSoul, sawLightOn, sawLightOff;
        bool sawPattern, sawSway;
        Bounds firstBody, secondBody;
        Quaternion initialSuspendedRotation, initialCeilingRotation;
        Vector3 initialCeilingPosition;
        GhostTrapAreaPresentation observedPresentation;
        public bool DamageContextActive => isActiveAndEnabled && Running;
        public GhostPlacementWorld DamageWorld => World;
        public float DamageFloorY => 0;

        public void ReadInput(HotelPlayer actor, out Vector2 move, out bool jump, out Vector3 mouse, out bool run)
        {
            move = Vector2.zero; jump = run = false; mouse = actor ? actor.transform.position + Vector3.right : Vector3.zero;
            if (!isActiveAndEnabled || !Running || actor != Fish || actor.Networked || actor.IsDead || boss) return;
            move = new Vector2(fishAxis, 0); run = fishRun; mouse = lamp;
            if (demoAction == "fish-obstacle" && !jumpIssued)
            {
                float remaining = jumpLaunchX - actor.transform.position.x;
                if (remaining <= .003f) { fishJump = true; move.x = 1; }
                else move.x = Mathf.Clamp01(remaining / Mathf.Max(.0001f, actor.Movement.RunningSpeed * Time.deltaTime));
            }
            jump = fishJump && actor.Movement.SimulationReady && actor.BodyController.isGrounded;
            if (jump)
            {
                fishJump = false;
                if (demoAction == "fish-obstacle") jumpIssued = true;
            }
        }
        struct Step { public string Kind, Name, Body; public float Seconds; }
        readonly List<Step> steps = new List<Step>();

        public Vector3 ClampMouse(Vector3 point) => new Vector3(Mathf.Clamp(point.x, -15, 15), Mathf.Clamp(point.y, 0, 8), 0);

        void Awake()
        {
            if (!Output || !Heading || !Caption || !Progress || !Fish || !OtherFish || !Ghost || !GhostFlight || !Haunting ||
                !CartDefinition || !ChandelierDefinition || !AnglerDefinition || !FishStunEffect || !FishStunAnimation ||
                !CarriedPreview || !World || !Supplies || !Arena || !FishContent || !GhostContent)
                throw new InvalidOperationException("Tutorial recording scene requires every authored reference.");
            foreach (var actor in new[] { Fish, OtherFish, Ghost })
            {
                if (actor.Networked) throw new InvalidOperationException("Recording actors must be authored offline.");
                actor.GetComponent<GameSideScrollMotor>().Bind(this);
            }
            if (Spotlight && Spotlight.material)
            {
                spotlightMaterial = new Material(Spotlight.material);
                Spotlight.material = spotlightMaterial;
            }
            Heading.text = "HAUNTED FISH HOTEL";
            Caption.text = "Authored tutorial recording stage";
            Progress.text = "Choose Fish or Ghost recording from Tools / Haunted Fish / Authored tutorials.";
        }
        public void Begin(TutorialRole role)
        {
            if (Running) throw new InvalidOperationException("Tutorial sequence is already running.");
            try
            {
                Role = role; recordingKey = "authored-tutorial-" + role + "-" + Guid.NewGuid().ToString("N"); Completed = false; Failure = ""; Elapsed = 0;
                Observations.Clear(); chapter = 0; controlled = -1; activeTrap = null; boss = false;
                fishAxis = trapAxis = 0; fishRun = fishJump = false;
                if (ArenaRoot.activeSelf && Arena.Active) Arena.Cancel(1);
                World.ResetRound(); Supplies.ResetRound();
                Ghost.HeldTrapFamily = ""; Ghost.ControlledCube = -1;
                World.InjectDefinitions(new[] { CartDefinition, ChandelierDefinition, ClickDefinition, AnglerDefinition });
                PrepareActor(Ghost, true, new Vector3(-8, 6.5f, 0));
                PrepareActor(Fish, role == TutorialRole.Fish, new Vector3(-8, 1, 0));
                PrepareActor(OtherFish, false, new Vector3(10, 1, 0));
                Haunting.enabled = true; Haunting.BindContext(this); World.BindHaunting(Haunting);
                Ghost.GhostFlightReady = true; Ghost.GhostFlightPosition = new Vector3(-8, 6.5f, 0);
                World.BeginRound(Ghost.InventoryScope, Ghost.RoundStateKey, Ghost.RoundVersion);
                Supplies.BeginRound(Ghost.InventoryScope, Ghost.RoundStateKey, Ghost.RoundVersion);
                Supplies.ConfigureDefinitions(new[] { new TrapDefinition { Prefab = CartDefinition, Weight = 1 }, new TrapDefinition { Prefab = ChandelierDefinition, Weight = 0 }, new TrapDefinition { Prefab = ClickDefinition, Weight = 0 }, new TrapDefinition { Prefab = AnglerDefinition, Weight = 0 } });
                Supplies.Speed = .7f;
                Supplies.Simulate(Ghost, 0);
                Require(Supplies.StartConveyor(), "production conveyor start");
                if (role == TutorialRole.Fish) Supplies.StopConveyor();
                Hallway.SetActive(true); ArenaRoot.SetActive(false);
                lamp = Fish.transform.position + Vector3.right;
                initialFish = Fish.transform.position;
                GhostFlight.position = new Vector3(-8, 6.5f, 0);
                GhostFlight.gameObject.SetActive(role == TutorialRole.Ghost);
                CarriedPreview.gameObject.SetActive(false);
                if (Spotlight) Spotlight.gameObject.SetActive(false);
                framesAtStart = Time.frameCount;
                Read((role == TutorialRole.Fish ? FishContent : GhostContent).RecordingInk);
                Running = true;
                routine = StartCoroutine(Play());
            }
            catch (Exception error) { Fail(error); throw; }
        }

        void Read(TextAsset ink)
        {
            if (!ink) throw new InvalidOperationException("Compiled recording Ink missing.");
            steps.Clear();
            var story = new Story(ink.text);
            void Queue(Step step)
            {
                if (!float.IsFinite(step.Seconds) || step.Seconds < 0 || step.Seconds > 30 || steps.Count >= 160)
                    throw new InvalidOperationException("Invalid recording sequence duration or size.");
                steps.Add(step);
            }
            story.BindExternalFunction("TutorialShot", (string name, float seconds) => Queue(new Step { Kind = "shot", Name = name, Seconds = seconds }));
            story.BindExternalFunction("TutorialCaption", (string heading, string body) => Queue(new Step { Kind = "caption", Name = heading, Body = body }));
            story.BindExternalFunction("TutorialDemo", (string action, float seconds) => Queue(new Step { Kind = "demo", Name = action, Seconds = seconds }));
            story.BindExternalFunction("TutorialSetup", (string action, float seconds) => Queue(new Step { Kind = "setup", Name = action, Seconds = seconds }));
            story.BindExternalFunction("Wait", (float seconds) => Queue(new Step { Kind = "wait", Seconds = seconds }));
            while (story.canContinue)
                if (!string.IsNullOrWhiteSpace(story.Continue())) throw new InvalidOperationException("Recording Ink must use timed functions.");
            if (story.hasError || story.currentChoices.Count > 0) throw new InvalidOperationException("Recording Ink cannot wait for choices.");
            foreach (var step in steps)
                if (step.Kind == "shot" && Array.Find(Shots, shot => shot.Name == step.Name) == null)
                    throw new InvalidOperationException("Missing authored camera shot: " + step.Name);
        }

        IEnumerator Play()
        {
            foreach (var step in steps)
            {
                chapter++;
                Progress.text = ""; // Opening intertitle identifies the role once.
                if (step.Kind == "caption") { Heading.text = step.Name; Caption.text = step.Body; continue; }
                if (step.Kind == "shot")
                {
                    var shot = Array.Find(Shots, value => value.Name == step.Name);
                    var before = Output.transform.position;
                    var rotation = Output.transform.rotation; float size = Output.orthographicSize, fov = Output.fieldOfView;
                    Output.orthographic = shot.Orthographic;
                    float t = 0;
                    do
                    {
                        float ratio = step.Seconds <= 0 ? 1 : CameraEase.Evaluate(Mathf.Clamp01(t / step.Seconds));
                        Output.transform.SetPositionAndRotation(Vector3.Lerp(before, shot.Pose.position, ratio), Quaternion.Slerp(rotation, shot.Pose.rotation, ratio));
                        Output.orthographicSize = Mathf.Lerp(size, shot.Size, ratio); Output.fieldOfView = Mathf.Lerp(fov, shot.FieldOfView, ratio);
                        if (t >= step.Seconds) break;
                        yield return null; t += Time.deltaTime;
                    } while (Running);
                    continue;
                }
                if (step.Kind == "demo" || step.Kind == "setup")
                {
                    try { StartDemo(step.Name); } catch (Exception e) { Fail(e); yield break; }
                }
                float began = Time.time;
                while (Running && Time.time - began < step.Seconds) yield return null;
                if (!Running) yield break;
                if (step.Kind == "demo" || step.Kind == "setup")
                {
                    try { FinishDemo(step.Name); } catch (Exception e) { Fail(e); yield break; }
                }
            }
            Running = false; Completed = true;
            fishAxis = trapAxis = 0; fishRun = false;
            RenderedFrames = Time.frameCount - framesAtStart;
            Observations.Add("Completed Ink sequence; rendered Unity frames=" + RenderedFrames + "; seconds=" + Elapsed.ToString("F3"));
            Progress.text = "";
            ReleaseBindings();
        }

        void StartDemo(string action)
        {
            demoAction = action; demoStarted = Time.time;
            Observations.Add("BEGIN " + action + " t=" + Elapsed.ToString("F3"));
            switch (action)
            {
                case "fish-walk": fishAxis = 1; initialFish = Fish.transform.position; break;
                case "fish-run": fishAxis = 1; fishRun = true; initialFish = Fish.transform.position; break;
                case "fish-jump":
                    Fish.Movement.Teleport(new Vector3(-6, 1, 0)); fishAxis = 1; fishJump = true; initialFish = Fish.transform.position; break;
                case "jump-setup":
                    PrepareTrapDemo(CartDefinition, -2);
                    jumpPrepared = jumpIssued = jumpContact = false;
                    jumpLaunchX = FindJumpLaunchX();
                    PrepareActor(Fish, true, new Vector3(jumpLaunchX - .6f, 1, 0));
                    jumpPrepared = true; trapAxis = 0; break;
                case "fish-obstacle":
                    Require(jumpPrepared, "stationary tablecart jump has settled authored starting positions");
                    fishAxis = 1; fishRun = true; fishJump = false; initialFish = Fish.transform.position;
                    beforeHealth = Fish.Health; beforeDeath = Fish.GhostEmergenceVersion; break;
                case "danger-band":
                    ObservePresentation(); patternStart = observedPresentation.DangerPatternTime; sawPattern = false; break;
                case "tablecart-hit":
                    PrepareTrapDemo(CartDefinition, -3);
                    PrepareActor(Fish, true, new Vector3(activeTrap.CollisionBounds.max.x + ActorRadius(Fish) + .3f, 1, 0));
                    beforeHealth = Fish.Health; beforeDeath = Fish.GhostEmergenceVersion; initialFish = Fish.transform.position;
                    sawStun = sawPiranha = sawRecovery = false; fishAxis = trapAxis = 0;
                    World.Possess(Ghost, activeTrap.Index, activeTrap.Position); break;
                case "chandelier-sway":
                    PrepareTrapDemo(ChandelierDefinition, 5);
                    World.Possess(Ghost, activeTrap.Index, activeTrap.Position);
                    ObservePresentation();
                    Require(observedPresentation.SuspendedPivot && observedPresentation.FixedCeilingLayout, "separated chandelier has authored suspended pivot and fixed base");
                    initialSuspendedRotation = observedPresentation.SuspendedPivot.localRotation;
                    initialCeilingRotation = observedPresentation.FixedCeilingLayout.rotation;
                    initialCeilingPosition = observedPresentation.FixedCeilingLayout.position; sawSway = false; break;
                case "angler-place":
                    PrepareTrapDemo(AnglerDefinition, -4);
                    float spare = activeTrap.Limits.y - activeTrap.CollisionBounds.max.x - activeTrap.BodyHalfSize.x -
                        2 * ActorRadius(Fish) - 2 * ActorRadius(OtherFish);
                    float gap = spare / 4;
                    Require(gap > .05f, "angler authored travel band has clear entry and exit space");
                    float firstX = activeTrap.CollisionBounds.max.x + ActorRadius(Fish) + gap;
                    float secondX = firstX + ActorRadius(Fish) + ActorRadius(OtherFish) + gap;
                    PrepareActor(Fish, true, new Vector3(firstX, 1, 0));
                    PrepareActor(OtherFish, true, new Vector3(secondX, 1, 0));
                    Require(secondX + ActorRadius(OtherFish) + activeTrap.BodyHalfSize.x + gap < activeTrap.Limits.y,
                        "angler authored travel band fits two victims and body exit");
                    ObservePresentation(); break;
                case "angler-light":
                    sawLightOn = sawLightOff = false; lightPhase = 0;
                    World.Possess(Ghost, -1, GhostFlight.position); break;
                case "angler-sweep":
                    Require(activeTrap && activeTrap.PiercesBodies, "angler demonstration uses production piercing trap");
                    World.Possess(Ghost, activeTrap.Index, activeTrap.Position);
                    firstBody = Fish.BodyController.bounds; secondBody = OtherFish.BodyController.bounds;
                    beforeDeath = Fish.GhostEmergenceVersion; otherBeforeDeath = OtherFish.GhostEmergenceVersion;
                    sawFirstSoul = sawSecondSoul = false; slowSpeed = float.MaxValue; clearSpeed = 0;
                    previousTrapX = entryX = activeTrap.Position.x; trapAxis = 1; break;
                case "fish-light": if (Spotlight) Spotlight.gameObject.SetActive(true); break;
                case "ghost-fly": break;
                case "package":
                    var package = Supplies.ClosestPackage(GhostFlight.position);
                    Require(package, "conveyor supplied authored package");
                    GhostFlight.position = package.transform.position;
                    Require(Supplies.TryTake(Ghost, package.PackageId, GhostFlight.position, Ghost.RoundStateKey, Ghost.RoundVersion), "production conveyor E/X pickup");
                    CarryArt.sprite = CartDefinition.Icon; CarriedPreview.gameObject.SetActive(true);
                    break;
                case "carry": break;
                case "invalid":
                    GhostFlight.position = new Vector3(-12, 1, 0); CarryArt.color = new Color(.9f, .3f, .2f, .8f);
                    Require(!World.TryPlace(Ghost, GhostFlight.position, "cart"), "production E/X rejects unsafe placement");
                    Require(!World.PlacementPoint(new Vector3(-12, 1, 0), "cart", out _), "production safe hallway rejected trap footprint"); break;
                case "place":
                    Require(World.PlacementPoint(new Vector3(-2, 1, 0), Ghost.HeldTrapFamily, out var point), "production floor placement valid");
                    GhostFlight.position = point;
                    Require(World.TryPlace(Ghost, point, Ghost.HeldTrapFamily), "production E/X placed held cart");
                    activeTrap = LastPlacedTrap(); controlled = activeTrap.Index;
                    CarriedPreview.gameObject.SetActive(false); break;
                case "possess":
                    World.Possess(Ghost, controlled, activeTrap.Position);
                    Require(Ghost.ControlledCube == controlled, "production E/X possession"); break;
                case "cart-move": trapAxis = 1; initialFish = activeTrap.Position; break;
                case "unpossess":
                    World.Possess(Ghost, -1, GhostFlight.position);
                    Require(Ghost.ControlledCube == -1, "production E/X unpossession"); break;
                case "chandelier":
                    Fish.gameObject.SetActive(false); OtherFish.gameObject.SetActive(false);
                    Spawn("chandelier", 5); GhostFlight.position = activeTrap.Position;
                    World.Possess(Ghost, activeTrap.Index, GhostFlight.position);
                    Require(Ghost.ControlledCube == activeTrap.Index, "production chandelier possession"); break;
                case "fall":
                    Require(World.AcceptTrapAction(Ghost, activeTrap.Index, new TrapInput(TrapInputKind.Activate)), "production Space chandelier activate"); initialFish = activeTrap.Position; break;
                case "reset":
                    Require(World.AcceptTrapAction(Ghost, activeTrap.Index, new TrapInput(TrapInputKind.Reset)), "production F / gamepad B reset"); break;
                case "click":
                    World.Possess(Ghost, -1, GhostFlight.position); Spawn("click", 10);
                    GhostFlight.position = activeTrap.Position; World.Possess(Ghost, activeTrap.Index, GhostFlight.position);
                    Require(World.AcceptTrapAction(Ghost, activeTrap.Index, new TrapInput(TrapInputKind.Click, point: activeTrap.CollisionBounds.center)), "production mouse click activates stationary trap"); break;
                case "dispose":
                    Require(World.Dispose(Ghost, activeTrap.Index), "production Q disposal");
                    Require(Ghost.ControlledCube == -1, "disposal releases possession"); activeTrap = null; break;
                case "boss":
                    fishAxis = trapAxis = 0; Hallway.SetActive(false); GhostFlight.gameObject.SetActive(false); Fish.gameObject.SetActive(false);
                    if (Spotlight) Spotlight.gameObject.SetActive(false);
                    ArenaRoot.SetActive(true); Arena.SetAuthority(true);
                    Require(Arena.TryBegin(1, 2), "production boss match started");
                    Arena.SetParticipantsReady(true); Arena.SetLocalViewer(Role == TutorialRole.Fish ? 1u : 2u, true);
                    boss = true;
                    var view = Arena.Camera;
                    Output.orthographic = false; Output.fieldOfView = view.fieldOfView; Output.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
                    foreach (var camera in ArenaRoot.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                    initialPuck = Arena.Puck.position; initialPuckDistance = 0; break;
                default: throw new InvalidOperationException("Unknown tutorial demonstration: " + action);
            }
        }

        float FindJumpLaunchX()
        {
            Physics.SyncTransforms();
            var motor = Fish.GetComponent<GameSideScrollMotor>();
            float speed = motor.RunningJumpSpeed > 0 ? motor.RunningJumpSpeed : Fish.Movement.RunningSpeed;
            float gravity = Fish.Movement.Gravity, jump = motor.JumpSpeed;
            Require(speed > 0 && gravity > 0 && jump > 0, "production jump parameters are valid");
            float radius = ActorRadius(Fish);
            float half = Mathf.Max(radius, Fish.BodyController.height * Mathf.Abs(Fish.transform.lossyScale.y) * .5f);
            float segment = half - radius, duration = 2 * jump / gravity;
            var colliders = activeTrap.GetComponentsInChildren<BoxCollider>();
            float centred = activeTrap.CollisionBounds.center.x - speed * jump / gravity;
            float best = float.NegativeInfinity, launch = centred;
            // Plan from the actual enabled solid parts, not the full placement AABB.
            // This chooses a command location; it never moves physics or alters tuning.
            for (int candidate = -50; candidate <= 50; candidate++)
            {
                float x0 = centred + candidate * radius / 50, clearance = float.PositiveInfinity;
                for (int sample = 0; sample <= 240; sample++)
                {
                    float t = duration * sample / 240;
                    float x = x0 + speed * t, y = DamageFloorY + half + jump * t - .5f * gravity * t * t;
                    foreach (var collider in colliders)
                    {
                        if (!collider.enabled || collider.isTrigger) continue;
                        var box = collider.bounds;
                        float dx = Mathf.Max(box.min.x - GameHauntingController.ContactMargin - x, 0, x - box.max.x - GameHauntingController.ContactMargin);
                        float dy = Mathf.Max(box.min.y - y - segment, 0, y - segment - box.max.y);
                        clearance = Mathf.Min(clearance, Mathf.Sqrt(dx * dx + dy * dy) - radius);
                    }
                }
                if (clearance > best) { best = clearance; launch = x0; }
            }
            Require(!float.IsInfinity(best) && best > .025f, "production run-jump trajectory clears actual tablecart solid parts");
            Observations.Add("PLANNED stationary-cart capsule clearance=" + best.ToString("F4") + "; native contact-free traversal still required");
            return launch;
        }

        static float ActorRadius(HotelPlayer actor)
        {
            var scale = actor.transform.lossyScale;
            return actor.BodyController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        void PrepareActor(HotelPlayer actor, bool visible, Vector3 position)
        {
            if (actor.Networked) throw new InvalidOperationException("Refusing to initialize a networked recording actor.");
            actor.gameObject.SetActive(true); actor.enabled = true;
            actor.ClearRoundInventory(); actor.GhostId = actor == Ghost ? actor.NetId : uint.MaxValue;
            actor.RoundStateKey = recordingKey; actor.RoundVersion = 1;
            actor.RoundIntroComplete = actor.RoundReleased = true;
            actor.GetComponent<GameSideScrollMotor>().Bind(this); actor.BindOfflineInput(this);
            actor.Teleport(position); actor.gameObject.SetActive(visible);
        }

        void PrepareTrapDemo(GhostTrap definition, float x)
        {
            fishAxis = trapAxis = 0; fishRun = fishJump = false;
            Fish.gameObject.SetActive(false); OtherFish.gameObject.SetActive(false);
            World.ResetRound(); World.BeginRound(Ghost.InventoryScope, Ghost.RoundStateKey, Ghost.RoundVersion);
            Spawn(definition.FamilyTag, x);
        }

        void ObservePresentation()
        {
            observedPresentation = activeTrap ? activeTrap.GetComponentInChildren<GhostTrapAreaPresentation>(true) : null;
            Require(observedPresentation, "recording uses production trap presentation");
            Require(observedPresentation.RangeMarker && observedPresentation.RangeMarker.name == "BOSS-cursor" &&
                observedPresentation.DangerPattern && observedPresentation.DangerPattern.name == "Fish-ver-ghost",
                "production danger band has BOSS-cursor endpoints and Fish-ver-ghost fill");
        }

        void ObserveTrapDemo()
        {
            float age = Time.time - demoStarted;
            if (demoAction == "fish-obstacle") jumpContact |= Fish.IsStunned || activeTrap.HadMotionContact(Fish);
            if (demoAction == "danger-band")
                sawPattern |= observedPresentation.DangerBandVisible && observedPresentation.DangerPatternTime > patternStart + .25f;
            if (demoAction == "tablecart-hit")
            {
                if (!sawStun && age > .5f) trapAxis = 1;
                if (Fish.Movement.IsStunned)
                {
                    sawStun = true; trapAxis = 0; activeTrap.StopInput();
                    sawPiranha |= FishStunEffect.activeInHierarchy && FishStunAnimation.isPlaying;
                }
                else if (sawStun)
                {
                    if (!sawRecovery) { sawRecovery = true; recoveredX = Fish.transform.position.x; }
                    fishAxis = 1;
                }
            }
            if (demoAction == "chandelier-sway" && age > .15f)
            {
                sawSway |= Quaternion.Angle(initialSuspendedRotation, observedPresentation.SuspendedPivot.localRotation) > .1f;
                // Camera projection settles on the first presentation frame; the fixed base
                // must stay fixed thereafter while the suspended child changes rotation.
                if (age < .3f) { initialCeilingPosition = observedPresentation.FixedCeilingLayout.position; initialCeilingRotation = observedPresentation.FixedCeilingLayout.rotation; }
                else Require(Vector3.Distance(initialCeilingPosition, observedPresentation.FixedCeilingLayout.position) < .01f &&
                    Quaternion.Angle(initialCeilingRotation, observedPresentation.FixedCeilingLayout.rotation) < .01f, "chandelier fixed base remains still");
            }
            if (demoAction == "angler-light")
            {
                if (age > .5f && lightPhase == 0) { World.Possess(Ghost, activeTrap.Index, activeTrap.Position); lightPhase = 1; }
                if (lightPhase == 1) sawLightOn |= observedPresentation.IsPossessed && observedPresentation.PossessionLightVisible;
                if (age > 1.5f && lightPhase == 1) { World.Possess(Ghost, -1, GhostFlight.position); lightPhase = 2; }
                if (lightPhase == 2) sawLightOff |= !observedPresentation.IsPossessed && !observedPresentation.PossessionLightVisible;
                if (age > 2.5f && lightPhase == 2) { World.Possess(Ghost, activeTrap.Index, activeTrap.Position); lightPhase = 3; }
            }
            if (demoAction == "angler-sweep")
            {
                float speed = Mathf.Abs(activeTrap.Position.x - previousTrapX) / Mathf.Max(.00001f, Time.deltaTime);
                if (speed > .01f)
                {
                    var body = activeTrap.CollisionBounds;
                    bool inside = body.Intersects(firstBody) || body.Intersects(secondBody);
                    if (inside) slowSpeed = Mathf.Min(slowSpeed, speed);
                    else if (activeTrap.Position.x > entryX + .01f) clearSpeed = Mathf.Max(clearSpeed, speed);
                }
                previousTrapX = activeTrap.Position.x;
                sawFirstSoul |= Fish.IsDead && Fish.DeathEmergence && Fish.DeathEmergence.gameObject.activeInHierarchy;
                sawSecondSoul |= OtherFish.IsDead && OtherFish.DeathEmergence && OtherFish.DeathEmergence.gameObject.activeInHierarchy;
            }
        }

        void ReleaseBindings()
        {
            if (World) World.UnbindHaunting(Haunting);
            if (Haunting) { Haunting.UnbindContext(this); Haunting.enabled = false; }
            foreach (var actor in new[] { Fish, OtherFish, Ghost })
                if (actor) { actor.UnbindOfflineInput(this); actor.GetComponent<GameSideScrollMotor>().Unbind(this); actor.Movement.ResetMotion(); actor.enabled = false; }
        }

        void Spawn(string family, float x)
        {
            if (Ghost.ControlledCube >= 0) World.Possess(Ghost, -1, GhostFlight.position);
            Require(World.Hold(Ghost, family, GhostFlight.position), "production held " + family);
            Require(World.PlacementPoint(new Vector3(x, 1, 0), family, out var point), "production placement point for " + family);
            Require(World.TryPlace(Ghost, point, family), "production placed " + family);
            activeTrap = LastPlacedTrap(); controlled = activeTrap.Index;
            Require(activeTrap, "placed authored " + family + " prefab");
        }

        GhostTrap LastPlacedTrap()
        {
            var state = JsonUtility.FromJson<GhostCubeSnapshot>(World.Snapshot);
            Require(state.Cubes.Count > 0, "production placement snapshot contains trap");
            var trap = World.Trap(state.Cubes[state.Cubes.Count - 1].Id);
            Require(trap, "production placement ID resolves to spawned trap");
            return trap;
        }

        void FinishDemo(string action)
        {
            if (action == "jump-setup") Require(jumpPrepared && Fish.Movement.SimulationReady, "stationary-cart jump setup settled at production collider-derived launch point");
            if (action == "danger-band") Require(sawPattern, "production-danger-band-animation");
            if (action == "tablecart-hit")
            {
                Require(sawStun && sawRecovery && !Fish.IsDead && Fish.Health == beforeHealth && Fish.GhostEmergenceVersion == beforeDeath &&
                    Fish.transform.position.x > initialFish.x + .2f && Fish.transform.position.x > recoveredX + .2f, "tablecart-nonlethal-knockback-stun");
                Require(sawPiranha && !FishStunEffect.activeInHierarchy && !Fish.Movement.IsStunned, "authored-piranha-stun-and-clear");
            }
            if (action == "chandelier-sway") Require(sawSway, "chandelier-separated-swing");
            if (action == "angler-light") Require(sawLightOn && sawLightOff, "angler-possession-light");
            if (action == "angler-sweep")
            {
                Require(Fish.IsDead && OtherFish.IsDead && Fish.ControlMode == HotelControlMode.Spectator && OtherFish.ControlMode == HotelControlMode.Spectator &&
                    Fish.GhostEmergenceVersion == beforeDeath + 1 && OtherFish.GhostEmergenceVersion == otherBeforeDeath + 1 &&
                    activeTrap.CollisionBounds.min.x > secondBody.max.x, "angler-crosses-multiple-fish");
                Require(slowSpeed < float.MaxValue && clearSpeed > slowSpeed * 1.2f, "angler-body-slowdown");
                Require(sawFirstSoul && sawSecondSoul && !Fish.DeathEmergence.gameObject.activeSelf && !OtherFish.DeathEmergence.gameObject.activeSelf,
                    "authored-death-soul-and-clear");
            }
            if (action == "fish-walk" || action == "fish-run")
                Require(Mathf.Abs(Fish.transform.position.x - initialFish.x) > .8f, "actual motor " + action + " displacement");
            if (action == "cart-move") Require(Mathf.Abs(activeTrap.Position.x - initialFish.x) > .5f, "actual cart swept movement");
            if (action == "fall") Require(activeTrap.Position.y < initialFish.y - .5f, "actual chandelier fell");
            if (action == "boss") Require(initialPuckDistance > .1f, "actual boss physics cue ball advanced");
            fishAxis = trapAxis = 0; fishRun = false;
            if (activeTrap) activeTrap.StopInput();
            if (action == "fish-light" && Spotlight) Spotlight.gameObject.SetActive(false);
            if (action == "fish-obstacle") Require(jumpIssued && !jumpContact && !Fish.IsDead && Fish.Health == beforeHealth &&
                Fish.GhostEmergenceVersion == beforeDeath && Fish.transform.position.x - ActorRadius(Fish) > activeTrap.CollisionBounds.max.x + .05f,
                "running-jump-clears-stationary-tablecart");
            demoAction = "";
            Observations.Add("END " + action + " t=" + Elapsed.ToString("F3"));
        }

        void Update()
        {
            if (!Running) return;
            try { Simulate(); } catch (Exception e) { Fail(e); }
        }
        void Simulate()
        {
            Elapsed += Time.deltaTime;
            // HotelPlayer owns input, motor simulation, stun replication and corpse presentation.
            // The recorder supplies only offline inputs and observes those production results.
            if (Fish.gameObject.activeSelf && !Fish.IsDead && !boss)
            {
                if (demoAction == "fish-walk" || demoAction == "fish-run" || demoAction == "fish-jump" || demoAction == "fish-obstacle")
                { var cameraPosition = Output.transform.position; cameraPosition.x = Mathf.Lerp(cameraPosition.x, Fish.transform.position.x + 1, 1 - Mathf.Exp(-5 * Time.deltaTime)); Output.transform.position = cameraPosition; }
                if (Fish.transform.position.y > initialFish.y + .5f && !Observations.Contains("OBSERVED actual grounded jump")) Observations.Add("OBSERVED actual grounded jump");
            }
            ObserveTrapDemo();
            if (Role == TutorialRole.Ghost && GhostFlight.gameObject.activeSelf)
            {
                string demo = CurrentDemo;
                if (demo == "ghost-fly") GhostFlight.position = Vector3.MoveTowards(GhostFlight.position, new Vector3(-2, 4, 0), 8 * Time.deltaTime);
                if (demo == "carry") GhostFlight.position = Vector3.MoveTowards(GhostFlight.position, new Vector3(-2, 1, 0), 8 * Time.deltaTime);
                if (Ghost.ControlledCube >= 0 && activeTrap) GhostFlight.position = activeTrap.Position;
                CarriedPreview.position = GhostFlight.position;
                if (GhostArt) GhostArt.flipX = false;
            }
            if (trapAxis != 0 && activeTrap) Require(World.AcceptTrapAction(Ghost, activeTrap.Index, new TrapInput(TrapInputKind.Move, trapAxis)), "cart input");
            if (spotlightMaterial && Spotlight.gameObject.activeSelf)
            {
                float x = .5f + Mathf.Sin(Elapsed * .8f) * .3f;
                spotlightMaterial.SetVector("_Mouse", new Vector4(x, .44f, 0, 0));
                spotlightMaterial.SetFloat("_Aspect", Output.aspect);
                spotlightMaterial.SetFloat("_Radius", .2f); spotlightMaterial.SetFloat("_Softness", .13f); spotlightMaterial.SetFloat("_Darkness", .85f);
            }
            if (boss)
            {
                Arena.ApplyInput(1, new Vector2(Mathf.Sin(Elapsed * 1.4f), Mathf.Cos(Elapsed) * .2f));
                Arena.ApplyInput(2, new Vector2(-Mathf.Sin(Elapsed * 1.1f), Mathf.Cos(Elapsed) * .2f));
                initialPuckDistance = Mathf.Max(initialPuckDistance, Vector3.Distance(Arena.Puck.position, initialPuck));
            }
        }
        string CurrentDemo
        {
            get { return demoAction; }
        }
        void Require(bool condition, string observation)
        {
            if (!condition) throw new InvalidOperationException(observation + " failed.");
            if (!Observations.Contains("PASS " + observation)) Observations.Add("PASS " + observation);
        }
        void Fail(Exception error) { Failure = error.ToString(); Running = false; ReleaseBindings(); Debug.LogException(error, this); }
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine); routine = null;
            Running = false; fishAxis = trapAxis = 0; fishRun = false;
            ReleaseBindings();
            if (Fish) Fish.Movement.ResetMotion();
            if (activeTrap) activeTrap.StopInput();
        }
        void OnDisable() => Stop();
        void OnDestroy() { if (spotlightMaterial) Destroy(spotlightMaterial); }
    }
}
