#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using HauntedFish.Multiplayer;
using Object=UnityEngine.Object;
public static class GameTutorialPlaybackBatch
{
    const string Key="GameTutorialPlaybackBatch.Armed";
    const string Evidence="C:/Users/wkeif/Documents/Codex/2026-10-09/task-3/TutorialNativePlayback";
    [Serializable] sealed class SceneRecord { public string Path; public bool Loaded,Active; }
    [Serializable] sealed class Journal { public SceneRecord[] Scenes; public bool Background; }
    static RoundHowToPage page; static int role;
    static void Owned()
    {
        if(!Application.isBatchMode||!Environment.GetCommandLineArgs().Contains("--game-tutorial-playback")||!Application.dataPath.Replace('\\','/').EndsWith("/Haunted Fish Hotel/Assets",StringComparison.Ordinal))
            throw new InvalidOperationException("Explicit owned --game-tutorial-playback batch required.");
    }
    public static void Run()
    {
        Owned(); if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Refusing existing Play session.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Refusing dirty scene.");
        foreach(var name in new[]{"Fish","Ghost"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/"+name+"RoundHowTo.prefab");
            if(!prefab||!prefab.GetComponent<VideoPlayer>().clip)throw new InvalidOperationException("Native bound "+name+" clip missing.");
        }
        Directory.CreateDirectory(Evidence);
        var journal=new Journal{Scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SceneRecord{Path=s.path,Loaded=s.isLoaded,Active=s.isActive}).ToArray(),Background=Application.runInBackground};
        SessionState.SetString(Key+".Journal",JsonUtility.ToJson(journal));SessionState.SetString(Key+".Error","");SessionState.SetBool(Key+".Exit",false);SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+180);
        SessionState.SetBool(Key,true);Attach();EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void ResumeExplicitCommand(){if(!SessionState.GetBool(Key,false))return;Owned();Attach();} // Resumes only an explicit armed command; never starts Play.
    static void Attach(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            Owned();if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))throw new TimeoutException("Native playback timeout, role="+role);
            if(SessionState.GetBool(Key+".Exit",false)){if(!EditorApplication.isPlayingOrWillChangePlaymode)Finish();return;}
            if(!EditorApplication.isPlaying)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
            string name=role==0?"Fish":"Ghost";
            if(!page)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/"+name+"RoundHowTo.prefab");page=Object.Instantiate(prefab).GetComponent<RoundHowToPage>();
                if(!page.HasRecording)throw new InvalidOperationException("Actual authored page lost clip binding.");
                page.Begin(role==0?1u:0u,0u,()=>{throw new InvalidOperationException("Playback test did not request Ready.");});
            }
            var video=page.GetComponent<VideoPlayer>();var raw=page.GetComponentInChildren<RawImage>(true);
            if(!video.isPrepared||video.frame<3||video.time<.3)return;
            if(!video.isPlaying||!video.isLooping||video.playOnAwake||!video.targetTexture||raw.texture!=video.targetTexture||!video.targetTexture.IsCreated())throw new InvalidOperationException("Native authored video playback state/bindings invalid.");
            var fields=new SerializedObject(page);var missing=(GameObject)fields.FindProperty("_MissingRecording").objectReferenceValue;
            if(missing.activeSelf)throw new InvalidOperationException("Missing-recording placeholder still visible after actual Begin.");
            var active=RenderTexture.active;var image=new Texture2D(640,360,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=video.targetTexture;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();
                var pixels=image.GetPixels32();var first=pixels[0];bool varied=false;for(int i=0;i<pixels.Length;i+=173)if(Math.Abs(pixels[i].r-first.r)+Math.Abs(pixels[i].g-first.g)+Math.Abs(pixels[i].b-first.b)>30){varied=true;break;}
                if(!varied)throw new InvalidOperationException("Native decoded role texture appears blank.");File.WriteAllBytes(Evidence+"/"+name+"-decoded.png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=active;Object.DestroyImmediate(image);}
            File.WriteAllText(Evidence+"/"+name+"-playback.txt","PASS actual authored RoundHowToPage.Begin played bound native VideoClip; prepared="+video.isPrepared+" playing="+video.isPlaying+" decodedFrame="+video.frame+" time="+video.time+" loop="+video.isLooping+" target="+video.targetTexture.width+"x"+video.targetTexture.height+" matches RawImage; missing-recording placeholder hidden; nonblank decoded texture captured. Clip="+video.clip.name);
            page.Cancel();Object.Destroy(page.gameObject);page=null;role++;
            if(role==2){SessionState.SetBool(Key+".Exit",true);EditorApplication.ExitPlaymode();}
        }
        catch(Exception error)
        {
            SessionState.SetString(Key+".Error",error.ToString());if(page){page.Cancel();Object.Destroy(page.gameObject);}page=null;
            if(EditorApplication.isPlaying){SessionState.SetBool(Key+".Exit",true);EditorApplication.ExitPlaymode();}else Finish();
        }
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;string error=SessionState.GetString(Key+".Error","");
        try
        {
            var journal=JsonUtility.FromJson<Journal>(SessionState.GetString(Key+".Journal",""));Application.runInBackground=journal.Background;
            if(journal.Scenes.Length==0||journal.Scenes.All(s=>string.IsNullOrEmpty(s.Path)))EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(journal.Scenes.Select(s=>new SceneSetup{path=s.Path,isLoaded=s.Loaded,isActive=s.Active}).ToArray());
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            GameIntroPrefabValidationTask.Run();File.WriteAllText(Evidence+"/result.txt","PASS both actual authored role pages played their distinct bound native clips into their assigned RawImage targets. Decoded nonblank frames saved. Scene setup restored; no scene or production input setting saved. Human UX, audible storm and two-client networking remain unverified.");SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }
        catch(Exception failed){File.WriteAllText(Evidence+"/result.txt","FAIL "+failed);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
#endif