#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HauntedFish.Theatre;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class TheatreNativeValidation
{
 const string Armed="Haunted.Theatre.Native.Armed",Folder="Tools/TheatreTests/Evidence";
 static TheatrePresentation _Presentation;static HotelPlayer _Player;static VideoPlayer _Video;static Keyboard _Keyboard;
 static int _Step,_Checks,_Exits;static double _Began;static Vector3 _Origin;static List<string> _Lines=new List<string>();
 static TheatreNativeValidation(){if(SessionState.GetBool(Armed,false))EditorApplication.update+=Tick;}
 static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);_Checks++;_Lines.Add("PASS "+label);}
 public static void Run()
 {
  if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--theatre-native-validation"))throw new Exception("Explicit owned batch validation required.");
  Directory.CreateDirectory(Folder);if(!File.Exists("Assets/Scenes/theatre.unity"))TheatreNativeAuthoring.Run();TheatreNativeAuthoring.AddPlayerReturnComponent();
  var lobby=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Single);
  try{Physics.SyncTransforms();var position=new Vector3(3.912f,1.1f,5.858f);var center=position+Vector3.up*.125f;var half=Vector3.up*(1.75f*.5f-.45f);var hits=Physics.OverlapCapsule(center-half,center+half,.45f,~0,QueryTriggerInteraction.Ignore);File.WriteAllText(Folder+"/return-clearance.txt","Candidate capsule overlap: "+string.Join(",",hits.Select(x=>x.name))+"\nFloor ray: "+Physics.Raycast(center,Vector3.down,out var floor,2,~0,QueryTriggerInteraction.Ignore)+" "+(floor.collider?floor.collider.name:"none")+"\nLobby remains unsaved; owner must bind and validate actual return.");}
  finally{EditorSceneManager.CloseScene(lobby,true);}
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/theatre.unity",OpenSceneMode.Single);
  var holder=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"),scene);holder.GetComponent<HotelPlayer>().Networked=false;holder.transform.position=new Vector3(2500,2,0);holder.name="Native validation owned nonnetwork player";
  SessionState.SetBool(Armed,true);SessionState.SetBool("Haunted.Theatre.Native.Background",Application.runInBackground);Application.runInBackground=true;
  EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 static void Tick()
 {
  if(!EditorApplication.isPlaying)return;
  try
  {
   if(_Step==0){_Presentation=UnityEngine.Object.FindFirstObjectByType<TheatrePresentation>();_Player=HotelPlayer.LocalPlayer;if(!_Presentation||!_Player)return;Check(_Presentation.ValidateAuthoredBindings(out string error),"complete authored presentation");Check(!_Presentation.GetComponentsInChildren<UnityEngine.Canvas>(true).Any(c=>c.enabled),"cinema has no enabled UI canvas");Check(_Presentation.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name.Contains("CinemaRow5")),"actual imported chair row has a mesh renderer");Check(!_Presentation.View.orthographic,"close perspective cinema camera");Check(!_Presentation.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.name.StartsWith("Seated")),"no fabricated audience players");Check(_Player.GetComponent<TheatreReturnRequest>(),"native compiled return behaviour present");_Video=Get<VideoPlayer>(_Presentation,"_Video");_Presentation.Open(_Player,null);Check(_Presentation.VisibleRealPlayerSilhouetteCount==1,"one actual player produces exactly one silhouette");_Presentation.ExitRequested+=()=>_Exits++;_Origin=Get<Transform>(_Presentation,"_Walker").localPosition;_Began=EditorApplication.timeSinceStartup;_Step=1;return;}
   if(EditorApplication.timeSinceStartup-_Began>30)throw new Exception("Native stage timeout "+_Step+" pose="+Get<Transform>(_Presentation,"_Walker").localPosition+" delta="+Time.unscaledDeltaTime+" current="+Keyboard.current+" a="+(_Keyboard!=null&&_Keyboard.aKey.isPressed));
   if(_Step==1||_Step==2)
   {
    if(!_Video.isPlaying||_Video.frame<2)return;
    Check(_Video.isPrepared&&_Video.width>0&&_Video.height>0,"real decoded production video stage "+_Step);Capture(_Step==1?"player-video.png":"ghost-video.png");
    if(_Step==1){_Presentation.GetType().GetMethod("OnEnd",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_Presentation,new object[]{_Video});Check(Get<int>(_Presentation,"_ClipIndex")==1,"fish completion handler automatically selects ghost");_Step=2;_Began=EditorApplication.timeSinceStartup;return;}
    _Presentation.GetType().GetMethod("OnEnd",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_Presentation,new object[]{_Video});Check(Get<int>(_Presentation,"_ClipIndex")==0,"ghost completion handler automatically selects fish");_Keyboard=InputSystem.AddDevice<Keyboard>();_Step=3;_Began=EditorApplication.timeSinceStartup;return;
   }
   if(_Step==3)
   {
    InputSystem.QueueStateEvent(_Keyboard,new KeyboardState(Key.D));InputSystem.Update();_Presentation.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_Presentation,null);if(EditorApplication.timeSinceStartup-_Began<.3)return;var moved=Get<Transform>(_Presentation,"_Walker").localPosition;Check(moved.x>_Origin.x&&moved.y==_Origin.y&&moved.z==_Origin.z,"held D moves only horizontally in close perspective");Capture("D-movement.png");Get<Transform>(_Presentation,"_Walker").localPosition=_Origin;_Step=5;_Began=EditorApplication.timeSinceStartup;return;
   }
   if(_Step==5)
   {
    InputSystem.QueueStateEvent(_Keyboard,new KeyboardState(Key.W,Key.Space));InputSystem.Update();if(EditorApplication.timeSinceStartup-_Began<.3)return;
    Check(Get<Transform>(_Presentation,"_Walker").localPosition==_Origin,"W and Space do not move or jump theatre actor");_Step=4;_Began=EditorApplication.timeSinceStartup;return;
   }
   if(_Step==4)
   {
    InputSystem.QueueStateEvent(_Keyboard,new KeyboardState(Key.A));InputSystem.Update();_Keyboard.MakeCurrent();
    _Presentation.GetType().GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(_Presentation,null);var pose=Get<Transform>(_Presentation,"_Walker").localPosition;if(_Checks<10)Check(pose.y==_Origin.y&&pose.z==_Origin.z,"A movement remains on authored lane");if(_Exits==0)return;
    Check(pose.y==_Origin.y&&pose.z==_Origin.z,"held A preserves authored lane Y and Z");Capture("A-left-exit.png");Check(_Exits==1&&pose.x<=-8.5f,"held A reaches far-left exit exactly once");_Presentation.Close();Check(!Get<bool>(_Presentation,"_Open")&&!_Video.isPlaying,"close stops video and presentation");
    _Presentation.Open(_Player,null);Check(Get<Transform>(_Presentation,"_Walker").localPosition==_Origin,"repeated entry resets actor safely");_Presentation.Close();_Player.gameObject.SetActive(false);Check(HotelPlayer.LocalPlayer==null,"disconnect/owned-player disable releases local identity");Complete(true,"native authored video and A/D/exit checks passed");
   }
  }
  catch(Exception error){Complete(false,error.ToString());}
 }
 static void Capture(string name)
 {
  var camera=_Presentation.View;var old=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
  try{target.Create();camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name,texture.EncodeToPNG());}
  finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);}
 }
 static void Complete(bool pass,string result)
 {
  SessionState.SetBool(Armed,false);EditorApplication.update-=Tick;if(_Keyboard!=null)InputSystem.RemoveDevice(_Keyboard);if(_Presentation)_Presentation.Close();Application.runInBackground=SessionState.GetBool("Haunted.Theatre.Native.Background",false);_Lines.Insert(0,(pass?"PASS":"FAIL")+" theatre native checks="+_Checks+" "+result);File.WriteAllLines(Folder+"/native-results.txt",_Lines);Debug.Log(_Lines[0]);EditorApplication.Exit(pass?0:1);
 }
}
#endif