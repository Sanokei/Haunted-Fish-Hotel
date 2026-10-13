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
public static class GameHallwayNativeValidation
{
 const string Folder="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/active-trap-affected-checks";
 const string Marker="GameHallwayNativeValidation.Play";
 static List<string> lines=new List<string>();
 static void Check(bool ok,string text){lines.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
 static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static void Owned(){if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-hallway-native"))throw new Exception("Explicit owned batch required");}
 public static void Run()
 {
  Owned();Directory.CreateDirectory(Folder);Scene preview=default;
  try
  {
   preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");var roots=preview.GetRootGameObjects();
   var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
   var game=roots.SelectMany(r=>r.GetComponentsInChildren<GameSceneController>(true)).Single();
   Check(Mathf.Abs(game.HallwayBounds.min.x+15)<.001f&&Mathf.Abs(game.HallwayBounds.max.x-53)<.001f,"native floor bounds -15 to 53");
   Check(Mathf.Abs(game.HallwayCeilingY-4.11f)<.001f,"native ceiling remains 4.11");
   var walls=all.Where(t=>t.name.StartsWith("Hallway wall v1-")).OrderBy(t=>t.position.x).ToArray();
   Check(walls.Length==6,"six full imported wall panels");
   float previous=-15;
   for(int i=0;i<6;i++){var sr=walls[i].GetComponent<SpriteRenderer>();Check(sr.sprite&&sr.sprite.name=="HALLWAY-Wall-v1-"+(i/2+1),"panel ordering "+i);Check(Mathf.Abs(sr.bounds.min.x-previous)<.002f&&Mathf.Abs(sr.bounds.size.y-4.11f)<.002f,"panel dimensions/seam "+i);previous=sr.bounds.max.x;}
   Check(all.Count(t=>t.name.Contains("Door top mesh attachment"))==13,"thirteen authored crown sockets");
   Check(all.Count(t=>t.name.StartsWith("Panel ")&&t.name.Contains("Sconce"))==20,"twenty authored sconces");
   Check(all.Count(t=>t.name.StartsWith("Panel ")&&t.name.EndsWith(" - Table"))==1&&all.Count(t=>t.name.StartsWith("Panel ")&&t.name.EndsWith(" - Fishbowl"))==1,"exactly one authored table and bowl");
   var table=all.Single(t=>t.name.StartsWith("Panel ")&&t.name.EndsWith(" - Table")).GetComponent<SpriteRenderer>();Check(table.bounds.min.x>=-15&&table.bounds.max.x<=-2,"only table fits unchanged safe hallway");
   var landing=all.Single(t=>t.name=="Attic stairwell landing - v1-3 continuation").GetComponent<SpriteRenderer>();Check(Mathf.Abs(landing.bounds.min.x-previous)<.002f&&Mathf.Abs(landing.bounds.max.x-53)<.002f,"landing wall covers final seam through endpoint");
   Check(Mathf.Abs(game.ClampGhost(new Vector3(100,100,0)).x-53)<.001f&&Mathf.Abs(game.ClampGhost(new Vector3(-100,1,0)).x+15)<.001f,"production ghost clamping uses doubled boundaries");
   var entrance=all.Single(t=>t.name=="BossFight entrance - hallway end");
   Check(Mathf.Abs(entrance.position.x-51.8f)<.001f,"end-entry follows new landing");
   var world=roots.SelectMany(r=>r.GetComponentsInChildren<GhostPlacementWorld>(true)).Single();
   Check(world.HasGraceZone&&Vector3.Distance(world.GraceBounds.center,new Vector3(-8.5f,2.055f,0))<.001f&&Vector3.Distance(world.GraceBounds.size,new Vector3(13,4.11f,2))<.001f,"original safe hallway collider dimensions preserved");
   var cursor=all.Single(t=>t.name=="Ghost - tentacle");var sprite=cursor.GetComponent<SpriteRenderer>();
   Check(sprite.sprite&&AssetDatabase.GetAssetPath(sprite.sprite)=="Assets/Art/BOSS-cursor.png","imported cursor sprite resolves authored binding");
   Check(Vector3.Distance(cursor.localScale,new Vector3(.56f,.56f,.56f))<.001f&&Vector3.Distance(cursor.localPosition,new Vector3(-8,2.7f,0))<.001f,"cursor authored scale/position preserved");
   Check(Mathf.Abs(sprite.bounds.size.y-1.4f)<.001f&&Mathf.Abs(sprite.bounds.size.x-1.176f)<.001f,"cursor native sprite dimensions");
   var foreground=all.Single(t=>t.name=="Tentacle foreground").GetComponent<Image>();Check(foreground.sprite==sprite.sprite&&!foreground.raycastTarget,"foreground source sprite and click-through binding");
   var emergence=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GhostEmergence.prefab");Check(emergence.GetComponentsInChildren<Image>(true).Any(x=>x.sprite&&AssetDatabase.GetAssetPath(x.sprite)=="Assets/Art/Fish-ver-ghost.png"),"player ghost artwork imports into shared emergence prefab");
   var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Main Camera");
   camera.scene=preview;camera.orthographic=true;camera.backgroundColor=new Color(.02f,.02f,.03f);camera.clearFlags=CameraClearFlags.SolidColor;
   foreach(var x in new[]{-10f,5f,20f,35f,49f}){camera.transform.position=new Vector3(x,2.055f,-10);camera.transform.rotation=Quaternion.identity;camera.orthographicSize=3.8f;Render(camera,"hallway-x"+x,960,540);}
   camera.transform.position=new Vector3(19,2.055f,-10);camera.orthographicSize=2.8f;Render(camera,"hallway-68-units",3200,256);
   File.WriteAllLines(Folder+"/edit-results.txt",lines);
  }
  catch(Exception e){File.WriteAllText(Folder+"/result.txt","FAIL native edit validation\n"+string.Join("\n",lines)+"\n"+e);if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);EditorApplication.Exit(1);return;}
  if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
  try{TrapPlacementIsolatedValidation.Run();File.Copy(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"hfh-trap-validation/native-result.txt"),Folder+"/trap-results.txt",true);}
  catch(Exception e){File.WriteAllText(Folder+"/trap-results.txt","FAIL "+e);}
  EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);
  SessionState.SetInt(Marker,1);SessionState.SetFloat(Marker+"Deadline",(float)EditorApplication.timeSinceStartup+90);
  EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 public static void Overlay()
 {
  Owned();Directory.CreateDirectory(Folder);Scene preview=default;GhostPlacementController controls=null;RenderTexture target=null;
  try
  {
   preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");var roots=preview.GetRootGameObjects();var game=roots.SelectMany(r=>r.GetComponentsInChildren<GameSceneController>(true)).Single();controls=roots.SelectMany(r=>r.GetComponentsInChildren<GhostPlacementController>(true)).Single();controls.Configure(null,game);
   var hud=(GameObject)Get(controls,"_Hud");hud.SetActive(true);var canvas=hud.GetComponentInParent<Canvas>();canvas.enabled=true;
   var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Main Camera");camera.scene=preview;camera.transform.SetPositionAndRotation(new Vector3(-8.5f,2.055f,-10),Quaternion.identity);camera.orthographic=true;camera.orthographicSize=3.8f;
   target=new RenderTexture(960,540,24);target.Create();camera.targetTexture=target;camera.aspect=960f/540;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
   Call(controls,"ShowForbiddenArea",camera);var area=(Image)Get(controls,"_ForbiddenArea");Check(area&&area.gameObject.activeInHierarchy&&area.color.r>.9f&&!area.raycastTarget,"actual forbidden presenter shows red click-through area");Check(area.rectTransform.rect.width>100&&area.rectTransform.rect.height>100,"area is a hallway-sized overlay");Canvas.ForceUpdateCanvases();Render(camera,"forbidden-hallway",960,540);
   Call(controls,"HideForbiddenArea");Check(!area.gameObject.activeSelf,"actual presenter clears overlay on hide");File.WriteAllLines(Folder+"/overlay-results.txt",lines.Concat(new[]{"LIMIT: direct production presenter invocation in isolated preview; actual OS placement/focus/ESC not exercised"}));
  }
  catch(Exception e){File.WriteAllText(Folder+"/overlay-results.txt","FAIL "+e);EditorApplication.Exit(1);return;}
  finally{if(controls){var area=(Image)Get(controls,"_ForbiddenArea");controls.GetType().GetField("_ForbiddenArea",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controls,null);if(area)UnityEngine.Object.DestroyImmediate(area.gameObject);}if(target)UnityEngine.Object.DestroyImmediate(target);if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);}
  EditorApplication.Exit(0);
 }
 static void Render(Camera camera,string name,int width,int height)
 {
  var rt=new RenderTexture(width,height,24);rt.Create();var texture=new Texture2D(width,height,TextureFormat.RGB24,false);var active=RenderTexture.active;
  try{camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());}
  finally{camera.targetTexture=null;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
 }
 [InitializeOnLoadMethod] static void Resume(){if(SessionState.GetInt(Marker,0)>0){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static void Tick()
 {
  int phase=SessionState.GetInt(Marker,0);if(phase==0)return;Owned();
  if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Marker+"Deadline",0)){Finish(false,"Play smoke timeout");return;}
  if(phase==1&&EditorApplication.isPlaying){SessionState.SetFloat(Marker+"At",(float)EditorApplication.timeSinceStartup);SessionState.SetInt(Marker,2);}
  if(phase==2&&EditorApplication.isPlaying&&EditorApplication.timeSinceStartup-SessionState.GetFloat(Marker+"At",0)>4)
  {
   try{Check(GameSceneController.Current,"production Game controller active in real Play mode");Check(HotelViewCamera.Current,"production view camera active in real Play mode");Check(GameSceneController.Current.HallwayBounds.max.x==53,"real Play scene uses extended hallway bounds");File.WriteAllLines(Folder+"/play-results.txt",lines);SessionState.SetInt(Marker,3);EditorApplication.ExitPlaymode();}
   catch(Exception e){Finish(false,e.ToString());}
  }
  if(phase==3&&!EditorApplication.isPlayingOrWillChangePlaymode)Finish(true,"native import/render and real Game Play smoke complete; input focus/ESC, network round, countdown and full transition acceptance require interactive validation");
 }
 static void Finish(bool pass,string text){SessionState.SetInt(Marker,0);EditorApplication.update-=Tick;File.WriteAllText(Folder+"/result.txt",(pass?"PASS ":"FAIL ")+text);EditorApplication.Exit(pass?0:1);}
}
#endif
