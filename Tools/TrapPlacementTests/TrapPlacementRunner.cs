using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
public sealed class TrapPlacementRunner : MonoBehaviour
{
    int checks; string folder; Mouse mouse;
    static T Get<T>(object o,string f)=>(T)o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
    static void Call(object o,string m)=>o.GetType().GetMethod(m,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
    void Check(bool v,string t){if(!v)throw new Exception(t);checks++;Debug.Log("PLACEMENT_CHECK: "+t);}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Helper.unity");EditorApplication.EnterPlaymode();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Monitor(){if(Environment.GetCommandLineArgs().Contains("--trap-placement")){var g=new GameObject("Isolated trap placement observer");DontDestroyOnLoad(g);g.AddComponent<TrapPlacementRunner>();}}
    IEnumerator Start(){folder=System.IO.Path.Combine(Application.dataPath,"../TrapPlacementEvidence");Directory.CreateDirectory(folder);InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;mouse=InputSystem.AddDevice<Mouse>();var e=Tests();while(true){object n;try{if(!e.MoveNext())break;n=e.Current;}catch(Exception x){Debug.LogException(x);File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"FAIL "+x);EditorApplication.Exit(1);yield break;}yield return n;}File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"PASS "+checks+" native authoritative placement/preview checks");Debug.Log("PLACEMENT_PASS "+checks);EditorApplication.Exit(0);}
    IEnumerator Tests()
    {
        float deadline=Time.unscaledTime+90;HauntedHotelMultiplayer session=null;LobbyManager lobby=null;
        while(Time.unscaledTime<deadline){session=FindFirstObjectByType<HauntedHotelMultiplayer>();lobby=FindFirstObjectByType<LobbyManager>();if(session&&session.ReadyToPlay&&lobby&&HotelPlayer.LocalPlayer)break;yield return null;}
        Check(session&&lobby&&session.ReadyToPlay,"Real native host establishes existing Lobby");
        if(lobby.IntroductionPlaying)lobby.SkipIntroduction();yield return null;yield return null;if(lobby.IntroductionPlaying)lobby.SkipIntroduction();
        var player=HotelPlayer.LocalPlayer;var stairs=Get<LobbyTrigger>(lobby,"_StairZone").GetComponent<Collider>();player.Teleport(stairs.bounds.center-player.BodyController.center);Physics.SyncTransforms();
        var start=Get<Button>(FindFirstObjectByType<MemberMenu>(FindObjectsInactive.Include),"_Start");
        while(session.SceneTravel.TargetScene=="Lobby"&&!start.interactable&&Time.unscaledTime<deadline)yield return null;
        if(session.SceneTravel.TargetScene=="Lobby")start.onClick.Invoke();
        while((!GameSceneController.Current||!player.GhostSetupReady)&&Time.unscaledTime<deadline)yield return null;
        var scene=GameSceneController.Current;Check(scene&&player.ControlMode==HotelControlMode.Ghost&&player.GhostSetupReady,"Actual random intro releases authoritative ghost setup without bypass");
        var world=scene.PlacementWorld;var controls=world.Controls;yield return null;yield return null;
        var cart=world.Definition("cart");var chandelier=world.Definition("chandelier");
        Check(world.HasGraceZone&&Mathf.Abs(world.GraceBounds.min.x+15)<.001f&&Mathf.Abs(world.GraceBounds.max.x+2)<.001f,"Serialized grace area covers hallway start through X=-2");
        for(int i=0;i<4;i++)Check(world.GraceBounds.Contains(new Vector3(scene.SpawnPosition(i).x,1,0)),"Authored player spawn "+i+" is inside grace area");
        Check(cart&&chandelier&&cart.PlacementMode==TrapPlacementMode.Floor&&chandelier.PlacementMode==TrapPlacementMode.HallwayCeiling,"Catalog preserves floor cart and authored ceiling chandelier modes");
        Check(!world.PlacementPoint(new Vector3(-10,1,0),"cart",out _),"Cart placement inside grace area is invalid");
        float edge=world.GraceBounds.max.x+cart.BodyHalfSize.x;
        Check(!world.PlacementPoint(new Vector3(edge-.002f,1,0),"cart",out _),"Full cart footprint cannot straddle grace boundary");
        Debug.Log("PLACEMENT_GEOMETRY floor="+scene.HallwayBounds+" ceiling="+scene.HallwayCeilingY+" grace="+world.GraceBounds+" half="+cart.BodyHalfSize);
        foreach(var hit in Physics.OverlapBox(new Vector3(edge,1,0),cart.HalfExtents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))Debug.Log("PLACEMENT_OBSTACLE "+hit.name+" "+hit.bounds);
        Check(world.PlacementPoint(new Vector3(edge,1,0),"cart",out var cartPoint),"Exact footprint contact outside safe edge is valid");
        Check(!world.PlacementPoint(new Vector3(14,1,0),"cart",out _)&&!world.PlacementPoint(new Vector3(float.NaN,1,0),"cart",out _),"Hallway end overflow and malformed placement are rejected");
        Set(controls,"_Position",new Vector3(-10,1,0));Check(world.Hold(player,"cart",new Vector3(-10,1,0)),"Native authority holds real cart inventory");
        player.RequestPlaceHeldTrap(new Vector3(-10,1,0),"cart",player.RoundStateKey,player.RoundVersion);
        Check(!player.GhostPlacementAccepted&&world.Count==0&&player.HeldTrapFamily=="cart","Owned placement RPC cannot bypass grace restriction or consume inventory");
        Set(controls,"_Position",cartPoint);player.RequestPlaceHeldTrap(cartPoint,"cart","stale",player.RoundVersion);
        Check(!player.GhostPlacementAccepted&&world.Count==0,"Stale-round placement RPC is rejected");
        player.RequestPlaceHeldTrap(cartPoint,"chandelier",player.RoundStateKey,player.RoundVersion);
        Check(!player.GhostPlacementAccepted&&world.Count==0,"Forged held-family placement is rejected");
        player.RequestPlaceHeldTrap(cartPoint,"cart",player.RoundStateKey,player.RoundVersion);
        Check(player.GhostPlacementAccepted&&world.Count==1,"Normal cart placement through current owned RPC succeeds outside grace");
        var placedCart=FindObjectsByType<GhostTrap>(FindObjectsSortMode.None).Single(t=>t.World==world&&t.FamilyTag=="cart");
        Physics.SyncTransforms();Check(Mathf.Abs(placedCart.CollisionBounds.min.y-scene.HallwayBounds.max.y)<.001f,"Normal cart retains floor-aligned collider");
        placedCart.AcceptInput(new TrapInput(TrapInputKind.Move,-1),Time.unscaledTime);placedCart.Simulate(10,Time.unscaledTime);Physics.SyncTransforms();
        Check(placedCart.CollisionBounds.min.x>=world.GraceBounds.max.x-.001f,"Large cart movement cannot enter the player grace area");
        Set(controls,"_Position",placedCart.Position);world.Possess(player,placedCart.Index,placedCart.Position);world.Dispose(player,placedCart.Index);yield return null;yield return new WaitForSecondsRealtime(.21f);
        Check(world.PlacementPoint(new Vector3(6,3.8f,9),"chandelier",out var basePoint),"Ceiling trap targets hallway without exact depth or floor selection");
        Check(Mathf.Abs(basePoint.y+chandelier.ArmedOffset.y+chandelier.BodyHalfSize.y-scene.HallwayCeilingY)<.001f,"Chandelier armed body derives exact ceiling anchor");
        Check(Mathf.Abs(basePoint.y-chandelier.BodyHalfSize.y-scene.HallwayBounds.max.y)<.001f,"Chandelier landing origin derives exact floor anchor");
        Check(!world.PlacementPoint(new Vector3(edge-.002f,3,0),"chandelier",out _),"Ceiling trap footprint obeys same safe-zone boundary");
        Set(controls,"_Position",new Vector3(6,3,0));Check(world.Hold(player,"chandelier",new Vector3(6,3,0)),"Real ceiling-trap inventory is held");
        Check(world.PlacementNearFlight(player,basePoint,"chandelier"),"Ceiling mode accepts ghost at ordinary hallway height");
        Set(controls,"_Position",new Vector3(6,12,0));Check(!world.PlacementNearFlight(player,basePoint,"chandelier"),"Ghost above hallway cannot remotely place into it");Set(controls,"_Position",new Vector3(6,3,0));
        Check(!world.PlacementNearFlight(player,new Vector3(14,basePoint.y,0),"chandelier"),"Ceiling pointer reach is authority bounded");
        var camera=HotelViewCamera.Current;var oldRot=camera.transform.rotation;float oldZoom=camera.orthographicSize;camera.transform.position=new Vector3(6,2,camera.transform.position.z);
        camera.transform.rotation=oldRot*Quaternion.Euler(0,0,8);camera.orthographicSize=oldZoom*.8f;
        var aim=new Vector3(6,2,0);var screen=camera.WorldToScreenPoint(aim);
        Check(GhostPlacementController.HallwayPointer(camera,screen,out var pointed)&&Vector3.Distance(pointed,aim)<.002f,"Pointer-to-hallway plane remains exact under camera roll and zoom");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});InputSystem.Update();Set(controls,"_Pending",false);Call(controls,"Update");
        var preview=Get<GameObject>(controls,"_Preview");var previewSprite=Get<SpriteRenderer>(controls,"_PreviewSprite");
        Check(preview.activeInHierarchy&&previewSprite.color==HotelPalette.Light&&Mathf.Abs(preview.transform.position.y+chandelier.BodyHalfSize.y-scene.HallwayCeilingY)<.002f,"Actual mouse preview is valid and ceiling snapped under camera effects");
        Check(Mathf.Abs(previewSprite.transform.localScale.x-previewSprite.transform.localScale.y)<.0001f,"Ceiling preview preserves artwork aspect ratio");Capture(camera,"01-valid-ceiling-preview-rolled");
        var blockedScreen=camera.WorldToScreenPoint(new Vector3(-1,2,0));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(blockedScreen.x,blockedScreen.y)});InputSystem.Update();Set(controls,"_Position",new Vector3(-1,2,0));Call(controls,"Update");
        Check(previewSprite.color==HotelPalette.Rust,"Actual mouse preview rejects safe-zone footprint overlap");Capture(camera,"02-invalid-grace-preview");
        camera.transform.rotation=oldRot;camera.orthographicSize=oldZoom;Set(controls,"_Position",new Vector3(6,2,0));
        player.RequestPlaceHeldTrap(new Vector3(6,12,0),"chandelier",player.RoundStateKey,player.RoundVersion);Check(!player.GhostPlacementAccepted&&world.Count==0,"Forged vertical placement RPC cannot bypass canonical anchors");
        player.RequestPlaceHeldTrap(basePoint,"chandelier",player.RoundStateKey,player.RoundVersion);Check(player.GhostPlacementAccepted&&world.Count==1,"Native ceiling placement succeeds at hallway height");
        yield return null;var placed=FindObjectsByType<GhostTrap>(FindObjectsSortMode.None).Single(t=>t.World==world&&t.FamilyTag=="chandelier");Physics.SyncTransforms();
        Check(Mathf.Abs(placed.CollisionBounds.max.y-scene.HallwayCeilingY)<.002f,"Actual instantiated chandelier collider aligns to ceiling");
        camera.transform.position=new Vector3(6,2,camera.transform.position.z);
        var art=placed.GetComponent<GhostTrapAreaPresentation>();Call(art,"LateUpdate");var rect=Get<RectTransform>(art,"_Cart");
        var corners=new Vector3[4];rect.GetWorldCorners(corners);var top=(corners[1]+corners[2])*.5f;var expected=camera.WorldToScreenPoint(new Vector3(6,scene.HallwayCeilingY,0));
        Check(Vector2.Distance(top,expected)<.1f,"Actual chandelier artwork top shares ceiling anchor");CaptureWorld("03-authored-ceiling-trap");
        Check(placed.AcceptInput(new TrapInput(TrapInputKind.Activate),Time.unscaledTime),"Existing Space activation remains functional");placed.Simulate(1,Time.unscaledTime);Physics.SyncTransforms();
        Check(placed.Phase==TrapPhase.Latched&&Mathf.Abs(placed.CollisionBounds.min.y-scene.HallwayBounds.max.y)<.05f,"Ceiling mode falls to floor using derived travel and existing collision");
        Call(art,"LateUpdate");rect.GetWorldCorners(corners);var artBottom=(corners[0]+corners[3])*.5f;var floorPixel=camera.WorldToScreenPoint(new Vector3(6,placed.CollisionBounds.min.y,0));
        Check(Vector2.Distance(artBottom,floorPixel)<.1f,"Uniformly fitted chandelier artwork lands at collider foot rather than clipping below floor");CaptureWorld("05-chandelier-floor-landing");
        Check(placed.AcceptInput(new TrapInput(TrapInputKind.Reset),Time.unscaledTime)&&Mathf.Abs(placed.CollisionBounds.max.y-scene.HallwayCeilingY)<.002f,"Existing reset restores derived ceiling anchor");
        Set(controls,"_Position",placed.Position);world.Possess(player,placed.Index,placed.Position);world.Dispose(player,placed.Index);yield return null;Check(world.Count==0,"Placement validation cleans up all real trap inventory");
        var stairsRoot=scene.gameObject.scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Hallway end stairs to ghost challenge");
        Check(stairsRoot&&stairsRoot.GetComponentsInChildren<BoxCollider>().Length==4,"Four actual serialized stair treads lead to hallway-end entrance");
        float releaseDeadline=Time.unscaledTime+20;while(!player.RoundReleased&&Time.unscaledTime<releaseDeadline)yield return null;
        player.SwitchEditorRole();yield return null;yield return null;
        player.Teleport(new Vector3(11.2f,0,0));var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));InputSystem.Update();
        yield return new WaitForSecondsRealtime(1.2f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
        Check(player.ControlMode==HotelControlMode.Fish&&player.transform.position.x>12.5f&&player.BodyController.bounds.min.y>.15f,"Native horizontal fish movement climbs authored stair steps");
        camera.transform.position=new Vector3(13.2f,2,camera.transform.position.z);camera.orthographicSize=3.5f;CaptureWorld("04-stairs-and-editor-dev-controls");

    }
    void Capture(Camera camera,string name){var target=new RenderTexture(1280,720,24);var prev=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),pixels.EncodeToPNG());RenderTexture.active=active;camera.targetTexture=prev;target.Release();Destroy(target);Destroy(pixels);}
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
}
