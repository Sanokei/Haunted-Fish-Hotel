using System;
using HauntedFish.BossFight;
class Program
{
 static int checks;static void Check(bool b,string label){checks++;if(!b)throw new Exception(label);}
 static void Main()
 {
  for(uint epoch=1;epoch<=100;epoch++){
   var board=new BossDefenseState(epoch,101,202);var empty=Array.Empty<BossDefenseFootprint>();Check(!board.TryPlace(101,epoch,0,1,0,-2,empty),"ready gate");board.Ready=true;
   Check(!board.TryPlace(303,epoch,0,1,0,-2,empty),"spectator ownership");Check(!board.TryPlace(202,epoch,0,1,0,-2,empty),"foreign slot");Check(!board.TryPlace(101,epoch+1,0,1,0,-2,empty),"epoch gate");Check(!board.TryPlace(101,epoch,0,1,0,2,empty),"opponent half");Check(!board.TryPlace(101,epoch,0,1,3,-2,empty),"rails");Check(!board.TryPlace(101,epoch,0,1,0,-4,empty),"goal approach");Check(!board.TryPlace(101,epoch,0,1,0,-.4f,empty),"center exclusion");Check(!board.TryPlace(101,epoch,0,1,float.NaN,-2,empty),"finite");Check(!board.TryPlace(101,epoch,0,1,0,-2,null),"missing blocker evidence");
   Check(!board.TryPlace(101,epoch,0,1,0,-2,new[]{new BossDefenseFootprint(0,-2,.25f)}),"cue ball/paddle overlap");Check(board.TryPlace(101,epoch,0,1,0,-2,empty),"own placement");Check(!board.TryPlace(101,epoch,0,1,1,-2,empty),"stale request");Check(!board.TryPlace(101,epoch,1,1,0,-2,empty),"no stacking");Check(board.TryPlace(101,epoch,1,1,1.5f,-2,empty),"can placed");Check(board.TryPlace(101,epoch,2,1,-1.5f,-2,empty),"radio placed");Check(board.TryPlace(202,epoch,3,1,0,2,empty),"mirrored ghost half");
   Check(!board.Hit(epoch,0,1,false),"only real verified cue ball");Check(board.Hit(epoch,0,1,true)&&board.Slot(0).Broken,"glass one hit");Check(!board.Hit(epoch,0,2,true),"broken collider cannot hit");Check(!board.TryPlace(101,epoch,0,2,0,-2,empty),"broken cannot replenish");
   Check(board.Hit(epoch,1,2,true)&&board.Slot(1).Health==1,"can first crack");Check(!board.Hit(epoch,1,2,true),"no duplicate/stay damage");board.ContactEnded(1,2);Check(board.Hit(epoch,1,2,true)&&board.Slot(1).Broken,"new contact breaks can");
   Check(board.Hit(epoch,2,3,true)&&board.Slot(2).Health==2,"radio first crack");board.ContactEnded(2,3);Check(board.Hit(epoch,2,3,true)&&board.Slot(2).Health==1,"radio second crack");board.ServeStarted();Check(board.Slot(2).Health==1,"serve retains damage");Check(board.Hit(epoch,2,3,true)&&board.Slot(2).Broken,"third contact breaks radio");
   board.Reset(epoch+1,101,202);Check(board.Slot(0).Health==1&&board.Slot(1).Health==2&&board.Slot(2).Health==3&&!board.Slot(1).Placed,"new match resets racks and health");
  }
  var sweep=new BossDefenseState(1,1,2);sweep.Ready=true;var blockers=Array.Empty<BossDefenseFootprint>();
  Check(sweep.CanPlace(1,0,-1.5f,-2,blockers)&&!sweep.Slot(0).Placed,"preview never mutates rack");
  Check(sweep.TryPlace(1,1,0,1,-1.5f,-2,blockers),"swept source placement");
  var obstruction=new[]{new BossDefenseFootprint(0,-2,.3f)};
  Check(!sweep.CanPlace(1,0,1.5f,-2,obstruction)&&!sweep.TryPlace(1,1,0,2,1.5f,-2,obstruction),"preview and authority reject crossing dynamic cue-ball/paddle");
  Check(sweep.Slot(0).X==-1.5f&&sweep.Slot(0).LastSequence==1,"rejection preserves placement and sequence");
  Check(!sweep.ClearStrikerPath(-2.5f,-2,-.5f,-2,.48f),"physical striker cannot tunnel through placed defense");
  Check(sweep.ClearStrikerPath(-2.5f,-3,-.5f,-3,.48f),"striker can move on clear second axis");
  Check(!sweep.ClearStrikerPath(float.NaN,0,0,0,.48f),"invalid physical path rejected");
  Check(sweep.Observe(1,1,true,7,1.5f,-2)&&sweep.Slot(1).Health==1&&sweep.Slot(1).LastSequence==7,"replica retains authority health and sequence");
  Check(!sweep.Observe(6,1,true,1,0,0)&&!sweep.Observe(1,3,true,1,0,0),"malformed replica rejected");
  foreach(float fov in new[]{35f,45f,60f})foreach(float aspect in new[]{.5625f,1f,1.333f,1.778f})foreach(float right in new[]{0f,3f,6f})foreach(float up in new[]{3f,5f,7f})foreach(float depth in new[]{5f,12f})
  {
   Check(BossPerspectiveFraming.ProjectedPointDistance(right,up,depth,fov,aspect,.3f,out float distance),"union framing valid");double tan=Math.Tan(fov*Math.PI/360);Check(Math.Abs(right/((distance+depth)*tan*aspect))<=.92001&&Math.Abs(up/((distance+depth)*tan))<=.92001,"ghost projected corner within margin");Check(distance+depth>.3,"near depth");
  }
  Check(!BossPerspectiveFraming.ProjectedPointDistance(float.NaN,1,1,45,1,.3f,out _),"invalid framing point");
  Console.WriteLine(checks+" staged defense rules checks passed.");
 }
}