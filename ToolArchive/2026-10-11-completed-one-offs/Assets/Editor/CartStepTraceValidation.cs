using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HauntedFish.Multiplayer;
public static class CartStepTraceValidation {
 const string Output="Tools/PlayerTests/Evidence/2026-10-10-jump/native-step-trace-v2.txt";
  static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static float Vertical(GameSideScrollMotor m)=>(float)typeof(GameSideScrollMotor).GetField("_Vertical",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m);
 sealed class Environment:IGameMovementEnvironment {public Vector3 ClampMouse(Vector3 p)=>p;}
 [MenuItem("Tools/Haunted Hotel/Trace cart-top jump contacts")]
 public static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists(Output))return;var lines=new List<string>();var previous=SceneManager.GetActiveScene();bool dirty=previous.isDirty;Scene scene=default;
 var current=typeof(GameSceneController).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);var oldGame=current.GetValue(null);
 try{
  scene=EditorSceneManager.NewPreviewScene();current.SetValue(null,null);
  GameObject Body(string name,Vector3 pos,Vector3 size){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=pos;go.AddComponent<BoxCollider>().size=size;return go;}
  Body("floor",new Vector3(0,-.5f,0),new Vector3(100,1,10));Body("ceiling",new Vector3(0,4.61f,0),new Vector3(100,1,10));
  var cart=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/ShoppingCartTrap.prefab"));SceneManager.MoveGameObjectToScene(cart,scene);cart.transform.position=new Vector3(0,1,0);var trap=cart.GetComponent<GhostTrap>();
  var solids=new List<BoxCollider>();foreach(var part in cart.GetComponentsInChildren<BoxCollider>(true))if(part.enabled&&!part.isTrigger)solids.Add(part);
  void PlaceCart(float x){cart.transform.position=new Vector3(x,1,0);foreach(var part in solids){part.enabled=false;part.enabled=true;}Physics.SyncTransforms();}
  var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab");var body=source.GetComponent<CharacterController>();var authoredMotor=source.GetComponent<GameSideScrollMotor>();
  var actor=new GameObject("capsule fixture");SceneManager.MoveGameObjectToScene(actor,scene);var cc=actor.AddComponent<CharacterController>();cc.height=body.height;cc.radius=body.radius;cc.center=body.center;cc.skinWidth=body.skinWidth;cc.stepOffset=body.stepOffset;cc.slopeLimit=body.slopeLimit;cc.minMoveDistance=body.minMoveDistance;
  var movement=actor.AddComponent<HotelPlayerMovement>();var motor=actor.GetComponent<GameSideScrollMotor>();Set(motor,"_Movement",movement);Set(motor,"_Controller",cc);Set(movement,"_Controller",cc);Set(movement,"_GameMotor",motor);motor.Bind(new Environment());movement.SetMode(HotelControlMode.Fish);movement.SetSimulationAuthority(true);movement.SetControlState(true,false,false);Set(movement,"_ResumeAfterFrame",-1);
  float ground=cc.height*.5f-cc.center.y, top=4.11f-cc.height*.5f-cc.center.y;var physics=scene.GetPhysicsScene();if(physics.Equals(Physics.defaultPhysicsScene))throw new Exception("Preview has no isolated physics; default scene was not simulated.");PlaceCart(0);physics.Simulate(1f/60);int checks=0;
  void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
  Check(physics.Raycast(new Vector3(0,3,0),Vector3.down,out var basketHit,3)&&basketHit.collider.transform.IsChildOf(cart.transform)&&Mathf.Abs(basketHit.point.y-1.7f)<.005f,"Native physics ray confirms basket solid at1.7");
  Check(physics.Raycast(new Vector3(.77f,3,0),Vector3.down,out var handleHit,3)&&handleHit.collider.transform.IsChildOf(cart.transform)&&Mathf.Abs(handleHit.point.y-2f)<.005f,"Native physics ray confirms handle solid at2.0");
  lines.Add($"PHYSICS basket={basketHit.point.y:F3} handle={handleHit.point.y:F3} solidParts={solids.Count}");
  Check(trap.BodyHalfSize==new Vector3(1.25f,1,.5f),"Full placement footprint retained");Check(trap.TouchesBody(new Bounds(new Vector3(-.6f,1,0),Vector3.one*.1f)),"Basket is solid");Check(trap.TouchesBody(new Bounds(new Vector3(.9f,1.9f,0),Vector3.one*.1f)),"Handle is solid");Check(!trap.TouchesBody(new Bounds(new Vector3(-.6f,1.9f,0),Vector3.one*.1f)),"Empty space above basket has no invisible collision");
  foreach(float step in new[]{.3f,.35f,.4f,.45f})foreach(float jump in new[]{10.48f,11f})foreach(float speed in new[]{1.8f,4.5f})foreach(float direction in new[]{1f,-1f})foreach(float toward in new[]{0f}){
   cc.stepOffset=step;
   int successes=0,attempts=0;float peak=0;bool anyCeiling=false,anyLanding=false;
   for(float launch=1.8f;launch<=12f;launch+=.1f){attempts++;cc.enabled=false;actor.transform.position=new Vector3(-launch*direction,ground,0);cc.enabled=true;PlaceCart(0);Physics.SyncTransforms();physics.Simulate(1f/60);cc.Move(Vector3.down*.2f);bool grounded=cc.isGrounded;motor.ResetMotion();motor.JumpSpeed=jump;movement.Gravity=source.GetComponent<HotelPlayerMovement>().Gravity;bool crossed=false,landed=false,onCart=false;bool run=speed>2;
    for(int i=0;i<300;i++){float dt=1f/60;var cp=cart.transform.position;cp.x=Mathf.Clamp(cp.x-direction*toward*dt,-3.5f,3.5f);PlaceCart(cp.x);Physics.SyncTransforms();physics.Simulate(dt);float vBefore=Vertical(motor);bool airborne=!cc.isGrounded;movement.AcceptInput(Vector2.right*direction,i==0||i==10,Vector3.zero,run);movement.Simulate(dt);var p=actor.transform.position;p.y=Mathf.Clamp(p.y,ground,top);actor.transform.position=p;peak=Mathf.Max(peak,p.y-ground);if(cc.isGrounded&&p.y>ground+.5f)onCart=true;
     if(speed>2&&(Mathf.Abs(launch-2f)<.051f||Mathf.Abs(launch-2.5f)<.051f||Mathf.Abs(launch-3f)<.051f)&&(step==.3f||step==.4f)&&i%10==0)lines.Add($"TRACE step={step} jump={jump} dir={direction} launch={launch:F2} frame={i} x={(p.x-cart.transform.position.x):F3} feet={(p.y-ground):F3} grounded={cc.isGrounded} flags={movement.LastCollisionFlags} vertical={Vertical(motor):F3} cartTopContact={onCart}");
     if(i==10&&airborne)Check(Vertical(motor)<=vBefore,"Airborne re-press cannot double jump");if((movement.LastCollisionFlags&CollisionFlags.Above)!=0){anyCeiling=true;Check(Vertical(motor)<=0,"Ceiling collision cancels upward velocity");}
     if((p.x-cart.transform.position.x)*direction>1.25f+cc.radius)crossed=true;if(i>5&&cc.isGrounded&&p.y<ground+.15f){landed=true;anyLanding=true;break;}
    }
    if(grounded&&crossed&&landed)successes++;
   }
   lines.Add($"step={step} jump={jump} gravity={source.GetComponent<HotelPlayerMovement>().Gravity} speed={speed} direction={direction} approaching={toward}: {successes}/{attempts} native crossings; apex={peak:F3}; ceiling={anyCeiling}; floorLanding={anyLanding}");
   
  }
  movement.SetControlState(false,false,false);var before=actor.transform.position;movement.AcceptInput(Vector2.right,true,Vector3.zero,true);movement.Simulate(.1f);Check(actor.transform.position==before,"Readiness blocks movement/jump");movement.SetControlState(true,true,true);before=actor.transform.position;movement.ReadOwnedInput(out var axis,out var inputJump,out _);Check(axis==Vector2.zero&&!inputJump,"Menu blocks owned jump input");
  SceneManager.SetActiveScene(previous);Check(previous.isDirty==dirty,"Main scene dirty state preserved");lines.Add($"PASS {checks} fixture safety/gate checks; crossing counts above are diagnostic, not a pass assertion; native edit-mode checks using production movement/motor and authored cart solid parts. Not Play Mode, input-device, peer synchronization, or human acceptance.");
 }catch(Exception e){lines.Add("FAIL "+e);}finally{current.SetValue(null,oldGame);if(previous.IsValid())SceneManager.SetActiveScene(previous);if(scene.IsValid()){foreach(var go in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}}
 Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Output));File.WriteAllLines(Output,lines);Debug.Log("JUMP NATIVE: "+Output);
 }
}
