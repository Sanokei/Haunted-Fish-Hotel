using System;
using System.Reflection;
using HauntedFish.BossFight;
using UnityEngine;
using UnityEngine.UI;
class Program
{
 static int checks;
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
 static void Call(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
 static void Main()
 {
  Check(BossMatch.FishGoalsToWin==3&&BossMatch.GhostGoalsToWin==1,"production thresholds remain 3 ghost lives and 1 fish life");
  var match=new BossMatch();int completed=0;match.Completed+=result=>completed++;
  for(int round=0;round<50;round++)
  {
   Check(match.TryBegin(101,202),"authoritative begin resets lives");
   uint epoch=match.Epoch;
   Check(!match.Score(epoch-1,true),"stale goal cannot change lives");
   for(int goal=1;goal<=3;goal++)
   {
    Check(match.Score(epoch,true),"authoritative fish goal accepted");
    Check(BossLivesState.ForViewer(BossViewerSide.Fish,true,true,false,match.FishScore,match.GhostScore).Remaining==3-goal,"server score produces correct ghost head count");
   }
   Check(!match.Active&&completed==round*2+1,"third ghost life ends match exactly once");
   Check(!match.Score(epoch,true),"duplicate terminal goal cannot change lives");
   Check(match.TryBegin(101,202)&&match.Score(match.Epoch,false),"new match ghost goal accepted");
   Check(!match.Active&&completed==round*2+2&&BossLivesState.ForViewer(BossViewerSide.Ghost,true,true,false,match.FishScore,match.GhostScore).Remaining==0,"one fish life ends match exactly once");
  }
  for(int score=-2;score<6;score++)
  {
   var g=BossLivesState.ForViewer(BossViewerSide.Fish,true,true,false,score,99);
   var f=BossLivesState.ForViewer(BossViewerSide.Ghost,true,true,false,99,score);
   Check(g.Visible&&g.Remaining==Math.Max(0,Math.Min(3,3-score)),"ghost reflects opponent goals, bounded");
   Check(f.Visible&&f.Remaining==(score<=0?1:0),"fish reflects opponent goals, bounded");
  }
  foreach(var side in new[]{BossViewerSide.Ghost,BossViewerSide.Fish,BossViewerSide.Spectator})
  {
   Check(!BossLivesState.ForViewer(side,false,true,false,0,0).Visible,"outside watch hides");
   Check(!BossLivesState.ForViewer(side,true,false,false,0,0).Visible,"missing snapshot hides");
   Check(!BossLivesState.ForViewer(side,true,true,true,0,0).Visible,"cancelled match hides");
  }
  Check(!BossLivesState.ForViewer(BossViewerSide.Spectator,true,true,false,0,0).Visible,"spectator overview has no opponent life row");
  Check(!BossLivesState.ForViewer((BossViewerSide)99,true,true,false,0,0).Visible,"unknown side hides");
  var hud=new BossHudPresentation();var canvas=new Canvas();var fishRow=new GameObject();var ghostRow=new GameObject();var fish=new Image();var ghosts=new[]{new Image(),new Image(),new Image()};
  Set(hud,"_Canvas",canvas);Set(hud,"_FishRow",fishRow);Set(hud,"_GhostRow",ghostRow);Set(hud,"_FishLife",fish);Set(hud,"_GhostLives",ghosts);
  var state=new BossArenaSnapshot();
  for(int cycle=0;cycle<30;cycle++)
  {
   for(int score=0;score<=3;score++)
   {
    state.FishScore=score;state.GhostScore=0;hud.Present(state,BossViewerSide.Fish,true,false);
    Check(canvas.enabled&&ghostRow.activeSelf&&!fishRow.activeSelf&&!fish.enabled,"fish viewer sees ghost opponent row");
    for(int i=0;i<3;i++)Check(ghosts[i].enabled==(i<3-score),"exactly one head disappears per fish goal");
   }
   state.FishScore=0;hud.Present(state,BossViewerSide.Ghost,true,false);
   Check(canvas.enabled&&fishRow.activeSelf&&!ghostRow.activeSelf&&fish.enabled,"ghost viewer sees one fish opponent head");
   for(int i=0;i<3;i++)Check(!ghosts[i].enabled,"stale ghost icons cleared after role switch");
   state.GhostScore=1;hud.Present(state,BossViewerSide.Ghost,true,false);Check(canvas.enabled&&!fish.enabled,"terminal fish goal clears its head during reaction");
   hud.Present(state,BossViewerSide.Spectator,true,false);Check(!canvas.enabled&&!fishRow.activeSelf&&!ghostRow.activeSelf,"spectator transition hides both rows");
   state.GhostScore=state.FishScore=0;hud.Present(state,BossViewerSide.Fish,true,true);Check(canvas.enabled&&ghosts[2].enabled,"practice reset uses identical lives without a practice label");
   hud.Present(null,BossViewerSide.Fish,true,false);Check(!canvas.enabled,"snapshot loss clears stale HUD");
  }
  hud.Present(state,BossViewerSide.Ghost,true,false);Call(hud,"OnDisable");Check(!canvas.enabled,"disable clears canvas");
  // Missing authored references must not lead to fallback UI construction.
  new BossHudPresentation().Present(state,BossViewerSide.Fish,true,false);checks++;
  var safe=new BossHudSafeArea();var content=new RectTransform();Set(safe,"_Content",content);Call(safe,"OnEnable");
  Check(content.anchorMin.x==0&&content.anchorMax.x==1&&content.anchorMax.y==1,"full display safe area");
  Screen.width=720;Screen.height=1280;Screen.safeArea=new Rect(20,40,680,1200);Canvas.Render();
  Check(Math.Abs(content.anchorMin.x-20f/720)<.00001&&Math.Abs(content.anchorMax.y-1240f/1280)<.00001,"notched portrait safe area");
  Check(content.offsetMin.x==0&&content.offsetMax.y==0,"safe-area offsets cleared");
  var saved=content.anchorMin;Call(safe,"OnDisable");Screen.safeArea=new Rect(0,0,720,1280);Canvas.Render();Check(content.anchorMin.x==saved.x,"disabled safe-area releases event subscription");
  Call(safe,"OnEnable");Check(content.anchorMin.x==0,"re-enable refreshes safe area");
  Screen.width=0;Canvas.Render();Check(content.anchorMax.x==1,"zero display dimensions ignored");Call(safe,"OnDisable");
  Console.WriteLine(checks+" Boss life HUD production-rule/presenter/safe-area checks passed (Unity doubles; not native Unity).");
 }
}
