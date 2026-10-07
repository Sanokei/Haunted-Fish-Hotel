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
            StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.SetVariant, StorySequenceStepKind.Wait, StorySequenceStepKind.SetVariant,
            StorySequenceStepKind.Wait, StorySequenceStepKind.SetVisible, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.Knock, StorySequenceStepKind.Wait, StorySequenceStepKind.PushTo,
            StorySequenceStepKind.MoveUI, StorySequenceStepKind.Wait, StorySequenceStepKind.SetVisible,
            StorySequenceStepKind.PushTo, StorySequenceStepKind.Wait, StorySequenceStepKind.ExpandPanel,
            StorySequenceStepKind.Wait, StorySequenceStepKind.FlashFrames, StorySequenceStepKind.DollyTo,
            StorySequenceStepKind.Wait }), "Authored storyboard uses explicit typed story functions in the required order");
        var variants = steps.Where(step => step.Kind == StorySequenceStepKind.SetVariant).ToArray();
        Check(variants[0].Target == "cloud" && variants[0].Variant == "cloud" && variants[1].Variant == "cloud_lightning",
            "The still cloud changes to its lightning variant");
        var knocks = steps.Single(step => step.Kind == StorySequenceStepKind.Knock);
        Check(knocks.Target == "door" && knocks.Count == 3 && knocks.Seconds == .45f, "Ink specifies the door, knock count and interval");
        var move = steps.Single(step => step.Kind == StorySequenceStepKind.MoveUI);
        Check(move.Target == "letter" && move.X == 40 && move.Y == 50 && move.Seconds == 1.8f, "Ink controls the diagonal ground slide and duration");
        Check(steps.Where(step => step.Kind == StorySequenceStepKind.PushTo).Select(step => step.Target)
            .SequenceEqual(new[] { "letter_under_door", "letter_read" }), "Both pushes address panels on the same strip");
        Check(steps[14].Target == "letter_read" && steps[14].Visible && steps[15].Kind == StorySequenceStepKind.PushTo,
            "The incoming reading panel is visible before the second push");
        Check(steps[16].Kind == StorySequenceStepKind.Wait && steps[16].Seconds == 5, "Reading shot lingers before expansion");
        Check(steps[17].Kind == StorySequenceStepKind.ExpandPanel && steps[17].Seconds == 4,
            "The reading rhombus slowly takes over the frame");
        Check(steps[19].Kind == StorySequenceStepKind.FlashFrames && steps[19].Count == 2,
            "Ink requests exactly two white rendered frames");
        Check(steps[20].Kind == StorySequenceStepKind.DollyTo && steps[20].Target == "gate_close" && steps[20].Seconds == 5,
            "The flash is followed by an exterior dolly");
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
            Console.WriteLine("Compiled LobbyIntroduction.json from the authored Ink source.");
        }
        else
            Check(StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.ChangeExtension(inkPath, ".json"))).Select(s => (s.Kind, s.Target, s.Variant, s.Visible, s.X, s.Y, s.Count, s.Seconds))
                .SequenceEqual(steps.Select(s => (s.Kind, s.Target, s.Variant, s.Visible, s.X, s.Y, s.Count, s.Seconds))), "Committed compiled Ink matches the source");
        Console.WriteLine($"Passed {checks} introduction checks.");
    }
}
