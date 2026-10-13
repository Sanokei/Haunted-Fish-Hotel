using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HauntedFish.Multiplayer;
public static class LobbyStormRepairTask
{
 const string E="C:/Users/wkeif/Documents/Codex/2026-10-09/task/LobbyAtmosphereEvidence";
 static double NextProbe;
 public static bool RequireSafeEditMode(bool requireCleanScenes=true){
 if(EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){Debug.LogWarning("Lobby authoring requires idle Edit mode; no work performed.");return false;}
 if(requireCleanScenes)for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isDirty){Debug.LogWarning("Unsaved scene changes: "+scene.path+"; no authoring performed.");return false;}}
 return true;
 }
 [MenuItem("Tools/Haunted Fish/Inspect Lobby Storm State")]
 public static void InspectState(){if(!EditorApplication.isCompiling&&!EditorApplication.isUpdating)Probe();}
 static void Probe(){
 if(EditorApplication.timeSinceStartup<NextProbe)return;NextProbe=EditorApplication.timeSinceStartup+2;
 var lines=new System.Collections.Generic.List<string>{"playing="+EditorApplication.isPlaying+" active="+EditorSceneManager.GetActiveScene().path,"listenerPause="+AudioListener.pause+" masterVolume="+AudioListener.volume};
 for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);lines.Add("scene="+scene.path+" loaded="+scene.isLoaded+" dirty="+scene.isDirty);if(!scene.isLoaded)continue;
 foreach(var listener in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AudioListener>(true)))lines.Add("listener="+listener.name+" enabled="+listener.enabled+" active="+listener.gameObject.activeInHierarchy);
 foreach(var storm in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<LobbyStormAudio>(true))){var so=new SerializedObject(storm);lines.Add("storm="+storm.name+" enabled="+storm.enabled+" active="+storm.gameObject.activeInHierarchy+" running="+storm.Running);foreach(var field in new[]{"_RainSource","_ThunderSource","_ThunderSourceB"}){var source=(AudioSource)so.FindProperty(field).objectReferenceValue;lines.Add(field+"="+(source?source.name+" active="+source.gameObject.activeInHierarchy+" enabled="+source.enabled+" playing="+source.isPlaying+" volume="+source.volume+" clip="+(source.clip?source.clip.name:"null"):"null"));}}
 foreach(var light in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<LightningEffectManager>(true)))lines.Add("lightOwner="+light.name+" active="+light.AmbientActive+" playing="+light.Playing+" valid="+light.ValidateAuthoredBindings(out var error)+" error="+error);
 }
 File.WriteAllLines(E+"/storm-live-status.txt",lines);
 }
 [MenuItem("Tools/Haunted Fish/Repair Lobby Storm Bindings")]
 public static void RepairFromMenu(){
 if(!RequireSafeEditMode())return;
 Run();
 }
 static void Reference(SerializedObject so,string field,UnityEngine.Object value){var p=so.FindProperty(field);if(p==null)throw new Exception("Missing storm field "+field);p.objectReferenceValue=value;}
 static void Run(){if(!RequireSafeEditMode())return;Scene scene=default;bool opened=false;int undo=-1;try{
 scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");if(!scene.isLoaded){if(!RequireSafeEditMode())return;scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);opened=true;}
 if(scene.isDirty){File.WriteAllText(E+"/storm-repair-status.txt","Waiting: loaded Lobby has unsaved changes; no writes.");return;}
 var roots=scene.GetRootGameObjects();var light=roots.SelectMany(x=>x.GetComponentsInChildren<LightningEffectManager>(true)).Single();var storm=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyStormAudio>(true)).Single();
 if(!File.Exists(E+"/Lobby_before_urgent_storm_repair.unity"))File.Copy(Application.dataPath+"/Scenes/Lobby.unity",E+"/Lobby_before_urgent_storm_repair.unity",false);
 if(!RequireSafeEditMode())return;Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Repair authored Lobby storm visual and audio bindings");
 Directory.CreateDirectory("Assets/Prefabs/LobbyAtmosphere");AssetDatabase.Refresh();
 typeof(LobbyAtmosphereAuthoringTask).GetMethod("AuthorLightning",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{light,scene});
 var so=new SerializedObject(storm);Reference(so,"_Lightning",light);
 var sourceNames=new[]{("_RainSource","Indoor rain bed"),("_ThunderSource","Thunder voice A"),("_ThunderSourceB","Thunder voice B")};
 foreach(var pair in sourceNames){var source=storm.GetComponentsInChildren<AudioSource>(true).Single(x=>x.name==pair.Item2);Reference(so,pair.Item1,source);source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.pitch=1;source.loop=pair.Item1=="_RainSource";EditorUtility.SetDirty(source);}
 var paths=new[]{"HFH_Rain_Indoor_Mix_Loop_60s","HFH_Thunder_Medium_Roll","HFH_Thunder_Soft_Crack","HFH_Thunder_Distant_Long","HFH_Thunder_Far_Rumble"};
 var clips=paths.Select(n=>{var path="Assets/Art/Storm/IndoorTransmission/"+n+"_Muffled.wav";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip)throw new Exception("Missing clip "+path);clip.LoadAudioData();return clip;}).ToArray();
 Reference(so,"_RainLoop",clips[0]);var cues=so.FindProperty("_ThunderClips");cues.arraySize=4;for(int i=0;i<4;i++)cues.GetArrayElementAtIndex(i).objectReferenceValue=clips[i+1];so.FindProperty("_RainGain").floatValue=.72f;so.FindProperty("_ThunderGain").floatValue=.85f;so.FindProperty("_RainDuringDialogue").floatValue=.45f;so.FindProperty("_ThunderDuringDialogue").floatValue=.6f;so.ApplyModifiedPropertiesWithoutUndo();
 if(!light.ValidateAuthoredBindings(out var error))throw new Exception(error);
 var camera=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>(true)).First(x=>x.name=="Main Camera");var listener=camera.GetComponent<AudioListener>();if(!listener)throw new Exception("Existing Lobby Main Camera AudioListener missing");if(!listener.enabled){Undo.RecordObject(listener,"Restore Lobby listener");listener.enabled=true;}if(!camera.gameObject.activeSelf){Undo.RecordObject(camera.gameObject,"Restore Lobby camera");camera.gameObject.SetActive(true);}
 var profileReport=new System.Collections.Generic.List<string>();foreach(var clip in clips){AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip,out string guid,out long id);profileReport.Add(clip.name+" channels="+clip.channels+" Hz="+clip.frequency+" seconds="+clip.length+" fileID="+id+" guid="+guid);}
 File.WriteAllLines(E+"/storm-clip-native-metadata.txt",profileReport);
 if(!RequireSafeEditMode(false))throw new InvalidOperationException("Editor state changed during authoring; save cancelled.");EditorUtility.SetDirty(storm);EditorUtility.SetDirty(light);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby storm repair save failed");
 File.WriteAllText(E+"/storm-repair-saved.txt","PASS actual saved Lobby: authored white-frame Canvas and receiver passes assigned; validator succeeds; audio owner plus three distinct authored2D sources and five actual clip references repaired; listener enabled and Main Camera active. Cue timing unchanged. Requires fresh Lobby runtime load to validate playing sources and strike pixels.");
 }catch(Exception error){if(undo>=0&&LobbyStormRepairTask.RequireSafeEditMode(false))Undo.RevertAllDownToGroup(undo);File.WriteAllText(E+"/storm-repair-error.txt",error.ToString());}finally{if(opened&&scene.isLoaded&&!scene.isDirty&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)EditorSceneManager.CloseScene(scene,true);}}
}
