using System;
using System.Collections;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
using Object=UnityEngine.Object;
public static class TrapManagerValidation
{
 static void Set(object value,string field,object data)=>value.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,data);
 public static IEnumerator Tests(Action<bool,string> check)
 {
  var cart=Resources.Load<GhostTrap>("Traps/ShoppingCartTrap");var click=Resources.Load<GhostTrap>("Traps/ClickTrap");var chandelier=Resources.Load<GhostTrap>("Traps/FallingChandelierTrap");
  var package=Resources.Load<GhostTrapSupply>("Traps/ConveyorPackage");check(package&&package.BoxArt&&package.BoxArt.name=="cardboardbox","Authored reusable package references actual cardboard box art");
  var host=new GameObject("Conveyor validation");host.SetActive(false);var world=host.AddComponent<GhostPlacementWorld>();var manager=host.AddComponent<TrapManager>();manager.enabled=false;host.SetActive(true);world.enabled=false;
  manager.Inject(world,null,package,Array.Empty<TrapDefinition>());check(manager.SelectWeighted(.5)==null&&world.Definition("cart")==null,"Empty catalog injects no traps and selects no packages");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=cart,Weight=0},new TrapDefinition{Prefab=click,Weight=-1},new TrapDefinition{Prefab=chandelier,Weight=float.NaN},new TrapDefinition{Weight=5}});
  check(manager.SelectWeighted(0)==null,"Zero, negative, NaN and missing-prefab weights are excluded");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=cart,Weight=float.PositiveInfinity},new TrapDefinition{Prefab=chandelier,Weight=1}});
  check(manager.SelectWeighted(0).Prefab==chandelier,"Infinite weights cannot monopolize the conveyor");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=cart,Weight=float.MaxValue},new TrapDefinition{Prefab=chandelier,Weight=float.MaxValue}});
  check(manager.SelectWeighted(.25).Prefab==cart&&manager.SelectWeighted(.75).Prefab==chandelier,"Double accumulation handles very large valid weights");
  check(manager.SelectWeighted(double.NaN)==null&&manager.SelectWeighted(double.PositiveInfinity)==null,"Invalid random samples are rejected");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=cart,Weight=5},new TrapDefinition{Prefab=click,Weight=2},new TrapDefinition{Prefab=chandelier,Weight=3}});
  var counts=new int[3];for(int i=0;i<10000;i++){var chosen=manager.SelectWeighted((i+.5)/10000);counts[chosen.Prefab==cart?0:chosen.Prefab==click?1:2]++;}
  check(counts[0]==5000&&counts[1]==2000&&counts[2]==3000,"Weights map exactly to 50/20/30 percent of the sample interval");
  var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(7381);Array.Clear(counts,0,3);
  for(int i=0;i<10000;i++){var chosen=manager.SelectWeighted(UnityEngine.Random.value);counts[chosen.Prefab==cart?0:chosen.Prefab==click?1:2]++;}UnityEngine.Random.state=randomState;
  check(Math.Abs(counts[0]-5000)<300&&Math.Abs(counts[1]-2000)<300&&Math.Abs(counts[2]-3000)<300,"Actual random weighted selections follow configured frequency");
  var custom=Object.Instantiate(cart);custom.gameObject.SetActive(false);Set(custom,"_FamilyTag","injected-designer-trap");Set(custom,"_Icon",null);
  var artwork=Resources.Load<Sprite>("cloud");manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=custom,DisplayArt=artwork,Weight=1}});
  check(world.Definition("injected-designer-trap")==custom,"Runtime injection accepts designer display art without requiring a prefab icon or conveyor type switches");
  var actor=new GameObject("Authority").AddComponent<HotelPlayer>();actor.gameObject.AddComponent<HotelPlayerMovement>().SetMode(HotelControlMode.Ghost);actor.ControlsReady=actor.RoundReleased=true;actor.RoundStateKey="conveyor-A";actor.RoundVersion=1;actor.NetId=42;
  world.BeginRound("editor-preview","conveyor-A",1);manager.RunPolicy=ConveyorRunPolicy.Manual;Set(manager,"_InitialPackages",1);manager.SpawnInterval=.5f;manager.Speed=2;
  manager.Simulate(actor,1);check(!manager.Running&&manager.PackageCount==0,"Manual policy remains stopped after release");
  check(manager.StartConveyor()&&manager.PackageCount==1,"Authority can start and seed authored packages");
  var first=manager.Package(0);check(first.FamilyTag==custom.FamilyTag&&first.DisplayArt==artwork,"Package art and family identity come from the same injected definition");
  var position=first.transform.position;manager.Simulate(actor,.25f);check(Mathf.Abs(first.transform.position.x-position.x-.5f)<.001f&&manager.PackageCount==1,"Speed advances packages while cadence waits");
  manager.Simulate(actor,.25f);check(manager.PackageCount==2,"Spawn interval emits a new weighted package");
  check(manager.StartConveyor()&&manager.PackageCount==2,"Repeated Start does not seed duplicate packages");
  manager.PauseConveyor();position=first.transform.position;manager.Simulate(actor,2);check(manager.Paused&&manager.Running&&first.transform.position==position&&manager.PackageCount==2,"Pause freezes motion, cadence and package identity");
  manager.ResumeConveyor();manager.Speed=4;manager.Simulate(actor,.1f);check(Mathf.Abs(first.transform.position.x-position.x-.4f)<.001f,"Resume applies edited speed");
  manager.Speed=float.NaN;manager.Simulate(actor,.1f);position=first.transform.position;manager.Simulate(actor,.1f);check(manager.Speed==0&&first.transform.position==position,"Invalid speed safely becomes stationary");
  manager.StopConveyor();manager.Simulate(actor,10);check(!manager.Running&&manager.PackageCount==2&&first.transform.position==position,"Stop freezes packages and suppresses automatic restart");
  check(!manager.TryTake(actor,0,position+Vector3.up*10,actor.RoundStateKey,1),"Package pickup validates authoritative proximity");
  actor.Networked=true;actor.IsServer=false;check(!manager.TryTake(actor,0,position,actor.RoundStateKey,1)&&!manager.StartConveyor(),"Observer cannot take, start or author packages");actor.Networked=false;
  string live=manager.Snapshot;
  var replicaHost=new GameObject("Conveyor replica");replicaHost.SetActive(false);var replica=replicaHost.AddComponent<TrapManager>();replica.enabled=false;replicaHost.SetActive(true);replica.Inject(world,null,package,new[]{new TrapDefinition{Prefab=custom,DisplayArt=artwork,Weight=1}});replica.BeginRound("editor-preview","conveyor-A",1);
  check(replica.ApplySnapshot(live)&&replica.PackageCount==2&&replica.Package(0).FamilyTag==first.FamilyTag&&replica.Package(0).DisplayArt==first.DisplayArt&&replica.Package(0).transform.position==first.transform.position,"Replica reconstructs identical package IDs, contents, art, positions and run state");
  check(manager.TryTake(actor,0,position,actor.RoundStateKey,1)&&actor.HeldTrapFamily==custom.FamilyTag&&manager.PackageCount==1,"Authority consumes actual package into held inventory");
  check(!manager.TryTake(actor,0,position,actor.RoundStateKey,1),"Repeated pickup cannot duplicate a consumed package");
  check(world.TryPlace(actor,new Vector3(8,1,0),custom.FamilyTag)&&world.Trap(0).FamilyTag==custom.FamilyTag,"Configured package contents place the injected trap prefab");
  check(replica.ApplySnapshot(manager.Snapshot)&&!replica.Package(0)&&!replica.ApplySnapshot(live),"Shared deletion removes consumed package and rejects stale revision");
  manager.ConfigureDefinitions(Array.Empty<TrapDefinition>());check(manager.PackageCount==0&&world.Count==1&&world.Definition(custom.FamilyTag)==custom,"Catalog removal clears supplies while retaining placed dependencies");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=chandelier,Weight=1}});world.BeginRound("editor-preview","conveyor-B",2);actor.RoundStateKey="conveyor-B";actor.RoundVersion=2;
  manager.RunPolicy=ConveyorRunPolicy.Periodic;Set(manager,"_StartDelay",1f);Set(manager,"_RunDuration",1f);Set(manager,"_PauseBetweenRuns",2f);manager.Speed=2;
  manager.Simulate(actor,.5f);check(!manager.Running&&manager.PackageCount==0,"New round resets packages and respects start delay");
  manager.Simulate(actor,.5f);check(manager.Running&&manager.PackageCount==1&&manager.Package(0).FamilyTag==chandelier.FamilyTag,"Delayed periodic run seeds configured chandelier");
  manager.Simulate(actor,1);check(!manager.Running,"Periodic run stops at configured duration");
  manager.Simulate(actor,1.9f);check(!manager.Running,"Periodic gap prevents premature restart");manager.Simulate(actor,.11f);check(manager.Running,"Periodic conveyor restarts after configured gap");
  manager.StopConveyor();manager.Simulate(actor,10);check(!manager.Running,"Explicit Stop overrides periodic restarts");
  check(!manager.TryTake(actor,0,Vector3.zero,"conveyor-A",1),"Old round pickup command cannot address reused package IDs");
  replica.BeginRound("different-room","conveyor-A",1);check(replica.PackageCount==0&&!replica.ApplySnapshot(live),"New session rejects old package snapshots even with the same round number");
  var beforeReset=JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot);int largest=-1;foreach(var record in beforeReset.Packages)largest=Math.Max(largest,record.Id);
  manager.ResetRound();manager.Simulate(actor,0);check(manager.StartConveyor(),"Same-round manager can restart after interruption");
  var restarted=JsonUtility.FromJson<ConveyorSnapshot>(manager.Snapshot);
  check(restarted.Packages.Count==1&&restarted.Packages[0].Id>largest&&restarted.Revision>beforeReset.Revision,"Same-round interruption never reuses IDs or rolls back shared revision");
  check(!manager.TryTake(actor,beforeReset.Packages[0].Id,Vector3.zero,actor.RoundStateKey,actor.RoundVersion),"Delayed pre-interruption pickup cannot consume a replacement package");
  manager.Speed=0;manager.SpawnInterval=float.NaN;Set(manager,"_MaxPackages",2);manager.Simulate(actor,1);
  check(manager.SpawnInterval==.05f&&manager.PackageCount==2,"Invalid cadence is safely bounded and maximum package count is enforced");
  manager.ResetRound();check(manager.PackageCount==0&&!manager.Running,"Reset disposes all packages and run state");
  Object.Destroy(custom.gameObject);Object.Destroy(actor.gameObject);Object.Destroy(host);Object.Destroy(replicaHost);yield return null;
 }
}
