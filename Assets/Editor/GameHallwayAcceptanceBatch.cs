#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HauntedFish.Multiplayer;
public static class GameHallwayAcceptanceBatch
{
 const string Folder="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/game-final-native";
 const string Key="GameHallwayAcceptanceBatch.Armed";
 static readonly List<string> notes=new List<string>();static readonly HashSet<int> digits=new HashSet<int>();
 static readonly List<string> travel=new List<string>();static double captureAt;static int captures;static bool ready;static float setupAt;
 static BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
 static void Check(bool ok,string text){notes.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
 static void Owned(){if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-hallway-acceptance"))throw new Exception("Explicit batch only");}
 public static void Run()
 {
  Owned();Directory.CreateDirectory(Folder);Scene preview=default;
  try
  {
   preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");var roots=preview.GetRootGameObjects();
   var authored=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
   var far=authored.Single(t=>t.name=="Hallway far-end boundary").GetComponent<BoxCollider>();var start=authored.Single(t=>t.name=="Hallway start boundary").GetComponent<BoxCollider>();
   Check(Mathf.Abs(start.bounds.max.x+15)<.002f&&Mathf.Abs(far.bounds.min.x-53)<.002f,"actual traversal collider faces match -15/53 endpoints");
   var stair=authored.Single(t=>t.name.StartsWith("Hallway end stairs"));Check(Mathf.Abs(stair.position.x-51.8f)<.002f&&stair.GetComponentsInChildren<BoxCollider>().All(c=>c.bounds.max.x<=53.002f),"authored stair colliders fit relocated landing");
   var entry=authored.Single(t=>t.name=="BossFight entrance - hallway end").GetComponent<BoxCollider>();Check(entry.isTrigger&&Mathf.Abs(entry.bounds.max.x-53)<.002f,"Boss trigger spans relocated stairs through new endpoint");
   var game=roots.SelectMany(r=>r.GetComponentsInChildren<GameSceneController>(true)).Single();var world=roots.SelectMany(r=>r.GetComponentsInChildren<GhostPlacementWorld>(true)).Single();
   var gameField=typeof(GameSceneController).GetField("<Current>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);var oldGame=gameField.GetValue(null);
   try{gameField.SetValue(null,game);world.InjectDefinitions(new[]{AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/ShoppingCartTrap.prefab").GetComponent<GhostTrap>(),AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traps/FallingChandelierTrap.prefab").GetComponent<GhostTrap>()});world.BeginRound("native-double","double-layout",1);Check(world.PlacementPoint(new Vector3(45,1,0),"cart",out _),"production canonical trap preview accepts extended hallway x45");Check(!world.PlacementPoint(new Vector3(-10,1,0),"cart",out _),"production canonical trap preview still rejects original safe hallway");}
   finally{gameField.SetValue(null,oldGame);}
   var lightning=roots.SelectMany(r=>r.GetComponentsInChildren<LightningEffectManager>(true)).Single();
   lightning.gameObject.SetActive(true);Check(lightning.ValidateAuthoredBindings(out var error),"shared storm bindings "+error);Check((bool)Call(lightning,"Prepare"),"shared owner prepares extended hallway");
   var bindings=roots.SelectMany(r=>r.GetComponentsInChildren<GameStormSurfaceBindings>(true)).Single();Check(bindings.Windows.Length==17&&bindings.OppositeWallProjections.Length==17,"seventeen native window/projector pairs");Call(lightning,"UseWindowMasks");
   var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Main Camera");camera.scene=preview;camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=3.8f;
   foreach(float x in new[]{-10f,10f,30f,49f})
   {
    camera.transform.position=new Vector3(x,2.055f,-10);Call(lightning,"StopVisuals");var before=Capture(camera,"storm-off-x"+x,false);Call(lightning,"ApplyPulse",2f);var after=Capture(camera,"storm-on-x"+x,false);int changed=0;
    for(int i=0;i<before.Length;i++)if(Math.Abs(after[i].r-before[i].r)+Math.Abs(after[i].g-before[i].g)+Math.Abs(after[i].b-before[i].b)>12)changed++;
    Check(changed>100,"projected strike changes actual hallway pixels at x="+x+" count="+changed);
   }
   Check(bindings.Windows.All(w=>!w.enabled),"source window masks remain invisible");lightning.StopAmbient();
   File.WriteAllLines(Folder+"/storm-results.txt",notes);
  }
  catch(Exception e){if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);Finish(false,e.ToString());return;}
  if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
  SessionState.SetInt(Key,1);SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+100);
  EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 [InitializeOnLoadMethod] static void Resume(){if(SessionState.GetInt(Key,0)>0){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static Color32[] Capture(Camera camera,string name,bool ui)
 {
  var rt=new RenderTexture(1280,720,24);rt.Create();var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
  var screen=ui?UnityEngine.Object.FindFirstObjectByType<HotelConnectionScreen>():null;var connection=screen?(CanvasGroup)Get(screen,"_Overlay"):null;float connectionAlpha=connection?connection.alpha:0;
  var canvases=ui?UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray():Array.Empty<Canvas>();
  try{if(connection)connection.alpha=0;camera.targetTexture=rt;camera.aspect=1280f/720;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}Canvas.ForceUpdateCanvases();
   if(name=="transition-00"){
    var art=UnityEngine.Object.FindFirstObjectByType<AtticTransitionAssets>();
    foreach(var fish in art.IntroFish){var corners=new Vector3[4];fish.Root.GetWorldCorners(corners);var points=corners.Select(p=>camera.WorldToScreenPoint(p)).ToArray();Check(points.Min(p=>p.x)>=0&&points.Max(p=>p.x)<=1280&&points.Min(p=>p.y)>=0&&points.Max(p=>p.y)<=720,"intro fish wholly framed "+fish.Root.name);}
   }
   camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());return texture.GetPixels32();}
  finally{if(connection)connection.alpha=connectionAlpha;foreach(var c in canvases)if(c){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
 }
 static void Tick()
 {
  int phase=SessionState.GetInt(Key,0);if(phase==0)return;Owned();
  try
  {
   if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"Deadline",0))throw new Exception("Timeout phase="+phase+" "+(HotelPlayer.LocalPlayer?"cinematic="+HotelPlayer.LocalPlayer.RoundCinematicComplete+" blocked="+HotelPlayer.LocalPlayer.InputBlocked:"no local actor"));
   if(phase==3){if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish(true,"offline single-client native storm pixels, production transition, Ready and authoritative countdown accepted; actual focus/ESC and connected multiplayer not executed");return;}
   if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
   var actor=HotelPlayer.LocalPlayer;if(!actor)return;var game=GameSceneController.Current;var selection=UnityEngine.Object.FindFirstObjectByType<GhostSelectionPresentation>();
   var attic=selection?Get(selection,"_Attic") as Component:null;
   if(attic)
   {
    var assets=attic.GetComponent<AtticTransitionAssets>();
    if(!notes.Any(n=>n.Contains("four saved intro fish visible"))) Check(assets&&assets.IntroFish.Length==4&&assets.IntroFish.All(f=>f&&f.gameObject.activeInHierarchy),"four saved intro fish visible during production transition");
    var hotel=attic.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="Hotel camera strip");
    travel.Add(Time.unscaledTime.ToString("F4")+","+hotel.anchoredPosition.x.ToString("F4")+","+hotel.anchoredPosition.y.ToString("F4"));
    if(EditorApplication.timeSinceStartup>captureAt&&captures<24){captureAt=EditorApplication.timeSinceStartup+.75;Capture(HotelViewCamera.Current,"transition-"+captures.ToString("D2"),true);captures++;}
   }
   if(!ready&&actor.RoundCinematicComplete)
   {
    Check(!actor.RoundReleased,"cinematic alone does not unlock round");var page=UnityEngine.Object.FindFirstObjectByType<RoundHowToPage>();var button=page?(Button)Get(page,"_Continue"):null;
    if(button&&button.interactable){Capture(HotelViewCamera.Current,"authored-ready-page",true);button.onClick.Invoke();notes.Add("AUTHORIZATION: invoked authored interactable Ready button programmatically; no OS input claimed");ready=true;setupAt=Time.unscaledTime;}
   }
   var grace=game?game.GetComponentInChildren<GraceCountdownPresentation>(true):null;
   if(grace&&grace.Digit.gameObject.activeInHierarchy&&int.TryParse(grace.Digit.text,out int digit)&&digits.Add(digit))
   {Check(!actor.RoundReleased&&Mathf.Abs(digit-Mathf.Ceil(actor.GhostSetupRemaining))<=1,"visible digit mirrors authoritative remaining time: "+digit);Capture(HotelViewCamera.Current,"countdown-"+digit,true);}
   if(ready&&actor.RoundReleased)
   {
    Check(digits.SetEquals(new[]{5,4,3,2,1}),"actual native countdown shows all five digits");Check(Time.unscaledTime-setupAt>=9.5f,"existing ten-second ghost setup preserved");Check(actor.RoundReady&&actor.RoundIntroComplete,"release follows explicit readiness");Check(captures>2&&travel.Count>4,"real production transition sampled in Play");
    var xs=travel.Select(row=>float.Parse(row.Split(',')[1])).ToArray();Check(xs.Max()-xs.Min()<.02f,"descent remains vertical with fixed authored horizontal framing");notes.Add("PIXEL LIMIT: capture temporarily excludes the unconnected session overlay; restore immediately; not a connected/OS screenshot");
    Check(grace&&!grace.Digit.gameObject.activeInHierarchy,"countdown clears at authoritative release");Capture(HotelViewCamera.Current,"released-gameplay",true);File.WriteAllLines(Folder+"/travel.csv",new[]{"time,x,y"}.Concat(travel));File.WriteAllLines(Folder+"/play-results.txt",notes);SessionState.SetInt(Key,3);EditorApplication.ExitPlaymode();
   }
  }
  catch(Exception e){Finish(false,e.ToString());}
 }
 static void Finish(bool pass,string text){SessionState.SetInt(Key,0);EditorApplication.update-=Tick;File.WriteAllText(Folder+"/result.txt",(pass?"PASS ":"FAIL ")+text+"\n"+string.Join("\n",notes));EditorApplication.Exit(pass?0:1);}
}
#endif
