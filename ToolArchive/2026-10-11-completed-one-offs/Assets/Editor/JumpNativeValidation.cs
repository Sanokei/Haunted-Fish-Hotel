using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HauntedFish.Multiplayer;
public static class JumpNativeValidation {
    static void RequireIdleEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Requires idle Edit mode outside prefab editing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save or discard scene changes before explicitly running this tool.");
    }

 const string Output="Tools/PlayerTests/Evidence/2026-10-10-jump/native-final-cart.txt";
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static float Vertical(GameSideScrollMotor m)=>(float)typeof(GameSideScrollMotor).GetField("_Vertical",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m);
 sealed class Environment:IGameMovementEnvironment {public Vector3 ClampMouse(Vector3 p)=>p;}
 [MenuItem("Tools/Haunted Hotel/Validate native cart jumps")]
 public static void Run(){ RequireIdleEditMode();if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists(Output))return;var lines=new List<string>();var previous=SceneManager.GetActiveScene();bool dirty=previous.isDirty;Scene scene=default;UnityEngine.Object ownedActions=null;HotelPlayer damageFish=null;List<HotelPlayer> registry=null;
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
  var actions=UnityEngine.Object.Instantiate(source.GetComponent<HotelPlayerMovement>().InputActions);ownedActions=actions;
  var gameMap=actions.FindActionMap("Game",true);var lobbyMap=actions.FindActionMap("Lobby",true);var bossMap=actions.FindActionMap("BossFight",true);
  Set(movement,"_OwnedActions",actions);Set(movement,"_GameMap",gameMap);Set(movement,"_LobbyMap",lobbyMap);Set(movement,"_BossMap",bossMap);
  Set(movement,"_GameMove",gameMap.FindAction("Move",true));Set(movement,"_GameJump",gameMap.FindAction("Jump",true));Set(movement,"_GameRun",gameMap.FindAction("Run",true));Set(movement,"_GameInteract",gameMap.FindAction("Interact",true));Set(movement,"_LobbyMove",lobbyMap.FindAction("Move",true));Set(movement,"_BossMove",bossMap.FindAction("Move",true));
  float ground=cc.height*.5f-cc.center.y, top=4.11f-cc.height*.5f-cc.center.y;var physics=scene.GetPhysicsScene();if(physics.Equals(Physics.defaultPhysicsScene))throw new Exception("Preview has no isolated physics; default scene was not simulated.");PlaceCart(0);physics.Simulate(1f/60);int checks=0;
  void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
  Check(physics.Raycast(new Vector3(0,3,0),Vector3.down,out var basketHit,3)&&basketHit.collider.transform.IsChildOf(cart.transform)&&Mathf.Abs(basketHit.point.y-1.7f)<.005f,"Native physics ray confirms basket solid at1.7");
  Check(physics.Raycast(new Vector3(.77f,3,0),Vector3.down,out var handleHit,3)&&handleHit.collider.transform.IsChildOf(cart.transform)&&Mathf.Abs(handleHit.point.y-2f)<.005f,"Native physics ray confirms handle solid at2.0");
  lines.Add($"PHYSICS basket={basketHit.point.y:F3} handle={handleHit.point.y:F3} solidParts={solids.Count}");
  Check(trap.BodyHalfSize==new Vector3(1.25f,1,.5f),"Full placement footprint retained");Check(trap.TouchesBody(new Bounds(new Vector3(-.6f,1,0),Vector3.one*.1f)),"Basket is solid");Check(trap.TouchesBody(new Bounds(new Vector3(.9f,1.9f,0),Vector3.one*.1f)),"Handle is solid");Check(!trap.TouchesBody(new Bounds(new Vector3(-.6f,1.9f,0),Vector3.one*.1f)),"Empty space above basket has no invisible collision");
  foreach(float jump in new[]{7f,authoredMotor.JumpSpeed,20f})foreach(float speed in new[]{1.8f,4.5f})foreach(float direction in new[]{1f,-1f})foreach(float toward in new[]{0f,5f}){
   int successes=0,attempts=0;var wins=new List<float>();float peak=0;bool anyCeiling=false,anyLanding=false;
   for(float launch=1.8f;launch<=12f;launch+=.1f){attempts++;cc.enabled=false;actor.transform.position=new Vector3(-launch*direction,ground,0);cc.enabled=true;PlaceCart(0);Physics.SyncTransforms();physics.Simulate(1f/60);cc.Move(Vector3.down*.2f);bool grounded=cc.isGrounded;motor.ResetMotion();motor.JumpSpeed=jump;movement.Gravity=source.GetComponent<HotelPlayerMovement>().Gravity;bool crossed=false,landed=false;bool run=speed>2;
    for(int i=0;i<300;i++){float dt=1f/60;var cp=cart.transform.position;cp.x=Mathf.Clamp(cp.x-direction*toward*dt,-3.5f,3.5f);PlaceCart(cp.x);Physics.SyncTransforms();physics.Simulate(dt);float vBefore=Vertical(motor);bool airborne=!cc.isGrounded;movement.AcceptInput(Vector2.right*direction,i==0||i==10,Vector3.zero,run);movement.Simulate(dt);var p=actor.transform.position;p.y=Mathf.Clamp(p.y,ground,top);actor.transform.position=p;peak=Mathf.Max(peak,p.y-ground);
     if(i==10&&airborne)Check(Vertical(motor)<=vBefore,"Airborne re-press cannot double jump");if((movement.LastCollisionFlags&CollisionFlags.Above)!=0){anyCeiling=true;Check(Vertical(motor)<=0,"Ceiling collision cancels upward velocity");}
     if((p.x-cart.transform.position.x)*direction>1.25f+cc.radius)crossed=true;if(i>5&&cc.isGrounded&&p.y<ground+.15f){landed=true;anyLanding=true;break;}
    }
    if(grounded&&crossed&&landed){successes++;wins.Add(launch);}
   }
   float longest=0,start=0,last=0;for(int w=0;w<wins.Count;w++){if(w==0||wins[w]-last>.11f)start=wins[w];last=wins[w];longest=Mathf.Max(longest,last-start);}
   lines.Add($"WINDOW jump={jump} speed={speed} direction={direction} approaching={toward}: min={(wins.Count>0?wins[0]:-1):F2} max={(wins.Count>0?wins[wins.Count-1]:-1):F2} longest={longest:F2}");
   lines.Add($"jump={jump} gravity={source.GetComponent<HotelPlayerMovement>().Gravity} speed={speed} direction={direction} approaching={toward}: {successes}/{attempts} native crossings; apex={peak:F3}; ceiling={anyCeiling}; floorLanding={anyLanding}");
   if(jump==authoredMotor.JumpSpeed)Check(successes>=5,"Native final walk/run crossing has multiple valid launch positions");if(jump==20f)Check(anyCeiling&&anyLanding,"Ceiling stress falls and lands");
  }
  movement.SetControlState(false,false,false);var before=actor.transform.position;movement.AcceptInput(Vector2.right,true,Vector3.zero,true);movement.Simulate(.1f);Check(actor.transform.position==before,"Readiness blocks movement/jump");movement.SetControlState(true,true,false);Check(movement.ActiveInputMap=="Game","Real authored Game map enables for local fish");movement.SetControlState(true,true,true);Check(movement.ActiveInputMap==""&&!gameMap.enabled,"Menu disables real owned Game map");before=actor.transform.position;movement.ReadOwnedInput(out var axis,out var inputJump,out _);Check(axis==Vector2.zero&&!inputJump,"Menu blocks owned jump input");
  movement.SetControlState(true,false,false);Set(movement,"_ResumeAfterFrame",-1);PlaceCart(0);cc.enabled=false;actor.transform.position=new Vector3(-10,ground,0);cc.enabled=true;physics.Simulate(1f/60);cc.Move(Vector3.down*.2f);motor.ResetMotion();movement.SharedDialogueLocked=true;before=actor.transform.position;movement.AcceptInput(Vector2.right,true,Vector3.zero,true);movement.Simulate(.1f);Check(Mathf.Abs(actor.transform.position.x-before.x)<.001f,"Shared dialogue blocks queued horizontal input");movement.SharedDialogueLocked=false;
  motor.ResetMotion();movement.CanMove=false;before=actor.transform.position;movement.AcceptInput(Vector2.right,true,Vector3.zero,true);movement.Simulate(.1f);Check(Mathf.Abs(actor.transform.position.x-before.x)<.001f&&actor.transform.position.y<=before.y+.01f,"CanMove/round lock blocks jump and horizontal input");movement.CanMove=true;
  var rules=new GameObject("scoped contact rules");SceneManager.MoveGameObjectToScene(rules,scene);var scopedScene=rules.AddComponent<GameSceneController>();var world=rules.AddComponent<GhostPlacementWorld>();var haunt=rules.AddComponent<GameHauntingController>();Set(scopedScene,"_PlacementWorld",world);current.SetValue(null,scopedScene);world.BeginRound("editor-preview","cart-native-final",1);
  damageFish=actor.AddComponent<HotelPlayer>();damageFish.Networked=false;Set(damageFish,"_Movement",movement);Set(damageFish,"_EditorRole",0);damageFish.RoundStateKey="cart-native-final";damageFish.RoundReleased=damageFish.RoundIntroComplete=true;
  registry=(List<HotelPlayer>)typeof(HotelPlayer).GetField("_ActivePlayers",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);if(!registry.Contains(damageFish))registry.Add(damageFish);
  float rate=50;var rateMatch=System.Text.RegularExpressions.Regex.Match(File.ReadAllText("Assets/Scenes/Game.unity"),@"(?m)^  _ContactSpookPerSecond: ([0-9.]+)");if(rateMatch.Success)rate=float.Parse(rateMatch.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);Set(haunt,"_ContactSpookPerSecond",rate);
  cc.enabled=false;actor.transform.position=new Vector3(.8f,1.1f,0);cc.enabled=true;physics.Simulate(1f/60);damageFish.Spook=0;Check(solids.Count==2&&solids[0].bounds.Intersects(cc.bounds)&&solids[1].bounds.Intersects(cc.bounds),"Damage fixture contacts both authored solid parts");haunt.ApplyTrapSpook(trap,trap.Position-Vector3.right*.1f,.1f);Check(Mathf.Abs(damageFish.Spook-rate*.1f)<.001f,"Two simultaneous solid contacts apply exactly one spook increment");float damage=damageFish.Spook;haunt.ApplyTrapSpook(trap,trap.Position,.1f);Check(damageFish.Spook==damage,"Stationary armed cart adds no moving-contact spook");damageFish.RoundReleased=false;haunt.ApplyTrapSpook(trap,trap.Position-Vector3.right*.1f,.1f);Check(damageFish.Spook==damage,"Grace/round release rejects spook");damageFish.RoundReleased=true;damageFish.InBossFight=true;haunt.ApplyTrapSpook(trap,trap.Position-Vector3.right*.1f,.1f);Check(damageFish.Spook==damage,"Boss gate rejects cart spook");damageFish.InBossFight=false;
  cc.enabled=false;actor.transform.position=new Vector3(.8f,ground+2.22f,0);cc.enabled=true;physics.Simulate(1f/60);damageFish.Spook=0;haunt.ApplyTrapSpook(trap,trap.Position-Vector3.right*.1f,.1f);Check(damageFish.Spook==0,"Physical clearance above handle avoids contact spook");lines.Add($"DAMAGE per-contact-step={rate*.1f:F3} duplicateParts=2 noDuplicate=True grace=True boss=True clearAbove=True");
  SceneManager.SetActiveScene(previous);Check(previous.isDirty==dirty,"Main scene dirty state preserved");lines.Add($"PASS {checks} native edit-mode checks using production movement/motor and authored cart solid parts. Not Play Mode, input-device, peer synchronization, or human acceptance.");
 }catch(Exception e){lines.Add("FAIL "+e);}finally{if(registry!=null&&damageFish)registry.Remove(damageFish);if(ownedActions)UnityEngine.Object.DestroyImmediate(ownedActions);current.SetValue(null,oldGame);if(previous.IsValid())SceneManager.SetActiveScene(previous);if(scene.IsValid()){foreach(var go in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}}
 Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Output));File.WriteAllLines(Output,lines);Debug.Log("JUMP NATIVE: "+Output);
 }
}
