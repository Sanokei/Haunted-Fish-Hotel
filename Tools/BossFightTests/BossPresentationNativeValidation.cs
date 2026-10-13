#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HauntedFish.BossFight;
using HauntedFish.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[InitializeOnLoad]
public static class BossPresentationNativeValidation
{
    const string Asset="Assets/Resources/BossFight/BossFightArena.prefab";
    static string Prefix=>System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishBossPresentationOcclusionNative-20261010");
    static int attempts;
    static BossPresentationNativeValidation(){EditorApplication.delayCall+=AutoRun;}
    static void AutoRun()
    {
        if(!File.Exists(Prefix+".request")||File.Exists(Prefix+".txt"))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
        {if(++attempts<3)EditorApplication.delayCall+=AutoRun;return;}
        Run();
    }
    static T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);
    [MenuItem("Tools/Haunted Hotel/Validate native boss presentation (isolated)")]
    public static void Run()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var active=SceneManager.GetActiveScene();var selection=Selection.objects;
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var dirties=Enumerable.Range(0,SceneManager.sceneCount).ToDictionary(i=>SceneManager.GetSceneAt(i).handle,i=>SceneManager.GetSceneAt(i).isDirty);
        var lines=new List<string>();int checks=0;GameObject root=null;
        string folder="Tools/BossFightTests/Evidence/2026-10-10-native-presentation-occlusion";Directory.CreateDirectory(folder);
        void Check(bool value,string label){if(!value)throw new Exception(label);checks++;lines.Add("PASS "+label);}
        try
        {
            root=PrefabUtility.LoadPrefabContents(Asset);
            var arena=root.GetComponentInChildren<BossArenaManager>(true);var view=root.GetComponentInChildren<BossArenaView>(true);
            var characters=root.GetComponentInChildren<BossCharacterPresentation>(true);
            var fish=characters.FishActor.GetComponentInChildren<SpriteRenderer>(true);var ghost=characters.GhostActor.GetComponentInChildren<SpriteRenderer>(true);
            Check(arena&&view&&fish&&ghost,"native authored boss/view/two artwork bindings");
            Check(BossMatch.FishGoalsToWin==3&&BossMatch.GhostGoalsToWin==1,"fish requires three goals; ghost requires one goal");
            Call(arena,"Awake");arena.SetAuthority(true);Check(arena.TryBegin(101,202),"isolated authoritative match admission");arena.SetParticipantsReady(true);
            Check(!arena.ApplyInput(303,Vector2.one),"spectator cannot own paddle");
            var hud=root.GetComponentInChildren<BossHudPresentation>(true);var lives=Get<Text>(hud,"_Lives");
            var storm=root.GetComponentInChildren<LightningEffectManager>(true);var audio=root.GetComponentInChildren<LobbyStormAudio>(true);
            var shadowAdapter=root.GetComponentInChildren<BossOpponentShadow>(true);var shadow=Get<SpriteRenderer>(storm,"_OpponentShadow");
            var roomLights=Get<Light[]>(storm,"_AuthoredRoomLights");var flash=Get<Canvas>(storm,"_FlashCanvas");
            Check(root.GetComponentsInChildren<LightningEffectManager>(true).Length==1&&root.GetComponentsInChildren<LobbyStormAudio>(true).Length==1,"one attic strike owner and one audio owner");
            Check(Get<LightningEffectManager>(audio,"_Lightning")==storm,"attic audio uses same strike owner");
            Check(storm.ValidateAuthoredBindings(out string error),"authored storm bindings valid: "+error);
            Check(Get<LightningEffectManager.WindowPass[]>(storm,"_AuthoredWindowPasses").Length==0,"attic uses room lighting without window projection");
            Check(!storm.gameObject.activeSelf&&!audio.gameObject.activeSelf&&!storm.AmbientActive,"attic storm/audio inactive at rest");
            int start=0,stop=0;storm.AmbientStateChanged+=v=>{if(v)start++;else stop++;};
            int hierarchy=root.GetComponentsInChildren<Transform>(true).Length;
            foreach(var side in new[]{BossViewerSide.Fish,BossViewerSide.Ghost,BossViewerSide.Spectator})
            {
                arena.SetLocalViewer(side==BossViewerSide.Fish?101u:side==BossViewerSide.Ghost?202u:303u,true);
                Check(fish.enabled==(side==BossViewerSide.Ghost)&&ghost.enabled==(side!=BossViewerSide.Ghost),"local own renderer hidden/opponent visible: "+side);
                Check(!view.CurrentCamera.orthographic,"authored perspective camera: "+side);
                Check(lives.text.Contains("1")&&lives.text.Contains("3"),"authored life HUD has one fish/three ghost: "+side);
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(720,1280)})
                {
                    var camera=view.CurrentCamera;var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.scene=root.scene;view.Fit();
                    Check(side==BossViewerSide.Ghost?camera.transform.position.z>0:camera.transform.position.z<0,"camera stands at viewer own end: "+side+" "+size);
                    var focus=Get<Transform>(view,"_Focus");
                    foreach(float x in new[]{-3.65f,3.65f})foreach(float y in new[]{-.4f,.3f})foreach(float z in new[]{-5.8f,5.8f})
                    {
                        var p=camera.WorldToViewportPoint(focus.position+new Vector3(x,y,z));
                        Check(p.z>camera.nearClipPlane&&p.x>=.035f&&p.x<=.965f&&p.y>=.035f&&p.y<=.965f,"full playable board/goal corner in native viewport: "+side+" "+size);
                    }
                    if(side!=BossViewerSide.Spectator)
                    {
                        uint id=side==BossViewerSide.Fish?101u:202u;var paddle=Get<Rigidbody>(arena,side==BossViewerSide.Fish?"_FishPaddle":"_GhostPaddle");
                        float dx=side==BossViewerSide.Fish?.8f:-.8f;
                        Vector3 screen=camera.WorldToScreenPoint(paddle.position+new Vector3(dx,0,0));var before=arena.Puck.position;
                        var axis=arena.PointerAxis(id,new Vector2(screen.x,screen.y),.05f);
                        Check(axis.x*dx>0&&axis.magnitude<=1.001f,"screen-right pointer maps to owned-end world axis: "+side+" "+size);
                        Check(arena.Puck.position==before,"pointer never teleports puck: "+side+" "+size);
                    }
                    var canvas=Get<Canvas>(hud,"_Canvas");canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
                    var old=RenderTexture.active;
                    try
                    {
                        camera.Render();RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                        tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();
                        File.WriteAllBytes(System.IO.Path.Combine(folder,side+"-"+size.x+"x"+size.y+".png"),tex.EncodeToPNG());
                        if(side==BossViewerSide.Spectator)
                        {
                            var visible=tex.GetPixels32();
                            var nearGoal=root.GetComponentsInChildren<BossGoal>(true).OrderBy(g=>g.transform.position.z).First().GetComponent<BoxCollider>();
                            Vector3 lo=nearGoal.transform.TransformPoint(nearGoal.center-nearGoal.size*.5f);
                            Vector3 hi=nearGoal.transform.TransformPoint(nearGoal.center+nearGoal.size*.5f);
                            float minX=size.x,minY=size.y,maxX=0,maxY=0;
                            foreach(float x in new[]{lo.x,hi.x})foreach(float y in new[]{lo.y,hi.y})foreach(float z in new[]{lo.z,hi.z})
                            {var p=camera.WorldToScreenPoint(new Vector3(x,y,z));minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
                            int x0=Mathf.Clamp(Mathf.FloorToInt(minX)-16,0,size.x-1),x1=Mathf.Clamp(Mathf.CeilToInt(maxX)+16,1,size.x);
                            int y0=Mathf.Clamp(Mathf.FloorToInt(minY)-16,0,size.y-1),y1=Mathf.Clamp(Mathf.CeilToInt(maxY)+16,1,size.y);
                            Check(x1-x0>8&&y1-y0>8,"near-goal occlusion ROI has native rendered area: "+size);
                            bool fishVisible=fish.enabled,ghostVisible=ghost.enabled;
                            Color32[] baseline,occluded;
                            try
                            {
                                fish.enabled=false;ghost.enabled=false;camera.Render();RenderTexture.active=rt;
                                tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();baseline=tex.GetPixels32();
                                File.WriteAllBytes(System.IO.Path.Combine(folder,"Spectator-no-art-baseline-"+size.x+"x"+size.y+".png"),tex.EncodeToPNG());
                                fish.enabled=true;ghost.enabled=false;camera.Render();RenderTexture.active=rt;
                                tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();occluded=tex.GetPixels32();
                                File.WriteAllBytes(System.IO.Path.Combine(folder,"Spectator-near-art-positive-control-"+size.x+"x"+size.y+".png"),tex.EncodeToPNG());
                            }
                            finally{fish.enabled=fishVisible;ghost.enabled=ghostVisible;}
                            int Changed(Color32[] a,Color32[] b)
                            {
                                int count=0;for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                                {int i=y*size.x+x;if(Mathf.Abs(a[i].r-b[i].r)>2||Mathf.Abs(a[i].g-b[i].g)>2||Mathf.Abs(a[i].b-b[i].b)>2)count++;}
                                return count;
                            }
                            int tolerance=Mathf.Max(4,(x1-x0)*(y1-y0)/1000),covered=Changed(visible,baseline),positive=Changed(occluded,baseline);
                            lines.Add("OCCLUSION nearGoalROI="+x0+","+y0+","+x1+","+y1+" viewChanged="+covered+" forcedNearFishChanged="+positive+" tolerance="+tolerance);
                            Check(covered<=tolerance,"spectator near goal matches unobstructed rendered baseline: "+size);
                            Check(positive>tolerance,"render check detects the old near-fish goal obstruction: "+size);
                        }
                        UnityEngine.Object.DestroyImmediate(tex);lines.Add("RENDER "+side+" "+size);
                    }
                    finally{RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);}
                    canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                }
                storm.gameObject.SetActive(true);storm.StartAmbient();storm.StartAmbient();
                Check(storm.AmbientActive,"native attic ambient starts: "+side);
                Check(Get<SpriteRenderer>(storm,"_OpponentRenderer")== (side==BossViewerSide.Ghost?fish:ghost),"shared storm selects visible far opponent: "+side);
                Call(storm,"ApplyPulse",1.35f);Call(shadowAdapter,"LateUpdate");
                Check(shadow.enabled&&shadow.sprite==(side==BossViewerSide.Ghost?fish.sprite:ghost.sprite),"one authored shadow copies visible opponent: "+side);
                float wall=side==BossViewerSide.Ghost?-1:1;
                Check(shadow.transform.position.z*wall>5.8f&&shadow.transform.localScale.magnitude>(side==BossViewerSide.Ghost?fish.transform.lossyScale:ghost.transform.lossyScale).magnitude,"enlarged opponent shadow placed beyond far goal: "+side);
                storm.StopAmbient();storm.StopAmbient();audio.StopAudio();storm.gameObject.SetActive(false);audio.gameObject.SetActive(false);
                Check(!storm.AmbientActive&&!storm.Playing&&!shadow.enabled&&!flash.enabled,"native stop restores no active strike/flash/shadow: "+side);
                Check(root.GetComponentsInChildren<Transform>(true).Length==hierarchy,"storm/view creates no hierarchy: "+side);
            }
            Check(start==3&&stop==3,"exactly one native ambient start/stop per entry despite repeated calls");
            arena.SetLocalViewer(101,false);Check(!view.CurrentCamera.gameObject.activeSelf,"boss view disabled on return");
            lines.Add("LIMIT: isolated Edit-mode native API/projection/render checks; no live coordinator, actual strike coroutine, DSP audition or Play-mode collision test.");
        }
        catch(Exception error){lines.Add("FAIL "+error);}
        finally
        {
            if(root)PrefabUtility.UnloadPrefabContents(root);
            try
            {
                Check(active==SceneManager.GetActiveScene(),"active user scene preserved");
                Check(setup.Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive).SequenceEqual(EditorSceneManager.GetSceneManagerSetup().Select(s=>s.path+"|"+s.isLoaded+"|"+s.isActive)),"loaded user scenes preserved");
                Check(Enumerable.Range(0,SceneManager.sceneCount).All(i=>dirties.TryGetValue(SceneManager.GetSceneAt(i).handle,out bool dirty)&&dirty==SceneManager.GetSceneAt(i).isDirty),"user dirty flags preserved");
                Check(selection.SequenceEqual(Selection.objects),"user selection preserved");
            }
            catch(Exception error){lines.Add("FAIL cleanup "+error);}
            lines.Insert(0,"NATIVE ISOLATED BOSS PRESENTATION checks="+checks);
            File.WriteAllLines(Prefix+".txt",lines);File.WriteAllLines(System.IO.Path.Combine(folder,"native-results.txt"),lines);
        }
    }
}
#endif