#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HauntedFish.Theatre;
using HauntedFish.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class TheatreNativeAuthoring
{
    const string Folder="Assets/Resources/Theatre",ScenePath="Assets/Scenes/theatre.unity";
    static void Ref(UnityEngine.Object owner,string field,UnityEngine.Object value){var s=new SerializedObject(owner);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static GameObject Group(string name,Transform parent=null){var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);go.AddComponent<PrettyHierarchy.PrettyObject>();return go;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    static Image Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color){var r=Rect(name,parent,min,max);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
    static Text Label(string name,Transform parent,Vector2 min,Vector2 max,string value,int size)
    {var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
    static void Button(string name,Transform parent,float x,string label,UnityAction action)
    {var panel=Panel(name,parent,new Vector2(x,.895f),new Vector2(x+.14f,.947f),new Color(.08f,.1f,.12f,.95f));panel.raycastTarget=true;var b=panel.gameObject.AddComponent<Button>();b.targetGraphic=panel;UnityEventTools.AddPersistentListener(b.onClick,action);Label("Label",panel.transform,Vector2.zero,Vector2.one,label,24);}
    static Sprite Source(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()??throw new Exception("Missing imported source sprite "+path);
    static SpriteRenderer Silhouette(string name,Transform parent,Sprite sprite,Vector3 position,float height,Material material)
    {var go=Group(name,parent);go.transform.localPosition=position;var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.color=Color.black;r.sharedMaterial=material;r.sortingOrder=100;r.transform.localScale=Vector3.one*(height/Mathf.Max(.01f,sprite.bounds.size.y));return r;}
    [MenuItem("Tools/Haunted Hotel/Author theatre scene (explicit)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Requires exclusive idle Unity window.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty user scenes before native authoring.");
        if(File.Exists(ScenePath))throw new Exception("Theatre scene already exists; use scoped native integration instead of replacing it.");
        var before=EditorSceneManager.GetSceneManagerSetup();var selection=Selection.objects;Scene scene=default;
        try
        {
            if(!AssetDatabase.IsValidFolder(Folder)){if(!AssetDatabase.IsValidFolder("Assets/Resources"))throw new Exception("Missing Resources");AssetDatabase.CreateFolder("Assets/Resources","Theatre");}
            var fish=Source("Assets/Art/HotelFish/FISH-right.png");var ghost=Source("Assets/Art/player_ghost.png");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HotelNetworkPlayer.prefab");var material=player.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite).sharedMaterial;
            var fishClip=AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/TutorialVideos/FishTutorialProduction.mp4");var ghostClip=AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/TutorialVideos/GhostTutorialProduction.mp4");if(!fishClip||!ghostClip)throw new Exception("Recorder-owned production clips are missing.");
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var root=Group("THEATRE — local tutorial visit");root.transform.position=new Vector3(3000,0,0);var p=root.AddComponent<TheatrePresentation>();
            var view=Group("Side-on theatre camera",root.transform);view.transform.localPosition=new Vector3(0,10,-30);var camera=view.AddComponent<Camera>();camera.depth=100;camera.orthographic=true;camera.orthographicSize=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.02f,.025f);camera.nearClipPlane=.1f;camera.farClipPlane=100;view.AddComponent<HotelViewCamera>();Ref(p,"_Camera",camera);
            var canvasGo=Group("AUTHORED — luminous screen and playback UI",root.transform);var canvas=canvasGo.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=20;canvasGo.AddComponent<GraphicRaycaster>();var scaler=canvasGo.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Panel("Screen surround",canvas.transform,new Vector2(.045f,.20f),new Vector2(.955f,.96f),new Color(.2f,.23f,.25f));
            var screen=Rect("Both-role tutorial screen",canvas.transform,new Vector2(.06f,.245f),new Vector2(.94f,.89f));var raw=screen.gameObject.AddComponent<RawImage>();raw.raycastTarget=false;var fit=screen.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9;
            string target=Folder+"/TutorialScreen.renderTexture";if(AssetDatabase.LoadAssetAtPath<RenderTexture>(target))throw new Exception("Existing theatre target asset");var texture=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32){name="Both tutorials — authored screen target"};AssetDatabase.CreateAsset(texture,target);raw.texture=texture;
            var videoGo=Group("Production tutorial projector",root.transform);var video=videoGo.AddComponent<VideoPlayer>();var audio=videoGo.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;video.playOnAwake=false;video.renderMode=VideoRenderMode.RenderTexture;video.targetTexture=texture;video.audioOutputMode=VideoAudioOutputMode.AudioSource;video.SetTargetAudioSource(0,audio);video.skipOnDrop=true;Ref(p,"_Video",video);Ref(p,"_FishVideo",fishClip);Ref(p,"_GhostVideo",ghostClip);
            var status=Label("Playback status",canvas.transform,new Vector2(.355f,.89f),new Vector2(.645f,.95f),"PLAYER + GHOST TUTORIALS",28);Ref(p,"_Status",status);
            Button("Player tutorial",canvas.transform,.08f,"PLAYER",p.PlayFish);Button("Ghost tutorial",canvas.transform,.23f,"GHOST",p.PlayGhost);
            var countdown=Rect("Centered 3–2–1 countdown",canvas.transform,new Vector2(.43f,.44f),new Vector2(.57f,.69f));var ring=Panel("Countdown ring",countdown,Vector2.zero,Vector2.one,new Color(1,1,1,.8f));ring.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");ring.type=Image.Type.Filled;ring.fillMethod=Image.FillMethod.Radial360;var number=Label("Countdown numeral",countdown,new Vector2(.1f,.1f),new Vector2(.9f,.9f),"3",90);number.color=Color.black;Ref(p,"_CountdownRoot",countdown.gameObject);Ref(p,"_CountdownRing",ring);Ref(p,"_Countdown",number);countdown.gameObject.SetActive(false);
            var seats=Group("FOREGROUND — black seatbacks and seated audience",root.transform);Panel("Black seatbacks",canvas.transform,Vector2.zero,new Vector2(1,.05f),Color.black);
            Silhouette("Seated fish audience left",seats.transform,fish,new Vector3(-8,2.8f,-24),4,material);Silhouette("Seated ghost audience right",seats.transform,ghost,new Vector3(7.5f,2.8f,-24),4.5f,material);
            var walker=Group("OWN CHARACTER — horizontal A D lane",root.transform);walker.transform.localPosition=new Vector3(0,2.7f,-25);var own=Silhouette("Owned black fish silhouette",walker.transform,fish,Vector3.zero,4.2f,material);Ref(p,"_Walker",walker.transform);Ref(p,"_OwnSilhouette",own);
            var exit=Group("EXIT — far left to Lobby archway",root.transform);exit.transform.localPosition=new Vector3(-9,2.7f,-25);var zone=exit.AddComponent<BoxCollider>();zone.isTrigger=true;zone.size=new Vector3(1,7,3);
            Label("A D and exit",canvas.transform,new Vector2(.02f,.055f),new Vector2(.32f,.105f),"A / D    LEFT: LOBBY EXIT",25);
            if(!p.ValidateAuthoredBindings(out string error))throw new Exception(error);
            if(root.GetComponentsInChildren<HotelPlayer>(true).Length!=0)throw new Exception("Theatre must not author a duplicate network player.");
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/TheatrePresentation.prefab");
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new Exception("Native theatre scene save failed");
            var builds=EditorBuildSettings.scenes.ToList();if(!builds.Any(s=>s.path==ScenePath))builds.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=builds.ToArray();
            AssetDatabase.SaveAssets();Debug.Log("THEATRE native scene authored; player prefab component addition remains separate explicit operation.");
        }
        finally
        {if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(before.Length>0)EditorSceneManager.RestoreSceneManagerSetup(before);Selection.objects=selection;}
    }
    // Parent/settings owner approves this exact asset before calling separately.
    public static void AddPlayerReturnComponent()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Requires exclusive idle Unity grant.");
        const string path="Assets/Resources/HotelNetworkPlayer.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(root.GetComponent<TheatreReturnRequest>())return;
            var components=root.GetComponentsInChildren<Component>(true).Where(c=>c).ToArray();var original=components.Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
            root.AddComponent<TheatreReturnRequest>();
            for(int i=0;i<components.Length;i++)if(EditorJsonUtility.ToJson(components[i])!=original[i])throw new Exception("Existing player component changed; abort addition.");
            string backup=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HauntedFishPlayer.before-theatre-return.prefab");if(!File.Exists(backup))File.Copy(path,backup);PrefabUtility.SaveAsPrefabAsset(root,path,out bool success);if(!success)throw new Exception("Native player component save failed");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif