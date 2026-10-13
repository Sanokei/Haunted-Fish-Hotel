using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using Monologue.Dialogue;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
public static class LobbyQABuild {
 public static void Run(){var settings=File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");string company=PlayerSettings.companyName,product=PlayerSettings.productName;try{
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();var session=all.Select(x=>x.GetComponent<HauntedHotelMultiplayer>()).Single(x=>x);var so=new SerializedObject(session);so.FindProperty("_AutoCreateOnStart").boolValue=false;so.ApplyModifiedProperties();
 var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab"),scene);actor.name="Authored QA local practice actor";actor.GetComponent<HotelPlayer>().Networked=false;actor.transform.position=new Vector3(0,1.1f,2);
 var camera=all.Select(x=>x.GetComponent<Camera>()).Single(x=>x&&x.name=="Main Camera");var qa=camera.gameObject.AddComponent<LobbyViewportQA>();qa.Manager=all.Select(x=>x.GetComponent<LobbyManager>()).Single(x=>x);qa.UI=all.Select(x=>x.GetComponent<StoryUI>()).Single(x=>x);qa.Storm=all.Select(x=>x.GetComponent<LightningEffectManager>()).Single(x=>x);qa.StormAudio=all.Select(x=>x.GetComponent<LobbyStormAudio>()).Single(x=>x);qa.Fireplace=all.Select(x=>x.GetComponent<LobbyFireplaceFlicker>()).Single(x=>x);qa.FireSource=(AudioSource)typeof(LobbyFireplaceFlicker).GetField("_Crackle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(qa.Fireplace);qa.Radio=all.Select(x=>x.GetComponent<LobbyRadioPlayback>()).Single(x=>x);qa.Mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Resources/HotelAudio.mixer");
 if(!EditorSceneManager.SaveScene(scene,"Assets/Diagnostics/LobbyNativeQA20261012/LobbyQA.unity"))throw new Exception("QA scene save failed");PlayerSettings.companyName="CodexLocalQA";PlayerSettings.productName="HauntedFishLobbyViewportQA";Directory.CreateDirectory(@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation\StandaloneQA\Player");
 var options=new BuildPlayerOptions{scenes=new[]{"Assets/Diagnostics/LobbyNativeQA20261012/LobbyQA.unity"},locationPathName=@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation\StandaloneQA\Player\LobbyViewportQA.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None};var report=BuildPipeline.BuildPlayer(options);File.WriteAllText(@"C:\Users\wkeif\Documents\Codex\2026-10-09\task\LobbyMoodPreparation\StandaloneQA\build-result.txt",report.summary.result+" bytes="+report.summary.totalSize+" errors="+report.summary.totalErrors);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Standalone QA build failed");Debug.Log("LOBBY_QA_BUILD_PASS");
 }catch(Exception e){Debug.LogException(e);}finally{PlayerSettings.companyName=company;PlayerSettings.productName=product;File.WriteAllBytes("ProjectSettings/ProjectSettings.asset",settings);EditorApplication.Exit(0);}}
}
