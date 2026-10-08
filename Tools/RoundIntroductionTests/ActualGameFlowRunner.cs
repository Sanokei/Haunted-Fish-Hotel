using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public sealed class ActualGameFlowRunner:MonoBehaviour {
 Keyboard keyboard; float releaseAt;
 float began;bool wheel,landed,attic,ghost,blur,released;int phase;string folder;
 static void Set(object value,string name,object data)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,data);
 static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
 void Awake(){InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;began=Time.unscaledTime;folder=System.IO.Path.Combine(Application.dataPath,"../ActualGameEvidence");Directory.CreateDirectory(folder);Application.logMessageReceived+=OnLog;}
 void OnLog(string message,string stack,LogType type){if(type==LogType.Exception){File.AppendAllText(System.IO.Path.Combine(folder,"exceptions.txt"),message+"\n"+stack+"\n");}}
 void Check(bool value,string text){if(!value)throw new Exception(text);Debug.Log("GAME_CHECK: "+text);}
 void Update(){try {
  if(keyboard!=null && releaseAt>0 && Time.unscaledTime>=releaseAt){InputSystem.QueueStateEvent(keyboard,new KeyboardState());releaseAt=0;}
  if(phase==3)return;
  if(Time.unscaledTime-began>60)throw new Exception("Ordinary Game entry timed out. Local player="+HotelPlayer.LocalPlayer);
  var scene=FindFirstObjectByType<GameSceneController>();var local=HotelPlayer.LocalPlayer;if(!scene||!local)return;
  var selection=scene.GetComponent<GhostSelectionPresentation>();var root=Get<GameObject>(selection,"_Root");var graphic=Get<GhostSelectionWheel>(selection,"_Graphic");
  if(!wheel&&root.activeInHierarchy){wheel=true;if(!local.Networked)Check(local.gameObject.CompareTag("EditorOnly") && local.gameObject.hideFlags==HideFlags.NotEditable,"Authored preview is excluded from builds and installed Mirage scene spawning");Check(!string.IsNullOrEmpty(local.RoundRoster),"Authored Game entry starts its authoritative round automatically");Capture(root.GetComponent<Canvas>(),"01-actual-game-wheel-spinning");}
  if(!landed&&graphic.Selected>=0&&graphic.SelectionPulse>.05f){landed=true;Capture(root.GetComponent<Canvas>(),"02-actual-game-selected-slice");Check(wheel,"Ordinary Game wheel lands and pulses");}
  var cutscene=Get<StoryUI>(selection,"_Attic");if(cutscene&&!attic){attic=true;Capture(cutscene.GetComponent<Canvas>(),"03-actual-game-wall-pan");Check(landed,"Scene starts authored attic prefab after landed slice");}
  if(cutscene&&cutscene.Find("ghost").gameObject.activeInHierarchy&&!ghost){ghost=true;Capture(cutscene.GetComponent<Canvas>(),"04-actual-game-attic-ghost");}
  if(cutscene&&cutscene.Find("return_blur").gameObject.activeInHierarchy&&!blur){blur=true;Capture(cutscene.GetComponent<Canvas>(),"05-actual-game-return-blur");}
  if(local.RoundReleased&&!released){released=true;Check(wheel&&landed&&attic&&ghost&&blur,"Ordinary Game entry completes wheel, wall/attic reveal and blurred return before releasing gameplay");}
  if(!released)return;
  var control=scene.GetComponent<GameEditorRoleSwitch>();var button=Get<Button>(control,"_Button");var canvas=Get<GameObject>(control,"_Root").GetComponent<Canvas>();if(!canvas.gameObject.activeInHierarchy)return;
  if(phase==0){Check(button.interactable,"Existing Editor ghost-team switch becomes visible and interactable after the round");Capture(canvas,"06-actual-game-team-switch");if(local.ControlMode==HotelControlMode.Ghost)Submit(button);phase=1;return;}
  if(phase==1){if(local.ControlMode!=HotelControlMode.Fish || (keyboard!=null && keyboard.enterKey.isPressed))return;Check(local.ControlMode==HotelControlMode.Fish,"Existing button switches ghost to player");Check(Get<Text>(control,"_Label").font && Get<Text>(control,"_Label").text=="Switch to Ghost","Existing Switch to Ghost label is visible in player mode");Capture(canvas,"07-actual-game-switch-to-ghost");Submit(button);phase=2;return;}
  if(local.ControlMode!=HotelControlMode.Ghost)return;Check(local.ControlMode==HotelControlMode.Ghost,"Existing button switches player to ghost without changing team rules");phase=3;StartCoroutine(RunInventoryValidation(InventoryTests(local,scene,button)));
 }catch(Exception e){Debug.LogException(e);File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"FAIL "+e);EditorApplication.Exit(1);}}
 IEnumerator RunInventoryValidation(IEnumerator test)
 {
  while(true){object next;try{if(!test.MoveNext())yield break;next=test.Current;}catch(Exception e){Debug.LogException(e);File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"FAIL "+e);EditorApplication.Exit(1);yield break;}yield return next;}
 }
 IEnumerator InventoryTests(HotelPlayer local,GameSceneController scene,Button button)
 {
  yield return null;
  var world=scene.GetComponent<GhostPlacementWorld>();var controls=scene.GetComponent<GhostPlacementController>();
  Check(world.Count==0,"Ordinary Game round starts with empty runtime inventory and no preplaced traps");
  var manager=FindFirstObjectByType<TrapManager>();
  Check(manager&&manager.PackageCount>0&&manager.Running,"Authored Trap Manager starts weighted packages after actual round release");
  var available=manager.ClosestPackage(controls.FlightPosition);string contained=available.FamilyTag;int packageId=available.PackageId;
  Check(available.BoxArt.name=="cardboardbox"&&available.DisplayArt==manager.Definition(contained).Artwork,"Actual Game box prints its configured contained trap artwork");
  var packageBefore=available.transform.position;Set(controls,"_Position",available.transform.position);yield return new WaitForSecondsRealtime(.3f);
  Check(available.transform.position.x>packageBefore.x,"Actual conveyor moves packages at configured speed");
  CaptureWorld("12-authoritative-conveyor-packages");
  available=manager.Package(packageId);Set(controls,"_Position",available.transform.position);yield return null;
  PressGhostKey(controls,Key.E);yield return null;yield return null;
  Check(local.HeldTrapFamily==contained&&!manager.Package(packageId),"Actual E input consumes shared package ID and grants its contained trap");
  CaptureWorld("13-package-opening-puff");
  var configuredTrap=world.Definition(contained);Vector3 firstPlacement=Vector3.zero;bool foundPlacement=false;
  foreach(float requestedX in new[]{8f,4f,0f,-4f,-8f,12f,-12f})if(world.PlacementPoint(new Vector3(requestedX,configuredTrap.BodyHalfSize.y,0),contained,out firstPlacement)){foundPlacement=true;break;}
  if(!foundPlacement){foreach(float x in new[]{8f,4f,0f,-4f,-8f,12f,-12f})foreach(var obstacle in Physics.OverlapBox(new Vector3(x,configuredTrap.BodyHalfSize.y,0)+configuredTrap.ArmedOffset,configuredTrap.HalfExtents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))Debug.Log("PLACEMENT_OBSTACLE x="+x+" name="+obstacle.name+" bounds="+obstacle.bounds);}
  Check(foundPlacement,"Actual authored hallway contains a clear position for package family "+contained);
  Set(controls,"_Position",firstPlacement);yield return new WaitForSecondsRealtime(.15f);
  PressGhostKey(controls,Key.E);yield return new WaitForSecondsRealtime(.2f);
  Debug.Log("GAME_PLACEMENT family="+contained+" accepted="+local.GhostPlacementAccepted+" reply="+local.GhostPlacementReply+" held="+local.HeldTrapFamily+" pending="+Get<bool>(controls,"_Pending")+" point="+firstPlacement);
  var placed=JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot);Check(placed.Cubes.Count==1&&placed.Cubes[0].FamilyTag==contained&&local.HeldTrapFamily=="","Actual E places the prefab contained in the consumed package");
  var fromPackage=world.Trap(placed.Cubes[0].Id);Set(controls,"_Position",fromPackage.Position);yield return null;yield return null;
  PressGhostKey(controls,Key.E);yield return null;yield return null;
  Check(local.ControlledCube==fromPackage.Index,"Actual E possesses the configured placed trap");CaptureWorld("15-configured-package-placed-trap");
  PressGhostKey(controls,Key.Q);yield return null;
  Check(local.HeldTrapFamily==""&&world.Count==0&&local.ControlledCube==-1,"Actual Q disposes the configured package's placed trap");
  var configured=Get<TrapDefinition[]>(manager,"_Traps");var chandelier=Resources.Load<GhostTrap>("Traps/FallingChandelierTrap");
  var forced=new List<TrapDefinition>();foreach(var entry in configured)forced.Add(new TrapDefinition{Prefab=entry.Prefab,DisplayArt=entry.DisplayArt,Weight=entry.Prefab==chandelier?1:0});
  manager.ConfigureDefinitions(forced);manager.ResetRound();manager.Simulate(local,0);
  var chandelierBox=manager.ClosestPackage(controls.FlightPosition);
  Check(chandelierBox&&chandelierBox.FamilyTag==chandelier.FamilyTag&&chandelierBox.DisplayArt==chandelier.Icon,"Actual runtime injection shows chandelier artwork on weighted boxes");
  Set(controls,"_Position",chandelierBox.transform.position);yield return new WaitForSecondsRealtime(.3f);CaptureWorld("14-chandelier-conveyor-package");
  manager.ConfigureDefinitions(new[]{new TrapDefinition{Prefab=Resources.Load<GhostTrap>("Traps/ShoppingCartTrap"),Weight=1}});
  manager.ResetRound();manager.Simulate(local,0);manager.PauseConveyor();
  int[] cartIds=new int[2];
  for(int i=0;i<2;i++) {
   var box=manager.ClosestPackage(controls.FlightPosition);Check(box&&box.FamilyTag=="cart","Multi-cart fixture obtains a real cart from the current conveyor");
   Set(controls,"_Position",box.transform.position);yield return null;PressGhostKey(controls,Key.E);yield return null;yield return null;
   Check(local.HeldTrapFamily=="cart","Each conveyor cart opens through actual E input");
   Set(controls,"_Position",new Vector3(i==0?1:8,1,0));yield return null;PressGhostKey(controls,Key.E);yield return null;yield return new WaitForSecondsRealtime(.22f);
   var all=JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot).Cubes;cartIds[i]=all[all.Count-1].Id;
   Check(world.Trap(cartIds[i])&&world.Trap(cartIds[i]).FamilyTag=="cart","Each opened cart registers its own stable instance ID");
  }
  Check(cartIds[0]!=cartIds[1],"Multiple shopping carts have distinct stable IDs");
  for(int pass=0;pass<4;pass++) {
   int id=cartIds[pass%2];var currentCart=world.Trap(id);var otherCart=world.Trap(cartIds[(pass+1)%2]);
   Set(controls,"_Position",currentCart.Position);yield return null;PressGhostKey(controls,Key.E);yield return null;yield return null;
   Check(local.ControlledCube==id,"Actual E independently possesses shopping cart "+id);
   float before=currentCart.Position.x,otherBefore=otherCart.Position.x;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(pass<2?(pass%2==0?Key.A:Key.D):(pass%2==0?Key.D:Key.A)));yield return new WaitForSecondsRealtime(.2f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
   if(Mathf.Abs(currentCart.Position.x-before)<=.05f){Debug.Log("CART_STUCK pass="+pass+" point="+currentCart.Position+" origin="+currentCart.CaptureState().Origin+" axis="+Get<float>(currentCart,"_Axis")+" limits="+currentCart.Limits);foreach(var hit in Physics.BoxCastAll(currentCart.Position,currentCart.HalfExtents,pass%2==0?Vector3.right:Vector3.left,currentCart.transform.rotation,.5f,~0,QueryTriggerInteraction.Ignore))Debug.Log("CART_HIT "+hit.collider.name+" distance="+hit.distance+" bounds="+hit.collider.bounds);}
   Check(Mathf.Abs(currentCart.Position.x-before)>.05f,"Actual movement input moves cart "+id);
   Check(Mathf.Abs(otherCart.Position.x-otherBefore)<.001f,"Unpossessed cart receives no other instance's input");
   PressGhostKey(controls,Key.E);yield return null;yield return null;Check(local.ControlledCube==-1,"Repeated E exits cart possession cleanly");
   float stopped=currentCart.Position.x;yield return new WaitForSecondsRealtime(.1f);Check(Mathf.Abs(currentCart.Position.x-stopped)<.001f,"Unpossessed cart stops previous movement immediately");
  }
  var firstCart=world.Trap(cartIds[0]);var secondCart=world.Trap(cartIds[1]);
  var ownState=firstCart.CaptureState();var foreignState=secondCart.CaptureState();var foreign=foreignState;foreign.OwnerId=local.NetId+100;foreign.Position=firstCart.Position+Vector3.right*1.6f;secondCart.ApplyState(foreign);
  var between=firstCart.Position+Vector3.right*1.2f;
  Check(world.NearbyCube(between,local)==firstCart.Index,"Nearest foreign cart cannot mask an eligible owned cart");
  world.Possess(local,secondCart.Index,secondCart.Position);Check(local.ControlledCube==-1,"Foreign cart possession remains rejected by authority");
  local.ControlledCube=secondCart.Index;Check(!world.AcceptTrapAction(local,secondCart.Index,new TrapInput(TrapInputKind.Move,1))&&!world.Dispose(local,secondCart.Index),"Foreign ownership rejects both cart input and disposal");local.ControlledCube=-1;secondCart.ApplyState(foreignState);Physics.SyncTransforms();
  var adjacent=foreignState;adjacent.Position=firstCart.Position+Vector3.right*2.6f;adjacent.Origin=adjacent.Position;secondCart.ApplyState(adjacent);Physics.SyncTransforms();
  Set(controls,"_Position",firstCart.Position);world.Possess(local,firstCart.Index,firstCart.Position);Check(world.AcceptTrapAction(local,firstCart.Index,new TrapInput(TrapInputKind.Move,1)),"Adjacent owned cart accepts its own input");
  float collisionStart=firstCart.Position.x;firstCart.Simulate(.5f,Time.unscaledTime);Check(firstCart.Position.x>=collisionStart && firstCart.Position.x+firstCart.BodyHalfSize.x<secondCart.Position.x-secondCart.BodyHalfSize.x,"Cart respects another cart's collider without passing through");
  collisionStart=firstCart.Position.x;world.AcceptTrapAction(local,firstCart.Index,new TrapInput(TrapInputKind.Move,-1));firstCart.Simulate(.05f,Time.unscaledTime);Check(firstCart.Position.x<collisionStart,"Cart can move away from a neighboring cart without stale blocking");
  world.Possess(local,-1,firstCart.Position);firstCart.ApplyState(ownState);secondCart.ApplyState(foreignState);Physics.SyncTransforms();
  Check(!world.RequestMatches(local,"previous-round",local.RoundVersion),"Delayed cart commands cannot target another round");
  // Frame both real placed instances for review without moving the gameplay camera.
  var viewBinding=FindFirstObjectByType<HotelViewCamera>();var gameplayCamera=viewBinding.Output;gameplayCamera.tag="Untagged";
  var reviewCamera=new GameObject("Disposable two-cart review camera").AddComponent<Camera>();reviewCamera.tag="MainCamera";reviewCamera.orthographic=true;reviewCamera.orthographicSize=4.5f;reviewCamera.transform.position=new Vector3((firstCart.Position.x+secondCart.Position.x)*.5f,2,-10);reviewCamera.clearFlags=CameraClearFlags.SolidColor;reviewCamera.backgroundColor=new Color(.05f,.05f,.1f);
  viewBinding.BindOutput(reviewCamera);CaptureWorld("16-independent-conveyor-carts");viewBinding.BindOutput(gameplayCamera);reviewCamera.tag="Untagged";gameplayCamera.tag="MainCamera";Destroy(reviewCamera.gameObject);
  foreach(int id in cartIds){Set(controls,"_Position",world.Trap(id).Position);yield return null;PressGhostKey(controls,Key.E);yield return null;PressGhostKey(controls,Key.Q);yield return null;yield return null;Check(!world.Trap(id)&&local.ControlledCube==-1&&!world.AcceptTrapAction(local,id,new TrapInput(TrapInputKind.Move,1)),"Actual Q clears independent cart and stale targeting "+id);}
  manager.ConfigureDefinitions(configured);

  Check(world.Count==0,"Ordinary Game round starts with empty runtime inventory and no preplaced traps");
  Check(world.Hold(local,"cart",new Vector3(0,1,0)),"Real local ghost acquires authoritative held inventory");
  yield return null;
  PressGhostKey(controls,Key.Q);yield return null;
  Check(local.HeldTrapFamily==""&&world.Count==0,"Actual Q input discards held item through ordinary ghost controller");
  world.Hold(local,"cart",new Vector3(0,1,0));Check(world.TryPlace(local,new Vector3(0,1,0),"cart"),"Real Game places enlarged authored cart on the floor");
  var records=JsonUtility.FromJson<GhostCubeSnapshot>(world.Snapshot);var trap=world.Trap(records.Cubes[0].Id);Set(controls,"_Position",trap.Position);world.Possess(local,trap.Index,trap.Position);
  yield return null;yield return null;
  Check(local.ControlledCube==trap.Index,"Real Game ghost possesses its owned cart");
  var foreground=FindFirstObjectByType<GhostTentaclePresentation>();
  Check(foreground&&foreground.GetComponent<Canvas>().sortingOrder>trap.GetComponentInChildren<Canvas>(true).sortingOrder,"Authored tentacle Canvas sorts in front of cart and aisle");
  CaptureWorld("08-enlarged-cart-tentacle-puff");
  yield return new WaitForSecondsRealtime(.18f);CaptureWorld("09-possession-cloud-burst");
  yield return new WaitForSecondsRealtime(.5f);CaptureWorld("11-cart-tentacle-after-puff");
  PressGhostKey(controls,Key.Q);yield return null;
  Check(world.Count==0&&local.ControlledCube==-1&&local.HeldTrapFamily=="","Actual Q input removes possessed cart and exits possession");
  yield return new WaitForSecondsRealtime(.6f);
  Check(FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None).Length==0,"All completed possession puffs dispose their runtime instances");
  Submit(button);yield return null;yield return null;
  Check(local.ControlMode==HotelControlMode.Fish,"Jump verification uses existing fish role");
  yield return new WaitForSecondsRealtime(.4f);
  var character=local.GetComponent<CharacterController>();Check(character.isGrounded,"Fish is grounded before jump");
  float baseY=local.transform.position.y,peakY=baseY,start=Time.unscaledTime;
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
  yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
  bool airborne=false;
  while(Time.unscaledTime-start<1.2f){peakY=Mathf.Max(peakY,local.transform.position.y);airborne|=!character.isGrounded;if(airborne&&character.isGrounded)break;yield return null;}
  float arc=peakY-baseY,flight=Time.unscaledTime-start;
  Check(airborne&&character.isGrounded&&arc>.2f&&arc<1.15f&&flight<.9f,"Short grounded jump lands: peak "+arc.ToString("F2")+" units, "+flight.ToString("F2")+" seconds");
  CaptureWorld("10-short-jump-landed");
  AuditGuardrails.Run(local,world,manager);
  File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"PASS actual Game entry, wheel/Ink attic/release, authored weighted Trap Manager, E package pickup, two independent conveyor carts, repeated possession/movement/unpossess, collision escape, owner filtering/rejection, stale-ID Q disposal, role button, tentacle, smoke and short jump");
  Debug.Log("ACTUAL_GAME_PASS");EditorApplication.Exit(0);
 }
 void PressGhostKey(GhostPlacementController controls,Key key)
 {
  if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.Update();
  controls.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(controls,null);
  InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
 }
 void CaptureWorld(string name)
 {
  var camera=HotelViewCamera.Current;var target=new RenderTexture(1280,720,24);var priorTarget=camera.targetTexture;camera.targetTexture=target;
  var canvases=new List<Canvas>();var modes=new List<RenderMode>();var cameras=new List<Camera>();var planes=new List<float>();
  // Project authored world art at the render target's size, rather than reusing the
  // previous GameView frame's pixel coordinates (which cropped the review image).
  foreach(var presentation in FindObjectsByType<GhostTrapAreaPresentation>(FindObjectsSortMode.None))presentation.GetType().GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(presentation,null);
  foreach(var foreground in FindObjectsByType<GhostTentaclePresentation>(FindObjectsSortMode.None))foreground.GetType().GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(foreground,null);
  var projected=new List<RectTransform>();
  foreach(var presentation in FindObjectsByType<GhostTrapAreaPresentation>(FindObjectsSortMode.None)){projected.Add(Get<RectTransform>(presentation,"_Area"));projected.Add(Get<RectTransform>(presentation,"_Cart"));}
  foreach(var foreground in FindObjectsByType<GhostTentaclePresentation>(FindObjectsSortMode.None))projected.Add(Get<Image>(foreground,"_Image").rectTransform);
  foreach(var puff in FindObjectsByType<PossessionSmoke>(FindObjectsSortMode.None))projected.Add(Get<RectTransform>(puff,"_Anchor"));
  var points=new List<Vector3>();foreach(var rect in projected)points.Add(rect.position);
  foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isRootCanvas&&canvas.isActiveAndEnabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay){canvases.Add(canvas);modes.Add(canvas.renderMode);cameras.Add(canvas.worldCamera);planes.Add(canvas.planeDistance);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
  Canvas.ForceUpdateCanvases();
  for(int i=0;i<projected.Count;i++){var canvas=projected[i].GetComponentInParent<Canvas>();if(RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform,points[i],camera,out var point))projected[i].position=point;}
  camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),pixels.EncodeToPNG());RenderTexture.active=prior;
  for(int i=0;i<canvases.Count;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
  for(int i=0;i<projected.Count;i++)projected[i].position=points[i];
  camera.targetTexture=priorTarget;target.Release();Destroy(target);Destroy(pixels);
 }
 void Submit(Button button){
  int active=0;foreach(var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))if(events.isActiveAndEnabled)active++;
  Check(active==1 && EventSystem.current && EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().isActiveAndEnabled,"Exactly one New Input System UI provider processes the role button");
  var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
  Check(module.submit && module.submit.action.enabled,"Authored UI submit action is bound and enabled");
  module.ActivateModule();EventSystem.current.SetSelectedGameObject(button.gameObject);if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
  // Hidden batch Editors do not reliably run navigation updates; drive the installed module with actual device events.
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));InputSystem.Update();Check(module.submit.action.WasPerformedThisFrame(),"Virtual keyboard submit reaches the actual UI action");module.Process();
  InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();

 }
 void Capture(Canvas canvas,string name){
  var camera=HotelViewCamera.Current;if(!camera)throw new Exception("Missing authored game camera");
  var target=new RenderTexture(1280,720,24);var priorTarget=camera.targetTexture;camera.targetTexture=target;
  var mode=canvas.renderMode;var priorCamera=canvas.worldCamera;var plane=canvas.planeDistance;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
  var prior=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
  RenderTexture.active=prior;canvas.renderMode=mode;canvas.worldCamera=priorCamera;canvas.planeDistance=plane;camera.targetTexture=priorTarget;target.Release();Destroy(target);Destroy(pixels);
 }
}
