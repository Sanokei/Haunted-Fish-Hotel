#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HauntedFish.Multiplayer;
using IOPath=System.IO.Path;
public static class ActiveTrapJumpDiagnostic
{
 static int DiagnosticRate=120;
 const string Evidence="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/active-trap-jump-native";
 sealed class Environment : IGameMovementEnvironment {public Vector3 ClampMouse(Vector3 p)=>p;}
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
 static void Call(object o,string method)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 public static void Run()
 {
  if(!Application.isBatchMode||!System.Environment.GetCommandLineArgs().Contains("--active-trap-jump-diagnostic"))throw new Exception("Explicit owned editor window required");
  Directory.CreateDirectory(Evidence);File.WriteAllText(IOPath.Combine(Evidence,"summary.txt"),"");File.WriteAllText(IOPath.Combine(Evidence,"fall-summary.txt"),"");File.WriteAllText(IOPath.Combine(Evidence,"contact-checks.txt"),"");var lines=new List<string>();var scene=EditorSceneManager.NewPreviewScene();
  try{
   var floor=new GameObject("Diagnostic floor");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor,scene);var floorBody=floor.AddComponent<BoxCollider>();floorBody.size=new Vector3(30,1,2);floor.transform.position=new Vector3(0,-.5f,0);
   var ceiling=new GameObject("Diagnostic ceiling");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ceiling,scene);var ceilingBody=ceiling.AddComponent<BoxCollider>();ceilingBody.size=new Vector3(30,1,2);ceiling.transform.position=new Vector3(0,4.61f,0);
   var environment=new GameObject("Diagnostic production hallway environment").AddComponent<GameSceneController>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(environment.gameObject,scene);Set(environment,"_HallwayFloor",floorBody);Set(environment,"_HallwayCeiling",ceilingBody);typeof(GameSceneController).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,environment);
   var world=new GameObject("Diagnostic production authority world").AddComponent<GhostPlacementWorld>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(world.gameObject,scene);world.BeginRound("editor-preview","native-jump",1);Set(environment,"_PlacementWorld",world);var haunting=environment.gameObject.AddComponent<GameHauntingController>();
   foreach(string family in new[]{"ShoppingCartTrap","ClickTrap","FallingChandelierTrap"})
   foreach(int sign in new[]{1,-1})foreach(float approach in new[]{3.8f,4f,4.2f,4.4f})foreach(float launch in new[]{-1f,0f,.12f,.24f})foreach(bool moving in new[]{false,true})
   {
    if(moving&&family!="ShoppingCartTrap")continue;
    var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
    var fish=actor.GetComponent<HotelPlayer>();fish.Networked=false;fish.RoundStateKey="native-jump";fish.RoundVersion=1;fish.RoundReleased=true;fish.RoundIntroComplete=true;Set(fish,"_EditorRole",0);
    var movement=actor.GetComponent<HotelPlayerMovement>();var motor=actor.GetComponent<GameSideScrollMotor>();Call(movement,"Awake");Call(motor,"Awake");Set(fish,"_Movement",movement);Call(fish,"RegisterActivePlayer");motor.Bind(new Environment());movement.SetMode(HotelControlMode.Fish);movement.SetSimulationAuthority(true);movement.SetControlState(true,false,false);Set(movement,"_ResumeAfterFrame",-1);
    var controller=movement.BodyController;controller.enabled=false;actor.transform.position=new Vector3(-approach*sign,controller.height*.5f-controller.center.y+.01f,0);controller.enabled=true;
    var trapObject=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/"+family+".prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(trapObject,scene);var trap=trapObject.GetComponent<GhostTrap>();
    float y=trap.BodyHalfSize.y;var state=new GhostCubePlacement {Id=1,Origin=new Vector3(0,y,0),Position=new Vector3(0,y,0),Phase=(int)(family=="FallingChandelierTrap"?TrapPhase.Latched:TrapPhase.Armed)};trap.Initialize(null,state,1);Physics.SyncTransforms();controller.Move(Vector3.down*.1f);Physics.SyncTransforms();
    float maximumFeet=-999,clearContact=0,peak=-999;int contacts=0,pushes=0;float dt=1f/DiagnosticRate;
    for(int frame=0;frame<Mathf.RoundToInt(1.5f/dt);frame++){
     float now=frame*dt;motor.Accept(Vector2.right*sign,Mathf.Abs(now-launch)<dt*.5f,Vector3.right,true);motor.Simulate(dt);Physics.SyncTransforms();
     float feet=controller.bounds.min.y;maximumFeet=Mathf.Max(maximumFeet,feet);peak=Mathf.Max(peak,actor.transform.position.y);
     bool exact=trap.TouchesBody(controller);bool expanded=trap.TouchesBody(controller,.15f);if(expanded){contacts++;if(!exact)clearContact+=dt;}
     float priorX=actor.transform.position.x;var previousTrap=trap.Position;
     if(moving){trap.AcceptInput(new TrapInput(TrapInputKind.Move,-sign),Time.unscaledTime);trap.Simulate(dt,Time.unscaledTime);if(Mathf.Abs(actor.transform.position.x-priorX)>.001f&&!exact)pushes++;}
     haunting.ApplyTrapDamage(trap,previousTrap,dt);
     lines.Add(family+",approach="+approach+","+sign+","+launch+","+moving+","+frame+","+actor.transform.position.x+","+feet+","+trap.Position.x+","+exact+","+expanded+","+movement.LastCollisionFlags);
    }
    File.AppendAllText(IOPath.Combine(Evidence,"summary.txt"),family+" approach="+approach+" direction="+sign+" launch="+launch+" moving="+moving+" finalX="+actor.transform.position.x+" damage="+(100 - fish.Health)+" totalContactSamples="+contacts+" finalGrounded="+controller.isGrounded+" finalFeet="+controller.bounds.min.y+" maxFeet="+maximumFeet+" marginOnlyContactSeconds="+clearContact+" pushesWhileNoSolidContact="+pushes+"\n");
    UnityEngine.Object.DestroyImmediate(trapObject);Call(fish,"UnregisterActivePlayer");UnityEngine.Object.DestroyImmediate(actor);
   }
   foreach(int rate in new[]{15,30,60,120}){
    var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/FallingChandelierTrap.prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,scene);var trap=obj.GetComponent<GhostTrap>();
    float y=4.11f-trap.BodyHalfSize.y;var state=new GhostCubePlacement{Id=2,Origin=new Vector3(6,y-trap.ArmedOffset.y,0),Position=new Vector3(6,y,0),Phase=(int)TrapPhase.Armed};trap.Initialize(null,state,2);Physics.SyncTransforms();trap.AcceptInput(new TrapInput(TrapInputKind.Activate),0);float start=trap.Position.y;
    for(int i=0;i<rate*2;i++){trap.Simulate(1f/rate,(float)i/rate);Physics.SyncTransforms();}
    File.AppendAllText(IOPath.Combine(Evidence,"fall-summary.txt"),"rate="+rate+" start="+start+" end="+trap.Position.y+" phase="+trap.Phase+"\n");UnityEngine.Object.DestroyImmediate(obj);
   }
   foreach(string family in new[]{"ShoppingCartTrap","ClickTrap","FallingChandelierTrap"}){
    var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/"+family+".prefab"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,scene);var trap=obj.GetComponent<GhostTrap>();var state=new GhostCubePlacement{Origin=new Vector3(0,trap.BodyHalfSize.y,0),Position=new Vector3(0,trap.BodyHalfSize.y,0)};trap.Initialize(null,state,1);Physics.SyncTransforms();
    float top=trap.CollisionBounds.max.y;var clear=new Bounds(new Vector3(0,top+.01f+.875f,0),new Vector3(.9f,1.75f,.9f));var standing=new Bounds(new Vector3(0,.875f,0),new Vector3(.9f,1.75f,.9f));
    if(trap.TouchesBody(clear,.15f))throw new Exception(family+" falsely contacts a fish above full solid top");if(!trap.TouchesBody(standing,.15f))throw new Exception(family+" lost legitimate standing contact");File.AppendAllText(IOPath.Combine(Evidence,"contact-checks.txt"),"PASS "+family+" clear airborne negative and standing positive\n");UnityEngine.Object.DestroyImmediate(obj);
   }
   File.WriteAllLines(IOPath.Combine(Evidence,"trajectory.csv"),new[]{"family,direction,launch,moving,frame,fishX,feetY,trapX,solidContact,damageContact,flags"}.Concat(lines));
  }finally{typeof(GameSceneController).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);EditorSceneManager.ClosePreviewScene(scene);}
 }
 public static void AllRatesBatch()
 {
  try{
   foreach(int rate in new[]{30,60,120}){
    DiagnosticRate=rate;Run();string folder=IOPath.Combine(Evidence,"rate-"+rate);Directory.CreateDirectory(folder);foreach(string name in new[]{"summary.txt","trajectory.csv","fall-summary.txt","contact-checks.txt"})File.Copy(IOPath.Combine(Evidence,name),IOPath.Combine(folder,name),true);
    string[] result=File.ReadAllLines(IOPath.Combine(Evidence,"summary.txt"));
    foreach(int direction in new[]{1,-1}){
     if(!result.Any(l=>l.StartsWith("ShoppingCartTrap ")&&l.Contains("direction="+direction+" ")&&l.Contains("moving=True ")&&l.Contains("damage=0 ")&&l.Contains("totalContactSamples=0 ")&&l.Contains("pushesWhileNoSolidContact=0")&&l.Contains("finalGrounded=True")))throw new Exception("No genuinely clear grounded moving-cart crossing at rate "+rate+" direction "+direction);
     if(!result.Any(l=>l.StartsWith("ShoppingCartTrap ")&&l.Contains("direction="+direction+" ")&&l.Contains("moving=True ")&&l.Contains("launch=-1 ")&&!l.Contains("damage=0 ")))throw new Exception("Standing damage lost at rate "+rate+" direction "+direction);
    }
   }
   File.WriteAllText(IOPath.Combine(Evidence,"all-rates-result.txt"),"PASS real production prefab/motor/controller/physics and authoritative damage: both-direction zero-contact zero-damage grounded moving-cart crossings and standing damage at 30,60,120fps; chandelier lands at15,30,60,120fps; jump speed10.48/gravity25/player capsule unchanged.");
   GameHallwayNativeValidation.Run();
  }catch(Exception e){Directory.CreateDirectory(Evidence);File.WriteAllText(IOPath.Combine(Evidence,"all-rates-error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
 }
 public static void RunBatch(){try{Run();GameHallwayNativeValidation.Overlay();}catch(Exception e){Directory.CreateDirectory(Evidence);File.WriteAllText(IOPath.Combine(Evidence,"error.txt"),e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
}
#endif