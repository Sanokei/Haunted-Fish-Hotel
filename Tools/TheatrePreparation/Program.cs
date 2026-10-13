using System;
using HauntedFish.Theatre;
class Program
{
 static int checks; static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Main()
 {
  for(uint id=1;id<=100;id++){
   var v=new TheatreVisit();Check(!v.Begin(id,false,true,0),"ineligible");Check(!v.Begin(id,true,false,0),"outside");Check(v.Begin(id,true,true,0),"entry");var rev=v.Version;
   Check(!v.Begin(id+1,true,true,0),"duplicate load");Check(!v.Loaded(id+1,rev),"foreign load");Check(!v.Loaded(id,rev-1),"stale load");Check(v.Loaded(id,rev),"loaded");Check(!v.Exit(id+1,1),"foreign exit");Check(v.Exit(id,1),"own exit");Check(!v.Begin(id,true,true,3),"must leave doorway");v.ObserveDoor(false);Check(!v.Begin(id,true,true,1.1),"cooldown");Check(v.Begin(id,true,true,3),"rearmed");v.Cancel(3);Check(!v.Loaded(id,v.Version-1),"canceled callback");
  }
  for(int i=-100;i<=100;i++){float x=TheatreVisit.Move(0,i,.5f,-9,9,3);Check(x>=-1.5f&&x<=1.5f,"bounded input");Check(TheatreVisit.Move(8,1,100,-9,9,3)==9,"bound");}
  Check(TheatreVisit.Countdown(0)==3&&TheatreVisit.Countdown(1)==2&&TheatreVisit.Countdown(2)==1&&TheatreVisit.Countdown(3)==0,"countdown");
  float held=0;for(int frame=0;frame<180;frame++)held=TheatreVisit.Move(held,-1,1f/60,-9,9,3);Check(held<=-8.5f,"held A reaches far-left exit");
  Check(TheatreVisit.Move(float.NaN,1,1,-9,9,3)==-9,"invalid position fails safely");Check(TheatreVisit.Move(0,float.PositiveInfinity,1,-9,9,3)==-9,"invalid axis fails safely");
  for(uint owner=1;owner<=100;owner++)
  {
   var state=new TheatreReturnState();Check(state.Begin(owner,7,0,8),"return begin");Check(!state.Begin(owner,7,0,8),"duplicate return blocked");
   Check(!state.Ready(owner,7,true),"position alone does not release before ack");Check(!state.Acknowledge(owner+1,7,true),"foreign ack ignored");Check(!state.Acknowledge(owner,6,true),"stale ack ignored");
   Check(state.Acknowledge(owner,7,true),"owned ack");Check(!state.Ready(owner,7,false),"ack alone waits for replicated outside position");Check(state.Ready(owner,7,true),"ack plus authoritative position releases");
   state.Cancel();Check(!state.Waiting&&!state.Acknowledge(owner,7,true),"cancel/disconnect releases and ignores late ack");
   Check(state.Begin(owner,9,10,8),"repeated return");Check(!state.Expired(17.9)&&state.Expired(18),"bounded latency timeout");Check(!state.Acknowledge(owner,7,true),"prior return cannot release new visit");
   state.Cancel();Check(state.Begin(owner,10,20,8),"post timeout recover");Check(state.Acknowledge(owner,10,false)&&!state.Waiting,"explicit rejection releases state");
   var visit=new TheatreVisit();Check(visit.Begin(owner,true,true,0)&&visit.Loaded(owner,visit.Version),"visit active during return");Check(!visit.Begin(owner,true,true,9),"latency blocks reentry");visit.Cancel(9);Check(!visit.Begin(owner,true,true,20),"still must leave door after failure");visit.ObserveDoor(false);Check(visit.Begin(owner,true,true,20),"reenter after failure and outside");
  }
  Console.WriteLine(checks+" theatre visit/input regression checks passed.");
 }
}