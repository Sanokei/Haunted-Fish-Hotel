#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class BossDefenseNativeBatch
{
 const string Armed="Haunted.BossDefense.Armed";
 static BossDefenseNativeBatch(){if(SessionState.GetBool(Armed,false))EditorApplication.playModeStateChanged+=State;}
 public static void Run()
 {
  if(!UnityEngine.Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--boss-defense-validation"))throw new Exception("Explicit exclusively owned batch required.");
  BossDefenseAuthoring.Apply();BossDefenseAuthoring.AppendOwnedPlayerAdapter();BossVisualExpansionAuthoring.Apply();
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/BossFight.unity",OpenSceneMode.Single);SessionState.SetBool(Armed,true);EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;EditorApplication.EnterPlaymode();
 }
 static void State(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode)return;SessionState.SetBool(Armed,false);EditorApplication.playModeStateChanged-=State;EditorApplication.delayCall+=()=>BossDefenseNativeRegression.RunInCurrentPlayScene();}
}
#endif
