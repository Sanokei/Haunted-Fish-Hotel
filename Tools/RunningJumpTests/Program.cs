using System;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;

internal static class Program
{
    static int _Checks;
    static int Main()
    {
        try
        {
            Rules(); GroundAndLaunch(); AirborneControl(); QueuedIntent(); Interruptions(); LandingAndRepeat(); FrameRates();
            Console.WriteLine($"PASS: {_Checks} running-jump checks (actual production rules/motor with adapter doubles; no native physics).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Check(bool valid, string message) { _Checks++; if (!valid) throw new Exception("FAIL: " + message); }
    static void Near(float actual, float expected, string message, float tolerance = .0001f)
        => Check(Math.Abs(actual - expected) <= tolerance, message + $" ({actual} vs {expected})");
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    sealed class Fixture
    {
        public readonly GameSideScrollMotor Motor = new GameSideScrollMotor { JumpSpeed = 10.48f };
        public readonly HotelPlayerMovement Move = new HotelPlayerMovement();
        public readonly CharacterController Body = new CharacterController { isGrounded = true };
        public Fixture(float boost = 7.5f)
        {
            Time.unscaledTime = 0;
            Move.Transform = Motor.transform; Move.Controller = Body;
            Set(Motor, "_Movement", Move); Set(Motor, "_Controller", Body); Set(Motor, "_RunningJumpSpeed", boost);
            Motor.Bind(new GameSceneController());
        }
        public bool Boosted => Get<RunningJumpState>(Motor, "_RunningJump").Active;
        public float Vertical => Get<float>(Motor, "_Vertical");
        public void Input(float axis = 1, bool jump = false, bool run = true) => Motor.Accept(new Vector2(axis, 0), jump, Vector3.zero, run);
        public void Step(float delta = .02f, bool stunned = false)
        { Motor.Simulate(delta, stunned); if (RunningJumpState.Finite(delta)) Time.unscaledTime += delta; }
        public void Tick(float axis = 1, bool jump = false, bool run = true, float delta = .02f)
        { Input(axis, jump, run); Step(delta); }
        public void Launch(float axis = 1) => Tick(axis, true);
    }
    static void Rules()
    {
        var state = new RunningJumpState();
        Check(!state.Begin(false, true, true, 1, 7.5f), "Airborne command cannot arm boost");
        Check(!state.Begin(true, false, true, 1, 7.5f), "Non-fish cannot arm boost");
        Check(!state.Begin(true, true, false, 1, 7.5f), "Walking jump cannot arm boost");
        Check(!state.Begin(true, true, true, 0, 7.5f), "Directionless jump cannot arm boost");
        Check(!state.Begin(true, true, true, .001f, 7.5f), "Stick drift cannot arm boost");
        Check(!state.Begin(true, true, true, 1, 0), "Missing serialized tuning preserves old behavior");
        Check(state.Begin(true, true, true, .5f, 7.5f), "Intentional analog run-jump arms boost");
        Near(state.HorizontalVelocity(.5f, true, false, 1.8f, 4.5f, 7.5f), 3.75f, "Analog magnitude is retained");
        Near(state.HorizontalVelocity(100, true, false, 1.8f, 4.5f, 7.5f), 7.5f, "Axis cannot amplify the configured speed");
        Near(state.HorizontalVelocity(-1, true, false, 1.8f, 4.5f, 7.5f), -4.5f, "Reversal cancels rather than reverses boost");
        Check(!state.Active, "Reversal clears the current jump's boost");
        Near(state.HorizontalVelocity(1, true, false, 1.8f, 4.5f, 7.5f), 4.5f, "Returning to launch direction cannot re-arm in air");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            Check(!state.Begin(true, true, true, 1, invalid), "Invalid tuning does not arm boost");
            Near(state.HorizontalVelocity(float.NaN, true, false, 1.8f, 4.5f, invalid), 0, "Invalid axis cannot inject movement");
        }
        state.Begin(true, true, true, 1, 3);
        Near(state.HorizontalVelocity(1, true, false, 1.8f, 4.5f, 3), 4.5f, "Low tuning never slows ordinary running");
    }
    static void GroundAndLaunch()
    {
        var f = new Fixture();
        f.Tick(run: false); Near(f.Move.Displacement.x, 1.8f * .02f, "Ground walk remains 1.8");
        f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Ground run remains 4.5");
        Check(!f.Boosted, "Running on ground does not grant boost");
        f.Launch(); Near(f.Move.Displacement.x, 7.5f * .02f, "Actual grounded running jump begins at authored7.5");
        Check(f.Boosted && f.Move.JumpSounds == 1, "One real launch starts boost and jump event once");
        Near(f.Move.Displacement.y, 10.48f * .02f - .5f * 25 * .02f * .02f, "Launch vertical integration is unchanged");
        f = new Fixture(0); f.Launch(); Near(f.Move.Displacement.x, 4.5f * .02f, "Old prefab with zero boost remains back-compatible");
        f = new Fixture(); f.Tick(jump: true, run: false); Near(f.Move.Displacement.x, 1.8f * .02f, "Ordinary walking jump is unchanged");
        f = new Fixture(); f.Launch(-10); Near(f.Move.Displacement.x, -7.5f * .02f, "Left launch is normalized and symmetric");
        f = new Fixture(); f.Launch(.1f); Near(f.Move.Displacement.x, .75f * .02f, "Small stick input gets proportional speed, not full boost");
        f = new Fixture(); f.Move.Mode = HotelControlMode.Lobby; f.Launch(); Check(!f.Boosted, "Non-fish mode cannot gain boost");
        f = new Fixture(); f.Body.isGrounded = false; f.Motor.transform.position = new Vector3(0, 1, 0); f.Tick();
        Near(f.Move.Displacement.x, 4.5f * .02f, "Walking off a ledge never grants run-jump speed");
    }
    static void AirborneControl()
    {
        var f = new Fixture(); f.Launch();
        f.Tick(); Near(f.Move.Displacement.x, 7.5f * .02f, "Holding run and launch direction maintains speed");
        float before = f.Vertical; f.Tick(jump: true);
        Near(f.Vertical, before - 25 * .02f, "Repeated airborne jump command cannot refresh vertical jump");
        f.Tick(axis: .5f); Near(f.Move.Displacement.x, 3.75f * .02f, "Air steering preserves analog magnitude");
        f.Tick(axis: -1); Near(f.Move.Displacement.x, -4.5f * .02f, "Air reversal uses ordinary steering speed");
        f.Tick(jump: true); Check(!f.Boosted, "Fresh airborne command cannot re-arm cancelled boost");
        f = new Fixture(); f.Launch(); f.Tick(axis: 0); Check(!f.Boosted, "Direction release cancels boost");
        f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Direction resume in air stays unboosted");
        f = new Fixture(); f.Launch(); f.Tick(run: false); Check(!f.Boosted, "Run release cancels boost");
        Near(f.Move.Displacement.x, 1.8f * .02f, "Run release restores walking air control");
        f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Run re-press in air cannot re-arm boost");
    }
    static void QueuedIntent()
    {
        var f = new Fixture(); f.Input(1, true, false); f.Input(1, false, true); f.Step();
        Check(!f.Boosted, "Later run packet cannot rewrite a walking-jump command");
        f = new Fixture(); f.Input(0, true, true); f.Input(1, false, true); f.Step();
        Check(!f.Boosted, "Later direction packet cannot rewrite a stationary-jump command");
        f = new Fixture(); f.Input(1, true, true); f.Input(-1, false, true); f.Step();
        Check(!f.Boosted, "Reversal between command and launch cancels boost");
        f = new Fixture(); f.Input(1, true, true); f.Body.isGrounded = false; f.Motor.transform.position = new Vector3(0, 1, 0); f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Queued input alone cannot substitute for a grounded launch");
        f = new Fixture(); f.Input(1, true, true); f.Step(0);
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Paused tick does not consume or launch a pending jump");
        f.Step(); Check(f.Boosted && f.Move.JumpSounds == 1, "Pending jump launches only on resumed simulation");
        f = new Fixture(); f.Input(1, true, true); f.Move.CanMove = false; f.Step(0); f.Move.CanMove = true; f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Hard lock cancels a pending launch even during a paused tick");
        f = new Fixture(); f.Move.CanMove = false; f.Input(1, true, true); f.Move.CanMove = true; f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Locked input cannot queue a boosted jump for after unlock");
        f = new Fixture(); f.Move.SharedDialogueLocked = true; f.Input(1, true, true); f.Move.SharedDialogueLocked = false; f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Dialogue-locked input cannot queue a boosted jump for later");
    }
    static void Interruptions()
    {
        var f = new Fixture(); f.Launch(); f.Move.CanMove = false; f.Tick();
        Check(!f.Boosted, "Hard movement lock cancels boost"); Near(f.Move.Displacement.x, 0, "Hard movement lock blocks horizontal motion");
        f.Move.CanMove = true; f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Unlock cannot restore old airborne boost");
        f = new Fixture(); f.Launch(); f.Move.SharedDialogueLocked = true; f.Tick();
        Check(!f.Boosted, "Shared dialogue clears boost"); Near(f.Move.Displacement.x, 0, "Shared dialogue blocks horizontal motion");
        f.Move.SharedDialogueLocked = false; f.Tick(); Check(!f.Boosted, "Dialogue ending does not resurrect boost");
        f = new Fixture(); f.Launch(); f.Step(stunned: true); Check(!f.Boosted, "Authoritative stun clears boost");
        f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Stun ending cannot resurrect boost");
        f = new Fixture(); f.Launch(); f.Move.IsStunned = true; f.Input(1, true, true); f.Step(stunned: true);
        Check(!f.Boosted && f.Move.JumpSounds == 1, "Stunned input cannot grant another jump/boost");
        f = new Fixture(); f.Launch(); f.Motor.ClearInput(); Check(!f.Boosted, "Stun ClearInput clears boost immediately");
        f = new Fixture(); f.Launch(); f.Motor.ResetMotion(); Check(!f.Boosted, "Death/teleport/scene reset clears boost immediately");
        f = new Fixture(); f.Launch(); f.Move.SimulationReady = false; f.Input(1, true, true); f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 1, "Authority loss rejects commands and clears boost");
        f = new Fixture(); f.Move.SimulationReady = false; f.Input(1, true, true); f.Step();
        Check(!f.Boosted && f.Move.JumpSounds == 0, "Observer cannot start a jump or boost");
        f = new Fixture(); f.Launch(); Time.unscaledTime += .31f; f.Step();
        Check(!f.Boosted, "Input timeout clears boost"); Near(f.Move.Displacement.x, 0, "Input timeout stops horizontal movement");
        f = new Fixture(); f.Launch(); f.Move.FailMove = true; f.Tick(); Check(!f.Boosted, "Failed collision movement cannot retain boost");
    }
    static void LandingAndRepeat()
    {
        var f = new Fixture(); f.Launch();
        for (int i = 0; i < 100 && !f.Body.isGrounded; i++) f.Tick();
        Check(f.Body.isGrounded && !f.Boosted, "Collision landing immediately clears boost");
        f.Tick(); Near(f.Move.Displacement.x, 4.5f * .02f, "Landed running remains4.5");
        f.Launch(); Check(f.Boosted && f.Move.JumpSounds == 2, "A new grounded intentional jump can boost again");
    }
    static void FrameRates()
    {
        float expectedApex = 10.48f * 10.48f / (2 * 25);
        foreach (int fps in new[] { 30, 60, 120, 240 })
        {
            var normal = new Fixture(0); var boosted = new Fixture();
            normal.Move.Ceiling = boosted.Move.Ceiling = 2.36f;
            float peak = 0;
            for (int frame = 0; frame < fps; frame++)
            {
                float dt = 1f / fps;
                normal.Tick(jump: frame == 0, delta: dt);
                boosted.Tick(jump: frame == 0, delta: dt);
                Near(boosted.Motor.transform.position.y, normal.Motor.transform.position.y, "Horizontal boost cannot alter vertical arc at" + fps + "fps");
                peak = Math.Max(peak, boosted.Motor.transform.position.y);
                Check((boosted.Move.LastCollisionFlags & CollisionFlags.Above) == 0, "Existing ceiling headroom remains at" + fps + "fps");
                if (frame + 1 == fps * 2 / 5)
                {
                    Near(boosted.Motor.transform.position.x, 3, "Boost reaches3 units horizontally after0.4 seconds at" + fps + "fps", .0005f);
                    Near(boosted.Motor.transform.position.y, 2.192f, "Vertical height remains analytic at0.4 seconds", .0005f);
                }
            }
            Near(peak, expectedApex, "Apex stays2.1966m with boost at" + fps + "fps", .004f);
            Check(!boosted.Boosted && boosted.Body.isGrounded, "Boost expires on landing at" + fps + "fps");
        }
    }
}
