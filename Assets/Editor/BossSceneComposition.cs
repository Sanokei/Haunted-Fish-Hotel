#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HauntedFish.BossFight;
using PrettyHierarchy;
// STAGED ONLY. No automatic invocation, imports, process launches, or Play changes.
public static class BossSceneComposition
{
 const string ScenePath="Assets/Scenes/BossFight.unity",Legacy="Assets/Resources/BossFight/BossFightArena.prefab",Folder="Assets/Resources/BossFight/SceneObjects",Evidence="Tools/BossFightTests/Evidence/composition";
 sealed class Reference{public Component Owner;public string Path;public UnityEngine.Object Value;}
 public static void ReportState()
 {
  Directory.CreateDirectory(Evidence);var lines=new List<string>{"UTC="+DateTime.UtcNow.ToString("o"),"Playing="+EditorApplication.isPlaying,"Paused="+EditorApplication.isPaused,"Compiling="+EditorApplication.isCompiling,"Updating="+EditorApplication.isUpdating,"PrefabStage="+(PrefabStageUtility.GetCurrentPrefabStage()!=null)};
  for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);lines.Add(s.path+" | dirty="+s.isDirty+" | loaded="+s.isLoaded+" | active="+(s==SceneManager.GetActiveScene()));}File.WriteAllLines(Evidence+"/editor-state.txt",lines);
 }
 public static void Run()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Exclusive idle Edit Mode required; no Play changes performed.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty user scenes first; no writes performed.");
  if(new DriveInfo(System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(ScenePath))).AvailableFreeSpace<512L*1024*1024)throw new Exception("512MiB free minimum for safe native import/save.");
  var before=EditorSceneManager.GetSceneManagerSetup();var selected=Selection.objects;string backup=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossScene.before-composition.unity");if(!File.Exists(backup))File.Copy(ScenePath,backup);
  try
  {
   var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var container=scene.GetRootGameObjects().SingleOrDefault(go=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)==Legacy);if(!container)throw new Exception("Exact legacy wrapper absent; inspect rather than guess.");
   var refs=References(scene);var poses=container.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localToWorldMatrix);PrefabUtility.UnpackPrefabInstance(container,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
   var groups=new Dictionary<string,GameObject>();foreach(string name in new[]{"ROOM - Attic and lighting","TABLE - Felt and physical actors","CHARACTERS - One contestant per side","DEFENSES - Six authored sideline pieces","UI - Challenge HUD","VIEWS - Cameras and storm anchors","MANAGERS - Match and presentation","ANCHORS - Participant spawns"})groups[name]=Group(name,scene);
   foreach(Transform child in container.transform.Cast<Transform>().ToArray())child.SetParent(groups[Category(child.name)].transform,true);
   if(container.GetComponents<Component>().Length==1&&container.transform.childCount==0)UnityEngine.Object.DestroyImmediate(container);else{container.name="Retained scene manager components";container.transform.SetParent(groups["MANAGERS - Match and presentation"].transform,true);}
   if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources/BossFight","SceneObjects");
   var replacements=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
   foreach(var pair in groups.Where(p=>!p.Key.StartsWith("MANAGERS")&&!p.Key.StartsWith("ANCHORS"))){var asset=PrefabUtility.SaveAsPrefabAsset(pair.Value,Folder+"/Boss"+pair.Key.Split(' ')[0]+".prefab",out bool saved);if(!saved||!asset)throw new Exception("Scoped native prefab save failed: "+pair.Key);var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);instance.name=pair.Value.name;Map(pair.Value.transform,instance.transform,replacements);UnityEngine.Object.DestroyImmediate(pair.Value);}
   foreach(var r in refs){if(replacements.TryGetValue(r.Owner,out var owner))r.Owner=(Component)owner;if(replacements.TryGetValue(r.Value,out var value))r.Value=value;}
   poses=poses.ToDictionary(p=>replacements.TryGetValue(p.Key,out var value)?(Transform)value:p.Key,p=>p.Value);
   Restore(refs);foreach(var r in refs){if(!r.Owner)throw new Exception("Original component lost");var p=new SerializedObject(r.Owner).FindProperty(r.Path);if(p==null||p.objectReferenceValue!=r.Value)throw new Exception("Scene reference changed: "+r.Owner.name+"."+r.Path);}
   foreach(var pose in poses){if(!pose.Key)continue;var now=pose.Key.localToWorldMatrix;for(int i=0;i<16;i++)if(Mathf.Abs(now[i]-pose.Value[i])>.002f)throw new Exception("World pose changed: "+pose.Key.name);}
   var managers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BossArenaManager>(true)).ToArray();if(managers.Length!=1)throw new Exception("Exactly one match manager required");var m=new SerializedObject(managers[0]);var board=(BossDefenseBoard)m.FindProperty("_Defenses").objectReferenceValue;var drag=(BossDragInput)m.FindProperty("_DragInput").objectReferenceValue;if(!board||!board.ValidateAuthoredBindings(out _)||!drag||!drag.ValidateAuthoredBindings(out _))throw new Exception("Gameplay bindings incomplete");
   if(scene.GetRootGameObjects().Any(go=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)==Legacy))throw new Exception("Whole-scene wrapper remains");if(!EditorSceneManager.SaveScene(scene))throw new Exception("Boss-only scene save failed");AssetDatabase.SaveAssets();Directory.CreateDirectory(Evidence);
   var hierarchy=new List<string>();foreach(var root in scene.GetRootGameObjects()){hierarchy.Add(root.name+" | prefab="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));foreach(Transform child in root.transform)hierarchy.Add("  "+child.name);}File.WriteAllLines(Evidence+"/hierarchy.txt",hierarchy);File.WriteAllText(Evidence+"/composition-results.txt","PASS8PrettyObject roots;6scoped reusable prefab instances; explicit managers/anchors; "+refs.Count+" original scene references and world poses preserved; original wrapper asset retained. Native gameplay/render followup required.");
  }
  finally{if(before.Length>0&&before.All(s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(before);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Selection.objects=selected;}
 }
 static void Map(Transform source,Transform target,Dictionary<UnityEngine.Object,UnityEngine.Object> map){map[source.gameObject]=target.gameObject;var a=source.GetComponents<Component>();var b=target.GetComponents<Component>();if(a.Length!=b.Length||source.childCount!=target.childCount)throw new Exception("Scoped prefab structure mismatch");for(int i=0;i<a.Length;i++){if(a[i].GetType()!=b[i].GetType())throw new Exception("Scoped component mismatch");map[a[i]]=b[i];}for(int i=0;i<source.childCount;i++)Map(source.GetChild(i),target.GetChild(i),map);}
 static string Category(string name)
 {
  if(new[]{"Felt hockey table","Fish paddle","Ghost paddle","Cue ball","Fish goal","Ghost goal"}.Contains(name))return "TABLE - Felt and physical actors";if(name=="Authored defense rack")return "DEFENSES - Six authored sideline pieces";if(name.StartsWith("Matched player")||name=="UI")return "CHARACTERS - One contestant per side";if(name=="Boss lives HUD")return "UI - Challenge HUD";if(name=="Boss perspective camera rigs"||name=="Opponent storm wall anchors")return "VIEWS - Cameras and storm anchors";if(name.EndsWith("participant spawn"))return "ANCHORS - Participant spawns";if(name=="Attic"||name=="Level")return "ROOM - Attic and lighting";return "MANAGERS - Match and presentation";
 }
 static GameObject Group(string name,Scene scene)
 {
  var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);var pretty=go.AddComponent<PrettyObject>();var p=new SerializedObject(pretty);p.FindProperty("useDefaultBackgroundColor").boolValue=false;p.FindProperty("backgroundColor").colorValue=name.StartsWith("MANAGERS")?new Color32(63,45,74,255):new Color32(30,65,66,255);p.FindProperty("useDefaultTextColor").boolValue=false;p.FindProperty("textColor").colorValue=new Color32(248,234,205,255);p.FindProperty("fontStyle").enumValueIndex=(int)FontStyle.Bold;p.ApplyModifiedPropertiesWithoutUndo();return go;
 }
 static List<Reference> References(Scene scene)
 {
  var refs=new List<Reference>();foreach(var c in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c)){var p=new SerializedObject(c).GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&!EditorUtility.IsPersistent(p.objectReferenceValue))refs.Add(new Reference{Owner=c,Path=p.propertyPath,Value=p.objectReferenceValue});}return refs;
 }
 static void Restore(IEnumerable<Reference> refs)
 {
  foreach(var r in refs){if(!r.Owner)throw new Exception("Authored component destroyed: "+r.Path);var s=new SerializedObject(r.Owner);var p=s.FindProperty(r.Path);if(p==null)throw new Exception("Authored field lost: "+r.Path);if(p.objectReferenceValue==r.Value)continue;p.objectReferenceValue=r.Value;s.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(r.Owner);}
 }
}
#endif
