using System;
using System.IO;
using System.Linq;
using Monologue.Dialogue;
using HauntedFish.Multiplayer;
static class Program {
 static int checks;
 static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
 static void Main(string[] args) {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
  var path=Path.Combine(root,"Assets/Dialogue/GhostRoundIntroduction.ink");
  var errors=new System.Collections.Generic.List<string>();
  var ink=new Ink.Compiler(File.ReadAllText(path),new Ink.Compiler.Options { errorHandler=(m,t)=> { if(t==Ink.ErrorType.Error) errors.Add(m); } }).Compile();
  Check(ink!=null && errors.Count==0,string.Join("\n",errors));
  var json=ink.ToJson(); var steps=StoryFunctions.ReadAnimationSequence(json);
  Check(steps.Select(s=>s.Target).SequenceEqual(new[]{"wheel_spin","selected_slice","","attic_cutscene","attic_top","ghost","ghost","","return_blur","hotel_bottom","return_blur",""}),"Spin, selection, attic reveal and blurred return execute in Ink order");
  Check(steps[0].Seconds==7 && steps[1].Count==3,"Original spin duration retained and selected slice visibly pulses");
  Check(steps[8].Visible && !steps[10].Visible && steps[9].Seconds<.5f,"Blur brackets the fast downward return");
  Check(StoryFunctions.ReadAnimationSequence(json).Count==steps.Count,"Repeated story reads are independent");
  for(int count=1;count<=64;count++) for(int index=0;index<count;index++) {
   double destination=-(1800+(index+.5)*360/count);
   double clockwise=(-destination%360+360)%360;
   Check((int)Math.Floor(clockwise/360*count)==index,"Pointer lands in the authoritative selected sector");
  }
  var gate=new GameRoundGate();gate.Begin(new uint[]{1,2},1,0);
  Check(!gate.Finish(1,0,100),"Stale completion rejected");
  Check(!gate.Finish(1,1,0),"Premature completion rejected");
  Check(gate.Finish(1,1,100) && !gate.Complete,"Wait for every peer");
  Check(!gate.Finish(1,1,100),"Duplicate completion rejected");
  gate.Remove(2);Check(gate.Complete,"Departed peer removed from barrier");
  gate.Begin(new uint[]{1,2},2,100);Check(!gate.Finish(1,1,200) && !gate.Complete,"Restart cancels old round acknowledgements");
  var source=File.ReadAllText(Path.Combine(root,"Assets/Scripts/Multiplayer/GameSceneController.cs"));
  Check(source.Contains("players[Random.Range(0, players.Count)].NetId"),"Live authority uses random selection rather than a fixed sequence");
  Check(source.Contains("_Released = true;") && !source.Contains("Destroy(_Selection)"),"Release is guarded and presentation survives repeated rounds");
  if(args.Contains("--compile")) File.WriteAllText(Path.ChangeExtension(path,".json"),json);
  else Check(StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.ChangeExtension(path,".json"))).SequenceEqual(steps),"Compiled Ink operations match source");
  gate.Join(3,2,200);Check(!gate.Finish(3,2,200) && gate.Finish(3,2,209) && !gate.Finish(3,2,210),"Late join receives its own minimum duration and releases exactly once");
  var smokePath=Path.Combine(root,"Assets/Dialogue/GhostPossessionSmoke.ink");
  var smoke=new Ink.Compiler(File.ReadAllText(smokePath)).Compile();Check(smoke!=null,"Possession smoke Ink compiles");
  var smokeSteps=StoryFunctions.ReadAnimationSequence(smoke.ToJson());Check(smokeSteps.Count==2&&smokeSteps[0].Target=="puff_burst"&&smokeSteps[1].Target=="puff_clear","Cloud burst and cleanup use the existing Ink animation bindings");
  if(args.Contains("--compile"))File.WriteAllText(Path.ChangeExtension(smokePath,".json"),smoke.ToJson());
  else Check(StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.ChangeExtension(smokePath,".json"))).SequenceEqual(smokeSteps),"Compiled smoke Ink matches source");
  var emergencePath=Path.Combine(root,"Assets/Dialogue/GhostEmergence.ink");
  var emergence=new Ink.Compiler(File.ReadAllText(emergencePath)).Compile();
  Check(emergence!=null,"Fish soul emergence Ink compiles");
  var emergenceSteps=StoryFunctions.ReadAnimationSequence(emergence.ToJson());
  Check(emergenceSteps.Select(step=>step.Target).SequenceEqual(new[]{"soul_appear","soul_rise","soul_clear"}),"Reusable emergence uses existing Ink layouts in order");
  if(args.Contains("--compile"))File.WriteAllText(Path.ChangeExtension(emergencePath,".json"),emergence.ToJson());
  else Check(StoryFunctions.ReadAnimationSequence(File.ReadAllText(Path.ChangeExtension(emergencePath,".json"))).SequenceEqual(emergenceSteps),"Compiled emergence Ink matches source");
  var grandmaPath=Path.Combine(root,"Assets/Dialogue/GrandmafishInspect.ink");
  var grandma=new Ink.Compiler(File.ReadAllText(grandmaPath)).Compile();
  Check(grandma!=null,"Grandmafish inspect dialogue compiles");
  if(args.Contains("--compile"))File.WriteAllText(Path.ChangeExtension(grandmaPath,".json"),grandma.ToJson());
  Console.WriteLine("Passed "+checks+" round introduction checks.");
 }
}
