using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HauntedFish.Multiplayer;
public static class LobbyRadioSceneIntegration {
 const string E="C:/Users/wkeif/Documents/Codex/2026-10-09/task/LobbyAtmosphereEvidence";
 
 [MenuItem("Tools/Haunted Fish/Integrate Authored Lobby Radio")]
 public static void Run(){
 if(!LobbyStormRepairTask.RequireSafeEditMode())return;
 Scene scene=default;bool opened=false;int undo=-1;
 try{
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Radio/LobbyRadioPlayback.prefab");if(!prefab)return;
 var player=prefab.GetComponent<LobbyRadioPlayback>();if(!player)throw new Exception("Radio playback script missing");
 var pso=new SerializedObject(player);var ads=pso.FindProperty("_Advertisements");if(ads.arraySize!=11)throw new Exception("Final eleven default radio references required");
 var rows=new System.Collections.Generic.List<string>();
 for(int i=0;i<11;i++){
 var clip=(AudioClip)ads.GetArrayElementAtIndex(i).objectReferenceValue;if(!clip)throw new Exception("Missing radio clip "+i);var path=AssetDatabase.GetAssetPath(clip);
 AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
 clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var duration=SourceDuration(path);
 bool rate=clip.frequency==22050||(EditorUserBuildSettings.activeBuildTarget==BuildTarget.WebGL&&clip.frequency==44100);
 if(clip.channels!=1||!rate||clip.length<=0||Math.Abs(clip.length-duration)>.002||clip.loadState==AudioDataLoadState.Failed)throw new Exception("Radio native/source mismatch "+path+" Hz="+clip.frequency+" seconds="+clip.length+" source="+duration);
 AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip,out string guid,out long id);rows.Add(clip.name+" mono Hz="+clip.frequency+" seconds="+clip.length+" source22050seconds="+duration+" guid="+guid+" fileID="+id);
 }
 File.WriteAllLines(E+"/radio-eleven-native-clips.txt",rows);
 scene=SceneManager.GetSceneByPath("Assets/Scenes/Lobby.unity");if(!scene.isLoaded){if(!LobbyStormRepairTask.RequireSafeEditMode())return;scene=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);opened=true;}if(scene.isDirty){File.WriteAllText(E+"/radio-scene-status.txt","Waiting for loaded Lobby unsaved changes; no writes.");return;}
 var roots=scene.GetRootGameObjects();var radio=roots.Single(x=>x.name=="Radio");var cursor=roots.SelectMany(x=>x.GetComponentsInChildren<LobbyCursorManager>(true)).Single();
 var light=roots.SelectMany(x=>x.GetComponentsInChildren<LightningEffectManager>(true)).Single();if(!light.ValidateAuthoredBindings(out var error))throw new Exception("Preserve storm repair: "+error);
 if(!File.Exists(E+"/Lobby_before_radio_integration.unity"))File.Copy("Assets/Scenes/Lobby.unity",E+"/Lobby_before_radio_integration.unity",false);
 if(!LobbyStormRepairTask.RequireSafeEditMode())return;Undo.IncrementCurrentGroup();undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Integrate authored Lobby radio playback");
 var current=radio.GetComponentInChildren<LobbyRadioPlayback>(true);
 if(!current){
 var old=radio.transform.Cast<Transform>().Single(x=>x.name=="RadioAudioSource");
 var oldSource=old.GetComponent<AudioSource>();if(!oldSource)throw new Exception("Original standalone radio voice missing");
 var position=old.localPosition;var rotation=old.localRotation;var scale=old.localScale;
 var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(instance,"Authored radio instance");instance.transform.SetParent(radio.transform,false);instance.transform.localPosition=position;instance.transform.localRotation=rotation;instance.transform.localScale=scale;
 current=instance.GetComponent<LobbyRadioPlayback>();var source=instance.GetComponent<AudioSource>();
 // Retain every original spatial/mixer/gain parameter, using the native-authored filters.
 source.volume=oldSource.volume;source.pitch=oldSource.pitch;source.spatialBlend=oldSource.spatialBlend;source.dopplerLevel=oldSource.dopplerLevel;source.minDistance=oldSource.minDistance;source.maxDistance=oldSource.maxDistance;source.rolloffMode=oldSource.rolloffMode;source.outputAudioMixerGroup=oldSource.outputAudioMixerGroup;source.priority=oldSource.priority;source.panStereo=oldSource.panStereo;source.mute=oldSource.mute;source.playOnAwake=false;
 PrefabUtility.RecordPrefabInstancePropertyModifications(source);PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
 Undo.DestroyObjectImmediate(old.gameObject);
 }
 var voice=current.GetComponent<AudioSource>();var so=new SerializedObject(cursor);Undo.RecordObject(cursor,"Bind authored radio cursor");so.FindProperty("_RadioPlayback").objectReferenceValue=current;so.FindProperty("_RadioAudio").objectReferenceValue=voice;so.ApplyModifiedPropertiesWithoutUndo();
 if(radio.GetComponentsInChildren<AudioSource>(true).Length!=1)throw new Exception("Exactly one radio voice expected");
 if(!light.ValidateAuthoredBindings(out error))throw new Exception(error);
 if(!LobbyStormRepairTask.RequireSafeEditMode(false))throw new InvalidOperationException("Editor state changed; radio save cancelled.");EditorUtility.SetDirty(cursor);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Lobby radio save failed");
 File.WriteAllText(E+"/radio-scene-saved.txt","PASS actual saved Lobby: one LobbyRadioPlayback prefab beneath Radio, original transform/music/source volume pitch spatial falloff mixer preserved; old standalone RadioAudioSource removed; LobbyCursorManager._RadioPlayback and _RadioAudio bound to same authored instance; all11 source22050 mono clips native verified (WebGL imports44100); storm validator passes. Live dial/music completion and listening pending.");
 
 }catch(Exception error){if(undo>=0&&LobbyStormRepairTask.RequireSafeEditMode(false))Undo.RevertAllDownToGroup(undo);File.WriteAllText(E+"/radio-scene-error.txt",error.ToString());}finally{if(opened&&scene.isLoaded&&!scene.isDirty&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)EditorSceneManager.CloseScene(scene,true);}
 }
 public static void RunBatchValidation(){
 if(!Application.isBatchMode)throw new InvalidOperationException("This entry point is for an explicitly invoked batch validation only.");
 if(!LobbyStormRepairTask.RequireSafeEditMode())throw new InvalidOperationException("Native editor is not idle/clean.");
 var started=DateTime.UtcNow;var originalScene=SceneManager.GetActiveScene().path;
 var helpers=new[]{typeof(LobbyStormRepairTask),typeof(LobbyAtmosphereAuthoringTask),typeof(LobbyMoodyInteriorAuthoringTask),typeof(LobbyRadioAuthoring),typeof(LobbyRadioSceneIntegration),typeof(StormAudioImportDiagnosis),typeof(StormAuthoredNativeValidation)};
 foreach(var helper in helpers)if(helper.GetCustomAttributes(typeof(InitializeOnLoadAttribute),false).Length!=0)throw new Exception("Automatic initializer remains: "+helper.Name);
 if(StormAudioImportDiagnosis.SupportedPlatformName(BuildTarget.NoTarget)!=null||StormAudioImportDiagnosis.SupportedPlatformName(BuildTarget.StandaloneWindows64)!="Standalone"||StormAudioImportDiagnosis.SupportedPlatformName(BuildTarget.WebGL)!="WebGL")throw new Exception("Canonical platform/no-target checks failed");
 StormAuthoredNativeValidation.Run();
 var native=E+"/storm-native-validation.txt";if(!File.Exists(native)||File.GetLastWriteTimeUtc(native)<started)throw new Exception("Native authored storm validation did not pass freshly; inspect storm-native-error.txt");
 StormAudioImportDiagnosis.Run();var audio=E+"/audio-import-manual-inspection.txt";if(!File.Exists(audio)||File.GetLastWriteTimeUtc(audio)<started)throw new Exception("Native audio inspection did not pass freshly; inspect audio-import-manual-error.txt");
 Run();
 var radio=E+"/radio-scene-saved.txt";if(!File.Exists(radio)||File.GetLastWriteTimeUtc(radio)<started)throw new Exception("Actual radio integration did not pass freshly; inspect radio-scene-error.txt/status.txt");
 if(SceneManager.GetActiveScene().path!=originalScene||EditorApplication.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Explicit batch command changed active scene or entered Play");
 File.WriteAllText(E+"/native-batch-validation.txt","PASS native Unity "+Application.unityVersion+" compile/import; all7 Lobby/Storm helpers have no InitializeOnLoad attribute; canonical Windows/WebGL and NoTarget mapping; authored storm lifecycle/profile checks and source audio inspection fresh; actual Lobby radio scene saved with11 verified refs; original active scene restored and no Play entered. Live gameplay/listening remains untested.");
 }
 static double SourceDuration(string path){
 using var r=new BinaryReader(File.OpenRead(path));var s=r.BaseStream;
 if(new string(r.ReadChars(4))!="RIFF")throw new Exception("Source WAV required");r.ReadUInt32();if(new string(r.ReadChars(4))!="WAVE")throw new Exception("WAVE required");
 int channels=0,rate=0,align=0;uint data=0;while(s.Position+8<=s.Length){var tag=new string(r.ReadChars(4));uint size=r.ReadUInt32();long end=s.Position+size+(size%2);if(tag=="fmt "){int format=r.ReadUInt16();channels=r.ReadUInt16();rate=r.ReadInt32();r.ReadUInt32();align=r.ReadUInt16();int bits=r.ReadUInt16();if(format!=1||bits!=16)throw new Exception("PCM16 source required "+path);}else if(tag=="data")data=size;s.Position=end;}
 if(channels!=1||rate!=22050||data==0)throw new Exception("Source mono22050 identity changed "+path);return (double)data/align/rate;
 }
}
