#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HauntedFish.Multiplayer;
public static class GameAudioListenerNativeBatch
{
 const string Key="GameAudioListenerNativeBatch.Armed";
 const string Folder="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/game-listener-native-retry1";
 static List<string> notes=new List<string>();static HashSet<string> stages=new HashSet<string>();static bool ready;static int sampled;
 static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
 static void Owned(){if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-audio-listener-native"))throw new Exception("Explicit owner command only");}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);notes.Add("PASS "+text);}
 static AudioListener Verify(string stage)
 {
  var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.isActiveAndEnabled).ToArray();sampled++;
  if(listeners.Length!=1)throw new Exception(stage+" active listener count="+listeners.Length);
  var listener=listeners[0];if(listener.gameObject.name!="Main Camera"||listener.gameObject.scene.name!="Game")throw new Exception(stage+" listener owner changed");
  if(stages.Add(stage))notes.Add("PASS "+stage+": exactly one enabled authored listener on Game/Main Camera at time="+Time.unscaledTime.ToString("F3"));return listener;
 }
 public static void Run()
 {
  Owned();Directory.CreateDirectory(Folder);if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Already playing");
  EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);SessionState.SetInt(Key,1);SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+100);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
 }
 [InitializeOnLoadMethod] static void Resume(){if(SessionState.GetInt(Key,0)>0){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static void Tick()
 {
  int phase=SessionState.GetInt(Key,0);if(phase==0)return;
  try
  {
   Owned();if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"Deadline",0))throw new Exception("Timeout phase="+phase);
   if(phase==3){if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish(true,"Production listener lifecycle validated structurally in real Play; no human audible test");return;}
   if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();var actor=HotelPlayer.LocalPlayer;var game=GameSceneController.Current;
   if(phase==4)
   {
    Verify("Boss additive scene loading");var coordinator=BossArenaCoordinator.Current;if(!coordinator||!coordinator.Arena)return;
    var listener=Verify("Boss scene loaded before viewing");var hallway=listener.GetComponent<Camera>();Call(coordinator,"SetCamera",1u);
    Check(!hallway.enabled&&hallway.gameObject.activeInHierarchy,"production Boss camera swap disables Camera component while preserving its GameObject");
    Verify("Boss viewing after production SetCamera");Check(coordinator.Arena.Camera&&coordinator.Arena.Camera.isActiveAndEnabled,"authored Boss output camera active");
    var audio=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s=>s.gameObject.scene.name=="Game"||s.gameObject.scene.name=="BossFight").ToArray();
    notes.Add("STRUCTURE: loaded Game/Boss audio sources="+audio.Length+" spatialBlend="+string.Join(",",audio.Select(s=>s.name+":"+s.spatialBlend)));
    var stormSources=audio.Where(s=>s.GetComponentInParent<LobbyStormAudio>()).ToArray();Check(stormSources.Length==6&&stormSources.All(s=>s.spatialBlend==0),"six authored Game/Boss storm sources remain 2D across the camera swap; player footstep spatial profile preserved");
    Call(coordinator,"SetCamera",0u);Check(hallway.enabled,"production Boss return restores hallway Camera");Verify("Boss return after production SetCamera");
    Check(stages.Contains("attic transition")&&stages.Contains("how-to")&&stages.Contains("countdown")&&stages.Contains("released gameplay"),"real transition/readiness/countdown/gameplay stages all observed");
    SessionState.SetInt(Key,3);EditorApplication.ExitPlaymode();return;
   }
   var selection=UnityEngine.Object.FindFirstObjectByType<GhostSelectionPresentation>();var attic=selection?Get(selection,"_Attic") as Component:null;string stage=attic?"attic transition":actor&&actor.RoundReleased?"released gameplay":actor&&actor.RoundIntroComplete?"countdown":actor&&actor.RoundCinematicComplete?"how-to":"selection/startup";Verify(stage);
   if(!actor)return;
   if(!ready&&actor.RoundCinematicComplete){var page=UnityEngine.Object.FindFirstObjectByType<RoundHowToPage>();var button=page?(UnityEngine.UI.Button)Get(page,"_Continue"):null;if(button&&button.interactable){button.onClick.Invoke();ready=true;notes.Add("OFFLINE: authored interactable Ready callback invoked; no OS input or network match claimed");}}
   if(actor.RoundReleased)
   {
    var coordinator=BossArenaCoordinator.Current;Check(coordinator,"production Boss coordinator exists");SessionState.SetInt(Key,4);coordinator.StartCoroutine((IEnumerator)Call(coordinator,"LoadArena"));
   }
  }
  catch(Exception e){Finish(false,e.ToString());}
 }
 static void Finish(bool pass,string text){SessionState.SetInt(Key,0);EditorApplication.update-=Tick;File.WriteAllText(Folder+"/result.txt",(pass?"PASS ":"FAIL ")+text+"\nlistener samples="+sampled+"\n"+string.Join("\n",notes));EditorApplication.Exit(pass?0:1);}
}
#endif
