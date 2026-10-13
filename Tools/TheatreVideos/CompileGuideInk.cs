// Standalone check using the project's vendored Ink compiler/runtime, with no Unity dependency.
// Compile this file plus Assets/Plugins/Ink/InkLibs/{InkCompiler,InkRuntime}/**/*.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class CompileGuideInk
{
    static List<string> Operations(string json)
    {
        var story = new Ink.Runtime.Story(json);
        var steps = new List<string>();
        story.BindExternalFunction("TutorialCaption", (string heading, string body) => steps.Add("caption|" + heading + "|" + body));
        story.BindExternalFunction("TutorialShot", (string name, float seconds) => steps.Add("shot|" + name + "|" + seconds));
        story.BindExternalFunction("TutorialDemo", (string name, float seconds) => steps.Add("demo|" + name + "|" + seconds));
        story.BindExternalFunction("TutorialSetup", (string name, float seconds) => steps.Add("setup|" + name + "|" + seconds));
        story.BindExternalFunction("Wait", (float seconds) => steps.Add("wait|" + seconds));
        while (story.canContinue)
            if (!string.IsNullOrWhiteSpace(story.Continue())) throw new Exception("Recording Ink must use timed functions.");
        if (story.hasError || story.currentChoices.Count > 0) throw new Exception("Recording Ink cannot wait for choices.");
        return steps;
    }

    static int Main(string[] args)
    {
        bool write = args.Contains("--compile");
        foreach (var role in new[] { "Fish", "Ghost" })
        {
            string path = "Assets/Dialogue/TutorialRecording/" + role + "Recording.ink";
            var errors = new List<string>();
            var compiled = new Ink.Compiler(File.ReadAllText(path), new Ink.Compiler.Options {
                errorHandler = (message, type) => { if (type == Ink.ErrorType.Error) errors.Add(message); }
            }).Compile();
            if (compiled == null || errors.Count > 0) throw new Exception(string.Join("\n", errors));
            string json = compiled.ToJson(), jsonPath = Path.ChangeExtension(path, ".json");
            var steps = Operations(json);
            string opening = "caption|" + (role == "Fish" ? "Guide to all Fish" : "Guide to the Ghost") + "|";
            var captions = steps.Where(step => step.StartsWith("caption|")).ToArray();
            if (!captions[0].StartsWith(opening) || captions.Count(step => step.StartsWith(opening)) != 1)
                throw new Exception(role + " opening title must occur exactly once.");
            if (steps.Any(step => step.Contains("becomes ghost") || step.Contains("become ghost") || step.Contains("Shopping Cart")))
                throw new Exception(role + " contains obsolete trap/role-swap copy.");
            if (write) File.WriteAllText(jsonPath, json);
            if (!steps.SequenceEqual(Operations(File.ReadAllText(jsonPath))))
                throw new Exception(role + " compiled Ink does not match its authored source.");
            Console.WriteLine("PASS " + role + ": vendored Ink compile, execution and serialized parity; " + captions.Length + " captions.");
        }
        return 0;
    }
}
