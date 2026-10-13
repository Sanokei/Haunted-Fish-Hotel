using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEngine;
using UnityEngine.UI;

// Executes production StoryUI/ParallaxPlayer and blur lifecycle against narrow engine doubles.
// This is not a Unity renderer, shader test, or native lifecycle result.
static class HallwayPanTests
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static float Number(string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    static void Drain(IEnumerator routine) { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); }
    static void Main()
    {
        string root = Environment.GetEnvironmentVariable("HAUNTED_FISH_ROOT");
        string prefab = File.ReadAllText(Path.Combine(root, "Assets/Resources/AtticRoundCutscene.prefab"));
        string source = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Multiplayer/GhostSelectionPresentation.cs"));
        var steps = StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.Combine(root, "Assets/Dialogue/GhostRoundIntroduction.json")));
        var cameraSteps = steps.Where(s => s.Kind == StorySequenceStepKind.ParallaxTo).ToArray();
        Check(cameraSteps.Length == 4, "Ascent, accelerating descent, door cut and hallway return are authored camera steps");
        Check(source.Contains("step.Parent, step.Easing"), "Presentation retains Ink easing when scaling sequence time");
        Check(!source.Contains("IEnumerator Descend") && !source.Contains("hotel_bottom"), "Manager has no hard-coded motion or early-cut exit");
        Check(source.IndexOf("CloseAttic();\n            var assets") < source.IndexOf("            finished();"), "Full cinematic closes before client intro acknowledgment and ready page");
        Check(!source.Contains("new GameObject") && !source.Contains("AddComponent"), "Sequence manager does not build UI objects");
        var match = Regex.Match(prefab, @"--- !u!224 &121\n(?<block>.*?)(?=--- !u!)", RegexOptions.Singleline);
        var position = Regex.Match(match.Groups["block"].Value, @"m_AnchoredPosition: \{x: ([-\d.]+), y: ([-\d.]+)\}");
        var rest = new Vector2(Number(position.Groups[1].Value), Number(position.Groups[2].Value));
        var poses = Regex.Matches(prefab, @"- Tag: (attic_top|hotel_bottom|hallway_end|hallway_start)\n      Camera: \{x: ([-\d.]+), y: ([-\d.]+)\}\n      Push: ([-\d.]+)")
            .Cast<Match>().Select(m => new StoryParallaxRig.Pose { Tag=m.Groups[1].Value, Camera=new Vector2(Number(m.Groups[2].Value), Number(m.Groups[3].Value)), Push=Number(m.Groups[4].Value) }).ToArray();
        Check(poses.Length == 4 && poses.All(p => p.Push == 0), "Saved poses preserve fish and hallway scale");
        foreach (float fps in new[] { 24f, 30f, 60f, 144f })
        {
            Time.unscaledDeltaTime = 1 / fps;
            var strip = new RectTransform { anchoredPosition=rest };
            var rig = new StoryParallaxRig { Tag="hotel_camera", Layers=new[] { new StoryParallaxRig.Layer { Target=strip, Depth=1 } }, Poses=poses };
            var ui = new StoryUI();
            Set(ui,"_Content",new GameObject()); Set(ui,"_ParallaxRigs",new[] { rig });
            ui.ValidateSequence(cameraSteps); ui.Prepare();
            Drain(ui.Play(cameraSteps[0]));
            Check(Math.Abs(strip.anchoredPosition.y+2160)<.001f,"Actual StoryUI reaches attic at "+fps+" FPS");
            Drain(ui.Play(cameraSteps[1]));
            Check(Math.Abs(strip.anchoredPosition.y)<.001f,"Actual StoryUI reaches descent cut at "+fps+" FPS");
            Drain(ui.Play(cameraSteps[2]));
            Check(strip.anchoredPosition.x < -3500,"Zero-duration authored cut places the camera at hallway end");
            var play = ui.Play(cameraSteps[3]); play.MoveNext(); var motion = (IEnumerator)play.Current;
            float previous=strip.anchoredPosition.x;
            while(motion.MoveNext())
            {
                Check(strip.anchoredPosition.x>=previous,"Pan always moves end-to-start and never reverses");
                Check(strip.localScale.x==1 && strip.localScale.y==1,"Fish retain authored size throughout the pan");
                previous=strip.anchoredPosition.x;
            }
            play.MoveNext();
            Check(Math.Abs(strip.anchoredPosition.x-rest.x)<.001f,"Final pose exactly matches the original four-fish starting frame");
            ui.Close();
            for(int stepIndex=0;stepIndex<cameraSteps.Length;stepIndex++)
            {
                ui.Prepare(); for(int i=0;i<stepIndex;i++) Drain(ui.Play(cameraSteps[i]));
                var outer=ui.Play(cameraSteps[stepIndex]); outer.MoveNext(); var inner=(IEnumerator)outer.Current;
                inner.MoveNext(); inner.MoveNext();
                ui.Close(); Drain(inner); Drain(outer);
                Check(strip.anchoredPosition==rest && strip.localScale.x==1,"Skip/disable restores every partial shot and invalidates stale motion");
            }
            for(int replay=0;replay<5;replay++)
            { ui.Prepare(); foreach(var step in cameraSteps) Drain(ui.Play(step)); ui.Close(); Check(strip.anchoredPosition==rest,"Repeated rounds have no camera drift"); }
        }
        var a=new Graphic { material=new Material() }; var b=new Graphic { material=new Material() };
        var originalA=a.material; var originalB=b.material; var vertical=new Material(); var horizontal=new Material();
        var blur=new AtticReturnBlur(); Set(blur,"_Graphics",new[]{a,b}); Set(blur,"_Material",vertical);
        for(int pass=0;pass<30;pass++)
        {
            Call(blur,"OnEnable"); Check(a.material==vertical && b.material==vertical,"Enable applies only saved cinematic material");
            Call(blur,"OnEnable"); Call(blur,"OnDisable"); Call(blur,"OnDisable");
            Check(a.material==originalA && b.material==originalB,"Repeated enable/skip/disable restores exact prior materials");
        }
        Call(blur,"OnEnable"); Set(blur,"_Graphics",Array.Empty<Graphic>()); Call(blur,"OnDisable");
        Check(a.material==originalA && b.material==originalB,"Inspector/rebinding during playback cannot strand a blurred graphic");
        Set(blur,"_Graphics",new[]{a,b}); Set(blur,"_Material",horizontal); Call(blur,"OnEnable"); Call(blur,"OnDisable");
        Check(a.material==originalA && b.material==originalB,"Horizontal move restores the same unblurred state");
        Set(blur,"_Material",null); Call(blur,"OnEnable"); Call(blur,"OnDisable");
        Check(a.material==originalA && b.material==originalB,"Missing material leaves graphics unchanged");
        Check(!File.ReadAllText(Path.Combine(root,"Assets/Scripts/Multiplayer/AtticReturnBlur.cs")).Contains("Camera"),"Blur has no gameplay camera coupling");
        Console.WriteLine("Passed "+checks+" hallway cinematic playback, cancellation, replay, direction and blur-restore checks (engine doubles; no native render).");
    }
}
namespace UnityEngine { public class Material : Object { } }
namespace UnityEngine.UI { public class Graphic : UnityEngine.Object { public UnityEngine.Material material; } }
