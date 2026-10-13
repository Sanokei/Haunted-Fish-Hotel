using System;
using System.IO;
using System.Linq;
using Monologue.Dialogue;

static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static string Compile(string source)
    {
        var errors = new System.Collections.Generic.List<string>();
        var story = new Ink.Compiler(source, new Ink.Compiler.Options
        {
            errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) errors.Add(message); }
        }).Compile();
        if (story == null || errors.Count > 0) throw new Exception(string.Join("\n", errors));
        return story.ToJson();
    }
    static void Reject(string source, string message)
    {
        bool rejected = false;
        try { StoryFunctions.ReadAnimationSequence(Compile(source)); }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    static void Main(string[] args)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var inkPath = Path.Combine(root, "Assets/Dialogue/LobbyIntroduction.ink");
        var json = Compile(File.ReadAllText(inkPath));
        var steps = StoryFunctions.ReadAnimationSequence(json);
        Check(steps.Select(step => step.Kind).SequenceEqual(new[] {
            StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.SetVariant, StorySequenceStepKind.Wait, StorySequenceStepKind.SetVariant,
            StorySequenceStepKind.Wait, StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.Knock, StorySequenceStepKind.Wait, StorySequenceStepKind.PushTo,
            StorySequenceStepKind.MoveUI, StorySequenceStepKind.Wait, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.PushTo, StorySequenceStepKind.Wait, StorySequenceStepKind.ExpandPanel,
            StorySequenceStepKind.Wait, StorySequenceStepKind.ParallaxTo, StorySequenceStepKind.FlashFrames, StorySequenceStepKind.ParallaxTo,
            StorySequenceStepKind.Wait, StorySequenceStepKind.LobbyShot, StorySequenceStepKind.LobbyShot,
            StorySequenceStepKind.LobbyLine, StorySequenceStepKind.LobbyShot, StorySequenceStepKind.Wait }), "Authored storyboard uses explicit typed story functions in the required order");
        Check(steps[0].Target == "exterior" && !steps[0].Visible &&
            steps[1].Target == "letter_read" && !steps[1].Visible,
            "Replay hides the previous exterior and reading reveals before showing the storm");
        var variants = steps.Where(step => step.Kind == StorySequenceStepKind.SetVariant).ToArray();
        Check(variants[0].Target == "cloud" && variants[0].Variant == "cloud" && variants[1].Variant == "cloud_lightning",
            "The still cloud changes to its lightning variant");
        var knocks = steps.Single(step => step.Kind == StorySequenceStepKind.Knock);
        Check(knocks.Target == "door" && knocks.Count == 3 && knocks.Seconds == .45f, "Ink specifies the door, knock count and interval");
        var move = steps.Single(step => step.Kind == StorySequenceStepKind.MoveUI);
        Check(move.Target == "letter" && move.X == 40 && move.Y == 50 && move.Seconds == 1.8f, "Ink controls the diagonal ground slide and duration");
        Check(steps.Where(step => step.Kind == StorySequenceStepKind.PushTo).Select(step => step.Target)
            .SequenceEqual(new[] { "letter_under_door", "letter_read" }), "Both pushes address panels on the same strip");
        Check(steps[16].Target == "letter_read" && steps[16].Visible && steps[17].Kind == StorySequenceStepKind.PushTo,
            "The incoming reading panel is visible before the second push");
        Check(steps[18].Kind == StorySequenceStepKind.Wait && steps[18].Seconds == 5, "Reading shot lingers before expansion");
        Check(steps[19].Kind == StorySequenceStepKind.ExpandPanel && steps[19].Seconds == 4,
            "The reading rhombus slowly takes over the frame");
        Check(steps[22].Kind == StorySequenceStepKind.FlashFrames && steps[22].Count == 2,
            "Ink requests exactly two white rendered frames");
        Check(steps[23].Kind == StorySequenceStepKind.ParallaxTo && steps[23].Target == "exterior" && steps[23].Variant == "gate_arrival" && steps[23].Seconds == 5 && steps[23].Easing == "smoother",
            "The flash is followed by the reusable authored crane-down/push-in camera pose");
        Check(steps.Where(s => s.Kind == StorySequenceStepKind.LobbyShot).Select(s => s.Target)
            .SequenceEqual(new[] { "entry", "walk_away", "reception" }), "Fish enters before walking away and the reception dolly follows");
        Check(steps.Single(s => s.Kind == StorySequenceStepKind.LobbyLine).Variant ==
            "Looks like nobody has checked into this place for years", "Fish caption exactly matches the requested dialogue");
        var grandmaPath = Path.Combine(root, "Assets/Dialogue/GrandmafishInspect.ink");
        var grandmaJson = Compile(File.ReadAllText(grandmaPath));
        var grandma = new Ink.Runtime.Story(grandmaJson);
        int cues = 0;
        while (grandma.canContinue)
        {
            var line = grandma.Continue().Trim();
            if (grandma.currentTags.Contains("animation: grandma_rimshot"))
            {
                Check(line == "She was quite the catch.", "Rimshot is attached only to the intended punchline");
                cues++;
            }
        }
        Check(cues == 1, "Grandma's story contains exactly one rimshot cue");
        checks += PunchlineAudioTests.Run();
        checks += DoorPushTests.Run(root);
        checks += ParallaxTests.Run(root);
        bool missingWorldRejected = false;
        try { HauntedFish.Multiplayer.LobbyIntroductionSequenceValidation.UIOnly(steps, null); }
        catch (InvalidOperationException error) { missingWorldRejected = error.Message.Contains("._World"); }
        Check(missingWorldRejected, "Missing world setup is rejected before the intro can appear to complete");
        bool missingCaptionRejected = false;
        try { HauntedFish.Multiplayer.LobbyIntroductionSequenceValidation.UIOnly(steps, s => s.Kind != StorySequenceStepKind.LobbyLine); }
        catch (InvalidOperationException error) { missingCaptionRejected = error.Message.Contains("LobbyLine(player)"); }
        Check(missingCaptionRejected, "Partial world setup identifies the unbound caption operation");
        bool missingReceptionRejected = false;
        try { HauntedFish.Multiplayer.LobbyIntroductionSequenceValidation.UIOnly(steps, s => s.Target != "reception"); }
        catch (InvalidOperationException error) { missingReceptionRejected = error.Message.Contains("LobbyShot(reception)"); }
        Check(missingReceptionRejected, "Partial camera setup identifies the unbound reception operation");
        var boundUI = HauntedFish.Multiplayer.LobbyIntroductionSequenceValidation.UIOnly(steps, s => true);
        Check(boundUI.Count == steps.Count - 4 && !boundUI.Any(s => s.Kind == StorySequenceStepKind.LobbyShot || s.Kind == StorySequenceStepKind.LobbyLine),
            "Complete world setup passes validation and retains every original UI operation");
        var lobbyManagerSource = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Multiplayer/Lobby/LobbyManager.cs"));
        int beginIntro = lobbyManagerSource.IndexOf("void BeginIntroduction()");
        int validateIntro = lobbyManagerSource.IndexOf("_IntroductionPresentation.ValidateSequence(steps)", beginIntro);
        int markPlayed = lobbyManagerSource.IndexOf("_IntroductionPlayed = true", beginIntro);
        Check(markPlayed > validateIntro, "A missing setup cannot mark the intro as played before validation succeeds");
        var conditional = StoryFunctions.ReadAnimationSequence(Compile("EXTERNAL SetVariant(objectTag,variantTag)\nVAR storm = true\n{storm:\n~ SetVariant(\"cloud\",\"cloud_lightning\")\n}\n-> END"));
        Check(conditional.Single().Variant == "cloud_lightning", "Ink variables and branching select typed operations");
        var zero = StoryFunctions.ReadAnimationSequence(Compile("EXTERNAL Wait(seconds)\n~ Wait(0)\n-> END"));
        Check(zero.Single().Seconds == 0, "Zero-duration operations are allowed");
        Reject("EXTERNAL Wait(seconds)\n~ Wait(-1)\n-> END", "Negative animation timing is rejected before playback");
        Reject("EXTERNAL SetVisible(objectTag,visible)\n~ SetVisible(\"\",true)\n-> END", "Empty object tags are rejected before playback");
        Reject("EXTERNAL Knock(objectTag,count,interval)\n~ Knock(\"door\",0,0.4)\n-> END", "Invalid knock counts are rejected");
        Reject("Unexpected dialogue\n-> END", "A dialogue story cannot silently replace the automatic animation sequence");
        var created = StoryFunctions.ReadAnimationSequence(Compile("EXTERNAL CreateUI(prefab,instance,parent)\n~ CreateUI(\"letter\",\"delivered\",\"floor\")\n-> END"));
        Check(created.Single().Kind == StorySequenceStepKind.CreateUI && created.Single().Target == "delivered" &&
            created.Single().Variant == "letter" && created.Single().Parent == "floor", "CreateUI retains explicit prefab, instance and parent tags");
        Reject("* [Choose something]\n-> END", "Choices cannot leave the introduction waiting indefinitely");
        int received = 0;
        StoryFunctions.OnSetVisible visible = (tag, value) => { Check(tag == "door" && value, "SetVisible dispatches typed object and visibility arguments"); received++; };
        StoryFunctions.OnMoveUI moved = (tag, x, y, seconds) => { Check(tag == "letter" && x == 10 && y == 20 && seconds == .5f, "MoveUI dispatches coordinates and duration like MoveTo"); received++; };
        StoryFunctions.OnSetVisibleEvent += visible;
        StoryFunctions.OnMoveUIEvent += moved;
        try
        {
            var eventStory = new Ink.Runtime.Story(Compile("EXTERNAL SetVisible(objectTag,visible)\nEXTERNAL MoveUI(objectTag,x,y,seconds)\n~ SetVisible(\"door\",true)\n~ MoveUI(\"letter\",10,20,0.5)\n-> END"));
            StoryFunctions.BindAnimationFunctions(eventStory);
            while (eventStory.canContinue) eventStory.Continue();
            StoryFunctions.UnbindAnimationFunctions(eventStory);
            Check(received == 2, "Ordinary story bindings emit the matching StoryFunctions events");
        }
        finally
        {
            StoryFunctions.OnSetVisibleEvent -= visible;
            StoryFunctions.OnMoveUIEvent -= moved;
        }
        var repeated = StoryFunctions.ReadAnimationSequence(json);
        Check(repeated.Count == steps.Count, "Replaying a sequence creates independent story bindings");
        if (args.Contains("--compile"))
        {
            File.WriteAllText(Path.ChangeExtension(inkPath, ".json"), json);
            File.WriteAllText(Path.ChangeExtension(grandmaPath, ".json"), grandmaJson);
            Console.WriteLine("Compiled lobby and Grandma Ink JSON from authored source.");
        }
        else
            Check(StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.ChangeExtension(inkPath, ".json"))).Select(s => (s.Kind, s.Target, s.Variant, s.Visible, s.X, s.Y, s.Count, s.Seconds, s.Easing))
                .SequenceEqual(steps.Select(s => (s.Kind, s.Target, s.Variant, s.Visible, s.X, s.Y, s.Count, s.Seconds, s.Easing))), "Committed compiled Ink matches the source");
        Console.WriteLine($"Passed {checks} introduction checks.");
    }
}
