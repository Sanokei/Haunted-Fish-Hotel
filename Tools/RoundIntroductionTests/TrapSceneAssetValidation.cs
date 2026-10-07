using System;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class TrapSceneAssetValidation {
 public static void Run(){try{
 var scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
 var world=UnityEngine.Object.FindFirstObjectByType<GhostPlacementWorld>();if(!world)throw new Exception("Missing scene world");
 var settings=new SerializedObject(world);
 var manager=UnityEngine.Object.FindFirstObjectByType<TrapManager>();if(!manager||manager.transform.childCount!=0)throw new Exception("Missing authored empty Trap Manager");
 var dependencies=new SerializedObject(manager);var catalog=dependencies.FindProperty("_Traps");if(catalog.arraySize!=3)throw new Exception("Trap definition catalog size");
 for(int i=0;i<3;i++)if(!catalog.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue||!catalog.GetArrayElementAtIndex(i).FindPropertyRelative("DisplayArt").objectReferenceValue)throw new Exception("Missing prefab/art reference");
 if(!dependencies.FindProperty("_World").objectReferenceValue||!dependencies.FindProperty("_GhostControls").objectReferenceValue||!dependencies.FindProperty("_PackagePrefab").objectReferenceValue)throw new Exception("Missing manager dependency");
 if(UnityEngine.Object.FindObjectsByType<GhostTrap>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new Exception("Unexpected preplaced traps");
 if(!settings.FindProperty("_TrapContainer").objectReferenceValue)throw new Exception("Missing trap group");
 var selection=UnityEngine.Object.FindFirstObjectByType<GhostSelectionPresentation>();if(!selection||!new SerializedObject(selection).FindProperty("_AtticPrefab").objectReferenceValue)throw new Exception("Attic hookup");
 if(UnityEngine.Object.FindObjectsByType<GhostTrapSupply>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new Exception("Unexpected fixed supply instances");
 var roots=scene.GetRootGameObjects();bool ghost=false,traps=false;foreach(var root in roots){ghost|=root.name=="Ghost";traps|=root.name=="Placed traps";}if(!ghost||!traps)throw new Exception("Missing hierarchy groups");
 Debug.Log("TRAP_SCENE_PASS: full production scripts and Mirage RPC weaving compiled; authored manager catalog/package dependencies, empty trap/supply scene, attic and hierarchy references verified");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
