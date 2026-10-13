#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using HauntedFish.Multiplayer;
public static class GameGameplayNativeValidation
{
 const string Folder="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/game-final-native";
 const string Marker="GameGameplayNativeValidation.Step";
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static double due; static HotelPlayer ghost,fish,remaining; static GameSceneController game;
 static GhostPlacementController controls; static GhostTrap cart,chandelier;
 static CanvasGroup blocker; static Mouse mouse;static Keyboard keyboard;
 static Vector3 cartBefore,corpse;static uint originalGhost;static int effectVersion;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static IDictionary Objects=>(IDictionary)Get(game.PlacementWorld,"_Objects");
 static void Note(string text)=>File.AppendAllText(Folder+"/gameplay-results.txt",text+"\n");
 static void Check(bool value,string text){Note((value?"PASS ":"FAIL ")+text);if(!value)throw new Exception(text);}
 static void Next(int phase,double delay=.35){SessionState.SetInt(Marker,phase);due=EditorApplication.timeSinceStartup+delay;}
 static void Input(Key key=Key.None,bool click=false,Vector2? point=null){
  InputSystem.QueueStateEvent(keyboard,key==Key.None?new KeyboardState():new KeyboardState(key));
  var pos=point??new Vector2(Mathf.Max(20,Screen.width-30),30);
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=0});
  if(click)InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});
 }
 static void Position(float x,float y){Set(controls,"_Position",new Vector3(x,y,0));}
 static void Capture(string name){typeof(GameHallwayAcceptanceBatch).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{HotelViewCamera.Current,"gameplay-"+name,true});}
 public static void Run(){
  if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-gameplay-native"))throw new Exception("Explicit Game window required");
  Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/gameplay-results.txt","HARNESS: offline production Game scene, mapped InputSystem device events (not OS input); ready state bootstrap for gameplay isolation. No connected RPC transport claimed.\n");
  SessionState.SetInt(Marker,1);SessionState.SetFloat(Marker+"Deadline",(float)EditorApplication.timeSinceStartup+90);
  EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetInt(Marker,0)>0){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static HotelPlayer Fish(string name,float x){
  var obj=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"));obj.name=name;
  var actor=obj.GetComponent<HotelPlayer>();actor.Networked=false;actor.RoundStateKey=ghost.RoundStateKey;actor.RoundVersion=ghost.RoundVersion;actor.RoundRoster=ghost.RoundRoster;
  actor.GhostId=originalGhost;actor.RoundReleased=actor.RoundIntroComplete=actor.RoundCinematicComplete=actor.RoundReady=true;
  Call(actor,"ApplyEditorRole",false);actor.Teleport(new Vector3(x,1,0));return actor;
 }
 static void Tick(){
  int step=SessionState.GetInt(Marker,0);if(step==0)return;
  try{
   if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Marker+"Deadline",0))throw new Exception("Native gameplay timeout phase="+step);
   if(step==99){if(!EditorApplication.isPlayingOrWillChangePlaymode){Note("PASS native gameplay run complete; connected host/client and OS interaction remain separate validation gaps");SessionState.SetInt(Marker,0);EditorApplication.update-=Tick;EditorApplication.Exit(0);}return;}
   if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<due)return;
   if(step==1){
    ghost=HotelPlayer.LocalPlayer;game=GameSceneController.Current;if(!ghost||!game||string.IsNullOrEmpty(ghost.RoundStateKey))return;
    Check(!ghost.Networked,"saved offline authority actor active");originalGhost=ghost.GhostId;
    ghost.RoundIntroComplete=ghost.RoundCinematicComplete=ghost.RoundReady=ghost.RoundReleased=true;
    Set(game,"_Chosen",true);Set(game,"_Released",true);Set(game,"_SetupStarted",false);
    UnityEngine.Object.FindFirstObjectByType<GhostSelectionPresentation>().Close();
    controls=UnityEngine.Object.FindFirstObjectByType<GhostPlacementController>();
    var screen=UnityEngine.Object.FindFirstObjectByType<HotelConnectionScreen>();blocker=screen?(CanvasGroup)Get(screen,"_Overlay"):null;
    if(screen)screen.enabled=false;if(blocker){blocker.alpha=0;blocker.blocksRaycasts=false;blocker.interactable=false;}
    mouse=Mouse.current??InputSystem.AddDevice<Mouse>();keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();Input();
    Check(Get(controls,"_ForbiddenArea") is Image&&Get(controls,"_PlacementPreview") is GameObject,"overlay/preview load from saved scene without authoring helper");
    Check(game.PlacementWorld.Definition("cart")!=null,"production conveyor trap definitions bound");Next(2,.7);
   }
   else if(step==2){var supply=game.Traps.ClosestPackage(controls.FlightPosition);if(!supply)return;Position(supply.transform.position.x,supply.transform.position.y);Input(Key.E);Next(3);}
   else if(step==3){Check(!string.IsNullOrEmpty(ghost.HeldTrapFamily),"mapped E obtains actual conveyor inventory");Note("PACKAGE "+ghost.HeldTrapFamily);Input(Key.Q);Next(4);}
   else if(step==4){Check(string.IsNullOrEmpty(ghost.HeldTrapFamily),"mapped Q disposes held package");Position(10,1);ghost.HeldTrapFamily="cart";Input(click:true);Next(5);}
   else if(step==5){Check(Objects.Count==1&&string.IsNullOrEmpty(ghost.HeldTrapFamily),"mapped mouse places exactly one cart and consumes held item");cart=Objects.Values.Cast<GhostTrap>().Single();Input(click:true);Next(6);}
   else if(step==6){Check(Objects.Count==1,"repeated click does not duplicate placed trap");Position(-8.5f,1);ghost.HeldTrapFamily="cart";Input();Next(7);}
   else if(step==7){Input(click:true);Next(8);}
   else if(step==8){Check(Objects.Count==1&&ghost.HeldTrapFamily=="cart","mapped click rejects safe hallway without consuming inventory");var overlay=(Image)Get(controls,"_ForbiddenArea");Check(overlay.gameObject.activeInHierarchy,"whole authored forbidden hallway overlay visible");Capture("safe-hallway-overlay");Position(16,1);Input();if(blocker){blocker.blocksRaycasts=true;blocker.interactable=true;}Next(9);}
   else if(step==9){Check(EventSystem.current&&EventSystem.current.IsPointerOverGameObject(),"blocking authored UI receives actual pointer raycast");Input(click:true);Next(10);}
   else if(step==10){Check(Objects.Count==1&&ghost.HeldTrapFamily=="cart","blocking UI prevents placement");if(blocker){blocker.blocksRaycasts=false;blocker.interactable=false;}Input();Next(11);}
   else if(step==11){Input(click:true);Next(12);}
   else if(step==12){Check(Objects.Count==2&&string.IsNullOrEmpty(ghost.HeldTrapFamily),"fresh click after UI close places once");Position(22,1);ghost.HeldTrapFamily="cart";Input(Key.E);Next(13);}
   else if(step==13){Check(Objects.Count==3&&string.IsNullOrEmpty(ghost.HeldTrapFamily),"mapped E placement retained");Input();Position(cart.Position.x,cart.Position.y);Next(14);}
   else if(step==14){Input(Key.E);Next(15);}
   else if(step==15){Check(ghost.ControlledCube==cart.Index,"mapped E possesses trap, not player body");cartBefore=cart.Position;Input(Key.D);Next(16,.6);}
   else if(step==16){Check(cart.Position.x>cartBefore.x+.05f,"possessed cart moves through actual mapped input");Capture("cart-pattern-and-markers");Input();Next(17);}
   else if(step==17){Input(Key.E);Next(18);}
   else if(step==18){Check(ghost.ControlledCube<0,"mapped E unpossesses trap");Position(30,2.7f);ghost.HeldTrapFamily="chandelier";Input();Next(19,.7);}
   else if(step==19){var point=HotelViewCamera.Current.WorldToScreenPoint(new Vector3(30,3.5f,0));Input(click:true,point:new Vector2(point.x,point.y));Next(20);}
   else if(step==20){Check(Objects.Count==4&&string.IsNullOrEmpty(ghost.HeldTrapFamily),"mapped pointer places ceiling chandelier");chandelier=Objects.Values.Cast<GhostTrap>().Single(t=>t.PlacementMode==TrapPlacementMode.HallwayCeiling);Position(chandelier.Position.x,chandelier.Position.y-.4f);Input();Next(21);}
   else if(step==21){Input(Key.E);Next(22);}
   else if(step==22){Check(ghost.ControlledCube==chandelier.Index,"mapped E possesses chandelier");Capture("possessed-chandelier-sway");Input(Key.Space);Next(23,.15);}
   else if(step==23){Check(chandelier.Phase==TrapPhase.Active||chandelier.Phase==TrapPhase.Latched,"mapped Space activates falling chandelier");Capture("chandelier-falling");Input();Next(24,1.3);}
   else if(step==24){Check(chandelier.Phase==TrapPhase.Latched,"chandelier settles on floor");Input(Key.E);Next(25);}
   else if(step==25){Input();var commands=(IHotelGameCommands)Get(game,"_PlayerCommands");ghost.HeldTrapFamily="cart";Position(45,1);game.PlacementWorld.PlacementPoint(new Vector3(45,1,0),"cart",out var place);
    Check(!commands.PlaceTrap(ghost,place,"cart","stale-key",ghost.RoundVersion),"actual command adapter rejects stale round key");ghost.HeldTrapFamily="";
    fish=Fish("Native death subject",35);remaining=Fish("Native remaining living fish",40);Next(26,.8);}
   else if(step==26){Call(fish,"RegisterLocalPlayer");Check(fish.ControlMode==HotelControlMode.Fish&&remaining.ControlMode==HotelControlMode.Fish,"two real prefab fish attached as living players");Check(game.LivingFishCount==2,"living count includes fish, excludes original ghost");
    Check(game.Haunting.ApplyDamage(fish,40)&&fish.Health==60&&!fish.IsDead,"production damage reduces health without role change");effectVersion=fish.GhostEmergenceVersion;Capture("before-real-death");
    Check(game.Haunting.ApplyDamage(fish,fish.Health),"production lethal damage accepted");corpse=fish.transform.position;Next(27,.05);}
   else if(step==27){Check(fish.IsDead&&fish.ControlMode==HotelControlMode.Spectator&&fish.GhostEmergenceVersion==effectVersion+1,"death publishes exactly one emergence and spectator state");Check(ghost.GhostId==originalGhost&&ghost.ControlMode==HotelControlMode.Ghost,"original ghost remains ghost; no fish-body exchange");Check(!fish.BodyController.enabled&&fish.Movement.ActiveInputMap=="","spectator has no body collision or movement input map");Capture("real-death-through-body");Next(28,.18);}
   else if(step==28){Capture("real-death-rising");Next(29,.6);}
   else if(step==29){Check((fish.transform.position-corpse).sqrMagnitude<.00001f,"corpse stays on floor after production emergence animation");Check(!game.Haunting.ApplyDamage(fish,100)&&fish.GhostEmergenceVersion==effectVersion+1,"repeat hit cannot revive, swap or replay death");fish.SwitchEditorRole();Check(fish.ControlMode==HotelControlMode.Spectator,"dead player cannot re-enter gameplay via editor role request");
    var camera=UnityEngine.Object.FindFirstObjectByType<GameCameraOwner>();Check(camera.Camera.Follow==remaining.transform,"spectator camera watches remaining living fish");Check(game.LivingFishCount==1,"remaining living fish continues round");Capture("corpse-and-spectator");
    var controlledBefore=fish.ControlledCube;game.PlacementWorld.Possess(fish,cart.Index,cart.Position);Check(fish.ControlledCube==controlledBefore,"spectator rejected by trap possession authority");Check(game.Haunting.ApplyDamage(remaining,remaining.Health),"last fish death accepted");Next(30,.7);}
   else if(step==30){Check(game.LivingFishCount==0&&game.AllFishDead,"last fish condition exposed without role transfer");fish.ClearRoundInventory();Check(fish.Health==100&&!fish.IsDead,"round reset restores health and death flag");Note("LIMIT: real mapped input and authority command adapter tested offline; connected host/client RPC transport, gamepad and OS menu interaction not executed.");SessionState.SetInt(Marker,99);EditorApplication.ExitPlaymode();}
  }catch(Exception e){Note("FAIL "+e);Debug.LogException(e);SessionState.SetInt(Marker,0);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
 }
}
#endif
