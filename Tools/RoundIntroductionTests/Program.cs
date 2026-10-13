using System;
using System.IO;
using System.Linq;
using Monologue.Dialogue;
using HauntedFish.Multiplayer;
static class Program {
 static int checks;
 static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
 static void Main(string[] args) {
  var root=Environment.GetEnvironmentVariable("HAUNTED_FISH_ROOT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
  var path=Path.Combine(root,"Assets/Dialogue/GhostRoundIntroduction.ink");
  var errors=new System.Collections.Generic.List<string>();
  var ink=new Ink.Compiler(File.ReadAllText(path),new Ink.Compiler.Options { errorHandler=(m,t)=> { if(t==Ink.ErrorType.Error) errors.Add(m); } }).Compile();
  Check(ink!=null && errors.Count==0,string.Join("\n",errors));
  var json=ink.ToJson(); var steps=StoryFunctions.ReadAnimationSequence(json);
  Check(steps.Select(s=>s.Target).SequenceEqual(new[]{"wheel_spin","selected_slice","","attic_cutscene","","hotel_camera","ghost","ghost","","upward_streaks","return_blur","hotel_camera","transition_flash","return_blur","upward_streaks","ghost","hotel_camera","","","hallway_blur","hotel_camera","hallway_blur",""}),"Complete wheel, attic, downward cut, door reveal and hallway pan execute in Ink order");
  Check(steps[0].Seconds==7 && steps[1].Count==3,"Original spin duration retained and selected slice visibly pulses");
  Check(steps[10].Visible && !steps[13].Visible && steps[11].Seconds<.5f && steps[11].Easing=="ease_in","Blur brackets the accelerating downward return");
  Check(steps[16].Variant=="hallway_end" && steps[16].Seconds==0 && steps[17].Kind==StorySequenceStepKind.FlashFrames && steps[17].Count==2,"A two-frame flash covers the cut to the saved end-door pose");
  Check(steps[19].Visible && steps[20].Variant=="hallway_start" && steps[20].Seconds==.85f && !steps[21].Visible && steps[22].Seconds==1,"Horizontal blur only brackets the fast return; the four starting fish receive a clear landing beat");
  Check(steps.All(s=>s.Kind!=StorySequenceStepKind.CreateUI),"The round sequence never creates UI objects");
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
  if(args.Contains("--compile") || args.Contains("--compile-round")) File.WriteAllText(Path.ChangeExtension(path,".json"),json);
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
  PresentationChecks.Run();
  HallwayStagingChecks.Run();
  ReadyUpChecks.Run();
  StormEntryChecks.Run();
  Console.WriteLine("Passed "+checks+" round introduction checks.");
 }
}
