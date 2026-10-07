using System;
using System.Collections;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
public static class TrapValidation
{
 static void Set(object o,string name,object v)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
 public static IEnumerator Tests(Action<bool,string> check)
 {
  var catalog=new[]{Resources.Load<GhostTrap>("Traps/ShoppingCartTrap"),Resources.Load<GhostTrap>("Traps/ClickTrap"),Resources.Load<GhostTrap>("Traps/FallingChandelierTrap")};
  foreach(var prefab in catalog) check(prefab && prefab.GetComponent<BoxCollider>() && prefab.Icon,"Authored trap prefab imports with collider, interface and icon: "+(prefab?prefab.FamilyTag:"missing"));
  var supply=Resources.Load<GameObject>("Traps/TrapSupply");check(supply && supply.GetComponent<GhostTrapSupply>(),"Authored reusable conveyor supply imports");
  var actions=Resources.Load<InputActionAsset>("HotelMultiplayerActions");var map=actions.FindActionMap("Ghost",true);
  check(map.FindAction("TrapClick",true).bindings[0].path=="<Mouse>/leftButton" && map.FindAction("TrapActivate",true).bindings[0].path=="<Keyboard>/space" && map.FindAction("TrapPoint",true)!=null,"Ghost mouse/Space dispatch uses New Input System bindings");
  check(map.FindAction("Dispose",true).bindings[0].path=="<Keyboard>/q","Q disposal has its own New Input System action");
  var world=new GameObject("Trap authority validation").AddComponent<GhostPlacementWorld>();world.enabled=false;Set(world,"_TrapPrefabs",catalog);world.BeginRound("editor-preview","round-A",1);
  check(world.Count==0,"Fresh round contains no preplaced traps");
  var owner=new GameObject("Ghost authority").AddComponent<HotelPlayer>();owner.gameObject.AddComponent<HotelPlayerMovement>().SetMode(HotelControlMode.Ghost);owner.ControlsReady=true;owner.RoundReleased=true;owner.RoundStateKey="round-A";owner.RoundVersion=1;owner.NetId=12;
  check(world.Hold(owner,"cart",new Vector3(-8,1,0))&&owner.PossessionEffectVersion==1,"Authority owns held inventory and emits one possession effect");
  check(!world.Hold(owner,"cart",Vector3.zero),"Duplicate pickup does not duplicate inventory or effect");
  check(world.Dispose(owner,-1)&&!world.Dispose(owner,-1)&&owner.HeldTrapFamily=="","Q discards held inventory exactly once");
  check(!world.TryPlace(owner,new Vector3(-8,1,0),"cart"),"Cannot place without authoritative held item");
  world.Hold(owner,"cart",new Vector3(-8,1,0));check(world.TryPlace(owner,new Vector3(-8,1,0),"cart"),"Runtime placement instantiates the authored prefab");
  var cart=world.Trap(0);check(cart is IGhostTrap && cart.ObjectSize==new Vector2(2.5f,2)&&cart.BackgroundSize==new Vector2(10,3),"Cart and aisle use enlarged authored dimensions");
  check(cart.GetComponent<BoxCollider>().size==new Vector3(2.5f,2,1),"Enlarged collision matches cart dimensions");
  Physics.SyncTransforms();check(!world.PlacementPoint(cart.Position,"cart",out _)&&!world.PlacementPoint(new Vector3(13.78f,1,0),"cart",out _),"Placement rejects enlarged body overlaps and hallway edges");
  world.Possess(owner,0,cart.Position);check(owner.ControlledCube==0,"Owner possesses its placed trap");
  int effect=owner.PossessionEffectVersion;world.Possess(owner,0,cart.Position);check(owner.PossessionEffectVersion==effect,"Duplicate possession does not replay puff");
  check(world.AcceptTrapAction(owner,0,new TrapInput(TrapInputKind.Move,1)),"Authority accepts owned movement input");
  float x=cart.Position.x;cart.Simulate(.1f,Time.unscaledTime);check(cart.Position.x>x,"Movement trap moves within authored area");
  check(!world.AcceptTrapAction(owner,0,new TrapInput(TrapInputKind.Click,point:cart.Position)),"Movement trap rejects click activation");
  check(!world.AcceptTrapAction(owner,1,new TrapInput(TrapInputKind.Move,1)),"Input cannot address an unpossessed trap ID");
  owner.Networked=true;owner.IsServer=false;check(!world.AcceptTrapAction(owner,0,new TrapInput(TrapInputKind.Move,1))&&!world.Dispose(owner,0),"Observer cannot author or dispose trap state");owner.Networked=false;
  owner.RoundReleased=false;check(!world.AcceptTrapAction(owner,0,new TrapInput(TrapInputKind.Move,1)),"Round barrier blocks trap control");owner.RoundReleased=true;
  var intruder=new GameObject("Wrong owner").AddComponent<HotelPlayer>();intruder.gameObject.AddComponent<HotelPlayerMovement>().SetMode(HotelControlMode.Ghost);intruder.ControlsReady=intruder.RoundReleased=true;intruder.RoundStateKey="round-A";intruder.RoundVersion=1;intruder.NetId=13;intruder.ControlledCube=0;
  check(!world.Dispose(intruder,0)&&!world.AcceptTrapAction(intruder,0,new TrapInput(TrapInputKind.Move,1)),"Wrong owner cannot control or dispose another player's trap");intruder.gameObject.SetActive(false);Object.Destroy(intruder.gameObject);
  var observer=new GameObject("Reconnected round observer").AddComponent<GhostPlacementWorld>();observer.enabled=false;Set(observer,"_TrapPrefabs",catalog);observer.BeginRound("editor-preview","round-A",1);
  string current=world.Snapshot;
  owner.Networked=true;owner.IsServer=false;owner.ControlsReady=false;owner.PlacedObjectsJson=current;
  observer.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(observer,null);
  check(observer.Count==0,"Disconnected replica cannot apply retained serialized inventory");
  owner.ControlsReady=true;
  observer.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(observer,null);
  check(observer.Count==1,"Connected same-round replica applies the authoritative snapshot");owner.Networked=false;
  check(observer.ApplySnapshot(current)&&observer.Count==1,"Same-lobby same-round reconnect reconstructs current authored inventory");
  observer.BeginRound("new-room","round-B",1);check(observer.Count==0&&!observer.ApplySnapshot(current),"New lobby rejects prior lobby serialized inventory");
  observer.BeginRound("editor-preview","round-B",1);check(!observer.ApplySnapshot(current),"Reused room code rejects old round nonce and cached snapshots");
  check(!world.RequestMatches(owner,"round-old",1)&&!world.RequestMatches(owner,"round-A",0),"Delayed commands cannot mutate a different round");
  check(world.Dispose(owner,0)&&owner.ControlledCube==-1&&world.Count==0&&!world.Dispose(owner,0),"Q removes owned possessed trap once and exits possession cleanly");
  observer.BeginRound("editor-preview","round-A",1);check(observer.ApplySnapshot(world.Snapshot)&&!observer.ApplySnapshot(current),"Deleted inventory stays deleted when an older revision arrives");
  yield return new WaitForSecondsRealtime(.21f);world.Hold(owner,"cart",Vector3.zero);check(world.TryPlace(owner,new Vector3(-4,1,0),"cart")&&world.Trap(1)&&!world.Trap(0),"Disposed IDs are never reused within a round");
  world.BeginRound("editor-preview","round-C",2);check(world.Count==0&&!world.ApplySnapshot(current),"New round clears active traps and invalidates prior state");
  Object.Destroy(observer.gameObject);
  var click=Object.Instantiate(catalog[1]);click.Initialize(world,new GhostCubePlacement{Position=new Vector3(0,.5f,0),Origin=new Vector3(0,.5f,0),FamilyTag="click"},1);
  check(!click.AcceptInput(new TrapInput(TrapInputKind.Activate),0),"Click trap rejects Space activation");
  check(!click.AcceptInput(new TrapInput(TrapInputKind.Click,point:Vector3.one*100),0),"Click trap rejects hits outside its collider");
  check(click.AcceptInput(new TrapInput(TrapInputKind.Click,point:click.Position),0),"Click trap accepts a hit on its own body");
  check(!click.AcceptInput(new TrapInput(TrapInputKind.Click,point:click.Position),0),"Active click trap rejects repeated activation");click.Simulate(1,1);check(click.Phase==TrapPhase.Armed && click.CaptureState().Activations==1,"Click pulse completes once and rearms");
  var chandelier=Object.Instantiate(catalog[2]);chandelier.Initialize(world,new GhostCubePlacement{Position=new Vector3(8,3.9f,0),Origin=new Vector3(8,.5f,0),FamilyTag="chandelier"},2);
  check(!chandelier.AcceptInput(new TrapInput(TrapInputKind.Move,1),0) && !chandelier.AcceptInput(new TrapInput(TrapInputKind.Click,point:chandelier.Position),0),"Chandelier isolates Space from movement and click");
  yield return Capture(chandelier,"05-chandelier-armed");
  check(chandelier.AcceptInput(new TrapInput(TrapInputKind.Activate),0),"Space starts authored chandelier fall");chandelier.Simulate(.35f,.35f);check(chandelier.Position.y<3.9f && chandelier.Position.y>.5f,"Chandelier falls progressively");
  yield return Capture(chandelier,"06-chandelier-falling");
  chandelier.Simulate(.35f,.7f);check(chandelier.Phase==TrapPhase.Latched && Mathf.Abs(chandelier.Position.y-.5f)<.001f,"Chandelier lands and latches without invented damage");
  yield return Capture(chandelier,"07-chandelier-landed");
  check(chandelier.AcceptInput(new TrapInput(TrapInputKind.Reset),1) && Mathf.Abs(chandelier.Position.y-3.9f)<.001f && chandelier.Phase==TrapPhase.Armed,"Manual reset restores authored armed height");
  var replica=Object.Instantiate(catalog[2]);replica.ApplyState(chandelier.CaptureState());check(replica.Position==chandelier.Position,"Snapshot restores chandelier position and phase");
  var smoke=Object.Instantiate(Resources.Load<PossessionSmoke>("PossessionSmoke"));Set(smoke,"_DestroyOnComplete",false);smoke.PlayAt(Vector3.zero);yield return new WaitForSecondsRealtime(.1f);smoke.PlayAt(Vector3.one);yield return new WaitForSecondsRealtime(.6f);
  var ui=(Monologue.Dialogue.StoryUI)smoke.GetType().GetField("_UI",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(smoke);
  var content=(GameObject)ui.GetType().GetField("_Content",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
  check(!content.activeSelf,"Replayed Ink puff completes and hides all authored clouds");smoke.PlayAt(Vector3.zero);check(content.activeSelf,"Puff can replay after completion");smoke.gameObject.SetActive(false);check(!content.activeSelf,"Interrupted puff cleans up immediately");Object.Destroy(smoke.gameObject);
  Object.Destroy(replica.gameObject);Object.Destroy(chandelier.gameObject);Object.Destroy(click.gameObject);Object.Destroy(world.gameObject);Object.Destroy(owner.gameObject);yield return null;
  yield return LateJoinTests(catalog,check);
 }
 static IEnumerator LateJoinTests(GhostTrap[] catalog,Action<bool,string> check)
 {
  var host=new GameObject("Current released round");host.SetActive(false);
  var world=host.AddComponent<GhostPlacementWorld>();Set(world,"_TrapPrefabs",catalog);
  var scene=host.AddComponent<GameSceneController>();Set(scene,"_PlacementWorld",world);host.SetActive(true);
  var a=new GameObject("Existing ghost");a.SetActive(false);a.AddComponent<HotelPlayerMovement>().SetMode(HotelControlMode.Ghost);var owner=a.AddComponent<HotelPlayer>();owner.NetId=11;owner.GhostId=11;owner.ControlsReady=true;a.SetActive(true);
  owner.RoundStateKey="kept-round";owner.RoundVersion=4;owner.RoundRoster="11";owner.RoundReleased=true;
  world.BeginRound("editor-preview","kept-round",4);world.Hold(owner,"cart",Vector3.zero);check(world.TryPlace(owner,new Vector3(-8,1,0),"cart"),"Late-join fixture uses actual authoritative runtime placement");
  var gate=(GameRoundGate)scene.GetType().GetField("_Gate",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(scene);gate.Begin(new uint[]{11},4,Time.unscaledTime);gate.Remove(11);
  Set(scene,"_Chosen",true);Set(scene,"_Released",true);
  var b=new GameObject("Reconnected fish");b.SetActive(false);b.AddComponent<HotelPlayerMovement>().SetMode(HotelControlMode.Fish);var joining=b.AddComponent<HotelPlayer>();joining.NetId=12;joining.ControlsReady=true;b.SetActive(true);
  scene.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(scene,null);
  check(world.Count==1&&owner.RoundReleased&&owner.RoundStateKey=="kept-round"&&joining.RoundStateKey=="kept-round"&&joining.RoundVersion==4&&!joining.RoundReleased,"Same-lobby released round admits a reconnect without clearing inventory or stopping existing gameplay");
  scene.Acknowledge(joining,4);check(!joining.RoundReleased,"New participant cannot skip its mandatory presentation");
  scene.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(scene,null);
  check(world.Count==1&&world.RoundKey=="kept-round","Repeated late-join reconciliation preserves current inventory");
  scene.Exit();check(world.Count==0,"Leaving Game clears owned runtime objects and cached snapshot");
  Object.Destroy(a);Object.Destroy(b);Object.Destroy(host);yield return null;
 }
 static IEnumerator Capture(GhostTrap trap,string filename)
 {
  var camera=new GameObject("Trap capture camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=4;camera.transform.position=new Vector3(8,2.5f,-10);camera.backgroundColor=new Color(.11f,.12f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;
  var texture=new RenderTexture(1280,720,24);camera.targetTexture=texture;
  yield return null;
  var presentation=trap.GetComponent<GhostTrapAreaPresentation>();presentation.enabled=false;
  var canvas=trap.GetComponentInChildren<Canvas>(true);
  var area=(RectTransform)presentation.GetType().GetField("_Area",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(presentation);
  var body=(RectTransform)presentation.GetType().GetField("_Cart",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(presentation);
  var areaScreen=area.position;var bodyScreen=body.position;
  canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
  if(RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform,areaScreen,camera,out var areaWorld))area.position=areaWorld;
  if(RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform,bodyScreen,camera,out var bodyWorld))body.position=bodyWorld;
  camera.Render();var prior=RenderTexture.active;RenderTexture.active=texture;
  var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../"+filename+".png"),pixels.EncodeToPNG());RenderTexture.active=prior;
  canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;presentation.enabled=true;camera.targetTexture=null;texture.Release();Object.Destroy(texture);Object.Destroy(pixels);Object.Destroy(camera.gameObject);yield return null;
 }

}
