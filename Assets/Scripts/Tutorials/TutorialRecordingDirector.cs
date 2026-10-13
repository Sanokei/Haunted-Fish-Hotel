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
    public sealed class TutorialRecordingDirector : MonoBehaviour, IGameMovementEnvironment
    {
        public Camera Output;
        public UnityEngine.Video.VideoPlayer Review;
        public RenderTexture ReviewTarget;
        public RawImage ReviewImage;
        public GameObject ReviewRoot;
        public Text Heading, Caption, Progress;
        public Image Spotlight;
        public HotelPlayer Fish, Ghost;
        public Transform GhostFlight, CarriedPreview;
        public SpriteRenderer GhostArt, CarryArt;
        public GhostPlacementWorld World;
        public TrapManager Supplies;
        public GhostTrap CartDefinition, ChandelierDefinition, ClickDefinition;
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
        string demoAction = "";
        struct Step { public string Kind, Name, Body; public float Seconds; }
        readonly List<Step> steps = new List<Step>();

        public Vector3 ClampMouse(Vector3 point) => new Vector3(Mathf.Clamp(point.x, -15, 15), Mathf.Clamp(point.y, 0, 8), 0);

        void Awake()
        {
            if (!Output || !Heading || !Caption || !Progress || !Fish || !Ghost || !GhostFlight ||
                !CarriedPreview || !World || !Supplies || !Arena || !FishContent || !GhostContent)
                throw new InvalidOperationException("Tutorial recording scene requires every authored reference.");
            Fish.GetComponent<GameSideScrollMotor>().Bind(this);
            Ghost.GetComponent<GameSideScrollMotor>().Bind(this);
            Fish.Movement.SetMode(HotelControlMode.Fish);
            Fish.Movement.SetSimulationAuthority(true);
            Fish.Movement.SetControlState(true, false, false);
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
            Role = role; Completed = false; Failure = ""; Elapsed = 0;
            Observations.Clear(); chapter = 0; controlled = -1; activeTrap = null; boss = false;
            fishAxis = trapAxis = 0; fishRun = fishJump = false;
            if (ArenaRoot.activeSelf && Arena.Active) Arena.Cancel(1);
            World.ResetRound(); Supplies.ResetRound();
            Ghost.HeldTrapFamily = ""; Ghost.ControlledCube = -1;
            World.InjectDefinitions(new[] { CartDefinition, ChandelierDefinition, ClickDefinition });
            Ghost.RoundStateKey = "authored-tutorial-" + role; Ghost.RoundVersion = 1;
            Ghost.RoundIntroComplete = Ghost.RoundReleased = true;
            Ghost.GhostFlightReady = true; Ghost.GhostFlightPosition = new Vector3(-8, 6.5f, 0);
            World.BeginRound(Ghost.InventoryScope, Ghost.RoundStateKey, Ghost.RoundVersion);
            Supplies.BeginRound(Ghost.InventoryScope, Ghost.RoundStateKey, Ghost.RoundVersion);
            Supplies.ConfigureDefinitions(new[] { new TrapDefinition { Prefab = CartDefinition, Weight = 1 }, new TrapDefinition { Prefab = ChandelierDefinition, Weight = 0 }, new TrapDefinition { Prefab = ClickDefinition, Weight = 0 } });
            Supplies.Speed = .7f;
            Supplies.Simulate(Ghost, 0);
            Require(Supplies.StartConveyor(), "production conveyor start");
            if (role == TutorialRole.Fish) Supplies.StopConveyor();
            Hallway.SetActive(true); ArenaRoot.SetActive(false);
            Fish.gameObject.SetActive(role == TutorialRole.Fish);
            Fish.Movement.SetSimulationAuthority(true);
            Fish.Movement.SetControlState(true, false, false);
            Fish.Movement.Teleport(new Vector3(-8, 1, 0)); lamp = Fish.transform.position + Vector3.right;
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
                Progress.text = (Role == TutorialRole.Fish ? "FISH" : "GHOST") + "  /  " + "GUIDE";
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
                if (step.Kind == "demo")
                {
                    try { StartDemo(step.Name); } catch (Exception e) { Fail(e); yield break; }
                }
                float began = Time.time;
                while (Running && Time.time - began < step.Seconds) yield return null;
                if (!Running) yield break;
                if (step.Kind == "demo")
                {
                    try { FinishDemo(step.Name); } catch (Exception e) { Fail(e); yield break; }
                }
            }
            Running = false; Completed = true;
            fishAxis = trapAxis = 0; fishRun = false;
            RenderedFrames = Time.frameCount - framesAtStart;
            Observations.Add("Completed Ink sequence; rendered Unity frames=" + RenderedFrames + "; seconds=" + Elapsed.ToString("F3"));
            Progress.text = "HAUNTED FISH HOTEL  /  " + (Role == TutorialRole.Fish ? "FISH" : "GHOST") + " GUIDE";
        }

        void StartDemo(string action)
        {
            demoAction = action;
            Observations.Add("BEGIN " + action + " t=" + Elapsed.ToString("F3"));
            switch (action)
            {
                case "fish-walk": fishAxis = 1; initialFish = Fish.transform.position; break;
                case "fish-run": fishAxis = 1; fishRun = true; initialFish = Fish.transform.position; break;
                case "fish-jump":
                    Fish.Movement.Teleport(new Vector3(-6, 1, 0)); fishAxis = 1; fishJump = true; initialFish = Fish.transform.position; break;
                case "fish-obstacle":
                    Fish.Movement.Teleport(new Vector3(-6, 1, 0)); Spawn("cart", -2);
                    fishAxis = 1; fishRun = true; fishJump = true; initialFish = Fish.transform.position; break;
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
            if (action == "fish-walk" || action == "fish-run")
                Require(Mathf.Abs(Fish.transform.position.x - initialFish.x) > .8f, "actual motor " + action + " displacement");
            if (action == "cart-move") Require(Mathf.Abs(activeTrap.Position.x - initialFish.x) > .5f, "actual cart swept movement");
            if (action == "fall") Require(activeTrap.Position.y < initialFish.y - .5f, "actual chandelier fell");
            if (action == "boss") Require(initialPuckDistance > .1f, "actual boss physics cue ball advanced");
            fishAxis = trapAxis = 0; fishRun = false;
            if (action == "fish-light" && Spotlight) Spotlight.gameObject.SetActive(false);
            if (action == "fish-obstacle") Require(Fish.transform.position.x > activeTrap.CollisionBounds.max.x + .2f, "actual fish motor passed the solid cart");
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
            if (Fish.gameObject.activeSelf && !boss)
            {
                Fish.Movement.SetMode(HotelControlMode.Fish);
                Fish.Movement.SetSimulationAuthority(true);
                Fish.Movement.SetControlState(true, false, false);
                Fish.Movement.AcceptInput(new Vector2(fishAxis, 0), fishJump && Fish.BodyController.isGrounded, lamp, fishRun);
                if (Fish.Movement.SimulationReady && Fish.BodyController.isGrounded) fishJump = false;
                Fish.Movement.Simulate(Time.deltaTime);
                var visual = Fish.GetComponentInChildren<HotelFishSprite>();
                if (visual) visual.Present(Fish.Movement.FacingLeft, Fish.Movement.Walking, false, fishRun);
                if (demoAction == "fish-walk" || demoAction == "fish-run" || demoAction == "fish-jump" || demoAction == "fish-obstacle") { var cameraPosition = Output.transform.position; cameraPosition.x = Mathf.Lerp(cameraPosition.x, Fish.transform.position.x + 1, 1 - Mathf.Exp(-5 * Time.deltaTime)); Output.transform.position = cameraPosition; }
                if (Fish.transform.position.y > initialFish.y + .5f && !Observations.Contains("OBSERVED actual grounded jump")) Observations.Add("OBSERVED actual grounded jump");
            }
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
        void Fail(Exception error) { Failure = error.ToString(); Running = false; Debug.LogException(error, this); }
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine); routine = null;
            Running = false; fishAxis = trapAxis = 0; fishRun = false;
            if (Fish) Fish.Movement.ResetMotion();
            if (activeTrap) activeTrap.StopInput();
        }
        void OnDisable() => Stop();
        void OnDestroy() { if (spotlightMaterial) Destroy(spotlightMaterial); }
    }
}
