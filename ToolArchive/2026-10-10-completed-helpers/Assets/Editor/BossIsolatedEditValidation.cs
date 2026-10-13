using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using HauntedFish.BossFight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in, once-only Edit-mode checks. No scene switches, physics simulation, Play mode,
// input-device events, live player/menu mutation, asset saves or selection changes.
[InitializeOnLoad]
public static class BossIsolatedEditValidation
{
    static string Prefix => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HauntedFishBossIsolatedChecks-20261010");
    static BossIsolatedEditValidation() { EditorApplication.delayCall += AutoRun; }
    static void AutoRun()
    {
        if (!File.Exists(Prefix + ".request") || File.Exists(Prefix + ".txt")) return;
        Run();
    }
    [MenuItem("Tools/Haunted Hotel/Validate isolated boss input and practice")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            File.WriteAllText(Prefix + ".txt", "SKIPPED: editor is playing, compiling or updating. No scene or input state changed. Use Tools/Haunted Hotel/Validate isolated boss input and practice in idle Edit mode.");
            return;
        }
        var active = SceneManager.GetActiveScene();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var selection = Selection.objects;
        var dirty = new Dictionary<int,bool>();
        for (int i=0;i<SceneManager.sceneCount;i++) dirty[SceneManager.GetSceneAt(i).handle]=SceneManager.GetSceneAt(i).isDirty;
        var lines = new List<string>(); int count=0;
        Scene preview=default;
        var owned = new List<GameObject>();
        void Check(bool ok,string label) { if (!ok) throw new InvalidOperationException(label); count++; lines.Add("PASS " + label); }
        void Field(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        GameObject Object(string name,params Type[] types)
        {
            var obj=EditorUtility.CreateGameObjectWithHideFlags(name,HideFlags.HideAndDontSave,types);
            owned.Add(obj); SceneManager.MoveGameObjectToScene(obj,preview); return obj;
        }
        try
        {
            preview=EditorSceneManager.NewPreviewScene();
            var camera=Object("Isolated boss projection camera",typeof(Camera)).GetComponent<Camera>();
            camera.enabled=false; camera.orthographic=true; camera.orthographicSize=8.4f;
            camera.pixelRect=new Rect(0,0,800,600); camera.transform.position=new Vector3(1000,12,-9);
            camera.transform.rotation=Quaternion.Euler(50,0,0);
            var puck=Object("Isolated cue ball",typeof(Rigidbody)).GetComponent<Rigidbody>();
            var fish=Object("Isolated fish paddle",typeof(Rigidbody)).GetComponent<Rigidbody>();
            var ghost=Object("Isolated ghost paddle",typeof(Rigidbody)).GetComponent<Rigidbody>();
            var arena=Object("Isolated boss manager",typeof(BossArenaManager)).GetComponent<BossArenaManager>();
            Field(arena,"_Camera",camera); Field(arena,"_Puck",puck); Field(arena,"_FishPaddle",fish); Field(arena,"_GhostPaddle",ghost);
            arena.SetAuthority(false); int results=0,snapshots=0;
            arena.Completed += _ => results++; arena.SnapshotChanged += _ => snapshots++;
            arena.BeginEditorPractice();
            Check(arena.EditorPracticing&&!arena.Active&&arena.FishId==0&&arena.GhostId==0,"practice has no network contestants or active match");
            Check(!arena.RegisterGoal(true)&&results==0&&snapshots==0,"practice goals award and publish nothing");
            Field(arena,"_NextServe",0f);
            typeof(BossArenaManager).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(arena,null);
            Check(!puck.isKinematic&&puck.linearVelocity.sqrMagnitude>0,"native practice Rigidbody receives physical serve velocity");
            var screen=camera.WorldToScreenPoint(new Vector3(1002,1.7f,-2)); var before=puck.position;
            var axis=arena.EditorPointerAxis(new Vector2(screen.x,screen.y));
            Check(axis.x>0&&axis.y>0&&axis.magnitude<=1.001f,"native camera ray projects practice pointer to bounded paddle axis");
            Check(puck.position==before,"pointer projection leaves native puck position unchanged");
            var velocity=puck.linearVelocity; arena.SetEditorPracticeInput(Vector2.one,true);
            Check(puck.isKinematic,"blocked practice input freezes native puck");
            arena.SetEditorPracticeInput(Vector2.zero,false);
            Check(!puck.isKinematic&&Vector3.Distance(puck.linearVelocity,velocity)<.0001f,"practice resume restores native puck velocity");
            arena.StopEditorPractice();
            Check(!arena.EditorPracticing&&!arena.Active&&puck.isKinematic&&results==0&&snapshots==0,"practice exit has no authoritative result or snapshot");
            arena.SetAuthority(true); Check(arena.TryBegin(11,22),"real match can begin after practice exits"); arena.SetParticipantsReady(true);
            var fishAxis=arena.PointerAxis(11,new Vector2(screen.x,screen.y),.05f);
            Check(fishAxis.x>0&&fishAxis.magnitude<=1.001f,"native participant camera projection works");
            Check(arena.PointerAxis(33,new Vector2(screen.x,screen.y),.05f)==Vector2.zero,"native spectator pointer denied");
            Check(arena.PointerAxis(11,new Vector2(float.NaN,0),.05f)==Vector2.zero,"native invalid pointer denied");
            Check(!BossPaddleInputPolicy.InputAllowed(true,1),"same menu focus gate blocks production and practice controls");
        }
        catch(Exception error) { lines.Add("FAIL "+error); }
        finally
        {
            for(int i=owned.Count-1;i>=0;i--) if(owned[i]) UnityEngine.Object.DestroyImmediate(owned[i]);
            if(preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            try
            {
                Check(SceneManager.GetActiveScene()==active,"active user scene preserved");
                var after=EditorSceneManager.GetSceneManagerSetup();
                bool same=setup.Length==after.Length;
                for(int i=0;same&&i<setup.Length;i++) same=setup[i].path==after[i].path&&setup[i].isLoaded==after[i].isLoaded&&setup[i].isActive==after[i].isActive;
                Check(same,"loaded user scenes preserved");
                bool clean=true; for(int i=0;i<SceneManager.sceneCount;i++) {var scene=SceneManager.GetSceneAt(i);clean&=dirty.TryGetValue(scene.handle,out bool value)&&value==scene.isDirty;}
                Check(clean,"user scene dirty flags preserved");
                bool selected=selection.Length==Selection.objects.Length; for(int i=0;selected&&i<selection.Length;i++) selected=selection[i]==Selection.objects[i];
                Check(selected,"user selection preserved");
            }
            catch(Exception error) { lines.Add("FAIL cleanup "+error); }
            lines.Insert(0,"ISOLATED EDIT MODE checks="+count+"; no Physics.Simulate or Play-mode runner executed.");
            File.WriteAllLines(Prefix+".txt",lines);
        }
    }
}