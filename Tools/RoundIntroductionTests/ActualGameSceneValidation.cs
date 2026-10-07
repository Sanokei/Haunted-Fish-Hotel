using System;
using System.Linq;
using System.Reflection;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class ActualGameSceneValidation {
 public static void RunLobby(){EditorSceneManager.OpenScene("Assets/Scenes/Helper.unity");EditorApplication.EnterPlaymode();}
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");EditorSceneManager.SaveScene(scene);
  scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
  var world=UnityEngine.Object.FindFirstObjectByType<GhostPlacementWorld>();var data=new SerializedObject(world);
  if(UnityEngine.Object.FindObjectsByType<GhostTrap>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new Exception("Game must have no preplaced traps");
  if(UnityEngine.Object.FindObjectsByType<GhostTrapSupply>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new Exception("No fixed supply instances should remain");
  var manager=UnityEngine.Object.FindFirstObjectByType<TrapManager>();var catalog=new SerializedObject(manager);
  if(!manager||manager.transform.childCount!=0||!catalog.FindProperty("_World").objectReferenceValue||!catalog.FindProperty("_GhostControls").objectReferenceValue||!catalog.FindProperty("_PackagePrefab").objectReferenceValue)throw new Exception("Authored empty Trap Manager dependency reference lost");
  var definitions=catalog.FindProperty("_Traps");if(definitions.arraySize!=3)throw new Exception("Authored trap definition list lost");
  for(int i=0;i<definitions.arraySize;i++)if(!definitions.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue||!definitions.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayArt").objectReferenceValue)throw new Exception("Injected definition art/prefab lost");
  for(int i=0;i<definitions.arraySize;i++) {
   var entry=definitions.GetArrayElementAtIndex(i);var trap=(GhostTrap)entry.FindPropertyRelative("Prefab").objectReferenceValue;
   if(trap.Icon&&trap.Icon.name=="shopping card"&&entry.FindPropertyRelative("Weight").floatValue>0&&trap.InputMode!=TrapInputMode.Movement)throw new Exception("Default supply must not disguise an immobile demo as an ordinary shopping cart");
  }
  Debug.Log("GAME_CHECK: Default shopping-cart supply contains movement carts only; stationary click example remains opt-in");
  Debug.Log("GAME_CHECK: Prefab instances and references survive actual Unity scene save/reload");
  EditorApplication.EnterPlaymode();
 }
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void StartMonitor(){if(Environment.GetCommandLineArgs().Contains("--actual-game-validation"))new GameObject("Actual scene observer").AddComponent<ActualGameFlowRunner>();if(Environment.GetCommandLineArgs().Contains("--actual-lobby-validation"))new GameObject("Actual lobby observer").AddComponent<ActualLobbyFlowRunner>();}
}
