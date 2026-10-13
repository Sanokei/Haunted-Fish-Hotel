using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HauntedFish.BossFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Uses native scene serialization only, after native prefab validation. No raw YAML edits.
[InitializeOnLoad]
public static class BossPrefabSceneIntegration
{
    const string Source="Assets/Scenes/BossFight.unity",Target="Assets/Resources/BossFight/BossFightArena.prefab";
    static string Prefix=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossPrefabSceneIntegration-20261010");
    static string Author=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossPerspectiveAuthoring-20261010.txt");
    static int attempts;
    static BossPrefabSceneIntegration(){EditorApplication.delayCall+=AutoRun;}
    static void AutoRun()
    {
        if(!File.Exists(Prefix+".request")||File.Exists(Prefix+".txt"))return;
        if(!File.Exists(Author)||!File.Exists(Target))
        {if(++attempts<3)EditorApplication.delayCall+=AutoRun;return;}
        Run();
    }
    [MenuItem("Tools/Haunted Hotel/Integrate validated boss arena prefab")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
        {File.WriteAllText(Prefix+".txt","SKIPPED: requires idle Edit mode; no user scene modified.");return;}
        var already=SceneManager.GetSceneByPath(Source);
        if(already.IsValid()&&already.isLoaded)
        {File.WriteAllText(Prefix+".txt","SKIPPED: BossFight scene is already open; preserving user scene and unsaved work. No source changed.");return;}
        var active=SceneManager.GetActiveScene();var selection=Selection.objects;
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var dirty=Enumerable.Range(0,SceneManager.sceneCount).ToDictionary(i=>SceneManager.GetSceneAt(i).handle,i=>SceneManager.GetSceneAt(i).isDirty);
        var lines=new System.Collections.Generic.List<string>();int checks=0;Scene working=default;
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks++;lines.Add("PASS "+label);}
        try
        {
            string hash=Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(Source)));
            var authored=File.ReadAllText(Author);
            Check(authored.Contains("SOURCE_SHA256 "+hash)&&!authored.Contains("FAIL "),"native prefab was authored from current saved boss scene and passed bindings");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Target);
            Check(prefab&&PrefabUtility.IsPartOfPrefabAsset(prefab),"native prefab asset loaded");
            var arena=prefab.GetComponentInChildren<BossArenaManager>(true);var view=prefab.GetComponentInChildren<BossArenaView>(true);var hud=prefab.GetComponentInChildren<BossHudPresentation>(true);
            Check(arena&&view&&hud,"serialized manager/view/HUD present");
            var fields=new SerializedObject(view);
            foreach(string name in new[]{"_Fish","_Ghost","_Spectator"}){var camera=(Camera)fields.FindProperty(name).objectReferenceValue;Check(camera&&!camera.orthographic,"authored perspective rig "+name);}
            File.Copy(Source,Prefix+".before.unity",true);
            // Additive opening never replaces or saves an existing user scene. Refuse if active changes.
            working=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
            Check(SceneManager.GetActiveScene()==active,"additive saved-boss load preserves active user scene");
            foreach(var root in working.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,working);
            Check(instance&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance)==Target,"native boss prefab instance connected");
            var live=instance.GetComponentInChildren<BossArenaManager>(true);var data=new SerializedObject(live);
            foreach(string name in new[]{"_Puck","_FishPaddle","_GhostPaddle","_Views","_Hud","_Reactions"})Check(data.FindProperty(name).objectReferenceValue,"native scene instance binding "+name);
            Check(EditorSceneManager.SaveScene(working,Source),"native Unity scene serialization saved BossFight prefab instance");
            lines.Add("SOURCE now contains one authored arena prefab instance; other user scene files not saved.");
        }
        catch(Exception error){lines.Add("FAIL "+error);}
        finally
        {
            if(working.IsValid()&&working.isLoaded)EditorSceneManager.CloseScene(working,true);
            try
            {
                Check(SceneManager.GetActiveScene()==active,"active user scene preserved after native integration");
                var after=EditorSceneManager.GetSceneManagerSetup();
                Check(setup.Length==after.Length&&setup.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive).SequenceEqual(after.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)),"original loaded user scene setup preserved");
                Check(Enumerable.Range(0,SceneManager.sceneCount).All(i=>dirty.TryGetValue(SceneManager.GetSceneAt(i).handle,out bool value)&&value==SceneManager.GetSceneAt(i).isDirty),"other user scene dirty flags preserved");
                Check(selection.SequenceEqual(Selection.objects),"user selection preserved");
            }
            catch(Exception error){lines.Add("FAIL cleanup "+error);}
            lines.Insert(0,"NATIVE PREFAB SCENE INTEGRATION checks="+checks);File.WriteAllLines(Prefix+".txt",lines);
        }
    }
}