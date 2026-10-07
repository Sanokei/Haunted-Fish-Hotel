using System;
using System.IO;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public sealed class ActualLobbyFlowRunner:MonoBehaviour {
 float began;bool placed,requested;string folder;
 static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
 void Awake(){DontDestroyOnLoad(gameObject);began=Time.unscaledTime;folder=System.IO.Path.Combine(Application.dataPath,"../ActualLobbyEvidence");Directory.CreateDirectory(folder);SceneManager.sceneLoaded+=Loaded;}
 void Loaded(Scene scene,LoadSceneMode mode){if(scene.name=="Game"){File.AppendAllText(System.IO.Path.Combine(folder,"events.txt"),"GAME_ENTERED via production Lobby travel\n");new GameObject("Game flow observer after Lobby travel").AddComponent<ActualGameFlowRunner>();}}
 void Update(){try{
  if(SceneManager.GetActiveScene().name=="Game")return;
  var session=FindFirstObjectByType<HauntedHotelMultiplayer>();if(!session)return;
  if(session.State==HotelSessionState.Failed)throw new Exception("Lobby-to-Game blocked: "+session.Status);
  if(Time.unscaledTime-began>100)throw new Exception("Lobby-to-Game timed out: "+session.State+" "+session.Status);
  var player=HotelPlayer.LocalPlayer;var lobby=FindFirstObjectByType<LobbyManager>();if(!session.ReadyToPlay||!player||!lobby)return;
  if(lobby.IntroductionPlaying){lobby.SkipIntroduction();File.AppendAllText(System.IO.Path.Combine(folder,"events.txt"),"Used existing introduction skip\n");return;}
  if(!placed){var zone=Get<LobbyTrigger>(lobby,"_StairZone").GetComponent<Collider>();player.Teleport(zone.bounds.center-player.GetComponent<CharacterController>().center);Physics.SyncTransforms();placed=true;File.AppendAllText(System.IO.Path.Combine(folder,"events.txt"),"Connected host placed in real authored stair ready zone\n");return;}
  if(!(bool)lobby.GetType().GetMethod("IsInReadyZone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(lobby,new object[]{player})||requested)return;
  var menu=FindFirstObjectByType<MemberMenu>(FindObjectsInactive.Include);var start=Get<Button>(menu,"_Start");if(!start.interactable)return;
  requested=true;start.onClick.Invoke();File.AppendAllText(System.IO.Path.Combine(folder,"events.txt"),"Invoked existing leader Start button with real ready-zone occupancy\n");
 }catch(Exception e){File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"BLOCKED "+e.Message);Debug.LogError(e.Message);EditorApplication.Exit(1);}}
}
